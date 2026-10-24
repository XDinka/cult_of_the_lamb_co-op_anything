/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     What the partner's hits knock out flies to the partner on our screen too
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// The loot of something the partner broke, as we watch it break.
    /// </summary>
    /// <remarks>
    /// When our game plays the partner's hit (a pot or grass in a dungeon, a tree, a berry bush or a
    /// rock in the cult) the loot comes out as it would, bounces, and then flies to the partner's
    /// lamb as it flies to whoever is picking it up. It is only a picture: it never reaches our
    /// inventory or the shared pickups (the partner's own game gives it, and shares it if it is to be
    /// shared). With no puppet of the partner here, nothing comes out, as before.
    /// </remarks>
    [HarmonyPatch]
    internal static class GhostLoot
    {
        /* How long a ghost may fly before it goes anyway */
        private const float Lifetime = 6f;

        private static int Scope;
        private static GameObject Target;
        /* The ghosts and what they were before (pickups come from a pool and go back to it) */
        private sealed class Was
        {
            public bool AddToInventory, PlayerOverride, CanStopFollowingPlayer, CanBeStolenByCritter;
            public float MagnetDistance;
            public UnityEvent Callback;
        }

        private static readonly Dictionary<PickUp, Was> Ghosts = new();

        /* Counters for the test harness report */
        internal static int Made;

        /// <summary>
        /// The partner's lamb on our screen, or null when it isn't here
        /// </summary>
        internal static GameObject PartnerHere()
        {
            for (uint Id = 0; Id < Data.InternalData.MaxPlayersPerServerInternal; Id++)
            {
                PlayerFarming Puppet = PlayerManager.GetPuppet(Id);
                if (Puppet != null && Puppet.gameObject.activeInHierarchy)
                    return Puppet.gameObject;
            }
            return null;
        }

        /// <summary>
        /// Loot spawned until the matching End flies to that lamb
        /// </summary>
        internal static void Begin(GameObject To)
        {
            if (Scope++ == 0)
                Target = To;
        }

        internal static void End()
        {
            if (Scope > 0 && --Scope == 0)
                Target = null;
        }

        internal static bool IsGhost(PickUp Item) => Item != null && Ghosts.ContainsKey(Item);

        private static void Haunt(PickUp Item, GameObject To)
        {
            if (Item == null || To == null || Ghosts.ContainsKey(Item))
                return;
            Ghosts[Item] = new Was
            {
                AddToInventory = Item.AddToInventory, PlayerOverride = Item.PlayerOverride, CanStopFollowingPlayer = Item.CanStopFollowingPlayer,
                CanBeStolenByCritter = Item.CanBeStolenByCritter, MagnetDistance = Item.MagnetDistance, Callback = Item.Callback
            };
            Item.AddToInventory = false;
            Item.Player = To;
            Item.PlayerOverride = true;
            Item.CanStopFollowingPlayer = false;
            Item.CanBeStolenByCritter = false;
            Item.MagnetDistance = 1000f;
            Item.Callback = new UnityEvent();
            ++Made;
            Plugin.MonoInstance.StartCoroutine(Expire(Item));
        }

        private static IEnumerator Expire(PickUp Item)
        {
            yield return new WaitForSeconds(Lifetime);
            if (Item != null && Ghosts.ContainsKey(Item) && Item.gameObject.activeInHierarchy)
                Item.gameObject.Recycle();
            Restore(Item);
        }

        private static void Restore(PickUp Item)
        {
            if (Item == null || !Ghosts.TryGetValue(Item, out Was Before))
                return;
            Ghosts.Remove(Item);
            Item.AddToInventory = Before.AddToInventory;
            Item.PlayerOverride = Before.PlayerOverride;
            Item.CanStopFollowingPlayer = Before.CanStopFollowingPlayer;
            Item.CanBeStolenByCritter = Before.CanBeStolenByCritter;
            Item.MagnetDistance = Before.MagnetDistance;
            Item.Callback = Before.Callback;
        }

        /* PATCHES ************************************************************/

        /// <summary>
        /// The pool hands the pickup over a moment later: the ghost is made then
        /// </summary>
        [HarmonyPatch(typeof(InventoryItem), nameof(InventoryItem.Spawn), new[] { typeof(InventoryItem.ITEM_TYPE), typeof(int), typeof(Vector3), typeof(float), typeof(Action<PickUp>) })]
        [HarmonyPrefix]
        private static void Spawn(ref Action<PickUp> result)
        {
            if (Scope == 0 || Target == null)
                return;
            GameObject To = Target;
            Action<PickUp> Then = result;
            result = Item =>
            {
                Haunt(Item, To);
                Then?.Invoke(Item);
            };
        }

        /// <summary>
        /// Devotion orbs fly to whoever earned them and count on the way: the partner's are theirs
        /// </summary>
        [HarmonyPatch(typeof(InventoryItem), nameof(InventoryItem.SpawnBlackSoul))]
        [HarmonyPrefix]
        private static bool BlackSoul() => Scope == 0;

        /// <summary>
        /// A ghost reaching the partner is gone, the game's pickup effect is all it leaves
        /// </summary>
        [HarmonyPatch(typeof(PickUp), "OnDisable")]
        [HarmonyPostfix]
        private static void Recycled(PickUp __instance) => Restore(__instance);
    }
}

/* EOF */
