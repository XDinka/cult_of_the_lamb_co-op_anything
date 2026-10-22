/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Chests, podiums, tarot and other room events roll the same for both players
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP.Debug;
using HarmonyLib;
using MMBiomeGeneration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// Makes a room's events come out the same in both games, while each player goes through
    /// them on their own.
    /// </summary>
    /// <remarks>
    /// Chest type and contents, weapon and relic podiums, tarot draws and whatever an interaction
    /// rolls when used all come from UnityEngine.Random, whose state differs between the two games.
    /// Right before each of those rolls the state is set from the room's seed, the run and what is
    /// being rolled (with a count, so the second draw differs from the first). Both players then
    /// get the same chest, the same weapons to choose from, the same cards, and each one opens,
    /// picks or skips in their own time. What a chest drops stays with whoever opened it: both
    /// open their own copy, so sharing those pickups would give everything twice.
    /// </remarks>
    internal static class RoomRandom
    {
        private static readonly Harmony Coroutines = new("cotlmp.roomrandom");
        private static readonly Dictionary<string, int> Counts = new();
        private static readonly ConditionalWeakTable<object, StrongBox<int>> Steps = new();

        /* Chest drops stay local, see SharedPickups */
        private static readonly HashSet<PickUp> ChestDrops = new();
        private static Vector3 ChestAt;
        private static float ChestOpenedAt = -100f;

        /* Counters for the test harness report */
        internal static int Seeded;

        public static void Initialize()
        {
            BiomeGenerator.OnBiomeChangeRoom += () =>
            {
                Counts.Clear();
                ChestDrops.Clear();
            };
            PatchChestCoroutines();
        }

        private static int Hash(string Text)
        {
            unchecked
            {
                uint Value = 2166136261;
                foreach (char C in Text)
                    Value = (Value ^ C) * 16777619;
                return (int)Value;
            }
        }

        /// <summary>
        /// Sets UnityEngine.Random from the room and what is about to be rolled, the n-th time it is rolled here
        /// </summary>
        private static void Seed(string What, int? Step = null)
        {
            if (!CoopSync.InSession)
                return;
            BiomeRoom Room = BiomeGenerator.Instance?.CurrentRoom;
            if (Room == null)
                return;

            int Count;
            if (Step.HasValue)
            {
                Count = Step.Value;
            }
            else
            {
                Counts.TryGetValue(What, out Count);
                Counts[What] = Count + 1;
            }

            unchecked
            {
                UnityEngine.Random.InitState(Room.Seed * 31 + CoopSync.CurrentRun * 17 + Hash(What) * 7 + Count);
            }
            ++Seeded;
        }

        private static string At(Component Thing) =>
            $"{Mathf.RoundToInt(Thing.transform.position.x * 2f)},{Mathf.RoundToInt(Thing.transform.position.y * 2f)}";

        /// <summary>
        /// True for an item that came out of our own chest
        /// </summary>
        internal static bool IsChestDrop(PickUp Item) => Item != null && ChestDrops.Contains(Item);

        /// <summary>
        /// The chest's reward coroutines roll as they go: set the state on each step of each of them
        /// </summary>
        private static void PatchChestCoroutines()
        {
            try
            {
                HarmonyMethod Prefix = new(typeof(RoomRandom).GetMethod(nameof(ChestStep), BindingFlags.NonPublic | BindingFlags.Static));
                foreach (MethodInfo Method in typeof(Interaction_Chest).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (Method.ReturnType != typeof(IEnumerator) || !Method.Name.Contains("Reward"))
                        continue;
                    MethodInfo MoveNext = AccessTools.EnumeratorMoveNext(Method);
                    if (MoveNext != null)
                        Coroutines.Patch(MoveNext, prefix: Prefix);
                }
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Can't hook the chest rewards: {e.Message}");
            }
        }

        private static void ChestStep(object __instance)
        {
            StrongBox<int> Step = Steps.GetOrCreateValue(__instance);
            Seed("chest-step:" + __instance.GetType().Name, Step.Value++);
        }

        [HarmonyPatch]
        private static class RoomRandomPatches
        {
            [HarmonyPatch(typeof(Interaction_Chest), "SetTypeOfChest")]
            [HarmonyPrefix]
            private static void ChestType() => Seed("chest-type");

            [HarmonyPatch(typeof(Interaction_Chest), nameof(Interaction_Chest.Reveal))]
            [HarmonyPrefix]
            private static void ChestReveal(Interaction_Chest __instance)
            {
                Seed("chest-reveal");
                ChestAt = __instance.transform.position;
                ChestOpenedAt = Time.time;
            }

            [HarmonyPatch(typeof(Interaction_Chest), "SelectReward")]
            [HarmonyPrefix]
            private static void ChestReward() => Seed("chest-reward");

            [HarmonyPatch(typeof(Interaction_Chest), "SelectReward")]
            [HarmonyPostfix]
            private static void ChestRewardPicked(Interaction_Chest __instance)
            {
                if (CoopSync.InSession)
                    Plugin.Logger?.LogInfo($"[COOP] Chest in {CoopSync.CurrentPlace()}: {Traverse.Create(__instance).Field("TypeOfChest").GetValue()} with {Traverse.Create(__instance).Field("pickedReward").GetValue()}");
            }

            [HarmonyPatch(typeof(Interaction_WeaponSelectionPodium), nameof(Interaction_WeaponSelectionPodium.OnEnableInteraction))]
            [HarmonyPrefix]
            private static void Podium(Interaction_WeaponSelectionPodium __instance) => Seed("podium@" + At(__instance));

            [HarmonyPatch(typeof(TarotCards), nameof(TarotCards.DrawRandomCard))]
            [HarmonyPrefix]
            private static void Tarot() => Seed("tarot");

            [HarmonyPatch(typeof(Interaction), nameof(Interaction.OnInteract))]
            [HarmonyPrefix]
            private static void Interact(Interaction __instance)
            {
                if (__instance != null)
                    Seed("interact:" + __instance.GetType().Name + "@" + At(__instance));
            }

            /// <summary>
            /// What lands next to our chest while it gives out its reward is the chest's
            /// </summary>
            [HarmonyPatch(typeof(InventoryItem), nameof(InventoryItem.Spawn), new[] { typeof(InventoryItem.ITEM_TYPE), typeof(int), typeof(Vector3), typeof(float), typeof(Action<PickUp>) })]
            [HarmonyPostfix]
            private static void Spawned(PickUp __result, Vector3 position)
            {
                if (__result == null || Time.time - ChestOpenedAt > 15f)
                    return;
                Vector3 Gap = position - ChestAt;
                Gap.z = 0f;
                if (Gap.magnitude < 2.5f && ChestDrops.Add(__result) && CoopSync.InSession)
                    Plugin.Logger?.LogInfo($"[COOP] Chest drop {__result.type} x{__result.Quantity}");
            }
        }
    }
}

/* EOF */
