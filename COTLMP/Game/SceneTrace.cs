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

        /* GoTos of our lamb and the puppets: since when each walks, and the counts the QA report reads */
        private const float GoToStuck = 10f;
        private static readonly Dictionary<PlayerFarming, float> Walking = new();
        /* How long each walk may take before it is stuck: its own time limit and a little, or ours without one */
        private static readonly Dictionary<PlayerFarming, float> WalkLimit = new();
        private static readonly HashSet<PlayerFarming> WalkTold = new();
        internal static int GoTos, GoTosEnded, GoTosAborted, GoTosBroken, GoTosStuck, GoTosReplaced;

        /* What a test counts as failed: a scene held for good, a walk's callback lost or never heard */
        internal static int Faults => Stuck + GoTosBroken + GoTosStuck + GoTosReplaced + InputsToPuppet + HudsOff + CoopHud.HudHeals + MenusStuck + RoomSync.LoopFaults;

        /*
         * The HUD as the player sees it, not its flag: Show sets Hidden=false at once and brings the hearts
         * and the map back in only from a coroutine a second later, so whatever cuts that short leaves the
         * flag up and the screen empty. In a dungeon, out of any scene, past this the parts must be in
         */
        private const float HudOffFor = 4f;
        private static float HudOffSince = -1f, HudArmedAt = -1f;
        private const float HudDisarmedFor = 10f;
        private static bool HudTold;
        internal static int HudsOff;
        private static readonly System.Reflection.FieldInfo HeartsSingle = AccessTools.Field(typeof(Health_Manager), "transition_Single");
        private static readonly System.Reflection.FieldInfo HeartsTwo = AccessTools.Field(typeof(Health_Manager), "transition_TwoPlayer");
        private static readonly System.Reflection.FieldInfo TopRightGroup = AccessTools.Field(typeof(HUD_Manager), "_topRightCanvasGroup");

        private static string HudPartsOff(HUD_Manager Hud)
        {
            List<string> Off = new();
            UI_Transitions Hearts = null;
            foreach (var Field in new[] { HeartsTwo, HeartsSingle })
            {
                if (Field?.GetValue(Hud.healthManager) is UI_Transitions Each && Each != null && Each.gameObject.activeInHierarchy)
                {
                    Hearts = Each;
                    break;
                }
            }
            /* Revealed comes only at the end of the move in: a move cut short leaves it unset, the bar half out */
            if (Hearts == null || !Hearts.Revealed)
                Off.Add(Hearts == null ? "hearts (none shown)" : "hearts");
            /* The blind faith fleece (13) keeps the map off by itself (ActivateMiniMap.OnEnable turns its object off) */
            if (Hud.MiniMapTransition != null && !Hud.MiniMapTransition.Revealed &&
                !(DataManager.Instance?.PlayerFleece == 13 && !Hud.MiniMapTransition.gameObject.activeSelf))
                Off.Add("map");
            return Off.Count == 0 ? null : string.Join(", ", Off);
        }

        /* For the test frames: the HUD's parts as the player sees them now */
        internal static string HudNow()
        {
            HUD_Manager Hud = HUD_Manager.Instance;
            if (Hud == null || Hud.healthManager == null)
                return "none";
            /* The top right (map, time) the game fades out itself while a room's doors are shut (RoomLockController) */
            float TopRight = TopRightGroup?.GetValue(Hud) is CanvasGroup Group && Group != null ? Group.alpha : -1f;
            string Corner = $"; top right {TopRight:0.0}, doors {(RoomLockController.DoorsOpen ? "open" : "shut")}";
            return (Hud.Hidden ? "hidden" : $"shown, {HudPartsOff(Hud) ?? "all"} {(HudPartsOff(Hud) == null ? "in" : "out")}") + Corner;
        }

        /* The HUD watchdog's heals seen so far: each is a scene's way in with no way out, to be found */
        private static int HealsSeen;

        private static void WatchHud()
        {
            if (CoopHud.HudHeals != HealsSeen)
            {
                HealsSeen = CoopHud.HudHeals;
                Plugin.Logger?.LogWarning($"[SCENE] INV-HUD the HUD watchdog had to bring the HUD back ({HealsSeen} so far) in {CoopSync.CurrentPlace()}; the last {Ring.Count} changes:\n{string.Join("\n", Ring)}");
            }
            HUD_Manager Hud = HUD_Manager.Instance;
            bool Should = Hud != null && !Hud.Hidden && Hud.healthManager != null && PlayerFarming.Instance != null &&
                          GameManager.IsDungeon(PlayerFarming.Location) && PlayerFarming.Location != FollowerLocation.IntroDungeon &&
                          !LetterBox.IsPlaying && !MMTransition.IsPlaying && !MMConversation.isPlaying && Time.timeScale > 0f;
            float Now = Time.unscaledTime;
            /* Disarmed for a moment (a room's change): the count goes on, the bars may be out through it */
            if (!Should)
            {
                if (HudOffSince >= 0f && Now - HudArmedAt > HudDisarmedFor)
                {
                    HudOffSince = -1f;
                    HudTold = false;
                }
                return;
            }
            HudArmedAt = Now;
            string Off = HudPartsOff(Hud);
            if (Off == null)
            {
                if (HudTold)
                    Plugin.Logger?.LogWarning($"[SCENE] INV-HUD over: the HUD is in again after {Now - HudOffSince:F0} s in {CoopSync.CurrentPlace()}");
                HudOffSince = -1f;
                HudTold = false;
                return;
            }
            if (HudOffSince < 0f)
                HudOffSince = Now;
            if (HudTold || Now - HudOffSince < HudOffFor)
                return;
            HudTold = true;
            ++HudsOff;
            Plugin.Logger?.LogWarning($"[SCENE] INV-HUD the HUD is shown by its flag, but {Off} stay out for {Now - HudOffSince:F0} s in {CoopSync.CurrentPlace()}: " +
                                      $"state={PlayerFarming.Instance.state?.CURRENT_STATE} ts={Time.timeScale:0.00}; the last {Ring.Count} changes:\n{string.Join("\n", Ring)}");
        }

        /* The menus' and talks' input was given to the partner's puppet (it has no controller: nothing reads on) */
        internal static int InputsToPuppet;

        /* A menu of the game's stood on screen past the HUD watchdog's patience, our lamb in play under it */
        internal static int MenusStuck;

        internal static void MenuStuck(string Menus, float For)
        {
            ++MenusStuck;
            Plugin.Logger?.LogWarning($"[SCENE] INV-MENU {Menus} on screen for {For:F0} s with our lamb in play (time running), in {CoopSync.CurrentPlace()}; the last {Ring.Count} changes:\n{string.Join("\n", Ring)}");
        }

        internal static void InputToPuppet(string Who)
        {
            ++InputsToPuppet;
            Plugin.Logger?.LogWarning($"[SCENE] INV-INPUT the UI input was going to the partner's puppet {Who}, given to our lamb instead, in {CoopSync.CurrentPlace()} from {Callers()}");
        }
        private static readonly System.Reflection.FieldInfo GoToCallback = AccessTools.Field(typeof(PlayerFarming), "GoToCallback");

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
            Walking.Clear();
            WalkLimit.Clear();
            WalkTold.Clear();
        }

        private static string Whose(PlayerFarming Player) =>
            Player == null ? "nobody" : Player == PlayerFarming.Instance ? "our lamb" : PlayerManager.IsPuppet(Player.health) ? "the partner's puppet" : Player.name;

        private static bool Waits(PlayerFarming Player) => Player != null && GoToCallback?.GetValue(Player) != null;

        /* A walk's line: its own, outside the scene states' ring and stacks (walks are many, those few) */
        private static void NoteWalk(string What)
        {
            try
            {
                Plugin.Logger?.LogInfo($"[SCENE] {What} in {CoopSync.CurrentPlace()} from {Callers()}");
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[SCENE] Tracing a walk failed: {e.Message}");
            }
        }

        /// <summary>
        /// The game walks this lamb somewhere and waits for it to arrive: whatever moves it or stops it meanwhile
        /// takes the arrival from whatever waits (a room's entry with no time limit, a leader's intro, fishing)
        /// </summary>
        internal static bool WalkedAndAwaited(PlayerFarming Player) => !WalksUnguarded && Player != null && Player.GoToAndStopping && Waits(Player);

        /* The same whatever the switch below says (what a test looks for) */
        internal static bool WalkedAndAwaitedAny(PlayerFarming Player) => Player != null && Player.GoToAndStopping && Waits(Player);

        /* Tests only (-cotlmp-nogotofix): the walks go unguarded as before, for the same build's "before" */
        internal static bool WalksUnguarded;

        /* The same change over and over (the game asks the HUD back every frame while a letterbox stands): told once, then counted */
        private static string LastWhat;
        private static int Repeats;

        private static void Note(string What, bool In)
        {
            if (!CoopSync.InSession || CoopManager.CoopActive)
                return;
            try
            {
                if (What == LastWhat)
                {
                    ++Repeats;
                    return;
                }
                if (Repeats > 0)
                    Plugin.Logger?.LogInfo($"[SCENE] ({LastWhat}: {Repeats} times more)");
                LastWhat = What;
                Repeats = 0;
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
            /* A choice on its wheel (the game waits for a player's pick) */
            ChoiceIndicator Choice = UnityEngine.Object.FindObjectOfType<ChoiceIndicator>();
            if (Choice != null && Choice.isActiveAndEnabled)
                return "a choice";
            if (COTLMP.Network.Reconnect.Active)
                return "connecting again";
            return null;
        }

        /// <summary>
        /// A walk the game waits for, never over: whatever waits (a room's entry, an interaction, a scene) waits on.
        /// Watched whatever else goes on (a wait at a door, a death screen, a load), as those are where it hides;
        /// a fallen lamb's walk is the game's to forget
        /// </summary>
        private static void WatchWalks()
        {
            float Now = Time.unscaledTime;
            foreach (var Pair in new List<KeyValuePair<PlayerFarming, float>>(Walking))
            {
                PlayerFarming Walker = Pair.Key;
                if (Walker == null || !Walker.GoToAndStopping)
                {
                    Walking.Remove(Walker);
                    continue;
                }
                float Limit = WalkLimit.TryGetValue(Walker, out float Given) ? Given : GoToStuck;
                if (Fallen(Walker) || !(Waits(Walker) || PlayerManager.IsPuppet(Walker.health)) || Now - Pair.Value <= Limit || !WalkTold.Add(Walker))
                    continue;
                ++GoTosStuck;
                Plugin.Logger?.LogWarning($"[SCENE] stuck: {Whose(Walker)} has walked for {Now - Pair.Value:F0} s (callback {(Waits(Walker) ? "waiting" : "none")}) in {CoopSync.CurrentPlace()}; " +
                                          $"the last {Ring.Count} changes:\n{string.Join("\n", Ring)}");
            }
        }

        private static bool Fallen(PlayerFarming Player) =>
            Player.state != null && (Player.state.CURRENT_STATE == StateMachine.State.GameOver || Player.state.CURRENT_STATE == StateMachine.State.Dead);

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
                    if (CoopSync.InSession && !CoopManager.CoopActive)
                    {
                        WatchWalks();
                        WatchHud();
                    }
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

        [HarmonyPatch(typeof(PlayerFarming), nameof(PlayerFarming.GoToAndStop), new Type[] { typeof(Vector3), typeof(GameObject), typeof(bool), typeof(bool), typeof(Action), typeof(float),
            typeof(bool), typeof(Action), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(Vector3?) })]
        [HarmonyPrefix]
        private static void WalkReplaces(PlayerFarming __instance)
        {
            if (!CoopSync.InSession || !WalkedAndAwaited(__instance) || Fallen(__instance))
                return;
            /* INV-GOTO: a new walk over one the game still waits for drops its callback just as an abort does;
               a fault of ours when our code asked for it (the game drops its own on purpose at times) */
            bool Ours = ByUs();
            if (Ours)
                ++GoTosReplaced;
            Plugin.Logger?.LogWarning($"[SCENE] INV-GOTO replaced{(Ours ? "" : " (by the game)")}: a new walk for {Whose(__instance)} drops the callback of the one it was on, in {CoopSync.CurrentPlace()}\n{Environment.StackTrace}");
        }

        [HarmonyPatch(typeof(PlayerFarming), nameof(PlayerFarming.GoToAndStop), new Type[] { typeof(Vector3), typeof(GameObject), typeof(bool), typeof(bool), typeof(Action), typeof(float),
            typeof(bool), typeof(Action), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(Vector3?) })]
        [HarmonyPostfix]
        private static void WalkBegins(PlayerFarming __instance, Vector3 TargetPosition, Action GoToCallback, float maxDuration)
        {
            if (!CoopSync.InSession || __instance == null)
                return;
            ++GoTos;
            Walking[__instance] = Time.unscaledTime;
            WalkLimit[__instance] = maxDuration > 0f ? Mathf.Max(GoToStuck, maxDuration + 2f) : GoToStuck;
            WalkTold.Remove(__instance);
            if (GoToCallback != null || PlayerManager.IsPuppet(__instance.health))
                NoteWalk($"{Whose(__instance)} walks to ({TargetPosition.x:F1},{TargetPosition.y:F1}) for {maxDuration:F0} s, callback {(GoToCallback != null ? "yes" : "no")}");
        }

        [HarmonyPatch(typeof(PlayerFarming), nameof(PlayerFarming.EndGoToAndStop))]
        [HarmonyPrefix]
        private static void WalkEnds(PlayerFarming __instance)
        {
            if (!CoopSync.InSession || __instance == null || !__instance.GoToAndStopping)
                return;
            ++GoTosEnded;
            float Took = Walking.TryGetValue(__instance, out float Since) ? Time.unscaledTime - Since : -1f;
            Walking.Remove(__instance);
            if (Waits(__instance) || PlayerManager.IsPuppet(__instance.health))
                NoteWalk($"{Whose(__instance)} arrived after {Took:F1} s, callback {(Waits(__instance) ? "runs" : "none")}");
        }

        [HarmonyPatch(typeof(PlayerFarming), nameof(PlayerFarming.AbortGoTo))]
        [HarmonyPrefix]
        private static void WalkAborted(PlayerFarming __instance)
        {
            if (!CoopSync.InSession || __instance == null || !__instance.GoToAndStopping)
                return;
            ++GoTosAborted;
            float Took = Walking.TryGetValue(__instance, out float Since) ? Time.unscaledTime - Since : -1f;
            Walking.Remove(__instance);
            bool Broken = Waits(__instance);
            NoteWalk($"{Whose(__instance)}'s walk aborted after {Took:F1} s{(Broken ? ", its callback LOST" : "")}");
            if (!Broken)
                return;
            /* INV-GOTO: whatever counted on that callback (both players there before a scene goes on) never hears it;
               a fault of ours when our code cut it short (the game cuts its own on purpose at times) */
            bool Ours = ByUs();
            if (Ours)
                ++GoTosBroken;
            Plugin.Logger?.LogWarning($"[SCENE] INV-GOTO{(Ours ? "" : " (by the game)")}: {Whose(__instance)}'s walk was aborted with its callback waiting, in {CoopSync.CurrentPlace()}\n{Environment.StackTrace}");
        }

        /* Our code (not this recorder, not a puppet's own removal) is on the way to this call */
        private static bool ByUs()
        {
            foreach (StackFrame Frame in new StackTrace(2, false).GetFrames() ?? Array.Empty<StackFrame>())
            {
                var Method = Frame.GetMethod();
                string Type = Method?.DeclaringType?.FullName ?? "";
                /* The test harness is no part of the mod: a walk of the game's it set going (the final test starts the
                   fight's intro itself) is the game's; the mod's code called from it is still the mod's */
                if (!Type.StartsWith("COTLMP") || Method.DeclaringType == typeof(SceneTrace) || Type.StartsWith("COTLMP.Debug"))
                    continue;
                return Method.Name != nameof(PlayerManager.DeletePlayer);
            }
            return false;
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
