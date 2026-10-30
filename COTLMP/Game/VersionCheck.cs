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

        /* The oldest partner mod the host lets play on its save: older ones take what the DLCs give out of it */
        private const string Oldest = "0.9.4";

        /* What an old friend's mod shows of the refusal: its chat (two lines, it can't know our words) */
        private const string RefusedEn = "Your mod is too old for this host: 0.9.4 or newer is needed. Update in the game's main menu.";
        private const string RefusedRu = "Ваш мод слишком старый для этого хоста: нужна 0.9.4 или новее. Обновитесь в главном меню игры.";

        /* An old friend's chat keeps a line 10 s and clears it when the session ends: it is let go after that */
        private const float LinesShown = 11.5f;

        /// <summary>
        /// The host turned the friend now joined away (a mod before 0.9.4, or none it names): nothing of theirs goes
        /// into our cult, our save doesn't go to them
        /// </summary>
        internal static bool Refused { get; private set; }

        /* This friend's joining: every friend says hello first (all versions), so whatever was known of the one
           before is gone; a step that waited across a joining stops */
        private static int Joining;

        private static bool Told, LettingGo;

        /* The Steam peer of the friend now joined (the newest one when its hello came): only they are let go */
        private static ulong FriendPeer;

        private static string Ours => Updater.Version;

        internal static void Reset()
        {
            Partner = null;
            Refused = Told = LettingGo = false;
            ++Joining;
        }

        /// <summary>
        /// The partner's mod may play on the host's save: a version that reads as one, 0.9.4 or newer
        /// </summary>
        internal static bool PartnerFit => Partner != null && !Updater.Newer(Oldest, Partner);

        /// <summary>
        /// The host: a friend joined (its hello). What was known of the friend before goes; one that names no
        /// version in a few seconds (mods before 0.2.9 never do) is refused like an old one.
        /// </summary>
        internal static void OnFriendHello()
        {
            if (!Plugin.GlobalsInternal.IsServerCreator)
                return;
            Reset();
            FriendPeer = SteamBridge.NewestPeer;
            Plugin.MonoInstance.StartCoroutine(AwaitVersion(Joining));
        }

        /// <summary>
        /// The host: a friend's connection went (they left, or were let go). Nothing known of them stays for the
        /// next one, and a step waiting on them stops.
        /// </summary>
        internal static void OnFriendGone(ulong Peer)
        {
            if (!Plugin.GlobalsInternal.IsServerCreator || (FriendPeer != 0 && Peer != FriendPeer))
                return;
            Partner = null;
            Refused = Told = LettingGo = false;
            FriendPeer = 0;
            ++Joining;
            IntroCoop.GoAlone("The friend left");
        }

        private static IEnumerator AwaitVersion(int This)
        {
            float Until = Time.realtimeSinceStartup + WaitForHost;
            while (Partner == null && Time.realtimeSinceStartup < Until && CoopSync.InSession && This == Joining)
                yield return null;
            if (This == Joining && CoopSync.InSession && Partner == null)
                Refuse();
        }

        /* An unfit friend: refused at once (nothing of theirs is taken), the host told. The friend's own mod shows it
           the versions and may update right there; it is let go only if it asks for our save anyway, or when we
           start the game */
        private static void Refuse()
        {
            if (Refused)
                return;
            Refused = true;
            Plugin.Logger?.LogWarning($"[COOP] The friend's mod is {Partner ?? "unknown"}, older than {Oldest}: nothing of theirs is taken, our save isn't sent");
            TellHost();
            LetGo();
            IntroCoop.GoAlone("The friend was refused");
        }

        /// <summary>
        /// The host: a friend asked for our save. Only a fit one gets it; a refused one hears why and is let go.
        /// </summary>
        internal static void OnSaveAsked(uint From, Action<uint> Send)
        {
            /* A late ask from a friend already gone: nobody to answer */
            if (FriendPeer == 0)
                return;
            if (PartnerFit && !Refused)
            {
                Send(From);
                return;
            }
            Refuse();
            LetGo();
        }

        /// <summary>
        /// The host is about to start the game from the lobby: a refused friend doesn't come along
        /// </summary>
        internal static bool KeepOutOfGame()
        {
            if (!Plugin.GlobalsInternal.IsServerCreator || !Refused)
                return false;
            LetGo();
            return true;
        }

        /// <summary>
        /// A deferred save still goes to the friend it was asked for: the same joining, still fit
        /// </summary>
        internal static int JoiningNow => Joining;

        internal static bool StillFit(int AskedIn) => AskedIn == Joining && PartnerFit && !Refused;

        /// <summary>
        /// The friend now joined was refused (the host's own game takes nothing of theirs, not even as a partner)
        /// </summary>
        internal static bool KeptOut => Plugin.GlobalsInternal.IsServerCreator && Refused;

        private static void LetGo()
        {
            if (LettingGo)
                return;
            LettingGo = true;
            Plugin.MonoInstance.StartCoroutine(SayAndLetGo(Joining));
        }

        private static IEnumerator SayAndLetGo(int This)
        {
            string Name = SteamBridge.LocalName() ?? "";
            foreach (string Line in new[] { RefusedEn, RefusedRu })
            {
                CoopSync.Send(CoopSync.Kind.ChatLine, Writer =>
                {
                    Writer.Write(Name);
                    Writer.Write(Line);
                });
            }
            Plugin.Logger?.LogInfo($"[COOP] Told the friend why, letting them go in {LinesShown:F0} s");
            yield return new WaitForSecondsRealtime(LinesShown);
            /* Only the friend refused: no peer known means nobody, never everyone */
            if (This == Joining && Refused && CoopSync.InSession && FriendPeer != 0)
                SteamBridge.LetPartnersGo(FriendPeer);
            if (This == Joining)
                LettingGo = false;
        }

        /* Once for each friend: the host hears why the friend can't come in. In the lobby a window (the game's
           notifications don't show there), in the game a notification (a window wouldn't stop the game) */
        private static void TellHost()
        {
            if (Told)
                return;
            Told = true;
            try
            {
                string Theirs = Partner ?? LocalizationManager.GetTranslation("Multiplayer/Version/Unknown");
                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Main Menu")
                    Open(string.Format(LocalizationManager.GetTranslation("Multiplayer/Version/FriendRefused"), Theirs), null, null);
                else
                    NotificationCentre.Instance?.PlayGenericNotificationNonLocalizedParams("Multiplayer/Version/FriendRefused", Theirs);
                Plugin.MonoInstance.StartCoroutine(Shoot("refused"));
            }
            catch (Exception e)
            {
                PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Can't show why the friend can't join: {e.Message}");
            }
        }

        /// <summary>
        /// Ours, to the partner: the joining player on joining, the host in answer
        /// </summary>
        internal static void Announce()
        {
            CoopSync.Send(CoopSync.Kind.ModVersion, Writer => Writer.Write(Ours));
            PatchCheck.Announce();
        }

        internal static void OnVersion(BinaryReader Reader)
        {
            string Theirs = Reader.ReadString();
            /* Only a version that can name a release: anything else is an unknown one (no update to it) */
            if (!COTLMPServer.Data.ModVersionText.IsValid(Theirs))
            {
                Plugin.Logger?.LogWarning($"[COOP] The partner's mod version doesn't read as one ({Theirs?.Length ?? 0} characters): taken as unknown");
                Theirs = null;
            }
            /* The host: a late version from a friend already gone (no new one said hello) is nobody's */
            if (Plugin.GlobalsInternal.IsServerCreator && FriendPeer == 0)
                return;
            Partner = Theirs;
            Plugin.Logger?.LogInfo($"[COOP] The partner's mod is {Theirs ?? "unknown"}, ours {Ours}");
            if (!Plugin.GlobalsInternal.IsServerCreator)
                return;

            /* The host answers every joining (the friend may come back on another version), says where it
               is (in the lobby or in a game), and hears of a different version as a note: the joining
               player decides */
            Announce();
            /* A friend whose mod would take what the DLCs give out of our save: refused now and let go; the
               lobby's phase isn't theirs to hear */
            if (!PartnerFit)
            {
                Refuse();
                return;
            }
            Lobby.Send();
            if (Theirs != Ours)
            {
                try
                {
                    NotificationCentre.Instance?.PlayGenericNotificationNonLocalizedParams("Multiplayer/Version/FriendHas",
                        Theirs ?? LocalizationManager.GetTranslation("Multiplayer/Version/Unknown"), Ours);
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
                Window = Open(Lines + "\n" + LocalizationManager.GetTranslation("Multiplayer/Version/HostOlder") +
                              /* A host before 0.9.4 takes what the DLCs give out of its own save */
                              (Updater.Newer(Oldest, Partner) ? "\n" + string.Format(LocalizationManager.GetTranslation("Multiplayer/Version/HostStrips"), Partner) : ""),
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
            if (!COTLMPServer.Data.ModVersionText.IsValid(Theirs))
            {
                Plugin.Logger?.LogWarning($"[COOP] The friend asks us to update to a version that doesn't read as one ({Theirs?.Length ?? 0} characters): not offered");
                return;
            }
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
        internal static UIMenuConfirmationWindow Open(string Body, string Yes, string No, string Header = null)
        {
            UIMenuConfirmationWindow Window = MonoSingleton<UIManager>.Instance.ConfirmationWindowTemplate.Instantiate();
            Window.Show();
            Window.Configure(Header ?? LocalizationManager.GetTranslation("Multiplayer/Version/Title"), Body, acceptOnly: No == null);
            if (Yes != null)
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
