/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Both players' hearts on screen, as in the game's co-op: the Lamb's and the Goat's
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// The hearts, curse, weapon and relic panels on screen.
    /// </summary>
    /// <remarks>
    /// The partner's puppet is a second lamb in the game's player list (enemies go for it through
    /// that list), so the game builds its two-player HUD, as in its own co-op: the Lamb's panel and
    /// the Goat's in the Goat's colours. Each panel follows its lamb's health.
    ///
    /// What went wrong before: the puppet was always "the Lamb", so on the host's side it took the
    /// Lamb's panel and ours showed nowhere, and as it was made and removed on every room change the
    /// panels were rebuilt around it and went blank. Setting itself up it also reset the save's extra
    /// hearts (HealthPlayer.InitHP clears the Lamb's blue, black and spirit hearts in a dungeon).
    ///
    /// Now the puppet is the Lamb or the Goat as its player is, it leaves the HUD and the hearts'
    /// data alone while it is made, and the HUD is built again for both once it stands there. Our own
    /// lamb is always the game's first player, so as the Goat its panel is built the way the game
    /// builds the second player's. The puppet's hearts are its player's, sent with its moves
    /// (<see cref="PuppetSync"/>).
    /// </remarks>
    [HarmonyPatch]
    internal static class CoopHud
    {
        private static readonly AccessTools.FieldRef<Health_Manager, GameObject> HealthBaseSingle =
            AccessTools.FieldRefAccess<Health_Manager, GameObject>("healthBaseSingle");
        private static readonly AccessTools.FieldRef<Health_Manager, GameObject> HealthSingleUniversal =
            AccessTools.FieldRefAccess<Health_Manager, GameObject>("healthSingleUniversal");
        private static readonly AccessTools.FieldRef<Health_Manager, GameObject> HealthDungeonTwoPlayer =
            AccessTools.FieldRefAccess<Health_Manager, GameObject>("healthDungeonTwoPlayer");
        private static readonly AccessTools.FieldRef<Health_Manager, UI_Transitions> TransitionTwoPlayer =
            AccessTools.FieldRefAccess<Health_Manager, UI_Transitions>("transition_TwoPlayer");
        private static readonly AccessTools.FieldRef<Health_Manager, UI_Transitions> TransitionSingle =
            AccessTools.FieldRefAccess<Health_Manager, UI_Transitions>("transition_Single");

        /* Counters for the test harness report */
        internal static int Rebuilt, GoatPanels, HeartsApplied;

        /// <summary>
        /// Builds the HUD again for every lamb in the game's list, the Lamb first
        /// </summary>
        internal static void Rebuild()
        {
            try
            {
                Health_Manager Health = HUD_Manager.Instance?.healthManager;
                if (Health == null)
                    return;
                foreach (PlayerFarming Player in PlayerFarming.players.Where(Each => Each != null && Each.health != null).OrderBy(Each => IsGoat(Each) ? 1 : 0).ToList())
                    Health.Init(Player);
                /* The panels in use now come in, if the HUD is up */
                if (!HUD_Manager.Instance.Hidden)
                    Health.Show();
                ++Rebuilt;
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Can't build the hearts panels again: {e.Message}");
            }
        }

        private static bool Ours(PlayerFarming Player) => Player != null && Player == PlayerFarming.Instance && !PlayerManager.IsPuppet(Player.health);

        private static bool IsGoat(PlayerFarming Player) =>
            Ours(Player) ? Plugin.GlobalsInternal.InGameSession && Appearance.WeAreGoat && !IntroCoop.Apart : !Player.isLamb;

        /* OUR HEARTS, FOR THE PARTNER ****************************************/

        /// <summary>
        /// Our hearts as the partner's puppet of us should show them: red, total, blue, black, spirit, spirit total, fire, ice
        /// </summary>
        internal static void WriteHearts(BinaryWriter Writer)
        {
            HealthPlayer Health = PlayerFarming.Instance?.health;
            float[] Values = Health == null ? new float[8] :
                new[] { Health.HP, Health.totalHP, Health.BlueHearts, Health.BlackHearts, Health.SpiritHearts, Health.TotalSpiritHearts, Health.FireHearts, Health.IceHearts };
            foreach (float Value in Values)
                Writer.Write(Value);
        }

        internal static float[] ReadHearts(BinaryReader Reader)
        {
            float[] Values = new float[8];
            for (int i = 0; i < Values.Length; ++i)
                Values[i] = Reader.ReadSingle();
            return Values;
        }

        /// <summary>
        /// The puppet's health shows its player's hearts (it never takes a hit itself, see RoomSync)
        /// </summary>
        internal static void ApplyHearts(PlayerFarming Puppet, float[] Values)
        {
            HealthPlayer Health = Puppet?.health;
            if (Health == null || Values == null || Values[1] <= 0f)
                return;
            if (Health.totalHP == Values[1] && Health.HP == Values[0] && Health.BlueHearts == Values[2] && Health.BlackHearts == Values[3] &&
                Health.SpiritHearts == Values[4] && Health.TotalSpiritHearts == Values[5] && Health.FireHearts == Values[6] && Health.IceHearts == Values[7])
                return;
            Health.totalHP = Values[1];
            Health.TotalSpiritHearts = Values[5];
            Health.BlueHearts = Values[2];
            Health.BlackHearts = Values[3];
            Health.SpiritHearts = Values[4];
            Health.FireHearts = Values[6];
            Health.IceHearts = Values[7];
            Health.HP = Values[0];
            ++HeartsApplied;
        }

        /* PATCHES ************************************************************/

        [HarmonyPatch(typeof(Health_Manager), nameof(Health_Manager.Init))]
        [HarmonyPrefix]
        private static bool Init(Health_Manager __instance, PlayerFarming p)
        {
            try
            {
                /* The puppet's panel comes once it is dressed as its player, see Rebuild */
                if (PlayerManager.CreatingPuppet)
                    return false;
                if (!Ours(p) || !IsGoat(p))
                    return true;

                /* Health_Manager.Init's way for a second player that isn't the Lamb; the panel takes its
                   colours from the lamb it shows, so ours is the Goat while it is built */
                AccessTools.Method(typeof(Health_Manager), "DisableObjects").Invoke(__instance, null);
                HUD_Hearts Hearts = __instance.health_secondPlayer;
                bool WasLamb = p.isLamb;
                p.isLamb = false;
                try
                {
                    if (GameManager.IsDungeon(PlayerFarming.Location))
                    {
                        if (__instance.TwoPlayerHudGameObject != null)
                            __instance.TwoPlayerHudGameObject.SetActive(true);
                        HealthDungeonTwoPlayer(__instance).SetActive(true);
                        Hearts.gameObject.SetActive(true);
                        Hearts.InitDungeon(p);
                    }
                    else
                    {
                        HealthSingleUniversal(__instance).SetActive(true);
                        HealthBaseSingle(__instance).SetActive(true);
                        Hearts.InitBase(p);
                    }
                }
                finally
                {
                    p.isLamb = WasLamb;
                }
                Hearts.isLamb = false;
                GoatAmmo(Hearts.faithAmmo);
                if (Hearts.coopIndicatorIcon != null)
                {
                    Hearts.coopIndicatorIcon.SetIcon(CoopIndicatorIcon.CoopIcon.Goat);
                    Hearts.coopIndicatorIcon.SetUsername(1);
                }
                ++GoatPanels;
                return false;
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Can't build the Goat's panel, the game's own instead: {e.Message}");
                return true;
            }
        }

        /// <summary>
        /// FaithAmmo.Init's Goat colours, which it only puts on with two players in the list
        /// </summary>
        private static void GoatAmmo(FaithAmmo Ammo)
        {
            if (Ammo == null)
                return;
            Traverse Fields = Traverse.Create(Ammo);
            var Container = Fields.Field("containerSpriteRenderer").GetValue<UnityEngine.UI.Image>();
            var Glow = Fields.Field("AmmoGlow").GetValue<UnityEngine.UI.Image>();
            var Red = Fields.Field("redGlowImage").GetValue<UnityEngine.UI.Image>();
            var Empty = Fields.Field("outOfAmmoBG").GetValue<UnityEngine.UI.Image>();
            if (Container == null || Glow == null || Red == null || Empty == null)
                return;
            Container.sprite = Fields.Field("containerGoat").GetValue<Sprite>();
            Glow.material = Fields.Field("AmmoGlowGoat").GetValue<Material>();
            Empty.color = StaticColors.GoatPurple;
            Red.color = StaticColors.GoatPurple;
        }

        /// <summary>
        /// The HUD brings in the two-player panels only in the game's own co-op; ours are the two-player
        /// ones whenever they are what is built (the partner is here, or we are the Goat)
        /// </summary>
        [HarmonyPatch(typeof(Health_Manager), nameof(Health_Manager.Show))]
        [HarmonyPrefix]
        private static bool Show(Health_Manager __instance) => Move(__instance, true);

        [HarmonyPatch(typeof(Health_Manager), nameof(Health_Manager.Hide))]
        [HarmonyPrefix]
        private static bool Hide(Health_Manager __instance) => Move(__instance, false);

        private static bool Move(Health_Manager Manager, bool In)
        {
            try
            {
                if (CoopManager.CoopActive || !LocationManager.IsDungeonActive() || !HealthDungeonTwoPlayer(Manager).activeSelf)
                    return true;
                UI_Transitions Panels = TransitionTwoPlayer(Manager);
                if (In)
                    Panels.MoveBackInFunction();
                else
                    Panels.MoveBackOutFunction();
                return false;
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Can't move the hearts panels: {e.Message}");
                return true;
            }
        }

        /// <summary>
        /// The puppet's health is its player's, never the save's: setting it up would clear the save's extra hearts
        /// </summary>
        [HarmonyPatch(typeof(HealthPlayer), nameof(HealthPlayer.InitHP))]
        [HarmonyPrefix]
        private static bool InitHP(HealthPlayer __instance) => !(PlayerManager.CreatingPuppet || PlayerManager.IsPuppet(__instance));
    }
}

/* EOF */
