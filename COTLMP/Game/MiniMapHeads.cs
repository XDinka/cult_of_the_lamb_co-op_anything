/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     The players' heads on the rooms of the dungeon's minimap where they are
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using MMBiomeGeneration;
using System;
using UnityEngine;
using UnityEngine.UI;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// Who is where on the floor.
    /// </summary>
    /// <remarks>
    /// On the minimap of a dungeon floor each player's head (the Lamb's or the Goat's, the game's co-op
    /// pictures) stands on the room they are in: ours on ours, the partner's on the room RoomSync last
    /// heard them in, when it is on this floor. In one room both stand side by side in its middle. A head
    /// is a child of its room's icon, so it pans and grows with the map as the room's icon does.
    /// </remarks>
    internal static class MiniMapHeads
    {
        private static Sprite LambFace, GoatFace;

        /* Counters for the test harness report */
        internal static int Placed;

        public static void Initialize()
        {
            GameObject Driver = new("COTLMP Minimap Heads");
            UnityEngine.Object.DontDestroyOnLoad(Driver);
            Driver.AddComponent<Runner>();
        }

        /* The game's co-op face pictures, from any co-op ring of the HUD */
        private static float LastLook = -100f;

        private static bool Faces()
        {
            if (LambFace != null && GoatFace != null)
                return true;
            /* A search of everything loaded: not more than once in a while until found */
            if (Time.unscaledTime - LastLook < 5f)
                return false;
            LastLook = Time.unscaledTime;
            foreach (CoopIndicatorIcon Ring in Resources.FindObjectsOfTypeAll<CoopIndicatorIcon>())
            {
                if (Ring == null)
                    continue;
                LambFace ??= Ring.LambImage != null ? Ring.LambImage.sprite : null;
                GoatFace ??= Ring.GoatImage != null ? Ring.GoatImage.sprite : null;
                if (LambFace != null && GoatFace != null)
                    return true;
            }
            return false;
        }

        /* The room of a place on our floor, or null */
        private static MiniMapIcon RoomOf(MiniMap Map, string Place, string Floor)
        {
            if (Place == null || Floor == null || !Place.StartsWith(Floor, StringComparison.Ordinal))
                return null;
            string[] At = Place.Substring(Floor.Length).Split(',');
            if (At.Length != 2 || !int.TryParse(At[0], out int X) || !int.TryParse(At[1], out int Y))
                return null;
            return Map.Icons.Find(Icon => Icon != null && Icon.X == X && Icon.Y == Y);
        }

        private sealed class Runner : MonoBehaviour
        {
            private Image Ours, Theirs;
            private float Next;

            private void Update()
            {
                if (Time.unscaledTime < Next)
                    return;
                Next = Time.unscaledTime + 0.2f;
                try
                {
                    Place();
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogWarning($"[COOP] Placing the heads on the minimap failed: {e.Message}");
                    Next = Time.unscaledTime + 5f;
                }
            }

            private Image Head(Image Old, string Name)
            {
                if (Old != null)
                    return Old;
                Image Made = new GameObject(Name, typeof(RectTransform)).AddComponent<Image>();
                Made.preserveAspect = true;
                Made.raycastTarget = false;
                return Made;
            }

            private static void Hide(Image Item)
            {
                if (Item != null)
                    Item.gameObject.SetActive(false);
            }

            private void Place()
            {
                MiniMap Map = MiniMap.Instance;
                PlayerFarming Player = PlayerFarming.Instance;
                string Here = CoopSync.CurrentPlace();
                int At = Here?.IndexOf('@') ?? -1;
                if (!CoopSync.InSession || Map == null || !Map.isActiveAndEnabled || Player == null || At < 0 || BiomeGenerator.Instance == null || !Faces())
                {
                    Hide(Ours);
                    Hide(Theirs);
                    return;
                }
                string Floor = Here.Substring(0, At + 1);

                MiniMapIcon Mine = RoomOf(Map, Here, Floor);
                MiniMapIcon Partner = RoomOf(Map, RoomSync.PartnerPlace, Floor);
                bool WeAreGoat = CoopHud.IsGoatPlayer(Player);

                Ours = Head(Ours, "COTLMP Our Head");
                Theirs = Head(Theirs, "COTLMP Partner Head");
                Ours.sprite = WeAreGoat ? GoatFace : LambFace;
                Theirs.sprite = WeAreGoat ? LambFace : GoatFace;

                /* Together: side by side in the room's middle, the Lamb on the left as in the game's co-op */
                bool Together = Mine != null && Mine == Partner;
                float Apart = Together ? 0.28f : 0f;
                Stand(Ours, Mine, Together ? (WeAreGoat ? Apart : -Apart) : 0f);
                Stand(Theirs, Partner, Together ? (WeAreGoat ? -Apart : Apart) : 0f);
            }

            /* On the room's icon, this share of its width off its middle; none: hidden */
            private static void Stand(Image Head, MiniMapIcon Room, float Shift)
            {
                if (Room == null || !Room.gameObject.activeInHierarchy)
                {
                    Hide(Head);
                    return;
                }
                RectTransform Box = Room.rectTransform != null ? Room.rectTransform : (RectTransform)Room.transform;
                RectTransform Rect = Head.rectTransform;
                if (Rect.parent != Box)
                {
                    Rect.SetParent(Box, false);
                    ++Placed;
                }
                Rect.SetAsLastSibling();
                Rect.anchorMin = Rect.anchorMax = Rect.pivot = new Vector2(0.5f, 0.5f);
                Rect.localScale = Vector3.one;
                Rect.localRotation = Quaternion.identity;
                float Size = Mathf.Min(Box.rect.width, Box.rect.height) * 0.62f;
                Rect.sizeDelta = new Vector2(Size, Size);
                Rect.anchoredPosition = new Vector2(Box.rect.width * Shift, 0f);
                Head.gameObject.SetActive(true);
            }
        }
    }
}

/* EOF */
