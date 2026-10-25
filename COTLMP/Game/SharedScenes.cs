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

        internal static bool Leading => Led;

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

            if (Place != GatePlace)
            {
                GatePlace = Place;
                Gating = InLeaderRoom(Place) && PartnerOnRun && RoomSync.PartnerPlace != Place && !LetterBox.IsPlaying;
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
                try
                {
                    NotificationCentre.Instance?.PlayGenericNotification("Multiplayer/Scene/WaitPartner");
                }
                catch
                {
                }
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
        private static bool Next() => IsHost || Current == null || MMConversation.CURRENT_CONVERSATION != Current || !(Led || Waiting);

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
