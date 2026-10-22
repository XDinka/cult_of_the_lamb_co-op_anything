/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     The room owner's enemy attacks (shots, beams, area attacks) show for the follower
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
    /// Mirrors what the owner's enemies put into a room, the way it moves there.
    /// </summary>
    /// <remarks>
    /// The follower's enemies don't think, so they never attack, while the owner's attacks do hit
    /// the follower (RoomSync). Enemy attacks that are objects of their own (projectiles, homing
    /// shots, beams, bombs, warning circles, shockwaves) come out of the game's object pool. Each one
    /// the owner's game takes from the pool is sent once with its prefab name, then its position,
    /// rotation and scale ten times a second until it goes back. The follower takes the same prefab
    /// from its own pool and moves the copy to match, so homing, turning and growing all look the
    /// same. The copies never hurt anyone: not the follower, not the enemies, not the pots.
    /// </remarks>
    internal static class HazardSync
    {
        private sealed class Tracked
        {
            public int Id;
            public GameObject Thing;
            public float Since;
        }

        private sealed class Ghost
        {
            public GameObject Thing;
            public Vector3 Position;
            public float Rotation;
            public Vector3 Scale;
            public float LastSeen;
        }

        private const float Interval = 0.1f;

        /// <summary>
        /// Pool objects with these in the name are enemy attacks even without a projectile or collider
        /// </summary>
        private static readonly string[] AttackWords =
        {
            "indicator", "warning", "target", "bomb", "grenade", "trap", "spike", "beam", "laser", "explosion",
            "shockwave", "slam", "tentacle", "fire", "flame", "ice", "poison", "goop", "acid", "bullet", "arrow",
            "projectile", "orb", "wave", "ring", "circle", "aoe", "blast", "spit", "web", "lightning"
        };

        /* Owner */
        private static readonly List<Tracked> Live = new();
        private static readonly List<GameObject> Fresh = new();
        private static int NextId = 1;

        /* Follower */
        private static readonly Dictionary<int, Ghost> Ghosts = new();
        private static readonly HashSet<GameObject> GhostObjects = new();
        private static readonly Dictionary<string, GameObject> Prefabs = new();
        private static bool Spawning;

        /* Counters for the test harness report */
        internal static int Sent, Shown, Missing, Moved;

        public static void Initialize()
        {
            CoopSync.EnemyRegistered += (Enemy, _) => Learn(Enemy);
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, _) =>
            {
                Prefabs.Clear();
                Ghosts.Clear();
                GhostObjects.Clear();
                Live.Clear();
                Fresh.Clear();
            };
            MMBiomeGeneration.BiomeGenerator.OnBiomeChangeRoom += () =>
            {
                foreach (Ghost Copy in Ghosts.Values)
                    Remove(Copy);
                Ghosts.Clear();
                GhostObjects.Clear();
                Live.Clear();
            };
            Plugin.MonoInstance.StartCoroutine(OwnerLoop());
            Plugin.MonoInstance.StartCoroutine(FollowerLoop());
        }

        private static string Key(GameObject Thing) => Thing.name.Replace("(Clone)", "").Trim();

        /// <summary>
        /// Remembers the prefabs an enemy's scripts can spawn (its fields)
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
                            else if (typeof(Component).IsAssignableFrom(Field.FieldType))
                                Prefab = (Field.GetValue(Script) as Component)?.gameObject;

                            /* Only assets, not objects in the scene */
                            if (Prefab != null && !Prefab.scene.IsValid())
                                Prefabs[Key(Prefab)] = Prefab;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Looking for {Enemy.name}'s attacks failed: {e.Message}");
            }
        }

        /// <summary>
        /// The prefab for a name: from an enemy's fields, or one the pool already holds
        /// </summary>
        private static GameObject FindPrefab(string Name)
        {
            if (Prefabs.TryGetValue(Name, out GameObject Prefab) && Prefab != null)
                return Prefab;
            try
            {
                foreach (GameObject Pooled in ObjectPool.instance.pooledObjects.Keys)
                {
                    if (Pooled != null && Key(Pooled) == Name)
                    {
                        Prefabs[Name] = Pooled;
                        return Pooled;
                    }
                }
            }
            catch (Exception e)
            {
                PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Searching the pool failed: {e.Message}");
            }
            return null;
        }

        /* OWNER **************************************************************/

        /// <summary>
        /// True for something the pool gave out that is an enemy attack
        /// </summary>
        private static bool IsAttack(GameObject Thing)
        {
            if (Thing == null || !Thing.activeInHierarchy || GhostObjects.Contains(Thing))
                return false;
            if (Thing.GetComponent<UnitObject>() != null || Thing.GetComponent<PickUp>() != null || Thing.GetComponent<PlayerFarming>() != null)
                return false;

            Projectile Shot = Thing.GetComponent<Projectile>();
            if (Shot != null)
                return Shot.team == Health.Team.Team2;

            string Name = Thing.name.ToLowerInvariant();
            foreach (string Word in AttackWords)
            {
                if (Name.Contains(Word))
                    return true;
            }
            return Thing.GetComponentInChildren<Collider2D>(true) != null && Thing.GetComponentInChildren<Health>(true) == null;
        }

        private static IEnumerator OwnerLoop()
        {
            var Wait = new WaitForSecondsRealtime(Interval);
            List<GameObject> Batch = new();

            while (true)
            {
                yield return Wait;
                if (!RoomSync.Owning)
                {
                    Live.Clear();
                    Fresh.Clear();
                    continue;
                }

                try
                {
                    string Place = CoopSync.CurrentPlace() ?? "";

                    /* New ones: spawned at least a frame ago, so their shooter has set them up */
                    Batch.Clear();
                    Batch.AddRange(Fresh);
                    Fresh.Clear();
                    foreach (GameObject Thing in Batch)
                    {
                        if (!IsAttack(Thing))
                            continue;
                        Tracked Entry = new() { Id = NextId++, Thing = Thing, Since = Time.time };
                        Live.Add(Entry);
                        Transform T = Thing.transform;
                        string Name = Key(Thing);
                        ++Sent;
                        CoopSync.Send(CoopSync.Kind.HazardSpawn, Writer =>
                        {
                            Writer.Write(Place);
                            Writer.Write(Entry.Id);
                            Writer.Write(Name);
                            Write(Writer, T);
                        });
                    }

                    /* Gone ones */
                    for (int Index = Live.Count - 1; Index >= 0; Index--)
                    {
                        Tracked Entry = Live[Index];
                        if (Entry.Thing != null && Entry.Thing.activeInHierarchy)
                            continue;
                        Live.RemoveAt(Index);
                        int Id = Entry.Id;
                        CoopSync.Send(CoopSync.Kind.HazardGone, Writer => Writer.Write(Id));
                    }

                    /* Where the live ones are */
                    for (int Start = 0; Start < Live.Count; Start += 30)
                    {
                        int End = Math.Min(Live.Count, Start + 30);
                        CoopSync.Send(CoopSync.Kind.HazardMove, Writer =>
                        {
                            Writer.Write(Place);
                            Writer.Write((byte)(End - Start));
                            for (int Index = Start; Index < End; Index++)
                            {
                                Writer.Write(Live[Index].Id);
                                Write(Writer, Live[Index].Thing.transform);
                            }
                        });
                    }
                }
                catch (Exception e)
                {
                    PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Sending enemy attacks failed: {e.Message}");
                }
            }
        }

        private static void Write(BinaryWriter Writer, Transform T)
        {
            Vector3 Position = T.position, Scale = T.lossyScale;
            Writer.Write(Position.x);
            Writer.Write(Position.y);
            Writer.Write(Position.z);
            Writer.Write(T.eulerAngles.z);
            Writer.Write(Scale.x);
            Writer.Write(Scale.y);
        }

        private static (Vector3, float, Vector3) Read(BinaryReader Reader)
        {
            Vector3 Position = new(Reader.ReadSingle(), Reader.ReadSingle(), Reader.ReadSingle());
            float Rotation = Reader.ReadSingle();
            Vector3 Scale = new(Reader.ReadSingle(), Reader.ReadSingle(), 1f);
            return (Position, Rotation, Scale);
        }

        /* FOLLOWER ***********************************************************/

        /// <summary>
        /// Handles the owner's attack messages.
        /// </summary>
        public static void OnRelay(CoopSync.Kind Type, BinaryReader Reader)
        {
            switch (Type)
            {
                case CoopSync.Kind.HazardSpawn:
                {
                    string Place = Reader.ReadString();
                    int Id = Reader.ReadInt32();
                    string Name = Reader.ReadString();
                    var (Position, Rotation, Scale) = Read(Reader);
                    if (RoomSync.Following && Place == CoopSync.CurrentPlace())
                        Spawn(Id, Name, Position, Rotation, Scale);
                    break;
                }

                case CoopSync.Kind.HazardMove:
                {
                    string Place = Reader.ReadString();
                    int Count = Reader.ReadByte();
                    bool Use = RoomSync.Following && Place == CoopSync.CurrentPlace();
                    for (int Index = 0; Index < Count; Index++)
                    {
                        int Id = Reader.ReadInt32();
                        var (Position, Rotation, Scale) = Read(Reader);
                        if (Use && Ghosts.TryGetValue(Id, out Ghost Copy))
                        {
                            Copy.Position = Position;
                            Copy.Rotation = Rotation;
                            Copy.Scale = Scale;
                            Copy.LastSeen = Time.time;
                            ++Moved;
                        }
                    }
                    break;
                }

                case CoopSync.Kind.HazardGone:
                {
                    int Id = Reader.ReadInt32();
                    if (Ghosts.TryGetValue(Id, out Ghost Copy))
                    {
                        Ghosts.Remove(Id);
                        Remove(Copy);
                    }
                    break;
                }
            }
        }

        private static void Spawn(int Id, string Name, Vector3 Position, float Rotation, Vector3 Scale)
        {
            GameObject Prefab = FindPrefab(Name);
            if (Prefab == null)
            {
                if (Missing++ < 10)
                    Plugin.Logger?.LogInfo($"[COOP] No prefab for the owner's {Name}");
                return;
            }

            Spawning = true;
            try
            {
                Transform Parent = MMRoomGeneration.GenerateRoom.Instance != null ? MMRoomGeneration.GenerateRoom.Instance.transform : null;
                GameObject Thing = ObjectPool.Spawn(Prefab, Parent);
                GhostObjects.Add(Thing);
                Place(Thing.transform, Position, Rotation, Scale);

                /* It moves where the owner's one moves, not on its own */
                Projectile Shot = Thing.GetComponent<Projectile>();
                if (Shot != null)
                {
                    Shot.team = Health.Team.Team2;
                    Shot.Speed = 0f;
                    Shot.DamageToNeutral = 0f;
                    Shot.LifeTime = 60f;
                }

                Ghosts[Id] = new Ghost { Thing = Thing, Position = Position, Rotation = Rotation, Scale = Scale, LastSeen = Time.time };
                ++Shown;
            }
            catch (Exception e)
            {
                PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Showing the owner's {Name} failed: {e.Message}");
            }
            finally
            {
                Spawning = false;
            }
        }

        private static void Place(Transform T, Vector3 Position, float Rotation, Vector3 Scale)
        {
            T.position = Position;
            Vector3 Angles = T.eulerAngles;
            Angles.z = Rotation;
            T.eulerAngles = Angles;
            Vector3 Parent = T.parent != null ? T.parent.lossyScale : Vector3.one;
            T.localScale = new Vector3(Parent.x != 0f ? Scale.x / Parent.x : Scale.x, Parent.y != 0f ? Scale.y / Parent.y : Scale.y, T.localScale.z);
        }

        private static void Remove(Ghost Copy)
        {
            if (Copy?.Thing == null)
                return;
            GhostObjects.Remove(Copy.Thing);
            try
            {
                Copy.Thing.Recycle();
            }
            catch
            {
                UnityEngine.Object.Destroy(Copy.Thing);
            }
        }

        private static IEnumerator FollowerLoop()
        {
            List<int> Stale = new();
            while (true)
            {
                yield return null;
                if (Ghosts.Count == 0)
                    continue;

                Stale.Clear();
                foreach (var Pair in Ghosts)
                {
                    Ghost Copy = Pair.Value;
                    /* Lost its end message, or the owner left the room */
                    if (Copy.Thing == null || !Copy.Thing.activeInHierarchy || Time.time - Copy.LastSeen > 2f || !RoomSync.Following)
                    {
                        Stale.Add(Pair.Key);
                        continue;
                    }
                    Transform T = Copy.Thing.transform;
                    float Blend = 1f - Mathf.Exp(-20f * Time.deltaTime);
                    Vector3 Next = (Copy.Position - T.position).magnitude > 3f ? Copy.Position : Vector3.Lerp(T.position, Copy.Position, Blend);
                    Place(T, Next, Mathf.LerpAngle(T.eulerAngles.z, Copy.Rotation, Blend), Copy.Scale);
                }
                foreach (int Id in Stale)
                {
                    Remove(Ghosts[Id]);
                    Ghosts.Remove(Id);
                }
            }
        }

        /// <summary>
        /// True if the attacker is one of our show-only copies (or part of one)
        /// </summary>
        internal static bool IsGhost(GameObject Attacker)
        {
            if (Attacker == null || GhostObjects.Count == 0)
                return false;
            for (Transform T = Attacker.transform; T != null; T = T.parent)
            {
                if (GhostObjects.Contains(T.gameObject))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Any enemy attack prefab known in this scene (test harness)
        /// </summary>
        internal static GameObject AnyProjectilePrefab()
        {
            foreach (GameObject Prefab in Prefabs.Values)
            {
                if (Prefab != null && Prefab.GetComponent<Projectile>() != null)
                    return Prefab;
            }
            try
            {
                foreach (GameObject Pooled in ObjectPool.instance.pooledObjects.Keys)
                {
                    if (Pooled != null && Pooled.GetComponent<Projectile>() != null)
                        return Pooled;
                }
            }
            catch
            {
            }
            return null;
        }

        [HarmonyPatch]
        private static class HazardSyncPatches
        {
            /// <summary>
            /// Everything the pool hands out passes here; the owner looks at it next tick
            /// </summary>
            [HarmonyPatch(typeof(ObjectPool), nameof(ObjectPool.Spawn), new[] { typeof(GameObject), typeof(Transform), typeof(Vector3), typeof(Quaternion), typeof(bool), typeof(bool) })]
            [HarmonyPostfix]
            private static void Spawned(GameObject __result)
            {
                if (!Spawning && __result != null && RoomSync.Owning)
                    Fresh.Add(__result);
            }

            /// <summary>
            /// Our show-only copies hurt nobody
            /// </summary>
            [HarmonyPatch(typeof(Health), nameof(Health.DealDamage))]
            [HarmonyPrefix]
            private static bool DealDamage(GameObject Attacker, ref bool __result)
            {
                if (GhostObjects.Count != 0 && IsGhost(Attacker))
                {
                    __result = false;
                    return false;
                }
                return true;
            }
        }
    }
}

/* EOF */
