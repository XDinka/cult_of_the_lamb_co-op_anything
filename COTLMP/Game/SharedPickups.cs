/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Pickups count for both players, like the inventory in local co-op
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP.Debug;
using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// Whatever one player picks up, both get.
    /// </summary>
    /// <remarks>
    /// In the game's own co-op the two lambs share one inventory. Here every player has their own
    /// save, so a pickup is added to both inventories. If the partner is in the same room, their
    /// copy of the item (the nearest pickup of the same type) disappears, so it isn't picked up twice.
    /// </remarks>
    internal static class SharedPickups
    {
        /// <summary>
        /// How far from where the partner picked it up our copy of an item may lie
        /// </summary>
        private const float MatchRadius = 3f;

        private static readonly FieldInfo CollectedField = AccessTools.Field(typeof(PickUp), "Collected");
        private static bool ApplyingRemote;

        /* Counters for the test harness report */
        internal static int Sent, Received, Removed;

        private static void OnPickedUp(PickUp Item)
        {
            if (ApplyingRemote || !CoopSync.InSession || Item == null || !Item.AddToInventory || Item.Quantity <= 0)
                return;

            /* Both players open their own copy of a chest, with the same contents */
            if (RoomRandom.IsChestDrop(Item))
                return;

            /* Only what our own lamb collected; the partner reports theirs */
            PlayerFarming Local = PlayerFarming.Instance;
            if (Local == null || (Item.Player != null && Item.Player != Local.gameObject))
                return;

            int Type = (int)Item.type, Quantity = Item.Quantity;
            Vector3 Position = Item.transform.position;
            string Place = CoopSync.CurrentPlace() ?? "";
            ++Sent;

            CoopSync.Send(CoopSync.Kind.PickedUp, Writer =>
            {
                Writer.Write(Place);
                Writer.Write(Type);
                Writer.Write(Quantity);
                Writer.Write(Position.x);
                Writer.Write(Position.y);
            });
        }

        /// <summary>
        /// The partner picked something up: add it to our inventory and take our copy off the floor.
        /// </summary>
        public static void OnRelay(uint From, BinaryReader Reader)
        {
            string Place = Reader.ReadString();
            int Type = Reader.ReadInt32();
            int Quantity = Reader.ReadInt32();
            Vector3 Position = new(Reader.ReadSingle(), Reader.ReadSingle(), 0f);

            ApplyingRemote = true;
            try
            {
                Inventory.AddItem(Type, Quantity);
                ++Received;

                if (Place == CoopSync.CurrentPlace())
                    RemoveNearest((InventoryItem.ITEM_TYPE)Type, Position);
            }
            catch (Exception e)
            {
                PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Adding the partner's pickup failed: {e.Message}");
            }
            finally
            {
                ApplyingRemote = false;
            }
        }

        private static void RemoveNearest(InventoryItem.ITEM_TYPE Type, Vector3 Position)
        {
            PickUp Best = null;
            float BestDistance = MatchRadius * MatchRadius;

            foreach (PickUp Item in PickUp.PickUps)
            {
                if (Item == null || Item.type != Type || !Item.gameObject.activeInHierarchy || (bool)CollectedField.GetValue(Item) || RoomRandom.IsChestDrop(Item))
                    continue;
                Vector3 Gap = Item.transform.position - Position;
                Gap.z = 0f;
                if (Gap.sqrMagnitude < BestDistance)
                {
                    BestDistance = Gap.sqrMagnitude;
                    Best = Item;
                }
            }

            if (Best == null)
                return;

            CollectedField.SetValue(Best, true);
            Best.gameObject.Recycle();
            ++Removed;
        }

        [HarmonyPatch]
        private static class SharedPickupsPatches
        {
            /// <summary>
            /// Runs before the pickup recycles itself, while its type, quantity and position are still set
            /// </summary>
            [HarmonyPatch(typeof(PickUp), nameof(PickUp.PickMeUp))]
            [HarmonyPrefix]
            private static void PickMeUp(PickUp __instance, out bool __state)
            {
                __state = __instance != null && !(bool)CollectedField.GetValue(__instance);
            }

            [HarmonyPatch(typeof(PickUp), nameof(PickUp.PickMeUp))]
            [HarmonyPostfix]
            private static void PickedUp(PickUp __instance, bool __state)
            {
                if (!__state)
                    return;
                try
                {
                    OnPickedUp(__instance);
                }
                catch (Exception e)
                {
                    PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Sharing a pickup failed: {e.Message}");
                }
            }
        }
    }
}

/* EOF */
