/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Test harness: frames of the online menu, the lobby and the shared opening
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using System.Collections;
using System.IO;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Debug
{
    internal static class LobbyShots
    {
        /* Where the frames go, null for none */
        internal static string Dir;

        /// <summary>
        /// The game's own frame, named for the side and the moment
        /// </summary>
        internal static IEnumerator Take(string Name)
        {
            if (Dir == null)
                yield break;
            Directory.CreateDirectory(Dir);
            yield return new WaitForEndOfFrame();
            string Side = Plugin.GlobalsInternal.IsServerCreator ? "host" : "client";
            ScreenCapture.CaptureScreenshot(Path.Combine(Dir, $"{Side}-{Name}.png"));
            Plugin.Logger?.LogInfo($"[QA] Frame {Side}-{Name}");
        }
    }
}

/* EOF */
