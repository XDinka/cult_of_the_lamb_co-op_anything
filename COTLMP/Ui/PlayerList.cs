/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     The players of the session in the pause menu
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP.Game;
using COTLMP.Network;
using HarmonyLib;
using I2.Loc;
using Lamb.UI.PauseMenu;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Ui
{
    /// <summary>
    /// In a session the pause menu lists its players at the top left, right of the game's
    /// logo: the co-op icon each one plays as, their Steam name, who hosts and the ping
    /// </summary>
    [HarmonyPatch]
    internal static class PlayerList
    {
        [HarmonyPatch(typeof(UIPauseMenuController), "OnShowStarted")]
        [HarmonyPostfix]
        private static void OnShowStarted(UIPauseMenuController __instance, TextMeshProUGUI ____coopButtonText)
        {
            if (!Plugin.GlobalsInternal.InGameSession)
                return;
            try
            {
                Build(__instance, ____coopButtonText);
            }
            catch (System.Exception Error)
            {
                Plugin.Logger?.LogWarning($"[COOP] Can't show the player list: {Error.Message}");
            }
        }

        private const float Left = 520f, Top = -46f, RowHeight = 54f, IconSize = 44f;

        private static void Build(UIPauseMenuController Menu, TextMeshProUGUI Template)
        {
            Transform Old = Menu.transform.Find("COTLMP Players");
            if (Old != null)
                Object.Destroy(Old.gameObject);

            GameObject Root = new("COTLMP Players", typeof(RectTransform));
            RectTransform Box = (RectTransform)Root.transform;
            Box.SetParent(Menu.transform, false);
            Box.anchorMin = Box.anchorMax = Box.pivot = new Vector2(0f, 1f);
            Box.anchoredPosition = new Vector2(Left, Top);
            Box.sizeDelta = new Vector2(640f, 300f);

            Text(Box, Template, LocalizationManager.GetTranslation("Multiplayer/UI/Players").ToUpperInvariant(), new Vector2(0f, 0f), 30f, new Color(0.95f, 0.91f, 0.84f, 0.75f));
            Root.AddComponent<Rows>().Setup(Box, Template);
        }

        private static TextMeshProUGUI Text(RectTransform Parent, TextMeshProUGUI Template, string Line, Vector2 At, float Size, Color Tint)
        {
            GameObject Holder = new("Text", typeof(RectTransform));
            RectTransform Box = (RectTransform)Holder.transform;
            Box.SetParent(Parent, false);
            Box.anchorMin = Box.anchorMax = Box.pivot = new Vector2(0f, 1f);
            Box.anchoredPosition = At;
            Box.sizeDelta = new Vector2(600f, RowHeight);
            TextMeshProUGUI Label = Holder.AddComponent<TextMeshProUGUI>();
            if (Template != null)
            {
                Label.font = Template.font;
                Label.fontSharedMaterial = Template.fontSharedMaterial;
            }
            Label.fontSize = Size;
            Label.color = Tint;
            Label.alignment = TextAlignmentOptions.MidlineLeft;
            Label.enableWordWrapping = false;
            Label.raycastTarget = false;
            Label.text = Line;
            return Label;
        }

        /// <summary>
        /// The rows, refreshed twice a second while the menu is up (names arrive, the ping moves)
        /// </summary>
        private sealed class Rows : MonoBehaviour
        {
            private RectTransform Box;
            private TextMeshProUGUI Template;
            private readonly List<GameObject> Made = new();
            private float Next;

            public void Setup(RectTransform Box, TextMeshProUGUI Template)
            {
                this.Box = Box;
                this.Template = Template;
                Refresh();
            }

            private void Update()
            {
                if (Time.unscaledTime >= Next)
                    Refresh();
            }

            private void Refresh()
            {
                Next = Time.unscaledTime + 0.5f;
                foreach (GameObject Old in Made)
                    Destroy(Old);
                Made.Clear();

                bool WeHost = Plugin.GlobalsInternal.IsServerCreator;
                string HostMark = LocalizationManager.GetTranslation("Multiplayer/UI/Host");
                string YouMark = LocalizationManager.GetTranslation("Multiplayer/UI/You");
                float Y = -RowHeight;

                Row(Dialogs.Icons.For(true), SteamBridge.LocalName(), WeHost ? $"{YouMark}, {HostMark}" : YouMark, null, Y);
                foreach (string Name in SteamBridge.PartnerNames())
                {
                    Y -= RowHeight;
                    float Ping = ConnectionQuality.PingMs;
                    string Latency = Ping >= 0f ? $"{Ping:F0} {LocalizationManager.GetTranslation("Multiplayer/UI/Ms")}" : null;
                    Row(Dialogs.Icons.For(false), Name, WeHost ? null : HostMark, Latency, Y);
                }
            }

            private void Row(Sprite Picture, string Name, string Mark, string Latency, float Y)
            {
                if (Picture != null)
                {
                    GameObject Icon = new("Icon", typeof(RectTransform));
                    RectTransform IconBox = (RectTransform)Icon.transform;
                    IconBox.SetParent(Box, false);
                    IconBox.anchorMin = IconBox.anchorMax = IconBox.pivot = new Vector2(0f, 1f);
                    IconBox.anchoredPosition = new Vector2(0f, Y - (RowHeight - IconSize) / 2f);
                    IconBox.sizeDelta = new Vector2(IconSize, IconSize);
                    Image Image = Icon.AddComponent<Image>();
                    Image.sprite = Picture;
                    Image.preserveAspect = true;
                    Image.raycastTarget = false;
                    Made.Add(Icon);
                }

                string Line = $"<color=#F3E9D6>{Escape(Name)}</color>";
                if (!string.IsNullOrEmpty(Mark))
                    Line += $"  <color=#C41E2A>{Mark}</color>";
                if (!string.IsNullOrEmpty(Latency))
                    Line += $"  <color=#AB9A86>{Latency}</color>";
                Made.Add(Text(Box, Template, Line, new Vector2(IconSize + 14f, Y), 30f, Color.white).gameObject);
            }

            /* Steam names may hold rich text tags */
            private static string Escape(string Name) => (Name ?? "").Replace("<", "<​");
        }
    }
}

/* EOF */
