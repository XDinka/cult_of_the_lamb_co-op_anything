/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     The room owner's enemy shots show in the follower's game
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP.Debug;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// Mirrors enemy projectiles from the room owner to the follower.
    /// </summary>
    /// <remarks>
    /// The follower's enemies don't think, so they never shoot, yet the owner's shots do hit the
    /// follower (through RoomSync). Each new enemy projectile in the owner's game is sent once:
    /// which prefab, where, angle and speed. The follower fires the same prefab from the pool. The
    /// prefabs are found on the follower's own copies of the enemies (the fields their scripts fire
    /// from), so nothing has to be loaded. The copies only show: their damage to the follower is
    /// refused like any other local enemy damage, and they leave pots alone.
    /// </remarks>
    internal static class ProjectileSync
    {
        private static readonly List<Projectile> Fresh = new();
        private static readonly Dictionary<string, GameObject> Prefabs = new();
        private static readonly HashSet<Projectile> Ghosts = new();

        /*
         * Projectiles the game's pools made (they warm up when a room loads), by name. When no enemy
         * script holds the prefab, one of these is copied instead.
         */
        private static readonly Dictionary<string, Projectile> Templates = new();
        private static bool Spawning;

        /* Counters for the test harness report */
        internal static int Sent, Shown, Missing, Seen;

        public static void Initialize()
        {
            CoopSync.EnemyRegistered += (Enemy, _) => Learn(Enemy);
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, _) =>
            {
                Prefabs.Clear();
                Ghosts.Clear();
                Templates.Clear();
            };
            Plugin.MonoInstance.StartCoroutine(Report());
        }

        /// <summary>
        /// Any enemy projectile prefab known in this scene (test harness)
        /// </summary>
        internal static GameObject AnyPrefab()
        {
            foreach (GameObject Prefab in Prefabs.Values)
            {
                if (Prefab != null)
                    return Prefab;
            }
            foreach (Projectile Template in Templates.Values)
            {
                if (Template != null)
                    return Template.gameObject;
            }
            return null;
        }

        private static string Key(GameObject Prefab) => Prefab.name.Replace("(Clone)", "").Trim();

        /// <summary>
        /// Remembers the projectile prefabs an enemy's scripts can fire
        /// </summary>
        private static void Learn(Health Enemy)
        {
            if (Enemy == null)
                return;
            try
            {
                foreach (MonoBehaviour Script in Enemy.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (Script == null || Script is Health)
                        continue;
                    for (Type Kind = Script.GetType(); Kind != null && Kind != typeof(MonoBehaviour); Kind = Kind.BaseType)
                    {
                        foreach (FieldInfo Field in Kind.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        {
                            GameObject Prefab = null;
                            if (Field.FieldType == typeof(GameObject))
                                Prefab = Field.GetValue(Script) as GameObject;
                            else if (typeof(Projectile).IsAssignableFrom(Field.FieldType))
                                Prefab = (Field.GetValue(Script) as Projectile)?.gameObject;

                            if (Prefab != null && Prefab.GetComponent<Projectile>() != null)
                                Prefabs[Key(Prefab)] = Prefab;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Looking for {Enemy.name}'s projectiles failed: {e.Message}");
            }
        }

        /// <summary>
        /// Sends the owner's new enemy shots, one frame after they appeared (the shooter sets their
        /// position, angle and speed right after taking them from the pool)
        /// </summary>
        private static IEnumerator Report()
        {
            List<Projectile> Batch = new();

            while (true)
            {
                yield return null;
                if (Fresh.Count == 0)
                    continue;

                Batch.Clear();
                Batch.AddRange(Fresh);
                Fresh.Clear();
                if (!RoomSync.Owning)
                    continue;

                foreach (Projectile Shot in Batch)
                {
                    try
                    {
                        if (Shot == null || !Shot.gameObject.activeInHierarchy || Shot.team != Health.Team.Team2)
                            continue;

                        string Name = Key(Shot.gameObject);
                        Vector3 Position = Shot.transform.position;
                        float Angle = Shot.Angle, Speed = Shot.Speed * Shot.SpeedMultiplier, Life = Shot.LifeTime;
                        string Place = CoopSync.CurrentPlace() ?? "";
                        ++Sent;
                        CoopSync.Send(CoopSync.Kind.Projectile, Writer =>
                        {
                            Writer.Write(Place);
                            Writer.Write(Name);
                            Writer.Write(Position.x);
                            Writer.Write(Position.y);
                            Writer.Write(Position.z);
                            Writer.Write(Angle);
                            Writer.Write(Speed);
                            Writer.Write(Life);
                        });
                    }
                    catch (Exception e)
                    {
                        PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Sending a shot failed: {e.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Fires the owner's shot in our game, for show.
        /// </summary>
        public static void OnRelay(BinaryReader Reader)
        {
            string Place = Reader.ReadString(), Name = Reader.ReadString();
            Vector3 Position = new(Reader.ReadSingle(), Reader.ReadSingle(), Reader.ReadSingle());
            float Angle = Reader.ReadSingle(), Speed = Reader.ReadSingle(), Life = Reader.ReadSingle();

            if (!RoomSync.Following || Place != CoopSync.CurrentPlace())
                return;
            Prefabs.TryGetValue(Name, out GameObject Prefab);
            Templates.TryGetValue(Name, out Projectile Template);
            if (Prefab == null && Template == null)
            {
                if (Missing++ < 5)
                    Plugin.Logger?.LogInfo($"[COOP] No prefab for the owner's shot {Name}");
                return;
            }

            Spawning = true;
            try
            {
                Transform Parent = GenerateRoomParent();
                Projectile Shot;
                if (Prefab != null)
                {
                    Shot = ObjectPool.Spawn(Prefab, Parent).GetComponent<Projectile>();
                }
                else
                {
                    Shot = UnityEngine.Object.Instantiate(Template.gameObject, Parent).GetComponent<Projectile>();
                    Shot.gameObject.SetActive(true);
                    UnityEngine.Object.Destroy(Shot.gameObject, Mathf.Max(1f, Life) + 2f);
                }
                Shot.transform.position = Position;
                Shot.Angle = Angle;
                Shot.Speed = Speed;
                Shot.SpeedMultiplier = 1f;
                Shot.LifeTime = Life;
                Shot.team = Health.Team.Team2;
                Shot.DamageToNeutral = 0f;
                Ghosts.Add(Shot);
                ++Shown;
            }
            catch (Exception e)
            {
                PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Showing the owner's shot failed: {e.Message}");
            }
            finally
            {
                Spawning = false;
            }
        }

        private static Transform GenerateRoomParent()
        {
            var Room = MMRoomGeneration.GenerateRoom.Instance;
            return Room != null ? Room.transform : null;
        }

        /// <summary>
        /// True for a shot we fired only to show the owner's
        /// </summary>
        internal static bool IsGhost(GameObject Attacker)
        {
            if (Attacker == null || Ghosts.Count == 0)
                return false;
            Projectile Shot = Attacker.GetComponent<Projectile>();
            return Shot != null && Ghosts.Contains(Shot);
        }

        [HarmonyPatch]
        private static class ProjectileSyncPatches
        {
            [HarmonyPatch(typeof(Projectile), "OnEnable")]
            [HarmonyPostfix]
            private static void OnEnable(Projectile __instance)
            {
                if (Spawning)
                    return;
                if (__instance != null && !Ghosts.Contains(__instance))
                    Templates[Key(__instance.gameObject)] = __instance;
                if (!CoopSync.InSession)
                    return;
                ++Seen;
                Ghosts.Remove(__instance);
                if (RoomSync.Owning)
                    Fresh.Add(__instance);
            }

            [HarmonyPatch(typeof(Projectile), "OnDisable")]
            [HarmonyPostfix]
            private static void OnDisable(Projectile __instance)
            {
                if (Ghosts.Count != 0)
                    Ghosts.Remove(__instance);
            }
        }
    }
}

/* EOF */
