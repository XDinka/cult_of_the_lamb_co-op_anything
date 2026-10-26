/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     The lobby before a shared game, opened by the host from a save slot
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP.Debug;
using COTLMP.Network;
using COTLMP.Ui;
using HarmonyLib;
using I2.Loc;
using MMTools;
using Lamb.UI;
using Lamb.UI.MainMenu;
using src.UINavigator;
using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// The lobby: the host picks a save (or an empty slot for a new game) in the main menu and
    /// friends join there before anything is loaded. The host's Play starts both: on a save, the
    /// host loads it and the friend takes it as before; on a new game, each plays the opening alone
    /// until the crown (see <see cref="IntroCoop"/>).
    /// </summary>
    /// <remarks>
    /// The host tells every joining player where it is (in the lobby, starting a save, starting a new
    /// game, or playing already). A player who joins from the main menu while the host waits in the
    /// lobby stands in it too; anyone else takes the host's save once the host's game is up.
    /// </remarks>
    [HarmonyPatch]
    internal static class Lobby
    {
        internal enum Phase : byte
        {
            /* In a game: a joining player takes the save */
            Playing = 0,
            /* In the lobby */
            Waiting = 1,
            /* Play pressed on a save */
            Continue = 2,
            /* Play pressed on a new game: the opening comes first */
            NewGame = 3
        }

        /* The host: the lobby's save and what Play does with it */
        internal static bool Open { get; private set; }
        private static int Slot;
        private static bool NewSave;
        private static UINewGameOptionsMenuController.NewGameOptions Options;
        private static Phase Ours = Phase.Playing;

        /// <summary>
        /// The lobby's save as the game's slot list shows it
        /// </summary>
        internal static string SaveLine { get; private set; }

        /* The joining player: what the host said */
        internal static Phase? Heard { get; private set; }
        internal static bool Waiting { get; private set; }

        /// <summary>
        /// The game's save slots are open to pick the lobby's save
        /// </summary>
        internal static bool PickingSlot;

        /* The test harness: the slot to open the lobby with ("new" for a new game) and when to press Play */
        internal static string AutoLobby;
        internal static float AutoPlay = 6f;

        /* Counters for the test harness report */
        internal static int Opened, Started, Stood;

        private static bool InMenu => SceneManager.GetActiveScene().name == "Main Menu";

        /* HOST ***************************************************************/

        /// <summary>
        /// Opens the lobby on this slot: online for friends, nothing loaded yet
        /// </summary>
        internal static void HostFrom(int SaveSlot, bool New, UINewGameOptionsMenuController.NewGameOptions NewOptions)
        {
            PickingSlot = false;
            Slot = SaveSlot;
            NewSave = New;
            Options = NewOptions;
            SaveLine = New ? LocalizationManager.GetTranslation("Multiplayer/Lobby/NewGame") : SlotLine(SaveSlot);

            SteamBridge.InviteOnOpen = false;
            bool Started = PauseMenuPatches.StartHosting();
            SteamBridge.InviteOnOpen = true;
            if (!Started)
            {
                Plugin.Logger?.LogError("[LOBBY] The lobby didn't open");
                OnlineMenu.Tell("Multiplayer/Lobby/Failed");
                OnlineMenu.ShowOnline();
                return;
            }

            Open = true;
            Ours = Phase.Waiting;
            ++Opened;
            Plugin.Logger?.LogInfo($"[LOBBY] Lobby open on {(New ? $"a new game in slot {SaveSlot} (difficulty {NewOptions.DifficultyIndex})" : $"slot {SaveSlot}")}");
            Send();
            OnlineMenu.ShowLobby();
        }

        /// <summary>
        /// Play: the game starts as the game's own menu would start it, and the friends follow
        /// </summary>
        internal static void Play()
        {
            if (!Open)
                return;
            Open = false;
            ++Started;
            Ours = NewSave && !Options.QuickStart ? Phase.NewGame : Phase.Continue;
            Plugin.Logger?.LogInfo($"[LOBBY] Play: {Ours} with {SteamBridge.PartnerIds().Count} friend(s)");
            Send();
            IntroCoop.Begin(Ours == Phase.NewGame && SteamBridge.PartnerIds().Count > 0);
            OnlineMenu.Leave();

            LoadMenu Menu = UIMainMenuController.Instance.LoadMenu;
            if (NewSave)
            {
                SaveAndLoad.SAVE_SLOT = Slot;
                AccessTools.Method(typeof(LoadMenu), "BeginNewGame").Invoke(Menu, new object[] { Options });
            }
            else
            {
                AccessTools.Method(typeof(LoadMenu), "ContinueGame").Invoke(Menu, new object[] { Slot });
            }
        }

        /// <summary>
        /// The host leaves the lobby: the friends are let go
        /// </summary>
        internal static void Close()
        {
            Plugin.Logger?.LogInfo("[LOBBY] Lobby closed");
            Open = false;
            Ours = Phase.Playing;
            PauseMenuPatches.EndSession(null);
        }

        /// <summary>
        /// Where the host is, to the joining players: on every join, and when it changes
        /// </summary>
        internal static void Send()
        {
            if (!Plugin.GlobalsInternal.IsServerCreator || !CoopSync.InSession)
                return;
            CoopSync.Send(CoopSync.Kind.Lobby, Writer =>
            {
                Writer.Write((byte)Ours);
                Writer.Write(SaveLine ?? "");
                Writer.Write(Options.DifficultyIndex);
                Writer.Write(Options.PermadeathMode);
            });
        }

        private static string SlotLine(int SaveSlot)
        {
            try
            {
                LoadMenu Menu = UIMainMenuController.Instance.LoadMenu;
                if (SaveSlot >= 0 && SaveSlot < Menu.SaveSlots.Length)
                    return Menu.SaveSlots[SaveSlot].GetComponentInChildren<TMP_Text>(true).text;
            }
            catch (Exception e)
            {
                COTLMP.Debug.Faults.Note("The save slot's name", e);
            }
            return $"#{SaveSlot + 1}";
        }

        /* JOINING PLAYER *****************************************************/

        internal static void OnLobby(BinaryReader Reader)
        {
            Phase Said = (Phase)Reader.ReadByte();
            string Line = Reader.ReadString();
            int Difficulty = Reader.ReadInt32();
            bool Permadeath = Reader.ReadBoolean();
            if (Plugin.GlobalsInternal.IsServerCreator)
                return;

            if (Heard != Said)
                Plugin.Logger?.LogInfo($"[LOBBY] The host is {Said} ({Line})");
            Heard = Said;
            SaveLine = Line;

            /* Only a player who stood in the lobby follows the host's Play; anyone else takes the save */
            if (!Waiting)
                return;
            if (Said == Phase.Waiting)
            {
                OnlineMenu.Refresh();
                return;
            }

            Waiting = false;
            OnlineMenu.Leave();
            if (Said == Phase.NewGame)
                IntroCoop.StartAlone(Difficulty, Permadeath);
            else
                HostSave.ContinueJoin();
        }

        /// <summary>
        /// The joining player, after the version check: true while waiting in the host's lobby instead
        /// of taking the host's save now
        /// </summary>
        internal static IEnumerator Decide(Action<bool> Hold)
        {
            float Until = Time.realtimeSinceStartup + 3f;
            while (Heard == null && Time.realtimeSinceStartup < Until && CoopSync.InSession)
                yield return null;

            if (Heard == Phase.Waiting && InMenu && CoopSync.InSession)
            {
                Waiting = true;
                ++Stood;
                Plugin.Logger?.LogInfo("[LOBBY] Standing in the host's lobby");
                OnlineMenu.ShowLobby();
                Hold(true);
                yield break;
            }
            Hold(false);
        }

        /// <summary>
        /// The session ended (left, closed or lost)
        /// </summary>
        internal static void Reset()
        {
            bool Shown = Open || Waiting;
            Open = Waiting = PickingSlot = false;
            Heard = null;
            Ours = Phase.Playing;
            if (Shown && InMenu)
                OnlineMenu.ShowOnline();
        }

        /* THE GAME'S SAVE SLOTS **********************************************/

        /// <summary>
        /// A save picked for the lobby opens it instead of the game
        /// </summary>
        [HarmonyPatch(typeof(LoadMenu), "ContinueGame")]
        [HarmonyPrefix]
        private static bool ContinueGame(LoadMenu __instance, int saveSlot)
        {
            if (!PickingSlot || __instance != UIMainMenuController.Instance?.LoadMenu)
                return true;
            MonoSingleton<UINavigatorNew>.Instance.LockInput = MonoSingleton<UINavigatorNew>.Instance.LockNavigation = false;
            HostFrom(saveSlot, false, default);
            return false;
        }

        /// <summary>
        /// An empty slot picked for the lobby: the game asked for the new game's options, the lobby opens with them
        /// </summary>
        [HarmonyPatch(typeof(LoadMenu), "BeginNewGame")]
        [HarmonyPrefix]
        private static bool BeginNewGame(LoadMenu __instance, UINewGameOptionsMenuController.NewGameOptions newGameOptions)
        {
            if (!PickingSlot || __instance != UIMainMenuController.Instance?.LoadMenu)
                return true;
            HostFrom(SaveAndLoad.SAVE_SLOT, true, newGameOptions);
            return false;
        }

        [HarmonyPatch(typeof(LoadMenu), "OnBackButtonClicked")]
        [HarmonyPrefix]
        private static bool SlotsBack(LoadMenu __instance)
        {
            if (!PickingSlot || __instance != UIMainMenuController.Instance?.LoadMenu)
                return true;
            PickingSlot = false;
            OnlineMenu.ShowOnline();
            return false;
        }

        [HarmonyPatch(typeof(LoadMenu), nameof(LoadMenu.OnCancelButtonInput))]
        [HarmonyPrefix]
        private static bool SlotsCancel(LoadMenu __instance)
        {
            if (!PickingSlot || __instance != UIMainMenuController.Instance?.LoadMenu)
                return true;
            if (__instance.CanvasGroup != null && __instance.CanvasGroup.interactable)
            {
                PickingSlot = false;
                OnlineMenu.ShowOnline();
            }
            return false;
        }

        /* THE TEST HARNESS ***************************************************/

        /// <summary>
        /// The host opens the lobby by itself, and presses Play a while after a friend stands in it
        /// </summary>
        internal static IEnumerator AutoHost()
        {
            while (!InMenu || MMTransition.IsPlaying || !OnlineMenu.Shown)
                yield return null;
            yield return new WaitForSecondsRealtime(4f);

            OnlineMenu.ShowOnline();
            yield return new WaitForSecondsRealtime(3f);
            yield return LobbyShots.Take("online");
            if (AutoLobby == "new")
            {
                HostFrom(6, true, new UINewGameOptionsMenuController.NewGameOptions
                {
                    DifficultyIndex = DifficultyManager.AllAvailableDifficulties().IndexOf(DifficultyManager.Difficulty.Medium)
                });
            }
            else
            {
                HostFrom(int.TryParse(AutoLobby, out int Number) ? Number : 6, false, default);
            }
            Plugin.Logger?.LogWarning($"[QA host] Lobby open: {Open}");
            yield return new WaitForSecondsRealtime(3f);
            yield return LobbyShots.Take("lobby-alone");

            while (Open && SteamBridge.PartnerIds().Count == 0)
                yield return null;
            yield return new WaitForSecondsRealtime(3f);
            yield return LobbyShots.Take("lobby");
            yield return new WaitForSecondsRealtime(Math.Max(0f, AutoPlay - 3f));
            if (Open)
                Play();
        }
    }
}

/* EOF */
