/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     A copy of the host's save before every game together
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using System;
using System.IO;
using System.Linq;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// The host's save, kept aside before a session.
    /// </summary>
    /// <remarks>
    /// Before the host plays a save with a friend (Play in the lobby on a save, or opening online from their
    /// own game) the slot's files as the game last wrote them (slot_N.mp, meta_N.mp and the friends'
    /// characters coop_players_N.dat) are copied to cotlmp-backups\slot_N\DATE-TIME next to the game's saves
    /// folder, not in it, so Steam Cloud doesn't carry them. The last five copies of a slot are kept. To go back,
    /// copy a copy's files into the saves folder over the slot's with the game closed.
    /// </remarks>
    internal static class SaveBackup
    {
        private const int Kept = 5;

        /* Counters for the test harness report */
        internal static int Taken;

        internal static string Root => Path.Combine(Application.persistentDataPath, "cotlmp-backups");

        /// <summary>
        /// Copies the slot's files aside; nothing if the slot has no save yet
        /// </summary>
        internal static void Take(int Slot, string Why)
        {
            try
            {
                string Saves = Path.Combine(Application.persistentDataPath, "saves");
                string[] Names = { $"slot_{Slot}.mp", $"meta_{Slot}.mp", $"coop_players_{Slot}.dat" };
                if (!File.Exists(Path.Combine(Saves, Names[0])))
                    return;
                string SlotDir = Path.Combine(Root, $"slot_{Slot}");
                string Dir = Path.Combine(SlotDir, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                Directory.CreateDirectory(Dir);
                foreach (string Name in Names)
                {
                    string From = Path.Combine(Saves, Name);
                    if (File.Exists(From))
                        File.Copy(From, Path.Combine(Dir, Name), true);
                }
                ++Taken;
                Plugin.Logger?.LogInfo($"[COOP] The save of slot {Slot} copied aside before {Why}: {Dir}");

                /* The oldest go (the folder names sort by time) */
                foreach (string Old in Directory.GetDirectories(SlotDir).OrderByDescending(Each => Each, StringComparer.Ordinal).Skip(Kept))
                    Directory.Delete(Old, true);
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Can't copy the save of slot {Slot} aside: {e.Message}");
            }
        }
    }
}

/* EOF */
