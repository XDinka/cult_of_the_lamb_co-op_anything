/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     A text chat between the players of a session
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP.Game;
using COTLMP.Network;
using MMTools;
using Rewired;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Ui
{
    /// <summary>
    /// Typing to the partner.
    /// </summary>
    /// <remarks>
    /// In a session T opens a line at the bottom left; Enter sends it, Esc closes it. While it is open the
    /// game hears nothing of the keyboard or the pad (the controller maps that were on go off, and exactly
    /// those come back after). The last lines stand at the bottom left with the sender's Steam name and fade
    /// after 10 s; with the line open the whole history shows. Drawn with IMGUI, whose default font takes
    /// Chinese, Japanese and Korean from the system's fonts; IME input is on while typing. Arabic lines are
    /// shaped the way the game shapes its own.
    /// </remarks>
    internal static class TextChat
    {
        internal const int MaxLength = 120;
        private const int Shown = 6, Kept = 30;
        private const float FadeAfter = 10f;

        private sealed class Entry
        {
            internal string Name, Text;
            internal bool Ours;
            internal float At;
        }

        private static readonly List<Entry> History = new();

        /// <summary>
        /// The line is open (the game's input is off)
        /// </summary>
        internal static bool Open { get; private set; }

        /* Counters for the test harness report */
        internal static int Sent, Received;

        private static string Typed = "";
        private static bool Focus;
        private static List<ControllerMap> Silenced;
        private static IMECompositionMode WasIme;
        private static bool Tipped;

        public static void Initialize()
        {
            GameObject Driver = new("COTLMP Text Chat");
            UnityEngine.Object.DontDestroyOnLoad(Driver);
            Driver.AddComponent<Runner>();
            /* A reconnect keeps the history, a session that ended doesn't */
            COTLMP.Network.Network.OnDisconnect += () =>
            {
                if (!Reconnect.Active)
                    Reset();
            };
        }

        /// <summary>
        /// Sends a line to the partner and shows it as ours (also the test harness's way to type)
        /// </summary>
        internal static void Say(string Text)
        {
            Text = Clean(Text);
            if (Text.Length == 0 || !CoopSync.InSession)
                return;
            string Name = SteamBridge.LocalName();
            CoopSync.Send(CoopSync.Kind.ChatLine, Writer =>
            {
                Writer.Write(Name ?? "");
                Writer.Write(Text);
            });
            Add(Name, Text, true);
            ++Sent;
        }

        /// <summary>
        /// The test harness types: the line opens, gets the text, a frame is taken, Enter sends it. True if the
        /// game got back exactly the controller maps that were on before.
        /// </summary>
        internal static IEnumerator TestType(string Text, Action<bool> Done)
        {
            Player Pad = ReInput.players.GetPlayer(0);
            List<ControllerMap> Before = Pad.controllers.maps.GetAllMaps().Where(Map => Map.enabled).ToList();
            OpenLine();
            bool Off = !Pad.controllers.maps.GetAllMaps().Any(Map => Map.enabled);
            for (int Index = 1; Index <= Text.Length; Index++)
            {
                Typed = Text.Substring(0, Index);
                yield return new WaitForSecondsRealtime(0.05f);
            }
            yield return new WaitForSecondsRealtime(0.5f);
            COTLMP.Debug.HudProbe.Take("chat-typing");
            /* The frame is taken at the end of this one: the line stays open until then */
            yield return null;
            yield return null;
            string Line = Typed;
            Close();
            Say(Line);
            yield return null;
            yield return null;
            yield return null;
            List<ControllerMap> After = Pad.controllers.maps.GetAllMaps().Where(Map => Map.enabled).ToList();
            bool Same = Off && Before.Count == After.Count && Before.All(After.Contains);
            Plugin.Logger?.LogWarning($"[QA] Chat typed: the game's input was {(Off ? "off" : "STILL ON")} while typing, {Before.Count} maps on before, {After.Count} after{(Same ? "" : " (NOT THE SAME)")}");
            Done(Same);
        }

        /// <summary>
        /// A line from the partner
        /// </summary>
        internal static void OnLine(BinaryReader Reader)
        {
            string Name = Reader.ReadString();
            string Text = Clean(Reader.ReadString());
            if (Text.Length == 0)
                return;
            Add(Name, Text, false);
            ++Received;
            Plugin.Logger?.LogInfo($"[CHAT] {Name}: {Text}");
        }

        /// <summary>
        /// The session is over for good (not a reconnect): the history goes with it
        /// </summary>
        internal static void Reset()
        {
            Close();
            History.Clear();
            Tipped = false;
        }

        private static void Add(string Name, string Text, bool Ours)
        {
            History.Add(new Entry { Name = string.IsNullOrEmpty(Name) ? "?" : Name, Text = Text, Ours = Ours, At = Time.realtimeSinceStartup });
            if (History.Count > Kept)
                History.RemoveAt(0);
        }

        /* One line of plain text: no line breaks or control characters, not longer than the limit */
        private static string Clean(string Text)
        {
            if (string.IsNullOrEmpty(Text))
                return "";
            char[] Chars = Text.Where(Char => !char.IsControl(Char)).ToArray();
            string Line = new string(Chars).Trim();
            return Line.Length > MaxLength ? Line.Substring(0, MaxLength) : Line;
        }

        private static bool CanOpen()
        {
            return CoopSync.InSession && !Open && !MMTransition.IsPlaying && !Reconnect.Active && PlayerFarming.Instance != null &&
                   !Lamb.UI.UIMenuBase.ActiveMenus.Any(Menu => Menu != null && Menu.isActiveAndEnabled);
        }

        private static void OpenLine()
        {
            Open = true;
            Typed = "";
            Focus = true;
            WasIme = Input.imeCompositionMode;
            Input.imeCompositionMode = IMECompositionMode.On;
            /* Only the maps that are on go off, and only those come back: the game turns some off itself */
            try
            {
                Player Pad = ReInput.players.GetPlayer(0);
                Silenced = Pad.controllers.maps.GetAllMaps().Where(Map => Map.enabled).ToList();
                foreach (ControllerMap Map in Silenced)
                    Map.enabled = false;
            }
            catch (Exception e)
            {
                Silenced = null;
                Plugin.Logger?.LogWarning($"[CHAT] Can't turn the game's input off while typing: {e.Message}");
            }
        }

        private static void Close()
        {
            if (!Open)
                return;
            Open = false;
            Typed = "";
            Input.imeCompositionMode = WasIme;
            /* A frame later: the Enter or Esc that closed the line mustn't reach the game as a press */
            List<ControllerMap> Back = Silenced;
            Silenced = null;
            if (Back != null)
                Plugin.MonoInstance.StartCoroutine(GiveBack(Back));
        }

        private static IEnumerator GiveBack(List<ControllerMap> Maps)
        {
            yield return null;
            yield return null;
            foreach (ControllerMap Map in Maps)
            {
                try
                {
                    Map.enabled = true;
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogWarning($"[CHAT] Can't give the game its input back: {e.Message}");
                }
            }
        }

        /*
         * Arabic as the game shows its own lines (the box doesn't join or turn the letters itself). Only a line that is
         * mostly Arabic: the game's turning is for whole lines, a word of it among Latin ones would come out wrong.
         */
        private static string Shaped(string Text)
        {
            int Arabic = Text.Count(Char => Char >= '؀' && Char <= 'ۿ');
            int Letters = Text.Count(char.IsLetter);
            if (Arabic == 0 || Arabic * 2 < Letters)
                return Text;
            try
            {
                return LocalizeIntegration.ConvertToRTL(Text);
            }
            catch (Exception)
            {
                return Text;
            }
        }

        private sealed class Runner : MonoBehaviour
        {
            private GUIStyle LineStyle, NameStyle, FieldStyle;
            private static Texture2D Pixel;

            private void Update()
            {
                try
                {
                    if (Open && (!CoopSync.InSession || MMTransition.IsPlaying || Reconnect.Active))
                        Close();
                    if (!Open && Input.GetKeyDown(KeyCode.T) && CanOpen())
                        OpenLine();
                    /* Once a session: how to chat */
                    if (!Tipped && CoopSync.InSession && PlayerFarming.Instance != null && !MMTransition.IsPlaying)
                    {
                        Tipped = true;
                        NotificationCentre.Instance?.PlayGenericNotification("Multiplayer/Chat/Tip");
                    }
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogWarning($"[CHAT] {e.Message}");
                }
            }

            private void OnGUI()
            {
                if (!CoopSync.InSession && !Open)
                    return;
                float Now = Time.realtimeSinceStartup;
                List<Entry> Lines = Open ? History.Skip(Math.Max(0, History.Count - 12)).ToList()
                                         : History.Where(Item => Now - Item.At < FadeAfter + 1f).Skip(Math.Max(0, History.Count - Shown)).ToList();
                if (Lines.Count == 0 && !Open)
                    return;

                if (Pixel == null)
                {
                    Pixel = new Texture2D(1, 1);
                    Pixel.SetPixel(0, 0, Color.white);
                    Pixel.Apply();
                }
                int Size = Mathf.Max(13, Screen.height / 46);
                LineStyle ??= new GUIStyle(GUI.skin.label) { wordWrap = true, richText = false };
                NameStyle ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, richText = false };
                FieldStyle ??= new GUIStyle(GUI.skin.textField) { richText = false };
                LineStyle.fontSize = NameStyle.fontSize = FieldStyle.fontSize = Size;

                float X = Screen.width * 0.02f, Width = Screen.width * 0.34f;
                float Bottom = Screen.height * 0.86f;
                Color Old = GUI.color;

                /* The line being typed, at the bottom */
                if (Open)
                {
                    float FieldHeight = Size * 1.9f;
                    Rect Field = new(X, Bottom - FieldHeight, Width, FieldHeight);
                    GUI.color = new Color(0.05f, 0.03f, 0.05f, 0.85f);
                    GUI.DrawTexture(new Rect(Field.x - 4f, Field.y - 4f, Field.width + 8f, Field.height + 8f), Pixel);
                    GUI.color = Old;

                    Event Key = Event.current;
                    if (Key.type == EventType.KeyDown && (Key.keyCode == KeyCode.Return || Key.keyCode == KeyCode.KeypadEnter))
                    {
                        string Line = Typed;
                        Key.Use();
                        Close();
                        Say(Line);
                        return;
                    }
                    if (Key.type == EventType.KeyDown && Key.keyCode == KeyCode.Escape)
                    {
                        Key.Use();
                        Close();
                        return;
                    }

                    GUI.SetNextControlName("COTLMP Chat");
                    Typed = GUI.TextField(Field, Typed ?? "", MaxLength, FieldStyle);
                    if (string.IsNullOrEmpty(Typed))
                    {
                        GUI.color = new Color(1f, 1f, 1f, 0.45f);
                        GUI.Label(new Rect(Field.x + Size * 0.4f, Field.y + Size * 0.2f, Field.width, Field.height), I2.Loc.LocalizationManager.GetTranslation("Multiplayer/Chat/Hint"), LineStyle);
                        GUI.color = Old;
                    }
                    if (Focus)
                    {
                        GUI.FocusControl("COTLMP Chat");
                        Focus = false;
                    }
                    Bottom = Field.y - Size * 0.6f;
                }

                /* The lines above it, the newest lowest */
                float Y = Bottom;
                for (int Index = Lines.Count - 1; Index >= 0; Index--)
                {
                    Entry Item = Lines[Index];
                    float Alpha = Open ? 1f : Mathf.Clamp01(FadeAfter + 1f - (Now - Item.At));
                    string Name = Item.Name + ":";
                    float NameWidth = NameStyle.CalcSize(new GUIContent(Name)).x;
                    string Text = Shaped(Item.Text);
                    float Height = Mathf.Max(LineStyle.CalcHeight(new GUIContent(Text), Width - NameWidth - Size * 0.6f), Size * 1.4f);
                    Y -= Height + Size * 0.25f;

                    GUI.color = new Color(0.05f, 0.03f, 0.05f, 0.6f * Alpha);
                    GUI.DrawTexture(new Rect(X - 4f, Y - 2f, Width + 8f, Height + 4f), Pixel);
                    /* The Goat's names purple, the Lamb's red, as their rings */
                    bool Goat = Item.Ours ? Appearance.WeAreGoat : !Appearance.WeAreGoat;
                    NameStyle.normal.textColor = Goat ? new Color(0.62f, 0.42f, 1f, Alpha) : new Color(0.93f, 0.25f, 0.22f, Alpha);
                    LineStyle.normal.textColor = new Color(0.97f, 0.94f, 0.88f, Alpha);
                    GUI.color = Old;
                    GUI.Label(new Rect(X, Y, NameWidth + 2f, Size * 1.5f), Name, NameStyle);
                    GUI.Label(new Rect(X + NameWidth + Size * 0.3f, Y, Width - NameWidth - Size * 0.3f, Height), Text, LineStyle);
                }
                GUI.color = Old;
            }
        }
    }
}

/* EOF */
