/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     An update waiting in a game: saved, then back to the lobby on the same save (or the game restarted for it)
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using BepInEx;
using COTLMP.Network;
using COTLMP.Ui;
using MMTools;
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// Putting an update in that was written while a game went on.
    /// </summary>
    /// <remarks>
    /// The player picks how: restart the lobby (the game is saved, the main menu comes, the new mod takes the
    /// old one's place there and the lobby opens again on the same save) or restart the game (saved, the game
    /// closes and starts again, past its title by itself, and opens the same lobby). Either way it stops at
    /// the lobby, waiting for the players: nothing is played until someone presses Play. A friend, who has no
    /// save of their own, leaves and joins the host again instead. What to open is kept in a mark file
    /// (BepInEx/config/cotlmp-open-lobby), read by the new mod once it is up.
    /// </remarks>
    internal static class Relobby
    {
        private static string Mark => Path.Combine(Paths.ConfigPath, "cotlmp-open-lobby");

        /// <summary>
        /// A lobby is to open again at this start (the test harness then doesn't go into a game by itself)
        /// </summary>
        internal static bool Marked => File.Exists(Mark);

        /* Counters for the test harness report */
        internal static int Opened;

        /// <summary>
        /// The lobby is to open again: nothing else comes up over the main menu meanwhile
        /// </summary>
        internal static bool Pending { get; private set; }

        /* The test harness: a while into a game the update is taken (from Updater.ReleaseFrom) and this is picked */
        internal static float TestAfter = -1f;
        internal static string TestChoice;

        private static bool IsHost => Plugin.GlobalsInternal.IsServerCreator;

        /// <summary>
        /// At the plugin's start: a lobby to open again (after a restart, or in place of the old mod)
        /// </summary>
        internal static void Initialize()
        {
            if (TestAfter > 0f)
                Plugin.MonoInstance.StartCoroutine(TestUpdate());
            try
            {
                if (!File.Exists(Mark))
                    return;
                bool Fresh = DateTime.Now - File.GetLastWriteTime(Mark) < TimeSpan.FromMinutes(10);
                string Text = File.ReadAllText(Mark).Trim();
                File.Delete(Mark);
                if (!Fresh || !int.TryParse(Text, out int Slot))
                    return;
                Plugin.Logger?.LogInfo($"[LOBBY] Opening the lobby on save slot {Slot} again");
                COTLMP.Debug.PressStart.On = true;
                Pending = true;
                Plugin.MonoInstance.StartCoroutine(Open(Slot));
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[LOBBY] Can't open the lobby again: {e.Message}");
            }
        }

        private static IEnumerator Open(int Slot)
        {
            /* The main menu up and standing (after the new mod brought it up again) */
            yield return new WaitForSecondsRealtime(2f);
            float Since = Time.realtimeSinceStartup;
            bool Told = false;
            while (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != HotUpdate.MenuScene || MMTransition.IsPlaying || !OnlineMenu.Shown)
            {
                if (!Told && Time.realtimeSinceStartup - Since > 30f)
                {
                    Told = true;
                    Plugin.Logger?.LogWarning($"[LOBBY] Still waiting for the main menu: scene {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}, " +
                                              $"transition {MMTransition.IsPlaying}, menu shown {OnlineMenu.Shown}");
                    COTLMP.Debug.HudProbe.Take("lobby-waiting");
                }
                yield return null;
            }
            yield return new WaitForSecondsRealtime(1.5f);
            COTLMP.Debug.PressStart.On = false;
            OnlineMenu.ShowOnline();
            yield return new WaitForSecondsRealtime(1f);
            Lobby.HostFrom(Slot, false, default);
            Pending = false;
            ++Opened;
            Plugin.Logger?.LogInfo($"[LOBBY] The lobby is open again on save slot {Slot}: {Lobby.Open}");
            yield return new WaitForSecondsRealtime(3f);
            COTLMP.Debug.HudProbe.Take("lobby-again");
        }

        /// <summary>
        /// The update is written and we are in a game: how to put it in, or later
        /// </summary>
        internal static void Ask(string Version)
        {
            string Body = string.Format(I2.Loc.LocalizationManager.GetTranslation(IsHost ? "Multiplayer/Update/ReadyHost" : "Multiplayer/Update/ReadyFriend"), Version);
            src.UI.UIMenuConfirmationWindow First = VersionCheck.Open(Body, "Multiplayer/Update/RestartLobby", "Multiplayer/Update/OtherWays",
                                                                       I2.Loc.LocalizationManager.GetTranslation("Multiplayer/Update/ReadyTitle"));
            bool Chosen = false;
            if (TestChoice != null)
                Plugin.MonoInstance.StartCoroutine(Pick(First, TestChoice));
            First.OnConfirm += () =>
            {
                Chosen = true;
                Plugin.MonoInstance.StartCoroutine(Go(false));
            };
            First.OnCancel += () =>
            {
                if (Chosen)
                    return;
                Chosen = true;
                src.UI.UIMenuConfirmationWindow Second = VersionCheck.Open(I2.Loc.LocalizationManager.GetTranslation("Multiplayer/Update/RestartGameBody"),
                                                                            "Multiplayer/Update/RestartGame", "Multiplayer/Update/Later",
                                                                            I2.Loc.LocalizationManager.GetTranslation("Multiplayer/Update/ReadyTitle"));
                Second.OnConfirm += () => Plugin.MonoInstance.StartCoroutine(Go(true));
                Second.OnCancel += () => Updater.TellReady();
            };
        }

        /* THE TEST HARNESS ***************************************************/

        private static IEnumerator TestUpdate()
        {
            if (File.Exists(Updater.TakenMark))
            {
                Plugin.Logger?.LogInfo($"[QA] Started again after the in-game update ({File.ReadAllText(Updater.TakenMark)})");
                File.Delete(Updater.TakenMark);
                yield break;
            }
            while (!CoopSync.InSession || PlayerFarming.Instance == null || MMTransition.IsPlaying)
                yield return new WaitForSecondsRealtime(1f);
            yield return new WaitForSecondsRealtime(TestAfter);
            File.WriteAllText(Updater.TakenMark, TestChoice ?? "");
            Plugin.Logger?.LogWarning($"[QA] Taking an update in the game, choosing {TestChoice}");
            Updater.TakeVersion("0.3.6", 0);
        }

        private static IEnumerator Pick(src.UI.UIMenuConfirmationWindow First, string Choice)
        {
            yield return new WaitForSecondsRealtime(2f);
            COTLMP.Debug.HudProbe.Take("update-ready");
            if (Choice == "lobby")
            {
                First.OnConfirm?.Invoke();
                First.Hide();
                yield break;
            }
            First.OnCancel?.Invoke();
            First.Hide();
            yield return new WaitForSecondsRealtime(2f);
            COTLMP.Debug.HudProbe.Take("update-other");
            src.UI.UIMenuConfirmationWindow Second = UnityEngine.Object.FindObjectsOfType<src.UI.UIMenuConfirmationWindow>().LastOrDefault();
            if (Second == null)
                yield break;
            if (Choice == "game")
                Second.OnConfirm?.Invoke();
            else
                Second.OnCancel?.Invoke();
            Second.Hide();
        }

        /// <summary>
        /// Saved (the host) or left (the friend), then to the main menu, or the game started again
        /// </summary>
        private static IEnumerator Go(bool Restart)
        {
            Plugin.Logger?.LogInfo($"[UPDATE] Putting the update in: {(Restart ? "the game starts again" : "back to the lobby")}");
            OnlineMenu.Wait("Multiplayer/Update/Saving");
            if (IsHost)
            {
                int Slot = SaveAndLoad.SAVE_SLOT;
                bool Saved = false;
                Action Done = () => Saved = true;
                SaveAndLoad.OnSaveCompleted += Done;
                try
                {
                    SaveAndLoad.Save();
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogWarning($"[UPDATE] Saving failed: {e.Message}");
                    Saved = true;
                }
                float Until = Time.realtimeSinceStartup + 10f;
                while (!Saved && Time.realtimeSinceStartup < Until)
                    yield return null;
                SaveAndLoad.OnSaveCompleted -= Done;
                File.WriteAllText(Mark, Slot.ToString());
                Plugin.Logger?.LogInfo($"[UPDATE] Saved on slot {Slot}{(Saved ? "" : " (no word from the save in 10 s)")}");
            }
            else
            {
                Updater.MarkRejoin(SteamBridge.JoinedHost);
            }

            PauseMenuPatches.EndSession(IsHost ? "Multiplayer/Steam/Stopped" : "Multiplayer/Steam/Left");
            yield return new WaitForSecondsRealtime(0.5f);
            if (Restart)
            {
                if (StartAgain())
                {
                    Application.Quit();
                    yield break;
                }
                Plugin.Logger?.LogWarning("[UPDATE] The game can't start again by itself: back to the lobby instead");
            }
            OnlineMenu.Wait(null);
            MMTransition.Play(MMTransition.TransitionType.ChangeSceneAutoResume, MMTransition.Effect.BlackFade, HotUpdate.MenuScene, 1f, "", null);
        }

        /// <summary>
        /// The game started again once this one has closed, with the arguments it has now
        /// </summary>
        private static bool StartAgain()
        {
            try
            {
                string Exe = Process.GetCurrentProcess().MainModule.FileName;
                string Arguments = string.Join(" ", Environment.GetCommandLineArgs().Skip(1).Select(Arg => Arg.Contains(" ") ? $"\"{Arg}\"" : Arg));
                /* BepInEx's loader marks our process as loaded in its environment: the game it starts would come without mods */
                foreach (string Name in Environment.GetEnvironmentVariables().Keys.Cast<string>().Where(Name => Name.StartsWith("DOORSTOP", StringComparison.OrdinalIgnoreCase)).ToList())
                    Environment.SetEnvironmentVariable(Name, null);
                /* Through the shell, a few seconds on, so it doesn't share our files and sockets and we are gone by then */
                string Mark2 = System.IO.Path.Combine(Paths.ConfigPath, "cotlmp-restart.txt");
                ProcessStartInfo Start = new("cmd.exe", $"/c ping -n 5 127.0.0.1 >nul & echo %DATE% %TIME% > \"{Mark2}\" & start \"\" \"{Exe}\" {Arguments}")
                {
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = Paths.GameRootPath
                };
                Process.Start(Start);
                Plugin.Logger?.LogInfo($"[UPDATE] The game starts again in a few seconds: {Start.FileName} {Start.Arguments}");
                return true;
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[UPDATE] Can't start the game again: {e.Message}");
                return false;
            }
        }
    }
}

/* EOF */
