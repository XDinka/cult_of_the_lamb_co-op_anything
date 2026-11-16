/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     The test harness: our roll and the partner's puppet (-cotlmp-rolltest, -cotlmp-rollwatch)
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using HarmonyLib;
using System.Collections;
using System.Linq;
using Unify.Input;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Debug
{
    internal static partial class TestHarness
    {
        /*
         * -cotlmp-rolltest: in each of the first places where the partner's puppet has stood with us for a few
         * seconds, the roll button of the game's first input player (our keyboard and pad) reads as pressed for
         * one frame, to whoever holds that player then: exactly what a real key does. Every 0.1 s for 1.5 s a
         * frame, with where our lamb and the puppet stand and what state the puppet is in.
         * -cotlmp-rollwatch: the same frames and lines on the partner's side, from the moment their puppet
         * (our lamb there) starts the roll animation.
         * Neither touches a save or anything paid.
         */
        internal static bool RollTest, RollWatch;
        private const int RollButton = 16;
        private const int RollPlaces = 3;
        private static int RollPressFrame = -1;

        private static PlayerFarming RollPuppet() =>
            PlayerFarming.players.FirstOrDefault(Player => Player != null && Player.health != null && COTLMP.Game.PlayerManager.IsPuppet(Player.health));

        private static IEnumerator RollTestLoop()
        {
            string Done = null;
            int Places = 0;
            while (Places < RollPlaces)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                string Here = COTLMP.Game.CoopSync.InSession ? COTLMP.Game.CoopSync.CurrentPlace() : null;
                PlayerFarming Us = PlayerFarming.Instance;
                if (Here == null || Here == Done || Us == null || RollPuppet() == null || MMTools.MMTransition.IsPlaying || LetterBox.IsPlaying)
                    continue;
                /* A few quiet seconds with the puppet here, both standing */
                float Since = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - Since < 3f && RollPuppet() != null && COTLMP.Game.CoopSync.CurrentPlace() == Here)
                    yield return null;
                PlayerFarming Puppet = RollPuppet();
                if (Puppet == null || COTLMP.Game.CoopSync.CurrentPlace() != Here || Us.state.CURRENT_STATE != StateMachine.State.Idle)
                    continue;
                Done = Here;
                ++Places;
                Plugin.Logger?.LogWarning($"{Tag} Roll test {Places} in {Here}: pressing roll for one frame");
                RollPressFrame = Time.frameCount + 1;
                yield return RollFrames($"roll{Places}");
            }
        }

        private static IEnumerator RollWatchLoop()
        {
            int Seen = 0;
            bool Rolling = false;
            while (Seen < RollPlaces)
            {
                yield return null;
                PlayerFarming Puppet = RollPuppet();
                string Playing = Puppet?.Spine?.AnimationState?.GetCurrent(0)?.Animation?.Name;
                bool Now = Playing != null && Playing.Contains("roll");
                if (Now && !Rolling)
                {
                    ++Seen;
                    Plugin.Logger?.LogWarning($"{Tag} Roll watch {Seen} in {COTLMP.Game.CoopSync.CurrentPlace()}: the puppet plays {Playing}");
                    yield return RollFrames($"rollwatch{Seen}");
                }
                Rolling = Now;
            }
        }

        private static IEnumerator RollFrames(string Name)
        {
            for (int Step = 0; Step <= 15; Step++)
            {
                PlayerFarming Us = PlayerFarming.Instance, Puppet = RollPuppet();
                Plugin.Logger?.LogWarning($"{Tag} {Name} +{Step * 0.1f:0.0}s: us {(Us != null ? Format(Us.transform.position) : "-")} {Us?.state?.CURRENT_STATE} {Us?.Spine?.AnimationState?.GetCurrent(0)?.Animation?.Name}; " +
                                          $"puppet {(Puppet != null ? Format(Puppet.transform.position) : "-")} {Puppet?.state?.CURRENT_STATE} {Puppet?.Spine?.AnimationState?.GetCurrent(0)?.Animation?.Name} " +
                                          $"speed {(Puppet?.playerController != null ? Puppet.playerController.speed.ToString("0.00") : "-")}");
                Shot($"{Name}-{Step:00}");
                yield return new WaitForSecondsRealtime(0.1f);
            }
        }

        [HarmonyPatch]
        private static class RollTestPatches
        {
            /* First, so that a fix's own postfix on the same method has the last word */
            [HarmonyPatch(typeof(InputSource), nameof(InputSource.GetButtonDown), new System.Type[] { typeof(int), typeof(PlayerFarming) })]
            [HarmonyPostfix]
            [HarmonyPriority(Priority.First)]
            private static void Press(int button, PlayerFarming playerFarming, ref bool __result)
            {
                if (!RollTest || button != RollButton || Time.frameCount != RollPressFrame || playerFarming == null)
                    return;
                if (playerFarming.rewiredPlayer != null && playerFarming.rewiredPlayer == RewiredInputManager.GetPlayer(0))
                {
                    __result = true;
                    /* Whether it counts is for the postfixes after this one (a puppet's input reads as untouched) */
                    Plugin.Logger?.LogWarning($"{Tag} Roll press offered to {(playerFarming == PlayerFarming.Instance ? "our lamb" : COTLMP.Game.PlayerManager.IsPuppet(playerFarming.health) ? "the PUPPET" : playerFarming.name)}");
                }
            }
        }
    }
}

/* EOF */
