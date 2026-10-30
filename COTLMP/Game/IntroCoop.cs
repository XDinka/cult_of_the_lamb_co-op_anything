/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     A new game together: the opening alone until the crown, then together
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP.Debug;
using COTLMP.Network;
using COTLMP.Ui;
using HarmonyLib;
using Lamb.UI;
using Lamb.UI.MainMenu;
using MMTools;
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// A new game from the lobby. Each player plays the opening in their own game (the Lamb led in
    /// chains to the altar, the death, the One Who Waits) until the crown. Whoever gets it first waits
    /// for the other; then both come back to life at the same moment in the same place, the friend as
    /// the Goat, and the rest of the opening is a shared run. On its way out to the cult the friend's
    /// game takes the host's save, as for any join.
    /// </summary>
    /// <remarks>
    /// The friend's opening runs on a slot no menu shows, with saving off: nothing of it is written.
    /// </remarks>
    [HarmonyPatch]
    internal static class IntroCoop
    {
        /// <summary>
        /// This game's opening is shared with a friend
        /// </summary>
        internal static bool Shared { get; private set; }

        /// <summary>
        /// The friend: our own opening, before the host's save (nothing is saved)
        /// </summary>
        internal static bool OwnIntro { get; private set; }

        /* The slot of the friend's opening: no menu shows it */
        private const int IntroSlot = 7;

        private static bool CrownedHere, CrownedThere;
        private static int HostRun;
        private static IntroDeathSceneManager Held;
        private static bool Passing;

        /* The test harness: get the crown this many seconds into the opening */
        internal static float SkipTo = -1f;

        /* The test harness: leave the opening for the cult this many seconds after coming back to life */
        internal static float LeaveAfter = -1f;

        /* Counters for the test harness report */
        internal static int Crowns, Waited, Together;

        /// <summary>
        /// The opening before both have the crown: each player's own
        /// </summary>
        internal static bool Apart => Shared && !(CrownedHere && CrownedThere) && SceneManager.GetActiveScene().name == "Game Biome Intro";

        internal static string State => $"{(Shared ? 1 : 0)}{(OwnIntro ? 1 : 0)}{(CrownedHere ? 1 : 0)}{(CrownedThere ? 1 : 0)}/{Crowns}/{Waited}/{Together}";

        /// <summary>
        /// The host pressed Play on a new game (shared when a friend stands in the lobby)
        /// </summary>
        internal static void Begin(bool WithFriend)
        {
            Shared = WithFriend;
            CrownedHere = CrownedThere = false;
            Held = null;
        }

        /// <summary>
        /// The host: the friend was refused for their version: the opening is ours alone from here (held at the
        /// crown, it goes on)
        /// </summary>
        internal static void GoAlone(string Why)
        {
            if (!Shared || OwnIntro)
                return;
            Shared = false;
            Plugin.Logger?.LogInfo($"[INTRO] {Why}: the opening goes on alone");
            if (Held != null)
                Release();
        }

        /// <summary>
        /// The friend: the host pressed Play on a new game, our own opening starts as the game starts one
        /// </summary>
        internal static void StartAlone(int Difficulty, bool Permadeath)
        {
            Begin(true);
            OwnIntro = true;
            Plugin.Logger?.LogInfo($"[INTRO] The host starts a new game: our own opening until the crown (difficulty {Difficulty})");
            SaveAndLoad.SAVE_SLOT = IntroSlot;
            AccessTools.Method(typeof(LoadMenu), "BeginNewGame").Invoke(UIMainMenuController.Instance.LoadMenu, new object[]
            {
                new UINewGameOptionsMenuController.NewGameOptions { DifficultyIndex = Difficulty, PermadeathMode = Permadeath }
            });
        }

        /// <summary>
        /// The session ended: a friend in their own opening goes back to the main menu
        /// </summary>
        internal static bool Reset()
        {
            bool Was = OwnIntro;
            Shared = OwnIntro = CrownedHere = CrownedThere = false;
            if (Held != null)
                Release();
            return Was;
        }

        /* THE CROWN **********************************************************/

        /// <summary>
        /// After the crown's video the game comes back to the place of the execution: held there
        /// until both have the crown
        /// </summary>
        [HarmonyPatch(typeof(IntroDeathSceneManager), "ChangeRoom")]
        [HarmonyPrefix]
        private static bool ChangeRoom(IntroDeathSceneManager __instance)
        {
            if (Passing || !Shared)
                return true;
            if (!CrownedHere)
            {
                CrownedHere = true;
                ++Crowns;
                Plugin.Logger?.LogInfo($"[INTRO] We have the crown, the partner {(CrownedThere ? "too" : "not yet")}");
                SendCrown();
            }
            if (CrownedThere || !CoopSync.InSession || VersionCheck.KeptOut || SteamBridge.PartnerIds().Count == 0)
            {
                Together += CrownedThere ? 1 : 0;
                return true;
            }

            ++Waited;
            Held = __instance;
            OnlineMenu.Wait("Multiplayer/Intro/WaitCrown");
            if (LobbyShots.Dir != null)
                Plugin.MonoInstance.StartCoroutine(Shot("crown-wait", 1f));
            return false;
        }

        private static void SendCrown()
        {
            CoopSync.Send(CoopSync.Kind.IntroCrown, Writer => Writer.Write(CoopSync.CurrentRun));
        }

        internal static void OnCrown(BinaryReader Reader)
        {
            int Run = Reader.ReadInt32();
            if (!Shared)
                return;
            if (!Plugin.GlobalsInternal.IsServerCreator)
                HostRun = Run;
            if (CrownedThere)
                return;
            CrownedThere = true;
            Plugin.Logger?.LogInfo($"[INTRO] The partner has the crown{(Held != null ? ", going on together" : "")}");

            /* We had it first and waited */
            if (Held != null)
            {
                ++Together;
                Release();
            }
        }

        private static void Release()
        {
            IntroDeathSceneManager Scene = Held;
            Held = null;
            OnlineMenu.Wait(null);
            if (Scene == null)
                return;
            Passing = true;
            try
            {
                AccessTools.Method(typeof(IntroDeathSceneManager), "ChangeRoom").Invoke(Scene, null);
            }
            finally
            {
                Passing = false;
            }
        }

        /// <summary>
        /// Both come back to life: one run for both, the friend in the host's
        /// </summary>
        [HarmonyPatch(typeof(IntroManager), nameof(IntroManager.ToggleGameScene))]
        [HarmonyPrefix]
        private static void Reborn()
        {
            if (!Shared || Plugin.GlobalsInternal.IsServerCreator || HostRun == 0)
                return;
            CoopSync.TakeRun(HostRun, (int)FollowerLocation.IntroDungeon);
        }

        [HarmonyPatch(typeof(IntroManager), nameof(IntroManager.ToggleGameScene))]
        [HarmonyPostfix]
        private static void Back()
        {
            if (!Shared)
                return;
            if (!Plugin.GlobalsInternal.IsServerCreator)
                Plugin.MonoInstance.StartCoroutine(StepAside());
            if (LobbyShots.Dir != null)
                Plugin.MonoInstance.StartCoroutine(Shot("reborn", 14f));
        }

        /// <summary>
        /// The game brings the player back to life on one spot: the friend a step beside the host
        /// </summary>
        private static IEnumerator StepAside()
        {
            /* The game puts a new player in after a frame or two */
            yield return new WaitForSecondsRealtime(1f);
            if (PlayerFarming.Instance != null)
                PlayerFarming.Instance.transform.position += new Vector3(1.4f, 0f, 0f);
        }

        private static IEnumerator Shot(string Name, float After)
        {
            yield return new WaitForSecondsRealtime(After);
            yield return LobbyShots.Take(Name);
        }

        /// <summary>
        /// The friend leaves the opening's Lamb skin for the Goat when the crown changes it
        /// </summary>
        [HarmonyPatch(typeof(SimpleSpineAnimator), nameof(SimpleSpineAnimator.SetSkin))]
        [HarmonyPrefix]
        private static void GoatSkin(SimpleSpineAnimator __instance, ref string Skin)
        {
            if (Shared && Skin == "Lamb_0" && Appearance.WeAreGoat && PlayerFarming.Instance != null &&
                __instance.transform.IsChildOf(PlayerFarming.Instance.transform))
                Skin = "Goat";
        }

        /* OUT TO THE CULT ****************************************************/

        /// <summary>
        /// The friend's opening ends on the way to the cult: the host's save is taken there instead
        /// </summary>
        [HarmonyPatch(typeof(MMTransition), nameof(MMTransition.Play))]
        [HarmonyPrefix]
        private static bool ToCult(string SceneToLoad)
        {
            if (!OwnIntro || SceneToLoad != "Base Biome 1")
                return true;
            OwnIntro = false;
            Shared = false;
            if (!CoopSync.InSession)
            {
                Plugin.Logger?.LogWarning("[INTRO] The opening is over but the host is gone: back to the main menu");
                MMTransition.Play(MMTransition.TransitionType.ChangeSceneAutoResume, MMTransition.Effect.BlackFade, "Main Menu", 1f, "", null);
                return false;
            }
            Plugin.Logger?.LogInfo("[INTRO] The opening is over: into the host's cult, on its save");
            HostSave.ContinueJoin();
            return false;
        }

        /// <summary>
        /// Nothing of the friend's own opening is saved
        /// </summary>
        [HarmonyPatch(typeof(SaveAndLoad), nameof(SaveAndLoad.Save), new Type[0])]
        [HarmonyPrefix]
        private static bool Save() => !OwnIntro;

        [HarmonyPatch(typeof(SaveAndLoad), "Saving")]
        [HarmonyPrefix]
        private static bool Saving() => !OwnIntro;

        [HarmonyPatch(typeof(SaveAndLoad), nameof(SaveAndLoad.SaveMetaData))]
        [HarmonyPrefix]
        private static bool SaveMetaData() => !OwnIntro;

        /* THE TEST HARNESS ***************************************************/

        /// <summary>
        /// Straight to the crown a while into the opening: the death, the One Who Waits giving it
        /// </summary>
        internal static IEnumerator AutoCrown()
        {
            while (true)
            {
                while (SceneManager.GetActiveScene().name != "Game Biome Intro" || MMTransition.IsPlaying)
                    yield return null;
                yield return new WaitForSecondsRealtime(SkipTo);
                /* It moves a new save's story on: only a test save */
                if (!COTLMP.Debug.TestHarness.OnTestSave("-cotlmp-skipintro"))
                    yield break;
                IntroManager Intro = UnityEngine.Object.FindObjectOfType<IntroManager>();
                if (Intro != null && !CrownedHere)
                {
                    Plugin.Logger?.LogWarning("[QA] Skipping to the crown");
                    MMConversation.CURRENT_CONVERSATION = null;
                    Intro.ToggleDeathScene();
                    yield return new WaitForSecondsRealtime(3f);
                    IntroDeathSceneManager Death = UnityEngine.Object.FindObjectOfType<IntroDeathSceneManager>();
                    Death?.GiveCrown();
                }
                if (LeaveAfter >= 0f)
                {
                    while (SceneManager.GetActiveScene().name == "Game Biome Intro" && !(CrownedHere && (CrownedThere || !Shared)))
                        yield return null;
                    yield return new WaitForSecondsRealtime(LeaveAfter + 25f);
                    if (SceneManager.GetActiveScene().name == "Game Biome Intro")
                    {
                        Plugin.Logger?.LogWarning("[QA] Leaving the opening for the cult");
                        MMTransition.Play(MMTransition.TransitionType.ChangeRoomWaitToResume, MMTransition.Effect.BlackFade, "Base Biome 1", 1f, "", null);
                    }
                }
                while (SceneManager.GetActiveScene().name == "Game Biome Intro")
                    yield return null;
            }
        }
    }
}

/* EOF */
