/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Both players' mod versions compared on joining, with a way to the same one
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP.Debug;
using COTLMP.Network;
using HarmonyLib;
using I2.Loc;
using Lamb.UI;
using src.Extensions;
using src.UI;
using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// The mod versions of the host and the player joining.
    /// </summary>
    /// <remarks>
    /// Both send theirs on joining. Before the host's save is taken, the joining player sees any
    /// difference, with both versions: playing on is allowed, but different versions may break things
    /// or a save. With the host on a newer version the joining player can take exactly that version:
    /// the game updates, starts again and joins the host again by itself. With the host on an older one,
    /// the host can be asked to update: the host's game then offers the same, and the host opens the
    /// game online again afterwards. A host older than this check (0.2.8 and before) sends nothing.
    /// </remarks>
    internal static class VersionCheck
    {
        /* How long the joining player waits for the host's version */
        private const float WaitForHost = 6f;

        /* The partner's version this session, null while unknown */
        internal static string Partner;

        /* The test harness: what the joining player picks by itself, and where the windows' frames go */
        internal static string AutoChoice, ShotsDir;

        /* Counters for the test harness report */
        internal static int Warned, Suggested, SuggestionsTaken;

        private static string Ours => Updater.Version;

        internal static void Reset() => Partner = null;

        /// <summary>
        /// Ours, to the partner: the joining player on joining, the host in answer
        /// </summary>
        internal static void Announce()
        {
            CoopSync.Send(CoopSync.Kind.ModVersion, Writer => Writer.Write(Ours));
        }

        internal static void OnVersion(BinaryReader Reader)
        {
            string Theirs = Reader.ReadString();
            Partner = Theirs;
            Plugin.Logger?.LogInfo($"[COOP] The partner's mod is {Theirs}, ours {Ours}");
            if (!Plugin.GlobalsInternal.IsServerCreator)
                return;

            /* The host answers every joining (the friend may come back on another version), and hears of a
               different version as a note: the joining player decides */
            Announce();
            if (Theirs != Ours)
            {
                try
                {
                    NotificationCentre.Instance?.PlayGenericNotificationNonLocalizedParams("Multiplayer/Version/FriendHas", Theirs, Ours);
                }
                catch (Exception e)
                {
                    PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Can't show the friend's version: {e.Message}");
                }
            }
        }

        /// <summary>
        /// The joining player, before taking the host's save: true to go on, false when updating instead
        /// </summary>
        internal static IEnumerator Gate(Action<bool> Done)
        {
            float Until = Time.realtimeSinceStartup + WaitForHost;
            while (Partner == null && Time.realtimeSinceStartup < Until && CoopSync.InSession)
                yield return null;
            if (!CoopSync.InSession || Partner == Ours)
            {
                Done(true);
                yield break;
            }

            ++Warned;
            string Host = Partner ?? LocalizationManager.GetTranslation("Multiplayer/Version/Unknown");
            string Lines = string.Format(LocalizationManager.GetTranslation("Multiplayer/Version/Lines"), Host, Ours) + "\n\n" +
                           LocalizationManager.GetTranslation("Multiplayer/Version/Risk");
            bool HostNewer = Partner != null && Updater.Newer(Partner, Ours);
            bool CanSuggest = Partner != null && !HostNewer;
            Plugin.Logger?.LogWarning($"[COOP] Mod versions differ: the host's {Host}, ours {Ours}");

            string Choice = null;
            UIMenuConfirmationWindow Window;
            if (HostNewer)
                Window = Open(Lines + "\n" + string.Format(LocalizationManager.GetTranslation("Multiplayer/Version/UpdateToHost"), Partner),
                              "Multiplayer/Version/Update", "Multiplayer/Version/PlayAnyway");
            else if (CanSuggest)
                Window = Open(Lines + "\n" + LocalizationManager.GetTranslation("Multiplayer/Version/HostOlder"),
                              "Multiplayer/Version/Suggest", "Multiplayer/Version/PlayAnyway");
            else
                Window = Open(Lines, "Multiplayer/Version/PlayAnyway", null);
            Window.OnConfirm += () => Choice = HostNewer ? "update" : CanSuggest ? "suggest" : "play";
            Window.OnCancel += () => Choice = "play";

            /* The harness picks for itself, after a frame of the window; back up after updating, it plays */
            if (AutoChoice != null)
            {
                yield return Shoot("join");
                Choice = AutoChoice;
                if (Choice == "update" && File.Exists(Updater.TakenMark))
                {
                    Plugin.Logger?.LogInfo($"[UPDATE] Joined again after taking {File.ReadAllText(Updater.TakenMark)}");
                    File.Delete(Updater.TakenMark);
                    Choice = "play";
                }
                else if (Choice == "update")
                {
                    File.WriteAllText(Updater.TakenMark, Partner);
                }
                Window.Hide();
            }
            while (Choice == null && CoopSync.InSession)
                yield return null;
            Plugin.Logger?.LogInfo($"[COOP] Mod versions differ, the joining player chose: {Choice}");

            switch (Choice)
            {
                case "update" when HostNewer:
                    Done(false);

                    /* Updating didn't work: playing on is still the player's to choose, so the join goes on */
                    Updater.TakeVersion(Partner, SteamBridge.JoinedHost, HostSave.ContinueJoin);
                    yield break;
                case "suggest" when CanSuggest:
                    ++Suggested;
                    CoopSync.Send(CoopSync.Kind.UpdateSuggest, Writer => Writer.Write(Ours));
                    break;
            }
            Done(true);
        }

        /// <summary>
        /// The host: the friend asks us to update to their version
        /// </summary>
        internal static void OnSuggest(BinaryReader Reader)
        {
            string Theirs = Reader.ReadString();
            if (!Plugin.GlobalsInternal.IsServerCreator)
                return;
            Plugin.Logger?.LogInfo($"[COOP] The friend asks us to update to {Theirs} (ours {Ours})");
            string Lines = string.Format(LocalizationManager.GetTranslation("Multiplayer/Version/FriendLines"), Theirs, Ours) + "\n\n" +
                           LocalizationManager.GetTranslation("Multiplayer/Version/HostUpdate");
            UIMenuConfirmationWindow Window = Open(Lines, "Multiplayer/Version/Update", "Multiplayer/Version/NotNow",
                                                   LocalizationManager.GetTranslation("Multiplayer/Version/FriendAsks"));
            Window.OnConfirm += () =>
            {
                ++SuggestionsTaken;
                Updater.TakeVersion(Theirs, 0);
            };
            if (ShotsDir != null)
                Plugin.MonoInstance.StartCoroutine(Shoot("suggested"));
        }

        /// <summary>
        /// The game's own confirmation window, with our words on its buttons
        /// </summary>
        private static UIMenuConfirmationWindow Open(string Body, string Yes, string No, string Header = null)
        {
            UIMenuConfirmationWindow Window = MonoSingleton<UIManager>.Instance.ConfirmationWindowTemplate.Instantiate();
            Window.Show();
            Window.Configure(Header ?? LocalizationManager.GetTranslation("Multiplayer/Version/Title"), Body, acceptOnly: No == null);
            Label(Window, "_confirmButton", Yes);
            if (No != null)
                Label(Window, "_cancelButton", No);
            return Window;
        }

        private static void Label(UIMenuConfirmationWindow Window, string Button, string Key)
        {
            try
            {
                Component Found = AccessTools.Field(typeof(UIMenuConfirmationWindow), Button)?.GetValue(Window) as Component;
                TMP_Text Text = Found?.GetComponentInChildren<TMP_Text>(true);
                if (Text == null)
                    return;
                Localize Translated = Text.GetComponent<Localize>();
                if (Translated != null)
                    Translated.enabled = false;
                Text.text = LocalizationManager.GetTranslation(Key);
            }
            catch (Exception e)
            {
                PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.UI_COMPONENT, $"Can't label {Button}: {e.Message}");
            }
        }

        private static IEnumerator Shoot(string Name)
        {
            yield return new WaitForSecondsRealtime(1.5f);
            if (ShotsDir == null)
                yield break;
            Directory.CreateDirectory(ShotsDir);
            ScreenCapture.CaptureScreenshot(Path.Combine(ShotsDir, $"version-{(Plugin.GlobalsInternal.IsServerCreator ? "host" : "client")}-{Name}.png"));
        }
    }
}

/* EOF */
