/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     One cult for both players: the host's world, the friend's hands in it
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP.Debug;
using HarmonyLib;
using MMTools;
using Spine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// Keeps the cult one world while the friend plays on the host's save.
    /// </summary>
    /// <remarks>
    /// As in the game's own co-op there is one world, and the host's game runs it: on the friend's
    /// side game time doesn't run (no follower or structure ticks), the clock is the host's, and
    /// followers walk and animate as the host's do. What either player changes reaches the other:
    /// <list type="bullet">
    /// <item>followers and structures as data: every member the game saves, compared once a second
    /// with what both sides last agreed on, and only the members that changed are sent (absolute
    /// values, so the same change made on both sides lands once);</item>
    /// <item>new and gone followers and structures, as whole objects;</item>
    /// <item>the inventory: the host's is the inventory, the friend sends what their own actions
    /// add or spend.</item>
    /// </list>
    /// </remarks>
    [HarmonyPatch]
    internal static class CultSync
    {
        private enum Item : byte
        {
            Follower = 0,
            Structure = 1
        }

        private enum Op : byte
        {
            /* Some members of an object changed */
            Members = 0,
            /* A whole object, new to the other side */
            Whole = 1,
            /* The object is gone */
            Gone = 2
        }

        private const string BaseScene = "Base Biome 1";

        /* Positions the host's followers stand at, ten times a second as the poses of the puppets */
        private const float PoseEvery = 0.1f;

        /* Members kept per side or streamed apart: where a follower stands */
        private static readonly HashSet<string> OwnMembers = new() { "LastPosition", "SavedFollowerTaskDestination" };

        /* Counters for the test harness report */
        internal static int MembersSent, MembersApplied, WholeSent, WholeApplied, GoneSent, GoneApplied, ItemsSent, ItemsApplied, PosesApplied, TimeApplied;

        /// <summary>
        /// The friend's side: we play in the host's world
        /// </summary>
        internal static bool Mirror => HostSave.Playing && CoopSync.InSession;

        /// <summary>
        /// The host's side, with a friend on our save who has it loaded
        /// </summary>
        private static bool Hosting => Plugin.GlobalsInternal.IsServerCreator && CoopSync.InSession && PartnerReady;

        private static bool PartnerReady;
        private static bool Ready;

        public static void Initialize()
        {
            Plugin.MonoInstance.StartCoroutine(Scan());
            Plugin.MonoInstance.StartCoroutine(SendTime());
            Plugin.MonoInstance.StartCoroutine(SendPoses());
            GameObject Driver = new("COTLMP Cult Sync");
            UnityEngine.Object.DontDestroyOnLoad(Driver);
            Driver.AddComponent<PoseDriver>();
        }

        /// <summary>
        /// The friend loaded the host's save: from now on both sides track the cult
        /// </summary>
        internal static void OnLoaded()
        {
            Reset();
            Ready = true;
            Baseline();
            CoopSync.Send(CoopSync.Kind.CultReady, Writer => Writer.Write((byte)1));
            Plugin.Logger?.LogInfo("[COOP] The cult is the host's now, telling the host");
        }

        /// <summary>
        /// The host: we are about to hand our save over; what changes after it goes out as changes
        /// </summary>
        internal static void OnSaveSent()
        {
            Reset();
            Baseline();
            PartnerReady = false;
        }

        internal static void OnPartnerReady()
        {
            if (!Plugin.GlobalsInternal.IsServerCreator)
                return;
            PartnerReady = true;
            Ready = true;
            Plugin.Logger?.LogInfo("[COOP] The friend has our save loaded, sharing the cult");
        }

        internal static void Reset()
        {
            Ready = PartnerReady = false;
            Agreed.Clear();
            Poses.Clear();
            LastInventory = null;
        }

        /* OBJECTS ************************************************************/

        /// <summary>
        /// What both sides last agreed on, per object: the object packed whole, as the game saves it
        /// </summary>
        private static readonly Dictionary<string, byte[]> Agreed = new();

        private static string Key(Item Kind, int Id) => $"{(char)('F' + (int)Kind * 13)}{Id}";

        private static IEnumerable<(Item Kind, int Id, object Value)> Objects()
        {
            if (DataManager.Instance == null)
                yield break;
            foreach (FollowerInfo Info in DataManager.Instance.Followers.ToList())
            {
                if (Info != null)
                    yield return (Item.Follower, Info.ID, Info);
            }
            foreach (StructuresData Data in StructureManager.StructuresDataAtLocation(FollowerLocation.Base).ToList())
            {
                if (Data != null)
                    yield return (Item.Structure, Data.ID, Data);
            }
        }

        private static object Find(Item Kind, int Id)
        {
            if (Kind == Item.Follower)
                return DataManager.Instance.Followers.Find(Info => Info != null && Info.ID == Id);
            return StructureManager.StructuresDataAtLocation(FollowerLocation.Base).Find(Data => Data != null && Data.ID == Id);
        }

        private static Type TypeOf(Item Kind) => Kind == Item.Follower ? typeof(FollowerInfo) : typeof(StructuresData);

        /* The members the game saves (MessagePack keys), per type */
        private static readonly Dictionary<Type, List<MemberInfo>> MembersOf = new();

        private static List<MemberInfo> Members(Type Of)
        {
            if (MembersOf.TryGetValue(Of, out var Found))
                return Found;
            Found = new List<MemberInfo>();
            foreach (MemberInfo Member in Of.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (OwnMembers.Contains(Member.Name) || !Member.GetCustomAttributes(true).Any(Attribute => Attribute.GetType().Name == "KeyAttribute"))
                    continue;
                if (Member is FieldInfo Field && !Field.IsInitOnly)
                    Found.Add(Member);
                else if (Member is PropertyInfo Property && Property.CanRead && Property.CanWrite && Property.GetIndexParameters().Length == 0)
                    Found.Add(Member);
            }
            MembersOf[Of] = Found;
            return Found;
        }

        private static Type MemberType(MemberInfo Member) => Member is FieldInfo Field ? Field.FieldType : ((PropertyInfo)Member).PropertyType;

        private static object Get(MemberInfo Member, object Of) => Member is FieldInfo Field ? Field.GetValue(Of) : ((PropertyInfo)Member).GetValue(Of);

        private static void Set(MemberInfo Member, object Of, object Value)
        {
            if (Member is FieldInfo Field)
                Field.SetValue(Of, Value);
            else
                ((PropertyInfo)Member).SetValue(Of, Value);
        }

        private static bool Same(byte[] A, byte[] B)
        {
            if (A == null || B == null || A.Length != B.Length)
                return false;
            for (int Index = 0; Index < A.Length; Index++)
            {
                if (A[Index] != B[Index])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Takes the cult as it is as what both sides agree on
        /// </summary>
        private static void Baseline()
        {
            foreach (var (Kind, Id, Value) in Objects())
                Agreed[Key(Kind, Id)] = HostSave.Pack(TypeOf(Kind), Value);
            LastInventory = Inventory();
        }

        /// <summary>
        /// Once a second: what changed in our cult since both sides last agreed goes to the other.
        /// A few objects a frame, so a big cult doesn't stall the game.
        /// </summary>
        private static IEnumerator Scan()
        {
            var Wait = new WaitForSecondsRealtime(1f);
            while (true)
            {
                yield return Wait;
                if (!Ready || !(Mirror || Hosting) || MMTransition.IsPlaying || DataManager.Instance == null)
                    continue;

                HashSet<string> Seen = new();
                int Done = 0;
                foreach (var (Kind, Id, Value) in Objects().ToList())
                {
                    Seen.Add(Key(Kind, Id));
                    try
                    {
                        Compare(Kind, Id, Value);
                    }
                    catch (Exception e)
                    {
                        PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Sharing {Key(Kind, Id)} failed: {e.Message}");
                    }
                    if (++Done % 25 == 0)
                        yield return null;
                }
                if (!Ready)
                    continue;
                try
                {
                    SendGone(Seen);
                    ScanInventory();
                }
                catch (Exception e)
                {
                    PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Sharing the cult failed: {e}");
                }
            }
        }

        private static void Compare(Item Kind, int Id, object Value)
        {
            string Name = Key(Kind, Id);
            byte[] Whole = HostSave.Pack(TypeOf(Kind), Value);

            /* New here: the other side gets all of it */
            if (!Agreed.TryGetValue(Name, out byte[] Was))
            {
                Agreed[Name] = Whole;
                SendWhole(Kind, Id, Whole);
                return;
            }
            if (Same(Was, Whole))
                return;

            /* Which members changed: against the object as last agreed */
            object Old = HostSave.Unpack(TypeOf(Kind), Was);
            List<(string, byte[])> Changed = new();
            foreach (MemberInfo Member in Members(TypeOf(Kind)))
            {
                byte[] Packed = HostSave.Pack(MemberType(Member), Get(Member, Value));
                if (!Same(HostSave.Pack(MemberType(Member), Get(Member, Old)), Packed))
                    Changed.Add((Member.Name, Packed));
            }
            Agreed[Name] = Whole;
            if (Changed.Count == 0)
                return;
            MembersSent += Changed.Count;
            CoopSync.Send(CoopSync.Kind.CultData, Writer =>
            {
                Writer.Write((byte)Op.Members);
                Writer.Write((byte)Kind);
                Writer.Write(Id);
                Writer.Write((ushort)Changed.Count);
                foreach (var (Member, Packed) in Changed)
                {
                    Writer.Write(Member);
                    Writer.Write(Packed.Length);
                    Writer.Write(Packed);
                }
            });
        }

        /// <summary>
        /// Gone here: gone there
        /// </summary>
        private static void SendGone(HashSet<string> Seen)
        {
            foreach (string Name in Agreed.Keys.ToList())
            {
                if (Seen.Contains(Name))
                    continue;
                Agreed.Remove(Name);
                Item Kind = Name[0] == 'F' ? Item.Follower : Item.Structure;
                int Id = int.Parse(Name.Substring(1));
                ++GoneSent;
                CoopSync.Send(CoopSync.Kind.CultData, Writer =>
                {
                    Writer.Write((byte)Op.Gone);
                    Writer.Write((byte)Kind);
                    Writer.Write(Id);
                });
            }
        }

        private static void SendWhole(Item Kind, int Id, byte[] Whole)
        {
            ++WholeSent;
            Vector3 At = Vector3.zero;
            if (Kind == Item.Follower)
            {
                Follower Body = FollowerManager.FindFollowerByID(Id);
                if (Body != null)
                    At = Body.transform.position;
            }
            CoopSync.Send(CoopSync.Kind.CultData, Writer =>
            {
                Writer.Write((byte)Op.Whole);
                Writer.Write((byte)Kind);
                Writer.Write(Id);
                Writer.Write(At.x);
                Writer.Write(At.y);
                Writer.Write(Whole.Length);
                Writer.Write(Whole);
            });
        }

        internal static void OnData(BinaryReader Reader)
        {
            Op Type = (Op)Reader.ReadByte();
            Item Kind = (Item)Reader.ReadByte();
            int Id = Reader.ReadInt32();
            if (!Ready || DataManager.Instance == null)
                return;
            string Name = Key(Kind, Id);

            try
            {
                switch (Type)
                {
                    case Op.Members:
                        ApplyMembers(Kind, Id, Name, Reader);
                        break;
                    case Op.Whole:
                        ApplyWhole(Kind, Id, Name, Reader);
                        break;
                    case Op.Gone:
                        ApplyGone(Kind, Id, Name);
                        break;
                }
            }
            catch (Exception e)
            {
                PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Taking the partner's change of {Name} failed: {e}");
            }
        }

        private static void ApplyMembers(Item Kind, int Id, string Name, BinaryReader Reader)
        {
            object Value = Find(Kind, Id);
            int Count = Reader.ReadUInt16();

            /* What both agree on takes the partner's members only: a change of ours not sent yet stays ours to send */
            object Shared = Agreed.TryGetValue(Name, out byte[] Was) ? HostSave.Unpack(TypeOf(Kind), Was) : null;
            Dictionary<string, MemberInfo> ByName = Members(TypeOf(Kind)).ToDictionary(Member => Member.Name);
            for (int Index = 0; Index < Count; Index++)
            {
                string Member = Reader.ReadString();
                byte[] Packed = Reader.ReadBytes(Reader.ReadInt32());
                if (Value == null || !ByName.TryGetValue(Member, out MemberInfo Info))
                    continue;
                object Taken = HostSave.Unpack(MemberType(Info), Packed);
                Set(Info, Value, Taken);
                if (Shared != null)
                    Set(Info, Shared, HostSave.Unpack(MemberType(Info), Packed));
                ++MembersApplied;
            }
            if (Shared != null)
                Agreed[Name] = HostSave.Pack(TypeOf(Kind), Shared);
            else if (Value != null)
                Agreed[Name] = HostSave.Pack(TypeOf(Kind), Value);
        }

        private static void ApplyWhole(Item Kind, int Id, string Name, BinaryReader Reader)
        {
            Vector3 At = new(Reader.ReadSingle(), Reader.ReadSingle(), 0f);
            byte[] Whole = Reader.ReadBytes(Reader.ReadInt32());
            if (Find(Kind, Id) != null)
                return;

            object Value = HostSave.Unpack(TypeOf(Kind), Whole);
            if (Kind == Item.Follower)
            {
                FollowerInfo Info = (FollowerInfo)Value;
                DataManager.Instance.Followers.Add(Info);
                if (Info.Location == FollowerLocation.Base && SceneManager.GetActiveScene().name == BaseScene)
                    FollowerManager.SpawnFollower(Info, At);
                else
                    FollowerBrain.GetOrCreateBrain(Info);
                Plugin.Logger?.LogInfo($"[COOP] The partner's new follower {Info.Name} ({Id}) joins our cult");
            }
            else
            {
                StructuresData Data = (StructuresData)Value;
                Place(Data);
                if (DataManager.Instance.StructureID < Data.ID)
                    DataManager.Instance.StructureID = Data.ID;
                Plugin.Logger?.LogInfo($"[COOP] The partner built {Data.Type} ({Id})");
            }
            ++WholeApplied;
            Agreed[Name] = HostSave.Pack(TypeOf(Kind), Value);
        }

        /// <summary>
        /// Puts a structure the partner made into our cult as it is, its id and all
        /// (StructureManager.BuildStructure without making a new one)
        /// </summary>
        private static void Place(StructuresData Data)
        {
            StructureManager.AddStructure(FollowerLocation.Base, Data);
            if (!LocationManager.LocationManagers.TryGetValue(FollowerLocation.Base, out var Manager) || Manager == null)
                return;
            if (!Data.PrefabPath.Contains("Assets"))
                Data.PrefabPath = "Assets/" + Data.PrefabPath + ".prefab";
            Addressables_wrapper.InstantiateAsync(Data.PrefabPath, Handle =>
            {
                try
                {
                    GameObject Placed = Manager.PlaceStructure(Data, Handle.Result.gameObject);
                    if (Placed != null)
                        Placed.transform.localScale = new Vector3(Data.Direction, Placed.transform.localScale.y, Placed.transform.localScale.z);
                }
                catch (Exception e)
                {
                    PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Placing the partner's {Data.Type} failed: {e.Message}");
                }
            });
        }

        private static void ApplyGone(Item Kind, int Id, string Name)
        {
            Agreed.Remove(Name);
            if (Kind == Item.Follower)
            {
                Follower Body = FollowerManager.FindFollowerByID(Id);
                FollowerManager.RemoveFollower(Id);
                FollowerManager.RemoveFollowerBrain(Id);
                if (Body != null)
                    UnityEngine.Object.Destroy(Body.gameObject);
                Plugin.Logger?.LogInfo($"[COOP] Follower {Id} left the partner's cult, and ours");
            }
            else
            {
                if (!StructureBrain.TryFindBrainByID(Id, out StructureBrain Brain) || Brain == null)
                    return;
                foreach (Structure Built in Structure.Structures.ToList())
                {
                    if (Built != null && Built.Brain == Brain)
                        UnityEngine.Object.Destroy(Built.gameObject);
                }
                Brain.Remove();
                Plugin.Logger?.LogInfo($"[COOP] The partner's {Brain.Data.Type} ({Id}) is gone, ours too");
            }
            ++GoneApplied;
        }

        /* INVENTORY **********************************************************/

        private static Dictionary<int, int> LastInventory;

        private static Dictionary<int, int> Inventory()
        {
            Dictionary<int, int> Items = new();
            foreach (InventoryItem Entry in DataManager.Instance.items)
            {
                if (Entry != null)
                    Items[Entry.type] = (Items.TryGetValue(Entry.type, out int Count) ? Count : 0) + Entry.quantity;
            }
            return Items;
        }

        /// <summary>
        /// The host: our inventory, whenever it changed. The friend: what changed on our side since
        /// the host's last one (our own actions: their world doesn't run here)
        /// </summary>
        private static void ScanInventory()
        {
            Dictionary<int, int> Now = Inventory();
            if (LastInventory != null && Now.Count == LastInventory.Count && Now.All(Pair => LastInventory.TryGetValue(Pair.Key, out int Was) && Was == Pair.Value))
                return;

            if (Hosting)
            {
                LastInventory = Now;
                CoopSync.Send(CoopSync.Kind.CultItems, Writer =>
                {
                    Writer.Write((byte)1);
                    Writer.Write((ushort)Now.Count);
                    foreach (var Pair in Now)
                    {
                        Writer.Write(Pair.Key);
                        Writer.Write(Pair.Value);
                    }
                });
                return;
            }

            /* The friend's own changes count only in the cult (a run's loot is shared by the run's own sync) */
            if (SceneManager.GetActiveScene().name != BaseScene || LastInventory == null)
            {
                LastInventory = Now;
                return;
            }
            List<(int, int)> Changes = new();
            foreach (int Type in Now.Keys.Union(LastInventory.Keys))
            {
                int Change = (Now.TryGetValue(Type, out int A) ? A : 0) - (LastInventory.TryGetValue(Type, out int B) ? B : 0);
                if (Change != 0)
                    Changes.Add((Type, Change));
            }
            LastInventory = Now;
            if (Changes.Count == 0)
                return;
            ItemsSent += Changes.Count;
            CoopSync.Send(CoopSync.Kind.CultItems, Writer =>
            {
                Writer.Write((byte)0);
                Writer.Write((ushort)Changes.Count);
                foreach (var (Type, Change) in Changes)
                {
                    Writer.Write(Type);
                    Writer.Write(Change);
                }
            });
        }

        internal static void OnItems(BinaryReader Reader)
        {
            bool Whole = Reader.ReadByte() == 1;
            int Count = Reader.ReadUInt16();
            List<(int, int)> Entries = new();
            for (int Index = 0; Index < Count; Index++)
                Entries.Add((Reader.ReadInt32(), Reader.ReadInt32()));
            if (!Ready || DataManager.Instance == null)
                return;

            /* The host's inventory is the inventory */
            if (Whole && Mirror)
            {
                Dictionary<int, int> Ours = Inventory();
                HashSet<int> Listed = new();
                foreach (var (Type, Quantity) in Entries)
                {
                    Listed.Add(Type);
                    int Have = Ours.TryGetValue(Type, out int Held) ? Held : 0;
                    if (Have != Quantity)
                        global::Inventory.ChangeItemQuantity(Type, Quantity - Have);
                }
                foreach (var Pair in Ours)
                {
                    if (!Listed.Contains(Pair.Key) && Pair.Value != 0)
                        global::Inventory.ChangeItemQuantity(Pair.Key, -Pair.Value);
                }
                LastInventory = Inventory();
                ++ItemsApplied;
                return;
            }

            /* The friend's own changes, onto ours */
            if (!Whole && Plugin.GlobalsInternal.IsServerCreator)
            {
                foreach (var (Type, Change) in Entries)
                    global::Inventory.ChangeItemQuantity(Type, Change);
                ItemsApplied += Entries.Count;
            }
        }

        /* TIME ***************************************************************/

        private static readonly MethodInfo StartNewPhase = AccessTools.Method(typeof(TimeManager), "StartNewPhase");

        private static IEnumerator SendTime()
        {
            var Wait = new WaitForSecondsRealtime(1f);
            while (true)
            {
                yield return Wait;
                if (!Hosting || DataManager.Instance == null)
                    continue;
                CoopSync.Send(CoopSync.Kind.CultTime, Writer =>
                {
                    Writer.Write(TimeManager.CurrentDay);
                    Writer.Write((int)TimeManager.CurrentPhase);
                    Writer.Write(TimeManager.CurrentGameTime);
                });
            }
        }

        internal static void OnTime(BinaryReader Reader)
        {
            int Day = Reader.ReadInt32(), Phase = Reader.ReadInt32();
            float Minutes = Reader.ReadSingle();
            if (!Mirror || !Ready || DataManager.Instance == null)
                return;

            /* A new part of the day starts as the game starts it (lighting, sounds, the clock) */
            if ((int)TimeManager.CurrentPhase != Phase && StartNewPhase != null)
            {
                try
                {
                    StartNewPhase.Invoke(null, new object[] { (DayPhase)Phase });
                }
                catch (Exception e)
                {
                    PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Starting the host's part of the day failed: {e.Message}");
                }

                /* What the game did on its own for the new part of the day isn't the friend's doing */
                if (LastInventory != null)
                    LastInventory = Inventory();
            }
            DataManager.Instance.CurrentDayIndex = Day;
            DataManager.Instance.CurrentPhaseIndex = Phase;
            DataManager.Instance.CurrentGameTime = Minutes;
            ++TimeApplied;
        }

        /* FOLLOWERS IN SIGHT *************************************************/

        private struct Pose
        {
            public Vector3 Position;
            public float ScaleX;
            public string Animation;
            public bool Loop;
            public float At;
        }

        private static readonly Dictionary<int, Pose> Poses = new();

        private static IEnumerator SendPoses()
        {
            var Wait = new WaitForSecondsRealtime(PoseEvery);
            while (true)
            {
                yield return Wait;
                if (!Hosting || SceneManager.GetActiveScene().name != BaseScene || MMTransition.IsPlaying)
                    continue;
                List<Follower> Here = FollowerManager.FollowersAtLocation(FollowerLocation.Base);
                if (Here == null || Here.Count == 0)
                    continue;
                try
                {
                    List<Follower> Shown = Here.Where(Body => Body != null && Body.Brain?.Info != null && Body.gameObject.activeInHierarchy).ToList();
                    CoopSync.Send(CoopSync.Kind.CultPoses, Writer =>
                    {
                        Writer.Write((ushort)Shown.Count);
                        foreach (Follower Body in Shown)
                        {
                            TrackEntry Track = Body.Spine?.AnimationState?.GetCurrent(1) ?? Body.Spine?.AnimationState?.GetCurrent(0);
                            Writer.Write(Body.Brain.Info.ID);
                            Writer.Write(Body.transform.position.x);
                            Writer.Write(Body.transform.position.y);
                            Writer.Write(Body.Spine?.Skeleton?.ScaleX ?? 1f);
                            Writer.Write(Track?.Animation?.Name ?? "");
                            Writer.Write(Track?.Loop ?? true);
                        }
                    });
                }
                catch (Exception e)
                {
                    PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Sending the followers' poses failed: {e.Message}");
                }
            }
        }

        internal static void OnPoses(BinaryReader Reader)
        {
            int Count = Reader.ReadUInt16();
            float Now = Time.unscaledTime;
            for (int Index = 0; Index < Count; Index++)
            {
                int Id = Reader.ReadInt32();
                Poses[Id] = new Pose
                {
                    Position = new Vector3(Reader.ReadSingle(), Reader.ReadSingle(), 0f),
                    ScaleX = Reader.ReadSingle(),
                    Animation = Reader.ReadString(),
                    Loop = Reader.ReadBoolean(),
                    At = Now
                };
            }
        }

        /// <summary>
        /// Moves our copies of the host's followers to where the host's stand, doing what they do
        /// </summary>
        private sealed class PoseDriver : MonoBehaviour
        {
            private void LateUpdate()
            {
                if (!Mirror || !Ready || Poses.Count == 0 || SceneManager.GetActiveScene().name != BaseScene)
                    return;
                foreach (var Pair in Poses)
                {
                    Follower Body = FollowerManager.FindFollowerByID(Pair.Key);
                    if (Body == null)
                        continue;
                    Pose Wanted = Pair.Value;
                    Vector3 Position = Body.transform.position;
                    Vector3 Target = new(Wanted.Position.x, Wanted.Position.y, Position.z);
                    Body.transform.position = (Target - Position).sqrMagnitude > 9f ? Target : Vector3.Lerp(Position, Target, Mathf.Clamp01(Time.unscaledDeltaTime * 12f));
                    if (Body.Spine?.Skeleton == null)
                        continue;
                    Body.Spine.Skeleton.ScaleX = Wanted.ScaleX;
                    if (string.IsNullOrEmpty(Wanted.Animation))
                        continue;
                    TrackEntry Current = Body.Spine.AnimationState.GetCurrent(1) ?? Body.Spine.AnimationState.GetCurrent(0);
                    if (Current?.Animation?.Name == Wanted.Animation)
                        continue;
                    Spine.Animation Found = Body.Spine.Skeleton.Data.FindAnimation(Wanted.Animation);
                    if (Found == null)
                        continue;
                    Body.Spine.AnimationState.SetAnimation(Current != null && Current.TrackIndex == 1 ? 1 : 0, Found, Wanted.Loop);
                    ++PosesApplied;
                }
            }
        }

        /* THE FRIEND'S WORLD DOESN'T RUN *************************************/

        [HarmonyPatch(typeof(TimeManager), nameof(TimeManager.Simulate))]
        [HarmonyPrefix]
        private static bool Simulate() => !Mirror;
    }
}

/* EOF */
