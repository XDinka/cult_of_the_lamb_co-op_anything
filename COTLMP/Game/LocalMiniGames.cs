/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     The game's games for two at one screen are played alone online
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// The game's own games for two players at one screen don't take the partner's puppet in.
    /// </summary>
    /// <remarks>
    /// The puppet stands in the game's list of players, as the game's second player does. Fishing walks
    /// every player of that list to the water and waits for each one's cast, and knucklebones offers its
    /// match for two with the second player at the other side of the board: the puppet would take a seat
    /// it can never play from (it has no input here), and the game would wait on it for good. Online each
    /// plays these on their own, as the game does with one player: the puppet is out of the list while we
    /// fish, and the match for two isn't offered. Playing them together over the network is its own work.
    /// </remarks>
    [HarmonyPatch]
    internal static class LocalMiniGames
    {
        private static readonly FieldInfo Fishing = AccessTools.Field(typeof(Interaction_Fishing), "isFishing");
        private static readonly FieldInfo FishingWait = AccessTools.Field(typeof(Interaction_Fishing), "waitForPlayersCoroutine");

        /* The puppets out of the list while we fish, with where they stood in it */
        private static readonly List<(int, PlayerFarming)> Away = new();

        /* Counters for the test harness report */
        internal static int FishedAlone, MatchesForTwoRefused;

        private static bool PuppetsIn()
        {
            if (!CoopSync.InSession || CoopManager.CoopActive)
                return false;
            foreach (PlayerFarming Player in PlayerFarming.players)
            {
                if (Player != null && PlayerManager.IsPuppet(Player.health))
                    return true;
            }
            return false;
        }

        [HarmonyPatch(typeof(Interaction_Fishing), nameof(Interaction_Fishing.OnInteract))]
        [HarmonyPrefix]
        private static void Fish(Interaction_Fishing __instance)
        {
            try
            {
                if (Away.Count > 0 || !PuppetsIn())
                    return;
                for (int Index = PlayerFarming.players.Count - 1; Index >= 0; Index--)
                {
                    PlayerFarming Player = PlayerFarming.players[Index];
                    if (Player != null && PlayerManager.IsPuppet(Player.health))
                    {
                        Away.Add((Index, Player));
                        PlayerFarming.players.RemoveAt(Index);
                    }
                }
                PlayerFarming.RefreshPlayersCount(false);
                ++FishedAlone;
                Plugin.Logger?.LogInfo("[COOP] Fishing: on our own, the partner's puppet out of the game's players meanwhile");
                Plugin.MonoInstance.StartCoroutine(PuppetsBack(__instance));
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Fishing alone failed: {e.Message}");
                PutBack();
            }
        }

        /* The fishing over (caught, lost or let go), or the spot gone: the puppets stand in the list again */
        private static IEnumerator PuppetsBack(Interaction_Fishing Spot)
        {
            float Since = Time.unscaledTime;
            bool Began = false;
            while (Time.unscaledTime - Since < 900f && Spot != null && Spot.isActiveAndEnabled)
            {
                yield return null;
                bool On = (bool)Fishing.GetValue(Spot) || FishingWait.GetValue(Spot) != null;
                if (On)
                    Began = true;
                else if (Began || Time.unscaledTime - Since > 5f)
                    break;
            }
            PutBack();
        }

        private static void PutBack()
        {
            if (Away.Count == 0)
                return;
            Away.Sort((A, B) => A.Item1.CompareTo(B.Item1));
            foreach (var (Index, Player) in Away)
            {
                /* Only a puppet that is still one (not taken down or hidden meanwhile, by a scene change) */
                if (Player == null || Player.health == null || !PlayerManager.IsPuppet(Player.health) || !Player.gameObject.activeInHierarchy)
                {
                    Plugin.Logger?.LogInfo("[COOP] Fishing over: the partner's puppet went meanwhile, it stays out of the game's players");
                    continue;
                }
                if (!PlayerFarming.players.Contains(Player))
                    PlayerFarming.players.Insert(Math.Min(Index, PlayerFarming.players.Count), Player);
            }
            Away.Clear();
            PlayerFarming.RefreshPlayersCount(false);
            Plugin.Logger?.LogInfo("[COOP] Fishing over: the partner's puppet is one of the game's players again");
        }

        /// <summary>
        /// The session is over: the puppets are gone with it, none comes back into the game's players
        /// </summary>
        internal static void ResetSession()
        {
            if (Away.Count > 0)
                Plugin.Logger?.LogInfo("[COOP] The session ended while fishing: the partner's puppet stays out of the game's players");
            Away.Clear();
        }

        [HarmonyPatch(typeof(Interaction_KnucklebonesBuilding), nameof(Interaction_KnucklebonesBuilding.GetLabel))]
        [HarmonyPostfix]
        private static void NoMatchForTwo(Interaction_KnucklebonesBuilding __instance)
        {
            if (PuppetsIn())
                __instance.HasSecondaryInteraction = false;
        }

        [HarmonyPatch(typeof(Interaction_KnucklebonesBuilding), nameof(Interaction_KnucklebonesBuilding.GetSecondaryLabel))]
        [HarmonyPostfix]
        private static void NoMatchForTwoLabel(Interaction_KnucklebonesBuilding __instance)
        {
            if (PuppetsIn())
                __instance.SecondaryLabel = "";
        }

        [HarmonyPatch(typeof(Interaction_KnucklebonesBuilding), nameof(Interaction_KnucklebonesBuilding.OnSecondaryInteract))]
        [HarmonyPrefix]
        private static bool NoMatchForTwoPlayed()
        {
            if (!PuppetsIn())
                return true;
            ++MatchesForTwoRefused;
            Plugin.Logger?.LogInfo("[COOP] Knucklebones for two isn't played online (the other seat would be the partner's puppet)");
            return false;
        }
    }
}

/* EOF */
