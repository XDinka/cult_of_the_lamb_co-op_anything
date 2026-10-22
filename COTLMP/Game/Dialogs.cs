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
    /// </remarks>
    internal static class Dialogs
    {
        private enum Step : byte
        {
            Line = 1,
            End = 2
        }

        private static readonly FieldInfo CurrentConversation = AccessTools.Field(typeof(MMConversation), "CURRENT_CONVERSATION");
        private static readonly FieldInfo PositionField = AccessTools.Field(typeof(MMConversation), "Position");

        /* What the partner says, shown by the overlay */
        private static string Speaker, Text;
        private static float ShownAt;
        private static bool Watching;
        private static List<CameraFollowTarget.Target> SavedTargets;

        /* Counters for the test harness report */
        internal static int LinesSent, LinesShown;

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

            private void OnGUI()
            {
                string Line = Text;
                if (string.IsNullOrEmpty(Line))
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
            }
        }

        [HarmonyPatch]
        private static class DialogsPatches
        {
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
