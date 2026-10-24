/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Trees, rocks and bushes in the cult: every hit shows on the other side as it lands
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP.Debug;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// Chopping, mining and picking in the cult, for the player who watches.
    /// </summary>
    /// <remarks>
    /// The cult's data goes once a second (see <see cref="CultSync"/>), which would show a tree going
    /// from whole to gone in one step. Hits on trees, rocks and berry bushes go as they land instead,
    /// a few times a second, and the watching side takes each one as the game does (the shake, the
    /// progress bar, the stump, the smoke) without the loot: that dropped for the player who hit.
    /// </remarks>
    [HarmonyPatch]
    internal static class CultResources
    {
        /* Hits not sent yet, by structure */
        private static readonly Dictionary<int, StructuresData> Pending = new();

        /* Our own replay of the other side's hits */
        private static bool Replaying;

        /* Counters for the test harness report */
        internal static int HitsSent, HitsApplied;

        private static bool Sharing => CultSync.IsReady && (CultSync.Mirror || CultSync.Hosting);

        public static void Initialize()
        {
            Plugin.MonoInstance.StartCoroutine(SendHits());
        }

        private static void Hit(StructureBrain Brain)
        {
            if (Replaying || !Sharing || Brain?.Data == null || Brain.Data.Location != FollowerLocation.Base)
                return;
            Pending[Brain.Data.ID] = Brain.Data;
        }

        [HarmonyPatch(typeof(Structures_Tree), nameof(Structures_Tree.TreeHit))]
        [HarmonyPostfix]
        private static void Chopped(Structures_Tree __instance) => Hit(__instance);

        [HarmonyPatch(typeof(Structures_BerryBush), nameof(Structures_BerryBush.PickBerries))]
        [HarmonyPostfix]
        private static void Picked(Structures_BerryBush __instance) => Hit(__instance);

        [HarmonyPatch(typeof(Structures_Rubble), nameof(Structures_Rubble.RemovalProgress), MethodType.Setter)]
        [HarmonyPostfix]
        private static void Mined(Structures_Rubble __instance) => Hit(__instance);

        private static IEnumerator SendHits()
        {
            var Wait = new WaitForSecondsRealtime(0.15f);
            while (true)
            {
                yield return Wait;
                if (Pending.Count == 0)
                    continue;
                if (!Sharing)
                {
                    Pending.Clear();
                    continue;
                }
                List<StructuresData> Hits = Pending.Values.ToList();
                Pending.Clear();
                HitsSent += Hits.Count;
                CoopSync.Send(CoopSync.Kind.CultHit, Writer =>
                {
                    Writer.Write((ushort)Hits.Count);
                    foreach (StructuresData Data in Hits)
                    {
                        Writer.Write(Data.ID);
                        Writer.Write(Data.Progress);
                    }
                });
            }
        }

        internal static void OnHits(BinaryReader Reader)
        {
            int Count = Reader.ReadUInt16();
            for (int Index = 0; Index < Count; Index++)
            {
                int Id = Reader.ReadInt32();
                float Progress = Reader.ReadSingle();
                if (Sharing && DataManager.Instance != null)
                    Apply(Id, Progress);
            }
        }

        private static void Apply(int Id, float Progress)
        {
            if (!StructureBrain.TryFindBrainByID(Id, out StructureBrain Brain) || Brain?.Data == null)
                return;
            float Delta = Progress - Brain.Data.Progress;
            if (Delta <= 0f)
                return;
            Replaying = true;
            try
            {
                switch (Brain)
                {
                    case Structures_Tree Tree:
                        Tree.TreeHit(Delta, dropLoot: false);
                        break;
                    case Structures_BerryBush Bush:
                        Bush.PickBerries(Delta, dropLoot: false);
                        break;
                    case Structures_Rubble Rock when Progress >= Rock.RemovalDurationInGameMinutes:
                        Break(Rock);
                        break;
                    case Structures_Rubble Rock:
                        Rock.RemovalProgress = Progress;
                        Rock.UpdateProgress();
                        break;
                    default:
                        return;
                }
                ++HitsApplied;
            }
            catch (Exception e)
            {
                PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Showing the partner's hit on {Brain.Data.Type} ({Id}) failed: {e.Message}");
            }
            finally
            {
                Replaying = false;
            }
            CultSync.Agree(Brain.Data);
        }

        /// <summary>
        /// A rock breaks as the game breaks it, but its stone was the other player's
        /// </summary>
        private static void Break(Structures_Rubble Rock)
        {
            Vector3 At = Rock.Data.Position;
            Rock.ForceRemoved = true;
            Rock.RemovalProgress = Rock.RemovalDurationInGameMinutes;
            AudioManager.Instance?.PlayOneShot(SoundConstants.GetBreakSoundPathForMaterial(SoundConstants.SoundMaterial.Stone), At);
            BiomeConstants.Instance?.EmitSmokeExplosionVFX(At);
        }

        /// <summary>
        /// The other side's tree or rock is gone before its last hit reached us: it goes as the game
        /// takes it away. True when that was ours to do.
        /// </summary>
        internal static bool Finish(StructureBrain Brain)
        {
            Replaying = true;
            try
            {
                switch (Brain)
                {
                    case Structures_Tree Tree when !Tree.TreeChopped:
                        Tree.TreeHit(Mathf.Max(1f, Tree.Data.ProgressTarget - Tree.Data.Progress), dropLoot: false);
                        return true;
                    case Structures_Rubble Rock when !Rock.ProgressFinished:
                        Break(Rock);
                        return true;
                    default:
                        return false;
                }
            }
            catch (Exception e)
            {
                PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Taking away the partner's {Brain.Data?.Type} failed: {e.Message}");
                return false;
            }
            finally
            {
                Replaying = false;
            }
        }
    }
}

/* EOF */
