/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Downed instead of dead while the partner fights on, and reviving each other
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP.Debug;
using HarmonyLib;
using System;
using System.Collections;
using System.IO;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// Local co-op's knockout, across the network.
    /// </summary>
    /// <remarks>
    /// In the game's own co-op a player who dies while the other one stands is knocked out: they
    /// play the downed animation, crawl around, and get up when the room is cleared. Only when both
    /// are down is it game over. Here the other player lives in another game, so:
    /// <list type="bullet">
    /// <item>Our death becomes the game's knockout while the partner is alive in a dungeon.</item>
    /// <item>The partner's puppet plays the same downed animations while they are down.</item>
    /// <item>Getting them up is the game's own deal: the downed puppet carries the game's revive
    /// interaction, with its label, its "can't afford" refusal and its price — two hearts off the
    /// reviver, spirit and spare hearts first. Clearing the room wakes them through the game itself.</item>
    /// <item>If we die while the partner is down, it's game over for both.</item>
    /// </list>
    /// </remarks>
    internal static class CoopLife
    {
        private enum Life : byte
        {
            Alive = 0,
            Down = 1,
            Dead = 2
        }

        private static Life Mine = Life.Alive;
        private static Life Partner = Life.Alive;

        /// <summary>
        /// The session is over: nothing of it is to steer the next one
        /// </summary>
        internal static void ResetSession()
        {
            Partner = Life.Alive;
            /*
             * A game over of the last session stays the game's; our state for the next one starts alive. Down stays down:
             * the loop ends that (nobody is left to get us up)
             */
            if (Mine == Life.Dead && PlayerFarming.Instance != null && PlayerFarming.Instance.health != null && PlayerFarming.Instance.health.HP > 0f)
                Mine = Life.Alive;
        }

        private static float PartnerChanged;

        /* Both down: our lamb is on its way out, nothing may stand it up again meanwhile */
        private static bool Dying;
        private static float DyingSince;
        private static readonly System.Reflection.MethodInfo GameOnDie = AccessTools.Method(typeof(PlayerController), "OnDie");

        /* Counters for the test harness report */
        internal static int KnockOuts, Revives, ForcedDeaths, RevivesGiven, WakesRefused;

        internal static bool PartnerDown => Partner == Life.Down;
        internal static bool PartnerFallen => Partner != Life.Alive;
        internal static bool WeAreDown => Mine == Life.Down;
        internal static bool WeAreDead => Mine == Life.Dead;

        /// <summary>
        /// The partner can still carry the run: they are alive and in a dungeon. In the opening each
        /// plays alone (nobody can come to get anyone up), so the game's own death stands — a
        /// knockout there left the player lying unkillable in their own prologue.
        /// </summary>
        private static bool PartnerFighting =>
            CoopSync.InSession && !IntroCoop.Apart && Partner == Life.Alive &&
            RoomSync.PartnerLastPlace != null && RoomSync.PartnerLastPlace.Contains("#");

        public static void Initialize()
        {
            Plugin.MonoInstance.StartCoroutine(Loop());
        }

        private static void SendLife()
        {
            CoopSync.Send(CoopSync.Kind.Life, Writer => Writer.Write((byte)Mine));
        }

        private static void SetMine(Life Value)
        {
            if (Mine == Value)
                return;
            Mine = Value;
            Plugin.Logger?.LogInfo($"[COOP] We are {Value}");
            if (CoopSync.InSession)
                SendLife();
        }

        /// <summary>
        /// Handles life and revive data from the partner.
        /// </summary>
        public static void OnRelay(CoopSync.Kind Type, BinaryReader Reader)
        {
            switch (Type)
            {
                case CoopSync.Kind.Life:
                {
                    Life Value = (Life)Reader.ReadByte();
                    if (Value == Partner)
                        break;
                    Partner = Value;
                    PartnerChanged = Time.realtimeSinceStartup;
                    Plugin.Logger?.LogInfo($"[COOP] The partner is {Value}");
                    PlayerManager.SetPuppetsDown(Value == Life.Down);

                    /* They fell for good while we were down: the run is over for both */
                    if (Value == Life.Dead && Mine == Life.Down)
                        StartDying();
                    break;
                }

                case CoopSync.Kind.Revive:
                {
                    /* The partner paid the game's price at our puppet: we get up */
                    float Value = Reader.ReadSingle();
                    if (Value >= 1f && Mine == Life.Down)
                        GetUp();
                    break;
                }
            }
        }

        private static void GetUp()
        {
            PlayerFarming Player = PlayerFarming.Instance;
            if (Player == null || !Player.IsKnockedOut)
                return;

            Plugin.Logger?.LogInfo("[COOP] The partner got us up");
            ++Revives;
            try
            {
                /* The partner's puppet is the reviver: the game plays its whole revive scene from them */
                CoopManager.WakeKnockedOutPlayer(Player, 2f, PlayerManager.GetAnyPuppet(), Interaction_HeartPickupBase.HeartPickupType.Red, false);

                /* Local co-op pauses the game for a revive; without that pause, a moment to get clear */
                Player.playerController?.MakeInvincible(2f);
            }
            catch (Exception e)
            {
                PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Getting up failed: {e}");
            }
        }

        private static void StartDying()
        {
            if (Dying)
                return;
            Dying = true;
            DyingSince = Time.realtimeSinceStartup;
            Plugin.MonoInstance.StartCoroutine(DieForReal());
        }

        private static IEnumerator DieForReal()
        {
            /* Let the knockout animation settle, the game refuses damage during custom animations */
            yield return new WaitForSeconds(1.2f);

            PlayerFarming Player = PlayerFarming.Instance;
            if (Player == null || Player.health == null || Mine == Life.Dead)
            {
                Dying = false;
                yield break;
            }

            Plugin.Logger?.LogInfo("[COOP] Down for good (both down, or nobody left to get us up): the run ends");
            ++ForcedDeaths;
            try
            {
                Player.IsKnockedOut = false;
                Player.health.invincible = false;
                Player.health.untouchable = false;
                Player.state.LockStateChanges = false;
                Player.state.CURRENT_STATE = StateMachine.State.Idle;
                Player.health.HP = Mathf.Min(Player.health.HP, 0.5f);
                Player.health.DealDamage(Player.health.CurrentHP + 1f, Player.gameObject, Player.transform.position, false, Health.AttackTypes.Melee, true, Health.AttackFlags.ForceKill);
            }
            catch (Exception e)
            {
                PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Dying with the partner failed: {e.Message}");
            }
            yield return new WaitForSeconds(0.5f);

            /* The hit didn't take (a revive's grace, an animation): go the game's own way to game over */
            if (Mine != Life.Dead && Player != null && Player.playerController != null)
            {
                Plugin.Logger?.LogInfo("[COOP] The last hit didn't take, ending the run the game's way");
                try
                {
                    Player.health.HP = 0f;
                    GameOnDie.Invoke(Player.playerController, new object[] { null, Player.transform.position, Player.health, Health.AttackTypes.Melee, Health.AttackFlags.ForceKill });
                }
                catch (Exception e)
                {
                    PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Ending the run failed: {e}");
                }
            }
            Dying = false;
        }

        /// <summary>
        /// Keeps our life state in sync and ends a run nobody can save.
        /// </summary>
        /* Down with no partner to get us up: since when (the friend may be connecting again) */
        private static float AloneSince = -1f;
        private const float AloneDownFor = 90f;

        /* Down while the partner stands outside the dungeon (back in the cult, the run over for them): since when */
        private static float AwaySince = -1f;
        private const float AwayDownFor = 15f;

        /// <summary>
        /// The partner is in the session, settled somewhere that isn't a dungeon: not on the way (a floor or a
        /// scene loading, coming back to the host), not connecting again
        /// </summary>
        private static bool PartnerOutOfTheRun =>
            CoopSync.InSession && RoomSync.PartnerPlace != null && !RoomSync.PartnerPlace.Contains("#") &&
            !COTLMP.Network.Reconnect.Active && !Rejoin.Going;

        private static IEnumerator Loop()
        {
            float NextAnnounce = 0f;

            while (true)
            {
                yield return null;
                try
                {
                    if (!CoopSync.InSession)
                    {
                        /* The session ended while we were down: nobody can get us up, the run ends as alone */
                        if (Mine == Life.Down && !Dying)
                        {
                            Plugin.Logger?.LogInfo("[COOP] The session ended while we were down: down for good");
                            StartDying();
                        }
                        Partner = Life.Alive;
                        AloneSince = -1f;
                        continue;
                    }

                    /* The host down and the friend gone from the session (longer than connecting again takes) */
                    bool Alone = Plugin.GlobalsInternal.IsServerCreator && (COTLMP.Ui.PauseMenuPatches.Server?.PlayerCount ?? 0) < 2;
                    if (Mine == Life.Down && Alone && !Dying)
                    {
                        if (AloneSince < 0f)
                            AloneSince = Time.realtimeSinceStartup;
                        else if (Time.realtimeSinceStartup - AloneSince > AloneDownFor)
                        {
                            Plugin.Logger?.LogInfo($"[COOP] Down and the friend gone for {AloneDownFor:F0} s: down for good");
                            StartDying();
                        }
                    }
                    else
                    {
                        AloneSince = -1f;
                    }

                    /* Down, and nobody in the dungeon to get us up: the run ends for us too */
                    if (Mine == Life.Down && !Dying && PartnerOutOfTheRun)
                    {
                        if (AwaySince < 0f)
                            AwaySince = Time.realtimeSinceStartup;
                        else if (Time.realtimeSinceStartup - AwaySince > AwayDownFor)
                        {
                            Plugin.Logger?.LogInfo($"[COOP] The partner left the dungeon {Time.realtimeSinceStartup - AwaySince:F0} s ago ({RoomSync.PartnerPlace}): the run ends for us");
                            AwaySince = -1f;
                            StartDying();
                        }
                    }
                    else
                    {
                        AwaySince = -1f;
                    }

                    PlayerFarming Player = PlayerFarming.Instance;
                    if (Player != null && Mine == Life.Down && !Player.IsKnockedOut && !Dying)
                        SetMine(Life.Alive);

                    /* The partner is gone for good while we are down: we go too (again, if a try failed) */
                    if (Partner == Life.Dead && Mine == Life.Down && !Dying && Time.realtimeSinceStartup - DyingSince > 3f)
                        StartDying();

                    if (Time.realtimeSinceStartup >= NextAnnounce)
                    {
                        NextAnnounce = Time.realtimeSinceStartup + 2f;
                        SendLife();
                    }
                }
                catch (Exception e)
                {
                    PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Life sync failed: {e.Message}");
                }
            }
        }

        /* PATCHES ************************************************************/

        [HarmonyPatch]
        private static class CoopLifePatches
        {
            /// <summary>
            /// Our lamb died: knocked out instead while the partner fights on, otherwise the usual game over
            /// </summary>
            [HarmonyPatch(typeof(PlayerController), "OnDie")]
            [HarmonyPrefix]
            private static bool OnDie(PlayerController __instance)
            {
                try
                {
                    PlayerFarming Player = __instance.GetComponent<PlayerFarming>();
                    if (Player == null || Player != PlayerFarming.Instance || PlayerManager.IsPuppet(Player.health))
                        return true;

                    if (PartnerFighting && !Player.IsKnockedOut)
                    {
                        Plugin.Logger?.LogInfo("[COOP] Down, the partner can get us up");
                        ++KnockOuts;
                        CoopManager.KnockOutPlayer(Player);
                        SetMine(Life.Down);
                        return false;
                    }

                    SetMine(Life.Dead);
                }
                catch (Exception e)
                {
                    PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Knockout failed, dying normally: {e}");
                }
                return true;
            }

            /// <summary>
            /// The game's own revive, used at the downed partner's puppet: the price (two hearts,
            /// spirit and spare ones first, as the game takes them) is paid here, and the word goes
            /// to the partner, whose own game gets them up. The puppet itself stands up when the
            /// partner's life state comes back as alive.
            /// </summary>
            [HarmonyPatch(typeof(Interaction_CoopRevive), nameof(Interaction_CoopRevive.OnInteract))]
            [HarmonyPrefix]
            private static bool ReviveAtPuppet(Interaction_CoopRevive __instance, StateMachine state)
            {
                try
                {
                    PlayerFarming Downed = __instance.GetComponent<PlayerFarming>();
                    if (Downed == null || Downed.health == null || !PlayerManager.IsPuppet(Downed.health))
                        return true;

                    PlayerFarming Reviver = __instance.playerFarming != null ? __instance.playerFarming : PlayerFarming.Instance;
                    if (Reviver == null || Reviver.health == null || Partner != Life.Down)
                        return false;

                    /* Not enough hearts: the game's own refusal */
                    int Cost = __instance.reviveCost;
                    if (Reviver.health.CurrentHP <= Cost)
                    {
                        CameraManager.instance?.ShakeCameraForDuration(1f, 3f, 1f);
                        Reviver.indicator?.PlayShake();
                        return false;
                    }

                    /* The game's price, taken the way Interaction_CoopRevive takes it */
                    Health Purse = Reviver.health;
                    while (Purse.SpiritHearts > 0f && Cost > 0) { Purse.SpiritHearts--; Cost--; }
                    while (Purse.BlackHearts > 0f && Cost > 0) { Purse.BlackHearts--; Cost--; }
                    while (Purse.FireHearts > 0f && Cost > 0) { Purse.FireHearts--; Cost--; }
                    while (Purse.IceHearts > 0f && Cost > 0) { Purse.IceHearts--; Cost--; }
                    while (Purse.BlueHearts > 0f && Cost > 0) { Purse.BlueHearts--; Cost--; }
                    while (Purse.HP > 0f && Cost > 0) { Purse.HP--; Cost--; }

                    ++RevivesGiven;
                    Plugin.Logger?.LogInfo($"[COOP] Got the partner up for {__instance.reviveCost} hearts (ours now {Purse.CurrentHP})");
                    AudioManager.Instance?.PlayOneShot("event:/player/goat_player/goat_bell", Downed.gameObject);
                    CoopSync.Send(CoopSync.Kind.Revive, Writer => Writer.Write(1f));
                    __instance.enabled = false;
                    return false;
                }
                catch (Exception e)
                {
                    PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Reviving at the puppet failed: {e}");
                    return false;
                }
            }

            /// <summary>
            /// Up again, however it happened (revive, cleared room, a heart)
            /// </summary>
            [HarmonyPatch(typeof(CoopManager), nameof(CoopManager.WakeKnockedOutPlayer))]
            [HarmonyPostfix]
            private static void Woke(PlayerFarming playerFarming)
            {
                if (playerFarming != null && playerFarming == PlayerFarming.Instance && !playerFarming.IsKnockedOut && !Dying && Mine != Life.Dead)
                    SetMine(Life.Alive);
            }

            /// <summary>
            /// The run is over for us (both fell, or nobody is left to get us up): nothing gets us up again. The game
            /// wakes the fallen as a room comes alive or is cleared, a second or more later and in real time, so
            /// such a wake could stand our lamb up as we die, its game over taken back, its death screen never shown
            /// </summary>
            [HarmonyPatch(typeof(CoopManager), nameof(CoopManager.WakeKnockedOutPlayer))]
            [HarmonyPrefix]
            private static bool NoWakeAfterTheEnd(PlayerFarming playerFarming)
            {
                if (playerFarming == null || playerFarming != PlayerFarming.Instance || !(Dying || Mine == Life.Dead))
                    return true;
                ++WakesRefused;
                Plugin.Logger?.LogInfo("[COOP] The game would get us up, but our run is over: staying down");
                return false;
            }

            /// <summary>
            /// A new run or the base: everyone starts alive
            /// </summary>
            [HarmonyPatch(typeof(GameManager), nameof(GameManager.NewRun))]
            [HarmonyPostfix]
            private static void NewRun()
            {
                SetMine(Life.Alive);
            }
        }
    }
}

/* EOF */
