/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Both players' crowns on the map between floors: where each stands and where each looks
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using HarmonyLib;
using Lamb.UI;
using Map;
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// The partner's crown on the map between floors.
    /// </summary>
    /// <remarks>
    /// The map shows our crown on the stop we are at. The partner's stands there too, in its
    /// player's colour (the Lamb's red, the Goat's purple, as the game's co-op marks them), and ours
    /// takes our colour. While a player points at a way on their map, a small faint crown of theirs
    /// sits on that stop on the other's map. The map and its picks are shared by MapSync; this only
    /// shows who is where and who looks at what.
    /// </remarks>
    [HarmonyPatch]
    internal static class MapCrowns
    {
        private sealed class Cursor
        {
            public int Run, Map;
            public Point At, Looking;
            public bool Goat, Open;
            public float Seen;
        }

        private const float SendEvery = 0.5f;
        private const float GoneAfter = 5f;

        private static readonly AccessTools.FieldRef<UIAdventureMapOverlayController, RectTransform> CrownField =
            AccessTools.FieldRefAccess<UIAdventureMapOverlayController, RectTransform>("_crownSpineRectTransform");
        private static readonly AccessTools.FieldRef<UIAdventureMapOverlayController, AdventureMapNode> CurrentField =
            AccessTools.FieldRefAccess<UIAdventureMapOverlayController, AdventureMapNode>("_currentNode");

        private static Cursor Partner;
        private static Point Looking;
        private static float SentAt;
        private static bool Changed;

        /* The copies on the map on screen */
        private static UIAdventureMapOverlayController Built;
        private static RectTransform PartnerCrown, PartnerLook;

        /* Counters for the test harness report */
        internal static int Sent, Received, Shown;

        internal static string PartnerState => Partner == null ? "-" : $"{Partner.At?.x},{Partner.At?.y}>{Partner.Looking?.x},{Partner.Looking?.y} run {Partner.Run}/{CoopSync.CurrentRun} map {Partner.Map}/{MapSync.MapIndex}";

        public static void Initialize()
        {
            GameObject Driver = new("COTLMP Map Crowns");
            UnityEngine.Object.DontDestroyOnLoad(Driver);
            Driver.AddComponent<Runner>();
        }

        /* The crown's eye takes a player's colour (the rest of it is drawn red and black) */
        private static readonly string[] EyeSlots = { "images/Crown_Eye", "images/Crown_Eye2", "images/Crown_Eye3", "images/Crown_Eye4" };

        /// <summary>
        /// A crown as a player's: the eye in their colour and their co-op head (the Lamb's or the Goat's) above it
        /// </summary>
        private static void Paint(RectTransform Crown, bool Goat, float Alpha)
        {
            foreach (Graphic Part in Crown.GetComponentsInChildren<Graphic>(true))
            {
                if (Part.name != HeadName)
                    Part.color = new Color(1f, 1f, 1f, Alpha);
            }
            foreach (Spine.Unity.SkeletonGraphic Art in Crown.GetComponentsInChildren<Spine.Unity.SkeletonGraphic>(true))
            {
                foreach (string Name in EyeSlots)
                {
                    Spine.Slot Eye = Art.Skeleton?.FindSlot(Name);
                    if (Eye == null)
                        continue;
                    Color Colour = Goat ? StaticColors.GoatPurple : Color.white;
                    Eye.R = Colour.r;
                    Eye.G = Colour.g;
                    Eye.B = Colour.b;
                }
            }

            Transform Head = Crown.Find(HeadName);
            Sprite Icon = Dialogs.Icons.Get(Goat);
            if (Head == null && Icon != null)
            {
                GameObject Made = new(HeadName, typeof(RectTransform), typeof(Image));
                Made.transform.SetParent(Crown, false);
                Head = Made.transform;
                RectTransform Place = (RectTransform)Head;
                Place.sizeDelta = new Vector2(70f, 70f);
                Place.anchoredPosition = new Vector2(0f, 120f);
                Made.GetComponent<Image>().raycastTarget = false;
            }
            if (Head != null)
            {
                Image Picture = Head.GetComponent<Image>();
                Picture.sprite = Icon;
                Picture.preserveAspect = true;
                Picture.color = new Color(1f, 1f, 1f, Alpha);
            }
        }

        private const string HeadName = "COTLMP Coop Head";

        /* OURS ***************************************************************/

        /// <summary>
        /// Where we are on the run's map, all through the run (the partner's map may open long after
        /// ours closed), and what we point at while ours is open
        /// </summary>
        private static void SendOurs(UIAdventureMapOverlayController Map)
        {
            Point At = (Map?.Map ?? MapManager.Instance?.CurrentMap)?.GetCurrentNode()?.point;
            if (!CoopSync.InSession || CoopSync.CurrentRun == 0 || At == null)
                return;
            if (Map == null)
                Looking = null;
            if (!Changed && Time.unscaledTime - SentAt < (Map != null ? SendEvery : SendEvery * 2f))
                return;
            Changed = false;
            SentAt = Time.unscaledTime;
            CoopSync.Send(CoopSync.Kind.MapCursor, Writer =>
            {
                Writer.Write(CoopSync.CurrentRun);
                Writer.Write(MapSync.MapIndex);
                Writer.Write((short)At.x);
                Writer.Write((short)At.y);
                Writer.Write((short)(Looking?.x ?? -1));
                Writer.Write((short)(Looking?.y ?? -1));
                Writer.Write(Appearance.WeAreGoat);
                Writer.Write(Map != null);
            });
            ++Sent;
        }

        /* THEIRS *************************************************************/

        public static void OnRelay(BinaryReader Reader)
        {
            Cursor Item = new() { Run = Reader.ReadInt32(), Map = Reader.ReadInt32() };
            Item.At = new Point(Reader.ReadInt16(), Reader.ReadInt16());
            int X = Reader.ReadInt16(), Y = Reader.ReadInt16();
            Item.Looking = X < 0 ? null : new Point(X, Y);
            Item.Goat = Reader.ReadBoolean();
            Item.Open = Reader.ReadBoolean();
            Item.Seen = Time.unscaledTime;
            Partner = Item;
            ++Received;
        }

        private static void ShowTheirs(UIAdventureMapOverlayController Map)
        {
            RectTransform Ours = CrownField(Map);
            if (Ours == null)
                return;

            if (Built != Map)
            {
                Built = Map;
                PartnerCrown = UnityEngine.Object.Instantiate(Ours.gameObject, Ours.parent).GetComponent<RectTransform>();
                PartnerCrown.name = "COTLMP Partner Crown";
                PartnerLook = UnityEngine.Object.Instantiate(Ours.gameObject, Ours.parent).GetComponent<RectTransform>();
                PartnerLook.name = "COTLMP Partner Looks";
                PartnerCrown.SetSiblingIndex(Ours.GetSiblingIndex());
                PartnerLook.SetSiblingIndex(Ours.GetSiblingIndex());
            }
            /* Every frame: the crown's own animation may set its eye back */
            if (CoopSync.InSession)
                Paint(Ours, Appearance.WeAreGoat, 1f);

            Cursor Item = Partner;
            if (Item != null && !Item.Open)
                Item.Looking = null;
            /* The map is the host's for both (MapSync): a stop of the partner's run that is on ours is theirs */
            bool Here = Item != null && CoopSync.InSession && Item.Run == CoopSync.CurrentRun && Time.unscaledTime - Item.Seen < GoneAfter;
            AdventureMapNode Stand = Here ? Map.NodeFromPoint(Item.At) : null;
            AdventureMapNode Look = Here && Item.Looking != null && !Item.Looking.Equals(Item.At) ? Map.NodeFromPoint(Item.Looking) : null;

            if (Stand != null)
            {
                /* Beside ours when both stand on the same stop */
                bool Together = CurrentField(Map) == Stand;
                PartnerCrown.position = Stand.transform.position;
                PartnerCrown.anchoredPosition += Together ? new Vector2(55f, -10f) : Vector2.zero;
                PartnerCrown.localScale = Ours.localScale * (Together ? 0.8f : 1f);
                Paint(PartnerCrown, Item.Goat, 1f);
                ++Shown;
            }
            else
            {
                PartnerCrown.localScale = Vector3.zero;
            }

            if (Look != null)
            {
                PartnerLook.position = Look.transform.position;
                PartnerLook.anchoredPosition += new Vector2(0f, 70f);
                PartnerLook.localScale = Ours.localScale * 0.5f;
                Paint(PartnerLook, Item.Goat, 0.6f);
            }
            else
            {
                PartnerLook.localScale = Vector3.zero;
            }
        }

        private sealed class Runner : MonoBehaviour
        {
            private void LateUpdate()
            {
                try
                {
                    UIAdventureMapOverlayController Map = MapSync.Shown;
                    SendOurs(Map);
                    if (Map == null)
                    {
                        Built = null;
                        return;
                    }
                    ShowTheirs(Map);
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogWarning($"[COOP] Map crowns failed: {e.Message}");
                    Built = null;
                }
            }
        }

        /* PATCHES ************************************************************/

        [HarmonyPatch(typeof(UIAdventureMapOverlayController), "OnNodeSelected")]
        [HarmonyPostfix]
        private static void Pointed(AdventureMapNode node)
        {
            Point Now = node?.MapNode?.point;
            if (Now == null || Now.Equals(Looking))
                return;
            Looking = Now;
            Changed = true;
        }
    }
}

/* EOF */
