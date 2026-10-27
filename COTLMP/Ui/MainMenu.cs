/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Main Menu UI management support
 * COPYRIGHT:	Copyright 2025 GeoB99 <geobman1999@gmail.com>
 */

/* IMPORTS ********************************************************************/

using COTLMP;
using COTLMP.Debug;
using HarmonyLib;
using BepInEx;
using I2.Loc;
using Lamb.UI;
using Lamb.UI.MainMenu;
using src.UI;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Ui
{
    /// <summary>
    /// Contains harmony patches of which hook up with the original
    /// game source code methods and data. Specifically, the patches
    /// serve to change the aspect of the main menu.
    /// </summary>
    internal static class Mainmenu
    {
        [HarmonyPatch]
        internal static class MainMenuPatches
        {
            /// <summary>
            /// Patches the private DLC on-click button private method, of which
            /// we hook up our Multiplayer dialog.
            /// </summary>
            /// <param name = "__instance">The type of the server password dialog box to be displayed.</param>
            /// <returns>Returns TRUE if tthe original method of the game is to be executed. FALSE if our method is to be executed instead.</returns>
            [HarmonyPatch(typeof(MainMenu), "OnDLCButtonClicked")]
            [HarmonyPrefix]
            private static bool OnMultiplayerButtonClickedPatch(MainMenu __instance)
            {
                /* The online menu: friends' lobbies and the host's own lobby */
                COTLMP.Debug.PrintLogger.PrintVerbose(DebugLevel.MESSAGE_LEVEL, DebugComponent.UI_COMPONENT, "The Multiplayer button has been clicked!");
                COTLMP.Ui.OnlineMenu.Open();
                return false;
            }

            /// <summary>
            /// Show a message when returning to main menu, when needed.
            /// </summary>
            [HarmonyPatch(typeof(MainMenu), "Start")]
            [HarmonyPostfix]
            private static void Start(MainMenu __instance)
            {
                if(PauseMenuPatches.Message != null)
                {
                    UIMenuConfirmationWindow Window = __instance.Push<UIMenuConfirmationWindow>(MonoSingleton<UIManager>.Instance.ConfirmationWindowTemplate);
                    Window.Configure(MultiplayerModLocalization.UI.Disconnected, PauseMenuPatches.Message, true);
                    COTLMP.Ui.LogButton.Add(Window);
                    PauseMenuPatches.Message = null;
                }

            }
        }
    }
}

/* EOF */
