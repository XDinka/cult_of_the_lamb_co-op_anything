/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     A flight recorder of every way into and out of a scene's state during a session
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using HarmonyLib;
using MMTools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// Records each change of what a scene does to the player: the letterbox in and out, a
    /// conversation starting and closing, the HUD hidden and shown, our lamb taken out of play
    /// (InActive, CustomAnimation) and given back. Each goes to the log as one line with where we
    /// are and who called (the first few with the whole call stack), and into a ring of the last
    /// ones. A scene state held far longer than any scene runs, with nothing of the mod's waiting
    /// on the partner to explain it, is logged as stuck with that ring: a way in whose way out never
    /// came, readable in the stand's logs and in a player's (the log button).
    /// </summary>
    [HarmonyPatch]
    internal static class SceneTrace
    {
        private const int FullStacks = 12;
        private const int Kept = 200;
        /* The letterbox or a lamb out of play with no conversation on; a conversation whose line doesn't move */
        private const float StuckSceneGame = 45f, StuckTalkGame = 90f;
        /* -cotlmp-stuckafter=N (tests): both held states count as stuck after N s */
        internal static float StuckAfter = -1f;
        private static float StuckScene => StuckAfter > 0f ? StuckAfter : StuckSceneGame;
        private static float StuckTalk => StuckAfter > 0f ? StuckAfter : StuckTalkGame;

        private static int Stacks;
        private static readonly Queue<string> Ring = new();
        internal static int Entries, Exits, Stuck;

        public static void Initialize()
        {
            Plugin.MonoInstance.StartCoroutine(Watch());
        }

        /// <summary>
        /// The session is over: the next one gets its own full stacks and its own ring
        /// </summary>
        internal static void ResetSession()
        {
            Stacks = 0;
            Ring.Clear();
        }

        private static void Note(string What, bool In)
        {
            if (!CoopSync.InSession || CoopManager.CoopActive)
                return;
            try
            {
                if (In)
                    ++Entries;
                else
                    ++Exits;
                PlayerFarming Player = PlayerFarming.Instance;
                string Line = $"[SCENE] {What} in {CoopSync.CurrentPlace()}: letterbox={LetterBox.IsPlaying} talk={MMConversation.isPlaying} " +
                              $"state={Player?.state?.CURRENT_STATE} hudhidden={HUD_Manager.Instance?.Hidden} ts={Time.timeScale:0.00} from {Callers()}";
                Ring.Enqueue($"{Time.unscaledTime:F1} {Line}");
                while (Ring.Count > Kept)
                    Ring.Dequeue();
                if (++Stacks <= FullStacks)
                    Line += "\n" + Environment.StackTrace;
                Plugin.Logger?.LogInfo(Line);
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[SCENE] Tracing failed: {e.Message}");
            }
        }

        /* The first callers outside this recorder and Harmony's glue */
        private static string Callers()
        {
            StringBuilder Text = new();
            int Taken = 0;
            foreach (StackFrame Frame in new StackTrace(2, false).GetFrames() ?? Array.Empty<StackFrame>())
            {
                var Method = Frame.GetMethod();
                if (Method == null || Method.DeclaringType == typeof(SceneTrace) || (Method.DeclaringType?.FullName ?? "").StartsWith("HarmonyLib") ||
                    Method.Name.StartsWith("DMD<"))
                    continue;
                if (Taken++ > 0)
                    Text.Append(" < ");
                Text.Append($"{Method.DeclaringType?.Name}.{Method.Name}");
                if (Taken >= 4)
                    break;
            }
            return Text.ToString();
        }

        /* Something of the mod's or the game's that rightly holds a scene this long */
        private static string Excuse()
        {
            if (SceneEnds.Waiting)
                return "waiting for the partner's scene";
            if (SharedScenes.HoldingRoom)
                return "holding for the partner";
            if (MMTransition.IsPlaying)
                return "a transition";
            if (MMVideoPlayer.Instance != null && MMVideoPlayer.Instance.activeSelf)
                return "a film";
            if (Lamb.UI.UIMenuBase.ActiveMenus.Count > 0)
                return "a menu";
            if (COTLMP.Network.Reconnect.Active)
                return "connecting again";
            return null;
        }

        private static IEnumerator Watch()
        {
            WaitForSecondsRealtime Wait = new(1f);
            float SceneSince = -1f, TalkSince = -1f;
            int TalkLine = -2;
            bool Told = false;
            while (true)
            {
                yield return Wait;
                try
                {
                    PlayerFarming Player = PlayerFarming.Instance;
                    if (!CoopSync.InSession || CoopManager.CoopActive || Player == null || Excuse() != null)
                    {
                        SceneSince = TalkSince = -1f;
                        Told = false;
                        continue;
                    }
                    float Now = Time.unscaledTime;
                    bool Talk = MMConversation.isPlaying && !MMConversation.isBark;
                    bool OutOfPlay = Player.state != null && (Player.state.CURRENT_STATE == StateMachine.State.InActive ||
                                                              Player.state.CURRENT_STATE == StateMachine.State.CustomAnimation);
                    bool Scene = !Talk && (LetterBox.IsPlaying || OutOfPlay);

                    if (Talk && MMConversation.Position == TalkLine)
                    {
                        if (TalkSince < 0f)
                            TalkSince = Now;
                    }
                    else
                    {
                        TalkSince = -1f;
                    }
                    TalkLine = Talk ? MMConversation.Position : -2;
                    SceneSince = Scene ? (SceneSince < 0f ? Now : SceneSince) : -1f;

                    bool Held = (SceneSince >= 0f && Now - SceneSince > StuckScene) || (TalkSince >= 0f && Now - TalkSince > StuckTalk);
                    if (!Held)
                    {
                        Told = false;
                        continue;
                    }
                    if (Told)
                        continue;
                    Told = true;
                    ++Stuck;
                    Plugin.Logger?.LogWarning($"[SCENE] stuck in {CoopSync.CurrentPlace()} for {Now - (SceneSince >= 0f ? SceneSince : TalkSince):F0} s: " +
                                              $"letterbox={LetterBox.IsPlaying} talk={Talk}:{MMConversation.Position} state={Player.state?.CURRENT_STATE} " +
                                              $"hudhidden={HUD_Manager.Instance?.Hidden} ts={Time.timeScale:0.00}; the last {Ring.Count} changes:\n{string.Join("\n", Ring)}");
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogWarning($"[SCENE] The stuck watch stumbled: {e.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(LetterBox), nameof(LetterBox.Show))]
        [HarmonyPrefix]
        private static void LetterBoxIn()
        {
            if (!LetterBox.IsPlaying)
                Note("letterbox in", true);
        }

        [HarmonyPatch(typeof(LetterBox), nameof(LetterBox.Hide))]
        [HarmonyPrefix]
        private static void LetterBoxOut()
        {
            if (LetterBox.IsPlaying)
                Note("letterbox out", false);
        }

        [HarmonyPatch(typeof(MMConversation), nameof(MMConversation.Play))]
        [HarmonyPostfix]
        private static void TalkIn(ConversationObject ConversationObject) =>
            Note($"talk in ({ConversationObject?.Entries?.Count} lines, {ConversationObject?.Responses?.Count ?? 0} answers)", true);

        [HarmonyPatch(typeof(MMConversation), "DoClose")]
        [HarmonyPrefix]
        private static void TalkOut(bool _isBark)
        {
            if (_isBark == MMConversation.isBark && !_isBark)
                Note($"talk out (callback {(MMConversation.CURRENT_CONVERSATION?.CallBack != null ? "yes" : "no")})", false);
        }

        [HarmonyPatch(typeof(MMConversation), nameof(MMConversation.ClearConversation))]
        [HarmonyPrefix]
        private static void TalkCleared()
        {
            if (MMConversation.isPlaying)
                Note("talk cleared (no close)", false);
        }

        [HarmonyPatch(typeof(HUD_Manager), nameof(HUD_Manager.Hide))]
        [HarmonyPrefix]
        private static void HudOut(HUD_Manager __instance, bool Snap)
        {
            if (!__instance.Hidden)
                Note($"HUD hidden (snap {Snap})", true);
        }

        [HarmonyPatch(typeof(HUD_Manager), nameof(HUD_Manager.Show))]
        [HarmonyPrefix]
        private static void HudIn(HUD_Manager __instance, bool Force)
        {
            if (__instance.Hidden || Force)
                Note($"HUD shown (force {Force})", false);
        }

        private static bool OutOfPlay(StateMachine.State State) =>
            State == StateMachine.State.InActive || State == StateMachine.State.CustomAnimation;

        [HarmonyPatch(typeof(StateMachine), nameof(StateMachine.CURRENT_STATE), MethodType.Setter)]
        [HarmonyPrefix]
        private static void StateChanges(StateMachine __instance, StateMachine.State value)
        {
            PlayerFarming Player = PlayerFarming.Instance;
            if (Player == null || __instance != Player.state || !CoopSync.InSession)
                return;
            StateMachine.State Before = __instance.CURRENT_STATE;
            if (OutOfPlay(Before) == OutOfPlay(value))
                return;
            Note($"our lamb {(OutOfPlay(value) ? "out of play" : "back in play")} ({Before} -> {value})", OutOfPlay(value));
        }
    }
}

/* EOF */
