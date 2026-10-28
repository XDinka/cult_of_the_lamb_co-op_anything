/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     The test harness: who picks up what in a run (-cotlmp-pickuptrace)
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using HarmonyLib;
using System;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Debug
{
    internal static partial class TestHarness
    {
        /* -cotlmp-pickuptrace: each pickup collected in a session, with whose lamb it flew to */
        internal static bool PickupTrace;
        internal static int PickedByUs, PickedByPuppet, PickedByNobody, AddedElsewhere;
        private static bool InPickMeUp;

        [HarmonyPatch]
        private static class PickupTracePatches
        {
            [HarmonyPatch(typeof(PickUp), nameof(PickUp.PickMeUp))]
            [HarmonyPrefix]
            private static void Picked(PickUp __instance)
            {
                if (!PickupTrace || __instance == null || !COTLMP.Game.CoopSync.InSession)
                    return;
                try
                {
                    if ((bool)AccessTools.Field(typeof(PickUp), "Collected").GetValue(__instance))
                        return;
                    GameObject By = __instance.Player;
                    PlayerFarming Farmer = By != null ? By.GetComponent<PlayerFarming>() : null;
                    string Who;
                    if (Farmer != null && Farmer == PlayerFarming.Instance)
                    {
                        Who = "our lamb";
                        ++PickedByUs;
                    }
                    else if (Farmer != null && COTLMP.Game.PlayerManager.IsPuppet(Farmer.health))
                    {
                        Who = "the partner's puppet";
                        ++PickedByPuppet;
                    }
                    else
                    {
                        Who = By == null ? "nobody" : By.name;
                        ++PickedByNobody;
                    }
                    InPickMeUp = true;
                    Plugin.Logger?.LogWarning($"{Tag} Pickup #{__instance.GetInstanceID()} {__instance.type} x{__instance.Quantity} (to inventory {__instance.AddToInventory}, chest {COTLMP.Game.RoomRandom.IsChestDrop(__instance)}) by {Who} " +
                                              $"in {COTLMP.Game.CoopSync.CurrentPlace()} at {Format(__instance.transform.position)}; " +
                                              $"so far ours {PickedByUs}, the puppet's {PickedByPuppet}, other {PickedByNobody}");
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogWarning($"{Tag} Pickup trace failed: {e.Message}");
                }
            }

            [HarmonyPatch(typeof(PickUp), nameof(PickUp.PickMeUp))]
            [HarmonyPostfix]
            private static void PickedDone() => InPickMeUp = false;

            /* Items that reach the inventory some other way than a pickup flying in (a room's reward, a script) */
            [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), new Type[] { typeof(int), typeof(int), typeof(bool) })]
            [HarmonyPrefix]
            private static void Added(int type, int quantity) => Elsewhere("added", type, quantity);

            [HarmonyPatch(typeof(Inventory), nameof(Inventory.ChangeItemQuantity), new Type[] { typeof(int), typeof(int), typeof(int) })]
            [HarmonyPrefix]
            private static void Changed(int type, int quantity) => Elsewhere("changed", type, quantity);

            private static void Elsewhere(string How, int Type, int Quantity)
            {
                if (!PickupTrace || InPickMeUp || Quantity == 0 || !COTLMP.Game.CoopSync.InSession || !LocationManager.IsDungeonActive())
                    return;
                ++AddedElsewhere;
                var Frames = new System.Diagnostics.StackTrace(2, false).GetFrames() ?? Array.Empty<System.Diagnostics.StackFrame>();
                string Callers = string.Join(" < ", System.Linq.Enumerable.Take(System.Linq.Enumerable.Select(System.Linq.Enumerable.Where(Frames,
                    Frame => Frame.GetMethod() != null && !Frame.GetMethod().Name.StartsWith("DMD<")), Frame => $"{Frame.GetMethod().DeclaringType?.Name}.{Frame.GetMethod().Name}"), 4));
                Plugin.Logger?.LogWarning($"{Tag} Inventory {How} {(InventoryItem.ITEM_TYPE)Type} {Quantity:+#;-#} not by a pickup in {COTLMP.Game.CoopSync.CurrentPlace()} from {Callers}");
            }
        }
    }
}

/* EOF */
