/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     The test harness: conversations, answers, scenes and films played together
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
    internal static partial class TestHarness
    {
        /// <summary>
        /// The text chat both ways: the host types (the game's input off while typing and back after), the friend answers
        /// </summary>
        private static IEnumerator ChatTogether()
        {
            yield return Until(() => CoopSync.InSession && PlayerFarming.Instance != null && !MMTransition.IsPlaying && AnyRemotePlayer(), 300f);
            yield return new WaitForSecondsRealtime(8f);
            if (Plugin.GlobalsInternal.IsServerCreator)
            {
                bool Same = false;
                yield return COTLMP.Ui.TextChat.TestType("Привет! 你好 こんにちは — chat test", Result => Same = Same || Result);
                Plugin.Logger?.LogWarning($"{Tag} Chat test: typed and sent, input back as before: {Same}");
            }
            else
            {
                yield return Until(() => COTLMP.Ui.TextChat.Received > 0, 60f);
                yield return new WaitForSecondsRealtime(1f);
                COTLMP.Ui.TextChat.Say($"Got it ({COTLMP.Ui.TextChat.Received}) — مرحبا 안녕");
                /* A long Arabic line over several rows: they must read top to bottom, right-aligned */
                COTLMP.Ui.TextChat.Say("مرحبا يا صديقي، لنذهب معًا إلى الغرفة التالية الآن ونقاتل الزعيم الكبير قبل أن يحل الليل");
                Plugin.Logger?.LogWarning($"{Tag} Chat test: answered");
            }
            yield return new WaitForSecondsRealtime(3f);
            COTLMP.Debug.HudProbe.Take("chat-lines");
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

        private static IEnumerator TalkOnce()
        {
            while (SceneManager.GetActiveScene().name != DungeonScene || PlayerFarming.Instance == null || !AnyRemotePlayer())
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

        /// <summary>
        /// Both copies play the same conversation in one dungeon room, the friend's a little later:
        /// the host reads on every 2 s and answers the second answer, the friend tries to read on
        /// every 0.3 s and to answer the first
        /// </summary>
        private static IEnumerator SceneTogether()
        {
            while (SceneManager.GetActiveScene().name != DungeonScene || PlayerFarming.Instance == null || !AnyRemotePlayer())
                yield return null;
            bool Host = Plugin.GlobalsInternal.IsServerCreator;
            yield return new WaitForSecondsRealtime(Host ? 6f : 7f);

            /* =hold: the host goes into the next room alone and its talk starts there (it waits for the friend),
               the friend comes in 14 s later and its copy starts once in */
            bool Hold = SceneBreak == "hold" || SceneBreak == "holdearly";
            /* =holdearly: the friend's copy starts at once, as the room still comes in (a room's own talk for both) */
            bool Early = SceneBreak == "holdearly";
            if (Hold)
            {
                SceneBreak = null;
                MMBiomeGeneration.BiomeRoom Here = MMBiomeGeneration.BiomeGenerator.Instance?.CurrentRoom;
                MMBiomeGeneration.BiomeRoom Into = Here == null ? null : MMBiomeGeneration.BiomeGenerator.Instance.Rooms?.Find(Room => Room != Here && Math.Abs(Room.x - Here.x) + Math.Abs(Room.y - Here.y) == 1);
                if (Into == null)
                {
                    Plugin.Logger?.LogError($"[QA] Scene test hold: no room next to ours");
                    ++Failed;
                    yield break;
                }
                if (!Host)
                    yield return new WaitForSecondsRealtime(14f);
                Plugin.Logger?.LogWarning($"{Tag} Scene test hold: we go into the room at {Into.x},{Into.y}");
                MMTransition.Play(MMTransition.TransitionType.ChangeRoom, MMTransition.Effect.BlackFade, MMTransition.NO_SCENE, 0.5f, "", () => MMBiomeGeneration.BiomeGenerator.ChangeRoom(Into.x, Into.y));
                if (Early && !Host)
                    yield return Until(() => PlayerFarming.Instance != null && (COTLMP.Game.CoopSync.CurrentPlace() ?? "").EndsWith($"@{Into.x},{Into.y}"), 20f);
                else
                {
                    yield return new WaitForSecondsRealtime(2f);
                    yield return Until(() => !MMTransition.IsPlaying && PlayerFarming.Instance != null, 20f);
                }
                /* The friend talks once in, with the host seen there (the way a room's talk starts once the room is on) */
                if (!Host && !Early)
                    yield return Until(() => COTLMP.Game.RoomSync.PartnerPlace == COTLMP.Game.CoopSync.CurrentPlace() && COTLMP.Game.GhostLoot.PartnerHere() != null && Time.timeScale > 0f, 15f);
                if (Host || !Early)
                    yield return new WaitForSecondsRealtime(Host ? 3f : 1f);
                Plugin.Logger?.LogWarning($"{Tag} Scene test hold: in {COTLMP.Game.CoopSync.CurrentPlace()}, the partner in {COTLMP.Game.RoomSync.PartnerPlace}, its puppet {(COTLMP.Game.GhostLoot.PartnerHere() != null ? "shown" : "not yet")}, time scale {Time.timeScale}");
            }

            GameObject Me = PlayerFarming.Instance.gameObject;
            var Entries = new List<MMTools.ConversationEntry>
            {
                new(Me, "Scene test line one", "talk", "", null, "Lamb"),
                new(Me, "Scene test line two", "talk", "", null, "Lamb"),
                new(Me, "Scene test line three", "talk", "", null, "Lamb"),
                new(Me, "Scene test line four", "talk", "", null, "Lamb")
            };
            int Picked = -1;
            GameObject Talker = null;
            var Responses = new List<MMTools.Response>
            {
                new("Scene test answer one", () => Picked = 0, "Scene test answer one"),
                new("Scene test answer two", () => Picked = 1, "Scene test answer two")
            };
            float Started = Time.unscaledTime;
            Plugin.Logger?.LogWarning($"{Tag} Scene test: starting");
            /* =scripted: a scene's own talk (as a chest's or a leader's intro): the game's end-of-talk
               handling is off, the scene lets the player go from its callback */
            if (SceneBreak == "scripted")
                MMTools.MMConversation.Play(new MMTools.ConversationObject(Entries, null, () => GameManager.GetInstance().OnConversationEnd()), false);
            /* =nearest: the game's own talk object, played with no player named, as Ratau's or a mini-boss's
               intro is: it goes to the player nearest to it, here the partner's puppet */
            else if (SceneBreak == "nearest")
            {
                SceneBreak = null;
                GameObject Puppet = COTLMP.Game.GhostLoot.PartnerHere();
                Talker = new GameObject("COTLMP test talker");
                Talker.transform.position = (Puppet != null ? Puppet.transform.position : Me.transform.position) + new Vector3(0.3f, 0f, 0f);
                var Talk = Talker.AddComponent<Interaction_SimpleConversation>();
                Talk.Entries = Entries;
                Talk.Responses = Responses;
                Talk.MovePlayerToListenPosition = false;
                Plugin.Logger?.LogWarning($"{Tag} Scene test nearest: the talk stands at the partner's puppet ({(Puppet != null ? "here" : "NOT here")}), nearest player {PlayerFarming.FindClosestPlayerGameObject(Talker.transform.position)?.name}");
                Talk.Play(null);
                var Owner = MonoSingleton<src.UINavigator.UINavigatorNew>.Instance.AllowInputOnlyFromPlayer;
                Plugin.Logger?.LogWarning($"{Tag} Scene test nearest: the talk's input is {(Owner == null ? "nobody's" : Owner == PlayerFarming.Instance ? "our lamb's" : COTLMP.Game.PlayerManager.IsPuppet(Owner.health) ? "the PUPPET's" : Owner.name)}, talk {MMTools.MMConversation.isPlaying}");
            }
            /* =plain: no answers, the talk's own callback (what a room, a fight or the next talk hangs on) */
            else if (SceneBreak == "plain")
            {
                SceneBreak = null;
                MMTools.MMConversation.Play(new MMTools.ConversationObject(Entries, null, () =>
                {
                    Picked = 9;
                    Plugin.Logger?.LogWarning($"{Tag} Scene test: the talk's callback ran");
                }));
            }
            else
                MMTools.MMConversation.Play(new MMTools.ConversationObject(Entries, Responses, null));
            var Next = AccessTools.Method(typeof(MMTools.MMConversation), "HandleNextClicked");
            var Answer = AccessTools.Method(typeof(MMTools.MMConversation), "OnGiveAnswer");
            int Last = -2;
            bool Released = false;
            bool OwnerTold = false;
            float Moved = Time.unscaledTime;
            bool QuietDone = false;
            while (MMTools.MMConversation.isPlaying && Time.unscaledTime - Started < (Hold ? 60f : 40f))
            {
                yield return new WaitForSecondsRealtime(Host ? 2f : 0.3f);
                var Box = MMTools.MMConversation.mmConversation;
                if (Box == null)
                    continue;
                if (MMTools.MMConversation.Position != Last)
                {
                    Last = MMTools.MMConversation.Position;
                    Moved = Time.unscaledTime;
                    Plugin.Logger?.LogWarning($"{Tag} Scene test: at line {Last} after {Time.unscaledTime - Started:F1} s, letterbox {LetterBox.IsPlaying}, state {PlayerFarming.Instance?.state?.CURRENT_STATE}");
                }
                if (Talker != null && !OwnerTold && MMTools.MMConversation.Position >= 0)
                {
                    OwnerTold = true;
                    var Owner = MonoSingleton<src.UINavigator.UINavigatorNew>.Instance.AllowInputOnlyFromPlayer;
                    Plugin.Logger?.LogWarning($"{Tag} Scene test nearest: at line 0 the input is {(Owner == null ? "nobody's" : Owner == PlayerFarming.Instance ? "our lamb's" : COTLMP.Game.PlayerManager.IsPuppet(Owner.health) ? "the PUPPET's" : Owner.name)}");
                    Shot("nearest-talk");
                }
                if (Hold && Host && !Released && COTLMP.Game.SharedScenes.TalksReleased > 0)
                {
                    Released = true;
                    yield return new WaitForSecondsRealtime(1f);
                    Plugin.Logger?.LogWarning($"{Tag} Scene test hold: released, letterbox {LetterBox.IsPlaying}, state {PlayerFarming.Instance?.state?.CURRENT_STATE}, line {MMTools.MMConversation.Position}");
                    Shot("hold-released");
                }
                if (Hold && !Host && !Released && MMTools.MMConversation.Position >= 0)
                {
                    Released = true;
                    yield return new WaitForSecondsRealtime(1.5f);
                    Shot("hold-released");
                }
                /* -cotlmp-scenequiet=N: from its second line the host's steps reach the friend N s late; the friend's frame once it stands */
                if (SceneQuiet > 0f && !QuietDone && (Host ? Last >= 1 : Time.unscaledTime - Moved > 7f))
                {
                    QuietDone = true;
                    if (Host)
                        COTLMP.Game.SharedScenes.DelayStepsFor(SceneQuiet);
                    else
                    {
                        Plugin.Logger?.LogWarning($"{Tag} Scene test: our copy stands at line {Last} for {Time.unscaledTime - Moved:F1} s, letterbox {LetterBox.IsPlaying}");
                        Shot("lead-quiet");
                    }
                }
                /* The host's copy breaks off at its second line, each the way the game or a player does it */
                if (Host && SceneBreak != null && Last >= 1)
                {
                    string Break = SceneBreak;
                    SceneBreak = null;
                    Plugin.Logger?.LogWarning($"{Tag} Scene test: the host's talk breaks off ({Break})");
                    if (Break == "chain" || Break == "scripted")
                        MMTools.MMConversation.Play(new MMTools.ConversationObject(new List<MMTools.ConversationEntry>
                        {
                            new(Me, "Scene test chained line", "talk", "", null, "Lamb")
                        }, null, null));
                    else if (Break == "scene")
                        MMTransition.Play(MMTransition.TransitionType.ChangeSceneAutoResume, MMTransition.Effect.BlackFade, "Base Biome 1", 1f, "", null);
                    else if (Break == "leave")
                        PauseMenuPatches.EndSession("Multiplayer/Steam/Stopped");
                    continue;
                }
                if (Box.DialogueWheel != null && Box.DialogueWheel.gameObject.activeSelf)
                    Answer.Invoke(Box, new object[] { Host ? 1 : 0 });
                else
                {
                    Box.TextPlayer?.SkipTypewriter();
                    Next.Invoke(Box, null);
                }
            }
            Plugin.Logger?.LogWarning($"{Tag} Scene test: over after {Time.unscaledTime - Started:F1} s, answer {Picked}, shared {COTLMP.Game.SharedScenes.Shared}, lines followed {COTLMP.Game.SharedScenes.LinesFollowed}, answers {COTLMP.Game.SharedScenes.AnswersFollowed}, ends {COTLMP.Game.SharedScenes.EndsFollowed}, leads lost {COTLMP.Game.SharedScenes.LeadsLost}, stuck {COTLMP.Game.SceneTrace.Stuck}, scripted released {COTLMP.Game.SharedScenes.ScriptedReleased}, late waits {COTLMP.Game.SharedScenes.LateWaits}, host lags {COTLMP.Game.SharedScenes.HostLags}");
            if (Talker != null)
                UnityEngine.Object.Destroy(Talker);
            yield return new WaitForSecondsRealtime(3f);
            yield return Until(() => !MMTransition.IsPlaying && PlayerFarming.Instance != null, 30f);
            yield return new WaitForSecondsRealtime(2f);
            Shot("scene-after");
            Plugin.Logger?.LogWarning($"{Tag} Scene test: after it in {SceneManager.GetActiveScene().name} talk={MMTools.MMConversation.isPlaying} letterbox={LetterBox.IsPlaying} state={PlayerFarming.Instance?.state?.CURRENT_STATE} hudhidden={HUD_Manager.Instance?.Hidden}");
        }

        /// <summary>
        /// Both copies play a scene in one dungeon room, the host's over 6 s before the friend's:
        /// the host's game must stand still until the friend's is over (scene ends test)
        /// </summary>
        private static IEnumerator SceneEndTogether()
        {
            while (SceneManager.GetActiveScene().name != DungeonScene || PlayerFarming.Instance == null || !AnyRemotePlayer())
                yield return null;
            bool Host = Plugin.GlobalsInternal.IsServerCreator;
            yield return new WaitForSecondsRealtime(Host ? 6f : 7f);
            float Started = Time.unscaledTime;
            Plugin.Logger?.LogWarning($"{Tag} Scene end test: our scene starts");
            GameManager.GetInstance().OnConversationNew();
            yield return new WaitForSecondsRealtime(Host != FriendEndsFirst ? 2f : 7f);
            GameManager.GetInstance().OnConversationEnd();
            float Ended = Time.unscaledTime;
            Plugin.Logger?.LogWarning($"{Tag} Scene end test: our scene is over after {Ended - Started:F1} s, time scale {Time.timeScale}");
            yield return new WaitForSecondsRealtime(1.5f);
            if (COTLMP.Game.SceneEnds.Waiting)
                Shot("scene-wait");
            Ended -= 1.5f;
            if (!Host && FriendEndsFirst)
            {
                yield return Until(() => COTLMP.Game.SceneEnds.Synced > 0 || Time.unscaledTime - Ended > 6.5f + 2.5f, 15f);
                if (COTLMP.Game.SceneEnds.Waiting)
                    Shot("sync-wait");
            }
            float Still = 0f;
            while (Time.unscaledTime - Ended < 15f)
            {
                if (Time.timeScale == 0f)
                    Still = Time.unscaledTime - Ended;
                yield return null;
            }
            Plugin.Logger?.LogWarning($"{Tag} Scene end test: held {COTLMP.Game.SceneEnds.Held}, stood still {Still:F1} s, waited {COTLMP.Game.SceneEnds.LastHeldFor:F1} s, synced {COTLMP.Game.SceneEnds.Synced} after {COTLMP.Game.SceneEnds.LastSyncFor:F1} s, time scale now {Time.timeScale}");
        }

        /// <summary>
        /// Both copies play the same film, the friend's 5 s later: the host's must hold its last
        /// frame until the friend's is over (film ends test)
        /// </summary>
        private static IEnumerator FilmEndTogether()
        {
            while (SceneManager.GetActiveScene().name != DungeonScene || PlayerFarming.Instance == null || !AnyRemotePlayer())
                yield return null;
            bool Host = Plugin.GlobalsInternal.IsServerCreator;
            yield return new WaitForSecondsRealtime(Host ? 6f : 11f);
            float Started = Time.unscaledTime;
            float Done = -1f;
            Plugin.Logger?.LogWarning($"{Tag} Film end test: our film starts");
            MMTools.MMVideoPlayer.Play(FilmName, () => Done = Time.unscaledTime, MMTools.MMVideoPlayer.Options.DISABLE, MMTools.MMVideoPlayer.Options.DISABLE);
            if (FilmSkipAt > 0f)
            {
                yield return Until(() => Done > 0f, FilmSkipAt);
                if (Done < 0f)
                {
                    Plugin.Logger?.LogWarning($"{Tag} Film end test: we press skip after {Time.unscaledTime - Started:F1} s");
                    COTLMP.Game.SceneEnds.Skip();
                    yield return new WaitForSecondsRealtime(1.5f);
                    Shot("film-skipped");
                }
            }
            else
            {
                yield return Until(() => Done > 0f, 4f);
                Shot("film-watching");
            }
            yield return Until(() => Done > 0f, 240f);
            Plugin.Logger?.LogWarning($"{Tag} Film end test: our film is over after {(Done > 0f ? Done - Started : -1f):F1} s, films held {COTLMP.Game.SceneEnds.FilmsHeld}, skips {COTLMP.Game.SceneEnds.FilmsSkipped}, waited {COTLMP.Game.SceneEnds.LastHeldFor:F1} s");
        }
    }
}

/* EOF */
