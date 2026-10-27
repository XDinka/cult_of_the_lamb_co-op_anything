/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     A conversation both games play in one place goes at the host's pace, as one
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using HarmonyLib;
using MMTools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// The game's scenes in a place both players stand in: a boss's words before the fight, the
    /// opening's and the story's talks, the ones a room starts by itself.
    /// </summary>
    /// <remarks>
    /// Both games play such a scene, each its own copy. They went apart: one player read on or
    /// skipped and their game went on to what comes after (the boss woke and fought) while the
    /// other's was still talking, and the boss broke for the one behind. In the game's own co-op
    /// there is one scene for both. Here the host's game leads: when the friend's game plays the same
    /// conversation (the same lines) in the same place, the friend can't move it on, each of the
    /// host's lines moves it on, the host's answer is its answer, and it ends when the host's ends,
    /// so what follows starts in both games together. A conversation the host's game doesn't play
    /// (the friend's own talk) is the friend's as before: if no line of it comes from the host in a
    /// few seconds, the friend reads it at their own pace.
    ///
    /// A conversation never starts in a dungeon room with one player alone while the other is on the
    /// same floor: it waits, its box hidden, until the partner comes in (or falls, leaves the floor, or
    /// 3 minutes pass), then it starts for both. Meanwhile the one in the room moves about it freely but
    /// can't go through its doors, and reads why in a line of the game's at the bottom of the screen; the
    /// partner is told every few seconds that they are waited for, with nothing of theirs held. A
    /// leader's room holds whoever comes in first at its door the same way.
    /// </remarks>
    [HarmonyPatch]
    internal static class SharedScenes
    {
        private enum Event : byte
        {
            Line = 1,
            Answer = 2,
            End = 3
        }

        /* How long the friend's copy waits for the host's before it is the friend's own */
        private const float WaitForHost = 4f;
        /* What the host's game did with a conversation is kept this long, for a friend who comes late */
        private const float Remember = 20f;

        private sealed class HostScene
        {
            public int Position = -1;
            public int Answer = -1;
            public bool Ended;
            public float At;
        }

        private static readonly MethodInfo ShowNextLine = AccessTools.Method(typeof(MMConversation), "ShowNextLine");
        private static readonly MethodInfo GiveAnswer = AccessTools.Method(typeof(MMConversation), "OnGiveAnswer");

        private static bool IsHost => Plugin.GlobalsInternal.IsServerCreator;

        /* Ours: the conversation on screen, whether the host leads it, since when we wait */
        private static ConversationObject Current;
        private static int CurrentKey;
        private static string CurrentPlace;
        private static bool Led;
        private static float StartedAt;
        private static bool Applying;

        /* The host's conversations by place and lines */
        private static readonly Dictionary<(string, int), HostScene> Hosts = new();

        /* Counters for the test harness report */
        internal static int Shared, LinesFollowed, AnswersFollowed, EndsFollowed, Released;

        public static void Initialize()
        {
            GameObject Driver = new("COTLMP Shared Scenes");
            UnityEngine.Object.DontDestroyOnLoad(Driver);
            Driver.AddComponent<Runner>();
        }

        private static int KeyOf(ConversationObject Conversation)
        {
            unchecked
            {
                uint Value = 2166136261;
                if (Conversation?.Entries == null)
                    return 0;
                foreach (ConversationEntry Entry in Conversation.Entries)
                {
                    foreach (char C in Entry?.TermToSpeak ?? "")
                        Value = (Value ^ C) * 16777619;
                    Value = (Value ^ '|') * 16777619;
                }
                return (int)Value;
            }
        }

        private static void Send(Event Type, int Key, string Place, int Value)
        {
            if (!CoopSync.InSession || Place == null)
                return;
            CoopSync.Send(CoopSync.Kind.SceneStep, Writer =>
            {
                Writer.Write((byte)Type);
                Writer.Write(Place);
                Writer.Write(Key);
                Writer.Write(Value);
            });
        }

        /* THE HOST'S STEPS ***************************************************/

        public static void OnRelay(BinaryReader Reader)
        {
            Event Type = (Event)Reader.ReadByte();
            string Place = Reader.ReadString();
            int Key = Reader.ReadInt32();
            int Value = Reader.ReadInt32();
            if (IsHost)
                return;

            if (!Hosts.TryGetValue((Place, Key), out HostScene Scene))
            {
                if (Hosts.Count > 32)
                    Hosts.Clear();
                Hosts[(Place, Key)] = Scene = new HostScene();
            }
            Scene.At = Time.unscaledTime;
            switch (Type)
            {
                case Event.Line:
                    Scene.Position = Mathf.Max(Scene.Position, Value);
                    break;
                case Event.Answer:
                    Scene.Answer = Value;
                    break;
                case Event.End:
                    Scene.Ended = true;
                    break;
            }
            Follow();
        }

        /// <summary>
        /// Moves our copy of the host's conversation to where the host's is
        /// </summary>
        private static void Follow()
        {
            if (IsHost || !MMConversation.isPlaying || MMConversation.isBark || MMConversation.CURRENT_CONVERSATION != Current || Current == null)
                return;
            if (!Hosts.TryGetValue((CurrentPlace, CurrentKey), out HostScene Scene) || Time.unscaledTime - Scene.At > Remember)
                return;
            /* Only when the host was here as it started: our own talk the host isn't part of stays ours */
            if (!Led && !Waiting)
                return;

            if (!Led)
            {
                Led = true;
                ++Shared;
                Plugin.Logger?.LogInfo($"[COOP] The host leads the conversation in {CurrentPlace} ({Current.Entries?.Count} lines)");
            }

            MMConversation Box = MMConversation.mmConversation;
            if (Box == null)
                return;
            Applying = true;
            try
            {
                int Guard = 0;
                while (MMConversation.isPlaying && MMConversation.CURRENT_CONVERSATION == Current && MMConversation.Position >= 0 &&
                       MMConversation.Position < Scene.Position && Guard++ < 64)
                {
                    Box.TextPlayer?.StopShowingText();
                    ShowNextLine.Invoke(Box, new object[] { true });
                    ++LinesFollowed;
                }
                if (MMConversation.CURRENT_CONVERSATION != Current)
                    return;
                if (Scene.Answer >= 0 && Current.Responses != null && Scene.Answer < Current.Responses.Count)
                {
                    ++AnswersFollowed;
                    Plugin.Logger?.LogInfo($"[COOP] The host answered {Scene.Answer}, so do we");
                    GiveAnswer.Invoke(Box, new object[] { Scene.Answer });
                    return;
                }
                if (Scene.Ended && (Current.Responses == null || Current.Responses.Count == 0))
                {
                    ++EndsFollowed;
                    Plugin.Logger?.LogInfo("[COOP] The host's conversation is over, ours too");
                    Box.Close();
                }
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Following the host's conversation failed: {e.Message}");
            }
            finally
            {
                Applying = false;
            }
        }

        private sealed class Runner : MonoBehaviour
        {
            private void Update()
            {
                try
                {
                    Gate();
                    Pull();
                    HoldTick();
                    WaitedFor();
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogWarning($"[COOP] Waiting for the partner failed: {e.Message}");
                    Gating = false;
                }
                if (Current == null || IsHost)
                    return;
                if (!MMConversation.isPlaying || MMConversation.CURRENT_CONVERSATION != Current)
                {
                    Current = null;
                    Led = false;
                    return;
                }
                Follow();
                /* Nothing of it from the host: it is ours alone */
                if (!Led && Waiting && Time.unscaledTime - StartedAt > WaitForHost)
                {
                    Waiting = false;
                    ++Released;
                    Plugin.Logger?.LogInfo($"[COOP] The host doesn't play the conversation in {CurrentPlace}: it is ours");
                }
            }
        }

        /* Our copy holds still until the host's shows up or the wait is over */
        private static bool Waiting;

        private static bool PartnerHere(string Place) =>
            CoopSync.InSession && Place != null && (RoomSync.PartnerPlace == Place || RoomSync.PartnerLastPlace == Place) && GhostLoot.PartnerHere() != null;

        /* THE LEADER'S ROOM: COMING IN TOGETHER *****************************/

        /* Whoever comes in first waits at the door this long at most */
        private const float GateAtMost = 90f;

        private static string GatePlace;
        private static Vector3 GateAt;
        private static float GateSince, GateNoted;
        private static bool Gating;

        /// <summary>
        /// The session is over: nothing of it is to steer the next one
        /// </summary>
        internal static void ResetSession()
        {
            /* Not waiting at a door any more: its line goes too */
            if (Gating)
                Dialogs.GameLine = null;
            GatePlace = null;
            Gating = false;
        }

        private static float PullAt = -1f;
        private static Vector3 PullTo;

        internal static int Gated, Pulled;

        private static bool InLeaderRoom(string Place) =>
            Place != null && Place.Contains("#") && MMBiomeGeneration.BiomeGenerator.Instance?.CurrentRoom?.IsBoss == true && !IntroCoop.Apart;

        /* The partner is on this run, standing, and could come in */
        private static bool PartnerOnRun => CoopSync.InSession && !CoopLife.PartnerFallen && SameStop(RoomSync.PartnerLastPlace, CoopSync.CurrentPlace());

        /* Two places on the same floor and stop of the map (the part before the room) */
        private static bool SameStop(string A, string B)
        {
            int At = A?.IndexOf('@') ?? -1, Bt = B?.IndexOf('@') ?? -1;
            return At > 0 && Bt > 0 && string.CompareOrdinal(A, 0, B, 0, Math.Max(At, Bt)) == 0;
        }

        /// <summary>
        /// The first into a leader's room waits by the door for the partner, as the opening waits at
        /// the crown: its scene and its fight start for both, never for one alone
        /// </summary>
        private static void Gate()
        {
            PlayerFarming Player = PlayerFarming.Instance;
            string Place = Player != null && !MMTools.MMTransition.IsPlaying ? CoopSync.CurrentPlace() : null;
            if (Place == null)
                return;
            /* Out of a session nothing is waited for: a session that starts in this room looks at it anew */
            if (!CoopSync.InSession && !Gating)
            {
                GatePlace = null;
                return;
            }

            if (Place != GatePlace)
            {
                GatePlace = Place;
                Gating = InLeaderRoom(Place) && PartnerOnRun && RoomSync.PartnerPlace != Place && !LetterBox.IsPlaying && !Rejoin.Going;
                if (Gating)
                {
                    GateAt = Player.transform.position;
                    GateSince = Time.unscaledTime;
                    GateNoted = -100f;
                    ++Gated;
                    Plugin.Logger?.LogInfo($"[COOP] First into the leader's room {Place}: waiting for the partner at the door");
                }
            }
            if (!Gating)
                return;

            if (RoomSync.PartnerPlace == Place || !PartnerOnRun || Time.unscaledTime - GateSince > GateAtMost)
            {
                Gating = false;
                Dialogs.GameLine = null;
                TellHold(Place, false);
                Plugin.Logger?.LogInfo($"[COOP] The leader's room {Place}: {(RoomSync.PartnerPlace == Place ? "the partner came in, going on together" : "going on alone")}");
                return;
            }
            Vector3 Gap = Player.transform.position - GateAt;
            Gap.z = 0f;
            if (Gap.magnitude > 1.5f)
            {
                Player.AbortGoTo();
                Player.transform.position = GateAt;
            }
            if (Time.unscaledTime - GateNoted > 6f)
            {
                GateNoted = Time.unscaledTime;
                Dialogs.GameLine = T("Multiplayer/Scene/WaitPartner");
                TellHold(Place, true);
            }
        }

        /// <summary>
        /// A scene started for the partner where we stand too: we are taken to where it started, so
        /// ours starts at once, as the game's co-op takes both players into it
        /// </summary>
        public static void OnPull(BinaryReader Reader)
        {
            string Place = Reader.ReadString();
            Vector3 At = new(Reader.ReadSingle(), Reader.ReadSingle(), 0f);
            if (Place != CoopSync.CurrentPlace() || !InLeaderRoom(Place) || LetterBox.IsPlaying)
                return;
            PullTo = At;
            PullAt = Time.unscaledTime + 0.6f;
        }

        private static void Pull()
        {
            if (PullAt < 0f || Time.unscaledTime < PullAt)
                return;
            PullAt = -1f;
            PlayerFarming Player = PlayerFarming.Instance;
            if (Player == null || LetterBox.IsPlaying || MMTools.MMTransition.IsPlaying || !InLeaderRoom(CoopSync.CurrentPlace()))
                return;
            ++Pulled;
            Plugin.Logger?.LogInfo("[COOP] The partner's scene started: going to where it starts");
            Gating = false;
            Player.AbortGoTo();
            Player.transform.position = PullTo + new Vector3(0.8f, 0f);
        }

        [HarmonyPatch(typeof(LetterBox), nameof(LetterBox.Show))]
        [HarmonyPostfix]
        private static void SceneShown()
        {
            try
            {
                string Place = CoopSync.CurrentPlace();
                PlayerFarming Player = PlayerFarming.Instance;
                if (Player == null || !CoopSync.InSession || !InLeaderRoom(Place) || RoomSync.PartnerPlace != Place)
                    return;
                Vector3 At = Player.transform.position;
                CoopSync.Send(CoopSync.Kind.ScenePull, Writer =>
                {
                    Writer.Write(Place);
                    Writer.Write(At.x);
                    Writer.Write(At.y);
                });
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Telling the partner about our scene failed: {e.Message}");
            }
        }

        /* A CONVERSATION IN A ROOM: NOT WITHOUT THE PARTNER ********************/

        /* A conversation waits for the partner this long at most */
        private const float HoldAtMost = 180f;

        private static readonly FieldInfo ConversationNewEvent = AccessTools.Field(typeof(MMConversation), "OnConversationNew");
        private static readonly FieldInfo ConversationRoot = AccessTools.Field(typeof(MMConversation), "Instance");

        private static ConversationObject HeldTalk;
        private static string HeldPlace;
        private static float HeldSince, HeldNoted;
        private static bool HeldInactive, HeldSnap, HeldLetterBox, Releasing;

        /* The partner waits for us in this place (to tell us now and then) */
        private static string WaitedIn;
        private static float WaitedNoted;

        internal static int TalksHeld, TalksReleased, HoldsTold;

        /* For the line box, as written (it lays right-to-left text out itself) */
        private static string T(string Key) => I2.Loc.LocalizationManager.GetTranslation(Key, FixForRTL: false);

        /// <summary>
        /// We wait in this room for the partner: its doors aren't ours to go through meanwhile
        /// </summary>
        internal static bool HoldingRoom => HeldTalk != null || Gating;

        /// <summary>
        /// We tried a door while we wait: the line says why it doesn't open
        /// </summary>
        internal static int DoorsHeld;

        internal static void DoorTried()
        {
            if (++DoorsHeld % 30 == 1)
                Plugin.Logger?.LogInfo("[COOP] A door of the room we wait in stays shut for us");
            Dialogs.GameLine = T(Gating ? "Multiplayer/Scene/WaitPartner" : "Multiplayer/Scene/HoldTalk");
        }

        /* A conversation is held where the partner could come and isn't */
        private static bool ShouldHold(string Place) =>
            CoopSync.InSession && Place != null && Place.Contains("#") && GameManager.IsDungeon(PlayerFarming.Location) && PartnerOnRun &&
            RoomSync.PartnerPlace != Place && !IntroCoop.Apart && !Rejoin.Going && !InLeaderRoom(Place) && PlayerFarming.Instance != null;

        /* The conversation's box and its speech bubble (drawn apart from it) show or not */
        private static void ShowBox(bool Shown)
        {
            List<GameObject> Parts = new();
            if (ConversationRoot?.GetValue(null) is GameObject Root && Root != null)
                Parts.Add(Root);
            if (MMConversation.mmConversation?.SpeechBubble != null)
                Parts.Add(MMConversation.mmConversation.SpeechBubble.gameObject);
            foreach (GameObject Part in Parts)
            {
                CanvasGroup Group = Part.GetComponent<CanvasGroup>() ?? Part.AddComponent<CanvasGroup>();
                Group.alpha = Shown ? 1f : 0f;
                Group.blocksRaycasts = Shown;
            }
        }

        private static void TellHold(string Place, bool Holding)
        {
            if (!CoopSync.InSession || Place == null)
                return;
            CoopSync.Send(CoopSync.Kind.SceneHold, Writer =>
            {
                Writer.Write(Place);
                Writer.Write(Holding);
            });
        }

        /// <summary>
        /// The partner waits for us somewhere (or doesn't any more)
        /// </summary>
        public static void OnHold(BinaryReader Reader)
        {
            string Place = Reader.ReadString();
            bool Holding = Reader.ReadBoolean();
            WaitedIn = Holding ? Place : null;
            WaitedNoted = -100f;
            Plugin.Logger?.LogInfo(Holding ? $"[COOP] The partner waits for us in {Place}" : $"[COOP] The partner doesn't wait for us in {Place} any more");
        }

        /* Waited for: told now and then, nothing of ours held */
        private static void WaitedFor()
        {
            if (WaitedIn == null)
                return;
            if (!CoopSync.InSession || CoopSync.CurrentPlace() == WaitedIn)
            {
                WaitedIn = null;
                return;
            }
            if (Time.unscaledTime - WaitedNoted < 8f || MMTransition.IsPlaying)
                return;
            WaitedNoted = Time.unscaledTime;
            ++HoldsTold;
            try
            {
                NotificationCentre.Instance?.PlayGenericNotification("Multiplayer/Scene/PartnerWaits");
            }
            catch
            {
            }
        }

        /// <summary>
        /// A conversation started in a room the partner isn't in: it waits, hidden, and we get our lamb back
        /// </summary>
        [HarmonyPatch(typeof(MMConversation), nameof(MMConversation.Play))]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Low)]
        private static void HoldTalk(ConversationObject ConversationObject, bool SetPlayerInactiveOnStart, bool SnapLetterBox, bool ShowLetterBox)
        {
            try
            {
                if (Releasing || HeldTalk != null || ConversationObject == null || !MMConversation.isPlaying || MMConversation.isBark)
                    return;
                string Place = CoopSync.CurrentPlace();
                if (!ShouldHold(Place))
                    return;
                HeldTalk = ConversationObject;
                HeldPlace = Place;
                HeldSince = Time.unscaledTime;
                HeldNoted = -100f;
                HeldInactive = SetPlayerInactiveOnStart;
                HeldSnap = SnapLetterBox;
                HeldLetterBox = ShowLetterBox;
                ++TalksHeld;
                ShowBox(false);
                GameManager.GetInstance()?.OnConversationEnd(true, true);
                Dialogs.GameLine = T("Multiplayer/Scene/HoldTalk");
                TellHold(Place, true);
                Plugin.Logger?.LogInfo($"[COOP] A conversation in {Place} waits for the partner to come in ({ConversationObject.Entries?.Count} lines)");
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Holding a conversation for the partner failed: {e.Message}");
            }
        }

        private static void HoldTick()
        {
            if (HeldTalk == null)
                return;
            string Place = CoopSync.CurrentPlace();

            /* It went meanwhile (we fell, the room changed): nothing to start */
            if (!MMConversation.isPlaying || MMConversation.CURRENT_CONVERSATION != HeldTalk || (Place != null && Place != HeldPlace))
            {
                Plugin.Logger?.LogInfo($"[COOP] The conversation held in {HeldPlace} is gone");
                DropHold(true);
                return;
            }
            bool Here = RoomSync.PartnerPlace == HeldPlace;
            bool Alone = !PartnerOnRun || Time.unscaledTime - HeldSince > HoldAtMost;
            if (Here && Time.unscaledTime - HeldNoted < 0f)
                return;
            if (Here && HeldNoted < 0f)
            {
                /* A moment for the partner to be in */
                HeldNoted = Time.unscaledTime + 1f;
                return;
            }
            if (!Here && !Alone)
                return;
            ReleaseHold(Here ? "the partner came in" : "going on alone");
        }

        private static void DropHold(bool Show)
        {
            if (Show)
                ShowBox(true);
            string Place = HeldPlace;
            HeldTalk = null;
            HeldPlace = null;
            Dialogs.GameLine = null;
            TellHold(Place, false);
        }

        private static void ReleaseHold(string Why)
        {
            ConversationObject Talk = HeldTalk;
            DropHold(true);
            ++TalksReleased;
            Plugin.Logger?.LogInfo($"[COOP] The conversation starts: {Why} ({Time.unscaledTime - HeldSince:F1} s)");
            Releasing = true;
            try
            {
                /* What Play does as it starts, now: the lamb stands still, the letterbox comes in */
                (ConversationNewEvent?.GetValue(null) as Delegate)?.DynamicInvoke(HeldInactive, HeldSnap, HeldLetterBox);
                Played(Talk);
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Starting the held conversation failed: {e.Message}");
            }
            finally
            {
                Releasing = false;
            }
        }

        /* PATCHES ************************************************************/

        [HarmonyPatch(typeof(MMConversation), nameof(MMConversation.Play))]
        [HarmonyPostfix]
        private static void Played(ConversationObject ConversationObject)
        {
            try
            {
                if (!CoopSync.InSession || ConversationObject == null)
                    return;
                Current = ConversationObject;
                CurrentKey = KeyOf(ConversationObject);
                SceneEnds.Talked(CurrentKey);
                CurrentPlace = CoopSync.CurrentPlace();
                StartedAt = Time.unscaledTime;
                Led = false;
                Waiting = !IsHost && PartnerHere(CurrentPlace);
                if (IsHost)
                    Send(Event.Line, CurrentKey, CurrentPlace, 0);
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Watching a conversation failed: {e.Message}");
            }
        }

        /// <summary>
        /// The host's lines go to the friend; the friend's copy the host leads, or that waits for it, doesn't move on its own
        /// </summary>
        [HarmonyPatch(typeof(MMConversation), "HandleNextClicked")]
        [HarmonyPrefix]
        private static bool Next() => !Held() && (IsHost || Current == null || MMConversation.CURRENT_CONVERSATION != Current || !(Led || Waiting));

        /* The conversation on screen waits for the partner: no reading on, no answer */
        private static bool Held() => HeldTalk != null && MMConversation.CURRENT_CONVERSATION == HeldTalk;

        /* Not a line on while it waits for the partner, whoever asks */
        [HarmonyPatch(typeof(MMConversation), "ShowNextLine")]
        [HarmonyPrefix]
        private static bool NextLine() => !Held();

        [HarmonyPatch(typeof(MMConversation), "ShowNextLine")]
        [HarmonyPostfix]
        private static void LineShown(bool playVOandAnim)
        {
            if (!IsHost || !playVOandAnim || MMConversation.isBark || Current == null || MMConversation.CURRENT_CONVERSATION != Current)
                return;
            Send(Event.Line, CurrentKey, CurrentPlace, MMConversation.Position);
        }

        [HarmonyPatch(typeof(MMConversation), "OnGiveAnswer")]
        [HarmonyPrefix]
        private static bool Answered(int Answer)
        {
            if (Held())
                return false;
            if (Current == null || MMConversation.CURRENT_CONVERSATION != Current)
                return true;
            if (IsHost)
            {
                Send(Event.Answer, CurrentKey, CurrentPlace, Answer);
                return true;
            }
            /* The host answers for both while it leads */
            return Applying || !(Led || Waiting);
        }

        [HarmonyPatch(typeof(MMConversation), nameof(MMConversation.Close))]
        [HarmonyPrefix]
        private static void Closed() => HostEnds();

        [HarmonyPatch(typeof(MMConversation), nameof(MMConversation.CloseImmediately))]
        [HarmonyPrefix]
        private static void ClosedNow() => HostEnds();

        private static void HostEnds()
        {
            if (IsHost && Current != null && MMConversation.CURRENT_CONVERSATION == Current)
            {
                Send(Event.End, CurrentKey, CurrentPlace, 0);
                Current = null;
            }
        }
    }
}

/* EOF */
