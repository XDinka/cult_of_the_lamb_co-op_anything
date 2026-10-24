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
    /// <item>Standing next to the downed partner for 3 seconds fills a bar and gets them up;
    /// walking away drains it slowly. Clearing the room wakes them through the game itself.</item>
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

        private const float ReviveSeconds = 3f;
        private const float ReviveRadius = 1.6f;

        private static Life Mine = Life.Alive;
        private static Life Partner = Life.Alive;
        private static float PartnerChanged;

        /* Revive bar: ours over the downed partner, or theirs over us, and when theirs last moved */
        private static float Progress, PartnerProgress;
        private static float LastProgressSent, PartnerProgressAt;

        /* Both down: our lamb is on its way out, nothing may stand it up again meanwhile */
        private static bool Dying;
        private static float DyingSince;
        private static readonly System.Reflection.MethodInfo GameOnDie = AccessTools.Method(typeof(PlayerController), "OnDie");

        /* Counters for the test harness report */
        internal static int KnockOuts, Revives, ForcedDeaths;

        internal static bool PartnerDown => Partner == Life.Down;
        internal static bool PartnerFallen => Partner != Life.Alive;
        internal static bool WeAreDown => Mine == Life.Down;
        internal static float ReviveProgress => Progress;

        /// <summary>
        /// The partner can still carry the run: they are alive and in a dungeon
        /// </summary>
        private static bool PartnerFighting =>
            CoopSync.InSession && Partner == Life.Alive && RoomSync.PartnerLastPlace != null && RoomSync.PartnerLastPlace.Contains("#");

        public static void Initialize()
        {
            Plugin.MonoInstance.StartCoroutine(Loop());
            GameObject Bar = new("COTLMP Revive Bar");
            UnityEngine.Object.DontDestroyOnLoad(Bar);
            Bar.AddComponent<ReviveBar>();
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
                    Progress = 0f;
                    if (Value != Life.Alive)
                        PartnerProgress = 0f;

                    /* They fell for good while we were down: the run is over for both */
                    if (Value == Life.Dead && Mine == Life.Down)
                        StartDying();
                    break;
                }

                case CoopSync.Kind.Revive:
                {
                    float Value = Reader.ReadSingle();
                    PartnerProgress = Partner == Life.Alive ? Value : 0f;
                    PartnerProgressAt = Time.realtimeSinceStartup;
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
            PartnerProgress = 0f;
            try
            {
                CoopManager.WakeKnockedOutPlayer(Player, 2f, null, Interaction_HeartPickupBase.HeartPickupType.Red, false);

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

            Plugin.Logger?.LogInfo("[COOP] Both of us are down, the run ends");
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
        /// Fills or drains the revive bar while the partner is down, and keeps our state in sync.
        /// </summary>
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
                        Partner = Life.Alive;
                        Progress = PartnerProgress = 0f;
                        continue;
                    }

                    PlayerFarming Player = PlayerFarming.Instance;
                    if (Player != null && Mine == Life.Down && !Player.IsKnockedOut && !Dying)
                        SetMine(Life.Alive);

                    /* The partner is gone for good while we are down: we go too (again, if a try failed) */
                    if (Partner == Life.Dead && Mine == Life.Down && !Dying && Time.realtimeSinceStartup - DyingSince > 3f)
                        StartDying();

                    /* Their bar stopped coming (they fell, the connection dropped): it isn't filling */
                    if (PartnerProgress > 0f && Time.realtimeSinceStartup - PartnerProgressAt > 1.5f)
                        PartnerProgress = 0f;

                    if (Time.realtimeSinceStartup >= NextAnnounce)
                    {
                        NextAnnounce = Time.realtimeSinceStartup + 2f;
                        SendLife();
                    }

                    UpdateRevive(Player);
                }
                catch (Exception e)
                {
                    PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Life sync failed: {e.Message}");
                }
            }
        }

        private static void UpdateRevive(PlayerFarming Player)
        {
            float Before = Progress;
            Vector3? Downed = Partner == Life.Down ? PlayerManager.GetAnyPuppetPosition() : null;

            if (Player == null || Mine != Life.Alive || !Downed.HasValue || MMTools.MMTransition.IsPlaying)
            {
                Progress = 0f;
            }
            else
            {
                Vector3 Gap = Player.transform.position - Downed.Value;
                Gap.z = 0f;
                if (Gap.magnitude < ReviveRadius)
                    Progress = Mathf.Min(1f, Progress + Time.deltaTime / ReviveSeconds);
                else
                    Progress = Mathf.Max(0f, Progress - Time.deltaTime / (ReviveSeconds * 2f));
            }

            if (Progress >= 1f)
            {
                Plugin.Logger?.LogInfo("[COOP] Got the partner up");
                CoopSync.Send(CoopSync.Kind.Revive, Writer => Writer.Write(1f));
                Progress = 0f;
                LastProgressSent = Time.realtimeSinceStartup;
                return;
            }

            /* Let them watch the bar fill, ten times a second while it moves */
            if (Progress != Before && Time.realtimeSinceStartup - LastProgressSent > 0.1f || (Before > 0f && Progress == 0f))
            {
                LastProgressSent = Time.realtimeSinceStartup;
                float Value = Progress;
                CoopSync.Send(CoopSync.Kind.Revive, Writer => Writer.Write(Value));
            }
        }

        /// <summary>
        /// Draws the revive bar over whoever is down.
        /// </summary>
        private sealed class ReviveBar : MonoBehaviour
        {
            private static Texture2D Pixel;

            private void OnGUI()
            {
                if (!CoopSync.InSession || Camera.main == null)
                    return;

                Vector3? Over = null;
                float Value = 0f;
                if (Partner == Life.Down && Progress > 0f)
                {
                    Over = PlayerManager.GetAnyPuppetPosition();
                    Value = Progress;
                }
                else if (Mine == Life.Down && PartnerProgress > 0f && PlayerFarming.Instance != null)
                {
                    Over = PlayerFarming.Instance.transform.position;
                    Value = PartnerProgress;
                }
                if (!Over.HasValue)
                    return;

                if (Pixel == null)
                {
                    Pixel = new Texture2D(1, 1);
                    Pixel.SetPixel(0, 0, Color.white);
                    Pixel.Apply();
                }

                Vector3 Screen = Camera.main.WorldToScreenPoint(Over.Value + Vector3.back * 1.6f);
                if (Screen.z < 0f)
                    return;

                float Width = UnityEngine.Screen.height * 0.09f, Height = Mathf.Max(6f, UnityEngine.Screen.height * 0.012f);
                Rect Back = new(Screen.x - Width / 2f, UnityEngine.Screen.height - Screen.y - Height / 2f, Width, Height);
                Color Old = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.75f);
                GUI.DrawTexture(new Rect(Back.x - 2f, Back.y - 2f, Back.width + 4f, Back.height + 4f), Pixel);
                GUI.color = new Color(0.25f, 0.05f, 0.05f, 0.9f);
                GUI.DrawTexture(Back, Pixel);
                GUI.color = new Color(0.93f, 0.18f, 0.16f, 1f);
                GUI.DrawTexture(new Rect(Back.x, Back.y, Back.width * Mathf.Clamp01(Value), Back.height), Pixel);
                GUI.color = Old;
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
            /// Up again, however it happened (revive, cleared room, a heart)
            /// </summary>
            [HarmonyPatch(typeof(CoopManager), nameof(CoopManager.WakeKnockedOutPlayer))]
            [HarmonyPostfix]
            private static void Woke(PlayerFarming playerFarming)
            {
                if (playerFarming != null && playerFarming == PlayerFarming.Instance && !playerFarming.IsKnockedOut)
                    SetMine(Life.Alive);
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
