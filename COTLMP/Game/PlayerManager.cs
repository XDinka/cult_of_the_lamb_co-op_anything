/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Define PlayerManager class
 * COPYRIGHT:	Copyright 2026 necoarcc <neco-arc@inbox.ru>
 */

/* IMPORTS ********************************************************************/

using COTLMP.Data;
using COTLMP.Debug;
using HarmonyLib;
using MMTools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// Class to manage local players created for network players
    /// </summary>
    internal static class PlayerManager
    {
        /// <summary>
        /// How quickly a managed player closes the gap to its latest network position (per second)
        /// </summary>
        private const float FollowRate = 12f;

        /// <summary>
        /// A managed player further than this from its network position is teleported instead of walked
        /// </summary>
        private const float SnapDistance = 6f;

        /// <summary>
        /// Below this distance a managed player counts as standing still
        /// </summary>
        private const float StopDistance = 0.05f;

        private readonly static PlayerFarming[] players;
        private static bool puppetsTargetable;

        /// <summary>
        /// Whether our enemies may go for the other players' puppets. RoomSync turns it on while
        /// we run the enemies of a room the partner is in; a hit on a puppet is sent to its player.
        /// </summary>
        internal static bool PuppetsTargetable
        {
            get => puppetsTargetable;
            set
            {
                puppetsTargetable = value;
                foreach (PlayerFarming farming in players)
                {
                    if (farming != null && farming.health != null)
                        farming.health.invincible = !value || puppetsDown;
                }
            }
        }

        private static bool puppetsDown;

        /// <summary>
        /// Shows the other player knocked out (or up again): the game's downed animations, and
        /// our enemies leave them alone.
        /// </summary>
        internal static void SetPuppetsDown(bool down)
        {
            puppetsDown = down;
            for (uint i = 0; i < players.Length; ++i)
            {
                PlayerFarming farming = players[i];
                if (farming == null)
                    continue;
                try
                {
                    ShowDowned(farming, skins[i] == Appearance.GoatSkin, down);
                }
                catch (Exception e)
                {
                    PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Showing player {i} downed failed: {e.Message}");
                }
            }
        }

        private static void ShowDowned(PlayerFarming farming, bool goat, bool down)
        {
            if (farming.health != null)
                farming.health.invincible = down || !puppetsTargetable;
            farming.IsKnockedOut = down;

            if (!down)
            {
                farming.playerController?.ResetSpecificMovementAnimations();
                farming.state.LockStateChanges = false;
                farming.state.CURRENT_STATE = StateMachine.State.Idle;
                return;
            }

            /* The same animations CoopManager.KnockOutPlayer uses */
            string prefix = goat ? "Downed/Goat/" : "Downed/";
            string suffix = goat ? "-goat" : "";
            farming.state.CURRENT_STATE = StateMachine.State.CustomAnimation;
            farming.CustomAnimation(prefix + "knockback-to-downed" + suffix, false);
            Plugin.MonoInstance.StartCoroutine(DownedLater(farming, prefix, suffix));
        }

        private static IEnumerator DownedLater(PlayerFarming farming, string prefix, string suffix)
        {
            yield return new WaitForSeconds(1f);
            if (farming == null || !farming.IsKnockedOut)
                yield break;
            farming.state.CURRENT_STATE = StateMachine.State.Idle;
            farming.playerController?.SetSpecificMovementAnimations(prefix + "idle" + suffix, prefix + "walk-up" + suffix, prefix + "walk-down" + suffix,
                                                                    prefix + "walk" + suffix, prefix + "walk-up-diagonal" + suffix, prefix + "walk-horizontal" + suffix);
        }

        /// <summary>
        /// What the camera follows on the (first) other player's puppet, if there is one
        /// </summary>
        internal static GameObject GetAnyPuppetObject()
        {
            foreach (PlayerFarming farming in players)
            {
                if (farming != null)
                    return farming.CameraBone != null ? farming.CameraBone : farming.gameObject;
            }
            return null;
        }

        /// <summary>
        /// Where the (first) other player's puppet stands, if there is one
        /// </summary>
        internal static Vector3? GetAnyPuppetPosition()
        {
            foreach (PlayerFarming farming in players)
            {
                if (farming != null)
                    return farming.transform.position;
            }
            return null;
        }

        /// <summary>
        /// True if the health belongs to the puppet of another player
        /// </summary>
        internal static bool IsPuppet(Health health)
        {
            if (health == null)
                return false;
            foreach (PlayerFarming farming in players)
            {
                if (farming != null && farming.health == health)
                    return true;
            }
            return false;
        }
        private readonly static Vector3?[] targets;
        private readonly static int[] skins;
        private readonly static string[] weapons;
        private readonly static int[] looks;

        /// <summary>
        /// True while a puppet is being made: what it sets up is not ours
        /// </summary>
        internal static bool CreatingPuppet { get; private set; }
        private readonly static Vector3?[] spineBase;

        /// <summary>
        /// Static constructor. Initializes the arrays and starts the move player coroutine.
        /// </summary>
        static PlayerManager()
        {
            players = new PlayerFarming[InternalData.MaxPlayersPerServerInternal];
            targets = new Vector3?[InternalData.MaxPlayersPerServerInternal];
            skins = new int[InternalData.MaxPlayersPerServerInternal];
            weapons = new string[InternalData.MaxPlayersPerServerInternal];
            looks = new int[InternalData.MaxPlayersPerServerInternal];
            spineBase = new Vector3?[InternalData.MaxPlayersPerServerInternal];
            Plugin.MonoInstance.StartCoroutine(MovePlayers());
        }

        /// <summary>
        /// Moves managed players towards their latest network position, never supposed to quit
        /// </summary>
        /// <remarks>
        /// PlayerFarming.GoToAndStop doesn't move a player without a Rewired player
        /// on game version 1.5.26, so the managed players are moved here directly and
        /// only their state machine is set so the walk animation plays.
        /// </remarks>
        private static IEnumerator MovePlayers()
        {
            while (true)
            {
                for (uint i = 0; i < targets.Length; ++i)
                {
                    try
                    {
                        FollowTarget(i);
                        SortAgainstLocal(i);
                    }
                    catch (Exception e)
                    {
                        PrintLogger.Print(DebugLevel.ERROR_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Moving player {i} failed: {e}");
                        targets[i] = null;
                    }
                }
                yield return null;
            }
        }

        /// <summary>
        /// Keeps a puppet clearly in front of or behind our lamb.
        /// </summary>
        /// <remarks>
        /// Both are transparent Spine meshes; standing on the same spot (both players are put at
        /// the door of a new room) they sort part by part and mix into one creature. The one lower
        /// on screen is nearer, so the puppet is nudged a little along the camera's view to match.
        /// The materials write depth and each attachment has its own z step, so the nudge must be
        /// larger than a whole skeleton's spread. Checked with in-game captures: 0.05 still mixed the
        /// two, 0.35 draws each one whole; a negative step along the camera's forward puts it behind.
        /// </remarks>
        private static void SortAgainstLocal(uint plr)
        {
            PlayerFarming farming = players[plr];
            PlayerFarming local = PlayerFarming.Instance;
            Camera camera = Camera.main;
            if (farming == null || farming.Spine == null || local == null || camera == null)
                return;

            Transform spine = farming.Spine.transform;
            if (!spineBase[plr].HasValue)
                spineBase[plr] = spine.localPosition;

            float gap = farming.transform.position.y - local.transform.position.y;
            Vector3 offset = Vector3.zero;
            if (Mathf.Abs(farming.transform.position.x - local.transform.position.x) < 2f && Mathf.Abs(gap) < 2f)
                offset = camera.transform.forward * (gap >= 0f ? -0.35f : 0.35f);

            spine.localPosition = spineBase[plr].Value + (spine.parent != null ? spine.parent.InverseTransformVector(offset) : offset);
        }

        private static void FollowTarget(uint plr)
        {
            PlayerFarming farming = players[plr];
            if (farming == null || !targets[plr].HasValue)
                return;

            Vector3 position = farming.transform.position;
            Vector3 target = targets[plr].Value;
            Vector3 gap = target - position;
            StateMachine state = farming.state;

            if (gap.magnitude > SnapDistance)
            {
                Place(farming, target);
                return;
            }

            /* A puppet that plays its player's own animations (PuppetSync) keeps its state */
            bool driven = PuppetSync.Driving(plr);
            if (gap.magnitude > StopDistance)
            {
                Place(farming, Vector3.Lerp(position, target, 1f - Mathf.Exp(-FollowRate * Time.deltaTime)));
                if (!driven && state != null && (state.CURRENT_STATE == StateMachine.State.Idle || state.CURRENT_STATE == StateMachine.State.Moving))
                {
                    state.CURRENT_STATE = StateMachine.State.Moving;
                    state.facingAngle = state.LookAngle = Mathf.Atan2(gap.y, gap.x) * Mathf.Rad2Deg;
                }
            }
            else
            {
                Place(farming, target);
                targets[plr] = null;
                if (!driven && state != null && state.CURRENT_STATE == StateMachine.State.Moving)
                    state.CURRENT_STATE = StateMachine.State.Idle;
            }
        }

        /// <summary>
        /// Puts a managed player at a point
        /// </summary>
        /// <remarks>
        /// The player's own movement calls Rigidbody2D.MovePosition every frame from where
        /// the body was, which undoes a plain transform change at the next physics step.
        /// Coroutines run after Update, so our MovePosition is the last one and wins.
        /// </remarks>
        private static void Place(PlayerFarming farming, Vector3 point)
        {
            Rigidbody2D body = farming.GetComponent<Rigidbody2D>();

            farming.transform.position = point;
            if (body != null)
            {
                body.position = point;
                body.MovePosition(point);
            }
        }

        /// <summary>
        /// Move a managed player with a given ID
        /// </summary>
        /// <param name="plr">The ID of the player you want to move</param>
        /// <param name="point">The point you want to move the player to</param>
        /// <param name="timeout">0 to place the player there at once, anything else to walk there</param>
        public static void MovePlayer(uint plr, Vector3 point, float timeout)
        {
            if (plr > players.Length - 1 || players[plr] == null)
                return;

            if (timeout <= 0)
            {
                MovePlayerNow(plr, point);
                return;
            }

            targets[plr] = point;
        }

        /// <summary>
        /// Move a managed player with a given ID instantly to the point provided
        /// </summary>
        /// <param name="plr">The ID of the player you want to move</param>
        /// <param name="point">The point you want to move the player to</param>
        public static void MovePlayerNow(uint plr, Vector3 point)
        {
            if (plr > players.Length - 1 || players[plr] == null)
                return;

            Place(players[plr], point);
            targets[plr] = null;
        }

        /// <summary>
        /// Delete every managed player
        /// </summary>
        public static void DeleteAllPlayers()
        {
            for (uint i = 0; i < players.Length; ++i)
            {
                DeletePlayer(i);
            }
        }

        /// <summary>
        /// Delete a managed player
        /// </summary>
        /// <param name="plr">The ID of the managed player you want to delete</param>
        public static void DeletePlayer(uint plr)
        {
            if (plr > players.Length - 1 || players[plr] == null)
                return;

            /* A puppet half torn down by a scene change deletes quietly (seen in the final fight's ending) */
            try
            {
                players[plr].AbortGoTo();
                CoopManager.RemoveCoopPlayerStatic(players[plr], withDelay: false);
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Deleting player {plr} left pieces behind: {e.Message}");
            }
            players[plr] = null;
            targets[plr] = null;
        }

        /// <summary>
        /// Create a managed player under a given ID
        /// </summary>
        /// <param name="id">The ID you want the managed player to have</param>
        /// <param name="pos">The position of the new managed player to be at</param>
        /// <param name="skin">The fleece ID to put on the new player</param>
        /// <remarks>
        /// If the ID is already taken, the existing player is deleted and recreated.
        /// </remarks>
        public static void CreatePlayer(uint id, Vector3 pos = new(), int skin = 0)
        {
            if (id > players.Length - 1)
                return;

            if (players[id] != null)
            {
                DeletePlayer(id);
            }

            /* Setting itself up (on enable too), it would take the HUD and our hearts' data, see CoopHud */
            GameObject plr;
            CreatingPuppet = true;
            try
            {
                plr = GameObject.Instantiate(CoopManager.Instance?.playerPrefab);
            }
            finally
            {
                CreatingPuppet = false;
            }
            if (plr == null)
                return;

            plr.transform.position = pos;

            var farming = plr.GetComponent<PlayerFarming>();
            if (farming == null)
            {
                GameObject.Destroy(plr);
                return;
            }

            try
            {
                /* The game's co-op tells the Lamb and the Goat apart by this (the HUD panel, its icon) */
                farming.isLamb = skin != Appearance.GoatSkin;
                farming.EnableCoopFeatures = false;
                farming.playerID = 1; // afaik the player id here doesn't matter
                CreatingPuppet = true;
                try
                {
                    farming.Init();
                }
                finally
                {
                    CreatingPuppet = false;
                }
                farming.rewiredPlayer = null;
                farming.transform.parent = PlayerFarming.players[0]?.transform.parent;
                plr.SetActive(true);
                farming.Spine.GetComponent<MeshRenderer>()?.enabled = true;

                /*
                 * The other player only shows here. Our enemies can't hurt it, and doors,
                 * room exits and pickups look for the Player tag, so walking into one
                 * mustn't move or reward us.
                 */
                if (farming.health != null)
                    farming.health.invincible = !puppetsTargetable || puppetsDown;
                foreach (Transform part in plr.GetComponentsInChildren<Transform>(true))
                {
                    if (part.CompareTag("Player"))
                        part.gameObject.tag = "Untagged";
                }
            }
            catch (Exception e)
            {
                GameObject.Destroy(plr);
                PrintLogger.Print(DebugLevel.ERROR_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Failed to create additional player: {e.Message}");
                return;
            }

            players[id] = farming;
            skins[id] = skin;
            weapons[id] = null;
            looks[id] = 0;
            spineBase[id] = null;
            ApplySkin(farming, skin);
            CoopHud.Rebuild();

            /* Met again (room change, scene load) while they are down */
            if (puppetsDown)
                ShowDowned(farming, skin == Appearance.GoatSkin, true);
        }

        /// <summary>
        /// The skin number a puppet's player sent, see Appearance
        /// </summary>
        internal static bool TryGetPuppetSkin(PlayerFarming farming, out int skin)
        {
            for (int i = 0; i < players.Length; ++i)
            {
                if (players[i] != null && players[i] == farming)
                {
                    skin = skins[i];
                    return true;
                }
            }
            skin = 0;
            return false;
        }

        /// <summary>
        /// The skin number of a puppet, or -1 if there is no such puppet
        /// </summary>
        internal static int GetLook(uint plr) => plr < players.Length && players[plr] != null ? looks[plr] : -1;

        internal static int GetSkin(uint plr) => plr < players.Length && players[plr] != null ? skins[plr] : -1;

        /// <summary>
        /// The puppet of a player, or null
        /// </summary>
        internal static PlayerFarming GetPuppet(uint plr) => plr < players.Length ? players[plr] : null;

        /// <summary>
        /// Arms a puppet with the weapon its player holds (the skin part named after it)
        /// </summary>
        internal static void SetPuppetWeapon(uint plr, string weapon)
        {
            if (plr > players.Length - 1 || players[plr] == null || weapons[plr] == weapon)
                return;
            weapons[plr] = weapon;
            ApplySkin(players[plr], skins[plr]);
        }

        /// <summary>
        /// Bruises, black and white and the mop as its player's own game shows them, see Appearance.Look
        /// </summary>
        internal static void SetPuppetLook(uint plr, int look)
        {
            if (plr > players.Length - 1 || players[plr] == null || looks[plr] == look)
                return;
            looks[plr] = look;
            ApplySkin(players[plr], skins[plr]);
        }

        /// <summary>
        /// Dresses a puppet as the Lamb in a fleece or as the Goat
        /// </summary>
        internal static Spine.Skin ApplySkin(PlayerFarming farming, int skin)
        {
            var data = farming.Spine.Skeleton.Data;
            var playerskin = farming.PlayerSkin = new Spine.Skin("Player Skin");
            string weaponName = null;
            int look = 0;
            for (int i = 0; i < players.Length; ++i)
            {
                if (players[i] != null && players[i] == farming)
                {
                    weaponName = weapons[i];
                    look = looks[i];
                }
            }
            bool goat = skin == Appearance.GoatSkin;

            /* Dressed as the player's own game dresses them (PlayerFarming.SetSkin), see Appearance.Look */
            Spine.Skin fleece = !goat && Appearance.LookBlackAndWhite(look) ? data.FindSkin($"Lamb_{skin}_BW") : null;
            playerskin.AddSkin(fleece ?? Appearance.FindSkin(data, skin));
            var weapon = data.FindSkin("Weapons/" + (weaponName ?? "Normal")) ?? data.FindSkin("Weapons/Normal");
            if (weapon != null)
                playerskin.AddSkin(weapon);
            int hurt = Appearance.LookHurt(look);
            var bruises = hurt > 0 ? data.FindSkin((goat ? "Goat_Hurt" : "Hurt") + hurt) : null;
            if (bruises != null)
                playerskin.AddSkin(bruises);
            int mops = Appearance.LookMops(look);
            var mop = mops > 0 ? data.FindSkin("Mops/" + mops) : null;
            if (mop != null)
                playerskin.AddSkin(mop);
            farming.Spine.Skeleton.SetSkin(playerskin);
            farming.Spine.Skeleton.SetSlotsToSetupPose();
            return playerskin;
        }

        /// <summary>
        /// Set the visual fleece of a managed player
        /// </summary>
        /// <param name="plr">The ID of the player you want to set the fleece of</param>
        /// <param name="skin">The fleece ID to set the player to</param>
        public static void SetPlayerSkin(uint plr, int skin = 0)
        {
            if (plr > players.Length - 1 || players[plr] == null)
                return;

            if (skins[plr] == skin)
                return;
            skins[plr] = skin;
            players[plr].isLamb = skin != Appearance.GoatSkin;
            ApplySkin(players[plr], skin);
            CoopHud.Rebuild();
        }

        /// <summary>
        /// Check if a managed player exists
        /// </summary>
        /// <param name="plr">The managed player ID</param>
        /// <returns>Whether a managed player exists or not</returns>
        public static bool DoesPlayerExist(uint plr)
        {
            if (plr > players.Length - 1)
                return false;
            return players[plr] != null;
        }

        /// <summary>
        /// Set the state of a given managed player
        /// </summary>
        /// <param name="plr">The ID of the player you want to set the state of</param>
        /// <param name="state">The state you want to set the player to</param>
        /// <param name="isCustomAnimation">Whether if the state you want to set is custom animation</param>
        /// <param name="customAnimation">If you want to set the state to custom animation, the name of the animation</param>
        /// <param name="customAnimationLoop">If you want to set the state to custom animation, whether the animation should loop</param>
        /// <remarks>
        /// If isCustomAnimation is true, the state parameter is ignored
        /// </remarks>
        public static void SetPlayerState(uint plr, StateMachine state, bool isCustomAnimation = false, string customAnimation = null, bool customAnimationLoop = false)
        {
            if (plr > players.Length - 1 || players[plr] == null)
                return;

            PlayerFarming farming = players[plr];
            farming.AbortGoTo();

            /* Its player's animations come through PuppetSync; only which way it looks is taken here */
            if (PuppetSync.Driving(plr))
            {
                if (!isCustomAnimation && state != null && farming.state != null)
                {
                    farming.state.facingAngle = state.facingAngle;
                    farming.state.LookAngle = state.LookAngle;
                }
                return;
            }
            if (isCustomAnimation)
            {
                farming.CustomAnimation(customAnimation, customAnimationLoop);
            }
            else
            {
                farming._state = state;
            }
        }

        /// <summary>
        /// Get the network position a managed player is walking to
        /// </summary>
        /// <param name="plr">The ID of the player</param>
        /// <returns>The target, or null if the player is standing where the network says</returns>
        public static Vector3? GetPlayerTarget(uint plr)
        {
            if (plr > players.Length - 1)
                return null;
            return targets[plr];
        }

        /// <summary>
        /// Get the world position of a managed player
        /// </summary>
        /// <param name="plr">The ID of the player</param>
        /// <returns>The position if the ID is valid. Otherwise, null</returns>
        public static Vector3? GetPlayerPosition(uint plr)
        {
            if (plr > players.Length - 1 || players[plr] == null)
                return null;
            return players[plr].transform.position;
        }

        /// <summary>
        /// This class contains the patches that the PlayerManager class needs to function
        /// </summary>
        [HarmonyPatch]
        private static class PlayerManagerPatches
        {
            /// <summary>
            /// Prevent the managed player from being controlled by the actual player
            /// </summary>
            /// <param name="__instance">The PlayerFarming instance</param>
            [HarmonyPatch(typeof(PlayerFarming), nameof(PlayerFarming.Update))]
            [HarmonyPostfix]
            private static void Update(PlayerFarming __instance)
            {
                int puppet = Array.IndexOf(players, __instance);
                if (puppet >= 0)
                    __instance.rewiredPlayer = null;

                /*
                 * Local co-op marks the Lamb and the Goat with coloured indicators (an accessibility
                 * setting the game only applies in local co-op). Show them in a session too.
                 */
                if (!Plugin.GlobalsInternal.InGameSession || CoopManager.CoopActive)
                    return;
                if (puppet < 0 && __instance != PlayerFarming.Instance)
                    return;
                try
                {
                    bool goat = puppet >= 0 ? skins[puppet] == Appearance.GoatSkin : Appearance.WeAreGoat;
                    bool show = SettingsManager.Settings.Accessibility.CoopVisualIndicators && AnyPuppet();
                    __instance.CoopIndicatorLamb?.gameObject.SetActive(show && !goat);
                    __instance.CoopIndicatorGoat?.gameObject.SetActive(show && goat);
                }
                catch (Exception e)
                {
                    COTLMP.Debug.Faults.Note("The co-op indicator", e);
                }
            }

            /// <summary>
            /// The other player's puppet doesn't look for things to interact with: its prompts
            /// would show on our screen (the partner sees theirs in their own game)
            /// </summary>
            [HarmonyPatch(typeof(Interactor), "Update")]
            [HarmonyPrefix]
            private static bool PuppetInteractor(Interactor __instance)
            {
                PlayerFarming farming = __instance.GetComponent<PlayerFarming>();
                if (farming == null || !players.Contains(farming))
                    return true;
                if (__instance.indicator != null && __instance.indicator.gameObject.activeSelf)
                    __instance.indicator.gameObject.SetActive(false);
                __instance.CurrentInteraction = null;
                return false;
            }

            private static bool AnyPuppet()
            {
                foreach (PlayerFarming farming in players)
                {
                    if (farming != null)
                        return true;
                }
                return false;
            }

            /// <summary>
            /// Prevents the game from adding the managed player to camera focus
            /// </summary>
            /// <param name="g">The gameobject the game is trying to add to the camera focus</param>
            /// <returns>true if the game should continue, false if not</returns>
            [HarmonyPatch(typeof(CameraFollowTarget), nameof(CameraFollowTarget.AddTarget))]
            [HarmonyPrefix]
            private static bool AddTarget(GameObject g)
            {
                PlayerFarming farming = g?.gameObject?.GetComponentInParent<PlayerFarming>(true);
                return farming == null || (farming == PlayerFarming.Instance && !players.Contains(farming)); // for some reason the players check doesnt work here so here's what i made
            }

            /// <summary>
            /// Prevent managed players from triggering a transition
            /// </summary>
            /// <param name="collision">The collider that collided with the transition zone</param>
            /// <returns>false if the collider is one of the managed players, true otherwise</returns>
            [HarmonyPatch(typeof(EnterBuilding), "OnTriggerEnter2D")]
            [HarmonyPrefix]
            private static bool OnTriggerEnter2D(Collider2D collision)
            {
                var farming = collision.GetComponent<PlayerFarming>();
                return !players.Contains(farming);
            }

            /// <summary>
            /// The partner's puppet never becomes the main player: the game's scenes walk its player
            /// list expecting the couch co-op lambs (the final fight's slow walk left the puppet as
            /// PlayerFarming.Instance on the friend's game — controls, icons and camera went with it)
            /// </summary>
            [HarmonyPatch(typeof(PlayerFarming), nameof(PlayerFarming.SetMainPlayer), typeof(PlayerFarming))]
            [HarmonyPrefix]
            private static bool MainPlayer(PlayerFarming playerFarming) =>
                playerFarming == null || !players.Contains(playerFarming);

            /// <summary>
            /// The doors out of a room, to the next floor, the base and the bosses are for the
            /// local player alone: the partner's puppet walking through one would take us along
            /// </summary>
            [HarmonyPatch]
            private static class PuppetDoors
            {
                private static readonly Type[] Doors =
                {
                    typeof(Door), typeof(ChangeLocation), typeof(GraveyardDoor), typeof(Inteaction_DoorRoomDoor),
                    typeof(Interaction_BaseDungeonDoor), typeof(Interaction_BiomeDoor), typeof(Interaction_MysticShop),
                    typeof(Interaction_TempleBossDoor), typeof(NextBiome), typeof(YngyaBossDoor), typeof(DLCOpenMountainMap)
                };

                private static IEnumerable<MethodBase> TargetMethods()
                {
                    foreach (Type Type in Doors)
                    {
                        MethodInfo Method = AccessTools.DeclaredMethod(Type, "OnTriggerEnter2D", new[] { typeof(Collider2D) });
                        if (Method != null)
                            yield return Method;
                    }
                }

                private static bool Prefix(Collider2D __0)
                {
                    if (__0 == null)
                        return true;
                    var farming = __0.GetComponentInParent<PlayerFarming>();

                    /* We wait in this room for the partner (a conversation or a leader): not out of it */
                    if (farming != null && farming == PlayerFarming.Instance && SharedScenes.HoldingRoom)
                    {
                        SharedScenes.DoorTried();
                        return false;
                    }
                    return farming == null || !players.Contains(farming);
                }
            }

            /// <summary>
            /// Destroy all managed players on transition
            /// </summary>
            [HarmonyPatch(typeof(MMTransition), nameof(MMTransition.Play))]
            [HarmonyPostfix]
            private static void MMTransitionPlay()
            {
                for (uint i = 0; i < players.Length; ++i)
                {
                    DeletePlayer(i);
                }
            }
        }
    }
}

/* EOF */
