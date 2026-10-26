/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     The night fox: one deal for both, made by the host
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP.Debug;
using HarmonyLib;
using Spine.Unity;
using System;
using System.Collections;
using System.IO;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// The fox that comes out of the dark at night (on the shore, in the Midas cave, at the mushroom
    /// research site and the decoration shop, and the DLC's) makes a deal that changes the save: what
    /// it takes (fish, a follower, a heart) and what it gives. The save is the host's, so the host
    /// makes the deal; with both players in its place, each game had its own fox and each player could
    /// start their own talk, and the two places went apart.
    /// </summary>
    /// <remarks>
    /// The friend's fox can't be talked to (a note says the host makes the deal). When the host talks
    /// to it, the friend's fox comes out as the host's does, and the talk shows at the bottom of the
    /// friend's screen as any of the host's talks; when it ends, the deal's outcome (the fox's
    /// encounters, introductions and the places it is done with) is the friend's too, and a fox that is
    /// done goes away there as well.
    /// </remarks>
    [HarmonyPatch]
    internal static class FoxSync
    {
        /* Counters for the test harness report */
        internal static int Talks, Mirrored, Refused;

        private static bool Friend => !Plugin.GlobalsInternal.IsServerCreator && CoopSync.InSession;

        private static bool Host => Plugin.GlobalsInternal.IsServerCreator && CoopSync.InSession;

        /* THE FRIEND: NOT ITS OWN DEAL ***************************************/

        [HarmonyPatch(typeof(Interaction_NightFox), nameof(Interaction_NightFox.OnInteract))]
        [HarmonyPrefix]
        private static bool Talk(Interaction_NightFox __instance) => Begin(__instance);

        [HarmonyPatch(typeof(Interaction_DLCNightFox), nameof(Interaction_DLCNightFox.OnInteract))]
        [HarmonyPrefix]
        private static bool TalkDlc(Interaction_DLCNightFox __instance) => Begin(__instance);

        private static bool Begin(Interaction Fox)
        {
            if (Friend)
            {
                ++Refused;
                try
                {
                    NotificationCentre.Instance?.PlayGenericNotification("Multiplayer/Fox/HostDeals");
                }
                catch (Exception)
                {
                }
                return false;
            }
            if (Host)
            {
                ++Talks;
                Plugin.Logger?.LogInfo($"[COOP] Talking to the fox in {PlayerFarming.Location}");
                Send(Fox, true);
                Plugin.MonoInstance.StartCoroutine(WatchTalk(Fox));
            }
            return true;
        }

        /* THE HOST: THE TALK AND ITS OUTCOME *********************************/

        private static IEnumerator WatchTalk(Interaction Fox)
        {
            yield return null;
            while (Fox != null && Fox.gameObject.activeInHierarchy && Activating(Fox) && CoopSync.InSession)
                yield return null;
            if (CoopSync.InSession)
                Send(Fox, false);
        }

        private static void Send(Interaction Fox, bool Starting)
        {
            DataManager Data = DataManager.Instance;
            FollowerLocation Place = PlayerFarming.Location;
            CoopSync.Send(CoopSync.Kind.FoxTalk, Writer =>
            {
                Writer.Write(Starting);
                Writer.Write(Fox is Interaction_DLCNightFox);
                Writer.Write((int)Place);
                Writer.Write(Data.CurrentFoxEncounter);
                Writer.Write(Data.CurrentDLCFoxEncounter);
                Writer.Write((int)Data.CurrentFoxLocation);
                Writer.Write(Data.FoxIntroductions.Contains(Place));
                Writer.Write(Data.FoxCompleted.Contains(Place));
            });
            if (!Starting)
                Plugin.Logger?.LogInfo($"[COOP] The fox talk in {Place} is over, done there: {Data.FoxCompleted.Contains(Place)}");
        }

        /* THE FRIEND: THE HOST'S TALK ****************************************/

        internal static void OnTalk(BinaryReader Reader)
        {
            bool Starting = Reader.ReadBoolean(), Dlc = Reader.ReadBoolean();
            FollowerLocation Place = (FollowerLocation)Reader.ReadInt32();
            int Encounter = Reader.ReadInt32(), DlcEncounter = Reader.ReadInt32();
            FollowerLocation FoxPlace = (FollowerLocation)Reader.ReadInt32();
            bool Introduced = Reader.ReadBoolean(), Done = Reader.ReadBoolean();
            DataManager Data = DataManager.Instance;
            if (!Friend || Data == null)
                return;
            ++Mirrored;

            /* The deal's outcome is the save's, whether we stand there or not */
            Data.CurrentFoxEncounter = Encounter;
            Data.CurrentDLCFoxEncounter = DlcEncounter;
            Data.CurrentFoxLocation = FoxPlace;
            if (Introduced && !Data.FoxIntroductions.Contains(Place))
                Data.FoxIntroductions.Add(Place);
            if (Done && !Data.FoxCompleted.Contains(Place))
                Data.FoxCompleted.Add(Place);
            Plugin.Logger?.LogInfo($"[COOP] The host's fox talk in {Place} {(Starting ? "starts" : $"is over (done there: {Done})")}");

            if (PlayerFarming.Location != Place)
                return;
            foreach (Interaction Fox in UnityEngine.Object.FindObjectsOfType<Interaction>())
            {
                if (!(Fox is Interaction_NightFox) && !(Fox is Interaction_DLCNightFox) || (Fox is Interaction_DLCNightFox) != Dlc)
                    continue;
                Show(Fox, Starting, Done);
            }
        }

        /// <summary>
        /// Our copy of the fox as the host's: out of the dark while they talk, gone with the deal done
        /// </summary>
        private static void Show(Interaction Fox, bool Starting, bool Done)
        {
            try
            {
                SkeletonAnimation Spine = Traverse.Create(Fox).Field("Spine").GetValue<SkeletonAnimation>();
                Traverse.Create(Fox).Field("Activating").SetValue(Starting);
                if (Starting)
                {
                    AccessTools.Method(Fox.GetType(), "ShowGoop")?.Invoke(Fox, null);
                    if (Spine != null)
                    {
                        Spine.gameObject.SetActive(true);
                        Spine.AnimationState.SetAnimation(0, "enter", false);
                        Spine.AnimationState.AddAnimation(0, "animation", true, 0f);
                    }
                    return;
                }
                if (Spine != null && Spine.gameObject.activeSelf)
                    Spine.AnimationState.SetAnimation(0, "exit", false);
                Plugin.MonoInstance.StartCoroutine(Away(Fox, Spine, Done));
            }
            catch (Exception e)
            {
                PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Can't show the host's fox: {e.Message}");
            }
        }

        private static IEnumerator Away(Interaction Fox, SkeletonAnimation Spine, bool Done)
        {
            yield return new WaitForSeconds(1f);
            if (Spine != null)
                Spine.gameObject.SetActive(false);
            if (Done && Fox != null)
                AccessTools.Method(Fox.GetType(), "Deactivate")?.Invoke(Fox, new object[] { true });
        }

        private static bool Activating(Interaction Fox)
        {
            try
            {
                return Traverse.Create(Fox).Field("Activating").GetValue<bool>();
            }
            catch (Exception e)
            {
                COTLMP.Debug.Faults.Note("The fox's state", e);
                return false;
            }
        }
    }
}

/* EOF */
