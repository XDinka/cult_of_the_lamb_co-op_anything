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
        private readonly static Vector3?[] targets;

        /// <summary>
        /// Static constructor. Initializes the arrays and starts the move player coroutine.
        /// </summary>
        static PlayerManager()
        {
            players = new PlayerFarming[InternalData.MaxPlayersPerServerInternal];
            targets = new Vector3?[InternalData.MaxPlayersPerServerInternal];
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

            if (gap.magnitude > StopDistance)
            {
                Place(farming, Vector3.Lerp(position, target, 1f - Mathf.Exp(-FollowRate * Time.deltaTime)));
                if (state != null && (state.CURRENT_STATE == StateMachine.State.Idle || state.CURRENT_STATE == StateMachine.State.Moving))
                {
                    state.CURRENT_STATE = StateMachine.State.Moving;
                    state.facingAngle = state.LookAngle = Mathf.Atan2(gap.y, gap.x) * Mathf.Rad2Deg;
                }
            }
            else
            {
                Place(farming, target);
                targets[plr] = null;
                if (state != null && state.CURRENT_STATE == StateMachine.State.Moving)
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

            players[plr].AbortGoTo();
            CoopManager.RemoveCoopPlayerStatic(players[plr], withDelay: false);
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

            GameObject plr = GameObject.Instantiate(CoopManager.Instance?.playerPrefab);
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
                farming.isLamb = true;
                farming.EnableCoopFeatures = false;
                farming.playerID = 1; // afaik the player id here doesn't matter
                farming.Init();
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
                    farming.health.invincible = true;
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
            var playerskin = farming.PlayerSkin = new Spine.Skin("Player Skin");
            playerskin.AddSkin(farming.Spine.Skeleton.Data.FindSkin($"Lamb_{skin}"));
            farming.Spine.Skeleton.SetSkin(playerskin);
            farming.Spine.Skeleton.SetToSetupPose();
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

            PlayerFarming farming = players[plr];
            var plrskin = farming.PlayerSkin = new Spine.Skin("Player Skin");
            plrskin.AddSkin(farming.Spine.Skeleton.Data.FindSkin($"Lamb_{skin}"));
            farming.Spine.skeleton.SetSkin(plrskin);
            farming.Spine.skeleton.SetToSetupPose();
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
        /// Get the state of a managed player
        /// </summary>
        /// <param name="plr">The ID of the player you want to get the state of</param>
        /// <returns>The StateMachine of the player if the ID is valid. Otherwise, null</returns>
        public static StateMachine GetPlayerState(uint plr)
        {
            if (plr > players.Length - 1 || players[plr] == null)
                return null;
            return players[plr].state;
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
                if (players.Contains(__instance))
                    __instance.rewiredPlayer = null;
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
