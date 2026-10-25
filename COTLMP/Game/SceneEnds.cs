/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     A scene or a film both players watch ends for both together
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using HarmonyLib;
using COTLMP.Ui;
using MMTools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Video;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// The end of a scene both players watch: a leader waking, a room's own scene, a story talk,
    /// the films the game plays (the mountain's opening film).
    /// </summary>
    /// <remarks>
    /// Both games play such a scene, each its own copy, and its words go at the host's pace
    /// (SharedScenes). What comes after the last words (the camera going over the room, the leader
    /// getting up) each game played on its own clock, and a film each game plays on its own: whoever
    /// was done first got their hands back and went on (the leader fought them) while the other
    /// still watched. As the opening waits at the crown, whoever is done first now waits: their
    /// game stands still behind a black screen that says so, until the partner's scene is over
    /// too, at most a little while (the partner's game may have hung or gone). A film can be
    /// skipped (the game's own skip prompt is shown, the skip button skips): whoever skips waits
    /// the same way while the partner watches on, so a skip never cuts the partner's film short.
    /// The friend who waited goes on only when the host says its game is there again (its world
    /// loaded after the scene or film), as the host's game leads; if that takes a while the black
    /// screen says so.
    /// </remarks>
    [HarmonyPatch]
    internal static class SceneEnds
    {
        private enum Event : byte
        {
            SceneOn = 1,
            SceneOff = 2,
            Talk = 3,
            FilmOn = 4,
            FilmOff = 5,
            Ready = 6
        }

        /* Whoever is done first waits this long at most */
        private const float SceneAtMost = 45f;
        private const float FilmAtMost = 60f;
        /* Two scenes in one dungeon room are one scene if they started this close */
        private const float StartedTogether = 20f;

        private static readonly FieldInfo GameTimeScale = AccessTools.Field(typeof(GameManager), "TimeScale");
        private static readonly MethodInfo FilmEnd = AccessTools.Method(typeof(MMVideoPlayer), "EndReached");
        private static readonly FieldInfo FilmPlayer = AccessTools.Field(typeof(MMVideoPlayer), "videoPlayer");
        private static readonly FieldInfo FilmSkippable = AccessTools.Field(typeof(MMVideoPlayer), "Skippable");
        private static readonly FieldInfo FilmCompleted = AccessTools.Field(typeof(MMVideoPlayer), "completed");

        /* Ours: the scene on screen, where, since when, the conversations in it */
        private static bool OurScene;
        private static string OurPlace;
        private static float OurSince;
        private static readonly HashSet<int> OurTalks = new();

        /* The partner's */
        private static bool PartnerScene;
        private static string PartnerPlace;
        private static float PartnerSince;
        private static readonly HashSet<int> PartnerTalks = new();
        private static string PartnerFilm;
        private static float PartnerFilmSince;

        /* Our film */
        private static string OurFilm;

        /* After a shared scene the host says its world is up again; the friend who waited goes on only then */
        private const float SyncAtMost = 30f;
        private static bool IsHost => Plugin.GlobalsInternal.IsServerCreator;
        private static bool HostReady = true;
        private static bool ReadyDue, Syncing, SyncScene, SyncShown, Froze;
        private static float EndedAt, SyncSince;
        /* The test harness makes the host slow to say so */
        internal static float ReadyDelay;
        internal static int Synced;
        internal static float LastSyncFor = -1f;

        /* Waiting for the partner: our game stands still, or our film's end is held */
        private static bool Holding, HoldingFilm, Releasing;
        private static float HoldSince;
        private static string HoldPlace;

        /* Counters for the test harness report */
        internal static int Held, FilmsHeld, FilmsSkipped;

        /* We wait for the partner (the test harness stands still meanwhile) */
        internal static bool Waiting => Holding || HoldingFilm || Syncing;
        internal static float LastHeldFor = -1f;

        public static void Initialize()
        {
            GameObject Driver = new("COTLMP Scene Ends");
            UnityEngine.Object.DontDestroyOnLoad(Driver);
            Driver.AddComponent<Runner>();
        }

        private static void Send(Event Type, string Text, int Value = 0)
        {
            if (!CoopSync.InSession)
                return;
            CoopSync.Send(CoopSync.Kind.SceneState, Writer =>
            {
                Writer.Write((byte)Type);
                Writer.Write(Text ?? "");
                Writer.Write(Value);
            });
        }

        public static void OnRelay(BinaryReader Reader)
        {
            Event Type = (Event)Reader.ReadByte();
            string Text = Reader.ReadString();
            int Value = Reader.ReadInt32();
            switch (Type)
            {
                case Event.SceneOn:
                    if (!IsHost)
                        HostReady = false;
                    PartnerScene = true;
                    PartnerPlace = Text;
                    PartnerSince = Time.unscaledTime;
                    PartnerTalks.Clear();
                    break;

                case Event.SceneOff:
                    PartnerScene = false;
                    break;

                case Event.Talk:
                    PartnerTalks.Add(Value);
                    break;

                case Event.FilmOn:
                    if (!IsHost)
                        HostReady = false;
                    PartnerFilm = Text;
                    PartnerFilmSince = Time.unscaledTime;
                    break;

                case Event.FilmOff:
                    PartnerFilm = null;
                    break;

                case Event.Ready:
                    if (!IsHost)
                        HostReady = true;
                    break;
            }
        }

        /* Our scene was the partner's scene too: the same dungeon room at about the same time, or the same talk */
        private static bool SharedWithPartner()
        {
            if (!CoopSync.InSession || !PartnerScene || IntroCoop.Apart || CoopLife.PartnerFallen || OurPlace == null || PartnerPlace != OurPlace)
                return false;
            if (OurPlace.Contains("#") && Mathf.Abs(PartnerSince - OurSince) < StartedTogether)
                return true;
            return OurTalks.Overlaps(PartnerTalks);
        }

        /// <summary>
        /// A conversation started in our scene: the partner learns which, so the same talk in both games is one scene
        /// </summary>
        internal static void Talked(int Key)
        {
            if (!OurScene || Key == 0 || !OurTalks.Add(Key))
                return;
            Send(Event.Talk, OurPlace, Key);
        }

        /* Whoever waits sees a black screen that says so, as at the opening's crown */
        private static void StartWaiting()
        {
            HoldSince = Time.unscaledTime;
            try
            {
                OnlineMenu.Wait("Multiplayer/Scene/WaitScene");
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Showing the wait failed: {e.Message}");
            }
        }

        private static void HideWait()
        {
            try
            {
                OnlineMenu.Wait(null);
            }
            catch
            {
            }
        }

        private static float GameScale => GameTimeScale?.GetValue(null) is float Value && Value > 0f ? Value : 1f;

        /// <summary>
        /// We stop waiting for the partner's scene; the friend whose host is done too then waits for the host's world
        /// </summary>
        private static void Release(string Why, bool Together = false)
        {
            bool Sync = Together && !IsHost && CoopSync.InSession && (Holding || HoldingFilm);
            if ((Holding || HoldingFilm) && !Sync)
                HideWait();
            bool WasScene = Holding;
            if (Holding)
            {
                Holding = false;
                LastHeldFor = Time.unscaledTime - HoldSince;
                if (!Sync)
                    Time.timeScale = GameScale;
                Plugin.Logger?.LogInfo($"[COOP] Waited {LastHeldFor:F1} s for the partner's scene: {Why}");
            }
            if (HoldingFilm)
                EndFilm(Why);
            if (Sync)
            {
                Syncing = true;
                SyncScene = WasScene;
                Froze = WasScene;
                SyncShown = false;
                SyncSince = EndedAt = Time.unscaledTime;
                Plugin.Logger?.LogInfo("[COOP] Waiting for the host's game to be there again");
            }
        }

        private static void EndSync(string Why)
        {
            if (!Syncing)
                return;
            Syncing = false;
            LastSyncFor = Time.unscaledTime - SyncSince;
            ++Synced;
            HideWait();
            if (Froze)
                Time.timeScale = GameScale;
            Froze = false;
            Plugin.Logger?.LogInfo($"[COOP] Waited {LastSyncFor:F1} s for the host's game: {Why}");
        }

        /* The host's scene or film is over: once its world is up again it tells the friend */
        private static void MarkEnded()
        {
            if (!IsHost || !CoopSync.InSession)
                return;
            ReadyDue = true;
            EndedAt = Time.unscaledTime;
        }

        /* Our world is up: no scene change going on, the lamb is there, no film */
        private static bool WorldReady =>
            Time.unscaledTime - EndedAt > 0.5f && !MMTransition.IsPlaying && PlayerFarming.Instance != null && OurFilm == null;

        private static void Sync()
        {
            if (IsHost)
            {
                if (ReadyDue && !Holding && !HoldingFilm && WorldReady && Time.unscaledTime - EndedAt > ReadyDelay)
                {
                    ReadyDue = false;
                    Send(Event.Ready, CoopSync.CurrentPlace());
                }
                return;
            }
            if (!Syncing)
                return;
            float For = Time.unscaledTime - SyncSince;
            if (HostReady)
                EndSync("the host's world is up, going on together");
            else if (!CoopSync.InSession || CoopLife.PartnerFallen)
                EndSync("going on alone");
            else if (For > SyncAtMost)
                EndSync("waited long enough, going on");
            if (!Syncing)
                return;

            /* Our game stands still once it is up (a film's end may load another place first) */
            bool Loading = MMTransition.IsPlaying || PlayerFarming.Instance == null || OurFilm != null;
            if (!Loading && (SyncScene || WorldReady))
            {
                Time.timeScale = 0f;
                Froze = true;
            }
            else if (Loading && Froze)
            {
                Time.timeScale = GameScale;
                Froze = false;
            }
            if (!SyncShown && For > 1f)
            {
                SyncShown = true;
                try
                {
                    OnlineMenu.Wait("Multiplayer/Scene/WaitSync");
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogWarning($"[COOP] Showing the wait failed: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Our film ends now, as when it plays to its end
        /// </summary>
        private static void EndFilm(string Why)
        {
            if (HoldingFilm)
            {
                LastHeldFor = Time.unscaledTime - HoldSince;
                Plugin.Logger?.LogInfo($"[COOP] Waited {LastHeldFor:F1} s for the partner's film: {Why}");
            }
            else
            {
                Plugin.Logger?.LogInfo($"[COOP] Our film {OurFilm} ends: {Why}");
            }
            HoldingFilm = false;
            Releasing = true;
            try
            {
                /* The game marks a film done before it ends it: the end we held must still run */
                if (MMVideoPlayer.mmVideoPlayer != null)
                    FilmCompleted?.SetValue(MMVideoPlayer.mmVideoPlayer, false);
                if (FilmPlayer?.GetValue(null) is VideoPlayer Player)
                    FilmEnd?.Invoke(null, new object[] { Player });
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Ending our film failed: {e.Message}");
            }
            finally
            {
                Releasing = false;
            }
        }

        /* The partner watches our film now */
        private static bool PartnerWatches =>
            CoopSync.InSession && !IntroCoop.Apart && !CoopLife.PartnerFallen && OurFilm != null && PartnerFilm == OurFilm &&
            Time.unscaledTime - PartnerFilmSince < FilmAtMost * 3f;

        /// <summary>
        /// We press skip on our film: alone it ends at once; while the partner watches it, we wait for theirs to end
        /// </summary>
        internal static void Skip()
        {
            if (OurFilm == null || HoldingFilm)
                return;
            ++FilmsSkipped;
            if (!PartnerWatches)
            {
                EndFilm("skipped");
                return;
            }
            Plugin.Logger?.LogInfo($"[COOP] We skip our film {OurFilm}, the partner watches on: waiting for them");
            HoldFilm();
        }

        /* Our film is over for us (played or skipped), the partner's isn't: it stops, we wait */
        private static void HoldFilm()
        {
            HoldingFilm = true;
            ++FilmsHeld;
            if (FilmPlayer?.GetValue(null) is VideoPlayer Player && Player.isPlaying)
                Player.Pause();
            Send(Event.FilmOff, OurFilm);
            StartWaiting();
        }

        /* Our film has a skip in a game, not on the title screen */
        private static bool CanSkip => OurFilm != null && PlayerFarming.Instance != null && !MMTransition.IsPlaying;

        private static void Watch()
        {
            if (!Holding && !HoldingFilm)
                return;
            float AtMost = Holding ? SceneAtMost : FilmAtMost;
            if (!CoopSync.InSession)
                Release("we are alone");
            else if (CoopLife.PartnerFallen)
                Release("the partner has fallen");
            else if (Time.unscaledTime - HoldSince > AtMost)
                Release("waited long enough, going on");
            else if (Holding && (!PartnerScene || PartnerPlace != HoldPlace))
                Release("theirs is over too, going on together", true);
            else if (HoldingFilm && PartnerFilm == null)
                Release("theirs is over too, going on together", true);
            else if (Holding && CoopSync.CurrentPlace() != HoldPlace)
                Release("we are somewhere else");
            else if (Holding)
                Time.timeScale = 0f;
        }

        /* PATCHES ************************************************************/

        [HarmonyPatch(typeof(LetterBox), nameof(LetterBox.Show))]
        [HarmonyPrefix]
        private static void SceneShows()
        {
            try
            {
                if (LetterBox.IsPlaying)
                    return;
                if (Holding)
                    Release("a new scene starts");
                EndSync("a new scene starts");
                OurScene = true;
                OurPlace = CoopSync.CurrentPlace();
                OurSince = Time.unscaledTime;
                OurTalks.Clear();
                Send(Event.SceneOn, OurPlace);
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Watching a scene start failed: {e.Message}");
            }
        }

        [HarmonyPatch(typeof(LetterBox), nameof(LetterBox.Hide))]
        [HarmonyPostfix]
        private static void SceneHidden()
        {
            try
            {
                if (!OurScene)
                    return;
                OurScene = false;
                Send(Event.SceneOff, OurPlace);
                MarkEnded();
                if (!SharedWithPartner() || PlayerFarming.Instance == null)
                    return;
                Holding = true;
                HoldPlace = OurPlace;
                ++Held;
                Time.timeScale = 0f;
                StartWaiting();
                Plugin.Logger?.LogInfo($"[COOP] Our scene in {OurPlace} is over, the partner's isn't: waiting for them");
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Watching a scene end failed: {e.Message}");
            }
        }

        [HarmonyPatch(typeof(MMVideoPlayer), nameof(MMVideoPlayer.Play))]
        [HarmonyPrefix]
        private static void FilmStarts(string _FileName)
        {
            if (!IntroCoop.Apart)
                Send(Event.FilmOn, _FileName);
            if (HoldingFilm)
                Release("a new film starts");
            OurFilm = _FileName;
        }

        [HarmonyPatch(typeof(MMVideoPlayer), nameof(MMVideoPlayer.Play))]
        [HarmonyPostfix]
        private static void FilmStarted()
        {
            try
            {
                if (CanSkip && MMVideoPlayer.mmVideoPlayer?.skipPrompt != null)
                    MMVideoPlayer.mmVideoPlayer.skipPrompt.SetActive(true);
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Showing the film's skip failed: {e.Message}");
            }
        }

        [HarmonyPatch(typeof(MMVideoPlayer), "EndReached")]
        [HarmonyPrefix]
        private static bool FilmEnds()
        {
            try
            {
                if (Releasing || OurFilm == null)
                    return Finish();
                if (HoldingFilm)
                    return false;
                /* The game's own skip on a film that can be skipped: as our skip */
                if (FilmPlayer?.GetValue(null) is VideoPlayer Playing && Playing.isPlaying && PartnerWatches)
                {
                    Skip();
                    return false;
                }
                if (PartnerWatches)
                {
                    Plugin.Logger?.LogInfo($"[COOP] Our film {OurFilm} is over, the partner's isn't: waiting for them");
                    HoldFilm();
                    return false;
                }
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Watching a film end failed: {e.Message}");
            }
            return Finish();
        }

        private static bool Finish()
        {
            if (OurFilm != null)
                MarkEnded();
            if (OurFilm != null && !HoldingFilm)
                Send(Event.FilmOff, OurFilm);
            OurFilm = null;
            return true;
        }

        [HarmonyPatch(typeof(MMVideoPlayer), nameof(MMVideoPlayer.ForceStopVideo))]
        [HarmonyPrefix]
        private static void FilmStopped()
        {
            if (HoldingFilm)
            {
                HoldingFilm = false;
                OnlineMenu.Wait(null);
            }
            if (OurFilm != null)
                Send(Event.FilmOff, OurFilm);
            OurFilm = null;
        }

        /// <summary>
        /// Keeps our game still while we wait, skips films
        /// </summary>
        private sealed class Runner : MonoBehaviour
        {
            private void Update()
            {
                try
                {
                    /* The game skips only films made skippable; the others we skip */
                    if (CanSkip && !HoldingFilm && FilmSkippable?.GetValue(MMVideoPlayer.mmVideoPlayer) is MMVideoPlayer.Options Options &&
                        Options == MMVideoPlayer.Options.DISABLE && InputManager.UI.GetCancelButtonDown())
                        Skip();
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogWarning($"[COOP] Skipping our film failed: {e.Message}");
                }
            }

            private void LateUpdate()
            {
                try
                {
                    Watch();
                    Sync();
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogWarning($"[COOP] Waiting for the partner's scene failed: {e.Message}");
                    Holding = HoldingFilm = Syncing = false;
                    Time.timeScale = 1f;
                    OnlineMenu.Wait(null);
                }
            }
        }
    }
}

/* EOF */
