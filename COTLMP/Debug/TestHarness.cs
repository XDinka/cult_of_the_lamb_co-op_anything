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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
    /// <item><c>-cotlmp-animtest</c>: every 3 s, while the partner is in the room, play one of the lamb's attack animations (puppet animation test)</item>
    /// <item><c>-cotlmp-nomapclick</c>: with -cotlmp-nextfloor, never click on the map, only wait for the partner's pick</item>
    /// <item><c>-cotlmp-solo</c>: the host fights and moves on without waiting for the partner (cleared-room test); the joining copy walks the host's trail</item>
    /// <item><c>-cotlmp-autotalk=N</c>: N seconds after both stand in one dungeon room, hold a short conversation (dialog sync test)</item>
    /// <item><c>-cotlmp-autoshoot</c>: while running a shared room, fire an enemy projectile at the partner every 3 s (projectile sync test)</item>
    /// <item><c>-cotlmp-autochoice=N</c>: N s after both stand in the cult, answer together: the host asks, sees which answer the partner points at and picks it; then the joining player asks, clicks an answer (it only points) and gets the host's pick (dialog choice test)</item>
    /// <item><c>-cotlmp-crashafter=N</c>: in a shared dungeon run, take a sword, a curse and a tarot card, then kill our own process N s later, the way a crash would (run recovery test)</item>
    /// <item><c>-cotlmp-netshots</c>: with -cotlmp-shots, save a frame the first time each kind of bad connection icon shows</item>
    /// <item><c>-cotlmp-netflood</c>: every 3 s send the partner a 20 KB packet and a burst of 300 small ones of a kind they ignore (network stress test; pair it with -cotlmp-netchaos=LOSS,DELAY,JITTER,EVERY,OUTAGE for a bad connection)</item>
    /// <item><c>-cotlmp-drawtest</c>: in each shared dungeon room draw 3 tarot cards the way a chest would and log them (host pools test); the host also gets extra unlocks in memory so the two saves differ</item>
    /// <item><c>-cotlmp-shots=DIR</c>: save the game's own frame to DIR a few seconds into every room shared with the partner</item>
    /// <item><c>-cotlmp-steamselftest</c>: after hosting, send a packet to our own SteamID over real Steam</item>
    /// </list>
    /// </summary>
    internal static class TestHarness
    {
        private static bool AutoHost, AutoEnter, AutoWalk, Report, SteamSelfTest, AutoKill;
        private static float QuitAfter, EndAfter, AutoDungeon;
        private static ulong RejoinHost;
        private static string ShotsDir;
        private static float AutoDie;
        private static bool Solo;
        private static bool NextFloor;
        private static bool NoMapClick;
        private static bool AnimTest;
        private static bool TarotTest;
        private static bool AutoCut;
        private static int[] WindowRect;
        private static float AutoTalk;
        private static bool AutoShoot;
        private static bool DrawTest;
        private static bool NetFlood, NetShots;
        private static float CrashAfter;
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
                else if (Arg.StartsWith("-cotlmp-crashafter=", StringComparison.OrdinalIgnoreCase))
                    float.TryParse(Arg.Substring("-cotlmp-crashafter=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out CrashAfter);
                else if (Arg.Equals("-cotlmp-drawtest", StringComparison.OrdinalIgnoreCase))
                    DrawTest = true;
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
            if (NextFloor && AutoEnter && !NoMapClick)
                Plugin.MonoInstance.StartCoroutine(ClickMaps());
            if (TarotTest)
                Plugin.MonoInstance.StartCoroutine(DrawTarot());
            if (AutoCut)
                Plugin.MonoInstance.StartCoroutine(CutThings());
            if (AutoHost || AutoEnter)
                Plugin.MonoInstance.StartCoroutine(EnterCult(AutoHost));
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
            if (AutoDie > 0)
                Plugin.MonoInstance.StartCoroutine(DieOnce());
            if (AutoTalk > 0)
                Plugin.MonoInstance.StartCoroutine(TalkOnce());
            if (AutoShoot)
                Plugin.MonoInstance.StartCoroutine(Shoot());
            if (DrawTest)
                Plugin.MonoInstance.StartCoroutine(Draw());
            if (NetFlood)
                Plugin.MonoInstance.StartCoroutine(Flood());
            if (CrashAfter > 0)
                Plugin.MonoInstance.StartCoroutine(CrashLater());
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
        /// Both sides of the dialog choice test, by role: the host picks, the joining player points.
        /// </summary>
        private static IEnumerator VoteOnChoices()
        {
            while (SceneManager.GetActiveScene().name != "Base Biome 1" || PlayerFarming.Instance == null || !AnyRemotePlayer())
                yield return null;
            yield return new WaitForSecondsRealtime(AutoChoice);
            bool Host = Plugin.GlobalsInternal.IsServerCreator;

            /* Round 1: the host asks; the joining player points at the second answer, the host is on the first, then picks the second */
            if (Host)
            {
                Ask("host");
                yield return Until(() => COTLMP.Game.Dialogs.OpenWheel != null && COTLMP.Game.Dialogs.PartnerPickNow >= 0, 30f);
                Plugin.Logger?.LogWarning($"{Tag} Round 1: the partner points at {COTLMP.Game.Dialogs.PartnerPickNow}");
                MoveTo(0);
                yield return new WaitForSecondsRealtime(2f);
                Shot("choice-host-own-wheel");
                yield return new WaitForSecondsRealtime(2f);
                ClickOwn(COTLMP.Game.Dialogs.PartnerPickNow);
            }
            else
            {
                yield return Until(() => COTLMP.Game.Dialogs.RemoteChoiceCount > 0, 40f);
                Plugin.Logger?.LogWarning($"{Tag} Round 1: the host's answers are up ({COTLMP.Game.Dialogs.RemoteChoiceCount})");
                yield return new WaitForSecondsRealtime(1f);
                COTLMP.Game.Dialogs.PickRemote(1);
                yield return Until(() => COTLMP.Game.Dialogs.PartnerPickNow == 0, 10f);
                Plugin.Logger?.LogWarning($"{Tag} Round 1: the host is on {COTLMP.Game.Dialogs.PartnerPickNow}");
                yield return new WaitForSecondsRealtime(0.5f);
                Shot("choice-client-sees-host");
            }

            yield return new WaitForSecondsRealtime(12f);

            /* Round 2: the joining player asks, moves to and clicks the first answer (it only points), the host picks the second */
            if (!Host)
            {
                Ask("client");
                yield return Until(() => COTLMP.Game.Dialogs.OpenWheel != null, 20f);
                yield return new WaitForSecondsRealtime(1f);
                MoveTo(0);
                yield return new WaitForSecondsRealtime(0.5f);
                ClickOwn(0);
                Plugin.Logger?.LogWarning($"{Tag} Round 2: clicked answer 0, wheel still open: {COTLMP.Game.Dialogs.OpenWheel != null}");
                yield return Until(() => COTLMP.Game.Dialogs.PartnerPickNow == 1, 20f);
                yield return new WaitForSecondsRealtime(0.5f);
                Shot("choice-client-own-wheel");
            }
            else
            {
                yield return Until(() => COTLMP.Game.Dialogs.RemoteChoiceCount > 0 && COTLMP.Game.Dialogs.PartnerPickNow >= 0, 40f);
                Plugin.Logger?.LogWarning($"{Tag} Round 2: the partner's answers are up, they point at {COTLMP.Game.Dialogs.PartnerPickNow}");
                COTLMP.Game.Dialogs.Point(1);
                yield return new WaitForSecondsRealtime(2f);
                Shot("choice-host-sees-client");
                yield return new WaitForSecondsRealtime(1f);
                COTLMP.Game.Dialogs.PickRemote(1);
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

        /// <summary>
        /// Gears up in a shared run, then dies the way a crash does: no goodbye, nothing saved.
        /// </summary>
        private static IEnumerator CrashLater()
        {
            while (COTLMP.Game.RunRecovery.Status != "kept" || PlayerFarming.Instance == null)
                yield return null;

            PlayerFarming Player = PlayerFarming.Instance;
            Player.playerWeapon.SetWeapon(EquipmentType.Sword, 2);
            Player.playerSpells.SetSpell(EquipmentType.Fireball, 1);
            TrinketManager.AddTrinket(new TarotCards.TarotCard(TarotCards.Card.MovementSpeed, 0), Player);
            Plugin.Logger?.LogWarning($"{Tag} Geared up: {Player.currentWeapon} {Player.currentWeaponLevel}, {Player.currentCurse}, {Player.RunTrinkets.Count} cards");

            yield return new WaitForSecondsRealtime(CrashAfter);
            Plugin.Logger?.LogWarning($"{Tag} Crashing on purpose in {COTLMP.Game.CoopSync.CurrentPlace()} {Gear()}");
            System.Diagnostics.Process.GetCurrentProcess().Kill();
        }

        private static string Gear()
        {
            PlayerFarming Player = PlayerFarming.Instance;
            if (Player == null)
                return "gear=-";
            return $"gear={Player.currentWeapon}:{Player.currentWeaponLevel}/{Player.currentCurse}/{Player.RunTrinkets.Count}cards/{Player.health?.HP}hp";
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
                Line.Append($" hazards={COTLMP.Game.HazardSync.Sent}/{COTLMP.Game.HazardSync.Shown}/{COTLMP.Game.HazardSync.Missing} moved={COTLMP.Game.HazardSync.Moved} pools={COTLMP.Game.HostPools.Received}/{COTLMP.Game.HostPools.Scopes}");
                Line.Append($" recovery={COTLMP.Game.RunRecovery.Status} {Gear()}");
                Line.Append($" flood={FloodSent}/{COTLMP.Game.CoopSync.UnknownReceived} chaos={COTLMP.Network.LoopbackPeerLink.ChaosStats} ping={COTLMP.Game.ConnectionQuality.PingMs:F0} loss={COTLMP.Game.ConnectionQuality.Loss:F2} broken={COTLMP.Game.ConnectionQuality.Broken} hops={COTLMP.Network.Network.PacketsOut}>{COTLMP.Network.SteamBridge.FromLocal}|{COTLMP.Network.SteamBridge.FromLink}>{COTLMP.Network.Network.PacketsIn}");
                Line.Append($" seeded={COTLMP.Game.RoomRandom.Seeded} chest={Interaction_Chest.Instance != null}");
                Line.Append($" props={PropSync.Sent}/{PropSync.Received}/{PropSync.Applied}/{PropSync.Missing}/{PropSync.AppliedOnEntry}/{PropSync.AlreadyBroken} propmatch={PropSync.ByNumber}/{PropSync.BySpawn}/{PropSync.ByNearest}");
                Line.Append($" tarot={SharedTarot.Sent}/{SharedTarot.Received}/{SharedTarot.Applied} time={TimeManager.CurrentGameTime:F0}:{TimeManager.CurrentPhase} gap={TimeSync.Gap:F1} timesync={TimeSync.Sent}/{TimeSync.Received}/{TimeSync.Skips}");
                Line.Append($" map={MapSync.LayoutsSent}/{MapSync.LayoutsAdopted}/{MapSync.PicksSent}/{MapSync.PicksApplied}/{MapSync.Conflicts} minimap={MiniMapSync.Marked} pose={PuppetSync.Sent}/{PuppetSync.Received}/{PuppetSync.Applied}/{PuppetSync.Appeared}/{PuppetSync.Vanished} enemyanims={RoomSync.EnemyAnimations} puppetanim={(COTLMP.Game.PlayerManager.GetPuppet(Plugin.GlobalsInternal.IsServerCreator ? 1u : 0u)?.Spine?.AnimationState?.GetCurrent(0)?.Animation?.Name)} myanim={(PlayerFarming.Instance?.Spine?.AnimationState?.GetCurrent(0)?.Animation?.Name)} node={(global::Map.MapManager.Instance?.CurrentNode?.point.x)},{(global::Map.MapManager.Instance?.CurrentNode?.point.y)}:{(global::Map.MapManager.Instance?.CurrentNode?.nodeType)}");
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
        /// Plays the lamb's attack animations one after another, for the partner's puppet to repeat
        /// </summary>
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
