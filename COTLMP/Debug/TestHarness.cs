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
    /// <item><c>-cotlmp-autodie=N</c>: N seconds after entering a dungeon, die once (knockout test); the other copy walks over to revive</item>
    /// <item><c>-cotlmp-solo</c>: the host fights and moves on without waiting for the partner (cleared-room test); the joining copy walks the host's trail</item>
    /// <item><c>-cotlmp-autotalk=N</c>: N seconds after both stand in one dungeon room, hold a short conversation (dialog sync test)</item>
    /// <item><c>-cotlmp-autoshoot</c>: while running a shared room, fire an enemy projectile at the partner every 3 s (projectile sync test)</item>
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
        private static float AutoTalk;
        private static bool AutoShoot;

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
                else if (Arg.Equals("-cotlmp-autoshoot", StringComparison.OrdinalIgnoreCase))
                    AutoShoot = true;
                else if (Arg.Equals("-cotlmp-solo", StringComparison.OrdinalIgnoreCase))
                    Solo = true;
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
            if (!string.IsNullOrEmpty(ShotsDir))
                Plugin.MonoInstance.StartCoroutine(Shots());
            if (AutoDie > 0)
                Plugin.MonoInstance.StartCoroutine(DieOnce());
            if (AutoTalk > 0)
                Plugin.MonoInstance.StartCoroutine(TalkOnce());
            if (AutoShoot)
                Plugin.MonoInstance.StartCoroutine(Shoot());
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

        private static IEnumerator ReportLoop()
        {
            var Wait = new WaitForSecondsRealtime(2f);
            StringBuilder Line = new();

            while (true)
            {
                yield return Wait;

                Line.Clear();
                Line.Append($"{Tag} place={COTLMP.Game.CoopSync.CurrentPlace()} seed={COTLMP.Game.CoopSync.LastSeed} enemies={Health.team2.Count} seen={COTLMP.Game.CoopSync.EnemiesSeen} hpsent={COTLMP.Game.CoopSync.HitsSent} hpapplied={COTLMP.Game.CoopSync.HitsApplied} killsapplied={COTLMP.Game.CoopSync.KillsApplied}");
                Line.Append($" own={COTLMP.Game.RoomSync.Owning} follow={COTLMP.Game.RoomSync.Following} snapsent={COTLMP.Game.RoomSync.SnapshotsSent} snapapplied={COTLMP.Game.RoomSync.SnapshotsApplied} puppethits={COTLMP.Game.RoomSync.PuppetHitsSent} hitstaken={COTLMP.Game.RoomSync.HitsTaken} removed={COTLMP.Game.RoomSync.EnemiesRemoved} followed={COTLMP.Game.RoomSync.FollowedCount} visible={CountVisibleEnemies()} pickups={COTLMP.Game.SharedPickups.Sent}/{COTLMP.Game.SharedPickups.Received}/{COTLMP.Game.SharedPickups.Removed}");
                Line.Append($" knockouts={COTLMP.Game.CoopLife.KnockOuts} revives={COTLMP.Game.CoopLife.Revives} down={COTLMP.Game.CoopLife.WeAreDown} partnerdown={COTLMP.Game.CoopLife.PartnerDown}");
                Line.Append($" cleared={COTLMP.Game.ClearedRooms.Sent}/{COTLMP.Game.ClearedRooms.Applied}/{COTLMP.Game.ClearedRooms.EnemiesHidden}");
                Line.Append($" dialog={COTLMP.Game.Dialogs.LinesSent}/{COTLMP.Game.Dialogs.LinesShown} camtargets={GameManager.GetInstance()?.CamFollowTarget?.targets.Count}:{(GameManager.GetInstance()?.CamFollowTarget?.targets.Count > 0 ? GameManager.GetInstance().CamFollowTarget.targets[0].gameObject?.name : "-")}");
                Line.Append($" shots={COTLMP.Game.ProjectileSync.Sent}/{COTLMP.Game.ProjectileSync.Shown}/{COTLMP.Game.ProjectileSync.Missing} shotsseen={COTLMP.Game.ProjectileSync.Seen} live={Projectile.Projectiles.Count}");
                Line.Append($" seeded={COTLMP.Game.RoomRandom.Seeded} chest={Interaction_Chest.Instance != null}");
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

                    if (Partner == null || Mine == null || Partner == Mine || !Partner.Contains("@") || !Mine.Contains("@") ||
                        Partner.Substring(0, Partner.IndexOf('@')) != Mine.Substring(0, Mine.IndexOf('@')))
                        continue;
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

        private static IEnumerator Shoot()
        {
            var Wait = new WaitForSecondsRealtime(3f);
            while (true)
            {
                yield return Wait;
                if (!COTLMP.Game.RoomSync.Owning)
                    continue;
                GameObject Prefab = COTLMP.Game.ProjectileSync.AnyPrefab();
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
                        Answer.Invoke(Conversation, new object[] { 0 });
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
