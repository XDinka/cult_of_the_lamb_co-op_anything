/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Grass, rocks, pots and other things broken in a dungeon room break for both players
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using HarmonyLib;
using MMBiomeGeneration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// Keeps what is broken in a dungeon room the same for both players.
    /// </summary>
    /// <remarks>
    /// Two kinds of things break in a room. Grass, crystals, spider nests, tiles and the like have
    /// health and die from hits: the partner's hits never land in our game, so what they cut stays
    /// whole here. Rocks, bones and other rubble break when a lamb or an enemy walks or rolls into
    /// them: the partner's puppet and our copies of the enemies don't stand exactly where they do
    /// for the partner, so different rocks break on each side. Whatever breaks in either game is
    /// sent by its name and place; the other game breaks the nearest thing of that name there (it
    /// drops nothing, the loot of the partner's breaks comes over as shared pickups). What broke in
    /// a run is kept, so a room breaks the same way, silently, when the other player walks in later.
    /// </remarks>
    internal static class PropSync
    {
        private enum Kind : byte
        {
            /* Something with health that died */
            Health = 0,
            /* Rubble that something walked into */
            Rubble = 1
        }

        private sealed class Broken
        {
            public Kind Type;
            public string Name;
            public Vector2 At;
            /* The n-th thing of that name in the room, or -1 */
            public int Number = -1;
            public bool Ours;
            /* Broken here during this visit of the room */
            public bool Done;
        }

        /* How far two copies of the same thing may stand apart */
        private const float Near = 0.75f;

        private static readonly FieldInfo Activated = AccessTools.Field(typeof(BreakableObject), "Activated");
        private static readonly MethodInfo CollideRoutine = AccessTools.Method(typeof(BreakableObject), "CollideRoutine");

        /* What broke in the run, by place */
        private static int Run;
        private static readonly Dictionary<string, List<Broken>> ByPlace = new();

        /// <summary>
        /// Where a thing with health stood once its room was up (a hit pot flies before it breaks,
        /// a critter runs about), and which of the things of its name in the room it is
        /// </summary>
        private sealed class Spawn
        {
            public Vector2 At;
            public int Number = -1;
            /* The room it was numbered in: pooled grass comes up again in the next room */
            public string Place;
        }

        private static int NumberedRun;

        private static readonly ConditionalWeakTable<Health, Spawn> Spawns = new();

        /* How many things of each name each room has numbered */
        private static readonly Dictionary<string, Dictionary<string, int>> NumbersByPlace = new();

        private static Vector2 SpawnOf(Health Thing) =>
            Spawns.TryGetValue(Thing, out Spawn Info) ? Info.At : (Vector2)Thing.transform.position;

        private static int NumberOf(Health Thing) =>
            Spawns.TryGetValue(Thing, out Spawn Info) ? Info.Number : -1;

        /// <summary>
        /// Numbers the room's things in the order they came up, the same order in both games
        /// since the room is built from the same seed; a room seen before keeps its numbers
        /// </summary>
        private static void NumberRoom()
        {
            string Place = CoopSync.CurrentPlace() ?? "";
            if (NumberedRun != CoopSync.CurrentRun)
            {
                NumberedRun = CoopSync.CurrentRun;
                NumbersByPlace.Clear();
            }
            if (!NumbersByPlace.TryGetValue(Place, out Dictionary<string, int> Numbers))
                NumbersByPlace[Place] = Numbers = new Dictionary<string, int>();
            List<Health> Fresh = new();
            foreach (Health Thing in Health.neutralTeam)
            {
                if (Thing == null || Thing.isPlayer || !Thing.gameObject.activeInHierarchy)
                    continue;
                if (!Spawns.TryGetValue(Thing, out Spawn Info) || Info.Number < 0 || Info.Place != Place)
                    Fresh.Add(Thing);
            }
            foreach (Health Thing in Fresh)
            {
                string Name = BaseName(Thing.gameObject);
                Numbers.TryGetValue(Name, out int Next);
                Numbers[Name] = Next + 1;
                Spawns.Remove(Thing);
                Spawns.Add(Thing, new Spawn { At = Thing.transform.position, Number = Next, Place = Place });
            }
            if (Fresh.Count > 0 && CoopSync.InSession)
            {
                List<string> Counts = new();
                foreach (var Pair in Numbers)
                    Counts.Add($"{Pair.Key} {Pair.Value}");
                Counts.Sort(StringComparer.Ordinal);
                Plugin.Logger?.LogInfo($"[COOP] Numbered in {Place}: {string.Join(", ", Counts)}");
            }
        }

        private static bool ApplyingRemote;
        private static readonly List<PickUp> RemoteDrops = new();

        /* Counters for the test harness report */
        internal static int Sent, Received, Applied, Missing, AppliedOnEntry, AlreadyBroken;

        public static void Initialize()
        {
            Health.OnDieAny += OnDieAny;
            BiomeGenerator.OnBiomeChangeRoom += () => Plugin.MonoInstance.StartCoroutine(BreakOnEntry());
        }

        private static string BaseName(GameObject Thing)
        {
            string Name = Trim(Thing.name);
            /* A part named for what it is ("Sprite") goes by what it belongs to */
            if (Name == "Sprite" && Thing.transform.parent != null)
                Name = Trim(Thing.transform.parent.name) + "/" + Name;
            return Name;
        }

        private static string Trim(string Name)
        {
            int Cut = Name.IndexOf(" (", StringComparison.Ordinal);
            if (Cut > 0)
                Name = Name.Substring(0, Cut);
            return Name.Replace("(Clone)", "").Trim();
        }

        private static readonly HashSet<string> Described = new();

        /// <summary>
        /// What a thing is, once per name, to tell what breaks in a room
        /// </summary>
        private static void Describe(Health Thing, string Name)
        {
            if (!Described.Add(Name))
                return;
            string Path = Thing.name;
            for (Transform Up = Thing.transform.parent; Up != null; Up = Up.parent)
                Path = Up.name + "/" + Path;
            List<string> Parts = new();
            foreach (Component Part in Thing.GetComponents<Component>())
                Parts.Add(Part?.GetType().Name);
            Plugin.Logger?.LogInfo($"[COOP] Breakable {Name}: {Path} [{string.Join(", ", Parts)}] number {NumberOf(Thing)}");
        }

        private static List<Broken> ForPlace(int ForRun, string Place)
        {
            if (ForRun != Run)
            {
                Run = ForRun;
                ByPlace.Clear();
            }
            if (!ByPlace.TryGetValue(Place, out List<Broken> List))
                ByPlace[Place] = List = new List<Broken>();
            return List;
        }

        private static bool InRun(out string Place)
        {
            Place = CoopSync.CurrentPlace();
            return CoopSync.InSession && CoopSync.CurrentRun != 0 && Place != null && Place.Contains("@");
        }

        /* OURS ***************************************************************/

        private static void Send(string Place, Broken Item)
        {
            CoopSync.Send(CoopSync.Kind.PropBroken, Writer =>
            {
                Writer.Write(CoopSync.CurrentRun);
                Writer.Write(Place);
                Writer.Write((byte)Item.Type);
                Writer.Write(Item.Name);
                Writer.Write(Item.At.x);
                Writer.Write(Item.At.y);
                Writer.Write((short)Item.Number);
            });
            ++Sent;
        }

        private static void Record(Kind Type, GameObject Thing, Vector2? At = null, int Number = -1)
        {
            if (ApplyingRemote || Thing == null || !InRun(out string Place))
                return;
            Broken Item = new() { Type = Type, Name = BaseName(Thing), At = At ?? (Vector2)Thing.transform.position, Number = Number, Ours = true };
            ForPlace(CoopSync.CurrentRun, Place).Add(Item);
            try
            {
                Send(Place, Item);
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Sending a broken {Item.Name} failed: {e.Message}");
            }
        }

        private static void OnDieAny(Health Victim)
        {
            if (Victim == null || Victim.isPlayer || Victim.team != Health.Team.Neutral || Victim.GetComponent<PlayerFarming>() != null)
                return;
            Describe(Victim, BaseName(Victim.gameObject));
            /* Something that came up after the room did may have spawned elsewhere for the partner: where it died tells more */
            Record(Kind.Health, Victim.gameObject, NumberOf(Victim) >= 0 ? SpawnOf(Victim) : Victim.transform.position, NumberOf(Victim));
        }

        /// <summary>
        /// A player who (re)joined gets what we broke in the run.
        /// </summary>
        internal static void SendAll(int ForRun)
        {
            if (ForRun == 0 || ForRun != Run)
                return;
            foreach (var Pair in ByPlace)
            {
                foreach (Broken Item in Pair.Value)
                {
                    if (Item.Ours)
                        Send(Pair.Key, Item);
                }
            }
        }

        /* THEIRS *************************************************************/

        /// <summary>
        /// Something broke in the partner's game.
        /// </summary>
        public static void OnRelay(System.IO.BinaryReader Reader)
        {
            int ForRun = Reader.ReadInt32();
            string Place = Reader.ReadString();
            Broken Item = new() { Type = (Kind)Reader.ReadByte(), Name = Reader.ReadString(), At = new Vector2(Reader.ReadSingle(), Reader.ReadSingle()), Number = Reader.ReadInt16() };
            ++Received;
            if (ForRun != CoopSync.CurrentRun && CoopSync.CurrentRun != 0 && ForRun != Run)
                return;
            ForPlace(ForRun, Place).Add(Item);

            if (ForRun == CoopSync.CurrentRun && Place == CoopSync.CurrentPlace())
                Break(Item, false);
        }

        /* How the things the partner broke were found here, for the test harness report */
        internal static int ByNumber, BySpawn, ByNearest;

        /// <summary>
        /// Our copy of a thing the partner broke: what came up where theirs did, else the same
        /// number in the room if it stands roughly there (a critter runs about), else the nearest
        /// one of that name (a critter runs further than a pushed pot rolls)
        /// </summary>
        private static Health FindHealth(Broken Item)
        {
            Health Numbered = null, Spawned = null, Running = null;
            float SpawnDistance = Near, RunDistance = float.MaxValue;
            foreach (Health Candidate in Health.neutralTeam)
            {
                if (Candidate == null || !Candidate.enabled || Candidate.HP <= 0f || !Candidate.gameObject.activeInHierarchy || Candidate.isPlayer)
                    continue;
                if (BaseName(Candidate.gameObject) != Item.Name)
                    continue;
                Vector2 From = SpawnOf(Candidate);
                float Distance = Vector2.Distance(From, Item.At);
                if (Item.Number >= 0 && NumberOf(Candidate) == Item.Number && Distance < 10f)
                    Numbered = Candidate;
                if (Distance < SpawnDistance)
                {
                    Spawned = Candidate;
                    SpawnDistance = Distance;
                }
                Distance = Mathf.Min(Distance, Vector2.Distance(Candidate.transform.position, Item.At));
                /* An enemy's body lies where it died, which differs between the games: any body of that enemy will do */
                /* A critter runs anywhere in the room: any one of them, so the count matches */
                float Reach = Item.Name.StartsWith("Dead Body", StringComparison.Ordinal) || Candidate.GetComponentInParent<Critter>() != null
                    ? float.MaxValue : 2.5f;
                if (Distance < Reach && Distance < RunDistance)
                {
                    Running = Candidate;
                    RunDistance = Distance;
                }
            }
            /* Where it came up is surest: a room's numbers drift when something comes up late on one side */
            if (Spawned != null)
            {
                ++BySpawn;
                return Spawned;
            }
            if (Numbered != null)
            {
                ++ByNumber;
                return Numbered;
            }
            if (Running != null)
                ++ByNearest;
            return Running;
        }

        private static BreakableObject FindRubble(Broken Item)
        {
            BreakableObject Best = null;
            float BestDistance = Near;
            foreach (BreakableObject Candidate in BreakableObject.BreakableObjects)
            {
                if (Candidate == null || !Candidate.gameObject.activeInHierarchy || (bool)Activated.GetValue(Candidate))
                    continue;
                float Distance = Vector2.Distance(Candidate.transform.position, Item.At);
                if (Distance < BestDistance && BaseName(Candidate.gameObject) == Item.Name)
                {
                    Best = Candidate;
                    BestDistance = Distance;
                }
            }
            return Best;
        }

        /// <summary>
        /// Breaks our copy of what the partner broke: as it breaks, or quietly when we walk in later
        /// </summary>
        private static void Break(Broken Item, bool Quietly)
        {
            ApplyingRemote = true;
            try
            {
                if (Item.Type == Kind.Rubble)
                {
                    BreakableObject Rubble = FindRubble(Item);
                    if (Rubble == null)
                    {
                        Missed(Item, Quietly);
                        return;
                    }
                    if (Quietly || PlayerFarming.Instance == null)
                    {
                        Activated.SetValue(Rubble, true);
                        foreach (GameObject Part in Rubble.gameObjectsToDisable)
                            Part?.SetActive(false);
                    }
                    else
                    {
                        Rubble.StopAllCoroutines();
                        Rubble.StartCoroutine((IEnumerator)CollideRoutine.Invoke(Rubble, new object[] { PlayerFarming.Instance.health }));
                    }
                    Item.Done = true;
                    ++Applied;
                    return;
                }

                Health Thing = FindHealth(Item);
                if (Thing == null)
                {
                    Missed(Item, Quietly);
                    return;
                }
                LongGrass Grass = Thing.GetComponent<LongGrass>();
                if (Quietly && Grass != null)
                {
                    Grass.SetCut();
                    Item.Done = true;
                    ++Applied;
                    return;
                }

                /* Its loot flies to the partner who broke it, see GhostLoot; with no partner here there is none */
                GameObject Partner = GhostLoot.PartnerHere();
                if (Partner == null)
                {
                    foreach (DropLootOnDeath Drop in Thing.GetComponentsInChildren<DropLootOnDeath>())
                        Drop.enabled = false;
                    foreach (DropMultipleLootOnDeath Drop in Thing.GetComponentsInChildren<DropMultipleLootOnDeath>())
                        Drop.enabled = false;
                }
                Thing.invincible = false;
                GhostLoot.Begin(Partner);
                try
                {
                    if (Thing.DealDamage(Thing.HP + 1f, Thing.gameObject, Thing.transform.position, false, Health.AttackTypes.Melee, true, Health.AttackFlags.ForceKill))
                    {
                        Item.Done = true;
                        ++Applied;
                    }
                }
                finally
                {
                    GhostLoot.End();
                }
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Breaking the partner's {Item.Name} failed: {e.Message}");
            }
            finally
            {
                ApplyingRemote = false;
            }
            if (RemoteDrops.Count > 0)
                Plugin.MonoInstance.StartCoroutine(RemoveDrops());
        }

        /// <summary>
        /// Nothing of that name stands there: it broke here already, or the rooms differ
        /// </summary>
        private static void Missed(Broken Item, bool Quietly)
        {
            if (Quietly)
                return;

            /* Both broke it: we did ourselves (what died may be gone from the scene), or ours is dead or put away */
            if (InRun(out string Place) && ByPlace.TryGetValue(Place, out List<Broken> List))
            {
                bool Body = Item.Name.StartsWith("Dead Body", StringComparison.Ordinal);
                foreach (Broken Mine in List)
                {
                    if (!Mine.Ours || Mine.Type != Item.Type || Mine.Name != Item.Name || Mine.Done)
                        continue;
                    if (Body || (Item.Number >= 0 && Mine.Number == Item.Number) || Vector2.Distance(Mine.At, Item.At) < Near)
                    {
                        /* One of ours answers one of theirs */
                        Mine.Done = true;
                        ++AlreadyBroken;
                        return;
                    }
                }
            }
            Health[] All = UnityEngine.Object.FindObjectsOfType<Health>(true);
            foreach (Health Candidate in All)
            {
                if (BaseName(Candidate.gameObject) != Item.Name || (Candidate.HP > 0f && Candidate.gameObject.activeInHierarchy && Candidate.enabled))
                    continue;
                if ((Item.Number >= 0 && NumberOf(Candidate) == Item.Number) || Vector2.Distance(SpawnOf(Candidate), Item.At) < Near)
                {
                    ++AlreadyBroken;
                    return;
                }
            }
            if (++Missing > 40)
                return;

            /* What stands nearest under that name, whatever its state, to tell a double break from a different room */
            string Nearest = "nothing of that name";
            float Best = float.MaxValue;
            foreach (Health Candidate in All)
            {
                if (BaseName(Candidate.gameObject) != Item.Name)
                    continue;
                float Distance = Vector2.Distance(SpawnOf(Candidate), Item.At);
                if (Distance < Best)
                {
                    Best = Distance;
                    Nearest = $"{Distance:F2} away, number {NumberOf(Candidate)}, hp {Candidate.HP:F1}, enabled {Candidate.enabled}, active {Candidate.gameObject.activeInHierarchy}, team {Candidate.team}, listed {Health.neutralTeam.Contains(Candidate)}";
                }
            }
            Plugin.Logger?.LogInfo($"[COOP] No {Item.Name} ({Item.Type}, number {Item.Number}) near {Item.At.x:F2},{Item.At.y:F2} to break: {Nearest}");
        }

        /// <summary>
        /// What our copy dropped as it broke goes again: the partner's drops reach us as pickups
        /// </summary>
        private static IEnumerator RemoveDrops()
        {
            List<PickUp> Drops = new(RemoteDrops);
            RemoteDrops.Clear();
            yield return null;
            foreach (PickUp Drop in Drops)
            {
                if (Drop != null)
                    UnityEngine.Object.Destroy(Drop.gameObject);
            }
        }

        /// <summary>
        /// A room we walk into breaks the way it broke for the partner
        /// </summary>
        private static IEnumerator BreakOnEntry()
        {
            yield return new WaitForSecondsRealtime(1f);
            while (MMTools.MMTransition.IsPlaying)
                yield return new WaitForSecondsRealtime(0.25f);
            /* Things come out of the pool before they are put in place: take where they stand now */
            NumberRoom();

            if (!InRun(out string Place) || Run != CoopSync.CurrentRun || !ByPlace.TryGetValue(Place, out List<Broken> List))
                yield break;

            /* A room comes back whole when we walk in again */
            foreach (Broken Item in List)
                Item.Done = false;
            int Before = Applied;
            foreach (Broken Item in new List<Broken>(List))
            {
                if (!Item.Ours && !Item.Done)
                    Break(Item, true);
            }
            AppliedOnEntry += Applied - Before;
            if (Applied > Before)
                Plugin.Logger?.LogInfo($"[COOP] {Applied - Before} things the partner broke in {Place} broken here too");
        }

        [HarmonyPatch]
        private static class PropSyncPatches
        {
            /// <summary>
            /// Something that comes up after the room is numbered keeps where it came up (no number)
            /// </summary>
            [HarmonyPatch(typeof(Health), nameof(Health.OnEnable))]
            [HarmonyPostfix]
            private static void Enabled(Health __instance)
            {
                if (__instance.isPlayer || __instance.team != Health.Team.Neutral || Spawns.TryGetValue(__instance, out _))
                    return;
                Spawn Info = new() { At = __instance.transform.position };
                Spawns.Add(__instance, Info);
                Plugin.MonoInstance.StartCoroutine(Settle(__instance, Info));
            }

            /// <summary>
            /// A thing out of a pool is put in place after it comes up: where it stands a moment later
            /// </summary>
            private static IEnumerator Settle(Health Thing, Spawn Info)
            {
                yield return new WaitForSeconds(0.3f);
                if (Thing != null && Info.Number < 0)
                    Info.At = Thing.transform.position;
            }

            [HarmonyPatch(typeof(BreakableObject), "CollideRoutine")]
            [HarmonyPrefix]
            private static void Collided(BreakableObject __instance) => Record(Kind.Rubble, __instance.gameObject);

            [HarmonyPatch(typeof(InventoryItem), nameof(InventoryItem.Spawn), new[] { typeof(InventoryItem.ITEM_TYPE), typeof(int), typeof(Vector3), typeof(float), typeof(Action<PickUp>) })]
            [HarmonyPostfix]
            private static void Spawned(PickUp __result)
            {
                if (ApplyingRemote && __result != null && !GhostLoot.IsGhost(__result) && GhostLoot.PartnerHere() == null)
                    RemoteDrops.Add(__result);
            }
        }
    }
}

/* EOF */
