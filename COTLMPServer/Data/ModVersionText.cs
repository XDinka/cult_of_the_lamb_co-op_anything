/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     What a mod version heard from a partner may be
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using System.Text.RegularExpressions;

/* CLASSES & CODE *************************************************************/

namespace COTLMPServer.Data
{
    /// <summary>
    /// A mod version as the partner tells it, before it goes anywhere near a release's address.
    /// </summary>
    /// <remarks>
    /// The partner's version names the release to take ("update to the host's version") and goes into
    /// GitHub's address and the installer's name: anything but digits and dots (a slash, "..") could point
    /// the update at another release. Only 1 to 4 numbers of up to 4 ASCII digits (not any script's digits), joined by dots, nothing after, 20 characters
    /// at most, are taken; anything else is an unknown version.
    /// </remarks>
    public static class ModVersionText
    {
        private static readonly Regex Shape = new Regex(@"^[0-9]{1,4}(\.[0-9]{1,4}){1,3}\z", RegexOptions.CultureInvariant);

        /// <summary>
        /// The partner's version can be taken as a release's version
        /// </summary>
        public static bool IsValid(string Version) => Version != null && Version.Length <= 20 && Shape.IsMatch(Version);
    }
}

/* EOF */
