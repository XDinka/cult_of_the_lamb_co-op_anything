/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Rooms one player opened on a dungeon floor show on the other's minimap
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using MMBiomeGeneration;
using System;
using System.Collections.Generic;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// Shares the explored rooms of a floor.
    /// </summary>
    /// <remarks>
    /// Each player walks the floor on their own. A room is marked visited on the minimap once
    /// someone walks into it: wherever the partner goes (RoomSync tells us) is marked on ours as if
    /// we had been there, so both see the same opened rooms, and the doors to them lose their
    /// "not visited" mark. Places are kept per run, so a floor we come to after the partner shows
    /// what they opened on it.
    /// </remarks>
    internal static class MiniMapSync
    {
        private static int Run;
        private static readonly HashSet<string> PartnerPlaces = new();

        /* Counters for the test harness report */
        internal static int Marked;

        public static void Initialize()
        {
            BiomeGenerator.OnBiomeChangeRoom += () => Mark(false);
        }

        /// <summary>
        /// The partner walked into a place
        /// </summary>
        internal static void PartnerAt(string Place)
        {
            if (string.IsNullOrEmpty(Place) || !Place.Contains("@") || CoopSync.CurrentRun == 0)
                return;
            if (Run != CoopSync.CurrentRun)
            {
                Run = CoopSync.CurrentRun;
                PartnerPlaces.Clear();
            }
            if (PartnerPlaces.Add(Place))
                Mark(true);
        }

        /// <summary>
        /// Marks the rooms of our floor the partner has been in
        /// </summary>
        private static void Mark(bool Refresh)
        {
            try
            {
                if (!CoopSync.InSession || Run != CoopSync.CurrentRun || BiomeGenerator.Instance == null)
                    return;
                string Here = CoopSync.CurrentPlace();
                if (Here == null || !Here.Contains("@"))
                    return;
                string Floor = Here.Substring(0, Here.IndexOf('@') + 1);

                bool Changed = false;
                foreach (string Place in PartnerPlaces)
                {
                    if (!Place.StartsWith(Floor, StringComparison.Ordinal))
                        continue;
                    string[] At = Place.Substring(Floor.Length).Split(',');
                    if (At.Length != 2 || !int.TryParse(At[0], out int X) || !int.TryParse(At[1], out int Y))
                        continue;
                    BiomeRoom Room = BiomeRoom.GetRoom(X, Y);
                    if (Room == null || Room.Visited)
                        continue;
                    Room.Discovered = true;
                    Room.Visited = true;
                    Changed = true;
                    ++Marked;
                }
                if (Changed && Refresh)
                    MiniMap.Instance?.OnChangeRoom();
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Marking the partner's rooms failed: {e.Message}");
            }
        }
    }
}

/* EOF */
