/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Which character each player looks like: the Lamb in their fleece, or the Goat
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using BepInEx.Configuration;
using HarmonyLib;
using Spine;
using System;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// Lets the two players tell each other apart, the way local co-op does with the Lamb and the Goat.
    /// </summary>
    /// <remarks>
    /// The player model has a skin per fleece ("Lamb_N") and one for the Goat. The game marks the Goat
    /// as fleece 1003, so that number travels in the handshake's skin field. "Auto" is the local co-op
    /// arrangement: the host is the Lamb in the fleece their save wears, whoever joins is the Goat.
    /// The choice changes how we look in our own game too, only while in a session; the save's fleece
    /// is never changed.
    /// </remarks>
    internal static class Appearance
    {
        public const int GoatSkin = 1003;
        public static readonly string[] Choices = { "Auto", "Lamb", "Goat" };

        private static ConfigEntry<string> Choice;

        /// <summary>
        /// Reads the setting and adds the selector to the mod's settings page.
        /// </summary>
        public static void Initialize()
        {
            Choice = Plugin.Config.Bind("Appearance", "Character", "Auto",
                                        new ConfigDescription("Auto: the host is the Lamb, a joining player is the Goat. Lamb uses the fleece your save wears.",
                                                              new AcceptableValueList<string>(Choices)));
            try
            {
                COTL_API.CustomSettings.CustomSettingsManager.AddHorizontalSelector(I2.Loc.MultiplayerModLocalization.UI.Settings.MultiplayerSettings_Title,
                                                                                    "Character",
                                                                                    Choice.Value,
                                                                                    Choices,
                                                                                    Index =>
                                                                                    {
                                                                                        Choice.Value = Choices[Math.Max(0, Math.Min(Index, Choices.Length - 1))];
                                                                                        Plugin.Config.Save();
                                                                                        Refresh();
                                                                                    });
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Can't add the Character setting: {e.Message}");
            }
        }

        /// <summary>
        /// True if we show as the Goat (in a session)
        /// </summary>
        public static bool WeAreGoat
        {
            get
            {
                string Value = Choice?.Value ?? "Auto";
                if (Value == "Goat")
                    return true;
                if (Value == "Lamb")
                    return false;
                return !Plugin.GlobalsInternal.IsServerCreator;
            }
        }

        /// <summary>
        /// The skin number other players get for us: our fleece, or 1003 for the Goat.
        /// </summary>
        public static int LocalSkin()
        {
            if (WeAreGoat)
                return GoatSkin;
            try
            {
                int Fleece = DataManager.Instance.PlayerVisualFleece;
                return Fleece < 0 ? 0 : Fleece;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// The base skin for a skin number, falling back to the plain Lamb.
        /// </summary>
        public static Skin FindSkin(SkeletonData Data, int Number)
        {
            return (Number == GoatSkin ? Data.FindSkin("Goat") : Data.FindSkin($"Lamb_{Number}")) ?? Data.FindSkin("Lamb_0");
        }

        /// <summary>
        /// Redraws our own lamb, after joining, leaving or changing the setting.
        /// </summary>
        public static void Refresh()
        {
            try
            {
                PlayerFarming.Instance?.SetSkin();
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Can't redraw the player: {e.Message}");
            }
        }

        [HarmonyPatch]
        private static class AppearancePatches
        {
            /// <summary>
            /// The game picks the Goat for fleece 1003, so show it that fleece while it builds our skin
            /// </summary>
            [HarmonyPatch(typeof(PlayerFarming), nameof(PlayerFarming.SetSkin), new[] { typeof(bool) })]
            [HarmonyPrefix]
            private static bool SetSkin(PlayerFarming __instance, ref int __state, ref Skin __result)
            {
                __state = int.MinValue;

                /* A puppet keeps the skin its player sent */
                if (PlayerManager.TryGetPuppetSkin(__instance, out int Puppet))
                {
                    __result = PlayerManager.ApplySkin(__instance, Puppet);
                    return false;
                }

                /* Both are the Lamb of the opening until the crown */
                if (!Plugin.GlobalsInternal.InGameSession || __instance != PlayerFarming.Instance || DataManager.Instance == null || IntroCoop.Apart)
                    return true;

                int Fleece = DataManager.Instance.PlayerVisualFleece;
                bool Goat = WeAreGoat;
                if (Goat == (Fleece == GoatSkin))
                    return true;

                __state = Fleece;
                DataManager.Instance.PlayerVisualFleece = Goat ? GoatSkin : 0;
                return true;
            }

            [HarmonyPatch(typeof(PlayerFarming), nameof(PlayerFarming.SetSkin), new[] { typeof(bool) })]
            [HarmonyFinalizer]
            private static void SetSkinDone(int __state)
            {
                /* Put the save's fleece back whatever happened */
                if (__state != int.MinValue && DataManager.Instance != null)
                    DataManager.Instance.PlayerVisualFleece = __state;
            }
        }
    }
}

/* EOF */
