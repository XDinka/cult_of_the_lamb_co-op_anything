/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Shared dungeon runs: same layout for both players and shared enemy health
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP.Debug;
using HarmonyLib;
using MMBiomeGeneration;
using MMRoomGeneration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// Keeps two players' dungeon runs close enough to play them together.
    /// </summary>
    /// <remarks>
    /// Each player still plays their own save, so nothing here is authoritative.
    /// <list type="bullet">
    /// <item>A run gets an id that both players share. A floor's seed is derived from it,
    /// so both players get the same rooms. The game draws every floor seed from
    /// DataManager.RandomSeed, which we replace just before BiomeGenerator.Start draws it.</item>
    /// <item>UnityEngine.Random is reset from the room seed when a room is generated or
    /// entered, so the same room gets the same enemies on both sides.</item>
    /// <item>Enemies are matched by place, name and the order they appear in. When one side
    /// damages an enemy, its health fraction goes to the other side, which lowers its own
    /// copy to match and kills it at zero.</item>
    /// </list>
    /// </remarks>
    internal static class CoopSync
    {
        private enum Kind : byte
        {
            RunStart = 1,
            EnemyHealth = 2
        }

        /// <summary>
        /// A partner's run is adopted if we start one in the same place within this many seconds
        /// </summary>
        private const float AdoptWindow = 600f;

        /* The run we are in and the last run the partner started */
        private static int RunId, RunLocation;
        private static float RunStartedAt;
        private static int PartnerRunId, PartnerRunLocation = -1;
        private static float PartnerRunTime;

        /* Enemy matching, reset on every scene load */
        private static readonly Dictionary<Health, string> EnemyIds = new();
        private static readonly Dictionary<string, Health> EnemiesById = new();
        private static readonly Dictionary<string, int> Counters = new();
        private static readonly Dictionary<string, float> SentFraction = new();
        private static readonly Dictionary<string, float> RemoteFraction = new();
        private static readonly Dictionary<string, float> Outgoing = new();
        private static bool ApplyingRemote;

        /* Counters for the test harness report */
        internal static int EnemiesSeen, HitsSent, HitsApplied, KillsApplied;
        internal static int LastSeed;

        /// <summary>
        /// True while connected to a session, the only time any of this runs.
        /// </summary>
        private static bool InSession => Plugin.GlobalsInternal.InGameSession && COTLMP.Network.Network.IsConnected;

        /// <summary>
        /// Starts the coroutines and hooks.
        /// </summary>
        public static void Initialize()
        {
            SceneManager.sceneLoaded += (_, _) => ResetEnemies();
            Plugin.MonoInstance.StartCoroutine(Flush());
        }

        /// <summary>
        /// Where the local player is: the scene, plus the floor and room in a dungeon.
        /// </summary>
        /// <returns>The place name the server groups players by, or null while there is no scene</returns>
        public static string CurrentPlace()
        {
            string Scene = SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(Scene))
                return null;

            BiomeGenerator Generator = BiomeGenerator.Instance;
            if (Generator == null || Generator.CurrentRoom == null || Generator.gameObject.scene != SceneManager.GetActiveScene())
                return Scene;

            return $"{Scene}#{GameManager.CurrentDungeonLayer}.{GameManager.CurrentDungeonFloor}@{Generator.CurrentRoom.x},{Generator.CurrentRoom.y}";
        }

        /* RELAY **************************************************************/

        private static void Send(Kind Type, Action<BinaryWriter> Body)
        {
            using MemoryStream Stream = new();
            using BinaryWriter Writer = new(Stream);
            Writer.Write((byte)Type);
            Body(Writer);
            Writer.Flush();
            COTLMP.Network.Network.SendRelay(Stream.ToArray());
        }

        /// <summary>
        /// Handles co-op sync data from another player, on the main thread.
        /// </summary>
        /// <param name="From">The server ID of the sender</param>
        /// <param name="Data">The payload as Send wrote it</param>
        public static void OnRelay(uint From, byte[] Data)
        {
            using MemoryStream Stream = new(Data);
            using BinaryReader Reader = new(Stream);

            switch ((Kind)Reader.ReadByte())
            {
                case Kind.RunStart:
                {
                    int Location = Reader.ReadInt32();
                    int Id = Reader.ReadInt32();
                    if (Id != RunId && Id != PartnerRunId)
                        Plugin.Logger?.LogInfo($"[COOP] Player {From} is in run {Id} in {(FollowerLocation)Location}");

                    /* Our own run coming back after they adopted it, or a repeat of what we know */
                    if (Id == RunId || (Id == PartnerRunId && Location == PartnerRunLocation))
                        break;
                    PartnerRunLocation = Location;
                    PartnerRunId = Id;
                    PartnerRunTime = Time.realtimeSinceStartup;
                    break;
                }

                case Kind.EnemyHealth:
                {
                    int Count = Reader.ReadByte();
                    for (int Index = 0; Index < Count; Index++)
                    {
                        string Id = Reader.ReadString();
                        float Fraction = Reader.ReadSingle();

                        if (!RemoteFraction.TryGetValue(Id, out float Known) || Fraction < Known)
                            RemoteFraction[Id] = Fraction;
                        if (EnemiesById.TryGetValue(Id, out Health Enemy) && Enemy != null)
                            ApplyFraction(Id, Enemy);
                    }
                    break;
                }
            }
        }

        /* RUNS ***************************************************************/

        /// <summary>
        /// A run starts: join the partner's run if they just started one in the same place,
        /// otherwise start a new one and tell them.
        /// </summary>
        private static void OnNewRun(FollowerLocation Location)
        {
            if (!InSession)
                return;

            RemoteFraction.Clear();
            if (PartnerRunLocation == (int)Location && Time.realtimeSinceStartup - PartnerRunTime < AdoptWindow)
            {
                RunId = PartnerRunId;
                RunLocation = (int)Location;
                RunStartedAt = PartnerRunTime;
                PartnerRunLocation = -1;
                Plugin.Logger?.LogInfo($"[COOP] Joining the partner's run {RunId} in {Location}");
                return;
            }

            RunId = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            RunLocation = (int)Location;
            RunStartedAt = Time.realtimeSinceStartup;
            Plugin.Logger?.LogInfo($"[COOP] Starting run {RunId} in {Location}");
            AnnounceRun();
        }

        /// <summary>
        /// Tells the partner which run we are in. Repeated while the run is young, so a
        /// partner who connects or comes back after we started can still join it.
        /// </summary>
        private static void AnnounceRun()
        {
            Send(Kind.RunStart, Writer =>
            {
                Writer.Write(RunLocation);
                Writer.Write(RunId);
            });
        }

        private static int Mix(params int[] Values)
        {
            unchecked
            {
                uint Hash = 2166136261;
                foreach (int Value in Values)
                {
                    Hash = (Hash ^ (uint)Value) * 16777619;
                    Hash ^= Hash >> 15;
                    Hash *= 0x2c1b3c6d;
                    Hash ^= Hash >> 12;
                }
                return (int)Hash;
            }
        }

        /* ENEMIES ************************************************************/

        private static void ResetEnemies()
        {
            EnemyIds.Clear();
            EnemiesById.Clear();
            Counters.Clear();
            SentFraction.Clear();
            Outgoing.Clear();

            /*
             * What the partner did is kept until the next run: ids carry the floor and room,
             * and the partner may have hit enemies before we loaded the floor.
             */
        }

        private static void Register(Health Enemy)
        {
            if (!InSession || Enemy == null || EnemyIds.ContainsKey(Enemy))
                return;

            string Place = CurrentPlace();
            if (Place == null || !Place.Contains("#"))
                return;

            string Name = Enemy.gameObject.name.Replace("(Clone)", "").Trim();
            string Group = Place + "|" + Name;
            Counters.TryGetValue(Group, out int Ordinal);
            Counters[Group] = Ordinal + 1;

            string Id = Group + "|" + Ordinal;
            EnemyIds[Enemy] = Id;
            EnemiesById[Id] = Enemy;
            SentFraction[Id] = 1f;
            ++EnemiesSeen;

            if (RemoteFraction.ContainsKey(Id))
                Plugin.MonoInstance.StartCoroutine(ApplyLater(Id, Enemy));
        }

        private static IEnumerator ApplyLater(string Id, Health Enemy)
        {
            /* Let the enemy finish spawning (InitHP) before lowering its health */
            yield return null;
            yield return null;
            if (Enemy != null)
                ApplyFraction(Id, Enemy);
        }

        /// <summary>
        /// Lowers our copy of an enemy to the health the other player left it with.
        /// </summary>
        private static void ApplyFraction(string Id, Health Enemy)
        {
            if (!RemoteFraction.TryGetValue(Id, out float Fraction) || Enemy.totalHP <= 0f || Enemy.HP <= 0f)
                return;

            float Target = Fraction * Enemy.totalHP;
            if (Enemy.HP <= Target + 0.01f)
                return;

            SentFraction[Id] = Mathf.Min(SentFraction.TryGetValue(Id, out float Sent) ? Sent : 1f, Fraction);
            ApplyingRemote = true;
            try
            {
                if (Target > 0.01f)
                {
                    Enemy.HP = Target;
                    ++HitsApplied;
                }
                else
                {
                    Plugin.MonoInstance.StartCoroutine(Kill(Enemy));
                }
            }
            finally
            {
                ApplyingRemote = false;
            }
        }

        /// <summary>
        /// Kills our copy of an enemy the other player killed. An enemy that is still
        /// spawning or briefly invincible refuses damage, so keep trying for a while.
        /// </summary>
        private static IEnumerator Kill(Health Enemy)
        {
            var Wait = new WaitForSeconds(0.2f);

            for (int Try = 0; Try < 50 && Enemy != null && Enemy.HP > 0f; Try++)
            {
                if (Enemy.enabled && Enemy.gameObject.activeInHierarchy && PlayerFarming.Instance != null)
                {
                    ApplyingRemote = true;
                    try
                    {
                        Enemy.HP = Mathf.Min(Enemy.HP, 0.01f);
                        if (Enemy.DealDamage(Enemy.totalHP + 1f, PlayerFarming.Instance.gameObject, Enemy.transform.position, false, Health.AttackTypes.Melee, true))
                        {
                            ++KillsApplied;
                            yield break;
                        }
                    }
                    catch (Exception e)
                    {
                        PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Killing a synced enemy failed: {e.Message}");
                    }
                    finally
                    {
                        ApplyingRemote = false;
                    }
                }
                yield return Wait;
            }
        }

        private static void OnHealthChanged(Health Enemy)
        {
            if (ApplyingRemote || !EnemyIds.TryGetValue(Enemy, out string Id) || Enemy.totalHP <= 0f)
                return;

            float Fraction = Mathf.Clamp01(Enemy.HP / Enemy.totalHP);
            if (SentFraction.TryGetValue(Id, out float Sent) && Fraction >= Sent - 0.001f)
                return;

            SentFraction[Id] = Fraction;
            Outgoing[Id] = Fraction;
        }

        /// <summary>
        /// Sends the health changes of the last tick in one packet.
        /// </summary>
        private static IEnumerator Flush()
        {
            var Wait = new WaitForSecondsRealtime(0.05f);
            List<KeyValuePair<string, float>> Batch = new();
            float NextAnnounce = 0f;

            while (true)
            {
                yield return Wait;

                if (InSession && RunId != 0 && Time.realtimeSinceStartup >= NextAnnounce &&
                    Time.realtimeSinceStartup - RunStartedAt < AdoptWindow)
                {
                    NextAnnounce = Time.realtimeSinceStartup + 3f;
                    try
                    {
                        AnnounceRun();
                    }
                    catch (Exception e)
                    {
                        PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Announcing the run failed: {e.Message}");
                    }
                }

                if (Outgoing.Count == 0)
                    continue;

                if (!InSession)
                {
                    Outgoing.Clear();
                    continue;
                }

                Batch.Clear();
                Batch.AddRange(Outgoing);
                Outgoing.Clear();

                /* Keep packets well under the server's size limit */
                for (int Start = 0; Start < Batch.Count; Start += 16)
                {
                    int End = Math.Min(Batch.Count, Start + 16);
                    try
                    {
                        Send(Kind.EnemyHealth, Writer =>
                        {
                            Writer.Write((byte)(End - Start));
                            for (int Index = Start; Index < End; Index++)
                            {
                                Writer.Write(Batch[Index].Key);
                                Writer.Write(Batch[Index].Value);
                            }
                        });
                        HitsSent += End - Start;
                    }
                    catch (Exception e)
                    {
                        PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Sending enemy health failed: {e.Message}");
                    }
                }
            }
        }

        /* PATCHES ************************************************************/

        [HarmonyPatch]
        private static class CoopSyncPatches
        {
            [HarmonyPatch(typeof(GameManager), nameof(GameManager.NewRun))]
            [HarmonyPostfix]
            private static void NewRun(FollowerLocation location)
            {
                try
                {
                    OnNewRun(location == FollowerLocation.None ? PlayerFarming.Location : location);
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogError($"[COOP] NewRun sync failed: {e}");
                }
            }

            /// <summary>
            /// Replace the run's random source with one both players share, right before the floor seed is drawn
            /// </summary>
            [HarmonyPatch(typeof(BiomeGenerator), "Start")]
            [HarmonyPrefix]
            private static void BiomeStart(BiomeGenerator __instance)
            {
                if (!InSession || RunId == 0 || !DataManager.UseDataManagerSeed || __instance.TestStartingLayer)
                    return;

                int Seed = Mix(RunId, (int)__instance.DungeonLocation, GameManager.CurrentDungeonLayer, GameManager.CurrentDungeonFloor);
                DataManager.RandomSeed = new System.Random(Seed);
            }

            [HarmonyPatch(typeof(BiomeGenerator), "Start")]
            [HarmonyPostfix]
            private static void BiomeStarted(BiomeGenerator __instance)
            {
                LastSeed = __instance.Seed;
                if (InSession)
                    Plugin.Logger?.LogInfo($"[COOP] Floor {__instance.DungeonLocation} layer {GameManager.CurrentDungeonLayer} floor {GameManager.CurrentDungeonFloor} seed {__instance.Seed} (run {RunId})");
            }

            [HarmonyPatch(typeof(GenerateRoom), nameof(GenerateRoom.Generate), new[] { typeof(int), typeof(GenerateRoom.ConnectionTypes), typeof(GenerateRoom.ConnectionTypes), typeof(GenerateRoom.ConnectionTypes), typeof(GenerateRoom.ConnectionTypes) })]
            [HarmonyPrefix]
            private static void Generate(int Seed)
            {
                if (InSession)
                    UnityEngine.Random.InitState(Seed);
            }

            [HarmonyPatch(typeof(BiomeRoom), nameof(BiomeRoom.Activate))]
            [HarmonyPrefix]
            private static void Activate(BiomeRoom __instance)
            {
                if (InSession)
                    UnityEngine.Random.InitState(Mix(__instance.Seed, 0x5eed));
            }

            [HarmonyPatch(typeof(Health), nameof(Health.SetTeam))]
            [HarmonyPostfix]
            private static void SetTeam(Health __instance, Health.Team teamTarget)
            {
                if (teamTarget != Health.Team.Team2 || __instance.isPlayer)
                    return;
                try
                {
                    Register(__instance);
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogError($"[COOP] Registering an enemy failed: {e}");
                }
            }

            [HarmonyPatch(typeof(Health), nameof(Health.HP), MethodType.Setter)]
            [HarmonyPostfix]
            private static void SetHP(Health __instance)
            {
                if (EnemyIds.Count != 0)
                    OnHealthChanged(__instance);
            }
        }
    }
}

/* EOF */
