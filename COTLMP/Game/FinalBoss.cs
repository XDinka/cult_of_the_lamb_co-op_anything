/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     The final altar's choice is made once, for both players
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// The choice at the final altar — refuse the crown or kneel — plays out for both players.
    /// </summary>
    /// <remarks>
    /// The last place of the game runs on local scene events: a walked-over trigger plays the
    /// entrance, the altar's wheel calls refuse or kneel on this game only. As in the game's own
    /// co-op, whoever reaches the altar decides for the room: the choice is sent over, and the
    /// partner's game makes the same call, with its own cinematic. Their altar is put out so the
    /// choice can't be made twice, and a puppet never springs the scene's walk-over triggers.
    /// </remarks>
    internal static class FinalBoss
    {
        private static readonly FieldInfo ControllerPlayer = AccessTools.Field(typeof(DeathCatController), "playerFarming");
        private static readonly FieldInfo AltarActivated = AccessTools.Field(typeof(Interaction_FinalBossAltar), "Activated");

        /* What happens at the altar, in the order it can happen; the fight's stages themselves run
           as in every leader's room: the owner plays them, the follower's frozen copies die along
           (RoomSync keeps a leader's own death wired), and each death carries the scene on.
           The altar's question shows on both screens: each player's icon stands on the answer they
           point at (as in conversations), the friend's pick is a vote, the host's hand decides. */
        private const byte OpRefuse = 0, OpKneel = 1, OpKilled = 2, OpWheel = 10, OpPoint = 11;

        private static string Name(byte Op) => Op switch
        {
            OpKneel => "knelt",
            OpKilled => "saw the one who waits fall",
            OpWheel => "stand at the crown's question",
            _ => "refused the crown",
        };

        /* The partner's step is being replayed here: don't send it back */
        private static bool Applying;

        /* The steps already taken this fight, ours or theirs */
        private static readonly System.Collections.Generic.HashSet<byte> Done = new();

        /* Counters for the test harness report */
        internal static int Sent, Received, Applied;

        internal static void ResetSession()
        {
            Done.Clear();
            EndingRan = false;
            BigCame = false;
        }

        /// <summary>
        /// The partner's game took a step of the fight: take the same one here.
        /// </summary>
        public static void OnRelay(BinaryReader Reader)
        {
            byte Op = Reader.ReadByte();
            ++Received;
            if (Op == OpPoint)
            {
                PartnerPick = Reader.ReadSByte();
                PlaceBadges();
                return;
            }
            Plugin.Logger?.LogInfo($"[COOP] In the final fight the partner {Name(Op)}");
            if (!Done.Add(Op))
                return;
            if (Op == OpWheel)
            {
                OpenOurWheel();
                return;
            }
            DeathCatController Controller = UnityEngine.Object.FindObjectOfType<DeathCatController>();
            if (Controller == null)
            {
                Plugin.Logger?.LogWarning("[COOP] The partner's step in the final fight, but there is no fight here");
                return;
            }
            if (Op <= OpKneel)
                PutOutAltar();
            try
            {
                /* The entrance scene picks the controller's player as the nearest one, which here can be
                   the partner's puppet (no animator, no weapon data): the step walks OUR lamb instead */
                if (ControllerPlayer.GetValue(Controller) is not PlayerFarming Walker || Walker == null ||
                    Walker.health == null || PlayerManager.IsPuppet(Walker.health))
                    ControllerPlayer.SetValue(Controller, PlayerFarming.Instance);
                Applying = true;
                switch (Op)
                {
                    case OpKneel: Controller.KneelCallback(); break;
                    case OpKilled: SweepFight(Controller); Controller.DeathCatKilled(); break;
                    default: StartFight(Controller); break;
                }
                ++Applied;
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Taking the partner's step here failed: {e}");
            }
            finally
            {
                Applying = false;
            }
        }

        /// <summary>
        /// The fight as the partner's refusal starts it, without the scene around it: as in any
        /// leader's room, this game only wakes its own copies of the room's enemies; the partner's
        /// cinematic reaches us through their puppet and the lines overlay
        /// </summary>
        private static void StartFight(DeathCatController Controller)
        {
            DataManager.Instance.BossesEncountered.Add(PlayerFarming.Location);
            foreach (BaseMonoBehaviour Part in (System.Collections.Generic.List<BaseMonoBehaviour>)
                     AccessTools.Field(typeof(DeathCatController), "guardian1Components").GetValue(Controller))
                if (Part != null)
                    Part.enabled = true;
            AccessTools.Method(typeof(DeathCatController), "ResetPlayerWalking").Invoke(Controller, null);
            if (AccessTools.Field(typeof(DeathCatController), "guardian1Intro").GetValue(Controller) is BossIntro Intro && Intro != null)
                Controller.StartCoroutine(Intro.PlayRoutine(false));
            /* We may be kneeling at the altar's talk ourselves: the fight begins, the talk is over */
            try
            {
                GameManager.GetInstance().OnConversationEnd();
                if (PlayerFarming.Instance != null && PlayerFarming.Instance.state.CURRENT_STATE == StateMachine.State.CustomAnimation)
                    PlayerFarming.Instance.state.CURRENT_STATE = StateMachine.State.Idle;
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Ending the altar's talk failed: {e.Message}");
            }
            Plugin.Logger?.LogInfo("[COOP] The final fight wakes here as the partner starts it");
        }

        /* Our side took a step: tell the partner, once */
        private static void Tell(byte Op)
        {
            bool Was = !Done.Add(Op);
            if (Op <= OpKneel && !Applying)
                PutOutAltar();
            if (Was || Applying || !CoopSync.InSession)
                return;
            try
            {
                CoopSync.Send(CoopSync.Kind.FinalChoice, Writer => Writer.Write(Op));
                ++Sent;
                Plugin.Logger?.LogInfo($"[COOP] In the final fight we {Name(Op)}, sent to the partner");
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Sending our step in the final fight failed: {e.Message}");
            }
        }

        /// <summary>
        /// By the ending the partner's fight holds nothing alive: whatever our slower scene chain
        /// still shows is put away the way RoomSync removes enemies the owner doesn't have, so the
        /// fallen one's follower doesn't stand inside a body that should be gone
        /// </summary>
        private static void SweepFight(DeathCatController Controller)
        {
            try
            {
                foreach (Health Enemy in new System.Collections.Generic.List<Health>(Health.team2))
                    if (Enemy != null && Enemy.HP > 0f)
                        CoopSync.KillLocal(Enemy);
                if (AccessTools.Field(typeof(DeathCatController), "deathCatClone").GetValue(Controller) is DeathCatClone Shadow && Shadow != null)
                    Shadow.gameObject.SetActive(false);
                if (AccessTools.Field(typeof(DeathCatController), "deathCatBig").GetValue(Controller) is EnemyDeathCatBoss Big && Big != null)
                {
                    if (Big.BaseFormSpine != null)
                        Big.BaseFormSpine.gameObject.SetActive(false);
                    if (Big.Spine != null)
                        Big.Spine.gameObject.SetActive(false);
                }
                Plugin.Logger?.LogInfo("[COOP] The fight's leftovers are put away for the ending");
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Putting the fight away failed: {e.Message}");
            }
        }

        /* The great form came (its transform ran here), so the shadow's death scene carried through */
        private static bool BigCame;

        /// <summary>
        /// The shadow's own death scene can trip over scenery this game hasn't staged (seen on the
        /// friend's side): if it never reaches the great form, bring the transform in its place
        /// </summary>
        private static System.Collections.IEnumerator BigAnyway(DeathCatController Controller)
        {
            yield return new WaitForSecondsRealtime(12f);
            if (BigCame || Controller == null || PlayerFarming.Location != FollowerLocation.Boss_5 || Done.Contains(OpKilled))
                yield break;
            Plugin.Logger?.LogInfo("[COOP] The shadow's death scene broke off: the great form comes anyway");
            try
            {
                if (ControllerPlayer.GetValue(Controller) is not PlayerFarming Walker || Walker == null ||
                    Walker.health == null || PlayerManager.IsPuppet(Walker.health))
                    ControllerPlayer.SetValue(Controller, PlayerFarming.Instance);
                Controller.DeathCatBigTransform();
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Bringing the great form failed: {e.Message}");
            }
        }

        /* The ending runs once, wherever the call comes from (our mirror and the body's own fall can race) */
        private static bool EndingRan;

        /* No second choice on this side, whoever made the first */
        private static void PutOutAltar()
        {
            CloseWheel();
            Interaction_FinalBossAltar Altar = UnityEngine.Object.FindObjectOfType<Interaction_FinalBossAltar>();
            if (Altar == null)
                return;
            AltarActivated.SetValue(Altar, true);
            Altar.enabled = false;
        }

        /* THE ALTAR'S QUESTION, ON BOTH SCREENS ******************************/

        private static readonly FieldInfo AltarWheel = AccessTools.Field(typeof(Interaction_FinalBossAltar), "c");
        private static readonly FieldInfo WheelChoice1 = AccessTools.Field(typeof(ChoiceIndicator), "_choice1");
        private static readonly FieldInfo WheelChoice2 = AccessTools.Field(typeof(ChoiceIndicator), "_choice2");

        /* The question both see, and the answer each player stands on */
        private static ChoiceIndicator Wheel;
        private static sbyte OurPick = -1, PartnerPick = -1;
        private static UnityEngine.UI.Image OurBadge, PartnerBadge;

        private static bool IsHost => Plugin.GlobalsInternal.IsServerCreator;

        private static MMButton WheelButton(int Option)
        {
            if (Wheel == null)
                return null;
            return ((ChoiceIndicator.ChoiceOption)(Option == 0 ? WheelChoice1 : WheelChoice2).GetValue(Wheel)).Button;
        }

        private static void TrackWheel(ChoiceIndicator Indicator)
        {
            Wheel = Indicator;
            OurPick = PartnerPick = -1;
            OurBadge = PartnerBadge = null;
        }

        /// <summary>
        /// The partner stands at the altar's question: the same question comes up here, through our
        /// own altar, so both can point and both see the icons. The friend's wheel only votes.
        /// </summary>
        private static void OpenOurWheel()
        {
            try
            {
                Interaction_FinalBossAltar Altar = UnityEngine.Object.FindObjectOfType<Interaction_FinalBossAltar>();
                if (Altar == null)
                {
                    Plugin.Logger?.LogWarning("[COOP] The partner is at the crown's question, but there is no altar here");
                    return;
                }
                if (AltarWheel.GetValue(Altar) is ChoiceIndicator Open && Open != null)
                {
                    TrackWheel(Open);
                    return;
                }
                if (Altar.state == null)
                    Altar.state = PlayerFarming.Instance?.state;
                AltarActivated.SetValue(Altar, true);
                Applying = true;
                try
                {
                    AccessTools.Method(typeof(Interaction_FinalBossAltar), "GiveChoice").Invoke(Altar, null);
                }
                finally
                {
                    Applying = false;
                }
                TrackWheel(AltarWheel.GetValue(Altar) as ChoiceIndicator);
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Raising the crown's question here failed: {e.Message}");
            }
        }

        /* Our icon moves to the answer we point at, and the partner hears of it */
        private static void SendPoint(int Option)
        {
            if (OurPick == Option)
                return;
            OurPick = (sbyte)Option;
            PlaceBadges();
            if (!CoopSync.InSession)
                return;
            try
            {
                CoopSync.Send(CoopSync.Kind.FinalChoice, Writer =>
                {
                    Writer.Write(OpPoint);
                    Writer.Write(OurPick);
                });
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Sending our answer failed: {e.Message}");
            }
        }

        /* The players' icons above the answers, ours and the partner's side by side on a shared one */
        private static void PlaceBadges()
        {
            if (Wheel == null)
                return;
            float Shift = OurPick >= 0 && OurPick == PartnerPick ? 0.55f : 0f;
            PlaceBadge(ref PartnerBadge, PartnerPick, Dialogs.Icons.For(false), Shift);
            PlaceBadge(ref OurBadge, OurPick, Dialogs.Icons.For(true), -Shift);
        }

        private static void PlaceBadge(ref UnityEngine.UI.Image Icon, int Option, Sprite Picture, float Shift)
        {
            MMButton Button = Option >= 0 ? WheelButton(Option) : null;
            if (Button == null || Picture == null)
            {
                if (Icon != null)
                    Icon.gameObject.SetActive(false);
                return;
            }
            RectTransform Holder = (RectTransform)Button.transform;
            if (Icon == null)
            {
                GameObject Badge = new("COTLMP Altar Icon", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.LayoutElement));
                Badge.GetComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
                Icon = Badge.GetComponent<UnityEngine.UI.Image>();
                Icon.preserveAspect = true;
                Icon.raycastTarget = false;
            }
            if (Icon.transform.parent != Holder)
                Icon.transform.SetParent(Holder, false);
            float Size = Mathf.Max(32f, Holder.rect.height * 0.55f);
            RectTransform Rect = Icon.rectTransform;
            Rect.anchorMin = Rect.anchorMax = new Vector2(0.5f, 1f);
            Rect.pivot = new Vector2(0.5f, 0.5f);
            Rect.sizeDelta = new Vector2(Size, Size);
            Rect.anchoredPosition = new Vector2(Shift * Size * 2f, Size * 0.7f);
            Icon.sprite = Picture;
            Icon.transform.SetAsLastSibling();
            Icon.gameObject.SetActive(true);
        }

        /* The question leaves both screens once the host's hand has decided */
        private static void CloseWheel()
        {
            if (Wheel == null)
                return;
            try
            {
                AccessTools.Method(typeof(ChoiceIndicator), "Hide").Invoke(Wheel, null);
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Closing the crown's question failed: {e.Message}");
            }
            Wheel = null;
        }

        [HarmonyPatch]
        private static class FinalBossPatches
        {
            /// <summary>
            /// The fight's room counts as a leader's room, so the mod's leader manners apply: the first
            /// in waits by the door, a scene starting for one pulls the other in, scenes end together
            /// </summary>
            [HarmonyPatch(typeof(MMBiomeGeneration.BiomeGenerator), "Start")]
            [HarmonyPostfix]
            private static void FloorMade(MMBiomeGeneration.BiomeGenerator __instance)
            {
                /* The player's location is set later than the generator starts: ask the generator itself */
                if (__instance.DungeonLocation == FollowerLocation.Boss_5)
                    Plugin.MonoInstance.StartCoroutine(MarkFightRoom(__instance));
            }

            /* The rooms and their prefabs come in over a few frames: keep looking for a short while */
            private static System.Collections.IEnumerator MarkFightRoom(MMBiomeGeneration.BiomeGenerator Generator)
            {
                for (float Since = Time.unscaledTime; Time.unscaledTime - Since < 30f; )
                {
                    if (Generator == null || Generator != MMBiomeGeneration.BiomeGenerator.Instance)
                        yield break;
                    foreach (MMBiomeGeneration.BiomeRoom Room in Generator.Rooms ?? new System.Collections.Generic.List<MMBiomeGeneration.BiomeRoom>())
                        if (Room?.GameObject != null && Room.GameObject.GetComponentInChildren<FinalBossIntroManager>(true) != null)
                        {
                            if (!Room.IsBoss)
                                Plugin.Logger?.LogInfo("[COOP] The final fight's room keeps a leader room's manners");
                            Room.IsBoss = true;
                            yield break;
                        }
                    yield return new WaitForSecondsRealtime(1f);
                }
            }

            /// <summary>
            /// The altar's question came up here: it comes up for the partner too
            /// </summary>
            [HarmonyPatch(typeof(Interaction_FinalBossAltar), "GiveChoice")]
            [HarmonyPostfix]
            private static void QuestionUp(Interaction_FinalBossAltar __instance)
            {
                if (!CoopSync.InSession)
                    return;
                TrackWheel(AltarWheel.GetValue(__instance) as ChoiceIndicator);
                Tell(OpWheel);
            }

            /* The answer each player stands on carries over, with the players' icons above the answers */
            [HarmonyPatch(typeof(ChoiceIndicator), "OnOptionSelected")]
            [HarmonyPostfix]
            private static void Pointed(ChoiceIndicator __instance, MMButton button)
            {
                if (__instance == null || __instance != Wheel || !CoopSync.InSession)
                    return;
                SendPoint(button == WheelButton(0) ? 0 : 1);
            }

            /// <summary>
            /// The friend's pick on the crown's question is a vote: their icon lands on it, the wheel
            /// stays, and the host's own pick is what turns the page (then it closes for both)
            /// </summary>
            [HarmonyPatch(typeof(ChoiceIndicator), "OnOptionClicked")]
            [HarmonyPrefix]
            private static bool Picked(ChoiceIndicator __instance, ChoiceIndicator.ChoiceOption choiceOption)
            {
                if (__instance == null || __instance != Wheel || !CoopSync.InSession || IsHost)
                    return true;
                SendPoint(choiceOption.Button == WheelButton(0) ? 0 : 1);
                return false;
            }

            /* The wheel left the screen on its own (a menu, a scene): forget it */
            [HarmonyPatch(typeof(ChoiceIndicator), "OnDisable")]
            [HarmonyPostfix]
            private static void WheelGone(ChoiceIndicator __instance)
            {
                if (__instance == Wheel)
                    Wheel = null;
            }

            [HarmonyPatch(typeof(DeathCatController), nameof(DeathCatController.RefuseToKneel))]
            [HarmonyPostfix]
            private static void Refused() => Tell(OpRefuse);

            [HarmonyPatch(typeof(DeathCatController), nameof(DeathCatController.KneelCallback))]
            [HarmonyPostfix]
            private static void Knelt() => Tell(OpKneel);

            [HarmonyPatch(typeof(DeathCatController), nameof(DeathCatController.DeathCatKilled))]
            [HarmonyPrefix]
            private static bool EndingOnce()
            {
                if (EndingRan)
                    return false;
                EndingRan = true;
                return true;
            }

            [HarmonyPatch(typeof(DeathCatController), nameof(DeathCatController.DeathCatKilled))]
            [HarmonyPostfix]
            private static void Felled() => Tell(OpKilled);

            /* The great form's transform ran here: the shadow's death scene carried through */
            [HarmonyPatch(typeof(DeathCatController), nameof(DeathCatController.DeathCatBigTransform))]
            [HarmonyPostfix]
            private static void BigHere() => BigCame = true;

            /* The shadow fell: if its death scene breaks off, the great form still comes */
            [HarmonyPatch(typeof(DeathCatClone), nameof(DeathCatClone.OnDie))]
            [HarmonyPostfix]
            private static void ShadowFell()
            {
                if (!CoopSync.InSession || BigCame)
                    return;
                DeathCatController Controller = UnityEngine.Object.FindObjectOfType<DeathCatController>();
                if (Controller != null)
                    Plugin.MonoInstance.StartCoroutine(BigAnyway(Controller));
            }

            /// <summary>
            /// The shadow's death scene reads the boss health bar, but the bar only comes up late in
            /// the transform cinematic — and with a partner the shadow can fall before a cinematic
            /// ends here. The bar comes up with the shadow itself, so its death always has it.
            /// </summary>
            [HarmonyPatch(typeof(DeathCatClone), "OnEnable")]
            [HarmonyPostfix]
            private static void ShadowUp(DeathCatClone __instance)
            {
                try
                {
                    if (!CoopSync.InSession || UIBossHUD.Instance != null || __instance == null)
                        return;
                    Health Life = __instance.GetComponent<Health>();
                    if (Life != null)
                        UIBossHUD.Play(Life, I2.Loc.ScriptLocalization.NAMES.DeathNPC);
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogWarning($"[COOP] Raising the boss bar early failed: {e.Message}");
                }
            }

            /* The scene's walk-over triggers take any player: never the partner's puppet */
            [HarmonyPatch(typeof(TriggerCallback), "OnTriggerEnter2D")]
            [HarmonyPrefix]
            private static bool Walked(Collider2D collision)
            {
                PlayerFarming Player = collision == null ? null : collision.GetComponentInParent<PlayerFarming>();
                return Player == null || Player.health == null || !PlayerManager.IsPuppet(Player.health);
            }
        }
    }
}

/* EOF */
