/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Whose game runs a follower: the host's, or the friend's while the friend has it in hand
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP.Debug;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// The friend's hands in the host's cult: talking to followers, sermons and rituals.
    /// </summary>
    /// <remarks>
    /// The host's game runs the cult (see <see cref="CultSync"/>). A follower the friend talks to, or
    /// that the friend's temple gathers for a sermon or a ritual, is the friend's to run until the
    /// friend's game is done with it: the friend's game ticks it as the game would and sends where it
    /// stands and what it does; the host's copy stands still (manual control) and shows that. Once the
    /// friend's game has nothing more for it (no task), it goes back to the host's game, which picks
    /// its next task from what it is now: its orders, role and needs are its data, already shared.
    /// As in the game's own co-op, a follower talks to one player at a time.
    /// </remarks>
    [HarmonyPatch]
    internal static class CultHands
    {
        private enum Hand : byte
        {
            /* The host's game runs it (back from the friend, or taken back by the host) */
            Host = 0,
            /* The friend's game runs it */
            Friend = 1,
            /* The host is talking to it, and done talking */
            HostTalks = 2,
            HostDone = 3
        }

        /* A follower taken is kept at least this long, while the talk or the gathering starts */
        private const float Hold = 1f;

        /* Out of a talk and the temple, a follower our game can't finish with goes back after this long */
        private const float HoldAtMost = 30f;

        /* Followers the friend's game runs now, and since when (the friend's side) */
        private static readonly Dictionary<int, float> Friends = new();

        /* Followers being talked to by this side's player, and the talk */
        private static readonly Dictionary<int, interaction_FollowerInteraction> Talks = new();

        /* Followers the host is talking to (the friend's side) */
        private static readonly HashSet<int> HostTalking = new();

        /* Followers the host moved to another place, for the friend's game to move too */
        private static readonly HashSet<int> Moving = new();

        /* Our own task changes, not a player's */
        private static bool Applying;

        private static readonly AccessTools.FieldRef<interaction_FollowerInteraction, bool> Talking =
            AccessTools.FieldRefAccess<interaction_FollowerInteraction, bool>("Activated");

        private static readonly MethodInfo SetDelta = AccessTools.PropertySetter(typeof(TimeManager), nameof(TimeManager.DeltaGameTime));

        /* Counters for the test harness report */
        internal static int Taken, Given, TakenBack, Ticked, Moved;

        internal static bool FriendRuns(int Id) => Friends.ContainsKey(Id);

        internal static int FriendCount => Friends.Count;

        public static void Initialize()
        {
            Plugin.MonoInstance.StartCoroutine(Watch());
        }

        /// <summary>
        /// The session is over or starts anew: every follower is the host's game's again
        /// </summary>
        internal static void Reset()
        {
            if (Plugin.GlobalsInternal.IsServerCreator)
                FreeAll();
            Friends.Clear();
            Talks.Clear();
            HostTalking.Clear();
            Moving.Clear();
        }

        private static bool InTemple => Interaction_TempleAltar.Instance != null && Interaction_TempleAltar.Instance.Activated;

        private static bool Gathers(FollowerTask Task) => Task is FollowerTask_AttendRitual || Task is FollowerTask_AttendTeaching;

        private static void Send(int Id, Hand Who, string Task = "", int Structure = 0)
        {
            CoopSync.Send(CoopSync.Kind.CultHands, Writer =>
            {
                Writer.Write(Id);
                Writer.Write((byte)Who);
                Writer.Write(Task ?? "");
                Writer.Write(Structure);
            });
        }

        /* THE FRIEND'S SIDE **************************************************/

        private static void Take(FollowerBrain Brain)
        {
            int Id = Brain?.Info?.ID ?? 0;
            if (Id == 0 || Friends.ContainsKey(Id))
                return;
            Friends[Id] = Time.unscaledTime;
            ++Taken;
            Send(Id, Hand.Friend);
            Plugin.Logger?.LogInfo($"[COOP] Follower {Brain.Info.Name} ({Id}) is in our hands");
        }

        /// <summary>
        /// Our game is done with the follower: its data goes first, then the follower, with the task it
        /// has if it still has one the host's game can carry on with
        /// </summary>
        private static void Give(int Id, bool WithTask)
        {
            Friends.Remove(Id);
            CultSync.Flush(Id);
            FollowerBrain Brain = FollowerBrain.FindBrainByID(Id);
            FollowerTask Task = WithTask ? Brain?.CurrentTask : null;
            ++Given;
            Send(Id, Hand.Host, Task?.GetType().FullName ?? "", Task?.UsingStructureID ?? 0);
            Plugin.Logger?.LogInfo($"[COOP] Follower {Id} is back in the host's hands{(Task != null ? $", {Task.Type}" : "")}");

            /* Ours shows the host's now: what our game had it doing stops here */
            if (Brain?.CurrentTask == null)
                return;
            Applying = true;
            try
            {
                Brain.CurrentTask.Abort();
                Brain.CompleteCurrentTask();
            }
            catch (Exception e)
            {
                PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Stopping follower {Id} here failed: {e.Message}");
            }
            finally
            {
                Applying = false;
            }
        }

        /// <summary>
        /// Our world doesn't run (see <see cref="CultSync"/>), but the followers in our hands do, and
        /// followers move between places as the game moves them
        /// </summary>
        internal static void RunOurs(float DeltaGameTime)
        {
            if (Friends.Count == 0 && Moving.Count == 0)
                return;
            SetDelta?.Invoke(null, new object[] { DeltaGameTime });
            foreach (int Id in Friends.Keys.ToList())
            {
                try
                {
                    Follower Body = FollowerManager.FindFollowerByID(Id);
                    if (Body != null && Body.Brain != null)
                    {
                        Body.Tick(DeltaGameTime);
                        ++Ticked;
                    }
                    else
                    {
                        FollowerManager.FindSimFollowerByID(Id)?.Tick(DeltaGameTime);
                    }
                }
                catch (Exception e)
                {
                    PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Running follower {Id} failed: {e.Message}");
                }
            }
            foreach (int Id in Friends.Keys.Union(Moving).ToList())
            {
                try
                {
                    Move(Id);
                }
                catch (Exception e)
                {
                    Moving.Remove(Id);
                    PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Moving follower {Id} failed: {e.Message}");
                }
            }
        }

        /// <summary>
        /// A follower that wants to be somewhere else gets there (TimeManager.Simulate's own moves)
        /// </summary>
        private static void Move(int Id)
        {
            FollowerBrain Brain = FollowerBrain.FindBrainByID(Id);
            if (Brain == null || Brain.DesiredLocation == Brain.Location)
            {
                Moving.Remove(Id);
                return;
            }
            ++Moved;
            Follower Body = FollowerManager.FindFollowerByID(Id);
            if (Body != null)
            {
                for (int Index = 0; Index < 98; Index++)
                    FollowerManager.FollowersAtLocation((FollowerLocation)Index).Remove(Body);
                if (LocationManager.GetLocationState(Brain.DesiredLocation) == LocationState.Unloaded)
                {
                    SimFollower Sim = FollowerManager.FindSimFollowerByID(Id);
                    if (Sim == null)
                        Sim = new SimFollower(Brain);
                    else
                        FollowerManager.SimFollowersAtLocation(Brain.Location).Remove(Sim);
                    Brain.Location = Brain.DesiredLocation;
                    FollowerManager.SimFollowersAtLocation(Brain.Location).Add(Sim);
                    UnityEngine.Object.Destroy(Body.gameObject);
                }
                else if (LocationManager.LocationManagers.TryGetValue(Brain.DesiredLocation, out LocationManager Manager) && Manager != null)
                {
                    Manager.AddFollower(Body);
                }
                else
                {
                    Brain.DesiredLocation = Brain.Location;
                    FollowerManager.FollowersAtLocation(Brain.Location).Add(Body);
                }
                return;
            }

            SimFollower Away = FollowerManager.FindSimFollowerByID(Id);
            if (Away == null)
                return;
            FollowerManager.SimFollowersAtLocation(Brain.Location).Remove(Away);
            LocationState State = LocationManager.GetLocationState(Brain.DesiredLocation);
            if (State != LocationState.Unloaded && LocationManager.LocationManagers.TryGetValue(Brain.DesiredLocation, out LocationManager There) && There != null)
                There.SpawnFollower(Away, State == LocationState.Active);
            if (State != LocationState.Active)
            {
                Brain.Location = Brain.DesiredLocation;
                FollowerManager.SimFollowersAtLocation(Brain.Location).Add(Away);
            }
        }

        /// <summary>
        /// The host moved a follower somewhere else: we move ours there too
        /// </summary>
        internal static void MoveTo(int Id, FollowerLocation Where)
        {
            FollowerBrain Brain = FollowerBrain.FindBrainByID(Id);
            if (Brain == null)
                return;
            Brain.DesiredLocation = Where;
            if (CultSync.Mirror)
                Moving.Add(Id);
        }

        /* THE HOST'S SIDE ****************************************************/

        /// <summary>
        /// Our followers in the friend's hands do as the friend's game says: back in ours, what they
        /// were doing there goes on if our game can carry it on, else they pick their next task
        /// </summary>
        private static void FreeAll()
        {
            foreach (int Id in Friends.Keys.ToList())
                Free(Id, null, 0);
            Friends.Clear();
        }

        private static void Free(int Id, string Task, int Structure)
        {
            Friends.Remove(Id);
            FollowerBrain Brain = FollowerBrain.FindBrainByID(Id);
            if (Brain == null)
                return;
            Applying = true;
            try
            {
                FollowerTask Next = Make(Task, Structure);
                if (Next != null)
                    Brain.HardSwapToTask(Next);
                else if (Brain.CurrentTaskType == FollowerTaskType.ManualControl)
                    Brain.CompleteCurrentTask();
            }
            catch (Exception e)
            {
                PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Taking follower {Id} back failed: {e.Message}");
            }
            finally
            {
                Applying = false;
            }
        }

        /// <summary>
        /// The friend's task for a follower, made again here when it can be: one that needs nothing, or
        /// only the structure it uses
        /// </summary>
        private static FollowerTask Make(string Task, int Structure)
        {
            if (string.IsNullOrEmpty(Task))
                return null;
            Type Of = AccessTools.TypeByName(Task);
            if (Of == null || !typeof(FollowerTask).IsAssignableFrom(Of) || Of.IsAbstract || Of == typeof(FollowerTask_ManualControl) ||
                Of == typeof(FollowerTask_AttendRitual) || Of == typeof(FollowerTask_AttendTeaching) || Of == typeof(FollowerTask_ChangeLocation))
                return null;
            foreach (ConstructorInfo Made in Of.GetConstructors())
            {
                ParameterInfo[] Wants = Made.GetParameters();
                if (Wants.All(Want => Want.IsOptional))
                    return (FollowerTask)Made.Invoke(Wants.Select(Want => Want.HasDefaultValue ? Want.DefaultValue : null).ToArray());
            }
            foreach (ConstructorInfo Made in Of.GetConstructors())
            {
                ParameterInfo[] Wants = Made.GetParameters();
                if (Structure != 0 && Wants.Length == 1 && Wants[0].ParameterType == typeof(int))
                    return (FollowerTask)Made.Invoke(new object[] { Structure });
            }
            return null;
        }

        /// <summary>
        /// The sync check found the two sides don't agree whose game runs a follower: the side that
        /// owns its check says again (see CultSync's check)
        /// </summary>
        /// <param name="Id">The follower</param>
        /// <param name="ExtraThere">The other side has it among the ones it thinks we run</param>
        internal static void Settle(int Id, bool ExtraThere)
        {
            if (Plugin.GlobalsInternal.IsServerCreator)
            {
                if (ExtraThere && Friends.ContainsKey(Id))
                {
                    Plugin.Logger?.LogInfo($"[COOP] Sync check: follower {Id} isn't in the friend's hands any more, back to ours");
                    Free(Id, null, 0);
                }
                else if (!ExtraThere && !Friends.ContainsKey(Id))
                {
                    Send(Id, Hand.Host);
                }
                return;
            }
            if (Friends.ContainsKey(Id) && !ExtraThere)
                Send(Id, Hand.Friend);
            else if (!Friends.ContainsKey(Id) && ExtraThere)
                Send(Id, Hand.Host);
        }

        /* BOTH SIDES *********************************************************/

        internal static void OnHands(BinaryReader Reader)
        {
            int Id = Reader.ReadInt32();
            Hand Who = (Hand)Reader.ReadByte();
            string Task = Reader.ReadString();
            int Structure = Reader.ReadInt32();
            if (!CultSync.IsReady || DataManager.Instance == null)
                return;

            if (Plugin.GlobalsInternal.IsServerCreator)
            {
                switch (Who)
                {
                    case Hand.Friend:
                        Friends[Id] = Time.unscaledTime;
                        Halt(Id);
                        break;
                    case Hand.Host:
                        Free(Id, Task, Structure);
                        break;
                }
                return;
            }

            switch (Who)
            {
                case Hand.Host:
                    if (Friends.Remove(Id))
                        Plugin.Logger?.LogInfo($"[COOP] The host took follower {Id} back");
                    break;
                case Hand.HostTalks:
                    HostTalking.Add(Id);
                    break;
                case Hand.HostDone:
                    HostTalking.Remove(Id);
                    break;
            }
        }

        /// <summary>
        /// The host's copy of a follower in the friend's hands stands still for the friend's game
        /// </summary>
        private static void Halt(int Id)
        {
            FollowerBrain Brain = FollowerBrain.FindBrainByID(Id);
            if (Brain == null || Brain.CurrentTaskType == FollowerTaskType.ManualControl)
                return;
            Applying = true;
            try
            {
                Brain.HardSwapToTask(new FollowerTask_ManualControl());
            }
            catch (Exception e)
            {
                PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Holding follower {Id} for the friend failed: {e.Message}");
            }
            finally
            {
                Applying = false;
            }
        }

        /// <summary>
        /// Four times a second: talks that ended, followers our game is done with
        /// </summary>
        private static IEnumerator Watch()
        {
            var Wait = new WaitForSecondsRealtime(0.25f);
            while (true)
            {
                yield return Wait;
                try
                {
                    Check();
                }
                catch (Exception e)
                {
                    PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Checking whose followers are whose failed: {e}");
                }
            }
        }

        private static void Check()
        {
            bool Host = Plugin.GlobalsInternal.IsServerCreator;
            if (!CultSync.IsReady || !(CultSync.Mirror || CultSync.Hosting))
            {
                if (Host && Friends.Count > 0 && DataManager.Instance != null)
                    FreeAll();
                Friends.Clear();
                Talks.Clear();
                HostTalking.Clear();
                Moving.Clear();
                return;
            }

            foreach (var Pair in Talks.ToList())
            {
                if (Pair.Value != null && Talking(Pair.Value))
                    continue;
                Talks.Remove(Pair.Key);
                if (Host)
                    Send(Pair.Key, Hand.HostDone);
            }
            if (Host)
                return;

            bool InBase = SceneManager.GetActiveScene().name == "Base Biome 1";
            foreach (var Pair in Friends.ToList())
            {
                FollowerBrain Brain = FollowerBrain.FindBrainByID(Pair.Key);
                if (Brain == null)
                    Friends.Remove(Pair.Key);
                else if (!InBase)
                    Give(Pair.Key, true);
                else if (!InTemple && !Talks.ContainsKey(Pair.Key) && Time.unscaledTime - Pair.Value > (Brain.CurrentTask == null ? Hold : HoldAtMost))
                    Give(Pair.Key, Brain.CurrentTask != null);
            }
        }

        /* PATCHES ************************************************************/

        /// <summary>
        /// Talking to a follower: the follower is this player's while they talk, the other can't
        /// </summary>
        [HarmonyPatch(typeof(interaction_FollowerInteraction), nameof(interaction_FollowerInteraction.OnInteract))]
        [HarmonyPrefix]
        private static bool OnTalk(interaction_FollowerInteraction __instance)
        {
            FollowerBrain Brain = __instance.follower?.Brain;
            if (Brain?.Info == null || !CultSync.IsReady)
                return true;
            int Id = Brain.Info.ID;
            if (CultSync.Mirror)
            {
                if (HostTalking.Contains(Id))
                    return Busy();
                Take(Brain);
                Talks[Id] = __instance;
                return true;
            }
            if (!CultSync.Hosting)
                return true;
            if (Friends.ContainsKey(Id))
                return Busy();
            if (!Talks.ContainsKey(Id))
                Send(Id, Hand.HostTalks);
            Talks[Id] = __instance;
            return true;
        }

        private static bool Busy()
        {
            try
            {
                NotificationCentre.Instance?.PlayGenericNotification("Multiplayer/Cult/FollowerBusy");
            }
            catch (Exception e)
            {
                PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Can't say the follower is busy: {e.Message}");
            }
            return false;
        }

        /// <summary>
        /// A player's own game gives followers tasks in the temple (gathering for a sermon or a
        /// ritual, and what the ritual has them do): those followers are that player's to run
        /// </summary>
        [HarmonyPatch(typeof(FollowerBrain), nameof(FollowerBrain.HardSwapToTask))]
        [HarmonyPrefix]
        private static void OnSwap(FollowerBrain __instance, FollowerTask nextTask) => OnTask(__instance, nextTask);

        [HarmonyPatch(typeof(FollowerBrain), nameof(FollowerBrain.TransitionToTask))]
        [HarmonyPrefix]
        private static void OnTransition(FollowerBrain __instance, FollowerTask nextTask) => OnTask(__instance, nextTask);

        private static void OnTask(FollowerBrain __instance, FollowerTask nextTask)
        {
            if (Applying || nextTask == null || __instance?.Info == null || !CultSync.IsReady || !(InTemple || Gathers(nextTask)))
                return;
            int Id = __instance.Info.ID;
            if (CultSync.Mirror)
            {
                Take(__instance);
                return;
            }
            if (CultSync.Hosting && Friends.Remove(Id))
            {
                ++TakenBack;
                Send(Id, Hand.Host);
                Plugin.Logger?.LogInfo($"[COOP] Our temple takes follower {Id} back from the friend");
            }
        }

        /// <summary>
        /// The host's game leaves the followers in the friend's hands alone
        /// </summary>
        [HarmonyPatch(typeof(Follower), nameof(Follower.Tick))]
        [HarmonyPrefix]
        private static bool TickBody(Follower __instance) => !(CultSync.Hosting && Friends.Count > 0 && __instance.Brain?.Info != null && Friends.ContainsKey(__instance.Brain.Info.ID));

        [HarmonyPatch(typeof(SimFollower), nameof(SimFollower.Tick))]
        [HarmonyPrefix]
        private static bool TickAway(SimFollower __instance) => !(CultSync.Hosting && Friends.Count > 0 && __instance.Brain?.Info != null && Friends.ContainsKey(__instance.Brain.Info.ID));
    }
}

/* EOF */
