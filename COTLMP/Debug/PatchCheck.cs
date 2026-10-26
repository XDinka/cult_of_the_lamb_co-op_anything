/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     The mod's patches go in one class at a time, and the ones a game update broke are told
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP.Game;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Debug
{
    /// <summary>
    /// Patching that survives a game update.
    /// </summary>
    /// <remarks>
    /// Harmony's PatchAll stops at the first patch whose method the game no longer has, and the mod
    /// doesn't start at all. Here every patch class goes in by itself: one that doesn't fit is logged
    /// and left out, the rest work, and the main menu tells the player (Ui.MainMenu). It is the same
    /// work PatchAll does, so starting the game takes no longer.
    /// </remarks>
    internal static class PatchCheck
    {
        /// <summary>
        /// The patch classes that didn't go in, with why
        /// </summary>
        internal static readonly List<string> Failed = new();

        /// <summary>
        /// The main menu told the player already
        /// </summary>
        internal static bool Told;

        /* Counters for the test harness report */
        internal static int Classes, PartnerFailed = -1;

        /// <summary>
        /// Entering a session: ours to the partner, and a note to ourselves if some didn't go in
        /// </summary>
        internal static void Announce()
        {
            CoopSync.Send(CoopSync.Kind.PatchReport, Writer => Writer.Write((byte)Math.Min(255, Failed.Count)));
            if (Failed.Count > 0)
                Notify("Multiplayer/UI/PatchesOurs", Failed.Count.ToString());
        }

        /// <summary>
        /// The partner's: a note to us if some of theirs didn't go in
        /// </summary>
        internal static void OnReport(BinaryReader Reader)
        {
            int Theirs = Reader.ReadByte();
            bool First = PartnerFailed != Theirs;
            PartnerFailed = Theirs;
            Plugin.Logger?.LogInfo($"[COOP] The partner's mod: {Theirs} patches left out by their game version, ours {Failed.Count}");
            if (Theirs > 0 && First)
                Notify("Multiplayer/UI/PatchesPartner", COTLMP.Network.SteamBridge.PartnerNames().FirstOrDefault() ?? "", Theirs.ToString());
        }

        private static void Notify(string Key, params string[] Values)
        {
            try
            {
                NotificationCentre.Instance?.PlayGenericNotificationNonLocalizedParams(Key, Values);
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Can't show the patches note: {e.Message}");
            }
        }

        /// <summary>
        /// Into the log, once it is there (patching comes first)
        /// </summary>
        internal static void Report()
        {
            foreach (string Line in Failed)
                Plugin.Logger?.LogError($"[COOP] A patch doesn't fit this game version, left out: {Line}");
            Plugin.Logger?.LogInfo($"[COOP] {Classes} patch classes in, {Failed.Count} left out");
        }

        internal static Harmony Apply(Assembly Mod)
        {
            /* A name of its own each time: the old mod's and a newer one's patches meet during an update in the game */
            Harmony Instance = new($"harmony-auto-{Guid.NewGuid()}");
            Failed.Clear();
            Classes = 0;
            foreach (Type Item in AccessTools.GetTypesFromAssembly(Mod))
            {
                try
                {
                    if (Instance.CreateClassProcessor(Item).Patch() != null)
                        ++Classes;
                }
                catch (Exception e)
                {
                    Exception Inner = e.InnerException ?? e;
                    Failed.Add($"{Item.FullName}: {Inner.Message}");
                }
            }
            return Instance;
        }

        /// <summary>
        /// Test switch -cotlmp-badpatch: a patch of a method the game hasn't got, as after a game update
        /// </summary>
        [HarmonyPatch]
        private static class BadPatch
        {
            private static bool Prepare() => Array.Exists(Environment.GetCommandLineArgs(), Arg => Arg.Equals("-cotlmp-badpatch", StringComparison.OrdinalIgnoreCase));

            private static MethodBase TargetMethod() => AccessTools.Method(typeof(PlayerFarming), "NoSuchMethodInThisGame");

            private static void Prefix()
            {
            }
        }
    }
}

/* EOF */
