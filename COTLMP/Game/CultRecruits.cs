/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     A newcomer waiting by the cult's gate is recruited once, by one player
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP.Debug;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// Newcomers waiting to be recruited (the first follower of a new game, the ones brought back
    /// from runs).
    /// </summary>
    /// <remarks>
    /// A newcomer is a follower the save already has, with its id, waiting in the list of recruits;
    /// both games show it at the gate. When one player recruited it, the other's game still had it
    /// standing there: the follower came over as a new member of the cult and stood next to it, the
    /// same follower twice, and it could be recruited a second time. Now the player who starts a
    /// recruit has it, the other's newcomer doesn't answer meanwhile (as a follower talks to one
    /// player at a time), and when the follower comes over the waiting copy is taken away first.
    /// </remarks>
    [HarmonyPatch]
    internal static class CultRecruits
    {
        private enum Step : byte
        {
            Start = 1,
            Done = 2
        }

        /* A recruit the partner started is theirs at most this long */
        private const float HoldAtMost = 120f;

        private static readonly Dictionary<int, float> PartnerRecruits = new();
        private static readonly HashSet<int> Ours = new();

        /* Counters for the test harness report */
        internal static int Refused, TakenAway, Started;

        private static void Send(Step Type, int Id)
        {
            if (!CoopSync.InSession)
                return;
            CoopSync.Send(CoopSync.Kind.Recruit, Writer =>
            {
                Writer.Write((byte)Type);
                Writer.Write(Id);
            });
        }

        public static void OnRelay(BinaryReader Reader)
        {
            Step Type = (Step)Reader.ReadByte();
            int Id = Reader.ReadInt32();
            if (Type == Step.Start)
            {
                PartnerRecruits[Id] = Time.unscaledTime;
                Plugin.Logger?.LogInfo($"[COOP] The partner is recruiting follower {Id}");
            }
            else
            {
                PartnerRecruits.Remove(Id);
            }
        }

        private static bool PartnerHas(int Id) =>
            PartnerRecruits.TryGetValue(Id, out float Since) && Time.unscaledTime - Since < HoldAtMost && CoopSync.InSession;

        /// <summary>
        /// The partner recruited this follower: the newcomer waiting here goes, the follower comes in its place
        /// </summary>
        internal static void TakeAway(int Id)
        {
            try
            {
                foreach (FollowerRecruit Waiting in UnityEngine.Object.FindObjectsOfType<FollowerRecruit>())
                {
                    if (Waiting == null || Waiting.Follower?.Brain?.Info == null || Waiting.Follower.Brain.Info.ID != Id)
                        continue;
                    FollowerBrain.FindBrainByID(Id)?.Cleanup();
                    FollowerBrain.AllBrains.RemoveAll(Brain => Brain?.Info != null && Brain.Info.ID == Id);
                    UnityEngine.Object.Destroy(Waiting.gameObject);
                    ++TakenAway;
                    Plugin.Logger?.LogInfo($"[COOP] Follower {Id} was recruited by the partner: our newcomer goes");
                }
                DataManager.Instance?.Followers_Recruit?.RemoveAll(Info => Info != null && Info.ID == Id);
                PartnerRecruits.Remove(Id);
            }
            catch (Exception e)
            {
                PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Taking our newcomer {Id} away failed: {e.Message}");
            }
        }

        /* PATCHES ************************************************************/

        [HarmonyPatch(typeof(FollowerRecruit), nameof(FollowerRecruit.OnInteract))]
        [HarmonyPrefix]
        private static bool Interact(FollowerRecruit __instance) => Start(__instance);

        [HarmonyPatch(typeof(FollowerRecruit), nameof(FollowerRecruit.OnSecondaryInteract))]
        [HarmonyPrefix]
        private static bool InteractSecond(FollowerRecruit __instance) => Start(__instance);

        internal static bool Start(FollowerRecruit Recruit)
        {
            int Id = Recruit?.Follower?.Brain?.Info?.ID ?? -1;
            if (Id < 0 || !CoopSync.InSession)
                return true;
            if (PartnerHas(Id))
            {
                ++Refused;
                try
                {
                    NotificationCentre.Instance?.PlayGenericNotification("Multiplayer/Cult/FollowerBusy");
                }
                catch
                {
                }
                return false;
            }
            if (Ours.Add(Id))
            {
                ++Started;
                Send(Step.Start, Id);
            }
            return true;
        }

        [HarmonyPatch(typeof(FollowerRecruit), "OnRecruitFinished")]
        [HarmonyPostfix]
        private static void Finished(FollowerRecruit __instance)
        {
            int Id = __instance?.Follower?.Brain?.Info?.ID ?? -1;
            if (Id >= 0 && Ours.Remove(Id))
                Send(Step.Done, Id);
        }
    }
}

/* EOF */
