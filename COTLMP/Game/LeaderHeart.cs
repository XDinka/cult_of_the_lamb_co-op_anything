/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     A leader's heart is taken once, for both players
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using HarmonyLib;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// The heart a defeated leader leaves is the game's one: whoever takes it, it is taken for both.
    /// </summary>
    /// <remarks>
    /// Each player's game has its own heart on its own leader's body, and taking it is what opens the
    /// room's chained door, reveals the chest and counts the leader beaten (the host's save keeps
    /// that). As in the game's own co-op, one player taking it is enough: the other's heart is taken
    /// there too, without their lamb walking up to it. A heart that isn't out yet on the other side
    /// (its leader still falling) is taken as soon as it is.
    /// </remarks>
    internal static class LeaderHeart
    {
        private static readonly FieldInfo HeartEnabled = AccessTools.Field(typeof(Interaction_MonsterHeart), "Enabled");
        private static readonly MethodInfo HeartTaken = AccessTools.Method(typeof(Interaction_MonsterHeart), "HeartHasBeenTaken");

        /* Where the partner took a heart that isn't out here yet */
        private static string Pending;

        /* Counters for the test harness report */
        internal static int Sent, Received, Applied;

        /// <summary>
        /// The partner took the heart where they stand.
        /// </summary>
        public static void OnRelay(BinaryReader Reader)
        {
            string Place = Reader.ReadString();
            ++Received;
            Plugin.Logger?.LogInfo($"[COOP] The partner took the leader's heart in {Place}");
            if (Place != CoopSync.CurrentPlace())
                return;
            Interaction_MonsterHeart Heart = UnityEngine.Object.FindObjectOfType<Interaction_MonsterHeart>();
            if (Heart != null && Heart.enabled && IsOut(Heart))
                Take(Heart);
            else
                Pending = Place;
        }

        internal static void ResetSession() => Pending = null;

        private static bool IsOut(Interaction_MonsterHeart Heart) => (bool)HeartEnabled.GetValue(Heart);

        /* The heart taken here as the partner took it there: the leader's body empty, then the game's own ending */
        private static void Take(Interaction_MonsterHeart Heart)
        {
            Pending = null;
            try
            {
                HeartEnabled.SetValue(Heart, false);
                Heart.StopAllCoroutines();
                if (Heart.state == null)
                    Heart.state = PlayerFarming.Instance?.state;
                Empty(Heart);
                /* Taken as the heart came out, the leader's fall still lays its full body down right after */
                Plugin.MonoInstance.StartCoroutine(EmptyAgain(Heart));
                HeartTaken.Invoke(Heart, null);
                ++Applied;
                Plugin.Logger?.LogInfo("[COOP] The leader's heart is taken here too");
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Taking the partner's heart failed: {e}");
            }
        }

        /* As the game empties the body when the lamb pulls the heart out */
        private static void Empty(Interaction_MonsterHeart Heart)
        {
            if (Heart == null)
                return;
            if (Heart.simpleSpineAnimator != null)
                Heart.simpleSpineAnimator.Animate("dead-noheart", 0, false);
            else
                Heart.GetComponentInChildren<Spine.Unity.SkeletonAnimation>()?.AnimationState?.SetAnimation(0, "dead-noheart", false);
        }

        private static IEnumerator EmptyAgain(Interaction_MonsterHeart Heart)
        {
            yield return null;
            yield return null;
            Empty(Heart);
        }

        [HarmonyPatch]
        private static class LeaderHeartPatches
        {
            [HarmonyPatch(typeof(Interaction_MonsterHeart), nameof(Interaction_MonsterHeart.OnInteract))]
            [HarmonyPrefix]
            private static void Interact(Interaction_MonsterHeart __instance, out bool __state) => __state = IsOut(__instance);

            [HarmonyPatch(typeof(Interaction_MonsterHeart), nameof(Interaction_MonsterHeart.OnInteract))]
            [HarmonyPostfix]
            private static void Interacted(Interaction_MonsterHeart __instance, bool __state)
            {
                /* Our lamb went for it */
                if (!__state || IsOut(__instance) || !CoopSync.InSession)
                    return;
                string Place = CoopSync.CurrentPlace();
                try
                {
                    CoopSync.Send(CoopSync.Kind.HeartTaken, Writer => Writer.Write(Place ?? ""));
                    ++Sent;
                    Plugin.Logger?.LogInfo($"[COOP] We took the leader's heart in {Place}, sent to the partner");
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogWarning($"[COOP] Sending the leader's heart failed: {e.Message}");
                }
            }

            [HarmonyPatch(typeof(Interaction_MonsterHeart), nameof(Interaction_MonsterHeart.Play))]
            [HarmonyPostfix]
            private static void Out(Interaction_MonsterHeart __instance)
            {
                if (Pending != null && Pending == CoopSync.CurrentPlace() && __instance.enabled && IsOut(__instance))
                    Take(__instance);
            }
        }
    }
}

/* EOF */
