/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     A room one player cleared is cleared for the other one too
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP.Debug;
using HarmonyLib;
using MMBiomeGeneration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// Keeps the two dungeons' rooms in the same state when the players split up.
    /// </summary>
    /// <remarks>
    /// Each game generates a room only when its player walks in, so a room the partner already
    /// fought through would come back full of enemies. When a room is completed we tell the
    /// partner; when they walk into it, their copy is completed at once the way the game marks a
    /// room it has finished: doors open, no waves, the enemies gone without dying (the partner
    /// already had their loot, and pickups are shared). A room the partner left half fought keeps
    /// the damage through CoopSync's shared enemy health.
    /// </remarks>
    internal static class ClearedRooms
    {
        /// <summary>
        /// How long after entering a cleared room late spawns are still caught
        /// </summary>
        private const float Watch = 8f;

        /* Per run, like CoopSync's enemy health: the partner may clear rooms before we start the run */
        private static readonly Dictionary<int, HashSet<string>> ClearedByRun = new();
        private static HashSet<string> Cleared => ForRun(CoopSync.CurrentRun);

        private static HashSet<string> ForRun(int Run)
        {
            if (!ClearedByRun.TryGetValue(Run, out var Rooms))
            {
                if (ClearedByRun.Count > 4)
                    ClearedByRun.Clear();
                ClearedByRun[Run] = Rooms = new();
            }
            return Rooms;
        }
        private static bool Clearing;
        private static string Watching;

        /* Counters for the test harness report */
        internal static int Sent, Applied, EnemiesHidden, Unstuck;

        /* A shut room with no enemy left opens after this long (waves come within a couple of seconds) */
        private const float StuckAfter = 8f;

        public static void Initialize()
        {
            BiomeGenerator.OnBiomeChangeRoom += OnRoomEntered;
            Plugin.MonoInstance.StartCoroutine(Unstick());
        }

        /// <summary>
        /// The room's doors are shut: none went down since they came up (a locked one, or the way
        /// back the game keeps shut behind the player, stays up in an open room too)
        /// </summary>
        private static bool DoorsShut() => !RoomLockController.DoorsOpen &&
            RoomLockController.RoomLockControllers.Exists(Lock => Lock != null && !Lock.Standalone && !Lock.Open && !Lock.ForcedLocked);

        private static bool IsBossRoom(BiomeRoom Room) =>
            Room != null && (Room.IsBoss || (Room.GameObject != null && Room.GameObject.GetComponentInChildren<MiniBossManager>(true) != null));

        private static bool AnyEnemyLeft()
        {
            foreach (Health Enemy in Health.team2)
            {
                if (Enemy != null && Enemy.gameObject.activeInHierarchy && Enemy.HP > 0f && Enemy.GetComponent<UnitObject>() != null)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// In a session two games share a room's fight, and a lost message could leave one of them
        /// shut in with nothing left to kill. Opens such a room, as the end of its fight would.
        /// </summary>
        private static IEnumerator Unstick()
        {
            float ShutSince = -1f;
            string ShutPlace = null;
            while (true)
            {
                yield return new WaitForSeconds(1f);
                try
                {
                    BiomeRoom Room = BiomeGenerator.Instance?.CurrentRoom;
                    string Place = CoopSync.CurrentPlace();
                    bool Stuck = CoopSync.InSession && Room != null && Place != null && !MMTools.MMTransition.IsPlaying && !IsBossRoom(Room) &&
                                 DoorsShut() && !AnyEnemyLeft();
                    if (!Stuck || Place != ShutPlace)
                    {
                        ShutSince = Stuck ? Time.time : -1f;
                        ShutPlace = Stuck ? Place : null;
                        continue;
                    }
                    if (Time.time - ShutSince < StuckAfter)
                        continue;

                    Plugin.Logger?.LogWarning($"[COOP] {Place} stayed shut with no enemy left (completed {Room.Completed}): opening it");
                    ++Unstuck;
                    ShutSince = -1f;
                    ShutPlace = null;
                    RoomLockController.RoomCompleted(!Room.Completed, true);
                }
                catch (Exception e)
                {
                    PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Checking for a shut room failed: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Sends every room cleared in a run again, for a player who (re)joined.
        /// </summary>
        public static void SendAll(int Run)
        {
            if (!ClearedByRun.TryGetValue(Run, out var Rooms))
                return;
            foreach (string Place in Rooms)
            {
                CoopSync.Send(CoopSync.Kind.RoomCleared, Writer =>
                {
                    Writer.Write(Run);
                    Writer.Write(Place);
                });
            }
        }

        /// <summary>
        /// The partner completed a room.
        /// </summary>
        public static void OnRelay(BinaryReader Reader)
        {
            int Run = Reader.ReadInt32();
            string Place = Reader.ReadString();
            Plugin.Logger?.LogInfo($"[COOP] Room {Place} cleared in run {Run} (ours {CoopSync.CurrentRun})");
            if (!ForRun(Run).Add(Place) || Run != CoopSync.CurrentRun)
                return;

            Plugin.Logger?.LogInfo($"[COOP] The partner cleared {Place}");

            /* We are standing in it: whatever is left here is ours to drop */
            if (Place == CoopSync.CurrentPlace())
                Plugin.MonoInstance.StartCoroutine(Clear(Place));
        }

        private static void OnRoomEntered()
        {
            try
            {
                string Place = CoopSync.CurrentPlace();
                if (CoopSync.InSession && Place != null && Cleared.Contains(Place))
                    Plugin.MonoInstance.StartCoroutine(Clear(Place));
            }
            catch (Exception e)
            {
                PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Checking a cleared room failed: {e.Message}");
            }
        }

        /// <summary>
        /// Completes our copy of a room the partner finished.
        /// </summary>
        private static IEnumerator Clear(string Place)
        {
            if (Watching == Place)
                yield break;
            Watching = Place;

            BiomeRoom Room = BiomeGenerator.Instance?.CurrentRoom;
            bool Done = Room == null || Room.Completed;
            bool Boss = IsBossRoom(Room);
            float Started = Time.realtimeSinceStartup;

            if (!Done)
            {
                Plugin.Logger?.LogInfo($"[COOP] {Place} was cleared by the partner, opening it");
                ++Applied;
            }

            while (Time.realtimeSinceStartup - Started < Watch && CoopSync.CurrentPlace() == Place)
            {
                try
                {
                    /* Waves that haven't come yet must not come */
                    foreach (EnemyRoundsBase Rounds in UnityEngine.Object.FindObjectsOfType<EnemyRoundsBase>())
                    {
                        if (Rounds.enabled)
                        {
                            Rounds.StopAllCoroutines();
                            Rounds.enabled = false;
                        }
                    }

                    foreach (Health Enemy in new List<Health>(Health.team2))
                    {
                        if (Enemy == null || !Enemy.gameObject.activeInHierarchy || Enemy.GetComponent<UnitObject>() == null)
                            continue;

                        /* A boss must die here too: its death opens the way on and counts for the run */
                        if (Boss)
                            CoopSync.KillLocal(Enemy);
                        else
                            Enemy.gameObject.SetActive(false);
                        ++EnemiesHidden;
                    }

                    /*
                     * A boss room completes itself once the boss is dead. Others open here, also when
                     * the room counts as completed already but its doors shut on us: its waves are off now
                     */
                    if ((!Done || DoorsShut()) && !Boss && Room != null)
                    {
                        if (Done)
                            Plugin.Logger?.LogInfo($"[COOP] {Place} is completed but shut, opening its doors");
                        Done = true;

                        /*
                         * Marked completed first, so RoomCompleted skips the per-room rewards (healing
                         * fleece, the room's completion reward): the partner had those already.
                         */
                        Clearing = true;
                        try
                        {
                            BiomeGenerator.Instance.CurrentRoom.Completed = true;
                            RoomLockController.RoomCompleted(false, true);

                            /* The fight's chest is ours too: the same one the partner got (RoomRandom) */
                            Interaction_Chest.Instance?.Reveal();
                        }
                        finally
                        {
                            Clearing = false;
                        }
                    }
                }
                catch (Exception e)
                {
                    PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Clearing {Place} failed: {e.Message}");
                }
                yield return new WaitForSeconds(0.25f);
            }

            Watching = null;
        }

        [HarmonyPatch]
        private static class ClearedRoomsPatches
        {
            /// <summary>
            /// We finished a dungeon room ourselves: tell the partner
            /// </summary>
            [HarmonyPatch(typeof(RoomLockController), nameof(RoomLockController.RoomCompleted))]
            [HarmonyPostfix]
            private static void RoomCompleted()
            {
                if (Clearing || !CoopSync.InSession)
                    return;
                try
                {
                    string Place = CoopSync.CurrentPlace();
                    if (Place == null || !Place.Contains("#") || !Cleared.Add(Place))
                        return;
                    ++Sent;
                    int Run = CoopSync.CurrentRun;
                    CoopSync.Send(CoopSync.Kind.RoomCleared, Writer =>
                    {
                        Writer.Write(Run);
                        Writer.Write(Place);
                    });
                }
                catch (Exception e)
                {
                    PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Sharing a cleared room failed: {e.Message}");
                }
            }
        }
    }
}

/* EOF */
