/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Both players' hearts on screen, as in the game's co-op: the Lamb's and the Goat's
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using HarmonyLib;
using System;
using System.Collections;
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
    ///
    /// In a dungeon with the partner here it is the game's co-op layout, each panel with its player's
    /// face and colours, but our own panel is the first (on top) and the partner's the second, for the
    /// host and the friend alike, and nothing is shrunk (the game's co-op makes the hearts smaller for
    /// two players sharing one screen; each of us has a screen of their own). In the cult the game's
    /// co-op shows only one panel, so ours alone. With a partner here both start with the game's co-op
    /// hearts. The friend's extra hearts are their own character's (see Rejoin).
    /// </remarks>
    [HarmonyPatch]
    internal static class CoopHud
    {
        private static readonly AccessTools.FieldRef<Health_Manager, GameObject> HealthBaseSingle =
            AccessTools.FieldRefAccess<Health_Manager, GameObject>("healthBaseSingle");
        private static readonly AccessTools.FieldRef<Health_Manager, GameObject> HealthDungeonTwoPlayer =
            AccessTools.FieldRefAccess<Health_Manager, GameObject>("healthDungeonTwoPlayer");
        private static readonly AccessTools.FieldRef<Health_Manager, UI_Transitions> TransitionTwoPlayer =
            AccessTools.FieldRefAccess<Health_Manager, UI_Transitions>("transition_TwoPlayer");

        /* Counters for the test harness report */
        internal static int Rebuilt, GoatPanels, HeartsApplied, Recounted;

        private static bool WasTogether;

        /// <summary>
        /// The session is over: nothing of it is to steer the next one
        /// </summary>
        internal static void ResetSession()
        {
            WasTogether = false;
        }

        public static void Initialize()
        {
            Plugin.MonoInstance.StartCoroutine(WatchPartner());
        }

        /// <summary>
        /// The partner came or went: outside a dungeon our hearts are counted again (the game's co-op
        /// starts each with fewer), as the game does for its own second player
        /// </summary>
        private static IEnumerator WatchPartner()
        {
            WaitForSecondsRealtime Wait = new(0.5f);
            while (true)
            {
                yield return Wait;
                bool Now = Together;
                if (Now == WasTogether)
                    continue;
                PlayerFarming Player = PlayerFarming.Instance;
                if (Player == null || Player.health == null || InDungeon || MMTools.MMTransition.IsPlaying)
                    continue;
                WasTogether = Now;
                try
                {
                    Player.health.InitHP();
                    Rebuild();
                    ++Recounted;
                    Plugin.Logger?.LogInfo($"[COOP] {(Now ? "The partner is here" : "On our own")}: hearts counted again, {Player.health.HP}/{Player.health.totalHP}");
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogWarning($"[COOP] Can't count our hearts again: {e.Message}");
                }
            }
        }

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
                /* Each Init starts by hiding every panel: in the cult only ours is built, as the game's co-op shows only one there */
                foreach (PlayerFarming Player in PlayerFarming.players.Where(Each => Each != null && Each.health != null && (InDungeon || Ours(Each))).OrderBy(Each => Ours(Each) ? 0 : 1).ToList())
                    Health.Init(Player);
                foreach (PlayerFarming Player in PlayerFarming.players)
                    Faces(Player);
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

        private static bool InDungeon => GameManager.IsDungeon(PlayerFarming.Location);

        /// <summary>
        /// The partner plays with us now (the game's co-op is two players in its list)
        /// </summary>
        private static bool Together => CoopSync.InSession && COTLMP.Network.SteamBridge.PartnerIds().Count > 0;

        private static bool Ours(PlayerFarming Player) => Player != null && Player == PlayerFarming.Instance && !PlayerManager.IsPuppet(Player.health);

        /// <summary>
        /// This lamb is the Goat (ours as the friend, or the partner's puppet), as the HUD shows it
        /// </summary>
        internal static bool IsGoatPlayer(PlayerFarming Player) => Player != null && IsGoat(Player);

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
            /* Down or out, their hearts are empty whatever their game shows (a game over refills them) */
            if (CoopLife.PartnerFallen)
            {
                Values = (float[])Values.Clone();
                Values[0] = Values[2] = Values[3] = Values[4] = Values[6] = Values[7] = 0f;
            }
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

                /* In the cult the game's co-op shows one panel, ours: the puppet's would hide it (an Init hides all
                   first) and the second player's panel there is a hidden one, left with every heart showing */
                if (!InDungeon)
                    return !PlayerManager.IsPuppet(p.health);
                if (CoopManager.CoopActive || PlayerFarming.players.Count < 2)
                    return true;
                bool Mine = Ours(p);
                if (!Mine && !PlayerManager.IsPuppet(p.health))
                    return true;
                bool Goat = IsGoat(p);
                bool WasLamb = p.isLamb;

                /* The game's co-op layout, as its own two players have it, but ours is the first panel (on top)
                   and the partner's the second, whichever of the two we are. A panel takes its colours from the lamb
                   it shows, so it is built as the Lamb or the Goat */
                if (Mine)
                    AccessTools.Method(typeof(Health_Manager), "DisableObjects").Invoke(__instance, null);
                HUD_Hearts Hearts = Mine ? __instance.health_firstPlayer : __instance.health_secondPlayer;
                if (__instance.TwoPlayerHudGameObject != null)
                    __instance.TwoPlayerHudGameObject.SetActive(true);
                HealthDungeonTwoPlayer(__instance).SetActive(true);
                p.isLamb = !Goat;
                try
                {
                    Hearts.gameObject.SetActive(true);
                    Hearts.InitDungeon(p);
                }
                finally
                {
                    p.isLamb = WasLamb;
                }
                Hearts.isLamb = !Goat;
                if (Goat)
                {
                    GoatAmmo(Hearts.faithAmmo, __instance.health_secondPlayer?.faithAmmo);
                    ++GoatPanels;
                }
                else
                {
                    LambAmmo(Hearts.faithAmmo, __instance.health_firstPlayer?.faithAmmo);
                }
                Faces(p);
                return false;
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Can't build the hearts panel, the game's own instead: {e.Message}");
                return true;
            }
        }

        [HarmonyPatch(typeof(Health_Manager), nameof(Health_Manager.Init))]
        [HarmonyPostfix]
        private static void Initialized(Health_Manager __instance, PlayerFarming p)
        {
            Faces(p);

            /* Alone in a room the game builds the single player's panel in the Lamb's colours: ours is the Goat's */
            try
            {
                if (InDungeon && Ours(p) && !CoopManager.CoopActive && PlayerFarming.players.Count < 2 && Plugin.GlobalsInternal.InGameSession)
                {
                    if (IsGoat(p))
                        GoatAmmo(__instance.health_SingleDungeon?.faithAmmo, __instance.health_secondPlayer?.faithAmmo);
                    else
                        LambAmmo(__instance.health_SingleDungeon?.faithAmmo, __instance.health_firstPlayer?.faithAmmo);
                }
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Can't colour our panel: {e.Message}");
            }
        }

        /// <summary>
        /// In a dungeon each panel's ring shows its player's face, the Lamb's or the Goat's, as in the game's co-op
        /// (which shows the rings only with its own second player)
        /// </summary>
        private static void Faces(PlayerFarming Player)
        {
            try
            {
                CoopIndicatorIcon Ring = Player?.hudHearts?.coopIndicatorIcon;
                if (Ring == null || CoopManager.CoopActive)
                    return;
                bool Show = ShowFaces;
                Ring.gameObject.SetActive(Show);
                if (Show)
                {
                    bool Goat = IsGoat(Player);
                    Ring.SetIcon(Goat ? CoopIndicatorIcon.CoopIcon.Goat : CoopIndicatorIcon.CoopIcon.Lamb);
                    /* The face and its glowing outline can come up switched off (it is the co-op's own animation's to show) */
                    GameObject Shown = Goat ? Ring.GoatIconContainer : Ring.LambIconContainer;
                    if (Shown != null)
                    {
                        foreach (UnityEngine.UI.Image Part in Shown.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                            Part.enabled = true;
                        foreach (UnityEngine.UI.Image Part in Shown.GetComponentsInParent<UnityEngine.UI.Image>(true))
                            Part.enabled = true;
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Can't show the panel's face: {e.Message}");
            }
        }

        /// <summary>
        /// The ring hides itself outside the game's own co-op (as it comes up, or a player joins or leaves): while
        /// ours show faces it stays as it is, with its player's face. (Showing it again after it hid would have it
        /// hide again as it comes up, round and round.)
        /// </summary>
        [HarmonyPatch(typeof(CoopIndicatorIcon), "UpdateIcon")]
        [HarmonyPrefix]
        private static bool RingUpdating(CoopIndicatorIcon __instance)
        {
            if (CoopManager.CoopActive || __instance == null || !ShowFaces)
                return true;
            foreach (PlayerFarming Player in PlayerFarming.players)
            {
                if (Player != null && Player.hudHearts != null && Player.hudHearts.coopIndicatorIcon == __instance)
                {
                    __instance.SetIcon(IsGoat(Player) ? CoopIndicatorIcon.CoopIcon.Goat : CoopIndicatorIcon.CoopIcon.Lamb);
                    return false;
                }
            }
            return true;
        }

        private static bool ShowFaces => InDungeon && Together && PlayerFarming.players.Count > 1;

        /// <summary>
        /// FaithAmmo.Init's Lamb colours (a panel the Goat had before)
        /// </summary>
        private static UnityEngine.UI.Image Part(Transform Root, string Name, string Parent)
        {
            foreach (UnityEngine.UI.Image Item in Root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                if (Item.name == Name && Item.transform.parent != null && Item.transform.parent.name == Parent)
                    return Item;
            }
            return null;
        }

        private static void LambAmmo(FaithAmmo Ammo, FaithAmmo Source)
        {
            Paint(Ammo, Source, "containerLamb", "AmmoGlowLamb", StaticColors.RedColor);
        }

        /// <summary>
        /// FaithAmmo.Init's Goat colours, which it only puts on with two players in the list
        /// </summary>
        private static void GoatAmmo(FaithAmmo Ammo, FaithAmmo Source)
        {
            Paint(Ammo, Source, "containerGoat", "AmmoGlowGoat", StaticColors.GoatPurple);
        }

        /// <summary>
        /// A ring in a character's colours: its container picture, its glow and its red parts (the single
        /// player's ring has the pictures of one character only: the co-op panel's are taken)
        /// </summary>
        private static void Paint(FaithAmmo Ammo, FaithAmmo Source, string Picture, string Shine, Color Colour)
        {
            if (Ammo == null)
                return;
            Traverse Fields = Traverse.Create(Ammo);
            Traverse Spare = Source != null ? Traverse.Create(Source) : null;
            /* The co-op panel's first: the single player's ring may hold one character's pictures in both */
            Sprite Sprite = Spare?.Field(Picture).GetValue<Sprite>() ?? Fields.Field(Picture).GetValue<Sprite>();
            Material Material = Spare?.Field(Shine).GetValue<Material>() ?? Fields.Field(Shine).GetValue<Material>();
            /* The single player's ring doesn't point at its parts: they are found by name */
            var Container = Fields.Field("containerSpriteRenderer").GetValue<UnityEngine.UI.Image>() ?? Part(Ammo.transform, "Container", "FaithBar");
            var Glow = Fields.Field("AmmoGlow").GetValue<UnityEngine.UI.Image>() ?? Part(Ammo.transform, "Glow", "FaithBar");
            var Red = Fields.Field("redGlowImage").GetValue<UnityEngine.UI.Image>() ?? Part(Ammo.transform, "RedGlow", "FaithProgressBar");
            var Empty = Fields.Field("outOfAmmoBG").GetValue<UnityEngine.UI.Image>() ?? Part(Ammo.transform, "Out of Ammo BG", "FaithBar");
            if (Container != null && Sprite != null)
                Container.sprite = Sprite;
            if (Glow != null && Material != null)
                Glow.material = Material;
            if (Red != null)
                Red.color = Colour;
            if (Empty != null)
                Empty.color = Colour;
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
        /// The game shrinks the hearts for two players sharing one screen; each of us has a screen of our own
        /// </summary>
        [HarmonyPatch(typeof(HUD_Manager), nameof(HUD_Manager.UpdatePlayerHudScale))]
        [HarmonyPostfix]
        private static void FullSize()
        {
            if (!CoopManager.CoopActive && CoopSync.InSession && HUD_Manager.Instance?.healthContainer != null)
                HUD_Manager.Instance.healthContainer.transform.localScale = Vector3.one;
        }

        /// <summary>
        /// The puppet's health is its player's, never the save's: setting it up would clear the save's extra hearts
        /// </summary>
        [HarmonyPatch(typeof(HealthPlayer), nameof(HealthPlayer.InitHP))]
        [HarmonyPrefix]
        private static bool InitHP(HealthPlayer __instance) => !(PlayerManager.CreatingPuppet || PlayerManager.IsPuppet(__instance));

        /// <summary>
        /// With the partner here each starts with the game's co-op hearts (fewer than alone)
        /// </summary>
        [HarmonyPatch(typeof(HealthPlayer), nameof(HealthPlayer.PLAYER_STARTING_HEALTH), MethodType.Getter)]
        [HarmonyPostfix]
        private static void StartingHearts(ref int __result)
        {
            if (CoopManager.CoopActive || !Together)
                return;
            __result = DifficultyManager.PrimaryDifficulty switch
            {
                DifficultyManager.Difficulty.Easy => 6,
                DifficultyManager.Difficulty.Hard => 4,
                DifficultyManager.Difficulty.ExtraHard => 2,
                _ => 4,
            };
        }

        /* WHO LOOKS LIKE WHOM: the game's icons assume player one is the Lamb ***/

        private static readonly AccessTools.FieldRef<HUD_Manager, UnityEngine.UI.Image> PlayerOneWidget =
            AccessTools.FieldRefAccess<HUD_Manager, UnityEngine.UI.Image>("_playerOneWidget");
        private static readonly AccessTools.FieldRef<HUD_Manager, UnityEngine.UI.Image> PlayerTwoWidget =
            AccessTools.FieldRefAccess<HUD_Manager, UnityEngine.UI.Image>("_playerTwoWidget");

        /* The heads the widgets came with: the first is drawn as the Lamb, the second as the Goat */
        private static Sprite LambHead, GoatHead;

        /// <summary>
        /// The marker of a player off the screen's edge shows who they LOOK like: the game gives the
        /// first player the Lamb's head and the second the Goat's, but the friend plays the Goat as
        /// the first player of their own game, with the host's puppet second
        /// </summary>
        [HarmonyPatch(typeof(HUD_Manager), "Update")]
        [HarmonyPostfix]
        private static void EdgeMarkers(HUD_Manager __instance)
        {
            if (CoopManager.CoopActive || !CoopSync.InSession || PlayerFarming.playersCount < 2)
                return;
            UnityEngine.UI.Image One = PlayerOneWidget(__instance), Two = PlayerTwoWidget(__instance);
            if (One == null || Two == null)
                return;
            if (LambHead == null)
            {
                LambHead = One.sprite;
                GoatHead = Two.sprite;
            }
            if (LambHead == null || GoatHead == null)
                return;
            One.sprite = IsGoat(PlayerFarming.players[0]) ? GoatHead : LambHead;
            Two.sprite = IsGoat(PlayerFarming.players[1]) ? GoatHead : LambHead;
        }

        /// <summary>
        /// The head by a hearts row follows its player's look too, wherever the game rebuilt the row
        /// (the final fight respawns the HUD on its own)
        /// </summary>
        [HarmonyPatch(typeof(HUD_Hearts), nameof(HUD_Hearts.InitDungeon))]
        [HarmonyPatch(typeof(HUD_Hearts), nameof(HUD_Hearts.InitBase))]
        [HarmonyPostfix]
        private static void RowHead(HUD_Hearts __instance)
        {
            if (CoopManager.CoopActive || !CoopSync.InSession || __instance.coopIndicatorIcon == null || __instance.playerFarming == null)
                return;
            __instance.coopIndicatorIcon.SetIcon(IsGoat(__instance.playerFarming) ? CoopIndicatorIcon.CoopIcon.Goat : CoopIndicatorIcon.CoopIcon.Lamb);
        }

        /// <summary>
        /// The damage flash finds the hurt player's icon by its look, not by the game's lamb-first seating
        /// </summary>
        [HarmonyPatch(typeof(CoopIndicatorIcon), nameof(CoopIndicatorIcon.AnimateDamageOnIcons))]
        [HarmonyPrefix]
        private static void FlashWho(PlayerFarming playerFarming, out (PlayerFarming, bool)? __state)
        {
            __state = null;
            if (CoopManager.CoopActive || !CoopSync.InSession || playerFarming == null)
                return;
            __state = (playerFarming, playerFarming.isLamb);
            playerFarming.isLamb = !IsGoat(playerFarming);
        }

        [HarmonyPatch(typeof(CoopIndicatorIcon), nameof(CoopIndicatorIcon.AnimateDamageOnIcons))]
        [HarmonyPostfix]
        private static void FlashedWho((PlayerFarming, bool)? __state)
        {
            if (__state != null)
                __state.Value.Item1.isLamb = __state.Value.Item2;
        }
    }
}

/* EOF */
