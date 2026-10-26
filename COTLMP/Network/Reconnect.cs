/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     A friend who lost the connection to the host connects again by themselves
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP.Game;
using COTLMP.Ui;
using MMTools;
using System;
using System.Collections;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Network
{
    /// <summary>
    /// Connecting again after a lost connection.
    /// </summary>
    /// <remarks>
    /// A friend on the host's save whose connection went quiet (nothing from the host for a while, not
    /// a goodbye) doesn't leave for the main menu at once. The game stops behind the black loading
    /// screen with "Connecting 1/3" next to its crown, and the friend joins the host again, as accepting
    /// an invite would: the host's save comes anew and Rejoin takes the friend back to the host with
    /// their hearts and gear. Three tries; after the third the friend goes to the main menu with the error.
    /// </remarks>
    internal static class Reconnect
    {
        internal const int Tries = 3;

        /* One try: finding the host's lobby and the handshake; then the host's save, which is bigger */
        private const float TryTime = 25f;
        private const float SaveTime = 90f;
        private const float Pause = 3f;

        /// <summary>
        /// Connecting again (the black screen is up)
        /// </summary>
        internal static bool Active { get; private set; }

        /* Counters for the test harness report */
        internal static int Started, Succeeded, Failed, Try;

        private static ulong Host;
        private static bool Holding;
        private static bool HandedOver;

        /* Test switch: -cotlmp-droplink=N[,D] cuts the link for D s (45 by default), N s into the friend's game on the host's save */
        private static float DropAfter = -1f;
        private static float DropFor = 45f;
        private static float DropUntil;
        private static float SteadySince = -1f;

        /// <summary>
        /// Nothing goes either way over the link (a test of a lost connection)
        /// </summary>
        internal static bool Outage => DropUntil > Time.realtimeSinceStartup;

        public static void Initialize()
        {
            foreach (string Arg in Environment.GetCommandLineArgs())
            {
                if (Arg.StartsWith("-cotlmp-droplink=", StringComparison.OrdinalIgnoreCase))
                {
                    string[] Parts = Arg.Substring("-cotlmp-droplink=".Length).Split(',');
                    float.TryParse(Parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out DropAfter);
                    if (Parts.Length > 1)
                        float.TryParse(Parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out DropFor);
                }
            }
            if (DropAfter >= 0f)
                Plugin.MonoInstance.StartCoroutine(DropLater());
        }

        /// <summary>
        /// The connection went quiet: connect again instead of leaving. False if this isn't for us
        /// (we aren't a friend on the host's save), and the session ends as before.
        /// </summary>
        /// <remarks>Runs on the main thread, while the session's network loop ends.</remarks>
        internal static bool Begin(ulong HostId)
        {
            if (Active)
            {
                /* A try that got through and then went quiet again: that try failed, the next one goes on */
                Plugin.Logger?.LogWarning("[COOP] The connection went quiet again while connecting");
                PauseMenuPatches.EndSession(null, KeepGame: true);
                return true;
            }
            if (HostId == 0 || !HostSave.Playing)
                return false;

            Active = true;
            Host = HostId;
            HandedOver = false;
            Try = 1;
            ++Started;
            Plugin.Logger?.LogWarning($"[COOP] Lost the connection to host {HostId}, connecting again");
            PauseMenuPatches.EndSession(null, KeepGame: true);
            Plugin.MonoInstance.StartCoroutine(Run());
            return true;
        }

        /// <summary>
        /// The host's save came: its loading takes the black screen over. True if it is ours (black now).
        /// </summary>
        internal static bool HandOver()
        {
            if (!Active)
                return false;
            HandedOver = true;
            if (!Holding)
                return false;
            Holding = false;
            /* Stopped, not faded out: the screen stays black for the save's own loading screen */
            MMTransition.StopCurrentTransition();
            return true;
        }

        private static IEnumerator Run()
        {
            /* A load going on brings its own black screen: ours comes after it */
            float Until = Time.realtimeSinceStartup + 15f;
            while (MMTransition.IsPlaying && Time.realtimeSinceStartup < Until)
                yield return null;
            if (!MMTransition.IsPlaying)
            {
                Holding = true;
                MMTransition.Play(MMTransition.TransitionType.ChangeRoomWaitToResume, MMTransition.Effect.BlackFade, MMTransition.NO_SCENE, 1f, "", null);
            }

            for (Try = 1; Try <= Tries && !HandedOver; Try++)
            {
                Plugin.Logger?.LogInfo($"[COOP] Connecting to host {Host} again, try {Try} of {Tries}");
                float Deadline = Time.realtimeSinceStartup + TryTime;
                bool Asked = false, Joined = false, Shot = false;
                float Began = Time.realtimeSinceStartup;
                while (!HandedOver && Time.realtimeSinceStartup < Deadline)
                {
                    Say();
                    /* The test harness's frame of the black screen */
                    if (!Shot && Time.realtimeSinceStartup - Began > 2f)
                    {
                        Shot = true;
                        COTLMP.Debug.HudProbe.Take($"reconnect-{Try}");
                    }
                    if (!Asked)
                        Asked = SteamBridge.JoinHostNow(Host);
                    /* Through: the host's save is on its way */
                    if (!Joined && SteamBridge.IsJoinedClient)
                    {
                        Joined = true;
                        Deadline = Time.realtimeSinceStartup + SaveTime;
                    }
                    /* Through and lost again */
                    if (Joined && !SteamBridge.IsJoinedClient)
                        break;
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                if (HandedOver)
                    break;

                Plugin.Logger?.LogWarning($"[COOP] Try {Try} of {Tries} to connect again didn't get through");
                SteamBridge.CancelJoin();
                if (CoopSync.InSession)
                    PauseMenuPatches.EndSession(null, KeepGame: true);
                if (Try < Tries)
                {
                    float Next = Time.realtimeSinceStartup + Pause;
                    while (Time.realtimeSinceStartup < Next)
                    {
                        Say();
                        yield return null;
                    }
                }
            }

            if (HandedOver)
            {
                ++Succeeded;
                Plugin.Logger?.LogInfo($"[COOP] Connected to the host again on try {Math.Min(Try, Tries)}");
                Active = false;
                yield break;
            }

            /* Given up: to the main menu, with the error */
            ++Failed;
            Plugin.Logger?.LogError($"[COOP] Couldn't connect to host {Host} again after {Tries} tries");
            Active = false;
            if (Holding)
            {
                Holding = false;
                MMTransition.StopCurrentTransition();
            }
            MonoSingleton<Lamb.UI.UIManager>.Instance.ForceDisableSaving = false;
            PauseMenuPatches.Message = I2.Loc.LocalizationManager.GetTranslation("Multiplayer/Reconnect/Failed");
            HostSave.Forget(0.01f);

            /* The test harness's frame of the main menu with the error */
            float Shown = Time.realtimeSinceStartup + 30f;
            while (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Main Menu" && Time.realtimeSinceStartup < Shown)
                yield return null;
            yield return new WaitForSecondsRealtime(4f);
            COTLMP.Debug.HudProbe.Take("reconnect-failed");
        }

        /// <summary>
        /// "Connecting 2/3" next to the loading screen's crown
        /// </summary>
        private static void Say()
        {
            try
            {
                if (MMTransition.Instance != null && MMTransition.Instance.LoadingText != null)
                    MMTransition.Instance.LoadingText.text = string.Format(I2.Loc.LocalizationManager.GetTranslation("Multiplayer/Reconnect/Connecting"), Math.Min(Try, Tries), Tries);
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Can't show the connecting line: {e.Message}");
            }
        }

        private static IEnumerator DropLater()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                bool Steady = SteamBridge.IsJoinedClient && HostSave.Playing && !Active && PlayerFarming.Instance != null && !MMTransition.IsPlaying;
                if (!Steady)
                {
                    SteadySince = -1f;
                    continue;
                }
                if (SteadySince < 0f)
                    SteadySince = Time.realtimeSinceStartup;
                if (Time.realtimeSinceStartup - SteadySince < DropAfter)
                    continue;
                Plugin.Logger?.LogWarning($"[QA] Cutting the link for {DropFor:F0} s (a lost connection)");
                DropUntil = Time.realtimeSinceStartup + DropFor;
                yield break;
            }
        }
    }
}

/* EOF */
