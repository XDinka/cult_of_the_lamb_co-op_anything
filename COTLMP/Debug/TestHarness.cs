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
    /// <item><c>-cotlmp-steamselftest</c>: after hosting, send a packet to our own SteamID over real Steam</item>
    /// </list>
    /// </summary>
    internal static class TestHarness
    {
        private static bool AutoHost, AutoEnter, AutoWalk, Report, SteamSelfTest, AutoKill;
        private static float QuitAfter, EndAfter, AutoDungeon;
        private static ulong RejoinHost;

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

            if (AutoHost || AutoEnter)
                Plugin.MonoInstance.StartCoroutine(EnterCult(AutoHost));
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

        private static IEnumerator ReportLoop()
        {
            var Wait = new WaitForSecondsRealtime(2f);
            StringBuilder Line = new();

            while (true)
            {
                yield return Wait;

                Line.Clear();
                Line.Append($"{Tag} place={COTLMP.Game.CoopSync.CurrentPlace()} seed={COTLMP.Game.CoopSync.LastSeed} enemies={Health.team2.Count} seen={COTLMP.Game.CoopSync.EnemiesSeen} hpsent={COTLMP.Game.CoopSync.HitsSent} hpapplied={COTLMP.Game.CoopSync.HitsApplied} killsapplied={COTLMP.Game.CoopSync.KillsApplied}");
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
                yield return new WaitForSecondsRealtime(5f);
                MMBiomeGeneration.BiomeRoom Room = MMBiomeGeneration.BiomeGenerator.Instance?.CurrentRoom;
                if (Room == null || MMTransition.IsPlaying)
                    continue;

                /* Move on together: wait for the partner in this room, then for the fight to end (or 40 s) */
                float Waited = 0f;
                while (Waited < 90f && !AnyRemotePlayer())
                {
                    yield return new WaitForSecondsRealtime(1f);
                    Waited += 1f;
                }
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
                /* Fight only with the partner in the room, like players would */
                if (PlayerFarming.Instance == null || MMTransition.IsPlaying || Health.team2.Count == 0 || !AnyRemotePlayer())
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

                bool Hit = Nearest.DealDamage(Mathf.Max(1f, Nearest.totalHP * 0.4f), PlayerFarming.Instance.gameObject, Nearest.transform.position, false, Health.AttackTypes.Melee, true);
                Plugin.Logger?.LogWarning($"{Tag} Hit {Nearest.name} -> {Hit}, hp {Nearest.HP:F1}/{Nearest.totalHP:F1}");
            }
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
