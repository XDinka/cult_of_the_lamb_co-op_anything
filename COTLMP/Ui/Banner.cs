/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Banner "Multiplayer Edition" header
 * COPYRIGHT:	Copyright 2025 GeoB99 <geobman1999@gmail.com>
 */

/* IMPORTS ********************************************************************/

using COTLMP;
using COTLMP.Data;
using HarmonyLib;
using BepInEx;
using I2.Loc;
using Lamb.UI;
using src.Extensions;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Ui
{
    /// <summary>
    /// Main class of which it implements the banner header of the mod.
    /// </summary>
    [HarmonyPatch]
    internal static class Banner
    {
        /// <summary>
        /// A coroutine that forces the loading of game assets into memory.
        /// This is needed because joining a game server from the serverlist
        /// makes the game not loading the assets due to a peculiar behavior of
        /// SceneLoad() of the game.
        ///
        /// It first tries to check the current active scene to unload and based
        /// on that it loads whatever game asset depending on that current scene.
        /// The game assets are loaded ONLY if the previous active scene was Main
        /// Menu and the serverlist scene isn't named like that. Consequently,
        /// the game was started with unintialized assets.
        /// </summary>
        private static IEnumerator BannerEnumerator()
        {
            yield return MonoSingleton<UIManager>.Instance.LoadPersistentGameAssets().YieldUntilCompleted();
        }

        /// <summary>
        /// Patches the private DLC check edition method, of which it replaces the banner header with ours.
        /// </summary>
        /// <param name = "__instance">The current instance value of the method being patched.</param>
        /// <param name = "__result">The current result value being returned. Typically the original method returns a IEnumerator.</param>
        /// <returns>Returns TRUE if tthe original method of the game is to be executed. FALSE if our method is to be executed instead.</returns>
        [HarmonyPatch(typeof(ShowIfSpecialEdition), "WaitForDLCCheck")]
        [HarmonyPrefix]
        private static bool BannerEditionPatch(ShowIfSpecialEdition __instance, ref IEnumerator __result)
        {
            /* Replace the banner header */
            __instance._localize.Term = "UI/Banner";
            __instance._text.text = MultiplayerModLocalization.UI.Multiplayer_Banner;
            __result = BannerEnumerator();
            try
            {
                Title(__instance.transform as RectTransform);
            }
            catch (System.Exception e)
            {
                Plugin.Logger?.LogWarning($"[LOBBY] Can't put the mod's title in the main menu: {e.Message}");
            }
            return false;
        }

        private static Sprite TitleSprite;

        /// <summary>
        /// The title screen ("Press any button" under the game's logo): the mod's title there too, the line under it
        /// </summary>
        [HarmonyPatch(typeof(Lamb.UI.MainMenu.UIMainMenuController), nameof(Lamb.UI.MainMenu.UIMainMenuController.Start))]
        [HarmonyPostfix]
        private static void TitleScreen() => Plugin.MonoInstance.StartCoroutine(FindTitle());

        /* The title screen comes a moment after the menu starts */
        private static IEnumerator FindTitle()
        {
            for (int Tries = 0; Tries < 40; Tries++)
            {
                RectTransform Found = null;
                try
                {
                    foreach (RectTransform Item in Object.FindObjectsOfType<RectTransform>(true))
                    {
                        if (Item.name == "AnyButtonContainer" && Item.parent != null && Item.parent.parent != null && Item.parent.parent.name == "StartingLogo")
                        {
                            Found = Item;
                            break;
                        }
                    }
                    if (Found != null)
                    {
                        Place(Found, 0.7f, 676f, 10f);
                        yield break;
                    }
                }
                catch (System.Exception e)
                {
                    Plugin.Logger?.LogWarning($"[LOBBY] Can't put the mod's title on the title screen: {e.Message}");
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.25f);
            }
        }

        private static Sprite LoadSprite()
        {
            if (TitleSprite != null)
                return TitleSprite;
            using Stream Source = typeof(Banner).Assembly.GetManifestResourceStream("coop-anything.png");
            if (Source == null)
                return null;
            using MemoryStream Bytes = new();
            Source.CopyTo(Bytes);
            Texture2D Picture = new(2, 2, TextureFormat.RGBA32, false);
            ImageConversion.LoadImage(Picture, Bytes.ToArray());
            Picture.filterMode = FilterMode.Bilinear;
            TitleSprite = Sprite.Create(Picture, new Rect(0f, 0f, Picture.width, Picture.height), new Vector2(0.5f, 0.5f), 100f);
            return TitleSprite;
        }

        /// <summary>
        /// The mod's title where this line is, the line moved down under it
        /// </summary>
        private static void Place(RectTransform Line, float Share, float Fallback, float Gap)
        {
            if (Line == null || Line.parent == null || Line.parent.Find("COTLMP Co-op Anything") != null || LoadSprite() == null)
                return;
            GameObject Holder = new("COTLMP Co-op Anything", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            RectTransform Rect = (RectTransform)Holder.transform;
            Rect.SetParent(Line.parent, false);
            Rect.SetSiblingIndex(Line.GetSiblingIndex());
            Rect.anchorMin = Line.anchorMin;
            Rect.anchorMax = Line.anchorMax;
            Rect.pivot = Line.pivot;
            Image Picture = Holder.GetComponent<Image>();
            Picture.sprite = TitleSprite;
            Picture.preserveAspect = true;
            Picture.raycastTarget = false;

            float Width = Fallback * Share;
            float Height = Width * TitleSprite.rect.height / TitleSprite.rect.width;
            Rect.sizeDelta = new Vector2(Width, Height);
            LayoutElement Layout = Holder.GetComponent<LayoutElement>();
            Layout.preferredWidth = Width;
            Layout.preferredHeight = Height;
            Rect.anchoredPosition = Line.anchoredPosition;
            Line.anchoredPosition -= new Vector2(0f, Height + Gap);
            Plugin.Logger?.LogInfo($"[LOBBY] The mod's title over {Line.name} ({Width:F0} x {Height:F0})");
        }

        /// <summary>
        /// "x CO-OP ANYTHING" under the game's logo, as on the mod's cover; the multiplayer version's line goes under it
        /// </summary>
        private static void Title(RectTransform Line)
        {
            /* As wide as the logo's line, its own height; the version's line under it */
            float Width = Line != null && Line.rect.width > 50f ? Mathf.Min(Line.rect.width, 520f) : 467f;
            Place(Line, 0.9f, Width, 4f);
        }
    }
}

/* EOF */
