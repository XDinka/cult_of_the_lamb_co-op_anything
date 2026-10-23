/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     The tarot reader deals both players the same cards
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// Makes a tarot reading in a dungeon offer both players the same two cards.
    /// </summary>
    /// <remarks>
    /// A seeded roll isn't enough here: the cards come from what the player's save has found,
    /// minus what they hold or met this run, minus cards their fleece or the season rules out,
    /// and a save that never had a reading is dealt the Lovers first. So whoever draws first at
    /// a reader sends the cards, and the other player's reading deals those instead. A card the
    /// other player already holds is drawn again from their own deck.
    /// </remarks>
    internal static class SharedTarot
    {
        /* The cards dealt at each reader, by run, place, reader and card number */
        private static readonly Dictionary<string, (int Card, int Level)> Dealt = new();
        private static readonly ConditionalWeakTable<Interaction_Tarot, StrongBox<int>> Counts = new();

        /* Counters for the test harness report */
        internal static int Sent, Received, Applied;

        private static string Key(Interaction_Tarot Reader, int Number) =>
            $"{CoopSync.CurrentRun}|{CoopSync.CurrentPlace()}|{Mathf.RoundToInt(Reader.transform.position.x * 2f)},{Mathf.RoundToInt(Reader.transform.position.y * 2f)}|{Number}";

        /// <summary>
        /// The partner's cards at a reader.
        /// </summary>
        public static void OnRelay(BinaryReader Reader)
        {
            string Id = Reader.ReadString();
            int Card = Reader.ReadInt32(), Level = Reader.ReadInt32();
            ++Received;
            if (!Dealt.ContainsKey(Id))
                Dealt[Id] = (Card, Level);
            Plugin.Logger?.LogInfo($"[COOP] The partner was dealt {(TarotCards.Card)Card} {Level} at {Id}");
        }

        [HarmonyPatch]
        private static class SharedTarotPatches
        {
            [HarmonyPatch(typeof(Interaction_Tarot), "GetCard")]
            [HarmonyPrefix]
            private static bool GetCard(Interaction_Tarot __instance, ref TarotCards.TarotCard __result, out string __state)
            {
                __state = null;
                if (!CoopSync.InSession || CoopSync.CurrentRun == 0)
                    return true;

                StrongBox<int> Count = Counts.GetOrCreateValue(__instance);
                string Id = Key(__instance, Count.Value++);
                __state = Id;
                if (!Dealt.TryGetValue(Id, out var Card))
                    return true;

                PlayerFarming Player = __instance.playerFarming;
                TarotCards.TarotCard Shared = new((TarotCards.Card)Card.Card, Card.Level);
                if (Player != null && TrinketManager.HasTrinket(Shared.CardType, Player))
                {
                    Plugin.Logger?.LogInfo($"[COOP] We already hold {Shared.CardType}, drawing our own");
                    return true;
                }

                if (Player != null)
                    TrinketManager.AddEncounteredTrinket(Shared, Player);
                /* Our first reading is this one: no Lovers later */
                DataManager.Instance.FirstTarot = true;
                __result = Shared;
                ++Applied;
                Plugin.Logger?.LogInfo($"[COOP] Dealt the partner's {Shared.CardType} {Shared.UpgradeIndex} at {Id}");
                return false;
            }

            [HarmonyPatch(typeof(Interaction_Tarot), "GetCard")]
            [HarmonyPostfix]
            private static void GotCard(TarotCards.TarotCard __result, string __state)
            {
                if (__state == null || __result == null || Dealt.ContainsKey(__state))
                    return;
                Dealt[__state] = ((int)__result.CardType, __result.UpgradeIndex);
                try
                {
                    CoopSync.Send(CoopSync.Kind.TarotDraw, Writer =>
                    {
                        Writer.Write(__state);
                        Writer.Write((int)__result.CardType);
                        Writer.Write(__result.UpgradeIndex);
                    });
                    ++Sent;
                    Plugin.Logger?.LogInfo($"[COOP] Dealt {__result.CardType} {__result.UpgradeIndex} at {__state}, sent to the partner");
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogWarning($"[COOP] Sending the tarot cards failed: {e.Message}");
                }
            }
        }
    }
}

/* EOF */
