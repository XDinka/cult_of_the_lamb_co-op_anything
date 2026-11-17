/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     A newcomer waiting by the cult's gate is recruited once, by one player
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP.Debug;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// Newcomers waiting to be recruited (the first follower of a new game, the ones brought back
    /// from runs).
    /// </summary>
    /// <remarks>
    /// A newcomer is a follower the save already has, with its id, waiting in the list of recruits;
    /// both games show it at the gate. When one player recruited it, the other's game still had it
    /// standing there: the follower came over as a new member of the cult and stood next to it, the
    /// same follower twice, and it could be recruited a second time. Now the player who starts a
    /// recruit has it, the other's newcomer doesn't answer meanwhile (as a follower talks to one
    /// player at a time), and when the follower comes over the waiting copy is taken away first.
    ///
    /// The story's first newcomer (the end of Ratau's talk about devotion) is the host's alone, once a save: on the
    /// host's save the friend's game shows the host's, and asks the host to make it when its own talk ended
    /// without the host's. The newcomer that joins is always the one standing at the gate.
    /// </remarks>
    [HarmonyPatch]
    internal static class CultRecruits
    {
        private enum Step : byte
        {
            Start = 1,
            Done = 2,
            /* The friend's own talk gave no story newcomer: the host makes it (a host before this takes it for a
               Done of no follower, and does nothing) */
            StoryNewcomer = 3
        }

        /* A recruit the partner started is theirs at most this long */
        private const float HoldAtMost = 120f;

        private static readonly Dictionary<int, float> PartnerRecruits = new();
        private static readonly HashSet<int> Ours = new();

        /* Counters for the test harness report */
        internal static int Refused, TakenAway, Started, GiftsLeftToHost, StoryNewcomersMade, StoryNewcomersRefused, StoryNewcomersOwn, StoryNewcomersShown, JoinedAsShown, JoinedUnlisted, JoinedAlreadyMember;

        private static void Send(Step Type, int Id)
        {
            if (!CoopSync.InSession)
                return;
            CoopSync.Send(CoopSync.Kind.Recruit, Writer =>
            {
                Writer.Write((byte)Type);
                Writer.Write(Id);
            });
        }

        public static void OnRelay(BinaryReader Reader)
        {
            Step Type = (Step)Reader.ReadByte();
            int Id = Reader.ReadInt32();
            if (Type == Step.Start)
            {
                PartnerRecruits[Id] = Time.unscaledTime;
                Plugin.Logger?.LogInfo($"[COOP] The partner is recruiting follower {Id}");
            }
            else if (Type == Step.StoryNewcomer)
            {
                StoryNewcomerAsked();
            }
            else
            {
                PartnerRecruits.Remove(Id);
            }
        }

        private static bool PartnerHas(int Id) =>
            PartnerRecruits.TryGetValue(Id, out float Since) && Time.unscaledTime - Since < HoldAtMost && CoopSync.InSession;

        /// <summary>
        /// The partner recruited this follower: the newcomer waiting here goes, the follower comes in its place
        /// </summary>
        internal static void TakeAway(int Id)
        {
            try
            {
                foreach (FollowerRecruit Waiting in UnityEngine.Object.FindObjectsOfType<FollowerRecruit>())
                {
                    if (Waiting == null || Waiting.Follower?.Brain?.Info == null || Waiting.Follower.Brain.Info.ID != Id)
                        continue;
                    FollowerBrain.FindBrainByID(Id)?.Cleanup();
                    FollowerBrain.AllBrains.RemoveAll(Brain => Brain?.Info != null && Brain.Info.ID == Id);
                    UnityEngine.Object.Destroy(Waiting.gameObject);
                    ++TakenAway;
                    Plugin.Logger?.LogInfo($"[COOP] Follower {Id} was recruited by the partner: our newcomer goes");
                }
                DataManager.Instance?.Followers_Recruit?.RemoveAll(Info => Info != null && Info.ID == Id);
                PartnerRecruits.Remove(Id);
            }
            catch (Exception e)
            {
                PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Taking our newcomer {Id} away failed: {e.Message}");
            }
        }

        /* PATCHES ************************************************************/

        [HarmonyPatch(typeof(FollowerRecruit), nameof(FollowerRecruit.OnInteract))]
        [HarmonyPrefix]
        private static bool Interact(FollowerRecruit __instance) => Start(__instance);

        [HarmonyPatch(typeof(FollowerRecruit), nameof(FollowerRecruit.OnSecondaryInteract))]
        [HarmonyPrefix]
        private static bool InteractSecond(FollowerRecruit __instance) => Start(__instance);

        internal static bool Start(FollowerRecruit Recruit)
        {
            int Id = Recruit?.Follower?.Brain?.Info?.ID ?? -1;
            if (Id < 0 || !CoopSync.InSession)
                return true;
            if (PartnerHas(Id))
            {
                ++Refused;
                try
                {
                    NotificationCentre.Instance?.PlayGenericNotification("Multiplayer/Cult/FollowerBusy");
                }
                catch
                {
                }
                return false;
            }
            if (Ours.Add(Id))
            {
                ++Started;
                Send(Step.Start, Id);
            }
            return true;
        }

        /* THE STORY'S NEWCOMER *********************************************/

        /* The host: the save in which the story's newcomer is made (by its own talk or on the friend's ask), the save
           a friend's ask is owed in (and which ask: a newer one supersedes it), and whether we make it right now */
        private static DataManager StoryNewcomerGivenIn, StoryNewcomerOwedIn;
        private static int StoryNewcomerAsk;
        private static bool MakingOnRequest, InOurCall;

        /* The friend: we wait for the host's newcomer to stand at our gate */
        private static bool WaitingForHostsNewcomer;

        /* How long the host is free in its cult before the friend's ask is made, when its own talk may still come */
        private const float FreeBeforeMaking = 2f;

        private static bool StoryNewcomerGiven => StoryNewcomerGivenIn != null && ReferenceEquals(StoryNewcomerGivenIn, DataManager.Instance);

        /* Each its own patch class: a game update breaking one leaves the other and the recruit lock in */
        [HarmonyPatch]
        private static class StoryNewcomerPatch
        {
            [HarmonyPatch(typeof(Onboarding), nameof(Onboarding.CreateFollowers))]
            [HarmonyPrefix]
            private static bool Prefix() => StoryNewcomer();
        }

        [HarmonyPatch]
        private static class JoinsAsShownPatch
        {
            [HarmonyPatch(typeof(FollowerManager), nameof(FollowerManager.RecruitFollower))]
            [HarmonyPrefix]
            private static void Prefix(FollowerRecruit recruit) => JoinsAsShown(recruit);
        }

        /// <summary>
        /// The story's newcomer (the end of Ratau's talk about devotion) is the host's to make, once a save. The
        /// friend's game, on the host's save, would make a second one with a name and look of its own and the same id:
        /// ours shows the host's when it comes with the cult's data. The game's own talk runs this inside the talk's
        /// close: whatever goes wrong here, the game's way goes on
        /// </summary>
        private static bool StoryNewcomer()
        {
            try
            {
                return CheckStoryNewcomer();
            }
            catch (Exception Error)
            {
                Plugin.Logger?.LogWarning($"[COOP] Checking the story's newcomer failed, the game makes it: {Error.Message}");
                return true;
            }
        }

        private static bool CheckStoryNewcomer()
        {
            /* The friend, on the host's save (a reconnect's wait too) */
            if (HostSave.Playing || CultSync.Mirror)
            {
                ++GiftsLeftToHost;
                bool HostsTalk = SharedScenes.HostsCallback;
                Plugin.Logger?.LogInfo($"[COOP] The story's newcomer is the host's to make ({(HostsTalk ? "the host closed the talk" : "our own copy of the talk")}): ours shows theirs");
                /* The host's game ran the same end only when it led the talk */
                if (!HostsTalk)
                {
                    Send(Step.StoryNewcomer, -1);
                    Plugin.Logger?.LogInfo("[COOP] Asked the host for the story's newcomer: request sent");
                }
                if (!WaitingForHostsNewcomer)
                {
                    WaitingForHostsNewcomer = true;
                    Plugin.MonoInstance.StartCoroutine(ShowHostsNewcomer());
                }
                return false;
            }
            /* The host: ours, made on the friend's ask */
            if (InOurCall)
                return true;
            /* Once a save, in a session or not (the gift belongs to the save) */
            if (StoryNewcomerGiven)
            {
                ++StoryNewcomersRefused;
                Plugin.Logger?.LogInfo("[COOP] The story's newcomer was given already (on the friend's request): not a second one");
                return false;
            }
            if (MakingOnRequest)
            {
                ++StoryNewcomersRefused;
                Plugin.Logger?.LogInfo("[COOP] The story's newcomer is being made on the friend's request: not a second one");
                return false;
            }
            /* Our own talk: in a session, or with a friend's ask owed in this save */
            if ((Plugin.GlobalsInternal.IsServerCreator && CoopSync.InSession) || ReferenceEquals(StoryNewcomerOwedIn, DataManager.Instance))
            {
                StoryNewcomerGivenIn = DataManager.Instance;
                bool Asked = StoryNewcomerOwedIn != null;
                StoryNewcomerOwedIn = null;
                ++StoryNewcomersOwn;
                Plugin.Logger?.LogInfo($"[COOP] The story's newcomer is made by our own talk{(Asked ? ": the friend's request is met by it" : "")}");
            }
            return true;
        }

        /// <summary>
        /// The session ended: the friend's wait for the host's newcomer goes. The host's ask owed stays with its save
        /// (made later if the host plays on in it), and so does the save in which the newcomer was made
        /// </summary>
        internal static void ResetSession()
        {
            WaitingForHostsNewcomer = false;
        }

        /// <summary>
        /// The host: the friend's own talk ended without us; we make the story's newcomer once we are free in our cult
        /// </summary>
        private static void StoryNewcomerAsked()
        {
            if (!Plugin.GlobalsInternal.IsServerCreator || DataManager.Instance == null)
                return;
            if (StoryNewcomerGiven)
            {
                ++StoryNewcomersRefused;
                Plugin.Logger?.LogInfo("[COOP] The friend asks for the story's newcomer: refused, it was given already");
                return;
            }
            if (ReferenceEquals(StoryNewcomerOwedIn, DataManager.Instance))
                return;
            StoryNewcomerOwedIn = DataManager.Instance;
            int Ask = ++StoryNewcomerAsk;
            Plugin.Logger?.LogInfo("[COOP] The friend asks for the story's newcomer");
            Plugin.MonoInstance.StartCoroutine(GiveStoryNewcomer(Ask));
        }

        /// <summary>
        /// The host free in its cult, as the game's followers wait for the lamb before their talks (Follower's
        /// own check), and with no menu open
        /// </summary>
        private static bool HostFree()
        {
            PlayerFarming Player = PlayerFarming.Instance;
            return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Base Biome 1" && PlayerFarming.Location == FollowerLocation.Base &&
                   !MMTools.MMTransition.IsPlaying && !LetterBox.IsPlaying && !(MMTools.MMConversation.isPlaying && !MMTools.MMConversation.isBark) &&
                   !SimulationManager.IsPaused && global::Lamb.UI.UIMenuBase.ActiveMenus.Count == 0 && Player?.state != null &&
                   PlayerFarming.LongToPerformPlayerStates.Contains(Player.state.CURRENT_STATE) &&
                   Onboarding.Instance != null && BiomeBaseManager.Instance != null;
        }

        private static IEnumerator GiveStoryNewcomer(int Ask)
        {
            DataManager Save = StoryNewcomerOwedIn;
            float FreeSince = -1f;
            bool Told = false;
            int Tries = 0;
            try
            {
                while (true)
                {
                    yield return new WaitForSecondsRealtime(0.5f);
                    /* Another ask took over, or our own talk met it */
                    if (Ask != StoryNewcomerAsk || StoryNewcomerOwedIn == null)
                        yield break;
                    if (StoryNewcomerGiven)
                        yield break;
                    if (!ReferenceEquals(DataManager.Instance, Save))
                    {
                        Plugin.Logger?.LogWarning("[COOP] The friend's request for the story's newcomer is dropped: another save is up");
                        yield break;
                    }
                    if (!HostFree())
                    {
                        FreeSince = -1f;
                        if (!Told)
                            Plugin.Logger?.LogInfo("[COOP] The story's newcomer for the friend is deferred: made once we are free in our cult");
                        Told = true;
                        continue;
                    }
                    if (FreeSince < 0f)
                        FreeSince = Time.realtimeSinceStartup;
                    /* Our own talk may still come and make it: unless it came and went already, a moment free first */
                    if (!ObjectiveManager.GroupExists("Objectives/GroupTitles/UnlockSacredKnowledge") && Time.realtimeSinceStartup - FreeSince < FreeBeforeMaking)
                        continue;

                    int Counter = DataManager.Instance.FollowerID, Listed = DataManager.Instance.Followers_Recruit.Count;
                    bool Went = false;
                    MakingOnRequest = true;
                    try
                    {
                        InOurCall = true;
                        try
                        {
                            Onboarding.Instance.CreateFollowers();
                            Went = true;
                        }
                        finally
                        {
                            InOurCall = false;
                        }
                    }
                    catch (Exception Error)
                    {
                        Plugin.Logger?.LogWarning($"[COOP] Making the story's newcomer on the friend's request failed: {Error.Message}");
                    }
                    if (Went)
                    {
                        /* Given, whatever the game's way gave: a newcomer made, or the one waiting at the gate shown (the
                           game's FirstFollower makes one only with nobody waiting) */
                        StoryNewcomerGivenIn = Save;
                        yield return new WaitForSecondsRealtime(4f);
                        MakingOnRequest = false;
                        if (DataManager.Instance.FollowerID != Counter || DataManager.Instance.Followers_Recruit.Count != Listed)
                        {
                            ++StoryNewcomersMade;
                            Plugin.Logger?.LogInfo("[COOP] Made the story newcomer on the friend's request");
                        }
                        else
                        {
                            ++StoryNewcomersShown;
                            Plugin.Logger?.LogInfo("[COOP] On the friend's request the game showed the one waiting at our gate, as it does");
                        }
                        yield break;
                    }
                    MakingOnRequest = false;
                    if (++Tries >= 3)
                    {
                        Plugin.Logger?.LogWarning("[COOP] The story's newcomer on the friend's request failed 3 times: left");
                        yield break;
                    }
                    Plugin.Logger?.LogWarning("[COOP] The story's newcomer on the friend's request: once more when we are free");
                    FreeSince = -1f;
                }
            }
            finally
            {
                MakingOnRequest = false;
                /* This ask is over, whichever way */
                if (Ask == StoryNewcomerAsk)
                    StoryNewcomerOwedIn = null;
            }
        }

        /// <summary>
        /// The friend: the host's newcomer at our gate, once it is in the cult's list and we are in the cult (a scene
        /// change waits for our next time there). Until it joined or we left the host's save
        /// </summary>
        private static IEnumerator ShowHostsNewcomer()
        {
            bool Away = false, Told = false;
            int Target = -1;
            try
            {
                while (true)
                {
                    yield return new WaitForSecondsRealtime(0.5f);
                    if (Target >= 0 && DataManager.Instance?.Followers?.Exists(Member => Member != null && Member.ID == Target) == true)
                    {
                        Plugin.Logger?.LogInfo($"[COOP] The host's newcomer ({Target}) joined the cult: no more waiting for it");
                        yield break;
                    }
                    if (!HostSave.Playing && !CultSync.Mirror)
                    {
                        Plugin.Logger?.LogInfo("[COOP] The story's newcomer isn't at our gate: we left the host's save");
                        yield break;
                    }
                    bool InCult = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Base Biome 1";
                    if (!InCult)
                    {
                        if (!Away)
                            Plugin.Logger?.LogInfo("[COOP] We left the cult before the host's newcomer came: it is shown when we are back");
                        Away = true;
                        continue;
                    }
                    List<FollowerInfo> Waiting = DataManager.Instance?.Followers_Recruit;
                    if (MMTools.MMTransition.IsPlaying || BiomeBaseManager.Instance == null || Waiting == null)
                        continue;
                    /* A newcomer of the list that hasn't joined yet */
                    FollowerInfo First = Waiting.Find(Info => Info != null && !DataManager.Instance.Followers.Exists(Member => Member != null && Member.ID == Info.ID));
                    if (First == null)
                        continue;
                    Target = First.ID;
                    bool Stands = false;
                    foreach (FollowerRecruit Standing in UnityEngine.Object.FindObjectsOfType<FollowerRecruit>())
                    {
                        if (Standing?.Follower?.Brain?.Info != null && Standing.Follower.Brain.Info.ID == First.ID)
                            Stands = true;
                    }
                    if (Stands)
                    {
                        Plugin.Logger?.LogInfo($"[COOP] The host's newcomer {First.Name} ({First.ID}) already stands at our gate");
                        yield break;
                    }
                    /* The game spawns the list's first: only when that is the one we wait for */
                    if (!ReferenceEquals(Waiting[0], First))
                    {
                        if (!Told)
                            Plugin.Logger?.LogInfo($"[COOP] The host's newcomer {First.Name} ({First.ID}) isn't first in the cult's list ({Waiting[0]?.Name}): waiting");
                        Told = true;
                        continue;
                    }
                    try
                    {
                        /* What the game's FirstFollower does, without making one: its guard (one at a time) */
                        if (!BiomeBaseManager.Instance.SpawnExistingRecruits || Waiting.Count <= 1)
                        {
                            /* As the base's own spawn of a waiting newcomer sets it (FollowerManager.SpawnExistingRecruitsInBase) */
                            First.Location = FollowerLocation.Base;
                            FollowerRecruit Shown = FollowerManager.SpawnExistingRecruit(BiomeBaseManager.Instance.RecruitSpawnLocation.transform.position);
                            if (Shown != null)
                            {
                                /* Only once it stands: a spawn that failed leaves the game's own showing of it on */
                                BiomeBaseManager.Instance.SpawnExistingRecruits = true;
                                Shown.ManualTriggerAnimateIn();
                                Plugin.Logger?.LogInfo($"[COOP] The host's newcomer {First.Name} ({First.ID}) stands at our gate{(Away ? ", back in the cult" : "")}");
                            }
                            else
                                Plugin.Logger?.LogWarning($"[COOP] The host's newcomer {First.Name} ({First.ID}) didn't spawn at our gate");
                        }
                        else
                            Plugin.Logger?.LogInfo($"[COOP] The host's newcomer {First.Name} ({First.ID}) waits with others: the gate shows them as the game does");
                    }
                    catch (Exception Error)
                    {
                        Plugin.Logger?.LogWarning($"[COOP] Showing the host's newcomer {First.ID} at our gate failed: {Error.Message}");
                    }
                    yield break;
                }
            }
            finally
            {
                WaitingForHostsNewcomer = false;
            }
        }

        /// <summary>
        /// The newcomer that joins is the one standing here: the game joins the cult list's entry with that id, and in
        /// a session the list may hold another copy (the partner's list came as the cult's data while ours stood
        /// waiting). The follower the game shows and the one the cult keeps would be two
        /// </summary>
        private static void JoinsAsShown(FollowerRecruit recruit)
        {
            try
            {
                JoinAsShown(recruit);
            }
            catch (Exception Error)
            {
                Plugin.Logger?.LogWarning($"[COOP] Checking the newcomer that joins failed: {Error.Message}");
            }
        }

        private static void JoinAsShown(FollowerRecruit recruit)
        {
            if (!CultSync.Mirror && !CultSync.Hosting)
                return;
            FollowerInfo Shown = recruit?.Follower?.Brain?._directInfoAccess;
            List<FollowerInfo> Waiting = DataManager.Instance?.Followers_Recruit;
            if (Shown == null || Waiting == null)
                return;
            int At = Waiting.FindIndex(Info => Info != null && Info.ID == Shown.ID);
            if (At < 0)
            {
                /* A member with that id already: putting it back would make it join a second time */
                if (DataManager.Instance.Followers.Exists(Member => Member != null && Member.ID == Shown.ID))
                {
                    ++JoinedAlreadyMember;
                    Plugin.Logger?.LogWarning($"[COOP] Newcomer {Shown.ID} ({Describe(Shown)}) has no entry in the cult's list and is a member already: not put back");
                    return;
                }
                /* The game's recruit takes the list's entry: with none it would throw and the recruit die half done */
                Waiting.Add(Shown);
                ++JoinedUnlisted;
                Plugin.Logger?.LogWarning($"[COOP] Newcomer {Shown.ID} had no entry in the cult's list: {Describe(Shown)}, standing here, joins");
                return;
            }
            if (ReferenceEquals(Waiting[At], Shown))
                return;
            FollowerInfo Listed = Waiting[At];
            Waiting[At] = Shown;
            ++JoinedAsShown;
            Plugin.Logger?.LogWarning($"[COOP] Newcomer {Shown.ID}: the cult's list had another copy of it ({Describe(Listed)}); {Describe(Shown)}, standing here, joins");
        }

        private static string Describe(FollowerInfo Info) => $"{Info.Name}, {Info.SkinName}, seed {Info.RandomSeed}";

        [HarmonyPatch(typeof(FollowerRecruit), "OnRecruitFinished")]
        [HarmonyPostfix]
        private static void Finished(FollowerRecruit __instance)
        {
            int Id = __instance?.Follower?.Brain?.Info?.ID ?? -1;
            if (Id >= 0 && Ours.Remove(Id))
                Send(Step.Done, Id);
        }
    }
}

/* EOF */
