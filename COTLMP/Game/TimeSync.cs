/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     The joining player's clock keeps the host's time of day
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using HarmonyLib;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// Keeps the time of day the same for both players: the host's clock is the one that counts.
    /// </summary>
    /// <remarks>
    /// Each save has its own clock, so one player could be fighting at night while the other has
    /// the morning. The host sends its time of day every two seconds, and the joining player's
    /// game is brought onto it, only ever forward: a few minutes behind and its
    /// clock runs twice as fast, a few minutes ahead and its clock stands still while the rest of
    /// the game goes on, further behind (or more than two hours ahead) and the game skips forward
    /// the way sleeping does. The joining player's cult lives through the hours skipped, so their
    /// followers eat and sleep by the host's day. The day count stays each save's own.
    /// </remarks>
    internal static class TimeSync
    {
        /* Game minutes per real second at normal speed (a day of 1200 minutes lasts 8 real minutes) */
        private const float MinutesPerSecond = 1200f / 480f;

        /* Closer than this counts as the same time */
        private const float Close = 3f;

        /* Up to this far behind the clock runs faster, further it skips */
        private const float RunUpTo = 60f;

        /* Up to this far ahead the clock waits, further it skips round to the host's time tomorrow */
        private const float WaitUpTo = 120f;

        private static readonly FieldInfo MinutesToSkip = AccessTools.Field(typeof(SimulationManager), "_gameMinutesToSkip");

        private static float HostTime = -1f, ReceivedAt;
        private static bool PausedByUs;

        /* For the test harness report */
        internal static int Sent, Received, Skips;
        internal static float Gap;

        private static bool IsHost => Plugin.GlobalsInternal.IsServerCreator;

        public static void Initialize()
        {
            Plugin.MonoInstance.StartCoroutine(Loop());
        }

        private static IEnumerator Loop()
        {
            var Wait = new WaitForSecondsRealtime(2f);
            while (true)
            {
                yield return Wait;
                try
                {
                    if (!CoopSync.InSession || DataManager.Instance == null)
                    {
                        HostTime = -1f;
                        continue;
                    }
                    if (IsHost)
                    {
                        CoopSync.Send(CoopSync.Kind.Time, Writer =>
                        {
                            Writer.Write(TimeManager.CurrentGameTime);
                            Writer.Write((int)TimeManager.CurrentPhase);
                        });
                        ++Sent;
                    }
                    else
                    {
                        Correct();
                    }
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogWarning($"[COOP] Time sync failed: {e.Message}");
                }
            }
        }

        /// <summary>
        /// The host's time of day.
        /// </summary>
        public static void OnRelay(BinaryReader Reader)
        {
            float Time = Reader.ReadSingle();
            Reader.ReadInt32();
            if (IsHost)
                return;
            if (HostTime < 0f)
                Plugin.Logger?.LogInfo($"[COOP] Host's time of day {Time:F0}, ours {TimeManager.CurrentGameTime:F0}");
            HostTime = Time;
            ReceivedAt = UnityEngine.Time.realtimeSinceStartup;
            ++Received;
        }

        /// <summary>
        /// How far the host's clock is ahead of ours, in game minutes (negative: behind), or null without a recent time
        /// </summary>
        private static float? Ahead()
        {
            if (IsHost || HostTime < 0f || !CoopSync.InSession || DataManager.Instance == null)
                return null;
            float Since = UnityEngine.Time.realtimeSinceStartup - ReceivedAt;
            if (Since > 10f)
                return null;

            float Host = HostTime + Mathf.Min(Since, 4f) * MinutesPerSecond;
            float Length = TimeManager.GetCurrentDayLength();
            float Difference = Mathf.Repeat(Host - TimeManager.CurrentGameTime, Length);
            if (Difference > Length - WaitUpTo)
                Difference -= Length;
            return Difference;
        }

        private static bool Skipping => MinutesToSkip != null && (float)MinutesToSkip.GetValue(null) > 0f;

        /// <summary>
        /// Skips forward when the clocks are far apart; small gaps are closed while the game runs
        /// </summary>
        private static void Correct()
        {
            float? Difference = Ahead();
            if (!Difference.HasValue)
                return;
            Gap = Difference.Value;
            if (Gap <= RunUpTo || Skipping || SimulationManager.IsPaused || MonoSingleton<Lamb.UI.UIManager>.Instance.IsPaused)
                return;

            ++Skips;
            Plugin.Logger?.LogInfo($"[COOP] Our clock is {Gap:F0} minutes behind the host's, skipping forward");
            SimulationManager.Skip(null, Gap, null);
        }

        [HarmonyPatch]
        private static class TimeSyncPatches
        {
            [HarmonyPatch(typeof(TimeManager), nameof(TimeManager.Simulate))]
            [HarmonyPrefix]
            private static void Simulate(ref float deltaGameTime, bool skippingTime)
            {
                if (skippingTime || deltaGameTime <= 0f)
                    return;
                float? Difference = Ahead();
                if (!Difference.HasValue)
                    return;

                if (Difference.Value < -Close && !TimeManager.PauseGameTime)
                {
                    /* Ahead of the host: the clock waits, the cult goes on */
                    TimeManager.PauseGameTime = true;
                    PausedByUs = true;
                }
                else if (Difference.Value > Close)
                {
                    deltaGameTime = Mathf.Min(deltaGameTime * 2f, deltaGameTime + Difference.Value);
                }
            }

            [HarmonyPatch(typeof(TimeManager), nameof(TimeManager.Simulate))]
            [HarmonyPostfix]
            private static void Simulated()
            {
                if (!PausedByUs)
                    return;
                TimeManager.PauseGameTime = false;
                PausedByUs = false;
            }
        }
    }
}

/* EOF */
