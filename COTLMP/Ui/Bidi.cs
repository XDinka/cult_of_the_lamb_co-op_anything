/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Right-to-left text (Arabic) in the boxes the mod draws itself
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using RTLTMPro;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Ui
{
    /// <summary>
    /// Arabic in IMGUI.
    /// </summary>
    /// <remarks>
    /// IMGUI lays every row out left to right with the letters as they come, unjoined. The game's own
    /// boxes get their Arabic joined and turned by RTLTMPro (FixRTL, then reversed), and only when the game
    /// is in Arabic. Here a text is taken as written (logical order): it is broken into rows by words
    /// first, each row measured as it will show, and only then each row is joined and turned, so the rows
    /// read top to bottom. A paragraph that starts in Arabic is turned whole and stands at the right; in
    /// one that starts in Latin letters only the Arabic stretches are turned, where they are.
    /// </remarks>
    internal static class Bidi
    {
        private static readonly FastStringBuilder Output = new(1024);
        private static readonly Dictionary<string, List<string>> Rows = new();

        internal static bool IsRtl(char Char) => TextUtils.IsRTLCharacter(Char);

        /// <summary>
        /// Anything right-to-left in it
        /// </summary>
        internal static bool Has(string Text)
        {
            if (string.IsNullOrEmpty(Text))
                return false;
            foreach (char Char in Text)
            {
                if (IsRtl(Char))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// The paragraph's direction: its first letter's
        /// </summary>
        internal static bool RtlFirst(string Text)
        {
            foreach (char Char in Text)
            {
                if (IsRtl(Char))
                    return true;
                if (char.IsLetter(Char))
                    return false;
            }
            return false;
        }

        /* Joined and turned, as the game does for its own boxes */
        private static string Turned(string Text)
        {
            Output.Clear();
            RTLSupport.FixRTL(Text, Output, false, true, true);
            Output.Reverse();
            return Output.ToString();
        }

        /// <summary>
        /// One row as it is to show
        /// </summary>
        internal static string Row(string Text)
        {
            if (!Has(Text))
                return Text;
            if (RtlFirst(Text))
                return Turned(Text);

            /* A Latin row: each Arabic stretch (with the spaces and marks between its words) turned where it stands */
            StringBuilder Built = new(Text.Length + 8);
            int Index = 0;
            while (Index < Text.Length)
            {
                if (!IsRtl(Text[Index]))
                {
                    Built.Append(Text[Index++]);
                    continue;
                }
                int End = Index, LastRtl = Index;
                while (End < Text.Length && (IsRtl(Text[End]) || !char.IsLetterOrDigit(Text[End])))
                {
                    if (IsRtl(Text[End]))
                        LastRtl = End;
                    ++End;
                }
                Built.Append(Turned(Text.Substring(Index, LastRtl + 1 - Index)));
                Index = LastRtl + 1;
            }
            return Built.ToString();
        }

        /// <summary>
        /// The text broken into rows of this width, by words, in reading order
        /// </summary>
        internal static List<string> Break(string Text, GUIStyle Style, float Width)
        {
            string Key = $"{Style.fontSize}|{Style.fontStyle}|{(int)Width}|{Text}";
            if (Rows.TryGetValue(Key, out List<string> Known))
                return Known;
            if (Rows.Count > 128)
                Rows.Clear();

            List<string> Made = new();
            foreach (string Paragraph in Text.Split('\n'))
            {
                string Current = "";
                foreach (string Word in Paragraph.Split(' '))
                {
                    string Longer = Current.Length == 0 ? Word : Current + " " + Word;
                    if (Current.Length > 0 && Style.CalcSize(new GUIContent(Row(Longer))).x > Width)
                    {
                        Made.Add(Current);
                        Current = Word;
                    }
                    else
                    {
                        Current = Longer;
                    }
                }
                Made.Add(Current);
            }
            Rows[Key] = Made;
            return Made;
        }

        private static float RowHeight(GUIStyle Style) => Style.CalcHeight(new GUIContent("Ag"), 10000f);

        /// <summary>
        /// How high the text stands at this width (as GUIStyle.CalcHeight for one without right-to-left)
        /// </summary>
        internal static float Height(string Text, GUIStyle Style, float Width)
        {
            if (!Has(Text))
                return Style.CalcHeight(new GUIContent(Text), Width);
            return Break(Text, Style, Width).Count * RowHeight(Style);
        }

        /// <summary>
        /// GUI.Label that reads right-to-left text right
        /// </summary>
        internal static void Label(Rect Area, string Text, GUIStyle Style)
        {
            if (!Has(Text))
            {
                GUI.Label(Area, Text, Style);
                return;
            }
            bool Wrap = Style.wordWrap;
            TextAnchor Anchor = Style.alignment;
            Style.wordWrap = false;
            Style.alignment = RtlFirst(Text) ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
            float High = RowHeight(Style), Y = Area.y;
            foreach (string Each in Wrap ? Break(Text, Style, Area.width) : new List<string> { Text })
            {
                GUI.Label(new Rect(Area.x, Y, Area.width, High), Row(Each), Style);
                Y += High;
            }
            Style.wordWrap = Wrap;
            Style.alignment = Anchor;
        }
    }
}

/* EOF */
