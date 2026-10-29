/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     One player runs the enemies of a dungeon room, the other follows
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP.Debug;
using HarmonyLib;
using Spine.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// Room ownership for dungeon rooms both players are in.
    /// </summary>
    /// <remarks>
    /// The player who was in the room first owns it. The owner's game runs the enemies as usual,
    /// and they may also go for the partner's puppet; a hit on the puppet is sent to the partner,
    /// who takes it on their own lamb (their dodge and i-frames still count). The follower's copies
    /// of the enemies stop thinking: they are moved to where the owner's enemies are and play the
    /// owner's animations, and they can't hurt the follower. Follower hits on them still count,
    /// through CoopSync's shared enemy health. Enemies the owner doesn't have are removed, so a
    /// room that came out different (another dungeon modifier, say) ends up like the owner's.
    /// Alone in a room, or outside dungeons, everyone runs their own game.
    /// </remarks>
    internal static class RoomSync
    {
        private sealed class Followed
        {
            public Health Enemy;
            public Rigidbody2D Body;
            public SkeletonAnimation Spine;
            public UnitObject[] Units;
            /* A leader's units, whose own death (the fall, the heart, the room opening) still runs while frozen */
            public List<UnitObject> Leaders = new();
            public Vector3? Target;
            /* The model's own height above the unit (spawn drops, flyers), from the owner */
            public float? SpineZ;
            public float ScaleX = 1f;
            public string Animation;
            public bool Loop;
            public float LastSeen;
            public float Registered;
            public bool Frozen;
            public bool SeenOnce;
            public MeshRenderer Renderer;
        }

        /// <summary>
        /// Enemies come in with their own spawn animation (out of the ground, down from above),
        /// run by their own scripts. They are only taken over once that is done.
        /// </summary>
        private const float SettleTime = 1.5f, MaxSettleTime = 6f;

        private const float SnapshotInterval = 0.1f;
        private const float FollowRate = 14f;
        /// <summary>
        /// An enemy the owner hasn't reported for this long is gone on the owner's side
        /// </summary>
        private const float MissingTimeout = 4f, NeverSeenTimeout = 10f;

        /* Where the partner is and whether they say they own it */
        internal static string PartnerPlace { get; private set; }

        /// <summary>
        /// Since when the partner says they are between places (loading), or -1
        /// </summary>
        internal static float PartnerLoadingSince { get; private set; } = -1f;

        /// <summary>
        /// Where the partner was last seen standing; a room change in between doesn't clear it
        /// </summary>
        internal static string PartnerLastPlace { get; private set; }
        private static bool PartnerOwns;
        private static string MyPlace;

        /// <summary>
        /// The session is over: nothing of it is to steer the next one
        /// </summary>
        internal static void ResetSession()
        {
            PartnerOwns = false;
        }

        private static bool IOwn = true;

        private static readonly Dictionary<Health, Followed> FollowedEnemies = new();
        private static readonly Dictionary<int, Followed> FollowedByHash = new();
        private static float FollowingSince;
        private static float LastHitSent;
        private static bool ApplyingHit;

        /* Counters for the test harness report */
        private static int HitsRefused;
        internal static int SnapshotsSent, SnapshotsApplied, PuppetHitsSent, HitsTaken, EnemiesRemoved, EnemyAnimations;
        /* Rounds of the follow loop that threw (a test fault); FollowThrow: the next round throws once (-cotlmp-followthrow) */
        internal static int LoopFaults;
        internal static bool FollowThrow;

        /* The partner's hit the game holds back for a moment (a dodge in time still clears it) */
        private static object RemoteHitQueued;

        /// <summary>
        /// True while we run the enemies of a dungeon room the partner is also in
        /// </summary>
        internal static int FollowedCount => FollowedEnemies.Count;

        internal static bool Owning => CoopSync.InSession && SharedRoom && IOwn;

        /// <summary>
        /// True while the partner runs the enemies of the dungeon room we are in
        /// </summary>
        internal static bool Following => CoopSync.InSession && SharedRoom && !IOwn;

        private static bool SharedRoom => MyPlace != null && MyPlace.Contains("#") && MyPlace == PartnerPlace;

        /// <summary>
        /// Starts the update loops and hooks.
        /// </summary>
        public static void Initialize()
        {
            CoopSync.EnemyRegistered += (Enemy, Id) =>
            {
                if (Following)
                    Follow(Enemy, Id);
            };
            Plugin.MonoInstance.StartCoroutine(PlaceLoop());
            Plugin.MonoInstance.StartCoroutine(SnapshotLoop());
            Plugin.MonoInstance.StartCoroutine(FollowLoop());
        }

        private static int Hash(string Id)
        {
            unchecked
            {
                uint Value = 2166136261;
                foreach (char C in Id)
                    Value = (Value ^ C) * 16777619;
                return (int)Value;
            }
        }

        /* PLACE AND OWNERSHIP ************************************************/

        private static IEnumerator PlaceLoop()
        {
            var Wait = new WaitForSecondsRealtime(0.25f);
            float NextAnnounce = 0f;

            while (true)
            {
                yield return Wait;
                try
                {
                    if (!CoopSync.InSession)
                    {
                        PartnerPlace = null;
                        PartnerLastPlace = null;
                        SetPlace(null);
                        continue;
                    }

                    string Place = MMTools.MMTransition.IsPlaying ? null : CoopSync.CurrentPlace();
                    if (Place != MyPlace)
                    {
                        SetPlace(Place);
                        NextAnnounce = 0f;
                    }

                    if (Time.realtimeSinceStartup >= NextAnnounce)
                    {
                        NextAnnounce = Time.realtimeSinceStartup + 2f;
                        Announce();
                    }
                }
                catch (Exception e)
                {
                    PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Room sync failed: {e}");
                }
            }
        }

        /// <summary>
        /// We arrived somewhere. If the partner is already there, the room is theirs.
        /// </summary>
        private static void SetPlace(string Place)
        {
            MyPlace = Place;
            IOwn = Place == null || Place != PartnerPlace || !PartnerOwns;
            Refresh();
        }

        private static void Announce()
        {
            /* On the way back to the host the rooms we pass aren't ours to run */
            if (Rejoin.Going)
                return;
            CoopSync.Send(CoopSync.Kind.Place, Writer =>
            {
                Writer.Write(MyPlace ?? "");
                Writer.Write(IOwn);
            });
        }

        private static void OnPlace(BinaryReader Reader)
        {
            string Place = Reader.ReadString();
            bool Owns = Reader.ReadBoolean();
            bool Arrived = Place != PartnerPlace;

            PartnerPlace = string.IsNullOrEmpty(Place) ? null : Place;
            if (PartnerPlace != null)
                PartnerLoadingSince = -1f;
            else if (PartnerLoadingSince < 0f)
                PartnerLoadingSince = Time.realtimeSinceStartup;
            PartnerOwns = Owns;
            if (PartnerPlace != null)
                PartnerLastPlace = PartnerPlace;
            MiniMapSync.PartnerAt(PartnerPlace);
            if (Arrived)
                Plugin.Logger?.LogInfo($"[COOP] Partner is in {(PartnerPlace ?? "a transition")}{(Owns ? " (theirs)" : "")}, we are in {MyPlace ?? "a transition"}");

            if (SharedRoom)
            {
                /* Both came in at once and both think it's theirs: the host keeps it */
                if (IOwn && Owns)
                    IOwn = Plugin.GlobalsInternal.IsServerCreator;
                /* Both think it's the other's (the owner left and came back): the host takes it */
                else if (!IOwn && !Owns)
                    IOwn = Plugin.GlobalsInternal.IsServerCreator;
            }
            else
            {
                IOwn = true;
            }

            if (Arrived)
                Announce();
            Refresh();
        }

        private static bool WasOwning, WasFollowing;

        /// <summary>
        /// Applies a change of role: puppets become targets for an owner, enemies freeze for a follower.
        /// </summary>
        private static void Refresh()
        {
            bool Own = Owning, Follow = Following;

            if (Own != WasOwning || Follow != WasFollowing)
                Plugin.Logger?.LogInfo($"[COOP] Room {MyPlace}: {(Own ? "we run the enemies" : Follow ? "the partner runs the enemies" : "on our own")}");

            PlayerManager.PuppetsTargetable = Own;

            if (Follow && !WasFollowing)
            {
                FollowingSince = Time.realtimeSinceStartup;
                foreach (var Pair in new List<KeyValuePair<Health, string>>(CoopSync.Enemies))
                {
                    if (Pair.Key != null && Pair.Value.StartsWith(MyPlace + "|", StringComparison.Ordinal))
                        RoomSync.Follow(Pair.Key, Pair.Value);
                }
            }
            else if (!Follow && WasFollowing)
            {
                ReleaseAll();
            }

            WasOwning = Own;
            WasFollowing = Follow;
        }

        /* FOLLOWER ***********************************************************/

        /// <summary>
        /// Stops our copy of an enemy from acting on its own.
        /// </summary>
        private static void Follow(Health Enemy, string Id)
        {
            if (Enemy == null || FollowedEnemies.ContainsKey(Enemy) || !Id.StartsWith(MyPlace + "|", StringComparison.Ordinal))
                return;

            /* Pots and other breakables only share their health; units are the ones that move and attack */
            if (Enemy.GetComponent<UnitObject>() == null)
                return;

            Followed Entry = new()
            {
                Enemy = Enemy,
                Body = Enemy.GetComponent<Rigidbody2D>(),
                Spine = MainSpine(Enemy.gameObject),
                Units = Enemy.GetComponents<UnitObject>(),
                LastSeen = Time.realtimeSinceStartup,
                Registered = Time.realtimeSinceStartup
            };
            Entry.Renderer = Entry.Spine != null ? Entry.Spine.GetComponent<MeshRenderer>() : null;

            FollowedEnemies[Enemy] = Entry;
            FollowedByHash[Hash(Id)] = Entry;
        }

        /// <summary>
        /// The skeleton an enemy's animator plays on (some carry other skeletons for effects and
        /// warnings, which the first one found may be): the same pick on both sides
        /// </summary>
        private static SkeletonAnimation MainSpine(GameObject Enemy)
        {
            SimpleSpineAnimator Animator = Enemy.GetComponentInChildren<SimpleSpineAnimator>();
            if (Animator != null && Animator.anim != null)
                return Animator.anim;
            return Enemy.GetComponentInChildren<SkeletonAnimation>() ?? Enemy.GetComponentInChildren<SkeletonAnimation>(true);
        }

        /// <summary>
        /// Stops an enemy's own scripts once it is fully in the room
        /// </summary>
        private static bool TryFreeze(Followed Entry, float Now)
        {
            if (Entry.Frozen)
                return true;

            float Age = Now - Entry.Registered;
            bool Visible = Entry.Renderer == null || (Entry.Renderer.enabled && Entry.Enemy.gameObject.activeInHierarchy);
            bool Spawning = Entry.Enemy.state != null && (Entry.Enemy.state.CURRENT_STATE == StateMachine.State.SpawnIn || Entry.Enemy.state.CURRENT_STATE == StateMachine.State.InActive);
            if (Age < SettleTime || ((!Visible || Spawning) && Age < MaxSettleTime))
                return false;

            foreach (UnitObject Unit in Entry.Units)
            {
                try
                {
                    Unit.StopAllCoroutines();
                    Unit.enabled = false;
                    /* Disabled, a unit stops hearing its health's death; a leader's death is its whole ending */
                    if (IsLeader(Unit))
                    {
                        Entry.Enemy.OnDie += Unit.OnDie;
                        Entry.Leaders.Add(Unit);
                    }
                }
                catch (Exception e)
                {
                    PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Can't stop {Entry.Enemy.name}: {e.Message}");
                }
            }
            if (Entry.Renderer != null)
                Entry.Renderer.enabled = true;
            /* Its animations come from the owner now: its animator mustn't put its own back, on state changes or every frame */
            foreach (SimpleSpineAnimator Animator in Entry.Enemy.GetComponentsInChildren<SimpleSpineAnimator>(true))
            {
                Animator.UpdateAnimsOnStateChange = false;
                Animator.enabled = false;
            }
            Entry.Frozen = true;
            return true;
        }

        /// <summary>
        /// A land's leader the way the game tells them (its tarot and relics spare them). In the final
        /// fight every actor counts: each one's own death is what carries the fight to its next stage,
        /// and the guardians stand beside the fight's controller, not under it
        /// </summary>
        private static bool IsLeader(UnitObject Unit) =>
            Unit.IsBoss || Unit.GetComponentInParent<BossIntro>() != null || Unit.GetComponentInParent<DeathCatController>() != null ||
            PlayerFarming.Location == FollowerLocation.Boss_5;

        /// <summary>
        /// Gives the enemies back their own minds, when the owner left or we became the owner.
        /// </summary>
        private static void ReleaseAll()
        {
            foreach (Followed Entry in FollowedEnemies.Values)
            {
                if (Entry.Enemy == null || !Entry.Frozen)
                    continue;
                /* Enabled again, they hear their death themselves */
                foreach (UnitObject Unit in Entry.Leaders)
                {
                    if (Unit != null)
                        Entry.Enemy.OnDie -= Unit.OnDie;
                }
                Entry.Leaders.Clear();
                foreach (UnitObject Unit in Entry.Units)
                {
                    try
                    {
                        if (Unit != null)
                            Unit.enabled = true;
                    }
                    catch (Exception e)
                    {
                        PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Can't restart {Entry.Enemy.name}: {e.Message}");
                    }
                }
                try
                {
                    /*
                     * Its own hop or fall was stopped halfway when it was frozen: down onto the ground, out of
                     * that state (nothing would land it now) and its animator back on
                     */
                    foreach (SkeletonAnimation Model in Entry.Enemy.GetComponentsInChildren<SkeletonAnimation>(true))
                    {
                        if (Model.transform == Entry.Enemy.transform)
                            continue;
                        Vector3 Local = Model.transform.localPosition;
                        Local.z = 0f;
                        Model.transform.localPosition = Local;
                    }
                    Vector3 Position = Entry.Enemy.transform.position;
                    Position.z = 0f;
                    Entry.Enemy.transform.position = Position;
                    if (Entry.Enemy.state != null)
                        Entry.Enemy.state.CURRENT_STATE = StateMachine.State.Idle;
                    foreach (SimpleSpineAnimator Animator in Entry.Enemy.GetComponentsInChildren<SimpleSpineAnimator>(true))
                    {
                        Animator.UpdateAnimsOnStateChange = true;
                        Animator.enabled = true;
                    }
                }
                catch (Exception e)
                {
                    PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Can't land {Entry.Enemy.name}: {e.Message}");
                }
            }
            FollowedEnemies.Clear();
            FollowedByHash.Clear();
        }

        private static void OnSnapshot(BinaryReader Reader)
        {
            string Place = Reader.ReadString();
            int Count = Reader.ReadByte();
            bool Use = Following && Place == MyPlace;
            float Now = Time.realtimeSinceStartup;

            for (int Index = 0; Index < Count; Index++)
            {
                int Id = Reader.ReadInt32();
                float X = Reader.ReadSingle(), Y = Reader.ReadSingle(), Z = Reader.ReadSingle(), SpineZ = Reader.ReadSingle(), ScaleX = Reader.ReadSingle();
                string Animation = Reader.ReadString();
                bool Loop = Reader.ReadBoolean();

                if (!Use || !FollowedByHash.TryGetValue(Id, out Followed Entry) || Entry.Enemy == null)
                    continue;

                /* Height too: enemies that drop in from above would otherwise stay up there */
                Entry.Target = new Vector3(X, Y, Z);
                Entry.SpineZ = SpineZ;
                Entry.ScaleX = ScaleX;
                Entry.LastSeen = Now;
                Entry.SeenOnce = true;
                if (Animation.Length != 0)
                {
                    Entry.Animation = Animation;
                    Entry.Loop = Loop;
                }
            }

            if (Use)
                ++SnapshotsApplied;
        }

        /// <summary>
        /// Moves the followed enemies every frame, after their own scripts ran.
        /// </summary>
        private static IEnumerator FollowLoop()
        {
            List<Followed> Gone = new();
            List<Followed> Round = new();

            while (true)
            {
                yield return null;
                if (FollowedEnemies.Count == 0)
                    continue;

                /* A round that throws past its entries is logged as an error and the loop goes on: dead, it would leave
                   the enemies here unfollowed for the rest of the session */
                try
                {
                    float Now = Time.realtimeSinceStartup;
                    Gone.Clear();

                    /* A kill here (one the owner hasn't got) runs the game's death, and that can bring the next one in
                       (a boss's next form, a brood) and follow it at once: so the round goes over a copy */
                    Round.Clear();
                    Round.AddRange(FollowedEnemies.Values);
                    if (FollowThrow)
                    {
                        FollowThrow = false;
                        throw new InvalidOperationException("a round made to throw (-cotlmp-followthrow)");
                    }
                    foreach (Followed Entry in Round)
                    {
                        try
                        {
                            if (Entry.Enemy == null || Entry.Enemy.HP <= 0f)
                            {
                                Gone.Add(Entry);
                                continue;
                            }

                            /* The owner hasn't got this one (anymore): our room came out different, or it died there */
                            /*
                             * Seen and then gone for a while: it died there and the kill didn't reach us.
                             * Never seen at all: our room came out different. Waves spawn at slightly
                             * different moments on the two sides, so that one waits longer.
                             */
                            float Timeout = Entry.SeenOnce ? MissingTimeout : NeverSeenTimeout;
                            if (SnapshotsApplied > 0 && Now - Entry.LastSeen > Timeout && Now - FollowingSince > Timeout)
                            {
                                Plugin.Logger?.LogInfo($"[COOP] Removing {Entry.Enemy.name}, the room's owner doesn't have it");
                                ++EnemiesRemoved;
                                CoopSync.KillLocal(Entry.Enemy);
                                Gone.Add(Entry);
                                continue;
                            }

                            /* Still spawning in on its own */
                            if (!TryFreeze(Entry, Now))
                                continue;

                            if (Entry.Target.HasValue)
                            {
                                Vector3 Position = Entry.Enemy.transform.position;
                                Vector3 Target = Entry.Target.Value;
                                Vector3 Next = (Target - Position).magnitude > 4f ? Target : Vector3.Lerp(Position, Target, 1f - Mathf.Exp(-FollowRate * Time.deltaTime));
                                Entry.Enemy.transform.position = Next;
                                if (Entry.Body != null)
                                {
                                    Entry.Body.velocity = Vector2.zero;
                                    Entry.Body.position = Next;
                                    Entry.Body.MovePosition(Next);
                                }
                            }

                            if (Entry.Spine != null && Entry.SpineZ.HasValue && Entry.Spine.transform != Entry.Enemy.transform)
                            {
                                Transform Model = Entry.Spine.transform;
                                Vector3 Local = Model.localPosition;
                                Local.z = Mathf.Lerp(Local.z, Entry.SpineZ.Value, 1f - Mathf.Exp(-FollowRate * Time.deltaTime));
                                Model.localPosition = Local;
                            }

                            if (Entry.Spine != null && Entry.Spine.Skeleton != null)
                            {
                                Entry.Spine.Skeleton.ScaleX = Entry.ScaleX;
                                if (Entry.Animation != null)
                                {
                                    var Current = Entry.Spine.AnimationState?.GetCurrent(0);
                                    if ((Current == null || Current.Animation == null || Current.Animation.Name != Entry.Animation) &&
                                        Entry.Spine.Skeleton.Data.FindAnimation(Entry.Animation) != null)
                                    {
                                        Entry.Spine.AnimationState.SetAnimation(0, Entry.Animation, Entry.Loop);
                                        ++EnemyAnimations;
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Following an enemy failed: {e.Message}");
                            Gone.Add(Entry);
                        }
                    }

                    foreach (Followed Entry in Gone)
                    {
                        /* Unity's Equals treats a destroyed object as equal to itself, so this finds it */
                        FollowedEnemies.Remove(Entry.Enemy);
                        foreach (var Pair in new List<KeyValuePair<int, Followed>>(FollowedByHash))
                        {
                            if (Pair.Value == Entry)
                                FollowedByHash.Remove(Pair.Key);
                        }
                    }
                }
                catch (Exception e)
                {
                    ++LoopFaults;
                    PrintLogger.Print(DebugLevel.ERROR_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Following the enemies failed: {e}");
                }
            }
        }

        /* OWNER **************************************************************/

        private static IEnumerator SnapshotLoop()
        {
            var Wait = new WaitForSecondsRealtime(SnapshotInterval);
            List<(int, Health)> Batch = new();

            while (true)
            {
                yield return Wait;
                if (!Owning)
                    continue;

                try
                {
                    Batch.Clear();
                    string Prefix = MyPlace + "|";
                    foreach (var Pair in CoopSync.Enemies)
                    {
                        Health Enemy = Pair.Key;
                        if (Enemy != null && Enemy.HP > 0f && Enemy.gameObject.activeInHierarchy && Pair.Value.StartsWith(Prefix, StringComparison.Ordinal) &&
                            Enemy.GetComponent<UnitObject>() != null)
                            Batch.Add((Hash(Pair.Value), Enemy));
                    }

                    for (int Start = 0; Start < Batch.Count || Start == 0; Start += 24)
                    {
                        int End = Math.Min(Batch.Count, Start + 24);
                        CoopSync.Send(CoopSync.Kind.EnemySnapshot, Writer =>
                        {
                            Writer.Write(MyPlace);
                            Writer.Write((byte)(End - Start));
                            for (int Index = Start; Index < End; Index++)
                            {
                                (int Id, Health Enemy) = Batch[Index];
                                Vector3 Position = Enemy.transform.position;
                                SkeletonAnimation Spine = MainSpine(Enemy.gameObject);
                                var Track = Spine != null ? Spine.AnimationState?.GetCurrent(0) : null;

                                Writer.Write(Id);
                                Writer.Write(Position.x);
                                Writer.Write(Position.y);
                                Writer.Write(Position.z);
                                Writer.Write(Spine != null && Spine.transform != Enemy.transform ? Spine.transform.localPosition.z : 0f);
                                Writer.Write(Spine != null && Spine.Skeleton != null ? Spine.Skeleton.ScaleX : 1f);
                                Writer.Write(Track?.Animation?.Name ?? "");
                                Writer.Write(Track?.Loop ?? true);
                            }
                        });
                        if (Batch.Count == 0)
                            break;
                    }
                    ++SnapshotsSent;
                }
                catch (Exception e)
                {
                    PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Sending enemies failed: {e.Message}");
                }
            }
        }

        /// <summary>
        /// One of our enemies hit the partner's puppet: the partner takes the hit instead.
        /// </summary>
        private static void SendHit(float Damage, Vector3 Location, Health.AttackTypes Type)
        {
            /* Their own i-frames filter repeats, this only keeps contact damage from flooding the link */
            if (Time.realtimeSinceStartup - LastHitSent < 0.1f)
                return;
            LastHitSent = Time.realtimeSinceStartup;
            ++PuppetHitsSent;

            CoopSync.Send(CoopSync.Kind.PlayerHit, Writer =>
            {
                Writer.Write(MyPlace ?? "");
                Writer.Write(Damage);
                Writer.Write(Location.x);
                Writer.Write(Location.y);
                Writer.Write((int)Type);
            });
        }

        private static void OnHit(BinaryReader Reader)
        {
            string Place = Reader.ReadString();
            float Damage = Reader.ReadSingle();
            Vector3 Location = new(Reader.ReadSingle(), Reader.ReadSingle(), 0f);
            Health.AttackTypes Type = (Health.AttackTypes)Reader.ReadInt32();

            PlayerFarming Player = PlayerFarming.Instance;
            if (Place != MyPlace || Player == null || Player.health == null)
                return;

            /* Blame the nearest of our enemies, the game's hit reactions look at the attacker */
            GameObject Attacker = null;
            float Best = float.MaxValue;
            foreach (Health Enemy in Health.team2)
            {
                if (Enemy == null)
                    continue;
                float Distance = (Enemy.transform.position - Location).sqrMagnitude;
                if (Distance < Best)
                {
                    Best = Distance;
                    Attacker = Enemy.gameObject;
                }
            }
            if (Attacker == null)
                return;

            ApplyingHit = true;
            try
            {
                if (Player.health.DealDamage(Damage, Attacker, Location, false, Type))
                {
                    ++HitsTaken;
                    RemoteHitQueued = Player.health.damageEventQueue;
                }
                else if (HitsRefused++ < 10)
                    Plugin.Logger?.LogInfo($"[COOP] Partner's hit for {Damage} refused: hp {Player.health.HP}, state {Player.state?.CURRENT_STATE}, untouchable {Player.health.untouchable}, invincible {Player.health.invincible}");
            }
            catch (Exception e)
            {
                PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Taking the partner's hit failed: {e.Message}");
            }
            finally
            {
                ApplyingHit = false;
            }
        }

        /// <summary>
        /// Handles room sync data from the partner.
        /// </summary>
        public static void OnRelay(CoopSync.Kind Type, uint From, BinaryReader Reader)
        {
            switch (Type)
            {
                case CoopSync.Kind.Place:
                    OnPlace(Reader);
                    break;
                case CoopSync.Kind.EnemySnapshot:
                    OnSnapshot(Reader);
                    break;
                case CoopSync.Kind.PlayerHit:
                    OnHit(Reader);
                    break;
            }
        }

        /// <summary>
        /// True if the attacker is an enemy or something an enemy fired
        /// </summary>
        private static bool FromEnemy(GameObject Attacker)
        {
            if (Attacker == null)
                return false;
            Health Source = Attacker.GetComponentInParent<Health>();
            if (Source != null && Source.team == Health.Team.Team2)
                return true;
            Projectile Shot = Attacker.GetComponent<Projectile>();
            return Shot != null && Shot.team == Health.Team.Team2;
        }

        /* PATCHES ************************************************************/

        [HarmonyPatch]
        private static class RoomSyncPatches
        {
            [HarmonyPatch(typeof(Health), nameof(Health.DealDamage))]
            [HarmonyPrefix]
            private static bool DealDamage(Health __instance, float Damage, GameObject Attacker, Vector3 AttackLocation, Health.AttackTypes AttackType, bool dealDamageImmediately, ref bool __result)
            {
                try
                {
                    /* A hit on the partner's puppet goes to the partner, the puppet itself never takes damage */
                    if (PlayerManager.IsPuppet(__instance))
                    {
                        if (Owning && FromEnemy(Attacker))
                            SendHit(Damage, AttackLocation, AttackType);
                        __result = false;
                        return false;
                    }

                    /* The partner's hit the game held back lands now */
                    if (dealDamageImmediately && RemoteHitQueued != null && __instance.damageEventQueue == RemoteHitQueued)
                    {
                        RemoteHitQueued = null;
                        return true;
                    }

                    /* Following, only the owner's enemies hurt us, through OnHit */
                    if (Following && !ApplyingHit && __instance.isPlayer && PlayerFarming.Instance != null &&
                        __instance == PlayerFarming.Instance.health && FromEnemy(Attacker))
                    {
                        __result = false;
                        return false;
                    }
                }
                catch (Exception e)
                {
                    PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Room sync damage check failed: {e.Message}");
                }
                return true;
            }
        }
    }
}

/* EOF */
