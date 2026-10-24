/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Test harness: past the title screen without a key press
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using HarmonyLib;
using Unify;
using Unify.Input;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Debug
{
    /// <summary>
    /// The title screen waits for a button on any pad; a test copy in the background never gets one.
    /// Every other frame a while into the title, the first player's pad reads as pressed.
    /// </summary>
    [HarmonyPatch]
    internal static class PressStart
    {
        internal static bool On;

        [HarmonyPatch(typeof(RewiredInputManager), nameof(RewiredInputManager.GetGamePadWithAnyAction))]
        [HarmonyPostfix]
        private static void Pressed(ref GamePad __result)
        {
            if (!On || __result != GamePad.None || Time.realtimeSinceStartup < 20f || Time.frameCount % 2 == 0)
                return;
            /* Only on the title screen: once the menu shows, a press would pick its buttons */
            if (SessionManager.instance == null || SessionManager.instance.HasStarted || COTLMP.Ui.OnlineMenu.Shown)
                return;
            __result = new RewiredGamePad(0);
        }
    }
}

/* EOF */
