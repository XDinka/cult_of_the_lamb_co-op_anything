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

        /// <summary>
        /// Both copies play the same conversation in one dungeon room, the friend's a little later:
        /// the host reads on every 2 s and answers the second answer, the friend tries to read on
        /// every 0.3 s and to answer the first
        /// </summary>
        private static IEnumerator SceneTogether()
        {
            while (SceneManager.GetActiveScene().name != "Dungeon1" || PlayerFarming.Instance == null || !AnyRemotePlayer())
                yield return null;
            bool Host = Plugin.GlobalsInternal.IsServerCreator;
            yield return new WaitForSecondsRealtime(Host ? 6f : 7f);

            GameObject Me = PlayerFarming.Instance.gameObject;
            var Entries = new List<MMTools.ConversationEntry>
            {
                new(Me, "Scene test line one", "talk", "", null, "Lamb"),
                new(Me, "Scene test line two", "talk", "", null, "Lamb"),
                new(Me, "Scene test line three", "talk", "", null, "Lamb"),
                new(Me, "Scene test line four", "talk", "", null, "Lamb")
            };
            int Picked = -1;
            var Responses = new List<MMTools.Response>
            {
                new("Scene test answer one", () => Picked = 0, "Scene test answer one"),
                new("Scene test answer two", () => Picked = 1, "Scene test answer two")
            };
            float Started = Time.unscaledTime;
            Plugin.Logger?.LogWarning($"{Tag} Scene test: starting");
            MMTools.MMConversation.Play(new MMTools.ConversationObject(Entries, Responses, null));
            var Next = AccessTools.Method(typeof(MMTools.MMConversation), "HandleNextClicked");
            var Answer = AccessTools.Method(typeof(MMTools.MMConversation), "OnGiveAnswer");
            int Last = -2;
            while (MMTools.MMConversation.isPlaying && Time.unscaledTime - Started < 40f)
            {
                yield return new WaitForSecondsRealtime(Host ? 2f : 0.3f);
                var Box = MMTools.MMConversation.mmConversation;
                if (Box == null)
                    continue;
                if (MMTools.MMConversation.Position != Last)
                {
                    Last = MMTools.MMConversation.Position;
                    Plugin.Logger?.LogWarning($"{Tag} Scene test: at line {Last} after {Time.unscaledTime - Started:F1} s");
                }
                if (Box.DialogueWheel != null && Box.DialogueWheel.gameObject.activeSelf)
                    Answer.Invoke(Box, new object[] { Host ? 1 : 0 });
                else
                {
                    Box.TextPlayer?.SkipTypewriter();
                    Next.Invoke(Box, null);
                }
            }
            Plugin.Logger?.LogWarning($"{Tag} Scene test: over after {Time.unscaledTime - Started:F1} s, answer {Picked}, shared {COTLMP.Game.SharedScenes.Shared}, lines followed {COTLMP.Game.SharedScenes.LinesFollowed}, answers {COTLMP.Game.SharedScenes.AnswersFollowed}, ends {COTLMP.Game.SharedScenes.EndsFollowed}");
        }

        /// <summary>
        /// Both copies play a scene in one dungeon room, the host's over 6 s before the friend's:
        /// the host's game must stand still until the friend's is over (scene ends test)
        /// </summary>
        private static IEnumerator SceneEndTogether()
        {
            while (SceneManager.GetActiveScene().name != "Dungeon1" || PlayerFarming.Instance == null || !AnyRemotePlayer())
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
            while (SceneManager.GetActiveScene().name != "Dungeon1" || PlayerFarming.Instance == null || !AnyRemotePlayer())
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
