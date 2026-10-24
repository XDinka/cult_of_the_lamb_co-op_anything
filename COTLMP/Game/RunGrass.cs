/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     A run's grass and flowers come from the same save numbers in both games
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// What a run's grass spots look at in the save, taken once by whoever starts the run.
    /// </summary>
    /// <remarks>
    /// Each grass spot rolls flower or grass from the room (see <see cref="RoomRandom"/>), but the
    /// odds come from the save: the first three runs have plain grass in the first land, a quest
    /// for flowers (or few of them in the inventory) doubles their odds, and Sozo's story decides
    /// the mushrooms. Two games apart by one run or a handful of flowers grew different rooms. The
    /// player who starts the run sends those numbers with it (<see cref="CoopSync"/>), the partner
    /// takes the run with them, and both games read them in place of their own for the whole run.
    /// </remarks>
    internal static class RunGrass
    {
        internal sealed class Inputs
        {
            public int DungeonRun;
            public int SozoStoryProgress;
            /* The flower odds factor in each of the first four lands */
            public float[] Factors = new float[Lands.Length];
        }

        private static readonly FollowerLocation[] Lands =
        {
            FollowerLocation.Dungeon1_1, FollowerLocation.Dungeon1_2, FollowerLocation.Dungeon1_3, FollowerLocation.Dungeon1_4
        };

        private static readonly InventoryItem.ITEM_TYPE[] LandFlowers =
        {
            InventoryItem.ITEM_TYPE.FLOWER_RED, InventoryItem.ITEM_TYPE.MUSHROOM_SMALL, InventoryItem.ITEM_TYPE.CRYSTAL, InventoryItem.ITEM_TYPE.SPIDER_WEB
        };

        /* The odds factor with few of the land's flowers in the inventory, as RandomGrassPicker.CheckQuests has it */
        private static readonly float[] FewFactors = { 1.5f, 1.25f, 1.5f, 1.5f };

        /// <summary>
        /// The numbers of the run we are in, or null out of a shared run
        /// </summary>
        internal static Inputs Run;

        /* Counters for the test harness report, and the test's switch to see the rooms without this */
        internal static int SpotsGrown;
        internal static bool Off;

        /// <summary>
        /// Our save's numbers right now (just after the game counted the new run)
        /// </summary>
        internal static Inputs Capture()
        {
            DataManager Data = DataManager.Instance;
            Inputs Taken = new() { DungeonRun = Data.dungeonRun, SozoStoryProgress = Data.SozoStoryProgress };
            for (int i = 0; i < Lands.Length; ++i)
                Taken.Factors[i] = Factor(i);
            return Taken;
        }

        /// <summary>
        /// RandomGrassPicker.CheckQuests: the last quest decides, if it collects the land's flower
        /// the odds double, else having 10 or fewer of them raises the odds a little
        /// </summary>
        private static float Factor(int Land)
        {
            var Objectives = DataManager.Instance.Objectives;
            if (Objectives == null || Objectives.Count == 0)
                return 1f;
            ObjectivesData Last = Objectives[Objectives.Count - 1];
            if (Last != null && Last.Type == global::Objectives.TYPES.COLLECT_ITEM && ((Objectives_CollectItem)Last).ItemType == LandFlowers[Land])
                return 2f;
            return Inventory.GetItemQuantity(LandFlowers[Land]) <= 10 ? FewFactors[Land] : 1f;
        }

        internal static void Write(BinaryWriter Writer, Inputs Numbers)
        {
            Numbers ??= Capture();
            Writer.Write(Numbers.DungeonRun);
            Writer.Write(Numbers.SozoStoryProgress);
            foreach (float Value in Numbers.Factors)
                Writer.Write(Value);
        }

        internal static Inputs Read(BinaryReader Reader)
        {
            Inputs Numbers = new() { DungeonRun = Reader.ReadInt32(), SozoStoryProgress = Reader.ReadInt32() };
            for (int i = 0; i < Numbers.Factors.Length; ++i)
                Numbers.Factors[i] = Reader.ReadSingle();
            return Numbers;
        }

        /* PATCHES ************************************************************/

        [HarmonyPatch]
        private static class RunGrassPatches
        {
            private static readonly AccessTools.FieldRef<RandomGrassPicker, bool> CheckedQuests =
                AccessTools.FieldRefAccess<RandomGrassPicker, bool>("checkedQuests");

            /// <summary>
            /// A grass spot grows from the run's numbers: its quest odds are applied here in place
            /// of CheckQuests, and the save's run count and Sozo's story are the run's while it picks
            /// </summary>
            [HarmonyPatch(typeof(RandomGrassPicker), "UpdateGrass")]
            [HarmonyPrefix]
            private static void Before(RandomGrassPicker __instance, out int[] __state)
            {
                __state = null;
                Inputs Numbers = Off ? null : Run;
                if (Numbers == null || !CoopSync.InSession || __instance == null)
                    return;

                FollowerLocation Here = PlayerFarming.Location;
                if (!CheckedQuests(__instance))
                {
                    CheckedQuests(__instance) = true;
                    int Land = Array.IndexOf(Lands, Here);
                    if (Land >= 0)
                    {
                        FlowerTypes Flower = __instance.Flowers?.Find(Type => Type?.Location != null && Type.Location.Contains(Here));
                        if (Flower != null)
                            Flower.PercentageChanceToSpawn.y *= Numbers.Factors[Land];
                    }
                }

                DataManager Data = DataManager.Instance;
                __state = new[] { Data.dungeonRun, Data.SozoStoryProgress };
                Data.dungeonRun = Numbers.DungeonRun;
                Data.SozoStoryProgress = Numbers.SozoStoryProgress;
                ++SpotsGrown;
            }

            [HarmonyPatch(typeof(RandomGrassPicker), "UpdateGrass")]
            [HarmonyFinalizer]
            private static void After(RandomGrassPicker __instance, int[] __state)
            {
                if (__state != null)
                {
                    DataManager.Instance.dungeonRun = __state[0];
                    DataManager.Instance.SozoStoryProgress = __state[1];
                }
                if (CoopSync.InSession && __instance != null)
                    Count(!FoundOne(__instance) ? 0 : GrassPicked(__instance) ? 1 : 2);
            }

            private static readonly AccessTools.FieldRef<RandomGrassPicker, bool> FoundOne =
                AccessTools.FieldRefAccess<RandomGrassPicker, bool>("FoundOne");

            private static readonly AccessTools.FieldRef<RandomGrassPicker, bool> GrassPicked =
                AccessTools.FieldRefAccess<RandomGrassPicker, bool>("GrassPicked");
        }

        /* What grew in each room: plain grass, the land's grass, flowers (for the test harness) */
        private static readonly Dictionary<string, int[]> GrownIn = new();

        private static void Count(int Kind)
        {
            string Place = CoopSync.CurrentPlace() ?? "";
            if (!GrownIn.TryGetValue(Place, out int[] Counts))
            {
                if (GrownIn.Count > 64)
                    GrownIn.Clear();
                GrownIn[Place] = Counts = new int[3];
            }
            ++Counts[Kind];
        }

        /// <summary>
        /// What grew in the room we are in, as plain/grass/flowers
        /// </summary>
        internal static string Grown()
        {
            return GrownIn.TryGetValue(CoopSync.CurrentPlace() ?? "", out int[] Counts) ? $"{Counts[0]}/{Counts[1]}/{Counts[2]}" : "-";
        }
    }
}

/* EOF */
