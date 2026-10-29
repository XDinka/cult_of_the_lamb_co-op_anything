/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     DLCs patches
 * COPYRIGHT:	Copyright 2026 GeoB99 <geobman1999@gmail.com>
 */

/* IMPORTS ********************************************************************/

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// The DLCs stay the game's own business: whoever owns one keeps it, in their own game and in a session.
    /// </summary>
    /// <remarks>
    /// Each time a game scene starts the game asks Steam about every DLC the save has and takes away from
    /// the save the ones the player doesn't own (GameManager.CheckDLCStatus: DataManager.Deactivate*DLC drop
    /// the decorations, the skins, the Woolhaven land, the followers' marriages...). The mod once answered
    /// "not owned" for all of them, so a DLC owner's save lost all that as soon as it loaded, and for good
    /// at the next save. Now the game asks Steam itself; only a friend playing in the host's game (whose
    /// save is the host's, sent over) never changes the host's DLCs by their own Steam account, neither
    /// taking them away nor adding the friend's own: what the friend's game changes in the cult goes back
    /// to the host's save.
    /// </remarks>
    [HarmonyPatch]
    internal static class DlcPatches
    {
        private static readonly string[] Dlcs = { "Heretic", "Cultist", "Sinful", "Pilgrim", "PrePurchase" };

        /* The game's methods by name, those this game version has (an update may drop or rename one) */
        private static IEnumerable<MethodBase> Methods(Type Of, IEnumerable<string> Names)
        {
            foreach (string Name in Names)
            {
                MethodBase Method = AccessTools.Method(Of, Name);
                if (Method != null)
                    yield return Method;
            }
        }

        private static IEnumerable<MethodBase> TargetMethods() =>
            Methods(typeof(DataManager), Dlcs.Select(Dlc => $"Deactivate{Dlc}DLC").Append("DeactivateWoolhavenDLC"));

        [HarmonyPrefix]
        private static bool KeepTheHostsDlc(MethodBase __originalMethod)
        {
            if (!HostSave.Playing)
                return true;
            Plugin.Logger?.LogInfo($"[COOP] Not owned here, kept as the host's save has it: {__originalMethod.Name} left out");
            return false;
        }

        /// <summary>
        /// The friend's own DLCs don't go into the host's save either (the game adds an owned DLC's things on load)
        /// </summary>
        [HarmonyPatch]
        private static class KeepTheHostsLack
        {
            private static IEnumerable<MethodBase> TargetMethods() =>
                Methods(typeof(DataManager), Dlcs.Select(Dlc => $"Activate{Dlc}DLC").Append("ActivateMajorDLC"));

            [HarmonyPrefix]
            private static bool Skip(MethodBase __originalMethod, ref bool __result)
            {
                if (!HostSave.Playing)
                    return true;
                Plugin.Logger?.LogInfo($"[COOP] Owned here, but the host's save is kept as it is: {__originalMethod.Name} left out");
                __result = false;
                return false;
            }
        }
    }
}

/* EOF */
