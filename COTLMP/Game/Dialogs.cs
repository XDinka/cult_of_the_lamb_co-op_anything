/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     The partner's conversations show for us too, with the camera on them
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP.Data;
using COTLMP.Debug;
using HarmonyLib;
using I2.Loc;
using Lamb.UI;
using MMTools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

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
    /// Answers are the host's to pick. When the answers come up, both players see them: the one
    /// talking in the game's own answer wheel, the other above the partner's line. Each player's
    /// icon (the Lamb or the Goat, as the game's co-op shows over their heads) stands by the answer
    /// they are on. The joining player can only point at the answer they would like; a click on
    /// their own wheel points too. The host's pick is the answer, in either player's conversation.
    /// Only when the host isn't in the room (and can't see the answers) does the joining player
    /// answer alone.
    /// </remarks>
    internal static class Dialogs
    {
        private enum Step : byte
        {
            Line = 1,
            End = 2,
            /* The answers came up in the sender's conversation */
            Choice = 3,
            /* The answer the sender is on (an index) */
            Highlight = 4,
            /* The host's pick in the joining player's conversation */
            Decide = 5
        }

        private const int None = -1;

        private static readonly AccessTools.FieldRef<DialogueWheel, DialogueWheel.DialogueOption[]> WheelOptions =
            AccessTools.FieldRefAccess<DialogueWheel, DialogueWheel.DialogueOption[]>("_options");

        /* Our own answers on screen */
        private static DialogueWheel LocalWheel;
        private static bool ApplyingDecision;

        /* The partner's answers, shown in the overlay */
        private static List<string> RemoteOptions;

        /* Which answer each player is on, of whichever answers are up */
        private static int OurPick = None, PartnerPick = None;

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
        internal static int LinesSent, LinesShown, HighlightsSent, HighlightsSeen, Decisions;

        /// <summary>
        /// The answers of the partner's conversation on our screen, for the test harness
        /// </summary>
        internal static int RemoteChoiceCount => RemoteOptions?.Count ?? 0;
        internal static int PartnerPickNow => PartnerPick;
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
            OurPick = PartnerPick = None;
            PickIcons.Hide();
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
        /// Handles the partner's conversation: lines, answers, what they point at, the host's pick, the end.
        /// </summary>
        public static void OnRelay(BinaryReader Reader)
        {
            Step Type = (Step)Reader.ReadByte();
            string Place = Reader.ReadString();

            switch (Type)
            {
                case Step.End:
                    Stop();
                    return;

                case Step.Choice:
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
                    OurPick = PartnerPick = None;
                    ShownAt = Time.unscaledTime;
                    Text ??= "";
                    Watch();
                    return;
                }

                case Step.Highlight:
                    PartnerPick = Reader.ReadSByte();
                    ++HighlightsSeen;
                    return;

                case Step.Decide:
                {
                    int Answer = Reader.ReadSByte();
                    Plugin.Logger?.LogInfo($"[COOP] The host picked answer {Answer}");
                    AnswerLocal(Answer);
                    return;
                }
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
        /// Tells the partner which answer we are on.
        /// </summary>
        internal static void Point(int Answer)
        {
            if (Answer == OurPick)
                return;
            OurPick = Answer;
            ++HighlightsSent;
            Send(Step.Highlight, Writer => Writer.Write((sbyte)Answer));
        }

        /// <summary>
        /// A click on one of the partner's answers: the answer if we host, a pointer if we joined.
        /// </summary>
        internal static void PickRemote(int Answer)
        {
            if (RemoteOptions == null || Answer < 0 || Answer >= RemoteOptions.Count)
                return;
            Point(Answer);
            if (!IsHost)
                return;
            ++Decisions;
            Plugin.Logger?.LogInfo($"[COOP] Picking answer {Answer} of the partner's conversation");
            Send(Step.Decide, Writer => Writer.Write((sbyte)Answer));
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
            OurPick = PartnerPick = None;
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
        /// We clicked one of our answers. Returns false when it only points: the host picks.
        /// </summary>
        private static bool OnLocalPick(int Answer)
        {
            if (ApplyingDecision || !CoopSync.InSession || IsHost || !PartnerHere)
                return true;

            Point(Answer);
            Plugin.Logger?.LogInfo($"[COOP] Pointing at answer {Answer}, the host picks");
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
            if (LocalWheel == null)
                OurPick = PartnerPick = None;
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

        /* ICONS **************************************************************/

        /// <summary>
        /// The players' icons from the game's own co-op: the Lamb's and the Goat's.
        /// </summary>
        private static class Icons
        {
            private static Sprite Lamb, Goat;

            /// <summary>
            /// The icon of a player, by the character they look like
            /// </summary>
            public static Sprite For(bool Ours)
            {
                bool OurGoat = Appearance.LocalSkin() == Appearance.GoatSkin;
                bool PartnerGoat = PartnerSkin() == Appearance.GoatSkin;

                /* Both dressed alike: tell them apart the way co-op does, the host the Lamb */
                if (OurGoat == PartnerGoat)
                {
                    OurGoat = !IsHost;
                    PartnerGoat = IsHost;
                }
                return Get(Ours ? OurGoat : PartnerGoat);
            }

            private static int PartnerSkin()
            {
                for (uint Id = 0; Id < InternalData.MaxPlayersPerServerInternal; Id++)
                {
                    int Skin = PlayerManager.GetSkin(Id);
                    if (Skin >= 0)
                        return Skin;
                }
                return None;
            }

            private static Sprite Get(bool IsGoat)
            {
                Sprite Known = IsGoat ? Goat : Lamb;
                if (Known != null)
                    return Known;

                Sprite Found = FromHead(IsGoat) ?? FromConversation(IsGoat);
                if (Found == null)
                    return null;
                Plugin.Logger?.LogInfo($"[COOP] Using the game's {(IsGoat ? "Goat" : "Lamb")} icon '{Found.name}' for answers");
                if (IsGoat)
                    Goat = Found;
                else
                    Lamb = Found;
                return Found;
            }

            /// <summary>
            /// The indicator over a player's head (an accessibility option of local co-op)
            /// </summary>
            private static Sprite FromHead(bool IsGoat)
            {
                PlayerFarming Player = PlayerFarming.Instance;
                GameObject Indicator = Player == null ? null : IsGoat ? Player.CoopIndicatorGoat : Player.CoopIndicatorLamb;
                if (Indicator == null)
                    return null;
                SpriteRenderer Renderer = Indicator.GetComponentInChildren<SpriteRenderer>(true);
                if (Renderer != null && Renderer.sprite != null)
                    return Renderer.sprite;
                Image Picture = Indicator.GetComponentInChildren<Image>(true);
                return Picture != null ? Picture.sprite : null;
            }

            /// <summary>
            /// The icon local co-op shows by a conversation for whoever answers
            /// </summary>
            private static Sprite FromConversation(bool IsGoat)
            {
                CoopIndicatorIcon Icon = MMConversation.mmConversation != null ? MMConversation.mmConversation.CoopIndicator : null;
                Image Picture = Icon == null ? null : IsGoat ? Icon.GoatImage : Icon.LambImage;
                return Picture != null ? Picture.sprite : null;
            }
        }

        /// <summary>
        /// The icons on the buttons of our own answer wheel, as part of the game's UI.
        /// </summary>
        private static class PickIcons
        {
            private static Image Ours, Partners;

            public static void Update()
            {
                DialogueWheel Wheel = OpenWheel;
                DialogueWheel.DialogueOption[] Options = Wheel != null ? WheelOptions(Wheel) : null;
                if (Options == null)
                {
                    Hide();
                    return;
                }

                /* Above the answer's text, centred; side by side (ours on the left) when both are on one answer */
                float Shift = PartnerPick == OurPick ? 0.55f : 0f;
                Place(ref Partners, Options, PartnerPick, Icons.For(false), Shift);
                Place(ref Ours, Options, OurPick, Icons.For(true), -Shift);
            }

            public static void Hide()
            {
                if (Ours != null)
                    Ours.gameObject.SetActive(false);
                if (Partners != null)
                    Partners.gameObject.SetActive(false);
            }

            private static void Place(ref Image Icon, DialogueWheel.DialogueOption[] Options, int Answer, Sprite Picture, float Shift)
            {
                bool Show = Picture != null && Answer >= 0 && Answer < Options.Length && Options[Answer].Button != null;
                if (!Show)
                {
                    if (Icon != null)
                        Icon.gameObject.SetActive(false);
                    return;
                }

                RectTransform Button = (RectTransform)Options[Answer].Button.transform;
                if (Icon == null)
                {
                    GameObject Holder = new("COTLMP Answer Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                    Holder.GetComponent<LayoutElement>().ignoreLayout = true;
                    Icon = Holder.GetComponent<Image>();
                    Icon.preserveAspect = true;
                    Icon.raycastTarget = false;
                }
                if (Icon.transform.parent != Button)
                    Icon.transform.SetParent(Button, false);

                float Size = Mathf.Max(32f, Button.rect.height * 0.55f);
                RectTransform Rect = Icon.rectTransform;
                Rect.anchorMin = Rect.anchorMax = new Vector2(0.5f, 1f);
                Rect.pivot = new Vector2(0.5f, 0.5f);
                Rect.sizeDelta = new Vector2(Size, Size);
                Rect.anchoredPosition = new Vector2(Shift * Size * 2f, Size * 0.7f);
                Icon.sprite = Picture;
                Icon.transform.SetAsLastSibling();
                Icon.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// The partner's line at the bottom of the screen, and their answers above it
        /// </summary>
        private sealed class DialogOverlay : MonoBehaviour
        {
            private GUIStyle NameStyle, TextStyle, OptionStyle;
            private static Texture2D Pixel;

            private void Update()
            {
                try
                {
                    PickIcons.Update();
                }
                catch (Exception e)
                {
                    PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Placing the answer icons failed: {e.Message}");
                }

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
                string Line = Text;
                if (Line == null)
                    return;

                if (Pixel == null)
                {
                    Pixel = new Texture2D(1, 1);
                    Pixel.SetPixel(0, 0, Color.white);
                    Pixel.Apply();
                }
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

                /* The partner's answers in a row above the line: the host picks, the joining player points */
                OptionStyle ??= new GUIStyle(GUI.skin.button) { wordWrap = true };
                OptionStyle.fontSize = Size;
                float Gap = Size, OptionWidth = (Box.width - Gap * (Options.Count - 1)) / Options.Count, OptionHeight = Size * 2.6f;
                float IconSize = OptionHeight * 0.8f;
                for (int Index = 0; Index < Options.Count; Index++)
                {
                    Rect Area = new(Box.x + Index * (OptionWidth + Gap), Box.y - OptionHeight - Gap * 0.6f, OptionWidth, OptionHeight);

                    /* The mouse over an answer points at it */
                    if (Event.current.type == EventType.Repaint && Area.Contains(Event.current.mousePosition))
                        Point(Index);

                    GUI.color = OurPick == Index ? new Color(0.96f, 0.82f, 0.45f, 1f) : Color.white;
                    if (GUI.Button(Area, $"{Index + 1}. {Options[Index]}", OptionStyle))
                        PickRemote(Index);
                    GUI.color = Old;

                    /* Icons at the answer's top right corner, the partner's outermost */
                    float X = Area.xMax - IconSize * 0.6f, Y = Area.y - IconSize * 0.35f;
                    if (PartnerPick == Index && DrawIcon(new Rect(X - IconSize / 2f, Y - IconSize / 2f, IconSize, IconSize), Icons.For(false)))
                        X -= IconSize * 1.05f;
                    if (OurPick == Index)
                        DrawIcon(new Rect(X - IconSize / 2f, Y - IconSize / 2f, IconSize, IconSize), Icons.For(true));
                }
            }

            private static bool DrawIcon(Rect Area, Sprite Picture)
            {
                if (Picture == null || Picture.texture == null)
                    return false;
                Rect Source = Picture.textureRect;
                Texture Atlas = Picture.texture;
                Rect Uv = new(Source.x / Atlas.width, Source.y / Atlas.height, Source.width / Atlas.width, Source.height / Atlas.height);

                /* Keep the picture's proportions inside the square */
                float Aspect = Source.width / Mathf.Max(1f, Source.height);
                Rect Fit = Aspect >= 1f ? new Rect(Area.x, Area.center.y - Area.height / Aspect / 2f, Area.width, Area.height / Aspect)
                                        : new Rect(Area.center.x - Area.width * Aspect / 2f, Area.y, Area.width * Aspect, Area.height);
                GUI.DrawTextureWithTexCoords(Fit, Atlas, Uv);
                return true;
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

            /// <summary>
            /// Moving to an answer on our own wheel points at it for the partner
            /// </summary>
            [HarmonyPatch(typeof(DialogueWheel), "OnOptionSelected")]
            [HarmonyPostfix]
            private static void WheelSelected(DialogueWheel __instance, MMButton button)
            {
                if (!CoopSync.InSession || __instance != LocalWheel)
                    return;
                try
                {
                    DialogueWheel.DialogueOption[] Options = WheelOptions(__instance);
                    for (int Index = 0; Options != null && Index < Options.Length; Index++)
                    {
                        if (Options[Index].Button == button)
                            Point(Index);
                    }
                }
                catch (Exception e)
                {
                    PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Sharing the answer we are on failed: {e.Message}");
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
                    PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Holding the answer for the host failed: {e.Message}");
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
