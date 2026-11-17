/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Ratau in the cult as the host's: shown where the host talks to him
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using HarmonyLib;
using MMTools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// The cult's story is told by Ratau, one of the Onboarding's sixteen Rat* children at a time.
    /// Each game runs its own Onboarding, and the steps that bring Ratau out (a quest done, a
    /// building put up) run on the host's save in the host's game: the friend saw the host's talk
    /// at the bottom of the screen with no Ratau there to say it.
    /// </summary>
    /// <remarks>
    /// Only the showing is the friend's, as with the night fox: when the host's talk with one of the
    /// Rat* children starts, the friend's game, in the same place and free, brings that child out
    /// where the host's stands, any other Rat* child out on its side away (a new step of the story
    /// starts so in the game, with its HideAll). It can't be talked
    /// to (the talk and what it gives are the host's) until the friend's own Onboarding takes its
    /// children in hand again. When the host's talk ends (and when its Ratau goes later), a Ratau we
    /// brought out that the host's game no longer has goes; one the friend's own story brought out
    /// stays. What the host's game has out comes out on ours when we are free there. The talk itself
    /// is never played on the friend's side and the friend's lamb never led: the lines come at the
    /// bottom of the screen as before.
    /// </remarks>
    internal static class RatauSync
    {
        /* Counters for the test harness report */
        internal static int Sent, Shown, Applied, Refused, Freed, Deferred;

        /* What a message tells: the host's talk starts, is over, or its Ratau went later than its end */
        private enum Step : byte
        {
            Start = 0,
            End = 1,
            Gone = 2
        }

        /* The host's talks with Ratau, counted: a later one makes an earlier one's watch stop */
        private static int Talks;

        /* Onboarding's Rat* children, by field: their number is what goes to the friend */
        private static readonly string[] Names =
        {
            "Rat1Indoctrinate", "Rat2Shrine", "Rat2Food", "Rat3Devotion", "Rat4GoToDungeon", "Rat5PauseScreen", "Rat6Meditation",
            "Rat7MessageBoard", "RatReturnWithFolloer", "RatBonesAndRitual", "RatPreachSermon", "RatMonsterHeart", "RatLoyalty",
            "RatRitaul", "RatSurvivalMode", "RatWinterMode"
        };

        private static readonly FieldInfo[] Fields = Array.ConvertAll(Names, Name => AccessTools.Field(typeof(Onboarding), Name));

        /* The host's talk ends; past this a talk still not over is taken for one the game dropped */
        private const float TalkAtMost = 600f;

        /* After the talk, how long Ratau may take to go before what is out is told */
        private const float Settle = 5f;

        /* The children we brought out that can't be talked to, until our own Onboarding takes them again */
        private static readonly List<Interaction_SimpleConversation> Held = new();

        private static GameObject Child(Onboarding Story, int Index)
        {
            if (Story == null || Index < 0 || Index >= Fields.Length || Fields[Index] == null)
                return null;
            GameObject Rat = Fields[Index].GetValue(Story) as GameObject;
            return Rat != null ? Rat : null;
        }

        /* THE HOST: ITS TALKS WITH RATAU ************************************/

        [HarmonyPatch]
        private static class TalkPatch
        {
            [HarmonyPatch(typeof(Interaction_SimpleConversation), nameof(Interaction_SimpleConversation.OnInteract), new[] { typeof(StateMachine) })]
            [HarmonyPrefix]
            private static void Prefix(Interaction_SimpleConversation __instance, out bool __state) => __state = __instance.Spoken;

            [HarmonyPatch(typeof(Interaction_SimpleConversation), nameof(Interaction_SimpleConversation.OnInteract), new[] { typeof(StateMachine) })]
            [HarmonyPostfix]
            private static void Postfix(Interaction_SimpleConversation __instance, bool __state) => Talked(__instance, __state);
        }

        private static void Talked(Interaction_SimpleConversation Talk, bool WasSpoken)
        {
            try
            {
                if (!CultSync.Hosting || WasSpoken || !Talk.Spoken)
                    return;
                Onboarding Story = Onboarding.Instance;
                if (Story == null)
                    return;
                int Index = Array.FindIndex(Fields, Field => Field != null && ReferenceEquals(Field.GetValue(Story), Talk.gameObject));
                if (Index < 0)
                    return;
                Send(Step.Start, Index, Talk.transform.position);
                Plugin.Logger?.LogInfo($"[COOP] Ratau's talk {Names[Index]} starts in {PlayerFarming.Location}: the friend's game shows him");
                Plugin.MonoInstance.StartCoroutine(WatchTalk(Talk, Index, ++Talks));
            }
            catch (Exception Error)
            {
                Plugin.Logger?.LogWarning($"[COOP] Telling the friend of Ratau's talk failed: {Error.Message}");
            }
        }

        private static IEnumerator WatchTalk(Interaction_SimpleConversation Talk, int Index, int Serial)
        {
            float Since = Time.unscaledTime, Quiet = -1f;
            yield return null;
            /* Over with its callback, or gone from the screen a second (the game cleared it or put another in its place) */
            while (CoopSync.InSession && Talk != null && !Talk.Finished && Time.unscaledTime - Since < TalkAtMost)
            {
                bool On = (MMConversation.isPlaying && !MMConversation.isBark) || LetterBox.IsPlaying;
                if (On)
                    Quiet = -1f;
                else if (Quiet < 0f)
                    Quiet = Time.unscaledTime;
                else if (Time.unscaledTime - Quiet > 1f)
                    break;
                yield return null;
            }
            /* The talk's callback takes Ratau away a moment later (RatPreachSermon goes in about 2 s): what is out is read after */
            float Over = Time.unscaledTime;
            while (CoopSync.InSession && Talk != null && Talk.gameObject.activeInHierarchy && Time.unscaledTime - Over < Settle)
                yield return null;
            if (!CultSync.Hosting)
                yield break;
            try
            {
                Send(Step.End, Index, Vector3.zero);
                Plugin.Logger?.LogInfo($"[COOP] Ratau's talk {Names[Index]} is over ({(Talk != null && Talk.Finished ? "closed" : "gone")}): Ratau out here {Out(Onboarding.Instance)}");
            }
            catch (Exception Error)
            {
                Plugin.Logger?.LogWarning($"[COOP] Telling the friend Ratau's talk is over failed: {Error.Message}");
            }

            /* Still out: the story may take him away much later (RatLoyalty's gift and second talk first); once, if no talk came since */
            if (Talk == null || !Talk.gameObject.activeInHierarchy)
                yield break;
            while (CoopSync.InSession && Talk != null && Talk.gameObject.activeInHierarchy && Serial == Talks && Time.unscaledTime - Since < TalkAtMost)
                yield return null;
            if (!CultSync.Hosting || Serial != Talks || (Talk != null && Talk.gameObject.activeInHierarchy))
                yield break;
            try
            {
                Send(Step.Gone, Index, Vector3.zero);
                Plugin.Logger?.LogInfo($"[COOP] Ratau of the talk {Names[Index]} went after it: Ratau out here {Out(Onboarding.Instance)}");
            }
            catch (Exception Error)
            {
                Plugin.Logger?.LogWarning($"[COOP] Telling the friend Ratau went failed: {Error.Message}");
            }
        }

        /* The Rat* children out in our game, one bit each */
        private static int Mask(Onboarding Story)
        {
            int Bits = 0;
            for (int Index = 0; Index < Fields.Length; ++Index)
            {
                GameObject Rat = Child(Story, Index);
                if (Rat != null && Rat.activeSelf)
                    Bits |= 1 << Index;
            }
            return Bits;
        }

        private static string Out(Onboarding Story)
        {
            int Bits = Mask(Story);
            List<string> On = new();
            for (int Index = 0; Index < Names.Length; ++Index)
            {
                if ((Bits & (1 << Index)) != 0)
                    On.Add(Names[Index]);
            }
            return On.Count == 0 ? "none" : string.Join(", ", On);
        }

        private static void Send(Step What, int Index, Vector3 At)
        {
            ++Sent;
            string Scene = SceneManager.GetActiveScene().name;
            FollowerLocation Place = PlayerFarming.Location;
            int Bits = What == Step.Start ? 0 : Mask(Onboarding.Instance);
            CoopSync.Send(CoopSync.Kind.RatauTalk, Writer =>
            {
                Writer.Write((byte)What);
                Writer.Write((byte)Index);
                Writer.Write(Scene ?? "");
                Writer.Write((int)Place);
                Writer.Write(At.x);
                Writer.Write(At.y);
                Writer.Write(At.z);
                Writer.Write((ushort)Bits);
            });
        }

        /* THE FRIEND: RATAU AS THE HOST'S *************************************/

        /*
         * The host's last end of a talk (or its Ratau gone later), in two parts. What it takes away (a
         * Ratau we brought out the host's game hasn't) goes at once, wherever in the scene we are, under
         * a menu or in a transition too: it waits only for a talk or scene of our own. What it brings out
         * waits for the host's place and our game free, as a talk's start does. Each part is taken once;
         * after two minutes what is to go goes, what is to come out is dropped.
         */
        private const float WaitAtMost = 120f;

        private static Step PendingStep;
        private static int PendingIndex = -1, PendingBits;
        private static string PendingScene;
        private static FollowerLocation PendingPlace;
        private static float PendingSince;
        private static bool PendingAway, PendingOut, PendingWatch;

        internal static void OnRelay(BinaryReader Reader)
        {
            byte Raw = Reader.ReadByte();
            int Index = Reader.ReadByte();
            string Scene = Reader.ReadString();
            FollowerLocation Place = (FollowerLocation)Reader.ReadInt32();
            Vector3 At = new(Reader.ReadSingle(), Reader.ReadSingle(), Reader.ReadSingle());
            int Bits = Reader.ReadUInt16();
            if (Raw > (byte)Step.Gone)
            {
                ++Refused;
                Plugin.Logger?.LogInfo($"[COOP] Ratau as in the host's game: a word of a kind we don't know ({Raw}) about {Name(Index)}, left as it is");
                return;
            }
            Step Kind = (Step)Raw;
            if (!CultSync.Mirror)
            {
                ++Refused;
                Plugin.Logger?.LogInfo($"[COOP] Ratau as in the host's game: not shown, {Describe(Kind, Index)} (not on the host's save yet)");
                return;
            }
            string What = Describe(Kind, Index);
            try
            {
                /* A newer word from the host: what waited goes (its Ratau gone later takes the end's place, with the newer set) */
                bool EndWaited = PendingIndex >= 0 && PendingStep == Step.End && PendingOut && Kind == Step.Gone && PendingIndex == Index;
                if (EndWaited)
                {
                    Plugin.Logger?.LogInfo($"[COOP] Ratau as in the host's game: {Describe(Step.End, Index)}, put off, now one word with its Ratau gone later (the host's newer set)");
                    PendingIndex = -1;
                }
                DropPending("the host's newer word");
                if (EndWaited)
                {
                    Kind = Step.End;
                    What = Describe(Kind, Index);
                }

                if (Kind == Step.Start)
                {
                    string Why = Refusal(Scene, Place);
                    if (Why == null)
                        Why = Show(Index, At);
                    if (Why != null)
                    {
                        ++Refused;
                        Plugin.Logger?.LogInfo($"[COOP] Ratau as in the host's game: not shown, {What} ({Why})");
                    }
                    return;
                }

                /* The end, or the Ratau gone later: another scene, or no story here, leaves ours as it is */
                string Ours = SceneManager.GetActiveScene().name;
                if (Ours != Scene || Onboarding.Instance == null)
                {
                    ++Refused;
                    Plugin.Logger?.LogInfo($"[COOP] Ratau as in the host's game: not shown, {What} ({(Ours != Scene ? $"the host in {Scene}, we in {Ours}" : "no Ratau's story here")})");
                    return;
                }
                PendingStep = Kind;
                PendingIndex = Index;
                PendingBits = Bits;
                PendingScene = Scene;
                PendingPlace = Place;
                PendingSince = Time.unscaledTime;
                PendingAway = true;
                PendingOut = Kind == Step.End;
                string Waits = TakeParts(false);
                if (PendingIndex < 0)
                    return;
                ++Deferred;
                Plugin.Logger?.LogInfo($"[COOP] Ratau as in the host's game: {What}, put off ({Waits}) until we are free there");
                if (!PendingWatch)
                    Plugin.MonoInstance.StartCoroutine(TakeLater());
            }
            catch (Exception Error)
            {
                ++Refused;
                Plugin.Logger?.LogWarning($"[COOP] Ratau as in the host's game failed, {What}: {Error.Message}");
            }
        }

        private static string Describe(Step Kind, int Index) => Kind switch
        {
            Step.Start => $"the host's talk {Name(Index)} starts",
            Step.End => $"the host's talk {Name(Index)} is over",
            _ => $"the host's Ratau of {Name(Index)} went"
        };

        /// <summary>
        /// Takes what of the word waiting can be taken now; the cell is empty once all of it is. Null,
        /// or why a part still waits. Last: the two minutes are over, what is to go goes regardless.
        /// </summary>
        private static string TakeParts(bool Last)
        {
            string Waits = null;
            int Index = PendingIndex, Bits = PendingBits;
            if (PendingAway)
            {
                string Busy = MMConversation.isPlaying && !MMConversation.isBark ? "we are in a talk" : LetterBox.IsPlaying ? "we are in a scene" : null;
                if (Busy == null || Last)
                {
                    PendingAway = false;
                    if (PendingStep == Step.End)
                        Away(Bits);
                    else
                    {
                        string Why = Went(Index);
                        if (Why != null)
                            Plugin.Logger?.LogInfo($"[COOP] Ratau as in the host's game: {Describe(Step.Gone, Index)}, left as it is ({Why})");
                    }
                }
                else
                    Waits = Busy;
            }
            if (PendingOut)
            {
                string Why = Refusal(PendingScene, PendingPlace);
                if (Why == null)
                {
                    PendingOut = false;
                    Out(Bits, Index);
                }
                else
                    Waits ??= Why;
            }
            if (!PendingAway && !PendingOut)
                PendingIndex = -1;
            return Waits;
        }

        /// <summary>
        /// The word put off: each part once, when our game is free for it (the taking away once no talk
        /// or scene of ours is on, or after two minutes regardless; the bringing out in the host's place,
        /// with no talk, scene, transition or menu of ours), or never (a newer word, our own story's
        /// step, another scene, the session's end)
        /// </summary>
        private static IEnumerator TakeLater()
        {
            PendingWatch = true;
            WaitForSecondsRealtime Wait = new(0.5f);
            try
            {
                while (PendingIndex >= 0)
                {
                    yield return Wait;
                    if (PendingIndex < 0)
                        break;
                    string What = Describe(PendingStep, PendingIndex);
                    if (!CultSync.Mirror || SceneManager.GetActiveScene().name != PendingScene)
                    {
                        DropPending("another scene");
                        break;
                    }
                    bool Last = Time.unscaledTime - PendingSince > WaitAtMost;
                    float Since = PendingSince;
                    try
                    {
                        string Waits = TakeParts(Last);
                        if (PendingIndex < 0)
                            Plugin.Logger?.LogInfo($"[COOP] Ratau as in the host's game: {What}, taken now {Time.unscaledTime - Since:F0} s on");
                        else if (Last)
                            DropPending($"never free there: {Waits}");
                    }
                    catch (Exception Error)
                    {
                        DropPending($"failed: {Error.Message}");
                        Plugin.Logger?.LogWarning($"[COOP] Ratau as in the host's game failed, {What} put off: {Error.Message}");
                    }
                }
            }
            finally
            {
                PendingWatch = false;
            }
        }

        private static void DropPending(string Why)
        {
            if (PendingIndex >= 0)
                Plugin.Logger?.LogInfo($"[COOP] Ratau as in the host's game: {Describe(PendingStep, PendingIndex)}, put off, dropped ({Why})");
            PendingStep = Step.Start;
            PendingIndex = -1;
            PendingBits = 0;
            PendingScene = null;
            PendingPlace = default;
            PendingSince = 0f;
            PendingAway = PendingOut = false;
        }

        private static string Name(int Index) => Index >= 0 && Index < Names.Length ? Names[Index] : $"#{Index}";

        /// <summary>
        /// Why our game can't bring the host's Ratau out now, or null: not in the host's scene and
        /// place, or busy with something of its own the show would break into
        /// </summary>
        private static string Refusal(string Scene, FollowerLocation Place)
        {
            string Ours = SceneManager.GetActiveScene().name;
            if (Ours != Scene)
                return $"the host in {Scene}, we in {Ours}";
            if (Onboarding.Instance == null)
                return "no Ratau's story here";
            if (PlayerFarming.Location != Place)
                return $"the host in {Place}, we in {PlayerFarming.Location}";
            if (MMTransition.IsPlaying)
                return "we are in a transition";
            if (MMConversation.isPlaying && !MMConversation.isBark)
                return "we are in a talk";
            if (LetterBox.IsPlaying)
                return "we are in a scene";
            if (global::Lamb.UI.UIMenuBase.ActiveMenus.Count > 0)
                return "a menu is open";
            return null;
        }

        private static string Show(int Index, Vector3 At)
        {
            Onboarding Story = Onboarding.Instance;
            GameObject Rat = Child(Story, Index);
            if (Rat == null)
                return $"{Name(Index)} is gone here";
            Interaction_SimpleConversation Talk = Rat.GetComponent<Interaction_SimpleConversation>();
            if (Talk == null)
                return $"{Name(Index)} has no talk here";
            if (Done(Talk))
                return $"{Name(Index)} is done with here (its delete conditions hold)";
            for (int Other = 0; Other < Fields.Length; ++Other)
            {
                GameObject Rest = Child(Story, Other);
                if (Other != Index && Rest != null && Rest.activeSelf)
                    Rest.SetActive(false);
            }
            Rat.transform.position = At;
            Bring(Talk, true);
            ++Shown;
            Plugin.Logger?.LogInfo($"[COOP] Ratau as in the host's game: {Name(Index)} out for the host's talk");
            return null;
        }

        /* After the host's talk: away goes each Ratau we brought out the host's game hasn't out (one our own story brought out stays) */
        private static void Away(int Bits)
        {
            Onboarding Story = Onboarding.Instance;
            List<string> Changed = new();
            for (int Index = 0; Index < Fields.Length; ++Index)
            {
                GameObject Rat = Child(Story, Index);
                if (Rat == null || !Rat.activeSelf || (Bits & (1 << Index)) != 0)
                    continue;
                Interaction_SimpleConversation Talk = Rat.GetComponent<Interaction_SimpleConversation>();
                if (Talk == null || !Held.Contains(Talk))
                {
                    Changed.Add($"{Names[Index]} ours, kept");
                    continue;
                }
                Rat.SetActive(false);
                Changed.Add($"{Names[Index]} away");
            }
            ++Applied;
            Plugin.Logger?.LogInfo($"[COOP] Ratau as in the host's game after its talk: {(Changed.Count == 0 ? "none to take away" : string.Join(", ", Changed))}");
        }

        /* After the host's talk: out comes each Ratau the host's game has out and ours hasn't; the one of the talk seen at once */
        private static void Out(int Bits, int Talked)
        {
            Onboarding Story = Onboarding.Instance;
            List<string> Changed = new();
            for (int Index = 0; Index < Fields.Length; ++Index)
            {
                GameObject Rat = Child(Story, Index);
                if (Rat == null || Rat.activeSelf || (Bits & (1 << Index)) == 0)
                    continue;
                Interaction_SimpleConversation Talk = Rat.GetComponent<Interaction_SimpleConversation>();
                if (Talk == null || Done(Talk))
                    continue;
                Bring(Talk, Index == Talked);
                Changed.Add($"{Names[Index]} out");
            }
            if (Changed.Count > 0)
                Plugin.Logger?.LogInfo($"[COOP] Ratau as in the host's game after its talk: {string.Join(", ", Changed)}");
        }

        /* The host's Ratau went after its talk was over: ours goes too, if it is one we brought out */
        private static string Went(int Index)
        {
            GameObject Rat = Child(Onboarding.Instance, Index);
            Interaction_SimpleConversation Talk = Rat != null ? Rat.GetComponent<Interaction_SimpleConversation>() : null;
            if (Rat == null || !Rat.activeSelf)
                return $"{Name(Index)} isn't out here";
            if (Talk == null || !Held.Contains(Talk))
                return $"{Name(Index)} here is our story's, not one we brought out";
            Rat.SetActive(false);
            ++Applied;
            Plugin.Logger?.LogInfo($"[COOP] Ratau as in the host's game: {Name(Index)} away, as the host's went after its talk");
            return null;
        }

        /* Out, and not to be talked to: the talk is the host's. Seen at once only for the host's talk: one the
           host's game keeps hidden until it is walked up to (its Start hides it) stays so here */
        private static void Bring(Interaction_SimpleConversation Talk, bool Seen)
        {
            Talk.Interactable = false;
            if (!Held.Contains(Talk))
                Held.Add(Talk);
            Talk.gameObject.SetActive(true);
            if (Seen && Talk.AnimateBeforeConversation && Talk.HideBeforeTriggered && Talk.Spine != null)
                Plugin.MonoInstance.StartCoroutine(Render(Talk));
        }

        /* The talk's Start, its first frame out, hides a Ratau that comes in with an animation: the host's is seen */
        private static IEnumerator Render(Interaction_SimpleConversation Talk)
        {
            yield return null;
            MeshRenderer Mesh = Talk != null && Talk.Spine != null ? Talk.Spine.GetComponent<MeshRenderer>() : null;
            if (Mesh != null)
                Mesh.enabled = true;
        }

        /* The game's own test in the talk's Start: a child done with is destroyed as it comes out */
        private static bool Done(Interaction_SimpleConversation Talk)
        {
            if (Talk.DeleteConditions == null || Talk.DeleteConditions.Count == 0)
                return false;
            foreach (var Condition in Talk.DeleteConditions)
            {
                if (DataManager.Instance.GetVariable(Condition.Variable) != Condition.Condition)
                    return false;
            }
            return true;
        }

        /* OUR ONBOARDING TAKES ITS CHILDREN AGAIN ******************************/

        [HarmonyPatch]
        private static class HideAllPatch
        {
            [HarmonyPatch(typeof(Onboarding), "HideAll")]
            [HarmonyPrefix]
            private static void Prefix() => Free("our story's next step");
        }

        /// <summary>
        /// The children we held can be talked to again: our own Onboarding (each of its steps starts
        /// with HideAll) has them back
        /// </summary>
        private static void Free(string Why)
        {
            DropPending(Why);
            try
            {
                int Count = 0;
                foreach (Interaction_SimpleConversation Talk in Held)
                {
                    if (Talk == null)
                        continue;
                    Talk.Interactable = true;
                    ++Count;
                }
                Held.Clear();
                if (Count > 0)
                {
                    ++Freed;
                    Plugin.Logger?.LogInfo($"[COOP] Ratau can be talked to again ({Why}): {Count}");
                }
            }
            catch (Exception Error)
            {
                Plugin.Logger?.LogWarning($"[COOP] Freeing Ratau failed: {Error.Message}");
            }
        }

        /// <summary>
        /// The session is over. A Ratau we brought out stays one that can't be talked to: a friend
        /// whose game stays on behind the reconnect screen would otherwise walk into his talk (all of
        /// them start on their own close by) and play it on the host's save. Held outlives the session
        /// on purpose (the -cotlmp-sessions test lists it as RatauSync.Held = N): our own story's next
        /// step (HideAll) has them back, and a save that comes in during another transition may not
        /// load the cult anew. What waited is dropped, all of it.
        /// </summary>
        internal static void ResetSession() => DropPending("the session is over");
    }
}

/* EOF */
