/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Command line switches that let two copies of the game test the mod unattended
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP.Data;
using COTLMP.Game;
using COTLMP.Ui;
using MMTools;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Debug
{
    /// <summary>
    /// Test-only automation. Nothing here runs unless one of its switches is on the command line:
    /// <list type="bullet">
    /// <item><c>-cotlmp-autohost</c>: go from the main menu into the cult and start a server</item>
    /// <item><c>-cotlmp-autoenter</c>: go from the main menu into the cult (for the joining copy)</item>
    /// <item><c>-cotlmp-autowalk</c>: once connected, walk the local lamb in a circle</item>
    /// <item><c>-cotlmp-report</c>: log the local and remote lamb positions every 2 seconds</item>
    /// <item><c>-cotlmp-quitafter=N</c>: quit the game after N seconds</item>
    /// <item><c>-cotlmp-endafter=N</c>: leave or stop hosting N seconds after the session starts</item>
    /// <item><c>-cotlmp-rejoin=HOST</c>: with -cotlmp-endafter, join the loopback host again after leaving</item>
    /// <item><c>-cotlmp-autodungeon=N</c>: N seconds after the session starts, start a run and enter the first dungeon</item>
    /// <item><c>-cotlmp-autokill</c>: in a dungeon, hit the nearest enemy every second (shared enemy health test)</item>
    /// <item><c>-cotlmp-autodie=N</c>: N seconds after entering a dungeon, die once (knockout test); the other copy walks over to revive</item>
    /// <item><c>-cotlmp-window=X,Y,W,H</c>: keep the game in a W x H window at X,Y (the game's own settings would make it full screen again on load)</item>
    /// <item><c>-cotlmp-tarottest</c>: in the first dungeon room both have, a tarot reader deals two cards to the host, then to the joining copy (shared tarot test)</item>
    /// <item><c>-cotlmp-autocut</c>: in a dungeon room shared with the partner, cut the nearest grass or other neutral thing every 2 s (prop sync test)</item>
    /// <item><c>-cotlmp-nextfloor</c>: in a dungeon, once no unvisited room is left, go to the floor's exit and through it; on the map the host clicks the last way out after 3 s, the joining copy (which walks through the exit itself once both are in that room) the first after 0.5 s; whoever is first, the other is taken there (map sync test)</item>
    /// <item><c>-cotlmp-lobby=SLOT|new</c>: open the lobby from the main menu on that save slot (or a new game in the hidden slot 6) and press Play a while after a friend stands in it</item>
    /// <item><c>-cotlmp-lobbyplay=N</c>: with -cotlmp-lobby, press Play N seconds after the friend came (6 by default)</item>
    /// <item><c>-cotlmp-lobbyjoin</c>: join the first friend's lobby from the online menu</item>
    /// <item><c>-cotlmp-lobbyshots=DIR</c>: frames of the online menu, the lobby and the opening's crown</item>
    /// <item><c>-cotlmp-skipintro=N</c>: N seconds into a new game's opening, go straight to the crown</item>
    /// <item><c>-cotlmp-leaveintro=N</c>: with -cotlmp-skipintro, leave the opening for the cult N seconds after coming back to life</item>
    /// <item><c>-cotlmp-pretendversion=0.2.6</c>: the update check takes this mod for that version</item>
    /// <item><c>-cotlmp-updatefrom=URL</c>: the release is read from there instead of GitHub (a file:/// one for tests)</item>
    /// <item><c>-cotlmp-versionchoice=update|play|suggest</c>: what the joining player picks when the mod versions differ</item>
    /// <item><c>-cotlmp-versionshots=DIR</c>: frames of the version windows</item>
    /// <item><c>-cotlmp-takeupdate</c>: an update offered is taken at once, the installer runs without its window</item>
    /// <item><c>-cotlmp-foxtest</c>: both go to the shore at night; the friend tries to talk to the night fox (refused), then the host makes its deal</item>
    /// <item><c>-cotlmp-culttest</c>: in the cult, each side changes it (the host: wood, a bed, a new follower; the friend: stone, a torch, a follower's name, the bed gone) for the other to show</item>
    /// <item><c>-cotlmp-hudtrace</c>: log every time the HUD is hidden or shown, with who asked</item>
    /// <item><c>-cotlmp-templetest</c>: the host builds a temple and gets three followers and bones; the friend goes in, preaches a sermon, holds a holiday and closes the altar through its menus</item>
    /// <item><c>-cotlmp-animtest</c>: every 3 s, while the partner is in the room, play one of the lamb's attack animations (puppet animation test)</item>
    /// <item><c>-cotlmp-pausetest</c>: every 8 s open the pause menu and log whether it is still open 2 s later; <c>-cotlmp-pausenoplayer</c> does it with no lamb in the player list, as in the game's opening</item>
    /// <item><c>-cotlmp-nomapclick</c>: with -cotlmp-nextfloor, never click on the map, only wait for the partner's pick</item>
    /// <item><c>-cotlmp-solo</c>: the host fights and moves on without waiting for the partner (cleared-room test); the joining copy walks the host's trail</item>
    /// <item><c>-cotlmp-autotalk=N</c>: N seconds after both stand in one dungeon room, hold a short conversation (dialog sync test)</item>
    /// <item><c>-cotlmp-autoshoot</c>: while running a shared room, fire an enemy projectile at the partner every 3 s (projectile sync test)</item>
    /// <item><c>-cotlmp-autochoice=N</c>: N s after both stand in the cult, answer together: the host asks, sees which answer the partner points at and picks it; then the joining player asks, clicks an answer (it only points) and gets the host's pick (dialog choice test)</item>
    /// <item><c>-cotlmp-netshots</c>: with -cotlmp-shots, save a frame the first time each kind of bad connection icon shows</item>
    /// <item><c>-cotlmp-netflood</c>: every 3 s send the partner a 20 KB packet and a burst of 300 small ones of a kind they ignore (network stress test; pair it with -cotlmp-netchaos=LOSS,DELAY,JITTER,EVERY,OUTAGE for a bad connection)</item>
    /// <item><c>-cotlmp-drawtest</c>: in each shared dungeon room draw 3 tarot cards the way a chest would and log them (host pools test); the host also gets extra unlocks in memory so the two saves differ</item>
    /// <item><c>-cotlmp-hurt=N</c>: N seconds after both stand in one dungeon room, the host's hearts go to two, one and back (bruises test); the joining copy logs and frames the looks the host's puppet takes on</item>
    /// <item><c>-cotlmp-runcount=N</c>: our save has had N runs (the grass of the first land grows from it), so two copies' saves differ; <c>-cotlmp-nograssfix</c> grows the grass from our own save as before</item>
    /// <item><c>-cotlmp-shots=DIR</c>: save the game's own frame to DIR a few seconds into every room shared with the partner</item>
    /// <item><c>-cotlmp-steamselftest</c>: after hosting, send a packet to our own SteamID over real Steam</item>
    /// </list>
    /// </summary>
    internal static class TestHarness
    {
        private static bool AutoHost, AutoEnter, AutoWalk, Report, SteamSelfTest, AutoKill, LobbyJoin, FoxTest;
        private static float QuitAfter, EndAfter, AutoDungeon;
        private static ulong RejoinHost;
        private static string ShotsDir;
        private static float AutoDie;
        private static float AutoHurt;
        private static bool Solo;
        private static bool NextFloor;
        private static bool NoMapClick;
        private static bool AnimTest, CultTest, TempleTest;
        private static bool PauseTest, PauseNoPlayer;
        private static bool TarotTest;
        private static bool AutoCut;
        private static int[] WindowRect;
        private static float AutoTalk;
        private static bool AutoShoot;
        private static bool DrawTest;
        private static int RunCount = -1;
        private static bool NetFlood, NetShots;
        private static float AutoChoice;
        private static int FloodSent;

        private static string Tag => $"[QA {(Plugin.GlobalsInternal.IsServerCreator ? "host" : "client")}]";

        /// <summary>
        /// Reads the switches and starts the coroutines they ask for.
        /// </summary>
        public static void Initialize()
        {
            foreach (string Arg in Environment.GetCommandLineArgs())
            {
                if (Arg.Equals("-cotlmp-autohost", StringComparison.OrdinalIgnoreCase))
                    AutoHost = true;
                else if (Arg.Equals("-cotlmp-autoenter", StringComparison.OrdinalIgnoreCase))
                    AutoEnter = true;
                else if (Arg.StartsWith("-cotlmp-rejoin=", StringComparison.OrdinalIgnoreCase))
                    ulong.TryParse(Arg.Substring("-cotlmp-rejoin=".Length), out RejoinHost);
                else if (Arg.StartsWith("-cotlmp-autodungeon=", StringComparison.OrdinalIgnoreCase))
                    float.TryParse(Arg.Substring("-cotlmp-autodungeon=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out AutoDungeon);
                else if (Arg.StartsWith("-cotlmp-autotalk=", StringComparison.OrdinalIgnoreCase))
                    float.TryParse(Arg.Substring("-cotlmp-autotalk=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out AutoTalk);
                else if (Arg.Equals("-cotlmp-netflood", StringComparison.OrdinalIgnoreCase))
                    NetFlood = true;
                else if (Arg.Equals("-cotlmp-netshots", StringComparison.OrdinalIgnoreCase))
                    NetShots = true;
                else if (Arg.StartsWith("-cotlmp-autochoice=", StringComparison.OrdinalIgnoreCase))
                    float.TryParse(Arg.Substring("-cotlmp-autochoice=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out AutoChoice);
                else if (Arg.Equals("-cotlmp-drawtest", StringComparison.OrdinalIgnoreCase))
                    DrawTest = true;
                else if (Arg.StartsWith("-cotlmp-runcount=", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(Arg.Substring("-cotlmp-runcount=".Length), out RunCount);
                else if (Arg.Equals("-cotlmp-nograssfix", StringComparison.OrdinalIgnoreCase))
                    COTLMP.Game.RunGrass.Off = true;
                else if (Arg.Equals("-cotlmp-autoshoot", StringComparison.OrdinalIgnoreCase))
                    AutoShoot = true;
                else if (Arg.Equals("-cotlmp-solo", StringComparison.OrdinalIgnoreCase))
                    Solo = true;
                else if (Arg.Equals("-cotlmp-nextfloor", StringComparison.OrdinalIgnoreCase))
                    NextFloor = true;
                else if (Arg.Equals("-cotlmp-nomapclick", StringComparison.OrdinalIgnoreCase))
                    NoMapClick = true;
                else if (Arg.Equals("-cotlmp-animtest", StringComparison.OrdinalIgnoreCase))
                    AnimTest = true;
                else if (Arg.StartsWith("-cotlmp-pretendversion=", StringComparison.OrdinalIgnoreCase))
                    COTLMP.Network.Updater.PretendVersion = Arg.Substring("-cotlmp-pretendversion=".Length);
                else if (Arg.StartsWith("-cotlmp-versionchoice=", StringComparison.OrdinalIgnoreCase))
                    COTLMP.Game.VersionCheck.AutoChoice = Arg.Substring("-cotlmp-versionchoice=".Length);
                else if (Arg.StartsWith("-cotlmp-versionshots=", StringComparison.OrdinalIgnoreCase))
                    COTLMP.Game.VersionCheck.ShotsDir = Arg.Substring("-cotlmp-versionshots=".Length);
                else if (Arg.StartsWith("-cotlmp-lobby=", StringComparison.OrdinalIgnoreCase))
                    COTLMP.Game.Lobby.AutoLobby = Arg.Substring("-cotlmp-lobby=".Length);
                else if (Arg.StartsWith("-cotlmp-lobbyplay=", StringComparison.OrdinalIgnoreCase))
                    float.TryParse(Arg.Substring("-cotlmp-lobbyplay=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out COTLMP.Game.Lobby.AutoPlay);
                else if (Arg.Equals("-cotlmp-lobbyjoin", StringComparison.OrdinalIgnoreCase))
                    LobbyJoin = true;
                else if (Arg.StartsWith("-cotlmp-lobbyshots=", StringComparison.OrdinalIgnoreCase))
                    LobbyShots.Dir = Arg.Substring("-cotlmp-lobbyshots=".Length);
                else if (Arg.StartsWith("-cotlmp-skipintro=", StringComparison.OrdinalIgnoreCase))
                    float.TryParse(Arg.Substring("-cotlmp-skipintro=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out COTLMP.Game.IntroCoop.SkipTo);
                else if (Arg.StartsWith("-cotlmp-leaveintro=", StringComparison.OrdinalIgnoreCase))
                    float.TryParse(Arg.Substring("-cotlmp-leaveintro=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out COTLMP.Game.IntroCoop.LeaveAfter);
                else if (Arg.StartsWith("-cotlmp-updateshot=", StringComparison.OrdinalIgnoreCase))
                    COTLMP.Network.Updater.OfferShot = Arg.Substring("-cotlmp-updateshot=".Length);
                else if (Arg.StartsWith("-cotlmp-updatefrom=", StringComparison.OrdinalIgnoreCase))
                    COTLMP.Network.Updater.ReleaseFrom = Arg.Substring("-cotlmp-updatefrom=".Length);
                else if (Arg.Equals("-cotlmp-takeupdate", StringComparison.OrdinalIgnoreCase))
                    COTLMP.Network.Updater.TakeOffer = COTLMP.Network.Updater.QuietInstaller = true;
                else if (Arg.Equals("-cotlmp-foxtest", StringComparison.OrdinalIgnoreCase))
                    FoxTest = true;
                else if (Arg.Equals("-cotlmp-culttest", StringComparison.OrdinalIgnoreCase))
                    CultTest = true;
                else if (Arg.Equals("-cotlmp-templetest", StringComparison.OrdinalIgnoreCase))
                    TempleTest = true;
                else if (Arg.Equals("-cotlmp-hudtrace", StringComparison.OrdinalIgnoreCase))
                    TraceHud();
                else if (Arg.Equals("-cotlmp-pausetest", StringComparison.OrdinalIgnoreCase))
                    PauseTest = true;
                else if (Arg.Equals("-cotlmp-pausenoplayer", StringComparison.OrdinalIgnoreCase))
                    PauseTest = PauseNoPlayer = true;
                else if (Arg.Equals("-cotlmp-tarottest", StringComparison.OrdinalIgnoreCase))
                    TarotTest = true;
                else if (Arg.Equals("-cotlmp-autocut", StringComparison.OrdinalIgnoreCase))
                    AutoCut = true;
                else if (Arg.StartsWith("-cotlmp-window=", StringComparison.OrdinalIgnoreCase))
                {
                    string[] Parts = Arg.Substring("-cotlmp-window=".Length).Split(',');
                    if (Parts.Length == 4)
                        WindowRect = Array.ConvertAll(Parts, Part => int.TryParse(Part, out int Value) ? Value : 0);
                }
                else if (Arg.StartsWith("-cotlmp-autodie=", StringComparison.OrdinalIgnoreCase))
                    float.TryParse(Arg.Substring("-cotlmp-autodie=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out AutoDie);
                else if (Arg.StartsWith("-cotlmp-hurt=", StringComparison.OrdinalIgnoreCase))
                    float.TryParse(Arg.Substring("-cotlmp-hurt=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out AutoHurt);
                else if (Arg.StartsWith("-cotlmp-shots=", StringComparison.OrdinalIgnoreCase))
                    ShotsDir = Arg.Substring("-cotlmp-shots=".Length);
                else if (Arg.Equals("-cotlmp-autokill", StringComparison.OrdinalIgnoreCase))
                    AutoKill = true;
                else if (Arg.Equals("-cotlmp-steamselftest", StringComparison.OrdinalIgnoreCase))
                    SteamSelfTest = true;
                else if (Arg.Equals("-cotlmp-autowalk", StringComparison.OrdinalIgnoreCase))
                    AutoWalk = true;
                else if (Arg.Equals("-cotlmp-report", StringComparison.OrdinalIgnoreCase))
                    Report = true;
                else if (Arg.StartsWith("-cotlmp-quitafter=", StringComparison.OrdinalIgnoreCase))
                    float.TryParse(Arg.Substring("-cotlmp-quitafter=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out QuitAfter);
                else if (Arg.StartsWith("-cotlmp-endafter=", StringComparison.OrdinalIgnoreCase))
                    float.TryParse(Arg.Substring("-cotlmp-endafter=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out EndAfter);
            }

            if (!AutoHost && !AutoEnter && !AutoWalk && !Report && QuitAfter <= 0 && EndAfter <= 0)
                return;

            /* Two copies side by side: the one without focus must keep running */
            Application.runInBackground = true;
            Plugin.Logger?.LogWarning($"[QA] Test switches on: autohost={AutoHost} autoenter={AutoEnter} autowalk={AutoWalk} report={Report} quitafter={QuitAfter}");

            if (WindowRect != null)
                Plugin.MonoInstance.StartCoroutine(KeepWindow());
            if (AnimTest)
                Plugin.MonoInstance.StartCoroutine(PlayAttacks());
            if (CultTest)
                Plugin.MonoInstance.StartCoroutine(ChangeCult());
            if (TempleTest)
                Plugin.MonoInstance.StartCoroutine(TempleRun());
            if (PauseTest)
                Plugin.MonoInstance.StartCoroutine(OpenPauseMenus());
            if (NextFloor && AutoEnter && !NoMapClick)
                Plugin.MonoInstance.StartCoroutine(ClickMaps());
            if (TarotTest)
                Plugin.MonoInstance.StartCoroutine(DrawTarot());
            if (AutoCut)
                Plugin.MonoInstance.StartCoroutine(CutThings());
            if (AutoHost || AutoEnter)
                Plugin.MonoInstance.StartCoroutine(EnterCult(AutoHost));
            PressStart.On = COTLMP.Game.Lobby.AutoLobby != null || LobbyJoin;
            if (COTLMP.Game.Lobby.AutoLobby != null)
                Plugin.MonoInstance.StartCoroutine(COTLMP.Game.Lobby.AutoHost());
            if (LobbyJoin)
                Plugin.MonoInstance.StartCoroutine(COTLMP.Ui.OnlineMenu.AutoJoin());
            if (FoxTest)
                Plugin.MonoInstance.StartCoroutine(MeetFox());
            if (COTLMP.Game.IntroCoop.SkipTo >= 0f)
                Plugin.MonoInstance.StartCoroutine(COTLMP.Game.IntroCoop.AutoCrown());
            if ((COTLMP.Game.Lobby.AutoLobby != null || LobbyJoin || COTLMP.Game.IntroCoop.SkipTo >= 0f) && !(AutoHost || AutoEnter))
                Plugin.MonoInstance.StartCoroutine(AdvanceConversations());
            if (AutoHost || AutoEnter)
                Plugin.MonoInstance.StartCoroutine(AdvanceConversations());
            if (AutoWalk)
                Plugin.MonoInstance.StartCoroutine(Walk());
            if (Report)
                Plugin.MonoInstance.StartCoroutine(ReportLoop());
            if (QuitAfter > 0)
                Plugin.MonoInstance.StartCoroutine(QuitLater());
            if (EndAfter > 0)
                Plugin.MonoInstance.StartCoroutine(EndLater());
            if (AutoDungeon > 0)
                Plugin.MonoInstance.StartCoroutine(EnterDungeon());
            if (AutoKill)
                Plugin.MonoInstance.StartCoroutine(HitEnemies());
            /* The voting test takes its own frames */
            if (!string.IsNullOrEmpty(ShotsDir) && NetShots)
                Plugin.MonoInstance.StartCoroutine(NetIconShots());
            else if (!string.IsNullOrEmpty(ShotsDir) && AutoChoice <= 0)
                Plugin.MonoInstance.StartCoroutine(Shots());
            if (!string.IsNullOrEmpty(ShotsDir))
                Plugin.MonoInstance.StartCoroutine(MapShots());
            if (AutoDie > 0)
                Plugin.MonoInstance.StartCoroutine(DieOnce());
            if (AutoHurt > 0)
                Plugin.MonoInstance.StartCoroutine(HurtTest());
            if (AutoTalk > 0)
                Plugin.MonoInstance.StartCoroutine(TalkOnce());
            if (AutoShoot)
                Plugin.MonoInstance.StartCoroutine(Shoot());
            if (DrawTest)
                Plugin.MonoInstance.StartCoroutine(Draw());
            if (NetFlood)
                Plugin.MonoInstance.StartCoroutine(Flood());
            if (AutoChoice > 0)
                Plugin.MonoInstance.StartCoroutine(VoteOnChoices());
            if (AutoKill)
                Plugin.MonoInstance.StartCoroutine(Rescue());
        }

        private static IEnumerator EnterCult(bool Host)
        {
            while (SceneManager.GetActiveScene().name != "Main Menu" || MMTransition.IsPlaying)
                yield return null;
            yield return new WaitForSecondsRealtime(3f);

            Plugin.Logger?.LogWarning($"{Tag} Entering the cult without a save");

            /* Whatever the game saves goes to a slot no menu shows (the save folder is the player's own) */
            SaveAndLoad.SAVE_SLOT = 6;
            AudioManager.Instance?.StopCurrentMusic();
            MMTransition.Play(MMTransition.TransitionType.ChangeRoomWaitToResume, MMTransition.Effect.BlackFade, "Base Biome 1", 2, "", null, null);

            while (PlayerFarming.Instance == null)
                yield return null;
            yield return new WaitForSecondsRealtime(2f);

            /*
             * Without a loaded save the game never unpauses the scene (BiomeBaseManager
             * resumes play only after loading one). The test copies have no saves, so
             * unpause by hand. Real players always join from their own loaded save.
             */
            MMTransition.CanResume = true;
            MMTransition.ResumePlay();
            Plugin.MonoInstance.StartCoroutine(KeepTimeRunning());

            /*
             * A fresh save shows the dodge tutorial on the first hits and refuses the damage while
             * it does. Real saves are past it; mark the test copies the same way.
             */
            DataManager.Instance.ShownDodgeTutorial = true;
            DataManager.Instance.ShownDodgeTutorialCount = 3;

            /* The same for the difficulty a fresh save asks for on arrival (it covers the screen) */
            DataManager.Instance.DifficultyChosen = true;
            Plugin.MonoInstance.StartCoroutine(SkipDifficulty());
            /* As loading a save does (the friend's copy gets it with the host's save) */
            DifficultyManager.ForceDifficulty(DataManager.Instance.MetaData.Difficulty);
            if (RunCount >= 0)
            {
                DataManager.Instance.dungeonRun = RunCount;
                Plugin.Logger?.LogWarning($"{Tag} Our save has had {RunCount} runs");
            }
            yield return new WaitForSecondsRealtime(1f);

            if (Host)
            {
                bool Started = PauseMenuPatches.StartHosting();
                Plugin.Logger?.LogWarning($"[QA host] StartHosting returned {Started}, server port {PauseMenuPatches.Server?.Port}");

                if (SteamSelfTest)
                {
                    yield return new WaitForSecondsRealtime(3f);
                    COTLMP.Network.SteamBridge.SendSelfTest();
                }
            }
        }

        /// <summary>
        /// Something keeps a save-less scene at timeScale 0, so hold it at 1 for the test copies
        /// </summary>
        private static IEnumerator KeepTimeRunning()
        {
            while (true)
            {
                /* Leave the pause menu its pause, so it opens in the test copies too */
            /* (conversations are advanced by AdvanceConversations, nobody presses a button here) */
                if (Time.timeScale == 0f && !Lamb.UI.UIMenuBase.ActiveMenus.Exists(Menu => Menu is Lamb.UI.PauseMenu.UIPauseMenuController))
                    Time.timeScale = 1f;
                yield return null;
            }
        }

        private static IEnumerator Walk()
        {
            Vector3 Center = Vector3.zero;
            PlayerFarming Walker = null;
            float Started = 0;

            while (true)
            {
                yield return null;

                if (!Plugin.GlobalsInternal.InGameSession || PlayerFarming.Instance == null || MMTransition.IsPlaying)
                {
                    Walker = null;
                    continue;
                }

                if (Walker != PlayerFarming.Instance)
                {
                    Walker = PlayerFarming.Instance;
                    Center = Walker.transform.position;
                    Started = Time.realtimeSinceStartup;
                    Plugin.Logger?.LogWarning($"{Tag} Walking a circle around {Center}");
                }

                /* One lap every 6 seconds, 2 units wide */
                float Angle = (Time.realtimeSinceStartup - Started) * Mathf.PI / 3f;
                Walker.transform.position = Center + new Vector3(Mathf.Cos(Angle), Mathf.Sin(Angle), 0f) * 2f;
            }
        }

        /// <summary>
        /// Both sides of the dialog choice test: whoever asks answers, the other points
        /// </summary>
        private static IEnumerator VoteOnChoices()
        {
            while (SceneManager.GetActiveScene().name != "Base Biome 1" || PlayerFarming.Instance == null || !AnyRemotePlayer())
                yield return null;
            yield return new WaitForSecondsRealtime(AutoChoice);
            bool Host = Plugin.GlobalsInternal.IsServerCreator;

            /* Round 1: the host asks; the joining player points at the second answer, the host answers the second */
            if (Host)
            {
                Ask("host");
                yield return Until(() => COTLMP.Game.Dialogs.OpenWheel != null && COTLMP.Game.Dialogs.PartnerPickNow >= 0, 30f);
                Plugin.Logger?.LogWarning($"{Tag} Round 1: the partner points at {COTLMP.Game.Dialogs.PartnerPickNow}");
                MoveTo(0);
                yield return new WaitForSecondsRealtime(2f);
                Shot("choice-host-own-wheel");
                ClickOwn(COTLMP.Game.Dialogs.PartnerPickNow);
                yield return Until(() => COTLMP.Game.Dialogs.OpenWheel == null, 10f);
                Plugin.Logger?.LogWarning($"{Tag} Round 1: answered, wheel closed: {COTLMP.Game.Dialogs.OpenWheel == null}");
            }
            else
            {
                yield return Until(() => COTLMP.Game.Dialogs.RemoteChoiceCount > 0, 40f);
                Plugin.Logger?.LogWarning($"{Tag} Round 1: the host's answers are up ({COTLMP.Game.Dialogs.RemoteChoiceCount})");
                yield return new WaitForSecondsRealtime(1f);
                COTLMP.Game.Dialogs.Point(1);
                yield return new WaitForSecondsRealtime(0.5f);
                Shot("choice-client-sees-host");
            }

            yield return new WaitForSecondsRealtime(12f);

            /* Round 2: the joining player asks and answers the first right away; the host points at the second */
            if (!Host)
            {
                Ask("client");
                yield return Until(() => COTLMP.Game.Dialogs.OpenWheel != null, 20f);
                yield return new WaitForSecondsRealtime(3f);
                Shot("choice-client-own-wheel");
                MoveTo(0);
                ClickOwn(0);
                yield return Until(() => COTLMP.Game.Dialogs.OpenWheel == null, 10f);
                Plugin.Logger?.LogWarning($"{Tag} Round 2: answered 0 ourselves, wheel closed: {COTLMP.Game.Dialogs.OpenWheel == null}");
            }
            else
            {
                yield return Until(() => COTLMP.Game.Dialogs.RemoteChoiceCount > 0, 40f);
                Plugin.Logger?.LogWarning($"{Tag} Round 2: the partner's answers are up ({COTLMP.Game.Dialogs.RemoteChoiceCount})");
                COTLMP.Game.Dialogs.Point(1);
                yield return new WaitForSecondsRealtime(1f);
                Shot("choice-host-sees-client");
            }
        }

        /// <summary>
        /// Moves the selection of our own wheel to an answer, as the stick or the mouse does
        /// </summary>
        private static void MoveTo(int Answer)
        {
            var Wheel = COTLMP.Game.Dialogs.OpenWheel;
            var Options = Wheel != null ? HarmonyLib.Traverse.Create(Wheel).Field("_options").GetValue<MMTools.DialogueWheel.DialogueOption[]>() : null;
            if (Options == null || Answer < 0 || Answer >= Options.Length)
                return;
            MonoSingleton<src.UINavigator.UINavigatorNew>.Instance.NavigateToNew(Options[Answer].Button);
            Plugin.Logger?.LogWarning($"{Tag} Moved to answer {Answer}");
        }

        private static void Ask(string Who)
        {
            GameObject Me = PlayerFarming.Instance.gameObject;
            var Entries = new System.Collections.Generic.List<MMTools.ConversationEntry>
            {
                new(Me, "Co-op choice test: which one?", "talk", "", null, "Lamb")
            };
            var Responses = new System.Collections.Generic.List<MMTools.Response>
            {
                new("A", () => Plugin.Logger?.LogWarning($"{Tag} Answer callback A ({Who} asked)"), "Multiplayer/UI/StartServer"),
                new("B", () => Plugin.Logger?.LogWarning($"{Tag} Answer callback B ({Who} asked)"), "Multiplayer/UI/LeaveServer")
            };
            Plugin.Logger?.LogWarning($"{Tag} Asking a question with two answers");
            MMTools.MMConversation.Play(new MMTools.ConversationObject(Entries, Responses, null));
        }

        /// <summary>
        /// Clicks one of our own answers the way the mouse does
        /// </summary>
        private static void ClickOwn(int Answer)
        {
            var Wheel = COTLMP.Game.Dialogs.OpenWheel;
            var Options = Wheel != null ? HarmonyLib.Traverse.Create(Wheel).Field("_options").GetValue<MMTools.DialogueWheel.DialogueOption[]>() : null;
            if (Options == null || Answer < 0 || Answer >= Options.Length)
            {
                Plugin.Logger?.LogWarning($"{Tag} No answer {Answer} to click");
                return;
            }
            Options[Answer].Button.onClick.Invoke();
        }

        /// <summary>
        /// Picks the medium difficulty on the selector a fresh save shows when the player arrives, if it is up
        /// </summary>
        private static IEnumerator SkipDifficulty()
        {
            float Start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - Start < 20f)
            {
                var Selector = UnityEngine.Object.FindObjectOfType<Lamb.UI.UIDifficultySelectorOverlayController>();
                if (Selector != null)
                {
                    HarmonyLib.Traverse.Create(Selector).Method("SelectDifficulty", 1).GetValue();
                    Plugin.Logger?.LogWarning($"{Tag} Closed the difficulty selector");
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.5f);
            }
        }

        private static void Shot(string Name)
        {
            if (string.IsNullOrEmpty(ShotsDir))
                return;
            string File = System.IO.Path.Combine(ShotsDir, $"{(Plugin.GlobalsInternal.IsServerCreator ? "host" : "client")}-{Name}.png");
            System.IO.Directory.CreateDirectory(ShotsDir);
            ScreenCapture.CaptureScreenshot(File);
            Plugin.Logger?.LogWarning($"{Tag} Saved {File}");
        }

        private static IEnumerator Until(Func<bool> Condition, float Seconds)
        {
            float Start = Time.realtimeSinceStartup;
            while (!Condition() && Time.realtimeSinceStartup - Start < Seconds)
                yield return null;
        }

        private static string Gear()
        {
            PlayerFarming Player = PlayerFarming.Instance;
            if (Player == null)
                return "gear=-";
            string Hearts(HealthPlayer Health) => Health == null ? "-" : $"{Health.HP}/{Health.totalHP}+b{Health.BlueHearts}+k{Health.BlackHearts}+s{Health.SpiritHearts}";
            PlayerFarming Puppet = Enumerable.Range(0, InternalData.MaxPlayersPerServerInternal).Select(Id => PlayerManager.GetPuppet((uint)Id)).FirstOrDefault(Each => Each != null);
            return $"gear={Player.currentWeapon}:{Player.currentWeaponLevel}/{Player.currentCurse}/{Player.RunTrinkets.Count}cards/{Player.health?.HP}hp hearts={Hearts(Player.health)} puppethearts={Hearts(Puppet?.health)}";
        }

        /// <summary>
        /// Floods the partner with data they don't use: one big packet and a burst of small ones.
        /// </summary>
        private static IEnumerator Flood()
        {
            var Wait = new WaitForSecondsRealtime(3f);
            byte[] Big = new byte[20000];
            new System.Random(5).NextBytes(Big);

            while (true)
            {
                yield return Wait;
                if (!COTLMP.Game.CoopSync.InSession)
                    continue;

                COTLMP.Game.CoopSync.Send((COTLMP.Game.CoopSync.Kind)250, Writer => Writer.Write(Big));
                for (int Index = 0; Index < 300; Index++)
                    COTLMP.Game.CoopSync.Send((COTLMP.Game.CoopSync.Kind)251, Writer => Writer.Write(Index));
                FloodSent += 301;
            }
        }

        private static IEnumerator ReportLoop()
        {
            var Wait = new WaitForSecondsRealtime(2f);
            StringBuilder Line = new();

            while (true)
            {
                yield return Wait;

                Line.Clear();
                Line.Append($"{Tag} place={COTLMP.Game.CoopSync.CurrentPlace()} seed={COTLMP.Game.CoopSync.LastSeed}/{MMBiomeGeneration.BiomeGenerator.Instance?.Seed} enemies={Health.team2.Count} seen={COTLMP.Game.CoopSync.EnemiesSeen} hpsent={COTLMP.Game.CoopSync.HitsSent} hpapplied={COTLMP.Game.CoopSync.HitsApplied} killsapplied={COTLMP.Game.CoopSync.KillsApplied}");
                Line.Append($" own={COTLMP.Game.RoomSync.Owning} follow={COTLMP.Game.RoomSync.Following} snapsent={COTLMP.Game.RoomSync.SnapshotsSent} snapapplied={COTLMP.Game.RoomSync.SnapshotsApplied} puppethits={COTLMP.Game.RoomSync.PuppetHitsSent} hitstaken={COTLMP.Game.RoomSync.HitsTaken} removed={COTLMP.Game.RoomSync.EnemiesRemoved} followed={COTLMP.Game.RoomSync.FollowedCount} visible={CountVisibleEnemies()} pickups={COTLMP.Game.SharedPickups.Sent}/{COTLMP.Game.SharedPickups.Received}/{COTLMP.Game.SharedPickups.Removed}");
                Line.Append($" knockouts={COTLMP.Game.CoopLife.KnockOuts} revives={COTLMP.Game.CoopLife.Revives} down={COTLMP.Game.CoopLife.WeAreDown} partnerdown={COTLMP.Game.CoopLife.PartnerDown}");
                Line.Append($" cleared={COTLMP.Game.ClearedRooms.Sent}/{COTLMP.Game.ClearedRooms.Applied}/{COTLMP.Game.ClearedRooms.EnemiesHidden}");
                Line.Append($" dialog={COTLMP.Game.Dialogs.LinesSent}/{COTLMP.Game.Dialogs.LinesShown} camtargets={GameManager.GetInstance()?.CamFollowTarget?.targets.Count}:{(GameManager.GetInstance()?.CamFollowTarget?.targets.Count > 0 ? GameManager.GetInstance().CamFollowTarget.targets[0].gameObject?.name : "-")}");
                Line.Append($" hazards={COTLMP.Game.HazardSync.Sent}/{COTLMP.Game.HazardSync.Shown}/{COTLMP.Game.HazardSync.Missing} moved={COTLMP.Game.HazardSync.Moved}");
                Line.Append($" {Gear()}");
                Line.Append($" flood={FloodSent}/{COTLMP.Game.CoopSync.UnknownReceived} chaos={COTLMP.Network.LoopbackPeerLink.ChaosStats} ping={COTLMP.Game.ConnectionQuality.PingMs:F0} loss={COTLMP.Game.ConnectionQuality.Loss:F2} broken={COTLMP.Game.ConnectionQuality.Broken} hops={COTLMP.Network.Network.PacketsOut}>{COTLMP.Network.SteamBridge.FromLocal}|{COTLMP.Network.SteamBridge.FromLink}>{COTLMP.Network.Network.PacketsIn}");
                Line.Append($" seeded={COTLMP.Game.RoomRandom.Seeded} chest={Interaction_Chest.Instance != null} ghosts={COTLMP.Game.GhostLoot.Made} gold={Inventory.GetItemQuantity(InventoryItem.ITEM_TYPE.BLACK_GOLD)} grass={COTLMP.Game.RunGrass.SpotsGrown}:{COTLMP.Game.RunGrass.Run?.DungeonRun}/{DataManager.Instance?.dungeonRun}:{COTLMP.Game.RunGrass.Grown()}");
                Line.Append($" props={PropSync.Sent}/{PropSync.Received}/{PropSync.Applied}/{PropSync.Missing}/{PropSync.AppliedOnEntry}/{PropSync.AlreadyBroken} propmatch={PropSync.ByNumber}/{PropSync.BySpawn}/{PropSync.ByNearest}");
                Line.Append($" tarot={SharedTarot.Sent}/{SharedTarot.Received}/{SharedTarot.Applied} time={TimeManager.CurrentDay}:{TimeManager.CurrentGameTime:F0}:{TimeManager.CurrentPhase} cult={CultSync.MembersSent}/{CultSync.MembersApplied}/{CultSync.WholeSent}/{CultSync.WholeApplied}/{CultSync.GoneSent}/{CultSync.GoneApplied}/{CultSync.ItemsSent}/{CultSync.ItemsApplied}/{CultSync.PosesApplied}/{CultSync.TimeApplied} {CultState()}");
                Line.Append($" map={MapSync.LayoutsSent}/{MapSync.LayoutsAdopted}/{MapSync.PicksSent}/{MapSync.PicksApplied}/{MapSync.Conflicts} minimap={MiniMapSync.Marked} hostsave={COTLMP.Game.HostSave.Playing}/{COTLMP.Game.HostSave.Loads}/{COTLMP.Game.HostSave.ReceivedBytes}/{COTLMP.Game.HostSave.SentBytes} pose={PuppetSync.Sent}/{PuppetSync.Received}/{PuppetSync.Applied}/{PuppetSync.Appeared}/{PuppetSync.Vanished} enemyanims={RoomSync.EnemyAnimations} puppetanim={(COTLMP.Game.PlayerManager.GetPuppet(Plugin.GlobalsInternal.IsServerCreator ? 1u : 0u)?.Spine?.AnimationState?.GetCurrent(0)?.Animation?.Name)} myanim={(PlayerFarming.Instance?.Spine?.AnimationState?.GetCurrent(0)?.Animation?.Name)} node={(global::Map.MapManager.Instance?.CurrentNode?.point.x)},{(global::Map.MapManager.Instance?.CurrentNode?.point.y)}:{(global::Map.MapManager.Instance?.CurrentNode?.nodeType)}");
                Line.Append($" myskin={COTLMP.Game.Appearance.LocalSkin()} goat={(PlayerFarming.Instance != null && PlayerFarming.Instance.IsGoat)} puppetskin={COTLMP.Game.PlayerManager.GetSkin(Plugin.GlobalsInternal.IsServerCreator ? 1u : 0u)}");
                Line.Append($" scene={SceneManager.GetActiveScene().name} session={Plugin.GlobalsInternal.InGameSession} online={COTLMP.Network.Network.IsConnected} relay={COTLMP.Network.SteamBridge.Active} ts={Time.timeScale:F2} netlocal={COTLMP.Network.Network.HasLocalPlayer} sent={COTLMP.Network.Network.SentPositions} recv={COTLMP.Network.Network.ReceivedPositions}");
                if (PlayerFarming.Instance != null)
                    Line.Append($" local={Format(PlayerFarming.Instance.transform.position)}");

                for (uint Id = 0; Id < InternalData.MaxPlayersPerServerInternal; Id++)
                {
                    Vector3? Position = PlayerManager.GetPlayerPosition(Id);
                    if (Position.HasValue)
                    {
                        Line.Append($" remote{Id}={Format(Position.Value)}");
                        Vector3? Target = PlayerManager.GetPlayerTarget(Id);
                        if (Target.HasValue)
                            Line.Append($"->{Format(Target.Value)}");
                    }
                }

                Plugin.Logger?.LogWarning(Line.ToString());
            }
        }

        private static string Format(Vector3 Value)
        {
            return string.Format(CultureInfo.InvariantCulture, "({0:F2},{1:F2})", Value.x, Value.y);
        }

        private static IEnumerator EndLater()
        {
            /* A host counts from the moment a friend shows up */
            while (!Plugin.GlobalsInternal.InGameSession || (Plugin.GlobalsInternal.IsServerCreator && !AnyRemotePlayer()))
                yield return null;
            yield return new WaitForSecondsRealtime(EndAfter);

            bool Host = Plugin.GlobalsInternal.IsServerCreator;
            Plugin.Logger?.LogWarning($"{Tag} Ending the session as the player would");
            PauseMenuPatches.EndSession(Host ? "Multiplayer/Steam/Stopped" : "Multiplayer/Steam/Left");
            Plugin.Logger?.LogWarning($"[QA] Session ended, still in scene {SceneManager.GetActiveScene().name}");

            /* The joining copy comes back once, to show leaving doesn't break the next join */
            if (!Host && RejoinHost != 0)
            {
                yield return new WaitForSecondsRealtime(3f);
                Plugin.Logger?.LogWarning("[QA client] Joining again");
                COTLMP.Network.SteamBridge.QueueFakeRejoin(RejoinHost);
                RejoinHost = 0;
            }
        }

        private static IEnumerator EnterDungeon()
        {
            while (!Plugin.GlobalsInternal.InGameSession || PlayerFarming.Instance == null || MMTransition.IsPlaying)
                yield return null;
            yield return new WaitForSecondsRealtime(AutoDungeon);

            Plugin.Logger?.LogWarning($"{Tag} Starting a run into Dungeon1 like the base door does");
            GameManager.NewRun("", false, FollowerLocation.Dungeon1_1);
            MMTransition.Play(MMTransition.TransitionType.ChangeRoomWaitToResume, MMTransition.Effect.BlackFade, "Dungeon1", 1f, "", null);

            while (SceneManager.GetActiveScene().name != "Dungeon1" || PlayerFarming.Instance == null)
                yield return null;
            yield return new WaitForSecondsRealtime(3f);
            MMTransition.CanResume = true;
            MMTransition.ResumePlay();
            Plugin.Logger?.LogWarning($"{Tag} In the dungeon at {COTLMP.Game.CoopSync.CurrentPlace()}");

            /* Walk the same way through the rooms on both sides: the first unvisited door, N, E, S, W */
            while (SceneManager.GetActiveScene().name == "Dungeon1")
            {
                yield return new WaitForSecondsRealtime(Plugin.GlobalsInternal.IsServerCreator ? 5f : 1f);
                MMBiomeGeneration.BiomeRoom Room = MMBiomeGeneration.BiomeGenerator.Instance?.CurrentRoom;
                if (Room == null || MMTransition.IsPlaying)
                    continue;

                /* The joining copy just follows the host into the next room */
                if (!Plugin.GlobalsInternal.IsServerCreator)
                {
                    string Mine = COTLMP.Game.CoopSync.CurrentPlace();
                    string Partner = COTLMP.Game.RoomSync.PartnerLastPlace;

                    /* In the test apart, linger so the host is rooms ahead */
                    if (Solo && Partner != Mine)
                        yield return new WaitForSecondsRealtime(6f);

                    /* Both at the floor's exit: through it on our own, the partner's puppet takes nobody along */
                    if (NextFloor && Partner == Mine && Mine != null && HasNextLayer(Room))
                    {
                        yield return new WaitForSecondsRealtime(3f);
                        yield return GoToNextFloor(Room, false);
                        continue;
                    }
                    if (Partner == null || Mine == null || Partner == Mine || !Partner.Contains("@") || !Mine.Contains("@"))
                        continue;

                    /* The host went on to the next floor: after them */
                    if (Partner.Substring(0, Partner.IndexOf('@')) != Mine.Substring(0, Mine.IndexOf('@')))
                    {
                        if (NextFloor)
                            yield return GoToNextFloor(Room, false);
                        continue;
                    }
                    string[] P = Partner.Substring(Partner.IndexOf('@') + 1).Split(',');
                    Vector2Int? Step = NextStep(Room, new Vector2Int(int.Parse(P[0]), int.Parse(P[1])));
                    if (!Step.HasValue)
                        continue;
                    Vector2Int Gap = Step.Value;
                    Plugin.Logger?.LogWarning($"{Tag} Following the host {Gap} from {Room.x},{Room.y}");
                    MMTransition.Play(MMTransition.TransitionType.ChangeRoom, MMTransition.Effect.BlackFade, MMTransition.NO_SCENE, 0.5f, "", () => MMBiomeGeneration.BiomeGenerator.ChangeRoom(Gap));
                    continue;
                }

                /* The host waits for the partner in this room, then for the fight to end (or 40 s) */
                float Waited = 0f;
                while (!Solo && Waited < 90f && !AnyRemotePlayer())
                {
                    yield return new WaitForSecondsRealtime(1f);
                    Waited += 1f;
                }
                yield return new WaitForSecondsRealtime(8f);
                Waited = 0f;
                while (Waited < 40f && Health.team2.Exists(Enemy => Enemy != null && Enemy.HP > 0f))
                {
                    yield return new WaitForSecondsRealtime(1f);
                    Waited += 1f;
                }
                yield return new WaitForSecondsRealtime(4f);

                Vector2Int? Way = null;
                if (Room.N_Room != null && Room.N_Room.Connected && Room.N_Room.Room != null && !Room.N_Room.Room.Visited) Way = new Vector2Int(0, 1);
                else if (Room.E_Room != null && Room.E_Room.Connected && Room.E_Room.Room != null && !Room.E_Room.Room.Visited) Way = new Vector2Int(1, 0);
                else if (Room.S_Room != null && Room.S_Room.Connected && Room.S_Room.Room != null && !Room.S_Room.Room.Visited) Way = new Vector2Int(0, -1);
                else if (Room.W_Room != null && Room.W_Room.Connected && Room.W_Room.Room != null && !Room.W_Room.Room.Visited) Way = new Vector2Int(-1, 0);
                if (!Way.HasValue && NextFloor)
                {
                    yield return GoToNextFloor(Room, true);
                    continue;
                }
                if (!Way.HasValue)
                {
                    Plugin.Logger?.LogWarning($"{Tag} No unvisited room next to {Room.x},{Room.y}");
                    continue;
                }

                Vector2Int Direction = Way.Value;
                Plugin.Logger?.LogWarning($"{Tag} Going {Direction} from {Room.x},{Room.y}");
                MMTransition.Play(MMTransition.TransitionType.ChangeRoom, MMTransition.Effect.BlackFade, MMTransition.NO_SCENE, 0.5f, "", () => MMBiomeGeneration.BiomeGenerator.ChangeRoom(Direction));
            }
        }

        private static IEnumerator HitEnemies()
        {
            var Wait = new WaitForSecondsRealtime(1f);

            while (true)
            {
                yield return Wait;
                if (PlayerFarming.Instance == null || MMTransition.IsPlaying || PlayerFarming.Instance.IsKnockedOut)
                    continue;

                /* Collect what the fight dropped, as walking over it would */
                foreach (PickUp Item in new System.Collections.Generic.List<PickUp>(PickUp.PickUps))
                {
                    if (Item != null && Item.gameObject.activeInHierarchy && Item.CanBePickedUp && Item.AddToInventory)
                    {
                        Item.Player = PlayerFarming.Instance.gameObject;
                        Plugin.Logger?.LogWarning($"{Tag} Picking up {Item.type} x{Item.Quantity}");
                        Item.PickMeUp();
                        break;
                    }
                }

                /* Fight only with the partner in the room, like players would (unless testing apart) */
                if (Health.team2.Count == 0 || (!Solo && !AnyRemotePlayer()))
                    continue;

                Health Nearest = null;
                foreach (Health Enemy in Health.team2)
                {
                    if (Enemy != null && Enemy.HP > 0f && Enemy.gameObject.activeInHierarchy &&
                        (Nearest == null || Enemy.transform.position.sqrMagnitude < Nearest.transform.position.sqrMagnitude))
                        Nearest = Enemy;
                }
                if (Nearest == null)
                    continue;

                /*
                 * The test copies' lamb has no weapon, and the game's player-hit path reads it, so hit the
                 * way tarot damage does (the enemy as its own attacker). The HP change is what gets synced.
                 */
                bool Hit = false;
                try
                {
                    Hit = Nearest.DealDamage(Mathf.Max(1f, Nearest.totalHP * 0.4f), Nearest.gameObject, Nearest.transform.position, false, Health.AttackTypes.Melee, true);
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogWarning($"{Tag} Hitting {Nearest.name} threw: {e.Message}");
                }
                Plugin.Logger?.LogWarning($"{Tag} Hit {Nearest.name} -> {Hit}, hp {Nearest.HP:F1}/{Nearest.totalHP:F1}");
            }
        }

        /// <summary>
        /// Captures only this game's frame (Unity's ScreenCapture), never the desktop
        /// </summary>
        private static IEnumerator Shots()
        {
            string Last = null;
            int Count = 0;

            while (Count < 6)
            {
                yield return new WaitForSecondsRealtime(1f);
                string Place = COTLMP.Game.CoopSync.CurrentPlace();
                if (Place == null || Place == Last || !Place.Contains("#") || !AnyRemotePlayer())
                    continue;

                Last = Place;
                yield return new WaitForSecondsRealtime(5f);
                string File = System.IO.Path.Combine(ShotsDir, $"{(Plugin.GlobalsInternal.IsServerCreator ? "host" : "client")}-{++Count}.png");
                System.IO.Directory.CreateDirectory(ShotsDir);
                ScreenCapture.CaptureScreenshot(File);
                Plugin.Logger?.LogWarning($"{Tag} Saved {File} in {Place}");
            }
        }

        /// <summary>
        /// One frame for each kind of connection icon, once it has been up for a second.
        /// </summary>
        private static IEnumerator NetIconShots()
        {
            HashSet<string> Taken = new();
            string Seen = null;
            float Since = 0f;

            while (Taken.Count < 6)
            {
                yield return new WaitForSecondsRealtime(0.25f);
                float Ping = COTLMP.Game.ConnectionQuality.PingMs, Loss = COTLMP.Game.ConnectionQuality.Loss;
                string State = COTLMP.Game.ConnectionQuality.Broken ? "broken" :
                               (Ping >= COTLMP.Game.ConnectionQuality.RedPing ? "pingred" : Ping >= COTLMP.Game.ConnectionQuality.YellowPing ? "pingyellow" : "pingok") +
                               (Loss >= COTLMP.Game.ConnectionQuality.RedLoss ? "-lossred" : Loss >= COTLMP.Game.ConnectionQuality.YellowLoss ? "-lossyellow" : "");
                if (State != Seen)
                {
                    Seen = State;
                    Since = Time.realtimeSinceStartup;
                    continue;
                }
                if (Taken.Contains(State) || Time.realtimeSinceStartup - Since < 1f || !COTLMP.Game.CoopSync.InSession)
                    continue;

                Taken.Add(State);
                string File = System.IO.Path.Combine(ShotsDir, $"{(Plugin.GlobalsInternal.IsServerCreator ? "host" : "client")}-net-{State}.png");
                ScreenCapture.CaptureScreenshot(File);
                Plugin.Logger?.LogWarning($"{Tag} Saved {File} (ping {Ping:F0} ms, loss {Loss:P0})");
            }
        }

        private static IEnumerator Draw()
        {
            string Last = null;
            bool Unlocked = false;
            while (true)
            {
                yield return new WaitForSecondsRealtime(1f);
                if (PlayerFarming.Instance == null || DataManager.Instance == null)
                    continue;

                /* Make the host's save differ from the joining one (in memory only, test copies) */
                if (!Unlocked && Plugin.GlobalsInternal.IsServerCreator && Plugin.GlobalsInternal.InGameSession)
                {
                    Unlocked = true;
                    foreach (TarotCards.Card Card in DataManager.AllTrinkets)
                    {
                        if (!DataManager.Instance.PlayerFoundTrinkets.Contains(Card))
                            DataManager.Instance.PlayerFoundTrinkets.Add(Card);
                    }
                    Plugin.Logger?.LogWarning($"{Tag} Host now has {DataManager.Instance.PlayerFoundTrinkets.Count} tarot cards");
                }

                string Place = COTLMP.Game.CoopSync.CurrentPlace();
                if (Place == null || Place == Last || !Place.Contains("#") || !AnyRemotePlayer())
                    continue;
                Last = Place;
                yield return new WaitForSecondsRealtime(3f);

                var Cards = new System.Collections.Generic.List<string>();
                for (int Index = 0; Index < 3; Index++)
                {
                    try
                    {
                        Cards.Add(TarotCards.DrawRandomCard(PlayerFarming.Instance)?.CardType.ToString() ?? "none");
                    }
                    catch (Exception e)
                    {
                        Cards.Add("error " + e.Message);
                    }
                }
                Plugin.Logger?.LogWarning($"{Tag} Drew in {Place}: {string.Join(", ", Cards)} (own pool {DataManager.Instance.PlayerFoundTrinkets.Count})");
            }
        }

        private static IEnumerator Shoot()
        {
            var Wait = new WaitForSecondsRealtime(3f);
            while (true)
            {
                yield return Wait;
                if (!COTLMP.Game.RoomSync.Owning)
                    continue;
                GameObject Prefab = COTLMP.Game.HazardSync.AnyProjectilePrefab();
                Vector3? Partner = COTLMP.Game.PlayerManager.GetAnyPuppetPosition();
                if (Prefab == null || !Partner.HasValue)
                    continue;
                try
                {
                    Vector3 From = Partner.Value + new Vector3(4f, 0f, 0f);
                    Projectile Shot = ObjectPool.Spawn(Prefab, MMRoomGeneration.GenerateRoom.Instance?.transform).GetComponent<Projectile>();
                    Shot.transform.position = From;
                    Shot.Angle = 180f;
                    Shot.Speed = 6f;
                    Shot.team = Health.Team.Team2;
                    Plugin.Logger?.LogWarning($"{Tag} Fired {Prefab.name} at the partner");
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogWarning($"{Tag} Firing failed: {e.Message}");
                }
            }
        }

        private static IEnumerator MeetFox()
        {
            while (SceneManager.GetActiveScene().name != "Base Biome 1" || PlayerFarming.Instance == null || !AnyRemotePlayer() || MMTransition.IsPlaying)
                yield return null;
            yield return new WaitForSecondsRealtime(10f);

            /* Night first (the friend's clock follows the host's), with followers for a deal */
            if (Plugin.GlobalsInternal.IsServerCreator)
            {
                HarmonyLib.AccessTools.Method(typeof(TimeManager), "StartNewPhase").Invoke(null, new object[] { DayPhase.Night });
                Plugin.Logger?.LogWarning($"{Tag} Night now: {TimeManager.CurrentPhase}");
            }
            yield return new WaitForSecondsRealtime(4f);
            Plugin.Logger?.LogWarning($"{Tag} To the shore at {TimeManager.CurrentPhase}");
            MMTransition.Play(MMTransition.TransitionType.ChangeRoomWaitToResume, MMTransition.Effect.BlackFade, "Hub-Shore", 1f, "", null);
            while (SceneManager.GetActiveScene().name != "Hub-Shore" || PlayerFarming.Instance == null || MMTransition.IsPlaying)
                yield return null;
            yield return new WaitForSecondsRealtime(8f);

            Interaction_NightFox Fox = UnityEngine.Object.FindObjectOfType<Interaction_NightFox>(true);
            Plugin.Logger?.LogWarning($"{Tag} Fox here: {Fox != null} active={Fox?.gameObject.activeInHierarchy} location={PlayerFarming.Location} phase={TimeManager.CurrentPhase}");
            if (Fox == null)
                yield break;
            PlayerFarming.Instance.transform.position = Fox.transform.position + Vector3.down * 2f;
            yield return new WaitForSecondsRealtime(3f);
            if (!string.IsNullOrEmpty(ShotsDir))
                Shot($"fox-before-{(Plugin.GlobalsInternal.IsServerCreator ? "host" : "client")}");

            /* The friend tries first, then the host talks */
            yield return new WaitForSecondsRealtime(Plugin.GlobalsInternal.IsServerCreator ? 8f : 2f);
            Plugin.Logger?.LogWarning($"{Tag} Talking to the fox");
            Fox.OnInteract(PlayerFarming.Instance.state);
            yield return new WaitForSecondsRealtime(Plugin.GlobalsInternal.IsServerCreator ? 6f : 14f);
            if (!string.IsNullOrEmpty(ShotsDir))
                Shot($"fox-talk-{(Plugin.GlobalsInternal.IsServerCreator ? "host" : "client")}");
            yield return new WaitForSecondsRealtime(40f);
            if (!string.IsNullOrEmpty(ShotsDir))
                Shot($"fox-after-{(Plugin.GlobalsInternal.IsServerCreator ? "host" : "client")}");
            Plugin.Logger?.LogWarning($"{Tag} Fox after: encounter={DataManager.Instance.CurrentFoxEncounter} done={string.Join(",", DataManager.Instance.FoxCompleted)} intro={string.Join(",", DataManager.Instance.FoxIntroductions)}");
        }

        private static IEnumerator TalkOnce()
        {
            while (SceneManager.GetActiveScene().name != "Dungeon1" || PlayerFarming.Instance == null || !AnyRemotePlayer())
                yield return null;
            yield return new WaitForSecondsRealtime(AutoTalk);

            GameObject Me = PlayerFarming.Instance.gameObject;
            var Entries = new System.Collections.Generic.List<MMTools.ConversationEntry>
            {
                new(Me, "Co-op test line one", "talk", "", null, "Lamb"),
                new(Me, "Co-op test line two", "talk", "", null, "Lamb"),
                new(Me, "Co-op test line three", "talk", "", null, "Lamb")
            };
            Plugin.Logger?.LogWarning($"{Tag} Starting a test conversation");
            MMTools.MMConversation.Play(new MMTools.ConversationObject(Entries, null, null));
        }

        private static IEnumerator DieOnce()
        {
            /* Counted from the first moment both stand in one dungeon room */
            while (SceneManager.GetActiveScene().name != "Dungeon1" || PlayerFarming.Instance == null || !AnyRemotePlayer())
                yield return null;
            yield return new WaitForSecondsRealtime(AutoDie);

            PlayerFarming Player = PlayerFarming.Instance;
            Plugin.Logger?.LogWarning($"{Tag} Dying on purpose, hp {Player.health.HP}");
            Player.health.invincible = false;
            Player.health.untouchable = false;
            Player.health.HP = 0.5f;
            Player.health.DealDamage(5f, Player.gameObject, Player.transform.position, false, Health.AttackTypes.Melee, true, Health.AttackFlags.ForceKill);
            yield return new WaitForSecondsRealtime(2f);
            Plugin.Logger?.LogWarning($"{Tag} After dying: knocked out {Player.IsKnockedOut}, state {Player.state.CURRENT_STATE}, down {COTLMP.Game.CoopLife.WeAreDown}");
        }

        /// <summary>
        /// The host's hearts go down to two, then one, then back up, a few seconds each (the game
        /// bruises the lamb at two and gives it a black eye at one); nothing else hurts it meanwhile
        /// </summary>
        private static IEnumerator HurtTest()
        {
            while (SceneManager.GetActiveScene().name != "Dungeon1" || PlayerFarming.Instance == null || !AnyRemotePlayer())
                yield return null;
            /* Hosting starts after the switches are read */
            if (!Plugin.GlobalsInternal.IsServerCreator)
            {
                yield return WatchLooks();
                yield break;
            }
            yield return new WaitForSecondsRealtime(AutoHurt);

            PlayerFarming Player = PlayerFarming.Instance;
            float Full = Player.health.HP;
            Player.health.invincible = true;
            foreach (float Hearts in new[] { 2f, 1f, Full })
            {
                Player.health.HP = Hearts;
                Plugin.Logger?.LogWarning($"{Tag} Hurt test: our hp {Player.health.HP}, our look {COTLMP.Game.Appearance.Look:X}");
                yield return new WaitForSecondsRealtime(6f);
            }
            Player.health.invincible = false;
        }

        /// <summary>
        /// Logs and frames each look the partner's puppet takes on
        /// </summary>
        private static IEnumerator WatchLooks()
        {
            int Seen = -1, Frames = 0;
            while (true)
            {
                for (uint Id = 0; Id < InternalData.MaxPlayersPerServerInternal; Id++)
                {
                    int Look = PlayerManager.GetLook(Id);
                    if (Look < 0 || Look == Seen)
                        continue;
                    Seen = Look;
                    Plugin.Logger?.LogWarning($"{Tag} Hurt test: player {Id}'s puppet looks {Look:X} (bruises {COTLMP.Game.Appearance.LookHurt(Look)})");
                    if (Frames++ < 4)
                    {
                        yield return new WaitForSecondsRealtime(0.5f);
                        PlayerFarming Puppet = PlayerManager.GetPuppet(Id);
                        Vector3 OnScreen = Puppet != null && Camera.main != null ? Camera.main.WorldToScreenPoint(Puppet.transform.position) : Vector3.zero;
                        Plugin.Logger?.LogWarning($"{Tag} Hurt test: the puppet stands at {OnScreen.x:F0},{Screen.height - OnScreen.y:F0} of {Screen.width}x{Screen.height}");
                        Shot($"puppet-hurt{COTLMP.Game.Appearance.LookHurt(Look)}");
                    }
                }
                yield return new WaitForSecondsRealtime(0.2f);
            }
        }

        /// <summary>
        /// Stands next to the downed partner until the bar fills, as a player would
        /// </summary>
        private static IEnumerator Rescue()
        {
            while (true)
            {
                yield return null;
                if (!COTLMP.Game.CoopLife.PartnerDown || PlayerFarming.Instance == null || MMTransition.IsPlaying)
                    continue;
                Vector3? Downed = COTLMP.Game.PlayerManager.GetAnyPuppetPosition();
                if (!Downed.HasValue)
                    continue;
                PlayerFarming.Instance.transform.position = Downed.Value + new Vector3(0.6f, 0f, 0f);
                if (Time.frameCount % 60 == 0)
                    Plugin.Logger?.LogWarning($"{Tag} Reviving the partner, bar {COTLMP.Game.CoopLife.ReviveProgress:F2}");
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr Window, IntPtr After, int X, int Y, int Width, int Height, uint Flags);

        /// <summary>
        /// Puts the game back in its small window whenever its settings made it bigger, and where it belongs on the desktop
        /// </summary>
        private static IEnumerator KeepWindow()
        {
            const uint NoSize = 0x1, NoZOrder = 0x4, NoActivate = 0x10;
            var Wait = new WaitForSecondsRealtime(2f);
            bool Placed = false;

            while (true)
            {
                if (Screen.fullScreenMode != FullScreenMode.Windowed || Screen.width != WindowRect[2] || Screen.height != WindowRect[3])
                {
                    Screen.SetResolution(WindowRect[2], WindowRect[3], FullScreenMode.Windowed);
                    Placed = false;
                    yield return null;
                }
                if (!Placed)
                {
                    IntPtr Window = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                    if (Window != IntPtr.Zero)
                        Placed = SetWindowPos(Window, IntPtr.Zero, WindowRect[0], WindowRect[1], 0, 0, NoSize | NoZOrder | NoActivate);
                }
                yield return Wait;
            }
        }

        /// <summary>
        /// The joining copy clicks the first way on whenever a map comes up (the host's puppet at
        /// the door brings it up too), before the host's pick is taken for it
        /// </summary>
        private static IEnumerator ClickMaps()
        {
            Lamb.UI.UIAdventureMapOverlayController Clicked = null;
            while (true)
            {
                yield return new WaitForSecondsRealtime(0.25f);
                Lamb.UI.UIAdventureMapOverlayController Overlay = MapSync.Open;
                if (Overlay == null || Overlay == Clicked)
                    continue;
                yield return new WaitForSecondsRealtime(0.5f);
                Clicked = Overlay;
                global::Map.Node Current = Overlay.Map?.GetCurrentNode();
                if (MapSync.Open != Overlay || Current == null || Current.outgoing.Count == 0)
                    continue;
                global::Map.Point Choice = Current.outgoing[0];
                Lamb.UI.AdventureMapNode Shown = Overlay.NodeFromPoint(Choice);
                Plugin.Logger?.LogWarning($"{Tag} Clicking node {Choice.x},{Choice.y} ({Shown?.MapNode?.nodeType}) of {Current.outgoing.Count} ways on, map from the host {MapSync.FollowingHost}");
                if (Shown != null)
                    Overlay.OnNodeChosen(Shown);
                yield return new WaitForSecondsRealtime(0.5f);
                Shot($"map-client-{GameManager.CurrentDungeonFloor}-{Time.frameCount}");
            }
        }

        /// <summary>
        /// Opens the pause menu over and over and logs whether it stays up
        /// </summary>
        private static IEnumerator OpenPauseMenus()
        {
            int Count = 0;
            while (true)
            {
                yield return new WaitForSecondsRealtime(8f);
                Lamb.UI.UIManager UI = MonoSingleton<Lamb.UI.UIManager>.Instance;
                if (PlayerFarming.Instance == null || MMTransition.IsPlaying || UI == null || UI.IsPaused || UI.MenusBlocked)
                    continue;
                ++Count;

                /* As in the game's opening, before the crown: no lamb in the player list */
                List<PlayerFarming> Hidden = null;
                if (PauseNoPlayer)
                {
                    Hidden = new List<PlayerFarming>(PlayerFarming.players);
                    PlayerFarming.players.Clear();
                    PlayerFarming.playersCount = 0;
                }
                try
                {
                    UI.ShowPauseMenu(PauseNoPlayer ? null : PlayerFarming.Instance);
                }
                catch (Exception Error)
                {
                    Plugin.Logger?.LogError($"{Tag} Pause {Count} threw: {Error}");
                }
                if (Hidden != null)
                {
                    yield return new WaitForSecondsRealtime(0.5f);
                    PlayerFarming.players.AddRange(Hidden);
                    PlayerFarming.playersCount = PlayerFarming.players.Count;
                }

                yield return new WaitForSecondsRealtime(2f);
                var Menu = Lamb.UI.UIMenuBase.ActiveMenus.Find(Item => Item is Lamb.UI.PauseMenu.UIPauseMenuController);
                Plugin.Logger?.LogWarning($"{Tag} Pause {Count} after 2 s: {(Menu != null && !Menu.IsHiding ? "open" : "CLOSED")}, timeScale {Time.timeScale}");
                Shot($"pause-{(Plugin.GlobalsInternal.IsServerCreator ? "host" : "client")}-{Count}");
                Menu?.OnCancelButtonInput();
            }
        }

        /// <summary>
        /// Both sides of the cult test, by role
        /// </summary>
        /// <summary>
        /// A frame of every map between floors, with both crowns on it
        /// </summary>
        private static IEnumerator MapShots()
        {
            for (int Count = 1; Count <= 4; Count++)
            {
                while (COTLMP.Game.MapSync.Shown == null)
                    yield return new WaitForSecondsRealtime(0.2f);
                yield return new WaitForSecondsRealtime(1.6f);
                Plugin.Logger?.LogWarning($"{Tag} Map {Count}: partner {COTLMP.Game.MapCrowns.PartnerState}, crowns shown {COTLMP.Game.MapCrowns.Shown}, sent {COTLMP.Game.MapCrowns.Sent}, got {COTLMP.Game.MapCrowns.Received}");
                Shot($"map-{Count}");
                while (COTLMP.Game.MapSync.Shown != null)
                    yield return new WaitForSecondsRealtime(0.2f);
            }
        }

        /* THE HUD TRACE ******************************************************/

        private static void TraceHud()
        {
            var Patcher = new Harmony("cotlmp.hudtrace");
            foreach (string Name in new[] { "Hide", "Show" })
                Patcher.Patch(AccessTools.Method(typeof(HUD_Manager), Name), prefix: new HarmonyMethod(typeof(TestHarness), nameof(HudCalled)));
            Patcher.Patch(AccessTools.Method(typeof(Health_Manager), nameof(Health_Manager.Init)), prefix: new HarmonyMethod(typeof(TestHarness), nameof(HudCalled)));
            foreach (string Name in new[] { "Hide", "HideBars", "Show" })
                Patcher.Patch(AccessTools.Method(typeof(Health_Manager), Name), prefix: new HarmonyMethod(typeof(TestHarness), nameof(HudCalled)));
            foreach (string Name in new[] { "MoveBackOutFunction", "hideBar" })
                Patcher.Patch(AccessTools.Method(typeof(UI_Transitions), Name), prefix: new HarmonyMethod(typeof(TestHarness), nameof(HudTransition)));
            Plugin.MonoInstance.StartCoroutine(LogHud());
        }

        /* Only the hearts panels' own moves */
        private static void HudTransition(UI_Transitions __instance, MethodBase __originalMethod)
        {
            if (__instance == null || __instance.GetComponentInParent<Health_Manager>() == null)
                return;
            string Stack = string.Join(" < ", new System.Diagnostics.StackTrace(2, false).GetFrames()?.Take(6)
                .Select(Frame => $"{Frame.GetMethod()?.DeclaringType?.Name}.{Frame.GetMethod()?.Name}") ?? Enumerable.Empty<string>());
            Plugin.Logger?.LogWarning($"{Tag} HUD panel {__instance.name} {__originalMethod.Name}: {Stack}");
        }

        private static IEnumerator LogHud()
        {
            string Last = null;
            while (true)
            {
                yield return new WaitForSecondsRealtime(3f);
                HUD_Manager Hud = HUD_Manager.Instance;
                if (Hud == null)
                    continue;
                Health_Manager Health = Hud.healthManager;
                string Hearts(HUD_Hearts Panel) => Panel == null ? "-" :
                    $"{(Panel.gameObject.activeInHierarchy ? "on" : "off")}:{(Traverse.Create(Panel).Field("playerFarming").GetValue() is PlayerFarming Bound && Bound != null ? (Bound == PlayerFarming.Instance ? "us" : PlayerManager.IsPuppet(Bound.health) ? "puppet" : "other") : "none")}";
                CanvasGroup Group = Hud.GetComponentInParent<CanvasGroup>();
                string State = $"hidden={Hud.Hidden} active={Hud.gameObject.activeInHierarchy} alpha={(Group != null ? Group.alpha : -1f):F2} container={Hud.healthContainer?.activeInHierarchy} " +
                               $"singleDungeon={Hearts(Health?.health_SingleDungeon)} singleBase={Hearts(Health?.health_SingleBase)} first={Hearts(Health?.health_firstPlayer)} second={Hearts(Health?.health_secondPlayer)} players={PlayerFarming.players.Count}";
                if (State != Last)
                    Plugin.Logger?.LogWarning($"{Tag} HUD state in {SceneManager.GetActiveScene().name}: {State}");
                Last = State;
            }
        }

        private static void HudCalled(MethodBase __originalMethod)
        {
            string Stack = string.Join(" < ", new System.Diagnostics.StackTrace(2, false).GetFrames()?.Take(6)
                .Select(Frame => $"{Frame.GetMethod()?.DeclaringType?.Name}.{Frame.GetMethod()?.Name}") ?? Enumerable.Empty<string>());
            Plugin.Logger?.LogWarning($"{Tag} HUD {__originalMethod.Name} in {SceneManager.GetActiveScene().name}: {Stack}");
        }

        /* THE TEMPLE TEST ****************************************************/

        /// <summary>
        /// The cult as the temple test sees it: the sermon day, the holiday, its cooldown, faith,
        /// what the followers think of us and how many are at the temple
        /// </summary>
        private static string TempleState()
        {
            var Followers = DataManager.Instance.Followers;
            return $"sermonday={DataManager.Instance.PreviousSermonDayIndex} day={TimeManager.CurrentDay} holiday={FollowerBrainStats.IsHoliday} " +
                   $"cooldown={UpgradeSystem.GetCoolDownNormalised(UpgradeSystem.Type.Ritual_Holiday):F2} faith={CultFaithManager.CurrentFaith:F1} " +
                   $"bones={Inventory.GetItemQuantity(InventoryItem.ITEM_TYPE.BONE)} adoration={Followers.Sum(Info => Info?.Adoration ?? 0f):F1} " +
                   $"thoughts={string.Join(",", Followers.Select(Info => $"{Info?.ID}:{string.Join("+", (Info?.Thoughts ?? new List<ThoughtData>()).Select(T => T.ThoughtType))}"))} " +
                   $"church={FollowerManager.FollowersAtLocation(FollowerLocation.Church).Count} where={PlayerFarming.Location} hands={CultHands.FriendCount}";
        }

        /// <summary>
        /// The host builds a temple and gets three followers and the bones for a holiday; the
        /// friend goes into the temple, preaches a sermon, holds the holiday ritual and closes the
        /// altar, as a player would through its menus. Both log the cult as it goes.
        /// </summary>
        private static IEnumerator TempleRun()
        {
            while (SceneManager.GetActiveScene().name != "Base Biome 1" || PlayerFarming.Instance == null || !AnyRemotePlayer() ||
                   (!Plugin.GlobalsInternal.IsServerCreator && !HostSave.Playing))
                yield return null;
            bool Host = Plugin.GlobalsInternal.IsServerCreator;
            Plugin.MonoInstance.StartCoroutine(LogTemple());
            Plugin.MonoInstance.StartCoroutine(CloseTutorials());

            if (Host)
            {
                yield return new WaitForSecondsRealtime(8f);
                Vector3 Here = PlayerFarming.Instance.transform.position;
                UpgradeSystem.UnlockAbility(UpgradeSystem.Type.Building_Temple, true);
                UpgradeSystem.UnlockAbility(UpgradeSystem.Type.Ritual_Holiday, true);
                StructuresData Temple = StructuresData.GetInfoByType(StructureBrain.TYPES.TEMPLE, 0);
                StructureManager.BuildStructure(FollowerLocation.Base, Temple, Here + new Vector3(0f, 9f), Temple.Bounds);
                Plugin.Logger?.LogWarning($"{Tag} Temple test: built a temple ({Temple.ID})");
                for (int Count = 0; Count < 3; Count++)
                {
                    FollowerInfo New = FollowerInfo.NewCharacter(FollowerLocation.Base);
                    DataManager.Instance.Followers.Add(New);
                    FollowerManager.SpawnFollower(New, Here + new Vector3(-2f - Count, 1f));
                }
                Inventory.ChangeItemQuantity(InventoryItem.ITEM_TYPE.BONE, 100);
                DataManager.Instance.PreviousSermonDayIndex = -1;
                Plugin.Logger?.LogWarning($"{Tag} Temple test: ready: {TempleState()}");
                yield break;
            }

            float Since = Time.realtimeSinceStartup;
            while ((Interaction_Temple.Instance == null || DataManager.Instance.Followers.Count < 3 ||
                    Inventory.GetItemQuantity(InventoryItem.ITEM_TYPE.BONE) < 75 || Time.realtimeSinceStartup - Since < 30f) && Time.realtimeSinceStartup - Since < 90f)
                yield return null;
            yield return new WaitForSecondsRealtime(3f);
            Plugin.Logger?.LogWarning($"{Tag} Temple test: going in, temple {Interaction_Temple.Instance != null}: {TempleState()}");
            if (Interaction_Temple.Instance == null)
                yield break;

            /* Through the door, as walking into it does */
            EnterBuilding Door = Interaction_Temple.Instance.Entrance;
            AccessTools.Method(typeof(EnterBuilding), "OnTriggerEnter2D").Invoke(Door, new object[] { PlayerFarming.Instance.GetComponent<Collider2D>() });
            yield return Until(() => PlayerFarming.Location == FollowerLocation.Church && !MMTransition.IsPlaying && Interaction_TempleAltar.Instance != null, 20f);
            yield return new WaitForSecondsRealtime(3f);
            Plugin.Logger?.LogWarning($"{Tag} Temple test: inside ({PlayerFarming.Location}): {TempleState()}");
            Shot("temple-inside");

            Interaction_TempleAltar.Instance.OnInteract(PlayerFarming.Instance.state);
            yield return Plugin.MonoInstance.StartCoroutine(PressAltar("OnSermonButtonClicked", "sermon"));
            Plugin.MonoInstance.StartCoroutine(TempleShots("sermon", 6));

            /* The altar's menu comes back once the sermon is over */
            yield return Plugin.MonoInstance.StartCoroutine(PressAltar("OnRitualsButtonClicked", "rituals", 150f));
            yield return Until(() => UnityEngine.Object.FindObjectOfType<Lamb.UI.Rituals.UIRitualsMenuController>() != null, 15f);
            yield return new WaitForSecondsRealtime(1.5f);
            var Rituals = UnityEngine.Object.FindObjectOfType<Lamb.UI.Rituals.UIRitualsMenuController>();
            Plugin.Logger?.LogWarning($"{Tag} Temple test: ritual menu {Rituals != null}, holding the holiday: {TempleState()}");
            if (Rituals != null)
                AccessTools.Method(Rituals.GetType(), "RitualItemSelected").Invoke(Rituals, new object[] { UpgradeSystem.Type.Ritual_Holiday });
            Plugin.MonoInstance.StartCoroutine(TempleShots("ritual", 6));

            yield return Until(() => AltarMenu() != null, 90f);
            yield return new WaitForSecondsRealtime(1.5f);
            Plugin.Logger?.LogWarning($"{Tag} Temple test: ritual over, closing the altar: {TempleState()}");
            AltarMenu()?.OnCancelButtonInput();
            yield return new WaitForSecondsRealtime(15f);
            Plugin.Logger?.LogWarning($"{Tag} Temple test: done: {TempleState()}");
        }

        private static Lamb.UI.AltarMenu.UIAltarMenuController AltarMenu()
        {
            var Menu = UnityEngine.Object.FindObjectOfType<Lamb.UI.AltarMenu.UIAltarMenuController>();
            return Menu != null && Menu.isActiveAndEnabled ? Menu : null;
        }

        /// <summary>
        /// Waits for the altar's menu and clicks one of its buttons; tutorials that come up meanwhile are closed
        /// </summary>
        private static IEnumerator PressAltar(string Button, string What, float Seconds = 20f)
        {
            float Since = Time.realtimeSinceStartup;
            string Seen = null;
            /* The menu last clicked takes a moment to go */
            while (AltarMenu() != null && Time.realtimeSinceStartup - Since < 10f)
                yield return null;
            while (AltarMenu() == null && Time.realtimeSinceStartup - Since < Seconds)
            {
                string Open = string.Join(",", UnityEngine.Object.FindObjectsOfType<Lamb.UI.UIMenuBase>().Where(Menu => Menu.isActiveAndEnabled).Select(Menu => Menu.GetType().Name));
                if (Open != Seen)
                {
                    Seen = Open;
                    Plugin.Logger?.LogWarning($"{Tag} Temple test: menus up: {Open}");
                }
                yield return new WaitForSecondsRealtime(0.5f);
            }
            yield return new WaitForSecondsRealtime(1.5f);
            var Altar = AltarMenu();
            Plugin.Logger?.LogWarning($"{Tag} Temple test: altar menu {Altar != null} after {Time.realtimeSinceStartup - Since:F0} s, choosing {What}: {TempleState()}");
            if (Altar != null)
                AccessTools.Method(Altar.GetType(), Button).Invoke(Altar, null);
        }

        private static IEnumerator TempleShots(string Name, int Count)
        {
            for (int Index = 1; Index <= Count; Index++)
            {
                yield return new WaitForSecondsRealtime(4f);
                Shot($"temple-{Name}-{Index}");
            }
        }

        /// <summary>
        /// The game's tutorial windows (the first sermon, the first ritual) are read and closed, as a player would
        /// </summary>
        private static IEnumerator CloseTutorials()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(1f);

                /* The first sermon's reward: the first upgrade on offer, then the tree closes */
                var Tree = UnityEngine.Object.FindObjectOfType<Lamb.UI.UIUpgradePlayerTreeMenuController>();
                if (Tree != null && Tree.isActiveAndEnabled)
                {
                    yield return new WaitForSecondsRealtime(2f);
                    var Nodes = Traverse.Create(Tree).Field("_treeNodes").GetValue<List<Lamb.UI.UpgradeTreeNode>>();
                    Lamb.UI.UpgradeTreeNode Node = Nodes?.FirstOrDefault(Each => Each != null && Each.State == Lamb.UI.UpgradeTreeNode.NodeState.Available);
                    Plugin.Logger?.LogWarning($"{Tag} Taking the upgrade {Node?.Upgrade}");
                    if (Node != null)
                        AccessTools.Method(typeof(Lamb.UI.UIUpgradeTreeMenuBase<Lamb.UI.UIPlayerUpgradeUnlockOverlayController>), "PerformUnlock").Invoke(Tree, new object[] { Node });
                    yield return new WaitForSecondsRealtime(8f);
                    if (Tree != null && Tree.isActiveAndEnabled)
                        Tree.Hide();
                    continue;
                }

                var Tutorial = UnityEngine.Object.FindObjectOfType<src.UI.Overlays.TutorialOverlay.UITutorialOverlayController>();
                if (Tutorial == null || !Tutorial.isActiveAndEnabled)
                    continue;
                yield return new WaitForSecondsRealtime(1.5f);
                if (Tutorial == null)
                    continue;
                Plugin.Logger?.LogWarning($"{Tag} Closing a tutorial window");
                Tutorial.Hide();
            }
        }

        private static IEnumerator LogTemple()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(10f);
                Plugin.Logger?.LogWarning($"{Tag} Temple: {TempleState()}");
            }
        }

        private static IEnumerator ChangeCult()
        {
            /* Who hosts is known once the session is up */
            while (SceneManager.GetActiveScene().name != "Base Biome 1" || PlayerFarming.Instance == null || !AnyRemotePlayer() ||
                   (!Plugin.GlobalsInternal.IsServerCreator && !HostSave.Playing))
                yield return null;
            bool Host = Plugin.GlobalsInternal.IsServerCreator;
            yield return new WaitForSecondsRealtime(Host ? 10f : 25f);
            Vector3 Here = PlayerFarming.Instance.transform.position;

            if (Host)
            {
                Inventory.ChangeItemQuantity(InventoryItem.ITEM_TYPE.LOG, 10);
                Plugin.Logger?.LogWarning($"{Tag} Cult test: +10 wood");
                yield return new WaitForSecondsRealtime(3f);
                StructuresData Bed = StructuresData.GetInfoByType(StructureBrain.TYPES.BED, 0);
                StructureManager.BuildStructure(FollowerLocation.Base, Bed, Here + new Vector3(3f, 0f), Bed.Bounds);
                Plugin.Logger?.LogWarning($"{Tag} Cult test: built a bed ({Bed.ID})");
                yield return new WaitForSecondsRealtime(3f);
                for (int Count = 0; Count < 2; Count++)
                {
                    FollowerInfo New = FollowerInfo.NewCharacter(FollowerLocation.Base);
                    DataManager.Instance.Followers.Add(New);
                    FollowerManager.SpawnFollower(New, Here + new Vector3(-2f - Count, 1f));
                    Plugin.Logger?.LogWarning($"{Tag} Cult test: new follower {New.Name} ({New.ID})");
                }
                yield return new WaitForSecondsRealtime(3f);
                yield return Plugin.MonoInstance.StartCoroutine(PickUpAtFeet(2));

                /* A tree by the friend, hit by us: the friend's frames show how it looks to the one who watches */
                yield return new WaitForSecondsRealtime(5f);
                Vector3 Friend = COTLMP.Game.PlayerManager.GetAnyPuppetPosition() ?? Here;
                Structures_Tree Chopped = StructureManager.GetAllStructuresOfType<Structures_Tree>(FollowerLocation.Base)
                    .Where(Brain => !Brain.TreeChopped && !Brain.Data.IsSapling).OrderBy(Brain => (Brain.Data.Position - Friend).sqrMagnitude).FirstOrDefault();
                for (int Swing = 0; Chopped != null && !Chopped.TreeChopped && Swing < 30; Swing++)
                {
                    Chopped.TreeHit(1f);
                    yield return new WaitForSecondsRealtime(0.6f);
                }
                Plugin.Logger?.LogWarning($"{Tag} Cult test: chopped tree {Chopped?.Data?.ID} by the friend");
            }
            else
            {
                Plugin.MonoInstance.StartCoroutine(WatchTree());
                Inventory.ChangeItemQuantity(InventoryItem.ITEM_TYPE.STONE, 4);
                Plugin.Logger?.LogWarning($"{Tag} Cult test: +4 stone");
                yield return new WaitForSecondsRealtime(3f);
                StructuresData Torch = StructuresData.GetInfoByType(StructureBrain.TYPES.DECORATION_TORCH, 0);
                StructureManager.BuildStructure(FollowerLocation.Base, Torch, Here + new Vector3(-3f, -1f), Torch.Bounds);
                Plugin.Logger?.LogWarning($"{Tag} Cult test: built a torch ({Torch.ID})");
                yield return new WaitForSecondsRealtime(3f);
                FollowerInfo Named = DataManager.Instance.Followers.LastOrDefault();
                if (Named != null)
                {
                    Named.Name = "Synced";
                    Plugin.Logger?.LogWarning($"{Tag} Cult test: renamed follower {Named.ID} to Synced");
                }
                yield return new WaitForSecondsRealtime(3f);
                StructureBrain Bed = StructureManager.GetAllStructuresOfType(StructureBrain.TYPES.BED).LastOrDefault();
                if (Bed != null)
                {
                    foreach (Structure Built in Structure.Structures.ToList())
                    {
                        if (Built != null && Built.Brain == Bed)
                            UnityEngine.Object.Destroy(Built.gameObject);
                    }
                    Bed.Remove();
                    Plugin.Logger?.LogWarning($"{Tag} Cult test: removed the bed ({Bed.Data.ID})");
                }
                yield return new WaitForSecondsRealtime(3f);
                yield return Plugin.MonoInstance.StartCoroutine(UseHands());
            }

            /* Frames of both cults at the same moments (both copies run on one clock), named by the time */
            for (int Frame = 1; Frame <= (Host ? 8 : 3); Frame++)
            {
                while (DateTime.Now.Second % 15 != 0)
                    yield return null;
                Shot($"cult-{(Host ? "host" : "client")}-{DateTime.Now:mmss}");
                yield return new WaitForSecondsRealtime(1.5f);
            }
        }

        /// <summary>
        /// The friend's hands in the host's cult: a talk ending in an order to sleep, the cult's data,
        /// a follower gathered for a sermon and let go
        /// </summary>
        /// <summary>
        /// Items dropped at our feet and picked up as the game picks them up: counted once for both
        /// </summary>
        private static IEnumerator PickUpAtFeet(int Count)
        {
            for (int Index = 0; Index < Count; Index++)
            {
                InventoryItem.Spawn(InventoryItem.ITEM_TYPE.MUSHROOM_SMALL, 1, PlayerFarming.Instance.transform.position, 0f);
                yield return new WaitForSecondsRealtime(1f);
            }
            Plugin.Logger?.LogWarning($"{Tag} Cult test: dropped {Count} mushrooms at our feet, now {Inventory.GetItemQuantity(InventoryItem.ITEM_TYPE.MUSHROOM_SMALL)}");
        }

        /// <summary>
        /// The friend's frames of the host's hits on a tree: we stand by it and watch
        /// </summary>
        private static IEnumerator WatchTree()
        {
            Structures_Tree Hit = null;
            while (Hit == null)
            {
                Hit = StructureManager.GetAllStructuresOfType<Structures_Tree>(FollowerLocation.Base).FirstOrDefault(Brain => Brain.Data.Progress > 0f && !Brain.TreeChopped);
                yield return null;
            }
            PlayerFarming.Instance.transform.position = Hit.Data.Position + new Vector3(1.5f, -1.5f);
            for (int Frame = 0; Frame < 30; Frame++)
            {
                Shot($"tree-{Frame:D2}");
                yield return new WaitForSecondsRealtime(0.3f);
            }
        }

        private static IEnumerator UseHands()
        {
            yield return Plugin.MonoInstance.StartCoroutine(PickUpAtFeet(3));
            /* A tree and a rock, hit by hand: the host sees each hit and the stump */
            Vector3 Here = PlayerFarming.Instance.transform.position;
            /* The tree nearest the host, for the host's frames of it */
            Vector3 Near = COTLMP.Game.PlayerManager.GetAnyPuppetPosition() ?? Here;
            Structures_Tree Tree = StructureManager.GetAllStructuresOfType<Structures_Tree>(FollowerLocation.Base)
                .Where(Brain => !Brain.TreeChopped && !Brain.Data.IsSapling).OrderBy(Brain => (Brain.Data.Position - Near).sqrMagnitude).FirstOrDefault();
            for (int Swing = 0; Tree != null && !Tree.TreeChopped && Swing < 30; Swing++)
            {
                Tree.TreeHit(1f);
                yield return new WaitForSecondsRealtime(0.7f);
            }
            Plugin.Logger?.LogWarning($"{Tag} Cult test: chopped tree {Tree?.Data?.ID}");
            Structures_Rubble Rock = StructureManager.GetAllStructuresOfType<Structures_Rubble>(FollowerLocation.Base)
                .OrderBy(Brain => (Brain.Data.Position - Here).sqrMagnitude).FirstOrDefault();
            for (int Swing = 0; Rock != null && !Rock.ProgressFinished && Swing < 30; Swing++)
            {
                Rock.RemovalProgress += Rock.RemovalDurationInGameMinutes / 6f;
                Rock.UpdateProgress();
                yield return new WaitForSecondsRealtime(0.7f);
            }
            Plugin.Logger?.LogWarning($"{Tag} Cult test: broke rock {Rock?.Data?.ID}");
            yield return new WaitForSecondsRealtime(3f);

            Follower Talked = FollowerManager.FindFollowerByID(DataManager.Instance.Followers.LastOrDefault()?.ID ?? 0);
            interaction_FollowerInteraction Talk = Talked?.GetComponentInChildren<interaction_FollowerInteraction>(true);
            if (Talk != null)
            {
                Talk.OnInteract(PlayerFarming.Instance.state);
                Plugin.Logger?.LogWarning($"{Tag} Cult test: talking to {Talked.Brain.Info.Name} ({Talked.Brain.Info.ID}), in our hands: {CultHands.FriendRuns(Talked.Brain.Info.ID)}");
                yield return new WaitForSecondsRealtime(4f);
                AccessTools.Method(typeof(interaction_FollowerInteraction), "OnFollowerCommandFinalized")
                    ?.Invoke(Talk, new object[] { new[] { FollowerCommands.Sleep } });

                /* The command wheel closes the talk when a player picks; picked here, we close it */
                yield return new WaitForSecondsRealtime(1f);
                var Wheel = Lamb.UI.UIManager.GetActiveMenu<Lamb.UI.FollowerInteractionWheel.UIFollowerInteractionWheelOverlayController>();
                Wheel?.Hide();
                if (AccessTools.Field(typeof(interaction_FollowerInteraction), "Activated")?.GetValue(Talk) is true)
                    AccessTools.Method(typeof(interaction_FollowerInteraction), "Close", new[] { typeof(bool), typeof(bool), typeof(bool) })
                        ?.Invoke(Talk, new object[] { true, true, false });
                Plugin.Logger?.LogWarning($"{Tag} Cult test: told {Talked.Brain.Info.ID} to sleep, override {Talked.Brain.CurrentOverrideTaskType}");
            }
            else
            {
                Plugin.Logger?.LogWarning($"{Tag} Cult test: no follower to talk to");
            }
            yield return new WaitForSecondsRealtime(6f);

            DataManager.Instance.PreviousSermonDayIndex = TimeManager.CurrentDay;
            DataManager.Instance.LastFeastDeclared = 777f;
            Plugin.Logger?.LogWarning($"{Tag} Cult test: sermon day {TimeManager.CurrentDay}, feast at 777");
            yield return new WaitForSecondsRealtime(3f);

            FollowerBrain Gathered = DataManager.Instance.Followers.Where(Info => Info != null && Info.ID != Talked?.Brain?.Info?.ID)
                .Select(Info => FollowerBrain.FindBrainByID(Info.ID)).FirstOrDefault(Brain => Brain != null);
            if (Gathered == null)
                yield break;
            try
            {
                Gathered.HardSwapToTask(new FollowerTask_AttendTeaching());
            }
            catch (Exception e)
            {
                /* The test's cult has no temple to go to */
                Plugin.Logger?.LogWarning($"{Tag} Cult test: gathering without a temple: {e.Message}");
            }
            Plugin.Logger?.LogWarning($"{Tag} Cult test: {Gathered.Info.Name} ({Gathered.Info.ID}) gathered for a sermon, in our hands: {CultHands.FriendRuns(Gathered.Info.ID)}");
            yield return new WaitForSecondsRealtime(25f);
            try
            {
                Gathered.CompleteCurrentTask();
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"{Tag} Cult test: letting go without a temple: {e.Message}");
            }
            Plugin.Logger?.LogWarning($"{Tag} Cult test: {Gathered.Info.ID} let go at {Gathered.Location}");
            yield return new WaitForSecondsRealtime(5f);

            /* Changes lost on the way: the sync check has to find each and take the host's */
            FollowerInfo Renamed = DataManager.Instance.Followers.FirstOrDefault(Info => Info?.Name == "Synced");
            if (Renamed != null)
                Renamed.Name = "Diverged";
            DataManager.Instance.PreviousSermonDayIndex = 99;
            Inventory.ChangeItemQuantity(InventoryItem.ITEM_TYPE.STONE, 3);
            StructureBrain Torch = StructureManager.GetAllStructuresOfType(StructureBrain.TYPES.DECORATION_TORCH).FirstOrDefault();
            if (Torch != null)
            {
                foreach (Structure Built in Structure.Structures.ToList())
                {
                    if (Built != null && Built.Brain == Torch)
                        UnityEngine.Object.Destroy(Built.gameObject);
                }
                Torch.Remove();
            }
            StructuresData Extra = StructuresData.GetInfoByType(StructureBrain.TYPES.DECORATION_TORCH, 0);
            StructureManager.BuildStructure(FollowerLocation.Base, Extra, Here + new Vector3(4f, 2f), Extra.Bounds);
            yield return null;
            CultSync.LoseChangesForTest();
            Plugin.Logger?.LogWarning($"{Tag} Cult test: lost a rename, the sermon day, 3 stone, torch {Torch?.Data?.ID} and an extra torch {Extra.ID} on the way");
        }

        private static string CultState()
        {
            if (DataManager.Instance == null)
                return "";
            var Followers = DataManager.Instance.Followers;
            return $"wood={Inventory.GetItemQuantity(InventoryItem.ITEM_TYPE.LOG)} stone={Inventory.GetItemQuantity(InventoryItem.ITEM_TYPE.STONE)} " +
                   $"structures={StructureManager.StructuresDataAtLocation(FollowerLocation.Base).Count} beds={StructureManager.GetAllStructuresOfType(StructureBrain.TYPES.BED).Count} " +
                   $"torches={StructureManager.GetAllStructuresOfType(StructureBrain.TYPES.DECORATION_TORCH).Count} followers={Followers.Count}:{string.Join(",", Followers.Select(Info => Info?.Name))} " +
                   $"bodies={FollowerManager.FollowersAtLocation(FollowerLocation.Base).Count} temple={Interaction_Temple.Instance != null} " +
                   $"mushrooms={Inventory.GetItemQuantity(InventoryItem.ITEM_TYPE.MUSHROOM_SMALL)} trees={StructureManager.GetAllStructuresOfType<Structures_Tree>(FollowerLocation.Base).Count} rocks={StructureManager.GetAllStructuresOfType<Structures_Rubble>(FollowerLocation.Base).Count} hits={CultResources.HitsSent}/{CultResources.HitsApplied} " +
                   $"sermonday={DataManager.Instance.PreviousSermonDayIndex} feast={DataManager.Instance.LastFeastDeclared:F0} " +
                   $"checks={CultSync.ChecksSent}/{CultSync.ChecksTaken}/{CultSync.GroupsDiffered}/{CultSync.FixesAsked}/{CultSync.FixesSent}/{CultSync.FixesTaken} load={CultSync.LoadMsPerSecond:F2}/{CultSync.WorstFrameMs:F2}/{CultSync.KBPerSecond:F2} " +
                   $"fox={COTLMP.Game.FoxSync.Talks}/{COTLMP.Game.FoxSync.Mirrored}/{COTLMP.Game.FoxSync.Refused} lobby={COTLMP.Game.Lobby.Opened}/{COTLMP.Game.Lobby.Started}/{COTLMP.Game.Lobby.Stood}/{COTLMP.Game.Lobby.Heard} intro={COTLMP.Game.IntroCoop.State} " +
                   $"versions={COTLMP.Game.VersionCheck.Partner}/{COTLMP.Network.Updater.Version}/{COTLMP.Game.VersionCheck.Warned}/{COTLMP.Game.VersionCheck.Suggested}/{COTLMP.Game.VersionCheck.SuggestionsTaken} " +
                   $"hands={CultHands.Taken}/{CultHands.Given}/{CultHands.TakenBack}/{CultHands.Ticked}/{CultHands.Moved}/{CultHands.FriendCount}/{CultHands.PicksLeftToHost} data={CultSync.DataApplied} " +
                   $"tasks={string.Join(",", Followers.Select(Info => $"{Info?.ID}:{FollowerBrain.FindBrainByID(Info?.ID ?? 0)?.CurrentTaskType}:{Info?.Location}:{Info?.CurrentOverrideTaskType}"))}";
        }

        private static IEnumerator PlayAttacks()
        {
            int Next = 0;
            while (true)
            {
                yield return new WaitForSecondsRealtime(3f);
                PlayerFarming Player = PlayerFarming.Instance;
                if (Player?.Spine?.AnimationState == null || MMTransition.IsPlaying || !AnyRemotePlayer())
                    continue;
                var Attacks = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(System.Linq.Enumerable.Where(Player.Spine.Skeleton.Data.Animations.Items,
                    Item => Item != null && Item.Name.IndexOf("attack", StringComparison.OrdinalIgnoreCase) >= 0), Item => Item.Name));
                if (Attacks.Count == 0)
                    continue;
                string Name = Attacks[Next++ % Attacks.Count];
                Player.Spine.AnimationState.SetAnimation(0, Name, false);
                Player.Spine.AnimationState.AddAnimation(0, "idle", true, 0f);
                Plugin.Logger?.LogWarning($"{Tag} Playing {Name}");
                yield return new WaitForSecondsRealtime(0.25f);
                Shot($"anim-{(Plugin.GlobalsInternal.IsServerCreator ? "host" : "client")}-{Next}");
            }
        }

        /// <summary>
        /// A reader in the room the two first share deals two cards, to the host first
        /// </summary>
        private static IEnumerator DrawTarot()
        {
            while (PlayerFarming.Instance == null || !CoopSync.InSession || CoopSync.CurrentRun == 0 || !AnyRemotePlayer() ||
                   MMBiomeGeneration.BiomeGenerator.Instance?.CurrentRoom == null || MMTransition.IsPlaying)
                yield return new WaitForSecondsRealtime(1f);
            yield return new WaitForSecondsRealtime(Plugin.GlobalsInternal.IsServerCreator ? 3f : 8f);

            GameObject Holder = new("COTLMP Test Tarot Reader");
            Holder.SetActive(false);
            Holder.transform.position = new Vector3(1.5f, 2.5f, 0f);
            Interaction_Tarot Reader = Holder.AddComponent<Interaction_Tarot>();
            Reader.playerFarming = PlayerFarming.Instance;
            var GetCard = typeof(Interaction_Tarot).GetMethod("GetCard", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var First = (TarotCards.TarotCard)GetCard.Invoke(Reader, new object[] { true });
            var Second = (TarotCards.TarotCard)GetCard.Invoke(Reader, new object[] { First == null || Array.IndexOf(TarotCards.CorruptedCards, First.CardType) < 0 });
            Plugin.Logger?.LogWarning($"{Tag} Tarot test at {CoopSync.CurrentPlace()}: {First?.CardType} {First?.UpgradeIndex}, {Second?.CardType} {Second?.UpgradeIndex}");
            UnityEngine.Object.Destroy(Holder);
        }

        /// <summary>
        /// Cuts the nearest neutral thing (grass mostly) as a hit would, while the partner is in the room
        /// </summary>
        private static IEnumerator CutThings()
        {
            var Wait = new WaitForSecondsRealtime(2f);
            while (true)
            {
                yield return Wait;
                if (PlayerFarming.Instance == null || MMTransition.IsPlaying || !AnyRemotePlayer() || !CoopSync.InSession)
                    continue;
                Health Nearest = null;
                float Best = float.MaxValue;
                foreach (Health Thing in Health.neutralTeam)
                {
                    if (Thing == null || !Thing.enabled || Thing.HP <= 0f || Thing.isPlayer || !Thing.gameObject.activeInHierarchy)
                        continue;
                    float Distance = Vector2.Distance(Thing.transform.position, PlayerFarming.Instance.transform.position);
                    if (Distance < Best)
                    {
                        Best = Distance;
                        Nearest = Thing;
                    }
                }
                if (Nearest == null)
                    continue;
                Plugin.Logger?.LogWarning($"{Tag} Cutting {Nearest.gameObject.name} at {Format(Nearest.transform.position)}");
                Nearest.DealDamage(Nearest.HP + 1f, PlayerFarming.Instance.gameObject, Nearest.transform.position, false, Health.AttackTypes.Melee, false, (Health.AttackFlags)0);
            }
        }

        private static bool HasNextLayer(MMBiomeGeneration.BiomeRoom Room)
        {
            foreach (var Link in new[] { Room.N_Room, Room.E_Room, Room.S_Room, Room.W_Room })
            {
                if (Link != null && Link.ConnectionType == MMRoomGeneration.GenerateRoom.ConnectionTypes.NextLayer)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// One step towards the floor's exit, or through it: the map comes up, the host picks the
        /// last way on, the joining copy clicks the first (which only waits for the host's pick)
        /// </summary>
        private static IEnumerator GoToNextFloor(MMBiomeGeneration.BiomeRoom Room, bool Host)
        {
            MMBiomeGeneration.BiomeRoom Exit = MMBiomeGeneration.BiomeGenerator.Instance?.Rooms?.Find(HasNextLayer);
            if (Exit == null)
            {
                Plugin.Logger?.LogWarning($"{Tag} No way to the next floor on this one");
                yield return new WaitForSecondsRealtime(5f);
                yield break;
            }
            if (Exit != Room)
            {
                Vector2Int? Step = NextStep(Room, new Vector2Int(Exit.x, Exit.y));
                if (!Step.HasValue)
                    yield break;
                Vector2Int Gap = Step.Value;
                Plugin.Logger?.LogWarning($"{Tag} Heading for the exit at {Exit.x},{Exit.y}: {Gap} from {Room.x},{Room.y}");
                MMTransition.Play(MMTransition.TransitionType.ChangeRoom, MMTransition.Effect.BlackFade, MMTransition.NO_SCENE, 0.5f, "", () => MMBiomeGeneration.BiomeGenerator.ChangeRoom(Gap));
                yield break;
            }

            Door Way = Door.Doors.Find(Item => Item != null && Item.isActiveAndEnabled && Item.ConnectionType == MMRoomGeneration.GenerateRoom.ConnectionTypes.NextLayer);
            if (Way == null || PlayerFarming.Instance == null)
            {
                Plugin.Logger?.LogWarning($"{Tag} No door to the next floor in the exit room");
                yield return new WaitForSecondsRealtime(5f);
                yield break;
            }
            int Floor = GameManager.CurrentDungeonFloor;
            Plugin.Logger?.LogWarning($"{Tag} Through the door to the next floor from floor {Floor}");
            typeof(Door).GetMethod("OnTriggerEnter2D", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                .Invoke(Way, new object[] { PlayerFarming.Instance.circleCollider2D });

            float Waited = 0f;
            while (MapSync.Open == null && Waited < 20f)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                Waited += 0.5f;
            }
            Lamb.UI.UIAdventureMapOverlayController Overlay = MapSync.Open;
            if (Overlay == null)
            {
                Plugin.Logger?.LogWarning($"{Tag} No map came up");
                yield break;
            }

            yield return new WaitForSecondsRealtime(3f);
            global::Map.Node Current = Overlay.Map?.GetCurrentNode();
            if (Host && !NoMapClick && MapSync.Open == Overlay && Current != null && Current.outgoing.Count > 0)
            {
                global::Map.Point Choice = Host ? Current.outgoing[Current.outgoing.Count - 1] : Current.outgoing[0];
                Lamb.UI.AdventureMapNode Shown = Overlay.NodeFromPoint(Choice);
                Plugin.Logger?.LogWarning($"{Tag} Clicking node {Choice.x},{Choice.y} ({Shown?.MapNode?.nodeType}) of {Current.outgoing.Count} ways on, map from the host {MapSync.FollowingHost}");
                if (Shown != null)
                    Overlay.OnNodeChosen(Shown);
            }

            Waited = 0f;
            while (GameManager.CurrentDungeonFloor == Floor && Waited < 60f)
            {
                yield return new WaitForSecondsRealtime(1f);
                Waited += 1f;
            }
            while (MMTransition.IsPlaying && Waited < 90f)
            {
                yield return new WaitForSecondsRealtime(1f);
                Waited += 1f;
            }
            yield return new WaitForSecondsRealtime(3f);
            global::Map.Node Now = global::Map.MapManager.Instance?.CurrentNode;
            Plugin.Logger?.LogWarning($"{Tag} Next floor: floor {GameManager.CurrentDungeonFloor} node {Now?.point.x},{Now?.point.y} {Now?.nodeType} modifier {(Now?.Modifier != null ? Now.Modifier.name : "-")} seed {MMBiomeGeneration.BiomeGenerator.Instance?.Seed} place {CoopSync.CurrentPlace()}");
        }

        /// <summary>
        /// The first door on the shortest way through the floor's rooms to a target room
        /// </summary>
        private static Vector2Int? NextStep(MMBiomeGeneration.BiomeRoom From, Vector2Int To)
        {
            var Seen = new System.Collections.Generic.HashSet<MMBiomeGeneration.BiomeRoom> { From };
            var Queue = new System.Collections.Generic.Queue<(MMBiomeGeneration.BiomeRoom, Vector2Int)>();
            Queue.Enqueue((From, Vector2Int.zero));

            while (Queue.Count > 0)
            {
                var (Room, First) = Queue.Dequeue();
                if (Room.x == To.x && Room.y == To.y)
                    return First;

                foreach (var (Link, Dir) in new[] { (Room.N_Room, new Vector2Int(0, 1)), (Room.E_Room, new Vector2Int(1, 0)), (Room.S_Room, new Vector2Int(0, -1)), (Room.W_Room, new Vector2Int(-1, 0)) })
                {
                    if (Link == null || !Link.Connected || Link.Room == null || !Seen.Add(Link.Room))
                        continue;
                    Queue.Enqueue((Link.Room, Room == From ? Dir : First));
                }
            }
            return null;
        }

        /// <summary>
        /// Nobody presses a button in the test copies: move conversations on as a player would,
        /// picking the first answer when there is a choice
        /// </summary>
        private static IEnumerator AdvanceConversations()
        {
            var Next = HarmonyLib.AccessTools.Method(typeof(MMTools.MMConversation), "ShowNextLine");
            var Answer = HarmonyLib.AccessTools.Method(typeof(MMTools.MMConversation), "OnGiveAnswer");
            var Wait = new WaitForSecondsRealtime(2f);

            while (true)
            {
                yield return Wait;
                var Conversation = MMTools.MMConversation.mmConversation;
                if (!MMTools.MMConversation.isPlaying || Conversation == null || MMTools.MMConversation.isBark)
                    continue;
                try
                {
                    var Current = MMTools.MMConversation.CURRENT_CONVERSATION;
                    bool Last = Current?.Entries != null && MMTools.MMConversation.Position >= Current.Entries.Count - 1;
                    if (Last && Current.Responses != null && Current.Responses.Count > 0)
                    {
                        /* The voting test answers on its own */
                        if (AutoChoice > 0)
                            continue;
                        Answer.Invoke(Conversation, new object[] { 0 });
                    }
                    else
                        Next.Invoke(Conversation, new object[] { true });
                    Plugin.Logger?.LogWarning($"{Tag} Advanced a conversation to line {MMTools.MMConversation.Position}");
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogWarning($"{Tag} Advancing a conversation failed: {e.Message}");
                }
            }
        }

        private static int CountVisibleEnemies()
        {
            int Count = 0;
            foreach (Health Enemy in Health.team2)
            {
                if (Enemy == null || Enemy.HP <= 0f || !Enemy.gameObject.activeInHierarchy || Enemy.GetComponent<UnitObject>() == null)
                    continue;
                var Spine = Enemy.GetComponentInChildren<Spine.Unity.SkeletonAnimation>();
                var Renderer = Spine != null ? Spine.GetComponent<MeshRenderer>() : null;
                if (Renderer == null || Renderer.enabled)
                    ++Count;
            }
            return Count;
        }

        private static bool AnyRemotePlayer()
        {
            for (uint Id = 0; Id < InternalData.MaxPlayersPerServerInternal; Id++)
            {
                if (PlayerManager.DoesPlayerExist(Id))
                    return true;
            }
            return false;
        }

        private static IEnumerator QuitLater()
        {
            yield return new WaitForSecondsRealtime(QuitAfter);
            Plugin.Logger?.LogWarning("[QA] Quitting after the test time limit");
            Application.Quit();
        }
    }
}

/* EOF */
