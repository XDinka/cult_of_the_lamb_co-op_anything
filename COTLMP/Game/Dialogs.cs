/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     The partner's conversations show for us too, with the camera on them
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP.Debug;
using HarmonyLib;
using I2.Loc;
using MMTools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// Mirrors conversations between the players.
    /// </summary>
    /// <remarks>
    /// When the partner talks to someone in the place we are in, their lines show at the bottom of
    /// our screen and our camera goes to them. Our own lamb is left alone: we can keep walking, and
    /// the camera comes back when they are done. Lines go as localization terms with the partner's
    /// text as a fallback, so each player reads them in their own language.
    ///
    /// Choices are voted on, as in co-op RPGs, and the host has the last word. When the answers
    /// come up, the partner sees them too (above the line) and can vote with the mouse or the 1-4
    /// keys; their mark shows next to the answer they picked. In the host's own conversation the
    /// host answers as usual. In the joining player's conversation their answer is a vote, and the
    /// host's pick (made in the same panel) is what their game answers. If the host isn't in the
    /// room, or doesn't decide within 30 s, the joining player answers alone.
    /// </remarks>
    internal static class Dialogs
    {
        private enum Step : byte
        {
            Line = 1,
            End = 2,
            /* The answers came up in the sender's conversation */
            Choice = 3,
            /* The sender's vote, an answer's index */
            Vote = 4,
            /* The host's decision for the joining player's conversation */
            Decide = 5
        }

        /// <summary>
        /// How long the joining player's answer waits for the host before it counts on its own
        /// </summary>
        private const float HostDecidesWithin = 30f;
        private const int NoVote = -1;

        private static readonly AccessTools.FieldRef<DialogueWheel, DialogueWheel.DialogueOption[]> WheelOptions =
            AccessTools.FieldRefAccess<DialogueWheel, DialogueWheel.DialogueOption[]>("_options");

        /* Our own answers on screen */
        private static DialogueWheel LocalWheel;
        private static float LocalChoiceAt;
        private static int OurLocalVote = NoVote;
        private static bool ApplyingDecision;

        /* The partner's answers, shown in the overlay */
        private static List<string> RemoteOptions;
        private static int OurRemoteVote = NoVote;

        /* The partner's vote, on whichever answers are up */
        private static int PartnerVote = NoVote;

        private static bool IsHost => Plugin.GlobalsInternal.IsServerCreator;

        /// <summary>
        /// The partner stands in our place (the server only shows us puppets in our place)
        /// </summary>
        private static bool PartnerHere => PlayerManager.GetAnyPuppetObject() != null;

        private static readonly FieldInfo CurrentConversation = AccessTools.Field(typeof(MMConversation), "CURRENT_CONVERSATION");
        private static readonly FieldInfo PositionField = AccessTools.Field(typeof(MMConversation), "Position");

        /* What the partner says, shown by the overlay */
        private static string Speaker, Text;
        private static float ShownAt;
        private static bool Watching;
        private static List<CameraFollowTarget.Target> SavedTargets;

        /* Counters for the test harness report */
        internal static int LinesSent, LinesShown, VotesSent, VotesSeen, Decisions;

        /// <summary>
        /// The answers of the partner's conversation on our screen, for the test harness
        /// </summary>
        internal static int RemoteChoiceCount => RemoteOptions?.Count ?? 0;
        internal static int PartnerVoteNow => PartnerVote;
        internal static DialogueWheel OpenWheel => LocalWheel != null && LocalWheel.isActiveAndEnabled ? LocalWheel : null;

        public static void Initialize()
        {
            MMConversation.OnConversationEnd += OnLocalEnd;
            GameObject Overlay = new("COTLMP Dialog Overlay");
            UnityEngine.Object.DontDestroyOnLoad(Overlay);
            Overlay.AddComponent<DialogOverlay>();
            Plugin.MonoInstance.StartCoroutine(KeepCamera());
        }

        private static void Send(Step Type, Action<BinaryWriter> Body)
        {
            if (!CoopSync.InSession)
                return;
            CoopSync.Send(CoopSync.Kind.Dialog, Writer =>
            {
                Writer.Write((byte)Type);
                Writer.Write(CoopSync.CurrentPlace() ?? "");
                Body?.Invoke(Writer);
            });
        }

        private static void OnLocalLine()
        {
            var Conversation = CurrentConversation.GetValue(null) as ConversationObject;
            int Position = (int)PositionField.GetValue(null);
            if (Conversation?.Entries == null || Position < 0 || Position >= Conversation.Entries.Count)
                return;

            ConversationEntry Entry = Conversation.Entries[Position];
            string NameTerm = Entry.CharacterName ?? "";
            string Term = Entry.TermToSpeak ?? "";
            string Name = NameTerm == "-" ? "" : Translate(NameTerm);
            string Line = Translate(Term);
            if (string.IsNullOrEmpty(Line))
                return;

            ++LinesSent;
            Send(Step.Line, Writer =>
            {
                Writer.Write(NameTerm);
                Writer.Write(Name);
                Writer.Write(Term);
                Writer.Write(Line);
            });
        }

        private static void OnLocalEnd(bool SetPlayerToIdle, bool ShowHUD)
        {
            LocalWheel = null;
            OurLocalVote = PartnerVote = NoVote;
            try
            {
                Send(Step.End, null);
            }
            catch (Exception e)
            {
                PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Sending a conversation end failed: {e.Message}");
            }
        }

        private static string Translate(string Term)
        {
            if (string.IsNullOrEmpty(Term))
                return "";
            string Value = LocalizationManager.GetTranslation(Term);
            return string.IsNullOrEmpty(Value) ? Term : Value;
        }

        /// <summary>
        /// Handles the partner's conversation lines and its end.
        /// </summary>
        public static void OnRelay(BinaryReader Reader)
        {
            Step Type = (Step)Reader.ReadByte();
            string Place = Reader.ReadString();

            if (Type == Step.End)
            {
                Stop();
                return;
            }

            if (Type == Step.Choice)
            {
                int Count = Reader.ReadByte();
                List<string> Options = new(Count);
                for (int Index = 0; Index < Count; Index++)
                {
                    string OptionTerm = Reader.ReadString(), Theirs = Reader.ReadString();
                    string Translated = string.IsNullOrEmpty(OptionTerm) ? null : LocalizationManager.GetTranslation(OptionTerm);
                    Options.Add(string.IsNullOrEmpty(Translated) ? Theirs : Translated);
                }
                if (Place != CoopSync.CurrentPlace() || MMConversation.isPlaying)
                    return;
                RemoteOptions = Options;
                OurRemoteVote = PartnerVote = NoVote;
                ShownAt = Time.unscaledTime;
                Text ??= "";
                Watch();
                return;
            }

            if (Type == Step.Vote)
            {
                PartnerVote = Reader.ReadSByte();
                ++VotesSeen;
                Plugin.Logger?.LogInfo($"[COOP] The partner votes for answer {PartnerVote}");
                return;
            }

            if (Type == Step.Decide)
            {
                int Answer = Reader.ReadSByte();
                Plugin.Logger?.LogInfo($"[COOP] The host decided on answer {Answer}");
                AnswerLocal(Answer);
                return;
            }

            string NameTerm = Reader.ReadString(), Name = Reader.ReadString();
            string Term = Reader.ReadString(), Line = Reader.ReadString();

            /* Only a conversation in the place we stand in concerns us */
            if (Place != CoopSync.CurrentPlace() || MMConversation.isPlaying)
                return;

            string Ours = LocalizationManager.GetTranslation(Term);
            Text = string.IsNullOrEmpty(Ours) ? Line : Ours;
            string OurName = NameTerm == "-" || string.IsNullOrEmpty(NameTerm) ? "" : LocalizationManager.GetTranslation(NameTerm);
            Speaker = string.IsNullOrEmpty(OurName) ? Name : OurName;
            ShownAt = Time.unscaledTime;
            ++LinesShown;
            Watch();
        }

        /// <summary>
        /// Our pick among the partner's answers: the decision if we host, a vote if we joined.
        /// </summary>
        internal static void PickRemote(int Answer)
        {
            if (RemoteOptions == null || Answer < 0 || Answer >= RemoteOptions.Count)
                return;
            OurRemoteVote = Answer;
            if (IsHost)
            {
                ++Decisions;
                Plugin.Logger?.LogInfo($"[COOP] Deciding on answer {Answer} of the partner's conversation");
                Send(Step.Decide, Writer => Writer.Write((sbyte)Answer));
            }
            else
            {
                ++VotesSent;
                Plugin.Logger?.LogInfo($"[COOP] Voting for answer {Answer} of the host's conversation");
                Send(Step.Vote, Writer => Writer.Write((sbyte)Answer));
            }
        }

        /// <summary>
        /// Our answers came up: show them to the partner.
        /// </summary>
        private static void OnLocalChoice(DialogueWheel Wheel)
        {
            List<MMTools.Response> Responses = MMConversation.CURRENT_CONVERSATION?.Responses;
            if (Responses == null || Responses.Count == 0)
                return;

            LocalWheel = Wheel;
            LocalChoiceAt = Time.unscaledTime;
            OurLocalVote = PartnerVote = NoVote;
            int Count = Mathf.Min(Responses.Count, WheelOptions(Wheel)?.Length ?? Responses.Count);
            Send(Step.Choice, Writer =>
            {
                Writer.Write((byte)Count);
                for (int Index = 0; Index < Count; Index++)
                {
                    string Term = Responses[Index]?.Term ?? "";
                    string Shown = Translate(Term);
                    Writer.Write(Term);
                    Writer.Write(string.IsNullOrEmpty(Shown) ? Responses[Index]?.text ?? "" : Shown);
                }
            });
        }

        /// <summary>
        /// We picked one of our answers. Returns false if it only counts as a vote for now.
        /// </summary>
        private static bool OnLocalPick(int Answer)
        {
            if (ApplyingDecision || !CoopSync.InSession || IsHost || !PartnerHere ||
                Time.unscaledTime - LocalChoiceAt > HostDecidesWithin)
                return true;

            OurLocalVote = Answer;
            ++VotesSent;
            Plugin.Logger?.LogInfo($"[COOP] Voting for answer {Answer}, the host decides");
            Send(Step.Vote, Writer => Writer.Write((sbyte)Answer));
            return false;
        }

        /// <summary>
        /// Answers our open conversation the way a click on the answer does.
        /// </summary>
        private static void AnswerLocal(int Answer)
        {
            DialogueWheel Wheel = OpenWheel;
            DialogueWheel.DialogueOption[] Options = Wheel != null ? WheelOptions(Wheel) : null;
            if (Options == null || Answer < 0 || Answer >= Options.Length)
                return;

            ApplyingDecision = true;
            try
            {
                Options[Answer].Button.onClick.Invoke();
            }
            finally
            {
                ApplyingDecision = false;
            }
        }

        /// <summary>
        /// Points our camera at the partner while they talk
        /// </summary>
        private static void Watch()
        {
            if (Watching)
                return;
            GameObject Partner = PlayerManager.GetAnyPuppetObject();
            CameraFollowTarget Camera = GameManager.GetInstance()?.CamFollowTarget;
            if (Partner == null || Camera == null)
                return;

            SavedTargets = new List<CameraFollowTarget.Target>(Camera.targets);
            Camera.targets.Clear();
            Camera.targets.Add(new CameraFollowTarget.Target(Partner, 1f));
            Watching = true;
        }

        private static void Stop()
        {
            Speaker = Text = null;
            RemoteOptions = null;
            OurRemoteVote = NoVote;
            if (LocalWheel == null)
                PartnerVote = NoVote;
            if (!Watching)
                return;
            Watching = false;

            CameraFollowTarget Camera = GameManager.GetInstance()?.CamFollowTarget;
            if (Camera == null)
                return;
            Camera.targets.Clear();
            if (SavedTargets != null)
            {
                foreach (CameraFollowTarget.Target Target in SavedTargets)
                {
                    if (Target?.gameObject != null)
                        Camera.targets.Add(Target);
                }
            }
            if (Camera.targets.Count == 0 && PlayerFarming.Instance != null)
                GameManager.GetInstance().AddToCamera(PlayerFarming.Instance.CameraBone);
            SavedTargets = null;
        }

        /// <summary>
        /// Holds the camera on the partner (other code adds our lamb back) and lets go if they vanish
        /// </summary>
        private static IEnumerator KeepCamera()
        {
            while (true)
            {
                yield return null;
                if (!Watching && Text == null)
                    continue;
                try
                {
                    GameObject Partner = PlayerManager.GetAnyPuppetObject();

                    /* The end got lost, we left, or our own conversation started: give the camera back */
                    if (!CoopSync.InSession || Partner == null || MMConversation.isPlaying || Time.unscaledTime - ShownAt > 30f)
                    {
                        Stop();
                        continue;
                    }

                    CameraFollowTarget Camera = GameManager.GetInstance()?.CamFollowTarget;
                    if (Watching && Camera != null && (Camera.targets.Count != 1 || Camera.targets[0].gameObject != Partner))
                    {
                        Camera.targets.Clear();
                        Camera.targets.Add(new CameraFollowTarget.Target(Partner, 1f));
                    }
                }
                catch (Exception e)
                {
                    PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Watching the partner's conversation failed: {e.Message}");
                    Stop();
                }
            }
        }

        /// <summary>
        /// The partner's line at the bottom of the screen
        /// </summary>
        private sealed class DialogOverlay : MonoBehaviour
        {
            private GUIStyle NameStyle, TextStyle;
            private static Texture2D Pixel;

            private GUIStyle OptionStyle, BadgeStyle;

            private void Update()
            {
                /* Number keys pick among the partner's answers (the game's own keys move our lamb) */
                if (RemoteOptions == null)
                    return;
                for (int Index = 0; Index < RemoteOptions.Count && Index < 4; Index++)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha1 + Index) || Input.GetKeyDown(KeyCode.Keypad1 + Index))
                        PickRemote(Index);
                }
            }

            private void OnGUI()
            {
                if (Pixel == null)
                {
                    Pixel = new Texture2D(1, 1);
                    Pixel.SetPixel(0, 0, Color.white);
                    Pixel.Apply();
                }
                int BadgeSize = Mathf.Max(16, Screen.height / 30);
                BadgeStyle ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                BadgeStyle.fontSize = Mathf.RoundToInt(BadgeSize * 0.7f);

                DrawLocalBadges(BadgeSize);

                string Line = Text;
                if (Line == null)
                    return;
                int Size = Mathf.Max(14, Screen.height / 40);
                NameStyle ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, richText = true };
                TextStyle ??= new GUIStyle(GUI.skin.label) { wordWrap = true, richText = true };
                NameStyle.fontSize = Size;
                TextStyle.fontSize = Size;
                NameStyle.normal.textColor = new Color(0.96f, 0.82f, 0.45f);
                TextStyle.normal.textColor = new Color(0.97f, 0.94f, 0.88f);

                float Width = Screen.width * 0.6f, Height = Size * 5.5f;
                Rect Box = new((Screen.width - Width) / 2f, Screen.height - Height - Screen.height * 0.05f, Width, Height);
                Color Old = GUI.color;
                GUI.color = new Color(0.05f, 0.03f, 0.05f, 0.85f);
                GUI.DrawTexture(Box, Pixel);
                GUI.color = new Color(0.75f, 0.12f, 0.12f, 1f);
                GUI.DrawTexture(new Rect(Box.x, Box.y, Box.width, 3f), Pixel);
                GUI.color = Old;

                float Pad = Size * 0.8f;
                if (!string.IsNullOrEmpty(Speaker))
                    GUI.Label(new Rect(Box.x + Pad, Box.y + Pad * 0.5f, Box.width - Pad * 2f, Size * 1.6f), Speaker, NameStyle);
                GUI.Label(new Rect(Box.x + Pad, Box.y + Pad * 0.5f + Size * 1.7f, Box.width - Pad * 2f, Height - Size * 2.2f), Line, TextStyle);

                List<string> Options = RemoteOptions;
                if (Options == null || Options.Count == 0)
                    return;

                /* The partner's answers in a row above the line, ours to vote on (or decide, as host) */
                OptionStyle ??= new GUIStyle(GUI.skin.button) { wordWrap = true };
                OptionStyle.fontSize = Size;
                float Gap = Size, OptionWidth = (Box.width - Gap * (Options.Count - 1)) / Options.Count, OptionHeight = Size * 2.6f;
                for (int Index = 0; Index < Options.Count; Index++)
                {
                    Rect Area = new(Box.x + Index * (OptionWidth + Gap), Box.y - OptionHeight - Gap * 0.6f, OptionWidth, OptionHeight);
                    bool Ours = OurRemoteVote == Index;
                    GUI.color = Ours ? new Color(0.96f, 0.82f, 0.45f, 1f) : Color.white;
                    if (GUI.Button(Area, $"{Index + 1}. {Options[Index]}", OptionStyle))
                        PickRemote(Index);
                    GUI.color = Old;
                    /* Marks inside the answer's right end: the partner's outermost, ours next to it */
                    float MarkY = Area.center.y, MarkX = Area.xMax - BadgeSize * 0.9f;
                    if (PartnerVote == Index)
                    {
                        Badge(new Vector2(MarkX, MarkY), BadgeSize, false);
                        MarkX -= BadgeSize * 1.4f;
                    }
                    if (Ours)
                        Badge(new Vector2(MarkX, MarkY), BadgeSize, true);
                }
            }

            /// <summary>
            /// The partner's mark next to the answer they voted for, on our own answers (and ours, while it waits for the host)
            /// </summary>
            private void DrawLocalBadges(int BadgeSize)
            {
                DialogueWheel Wheel = OpenWheel;
                if (Wheel == null || (PartnerVote == NoVote && OurLocalVote == NoVote))
                    return;
                DialogueWheel.DialogueOption[] Options = WheelOptions(Wheel);
                if (Options == null)
                    return;

                if (PartnerVote >= 0 && PartnerVote < Options.Length && Options[PartnerVote].Button != null)
                    Badge(ScreenPoint(Options[PartnerVote].Button.transform as RectTransform), BadgeSize, false);
                if (OurLocalVote >= 0 && OurLocalVote < Options.Length && Options[OurLocalVote].Button != null)
                {
                    Vector2 At = ScreenPoint(Options[OurLocalVote].Button.transform as RectTransform);
                    Badge(new Vector2(At.x - BadgeSize * 1.3f, At.y), BadgeSize, true);
                }
            }

            /// <summary>
            /// The top right corner of a UI element, in GUI coordinates
            /// </summary>
            private static Vector2 ScreenPoint(RectTransform Rect)
            {
                if (Rect == null)
                    return Vector2.zero;
                Vector3[] Corners = new Vector3[4];
                Rect.GetWorldCorners(Corners);
                Canvas Canvas = Rect.GetComponentInParent<Canvas>();
                Camera Camera = Canvas == null || Canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Canvas.worldCamera;
                Vector2 Point = RectTransformUtility.WorldToScreenPoint(Camera, Corners[2]);
                return new Vector2(Point.x, Screen.height - Point.y);
            }

            /// <summary>
            /// A square marker with the player's number (1 the host, 2 the joining player, as in the
            /// game's co-op), in the colour of their character: red for the Lamb, green for the Goat
            /// </summary>
            private void Badge(Vector2 Centre, int Size, bool Ours)
            {
                bool HostsMark = Ours == IsHost;
                Color Old = GUI.color;
                Rect Area = new(Centre.x - Size / 2f, Centre.y - Size / 2f, Size, Size);
                GUI.color = new Color(0f, 0f, 0f, 0.8f);
                GUI.DrawTexture(new Rect(Area.x - 2f, Area.y - 2f, Area.width + 4f, Area.height + 4f), Pixel);
                GUI.color = HostsMark ? new Color(0.85f, 0.2f, 0.2f, 1f) : new Color(0.36f, 0.75f, 0.42f, 1f);
                GUI.DrawTexture(Area, Pixel);
                GUI.color = Color.white;
                GUI.Label(Area, HostsMark ? "1" : "2", BadgeStyle);
                GUI.color = Old;
            }
        }

        [HarmonyPatch]
        private static class DialogsPatches
        {
            [HarmonyPatch(typeof(DialogueWheel), "OnEnable")]
            [HarmonyPostfix]
            private static void WheelShown(DialogueWheel __instance)
            {
                if (!CoopSync.InSession)
                    return;
                try
                {
                    OnLocalChoice(__instance);
                }
                catch (Exception e)
                {
                    PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Sharing the answers failed: {e.Message}");
                }
            }

            [HarmonyPatch(typeof(DialogueWheel), "OnOptionClicked")]
            [HarmonyPrefix]
            private static bool WheelClicked(int option)
            {
                try
                {
                    return OnLocalPick(option);
                }
                catch (Exception e)
                {
                    PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Voting failed: {e.Message}");
                    return true;
                }
            }

            [HarmonyPatch(typeof(MMConversation), "ShowNextLine")]
            [HarmonyPostfix]
            private static void ShowNextLine(bool playVOandAnim)
            {
                if (!playVOandAnim || !CoopSync.InSession || MMConversation.isBark)
                    return;
                try
                {
                    OnLocalLine();
                }
                catch (Exception e)
                {
                    PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Sharing a conversation line failed: {e.Message}");
                }
            }
        }
    }
}

/* EOF */
