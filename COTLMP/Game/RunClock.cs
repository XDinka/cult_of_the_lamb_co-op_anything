/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     One clock for a run: the host's
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using HarmonyLib;
using Lamb.UI.DeathScreen;
using System;
using System.Collections;
using System.IO;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// How long a run has gone is the host's, for both, as the game's own co-op has one clock.
    /// </summary>
    /// <remarks>
    /// The death screen shows a run's time from when that game started it (its scaled time since
    /// DataManager.dungeonRunDuration; the screen holds the time scale at 0 while it is up, so the
    /// time stands). Each game starts its run at its own moment, and a shared scene's wait stops one
    /// side's time for up to a minute, so the two clocks drift apart. The host tells the friend its
    /// run's time every second while its run goes on; the friend's run started that long ago in its
    /// own time. When the run ends for both at once (the friend down or dead as the host falls, the
    /// run done) the host's time at its end is the friend's too. An end of the host's alone (it
    /// escaped, it fell while the friend still fights) leaves the friend its own clock.
    /// </remarks>
    [HarmonyPatch]
    internal static class RunClock
    {
        private static bool IsHost => Plugin.GlobalsInternal.IsServerCreator;

        /* The host's time at an end shared by both, for this run (0: none yet) */
        private static int FinalRun;
        private static float FinalTime = -1f;

        /* Counters for the test harness report */
        internal static int Sent, Applied;

        public static void Initialize()
        {
            Plugin.MonoInstance.StartCoroutine(Tick());
        }

        private static float Elapsed => Time.time - DataManager.Instance.dungeonRunDuration;

        private static void Tell(bool Final)
        {
            if (!IsHost || !CoopSync.InSession || CoopSync.CurrentRun == 0 || DataManager.Instance == null || !LocationManager.IsDungeonActive())
                return;
            int Run = CoopSync.CurrentRun;
            float Value = Elapsed;
            CoopSync.Send(CoopSync.Kind.RunClock, Writer =>
            {
                Writer.Write(Run);
                Writer.Write(Value);
                Writer.Write(Final);
            });
            ++Sent;
        }

        private static IEnumerator Tick()
        {
            WaitForSecondsRealtime Wait = new(1f);
            while (true)
            {
                yield return Wait;
                try
                {
                    /* Over on our side once our death screen is up: what it shows is what we sent last */
                    if (UIDeathScreenOverlayController.Instance == null)
                        Tell(false);
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogWarning($"[COOP] Telling the run's time failed: {e.Message}");
                }
            }
        }

        /// <summary>
        /// The host's run time: ours is set to it
        /// </summary>
        public static void OnRelay(BinaryReader Reader)
        {
            int Run = Reader.ReadInt32();
            float Value = Reader.ReadSingle();
            bool Final = Reader.BaseStream.Position < Reader.BaseStream.Length && Reader.ReadBoolean();
            if (IsHost || Run != CoopSync.CurrentRun || DataManager.Instance == null)
                return;
            if (Final)
            {
                FinalRun = Run;
                FinalTime = Value;
                Plugin.Logger?.LogInfo($"[COOP] The run ended for both at the host's {Value:F1} s");
                /* Our death screen came first and has written its time already: it shows the host's */
                var Screen = UIDeathScreenOverlayController.Instance;
                if (Screen != null && TimeText?.GetValue(Screen) is TMPro.TMP_Text Shown && Shown.text != null && Shown.text.Contains(":"))
                    Shown.text = Format(Value);
            }
            /* Past an end shared by both only that end counts */
            else if (FinalRun == Run && FinalTime >= 0f)
                return;
            /* Our own death screen is up: its time stands as it is, unless it is the shared end */
            if (UIDeathScreenOverlayController.Instance != null && !Final)
                return;
            float Before = Elapsed;
            DataManager.Instance.dungeonRunDuration = Time.time - Value;
            if (++Applied == 1 || Mathf.Abs(Before - Value) > 2f)
                Plugin.Logger?.LogInfo($"[COOP] The run's time is the host's: {Value:F1} s (ours was {Before:F1} s)");
        }

        private static readonly System.Reflection.FieldInfo TimeText = AccessTools.Field(typeof(UIDeathScreenOverlayController), "_timeText");

        /* The death screen's way of writing a run's time (hours:minutes:seconds) */
        private static string Format(float Seconds)
        {
            int Minutes = Mathf.FloorToInt(Seconds / 60f), Hours = 0;
            while (Minutes > 60)
            {
                ++Hours;
                Minutes -= 60;
            }
            int Rest = Mathf.FloorToInt(Seconds % 60f);
            return $"{Hours:00}:{Minutes:00}:{Rest:00}";
        }

        /* A run ends for both together: the friend down or gone as the host falls, or the run itself over */
        private static bool EndsForBoth(UIDeathScreenOverlayController.Results Result) =>
            Result == UIDeathScreenOverlayController.Results.Completed || Result == UIDeathScreenOverlayController.Results.AwokenMountain ||
            (Result == UIDeathScreenOverlayController.Results.Killed && CoopLife.PartnerFallen);

        /* A death screen comes */
        [HarmonyPatch(typeof(UIDeathScreenOverlayController), nameof(UIDeathScreenOverlayController.Show), new Type[] { typeof(UIDeathScreenOverlayController.Results), typeof(bool) })]
        [HarmonyPrefix]
        private static void DeathScreen(UIDeathScreenOverlayController.Results result)
        {
            if (!CoopSync.InSession || DataManager.Instance == null)
                return;
            try
            {
                if (IsHost)
                    Tell(EndsForBoth(result));
                /* The host's run ended with ours: the host's time at its end */
                else if (FinalRun == CoopSync.CurrentRun && FinalTime >= 0f)
                    DataManager.Instance.dungeonRunDuration = Time.time - FinalTime;
                /* It draws one room per floor of the run: the host's floors, also for a friend who joined on the way */
                MapSync.AlignFloors("the death screen");
                Plugin.Logger?.LogInfo($"[COOP] Death screen ({result}): the run took {Elapsed:F1} s, {Map.MapManager.Instance?.DungeonConfig?.layers?.Count} floors");
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] The run's time at its end failed: {e.Message}");
            }
        }

        /* A new run: nothing of the last one's end stays */
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.NewRun))]
        [HarmonyPostfix]
        private static void NewRun()
        {
            FinalRun = 0;
            FinalTime = -1f;
        }
    }
}

/* EOF */
