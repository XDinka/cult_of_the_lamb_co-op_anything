/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Both players get the host's map between floors and go where the host picks
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using HarmonyLib;
using I2.Loc;
using Lamb.UI;
using Map;
using MMBiomeGeneration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// Keeps the map between floors the same for both players.
    /// </summary>
    /// <remarks>
    /// The game builds the map of a run (which floors, rewards and modifiers come next) the first
    /// time it is shown, from a seed and from the player's save: objectives, unlocks and inventory
    /// decide some of the nodes, so two saves never build the same map. The host builds theirs as
    /// soon as the first room of the run is up and sends it node by node; the joining player's game
    /// takes it in place of its own. Which way to go is the host's to pick, as with dialog answers:
    /// the joining player sees the map and waits, and the host's pick is taken for them, with the
    /// node as the host had it (a shuffle or a curse on the host's side may have changed it). Only
    /// when the host isn't in the run does the joining player pick alone.
    /// </remarks>
    internal static class MapSync
    {
        private enum Step : byte
        {
            /* The host's map: run, how many maps the run had before, the nodes */
            Layout = 1,
            /* The host went to a node: run, map, step on the path, the node */
            Pick = 2
        }

        private sealed class NodeData
        {
            public int X, Y, Type, Location;
            public string Blueprint, Modifier;
            public Vector2 Position;
            public bool Hidden, CanBeHidden;
            public List<Point> Incoming = new(), Outgoing = new();
        }

        private static readonly PropertyInfo CurrentMapProperty = AccessTools.Property(typeof(MapManager), nameof(MapManager.CurrentMap));
        private static readonly FieldInfo GeneratorNodes = AccessTools.Field(typeof(MapGenerator), "nodes");

        /* Maps built in this run, on our side */
        private static int MapRun, MapCount;

        /* Host: what we sent, again for a player who (re)joins */
        private static byte[] SentLayout;
        private static readonly List<byte[]> SentPicks = new();

        /* Joining player: the host's maps and picks of the run */
        private static int HostRun;
        private static readonly Dictionary<int, List<NodeData>> HostLayouts = new();
        private static readonly Dictionary<(int Map, int Step), NodeData> HostPicks = new();

        /* Joining player: the map on screen comes from the host */
        private static bool Following;
        private static UIAdventureMapOverlayController OpenMap;
        private static bool OpenInteractive;
        private static float OpenedAt;
        private static bool ApplyingHost;

        private static Dictionary<string, DungeonModifier> Modifiers;

        /* Counters for the test harness report */
        internal static int LayoutsSent, LayoutsAdopted, PicksSent, PicksApplied, ClicksHeld;

        private static bool IsHost => Plugin.GlobalsInternal.IsServerCreator;

        /// <summary>
        /// The map on screen, while the player can pick on it (for the test harness)
        /// </summary>
        internal static UIAdventureMapOverlayController Open =>
            OpenMap != null && OpenInteractive && OpenMap.gameObject.activeInHierarchy ? OpenMap : null;

        internal static bool FollowingHost => Following;

        public static void Initialize()
        {
            BiomeGenerator.OnBiomeChangeRoom += BuildEarly;
            GameObject Overlay = new("COTLMP Map Overlay");
            UnityEngine.Object.DontDestroyOnLoad(Overlay);
            Overlay.AddComponent<MapOverlay>();
        }

        /// <summary>
        /// The host is in our run and on a dungeon floor, so the way is theirs to pick
        /// </summary>
        private static bool HostInRun =>
            CoopSync.CurrentRun != 0 && CoopSync.PartnerRun == CoopSync.CurrentRun &&
            RoomSync.PartnerLastPlace != null && RoomSync.PartnerLastPlace.Contains("#");

        private static void CountMap()
        {
            if (MapRun != CoopSync.CurrentRun)
            {
                MapRun = CoopSync.CurrentRun;
                MapCount = 0;
                SentLayout = null;
                SentPicks.Clear();
            }
            ++MapCount;
        }

        /* HOST ***************************************************************/

        /// <summary>
        /// The host builds the run's map once its first room is up, so it is with the partner
        /// before they reach the end of the floor. The floor seed and the random state the
        /// game would have had are put back.
        /// </summary>
        private static void BuildEarly()
        {
            if (!CoopSync.InSession || !IsHost || CoopSync.CurrentRun == 0)
                return;
            MapManager Manager = MapManager.Instance;
            BiomeGenerator Generator = BiomeGenerator.Instance;
            if (Manager == null || Generator == null || Manager.MapGenerated || Manager.DungeonConfig == null)
                return;

            int Seed = Generator.Seed;
            UnityEngine.Random.State State = UnityEngine.Random.state;
            try
            {
                Manager.GenerateNewMap();
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Building the map early failed: {e.Message}");
            }
            finally
            {
                Generator.Seed = Seed;
                UnityEngine.Random.state = State;
            }
        }

        private static void SendLayout(Map.Map Built)
        {
            using MemoryStream Stream = new();
            using BinaryWriter Writer = new(Stream);
            Writer.Write((byte)CoopSync.Kind.Map);
            Writer.Write((byte)Step.Layout);
            Writer.Write(CoopSync.CurrentRun);
            Writer.Write(MapCount);
            Writer.Write((short)Built.nodes.Count);
            foreach (Node Item in Built.nodes)
                WriteNode(Writer, Item);
            Writer.Flush();
            SentLayout = Stream.ToArray();
            COTLMP.Network.Network.SendRelay(SentLayout);
            ++LayoutsSent;
            Plugin.Logger?.LogInfo($"[COOP] Sent map {MapCount} of run {CoopSync.CurrentRun}: {Built.nodes.Count} nodes, {SentLayout.Length} bytes");
        }

        private static void SendPick(Node Item, int PathStep)
        {
            using MemoryStream Stream = new();
            using BinaryWriter Writer = new(Stream);
            Writer.Write((byte)CoopSync.Kind.Map);
            Writer.Write((byte)Step.Pick);
            Writer.Write(CoopSync.CurrentRun);
            Writer.Write(MapCount);
            Writer.Write((short)PathStep);
            WriteNode(Writer, Item);
            Writer.Flush();
            byte[] Packet = Stream.ToArray();
            SentPicks.Add(Packet);
            COTLMP.Network.Network.SendRelay(Packet);
            ++PicksSent;
            Plugin.Logger?.LogInfo($"[COOP] Picked node {Item.point.x},{Item.point.y} ({Item.nodeType}) as step {PathStep} of map {MapCount}");
        }

        /// <summary>
        /// A player (re)joined: the map of our run and the way we went on it.
        /// </summary>
        internal static void SendAll()
        {
            if (!IsHost || MapRun != CoopSync.CurrentRun || SentLayout == null)
                return;
            COTLMP.Network.Network.SendRelay(SentLayout);
            foreach (byte[] Packet in SentPicks)
                COTLMP.Network.Network.SendRelay(Packet);
        }

        /* WIRE ***************************************************************/

        private static void WriteNode(BinaryWriter Writer, Node Item)
        {
            Writer.Write((short)Item.point.x);
            Writer.Write((short)Item.point.y);
            Writer.Write((int)Item.nodeType);
            Writer.Write(Item.blueprint != null ? Item.blueprint.name : "");
            Writer.Write(Item.position.x);
            Writer.Write(Item.position.y);
            Writer.Write(Item.Modifier != null ? Item.Modifier.name : "");
            Writer.Write(Item.Hidden);
            Writer.Write(Item.CanBeHidden);
            Writer.Write((int)Item.DungeonLocation);
            Writer.Write((byte)Item.incoming.Count);
            foreach (Point From in Item.incoming)
            {
                Writer.Write((short)From.x);
                Writer.Write((short)From.y);
            }
            Writer.Write((byte)Item.outgoing.Count);
            foreach (Point To in Item.outgoing)
            {
                Writer.Write((short)To.x);
                Writer.Write((short)To.y);
            }
        }

        private static NodeData ReadNode(BinaryReader Reader)
        {
            NodeData Item = new()
            {
                X = Reader.ReadInt16(),
                Y = Reader.ReadInt16(),
                Type = Reader.ReadInt32(),
                Blueprint = Reader.ReadString(),
                Position = new Vector2(Reader.ReadSingle(), Reader.ReadSingle()),
                Modifier = Reader.ReadString(),
                Hidden = Reader.ReadBoolean(),
                CanBeHidden = Reader.ReadBoolean(),
                Location = Reader.ReadInt32()
            };
            int Count = Reader.ReadByte();
            for (int Index = 0; Index < Count; Index++)
                Item.Incoming.Add(new Point(Reader.ReadInt16(), Reader.ReadInt16()));
            Count = Reader.ReadByte();
            for (int Index = 0; Index < Count; Index++)
                Item.Outgoing.Add(new Point(Reader.ReadInt16(), Reader.ReadInt16()));
            return Item;
        }

        /// <summary>
        /// Handles the host's map and picks.
        /// </summary>
        public static void OnRelay(BinaryReader Reader)
        {
            Step Type = (Step)Reader.ReadByte();
            int Run = Reader.ReadInt32();
            int Index = Reader.ReadInt32();
            if (IsHost)
                return;

            if (Run != HostRun)
            {
                HostRun = Run;
                HostLayouts.Clear();
                HostPicks.Clear();
            }

            switch (Type)
            {
                case Step.Layout:
                {
                    int Count = Reader.ReadInt16();
                    List<NodeData> Nodes = new(Count);
                    for (int Item = 0; Item < Count; Item++)
                        Nodes.Add(ReadNode(Reader));
                    HostLayouts[Index] = Nodes;
                    Plugin.Logger?.LogInfo($"[COOP] Got the host's map {Index} of run {Run}: {Count} nodes");
                    break;
                }

                case Step.Pick:
                {
                    int PathStep = Reader.ReadInt16();
                    NodeData Item = ReadNode(Reader);
                    HostPicks[(Index, PathStep)] = Item;
                    Plugin.Logger?.LogInfo($"[COOP] The host went to node {Item.X},{Item.Y} as step {PathStep} of map {Index}");
                    break;
                }
            }
        }

        /* JOINING PLAYER *****************************************************/

        private static Dictionary<string, NodeBlueprint> Blueprints(MapConfig Config)
        {
            Dictionary<string, NodeBlueprint> ByName = new();
            void Add(NodeBlueprint Blueprint)
            {
                while (Blueprint != null && !ByName.ContainsKey(Blueprint.name))
                {
                    ByName[Blueprint.name] = Blueprint;
                    Blueprint = Blueprint.UnlockedBlueprint;
                }
            }

            if (Config.nodeBlueprints != null)
                foreach (NodeBlueprint Blueprint in Config.nodeBlueprints)
                    Add(Blueprint);
            Add(Config.FirstFloorBluePrint);
            Add(Config.SecondFloorBluePrint);
            Add(Config.MiniBossFloorBluePrint);
            Add(Config.TreasureBluePrint);
            Add(Config.LeaderFloorBluePrint);
            if (Config.layers != null)
            {
                foreach (MapLayer Layer in Config.layers)
                {
                    Add(Layer.BluePrint);
                    if (Layer.OtherBluePrints != null)
                        foreach (NodeBlueprint Blueprint in Layer.OtherBluePrints)
                            Add(Blueprint);
                }
            }
            return ByName;
        }

        private static DungeonModifier FindModifier(string Name)
        {
            if (string.IsNullOrEmpty(Name))
                return null;
            Modifiers ??= Resources.LoadAll<DungeonModifier>("Data/Dungeon Modifiers")
                .GroupBy(Modifier => Modifier.name).ToDictionary(Group => Group.Key, Group => Group.First());
            return Modifiers.TryGetValue(Name, out DungeonModifier Found) ? Found : null;
        }

        private static NodeBlueprint FindBlueprint(NodeData Item, Dictionary<string, NodeBlueprint> ByName, MapConfig Config) =>
            ByName.TryGetValue(Item.Blueprint, out NodeBlueprint Found) ? Found : MapManager.GetBlueprint((NodeType)Item.Type, Config);

        /// <summary>
        /// Puts what the host had for a node into ours
        /// </summary>
        private static void Fill(Node Target, NodeData Item, NodeBlueprint Blueprint)
        {
            Target.nodeType = (NodeType)Item.Type;
            Target.blueprint = Blueprint;
            Target.position = Item.Position;
            Target.Modifier = FindModifier(Item.Modifier);
            Target.Hidden = Item.Hidden;
            Target.CanBeHidden = Item.CanBeHidden;
            Target.DungeonLocation = (FollowerLocation)Item.Location;
        }

        /// <summary>
        /// The host's map as one of ours, or null if any of its nodes can't be made here
        /// </summary>
        private static Map.Map Build(List<NodeData> Nodes, MapConfig Config)
        {
            var ByName = Blueprints(Config);
            List<Node> Built = new(Nodes.Count);

            /* The node constructor rolls whether it is hidden: keep the game's random state */
            UnityEngine.Random.State State = UnityEngine.Random.state;
            try
            {
                foreach (NodeData Item in Nodes)
                {
                    NodeBlueprint Blueprint = FindBlueprint(Item, ByName, Config);
                    if (Blueprint == null)
                    {
                        Plugin.Logger?.LogWarning($"[COOP] The host's map has a node we can't make ({Item.Blueprint}, {(NodeType)Item.Type})");
                        return null;
                    }
                    Node Made = new((NodeType)Item.Type, Blueprint, new Point(Item.X, Item.Y));
                    Fill(Made, Item, Blueprint);
                    Made.incoming = new List<Point>(Item.Incoming);
                    Made.outgoing = new List<Point>(Item.Outgoing);
                    Built.Add(Made);
                }
            }
            finally
            {
                UnityEngine.Random.state = State;
            }

            /* The map screen also reads the nodes layer by layer from the generator */
            GeneratorNodes?.SetValue(null, Built.GroupBy(Item => Item.point.y).OrderBy(Layer => Layer.Key)
                .Select(Layer => Layer.OrderBy(Item => Item.point.x).ToList()).ToList());
            return new Map.Map(Config.name, Built, new List<Point>());
        }

        /// <summary>
        /// Takes the host's pick on the map on screen, with the node as the host had it
        /// </summary>
        private static void Apply(UIAdventureMapOverlayController Overlay, NodeData Item)
        {
            Map.Map Current = Overlay.Map;
            Node Target = Current?.GetNode(new Point(Item.X, Item.Y));
            AdventureMapNode Shown = Target != null ? Overlay.NodeFromPoint(Target.point) : null;
            if (Shown == null)
            {
                Plugin.Logger?.LogWarning($"[COOP] The host's pick {Item.X},{Item.Y} isn't on our map, picking alone");
                Following = false;
                return;
            }

            NodeBlueprint Blueprint = FindBlueprint(Item, Blueprints(MapManager.Instance.DungeonConfig), MapManager.Instance.DungeonConfig);
            if (Blueprint != null)
            {
                Fill(Target, Item, Blueprint);
                Shown.Configure(Target);
            }

            int Before = Current.path.Count;
            ApplyingHost = true;
            try
            {
                Overlay.OnNodeChosen(Shown);
                /* A node with a price is only unlocked by the first click */
                if (Current.path.Count == Before)
                    Overlay.OnNodeChosen(Shown);
            }
            finally
            {
                ApplyingHost = false;
            }
            ++PicksApplied;
            OpenMap = null;
            Plugin.Logger?.LogInfo($"[COOP] Went to the host's node {Item.X},{Item.Y} ({(NodeType)Item.Type})");
        }

        /// <summary>
        /// The joining player's map is open and the host has picked: go there
        /// </summary>
        private static void Update()
        {
            UIAdventureMapOverlayController Overlay = Open;
            if (Overlay == null || IsHost || !Following || !CoopSync.InSession || !HostInRun)
                return;
            if (Time.unscaledTime - OpenedAt < 1.5f || Overlay.Map == null)
                return;
            if (HostPicks.TryGetValue((MapCount, Overlay.Map.path.Count), out NodeData Item))
                Apply(Overlay, Item);
        }

        private static bool Waiting =>
            !IsHost && Following && CoopSync.InSession && HostInRun && Open != null && Open.Map != null &&
            !HostPicks.ContainsKey((MapCount, Open.Map.path.Count));

        private sealed class MapOverlay : MonoBehaviour
        {
            private GUIStyle Style;

            private void Update()
            {
                try
                {
                    MapSync.Update();
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogError($"[COOP] Following the host's map failed: {e}");
                    OpenMap = null;
                }
            }

            private void OnGUI()
            {
                if (Event.current.type != EventType.Repaint || !Waiting)
                    return;
                Style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                Style.fontSize = Mathf.RoundToInt(Screen.height / 30f);

                const string Term = "Multiplayer/Map/WaitingForHost";
                string Text = LocalizationManager.GetTranslation(Term);
                if (string.IsNullOrEmpty(Text) || Text == Term)
                    Text = LocalizationManager.CurrentLanguage == "Russian" ? "Ждём, куда поведёт хост" : "Waiting for the host to choose the way";
                Rect Area = new(0f, Screen.height * 0.86f, Screen.width, Screen.height * 0.08f);
                Color Old = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.8f);
                GUI.Label(new Rect(Area.x + 2f, Area.y + 2f, Area.width, Area.height), Text, Style);
                GUI.color = Old;
                GUI.Label(Area, Text, Style);
            }
        }

        /* PATCHES ************************************************************/

        [HarmonyPatch]
        private static class MapSyncPatches
        {
            [HarmonyPatch(typeof(MapManager), nameof(MapManager.GenerateNewMap))]
            [HarmonyPrefix]
            private static bool GenerateNewMap(MapManager __instance, ref Map.Map __result)
            {
                if (!CoopSync.InSession || CoopSync.CurrentRun == 0)
                    return true;
                CountMap();
                if (IsHost)
                    return true;

                Following = false;
                if (HostRun != CoopSync.CurrentRun || !HostLayouts.TryGetValue(MapCount, out List<NodeData> Nodes))
                {
                    Plugin.Logger?.LogWarning($"[COOP] No map {MapCount} from the host for run {CoopSync.CurrentRun}, using our own");
                    return true;
                }

                Map.Map Built;
                try
                {
                    Built = Build(Nodes, __instance.DungeonConfig);
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogWarning($"[COOP] Taking the host's map failed: {e}");
                    Built = null;
                }
                if (Built == null)
                    return true;

                CurrentMapProperty.SetValue(__instance, Built);
                __instance.MapGenerated = true;
                __result = Built;
                Following = true;
                ++LayoutsAdopted;
                Plugin.Logger?.LogInfo($"[COOP] Took the host's map {MapCount}: {Built.nodes.Count} nodes");
                return false;
            }

            [HarmonyPatch(typeof(MapManager), nameof(MapManager.GenerateNewMap))]
            [HarmonyPostfix]
            private static void GeneratedNewMap(Map.Map __result)
            {
                if (!CoopSync.InSession || !IsHost || CoopSync.CurrentRun == 0 || __result == null)
                    return;
                try
                {
                    SendLayout(__result);
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogWarning($"[COOP] Sending the map failed: {e.Message}");
                }
            }

            [HarmonyPatch(typeof(MapManager), nameof(MapManager.AddNodeToPath))]
            [HarmonyPrefix]
            private static void AddNodeToPath(MapManager __instance, Node mapNode)
            {
                if (!CoopSync.InSession || !IsHost || CoopSync.CurrentRun == 0 || mapNode == null || __instance.CurrentMap == null)
                    return;
                try
                {
                    SendPick(mapNode, __instance.CurrentMap.path.Count);
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogWarning($"[COOP] Sending the map pick failed: {e.Message}");
                }
            }

            [HarmonyPatch(typeof(MapManager), nameof(MapManager.ShowMap))]
            [HarmonyPostfix]
            private static void ShowMap(UIAdventureMapOverlayController __result, bool disableInput)
            {
                OpenMap = __result;
                OpenInteractive = !disableInput;
                OpenedAt = Time.unscaledTime;
            }

            /// <summary>
            /// The joining player's clicks on the host's map wait for the host's pick
            /// </summary>
            [HarmonyPatch(typeof(UIAdventureMapOverlayController), nameof(UIAdventureMapOverlayController.OnNodeChosen))]
            [HarmonyPrefix]
            private static bool OnNodeChosen(UIAdventureMapOverlayController __instance)
            {
                if (ApplyingHost || IsHost || !Following || !CoopSync.InSession || !HostInRun ||
                    __instance != OpenMap || !OpenInteractive)
                    return true;
                ++ClicksHeld;
                return false;
            }

            /// <summary>
            /// A shuffle on the joining player's side would leave the host's map
            /// </summary>
            [HarmonyPatch(typeof(MapManager), nameof(MapManager.CanShuffle), MethodType.Getter)]
            [HarmonyPostfix]
            private static void CanShuffle(ref bool __result)
            {
                if (__result && !IsHost && Following && CoopSync.InSession && HostInRun)
                    __result = false;
            }
        }
    }
}

/* EOF */
