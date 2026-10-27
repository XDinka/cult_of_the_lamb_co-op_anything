/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Globals data of the mod
 * COPYRIGHT:	Copyright 2025-2026 GeoB99 <geobman1999@gmail.com>
 */

/* IMPORTS ********************************************************************/

using COTLMP;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Data
{
    /// <summary>
    /// Main class of which mod data is stored.
    /// </summary>
    internal sealed class ModDataGlobals
    {
        /// <summary>
        /// Enable or Disable the execution of the mod.
        /// </summary>
        public bool EnableMod;

        /// <summary>
        /// The name of the player in-game
        /// </summary>
        public string PlayerName;

        public ModDataGlobals(bool Enable,
                              string PlName)
        {
            EnableMod = Enable;
            PlayerName = PlName;
        }
    }
}

/* EOF */
