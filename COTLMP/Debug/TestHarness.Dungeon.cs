/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     The test harness: runs, fights, floors and the leader
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP.Data;
using COTLMP.Game;
using COTLMP.Ui;
using MMTools;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Debug
{
    internal static partial class TestHarness
    {
        private static IEnumerator EnterDungeon()
        {
            while (!Plugin.GlobalsInternal.InGameSession || PlayerFarming.Instance == null || MMTransition.IsPlaying)
                yield return null;
            yield return new WaitForSecondsRealtime(AutoDungeon);

            Plugin.Logger?.LogWarning($"{Tag} Starting a run into {DungeonScene} like the base door does");
            GameManager.NewRun("", false, DungeonStart);
            MMTransition.Play(MMTransition.TransitionType.ChangeRoomWaitToResume, MMTransition.Effect.BlackFade, DungeonScene, 1f, "", null);

            while (SceneManager.GetActiveScene().name != DungeonScene || PlayerFarming.Instance == null)
                yield return null;
            yield return new WaitForSecondsRealtime(3f);
            MMTransition.CanResume = true;
            MMTransition.ResumePlay();
            Plugin.Logger?.LogWarning($"{Tag} In the dungeon at {COTLMP.Game.CoopSync.CurrentPlace()}");

            /* Walk the same way through the rooms on both sides: the first unvisited door, N, E, S, W */
            while (SceneManager.GetActiveScene().name == DungeonScene)
            {
                yield return new WaitForSecondsRealtime(Plugin.GlobalsInternal.IsServerCreator ? 5f : 1f);
                MMBiomeGeneration.BiomeRoom Room = MMBiomeGeneration.BiomeGenerator.Instance?.CurrentRoom;
                if (Room == null || MMTransition.IsPlaying || COTLMP.Game.SceneEnds.Waiting)
                    continue;

                /* The joining copy just follows the host into the next room */
                if (!Plugin.GlobalsInternal.IsServerCreator)
                {
                    string Mine = COTLMP.Game.CoopSync.CurrentPlace();
                    string Partner = COTLMP.Game.RoomSync.PartnerLastPlace;

                    /* In the test apart, linger so the host is rooms ahead */
                    if (Solo && Partner != Mine)
                        yield return new WaitForSecondsRealtime(6f);

                    /* Both at the floor's exit: through it on our own, the partner's puppet takes nobody along */
                    if (NextFloor && Partner == Mine && Mine != null && HasNextLayer(Room))
                    {
                        yield return new WaitForSecondsRealtime(3f);
                        yield return GoToNextFloor(Room, false);
                        continue;
                    }
                    if (Partner == null || Mine == null || Partner == Mine || !Partner.Contains("@") || !Mine.Contains("@"))
                        continue;

                    /* The host went on to the next floor: after them */
                    if (Partner.Substring(0, Partner.IndexOf('@')) != Mine.Substring(0, Mine.IndexOf('@')))
                    {
                        if (NextFloor)
                            yield return GoToNextFloor(Room, false);
                        continue;
                    }
                    string[] P = Partner.Substring(Partner.IndexOf('@') + 1).Split(',');
                    Vector2Int? Step = NextStep(Room, new Vector2Int(int.Parse(P[0]), int.Parse(P[1])));
                    if (!Step.HasValue)
                        continue;
                    Vector2Int Gap = Step.Value;
                    Plugin.Logger?.LogWarning($"{Tag} Following the host {Gap} from {Room.x},{Room.y}");
                    MMTransition.Play(MMTransition.TransitionType.ChangeRoom, MMTransition.Effect.BlackFade, MMTransition.NO_SCENE, 0.5f, "", () => MMBiomeGeneration.BiomeGenerator.ChangeRoom(Gap));
                    continue;
                }

                /* The host waits for the partner in this room, then for the fight to end (or 40 s) */
                float Waited = 0f;
                while (!Solo && Waited < 90f && !AnyRemotePlayer())
                {
                    yield return new WaitForSecondsRealtime(1f);
                    Waited += 1f;
                }
                yield return new WaitForSecondsRealtime(8f);
                Waited = 0f;
                while (Waited < 40f && Health.team2.Exists(Enemy => Enemy != null && Enemy.HP > 0f))
                {
                    yield return new WaitForSecondsRealtime(1f);
                    Waited += 1f;
                }
                yield return new WaitForSecondsRealtime(4f);
                while (COTLMP.Game.SceneEnds.Waiting)
                    yield return null;

                Vector2Int? Way = null;
                if (Room.N_Room != null && Room.N_Room.Connected && Room.N_Room.Room != null && !Room.N_Room.Room.Visited) Way = new Vector2Int(0, 1);
                else if (Room.E_Room != null && Room.E_Room.Connected && Room.E_Room.Room != null && !Room.E_Room.Room.Visited) Way = new Vector2Int(1, 0);
                else if (Room.S_Room != null && Room.S_Room.Connected && Room.S_Room.Room != null && !Room.S_Room.Room.Visited) Way = new Vector2Int(0, -1);
                else if (Room.W_Room != null && Room.W_Room.Connected && Room.W_Room.Room != null && !Room.W_Room.Room.Visited) Way = new Vector2Int(-1, 0);
                if (!Way.HasValue && NextFloor)
                {
                    yield return GoToNextFloor(Room, true);
                    continue;
                }
                if (!Way.HasValue)
                {
                    Plugin.Logger?.LogWarning($"{Tag} No unvisited room next to {Room.x},{Room.y}");
                    continue;
                }

                Vector2Int Direction = Way.Value;
                Plugin.Logger?.LogWarning($"{Tag} Going {Direction} from {Room.x},{Room.y}");
                MMTransition.Play(MMTransition.TransitionType.ChangeRoom, MMTransition.Effect.BlackFade, MMTransition.NO_SCENE, 0.5f, "", () => MMBiomeGeneration.BiomeGenerator.ChangeRoom(Direction));
            }
        }

        private static IEnumerator HitEnemies()
        {
            var Wait = new WaitForSecondsRealtime(1f);

            while (true)
            {
                yield return Wait;
                /* A player can't swing during a cinematic: hitting through one would outrun the fight's stages */
                if (PlayerFarming.Instance == null || MMTransition.IsPlaying || PlayerFarming.Instance.IsKnockedOut || LetterBox.IsPlaying)
                    continue;

                /* Collect what the fight dropped, as walking over it would */
                foreach (PickUp Item in new System.Collections.Generic.List<PickUp>(PickUp.PickUps))
                {
                    if (Item != null && Item.gameObject.activeInHierarchy && Item.CanBePickedUp && Item.AddToInventory)
                    {
                        Item.Player = PlayerFarming.Instance.gameObject;
                        Plugin.Logger?.LogWarning($"{Tag} Picking up {Item.type} x{Item.Quantity}");
                        Item.PickMeUp();
                        break;
                    }
                }

                /* Fight only with the partner in the room, like players would (unless testing apart) */
                if (Health.team2.Count == 0 || (!Solo && !AnyRemotePlayer()))
                    continue;

                Health Nearest = null;
                try
                {
                    foreach (Health Enemy in new System.Collections.Generic.List<Health>(Health.team2))
                    {
                        if (Enemy != null && Enemy.HP > 0f && Enemy.gameObject.activeInHierarchy &&
                            (Nearest == null || Enemy.transform.position.sqrMagnitude < Nearest.transform.position.sqrMagnitude))
                            Nearest = Enemy;
                    }
                }
                catch (Exception e)
                {
                    /* A body can be torn down between frames while we look at it */
                    Plugin.Logger?.LogWarning($"{Tag} Looking for an enemy threw: {e.Message}");
                    continue;
                }
                if (Nearest == null)
                    continue;

                /*
                 * The test copies' lamb has no weapon, and the game's player-hit path reads it, so hit the
                 * way tarot damage does (the enemy as its own attacker). The HP change is what gets synced.
                 */
                bool Hit = false;
                string Name = Nearest.name;
                try
                {
                    Hit = Nearest.DealDamage(Mathf.Max(1f, Nearest.totalHP * 0.4f), Nearest.gameObject, Nearest.transform.position, false, Health.AttackTypes.Melee, true);
                    Plugin.Logger?.LogWarning($"{Tag} Hit {Name} -> {Hit}, hp {Nearest.HP:F1}/{Nearest.totalHP:F1}");
                }
                catch (Exception e)
                {
                    /* A killing blow can tear the body down mid-swing */
                    Plugin.Logger?.LogWarning($"{Tag} Hitting {Name} threw: {e.Message}");
                }
            }
        }

        private static IEnumerator Draw()
        {
            string Last = null;
            bool Unlocked = false;
            while (true)
            {
                yield return new WaitForSecondsRealtime(1f);
                if (PlayerFarming.Instance == null || DataManager.Instance == null)
                    continue;

                /* Make the host's save differ from the joining one (in memory only, test copies) */
                if (!Unlocked && Plugin.GlobalsInternal.IsServerCreator && Plugin.GlobalsInternal.InGameSession)
                {
                    Unlocked = true;
                    foreach (TarotCards.Card Card in DataManager.AllTrinkets)
                    {
                        if (!DataManager.Instance.PlayerFoundTrinkets.Contains(Card))
                            DataManager.Instance.PlayerFoundTrinkets.Add(Card);
                    }
                    Plugin.Logger?.LogWarning($"{Tag} Host now has {DataManager.Instance.PlayerFoundTrinkets.Count} tarot cards");
                }

                string Place = COTLMP.Game.CoopSync.CurrentPlace();
                if (Place == null || Place == Last || !Place.Contains("#") || !AnyRemotePlayer())
                    continue;
                Last = Place;
                yield return new WaitForSecondsRealtime(3f);

                var Cards = new System.Collections.Generic.List<string>();
                for (int Index = 0; Index < 3; Index++)
                {
                    try
                    {
                        Cards.Add(TarotCards.DrawRandomCard(PlayerFarming.Instance)?.CardType.ToString() ?? "none");
                    }
                    catch (Exception e)
                    {
                        Cards.Add("error " + e.Message);
                    }
                }
                Plugin.Logger?.LogWarning($"{Tag} Drew in {Place}: {string.Join(", ", Cards)} (own pool {DataManager.Instance.PlayerFoundTrinkets.Count})");
            }
        }

        private static IEnumerator Shoot()
        {
            var Wait = new WaitForSecondsRealtime(3f);
            while (true)
            {
                yield return Wait;
                if (!COTLMP.Game.RoomSync.Owning)
                    continue;
                GameObject Prefab = COTLMP.Game.HazardSync.AnyProjectilePrefab();
                Vector3? Partner = COTLMP.Game.PlayerManager.GetAnyPuppetPosition();
                if (Prefab == null || !Partner.HasValue)
                    continue;
                try
                {
                    Vector3 From = Partner.Value + new Vector3(4f, 0f, 0f);
                    Projectile Shot = ObjectPool.Spawn(Prefab, MMRoomGeneration.GenerateRoom.Instance?.transform).GetComponent<Projectile>();
                    Shot.transform.position = From;
                    Shot.Angle = 180f;
                    Shot.Speed = 6f;
                    Shot.team = Health.Team.Team2;
                    Plugin.Logger?.LogWarning($"{Tag} Fired {Prefab.name} at the partner");
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogWarning($"{Tag} Firing failed: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Both copies go to the land's leader (the Leshy in the first land): the floor is made again
        /// with the leader's room first, the host's a moment before the friend's, as players never
        /// arrive at once; both log the scene, the conversation and the leader's health as it goes
        /// </summary>
        private static IEnumerator FightLeader()
        {
            while (SceneManager.GetActiveScene().name != DungeonScene || PlayerFarming.Instance == null || !AnyRemotePlayer() || MMTransition.IsPlaying)
                yield return null;
            bool Host = Plugin.GlobalsInternal.IsServerCreator;
            /* Both standing in the room before either changes anything */
            yield return new WaitForSecondsRealtime(3f);
            MMBiomeGeneration.BiomeGenerator Generator = MMBiomeGeneration.BiomeGenerator.Instance;
            /*
             * A layer's guardian (layers 1 to 3: the land's one guardian room, whose guardian goes by the layer) or
             * the land's bishop (layer 4: "Boss Room Dungeon 1_N" is land N's), for both at once
             */
            if (BossLayer > 0 && Generator != null)
            {
                Generator.LeaderRoomPath = BossLayer < 4 ? $"Assets/_Rooms/MiniBoss Room Dungeon {Land}.prefab" : $"Assets/_Rooms/Boss Room Dungeon 1_{Land}.prefab";
                GameManager.CurrentDungeonLayer = BossLayer;
            }
            yield return new WaitForSecondsRealtime((Host ? 8f : 9.5f) + BossLate);
            Plugin.Logger?.LogWarning($"{Tag} Boss test: going to the leader's room {Generator?.LeaderRoomPath}");
            if (Generator == null || string.IsNullOrEmpty(Generator.LeaderRoomPath))
                yield break;
            PlayerFarming.Instance.health.invincible = true;
            /* The leader's room is put at its place on a floor once the run's map has a stop */
            Map.Map RunMap = Map.MapManager.Instance?.CurrentMap ?? Map.MapManager.Instance?.GenerateNewMap();
            if (RunMap != null && RunMap.GetCurrentNode() == null && RunMap.nodes.Count > 0)
                RunMap.path.Add(RunMap.nodes[0].point);
            Generator.Regenerate(null);
            yield return new WaitForSecondsRealtime(1f);
            while (MMTransition.IsPlaying || Generator.CurrentRoom == null)
                yield return null;
            yield return new WaitForSecondsRealtime(2f);
            MMBiomeGeneration.BiomeRoom Lair = Generator.Rooms.FirstOrDefault(Room => Room != null && (Room.IsBoss || Room.y == MMBiomeGeneration.BiomeGenerator.BossCoords.y));
            Plugin.Logger?.LogWarning($"{Tag} Boss test: rooms {string.Join(", ", Generator.Rooms.Select(Room => $"{Room.x},{Room.y}{(Room.IsBoss ? "!" : "")}"))}; the leader's {Lair?.x},{Lair?.y}");
            if (Lair != null && Lair != Generator.CurrentRoom)
                MMBiomeGeneration.BiomeGenerator.ChangeRoom(Lair.x, Lair.y);
            yield return new WaitForSecondsRealtime(1f);
            while (MMTransition.IsPlaying)
                yield return null;
            yield return new WaitForSecondsRealtime(Host ? 2f : 3.5f);
            yield return WalkInAndWatch("Boss test");
        }

        /// <summary>
        /// Into the room ahead, where its fight starts, then its state every 2 s for 150 s, reading its talk on
        /// as a player would (the friend faster than the host)
        /// </summary>
        private static IEnumerator WalkInAndWatch(string Name)
        {
            bool Host = Plugin.GlobalsInternal.IsServerCreator;
            /* Into the room, where its fight starts */
            Plugin.Logger?.LogWarning($"{Tag} {Name}: walking into the room from {PlayerFarming.Instance.transform.position}");
            for (int Step = 0; Step < 14 && !LetterBox.IsPlaying && !MMTools.MMConversation.isPlaying; Step++)
            {
                PlayerFarming.Instance.GoToAndStop(PlayerFarming.Instance.transform.position + new Vector3(0f, 5f), null, false, false, null, 4f, true);
                yield return new WaitForSecondsRealtime(2.5f);
            }
            Plugin.Logger?.LogWarning($"{Tag} {Name}: stopped at {PlayerFarming.Instance.transform.position}, letterbox {LetterBox.IsPlaying}, talk {MMTools.MMConversation.isPlaying}, enemies {Health.team2.Count}");
            float Since = Time.unscaledTime;
            string Last = null;
            /* The room's leader, kept after it leaves the enemies: what it does once dead */
            Health Boss = null;
            while (Time.unscaledTime - Since < 150f)
            {
                yield return new WaitForSecondsRealtime(2f);
                Health Leader = null;
                foreach (Health Enemy in Health.team2)
                {
                    if (Enemy != null && (Leader == null || Enemy.totalHP > Leader.totalHP))
                        Leader = Enemy;
                }
                if (Leader != null && (Boss == null || Leader.totalHP > Boss.totalHP))
                    Boss = Leader;
                var BossState = Boss == null ? null : Boss.GetComponent<StateMachine>();
                var BossSpine = Boss == null ? null : Boss.GetComponentInChildren<Spine.Unity.SkeletonAnimation>();
                string BossNow = Boss == null ? "-" : $"{Boss.name}:{Boss.HP:F0}:{(Boss.gameObject.activeInHierarchy ? "on" : "off")}:{BossState?.CURRENT_STATE}:{BossSpine?.AnimationState?.GetCurrent(0)?.Animation?.Name}:{(Boss.GetComponent<UnitObject>()?.enabled == true ? "mind" : "nomind")}";
                string State = $"place={COTLMP.Game.CoopSync.CurrentPlace()} letterbox={LetterBox.IsPlaying} talk={MMTools.MMConversation.isPlaying}:{MMTools.MMConversation.Position} " +
                               $"leader={Leader?.name}:{Leader?.HP:F0}/{Leader?.totalHP:F0} enemies={Health.team2.Count} boss={BossNow} own={COTLMP.Game.RoomSync.Owning} follow={COTLMP.Game.RoomSync.Following} state={PlayerFarming.Instance?.state?.CURRENT_STATE}";
                if (State != Last)
                    Plugin.Logger?.LogWarning($"{Tag} {Name} {Time.unscaledTime - Since:F0}s: {State}");
                Last = State;
                /* The friend takes the leader's heart, once it's out: the host's must go with it */
                var Heart = UnityEngine.Object.FindObjectOfType<Interaction_MonsterHeart>();
                if (!Host && Heart != null && Heart.enabled && (bool)AccessTools.Field(typeof(Interaction_MonsterHeart), "Enabled").GetValue(Heart) &&
                    !LetterBox.IsPlaying && PlayerFarming.Instance != null)
                {
                    Plugin.Logger?.LogWarning($"{Tag} {Name}: taking the leader's heart");
                    Heart.OnInteract(PlayerFarming.Instance.state);
                }
                /* A tutorial window (the first leader's heart) closed as a player would, after a look */
                var Tutorial = UnityEngine.Object.FindObjectOfType<src.UI.Overlays.TutorialOverlay.UITutorialOverlayController>();
                if (Tutorial != null && Tutorial.isActiveAndEnabled)
                {
                    Plugin.Logger?.LogWarning($"{Tag} {Name}: closing a tutorial window");
                    Tutorial.Hide();
                }
                /* Read on as a player would, the friend faster than the host */
                var Box = MMTools.MMConversation.mmConversation;
                if (MMTools.MMConversation.isPlaying && Box != null)
                {
                    Box.TextPlayer?.SkipTypewriter();
                    AccessTools.Method(typeof(MMTools.MMConversation), "HandleNextClicked").Invoke(Box, null);
                    if (!Host)
                        AccessTools.Method(typeof(MMTools.MMConversation), "HandleNextClicked").Invoke(Box, null);
                }
            }
        }

        /// <summary>
        /// -cotlmp-finaltest: from the cult into the last place of the game (as its chained door does), the host first,
        /// then the fight's state as in the leader test
        /// </summary>
        private static IEnumerator FinalTogether()
        {
            yield return Until(() => Plugin.GlobalsInternal.InGameSession && PlayerFarming.Instance != null && AnyRemotePlayer() && !MMTransition.IsPlaying, 400f);
            bool Host = Plugin.GlobalsInternal.IsServerCreator;
            yield return new WaitForSecondsRealtime(Host ? 6f : 10f);
            Plugin.Logger?.LogWarning($"{Tag} Final test: through the chained door");
            GameManager.NewRun("", false);
            MMTransition.Play(MMTransition.TransitionType.ChangeRoomWaitToResume, MMTransition.Effect.BlackFade, "Dungeon Final", 1f, "", null);
            yield return Until(() => SceneManager.GetActiveScene().name == "Dungeon Final" && PlayerFarming.Instance != null, 60f);
            yield return new WaitForSecondsRealtime(3f);
            MMTransition.CanResume = true;
            MMTransition.ResumePlay();
            Plugin.Logger?.LogWarning($"{Tag} Final test: in {SceneManager.GetActiveScene().name} at {COTLMP.Game.CoopSync.CurrentPlace()}");
            if (PlayerFarming.Instance != null)
                PlayerFarming.Instance.health.invincible = true;
            yield return new WaitForSecondsRealtime(Host ? 4f : 6f);
            /* The landing room comes first; the intro, the podiums and the fight are a room north — go as its door would, the host a moment ahead */
            var Generator = MMBiomeGeneration.BiomeGenerator.Instance;
            MMBiomeGeneration.BiomeRoom IntroRoom = Generator?.Rooms.FirstOrDefault(Room => Room?.GameObject != null &&
                Room.GameObject.GetComponentInChildren<FinalBossIntroManager>(true) != null);
            Plugin.Logger?.LogWarning($"{Tag} Final test: rooms {string.Join(", ", Generator.Rooms.Select(Room => $"{Room.x},{Room.y}:{(Room.GameObject == null ? "-" : Room.GameObject.name)}{(Generator.CurrentRoom == Room ? "*" : "")}"))}; the intro's {IntroRoom?.x},{IntroRoom?.y}");
            if (IntroRoom != null && Generator.CurrentRoom != IntroRoom)
            {
                MMBiomeGeneration.BiomeGenerator.ChangeRoom(IntroRoom.x, IntroRoom.y);
                yield return new WaitForSecondsRealtime(1f);
                while (MMTransition.IsPlaying)
                    yield return null;
            }
            /* The intro lands everyone at the pentagram and brings the demons */
            yield return new WaitForSecondsRealtime(8f);
            FinalBossIntroManager Final = UnityEngine.Object.FindObjectOfType<FinalBossIntroManager>();
            Plugin.Logger?.LogWarning($"{Tag} Final test: manager {(Final == null ? "none" : "on")}, me {PlayerFarming.Instance.transform.position}, " +
                                      $"players {string.Join(",", PlayerFarming.players.Select(Player => $"{Player.transform.position}"))}");
            /* The podiums: a weapon and a curse each, the door opens once everyone holds both */
            if (Final != null)
            {
                foreach (string Kind in new[] { "weaponPodiums", "cursePodiums" })
                {
                    var Podiums = (Interaction_WeaponSelectionPodium[])AccessTools.Field(typeof(FinalBossIntroManager), Kind).GetValue(Final);
                    foreach (Interaction_WeaponSelectionPodium Podium in Host ? Podiums : Podiums.Reverse())
                    {
                        EquipmentType Before = Kind == "weaponPodiums" ? PlayerFarming.Instance.currentWeapon : PlayerFarming.Instance.currentCurse;
                        if (Before != EquipmentType.None)
                            break;
                        if (Podium == null || !Podium.isActiveAndEnabled)
                            continue;
                        Podium.OnInteract(PlayerFarming.Instance.state);
                        yield return new WaitForSecondsRealtime(4f);
                    }
                }
                RoomLockController Door = (RoomLockController)AccessTools.Field(typeof(FinalBossIntroManager), "blockingDoor").GetValue(Final);
                for (int Look = 0; Look < 10 && Door != null && !Door.Completed; Look++)
                    yield return new WaitForSecondsRealtime(2f);
                Plugin.Logger?.LogWarning($"{Tag} Final test: holding {PlayerFarming.Instance.currentWeapon}/{PlayerFarming.Instance.currentCurse}, " +
                                          $"partner {string.Join(",", PlayerFarming.players.Where(Player => Player != PlayerFarming.Instance).Select(Player => $"{Player.currentWeapon}/{Player.currentCurse}"))}, door open {Door?.Completed}");
            }
            /* The entrance scene: each game plays it as its walked-over trigger would (the test's walker
             * teleports and never crosses it), then reads the talk on as a player would */
            DeathCatController Fight = UnityEngine.Object.FindObjectOfType<DeathCatController>();
            Plugin.Logger?.LogWarning($"{Tag} Final test: fight controller {(Fight == null ? "none" : "found")}");
            if (Fight != null)
            {
                yield return new WaitForSecondsRealtime(Host ? 0f : 2f);
                Fight.Play();
                yield return Until(() => MMTools.MMConversation.isPlaying, 20f);
                float Reading = Time.unscaledTime;
                while ((MMTools.MMConversation.isPlaying || LetterBox.IsPlaying) && Time.unscaledTime - Reading < 90f)
                {
                    var Box = MMTools.MMConversation.mmConversation;
                    if (MMTools.MMConversation.isPlaying && Box != null)
                    {
                        Box.TextPlayer?.SkipTypewriter();
                        AccessTools.Method(typeof(MMTools.MMConversation), "HandleNextClicked").Invoke(Box, null);
                        if (!Host)
                            AccessTools.Method(typeof(MMTools.MMConversation), "HandleNextClicked").Invoke(Box, null);
                    }
                    yield return new WaitForSecondsRealtime(1f);
                }
                Plugin.Logger?.LogWarning($"{Tag} Final test: entrance scene done after {Time.unscaledTime - Reading:0}s");
                yield return new WaitForSecondsRealtime(3f);
            }
            /* The altar north: the host walks up and refuses the crown, the friend looks on from behind;
             * the friend's fight must start from the partner's choice alone */
            Interaction_FinalBossAltar Altar = UnityEngine.Object.FindObjectOfType<Interaction_FinalBossAltar>();
            Plugin.Logger?.LogWarning($"{Tag} Final test: altar {(Altar == null ? "none" : $"at {Altar.transform.position}")}");
            if (Altar != null)
            {
                /* The walk can't thread the arena: the test jumps to the spot, the host close enough to kneel */
                Vector3 Spot = Altar.transform.position + new Vector3(Host ? 0f : -1f, Host ? -2f : -7f);
                PlayerFarming.Instance.transform.position = Spot;
                yield return new WaitForSecondsRealtime(2f);
                Plugin.Logger?.LogWarning($"{Tag} Final test: by the altar at {PlayerFarming.Instance.transform.position}");
                if (Host)
                {
                    /* As a player would: the kneel bow plays, the question comes up for both, then the refusal */
                    var WheelField = AccessTools.Field(typeof(Interaction_FinalBossAltar), "c");
                    yield return Until(() => Altar == null || WheelField.GetValue(Altar) != null, 20f);
                    Plugin.Logger?.LogWarning($"{Tag} Final test: the crown's question is {(Altar != null && WheelField.GetValue(Altar) != null ? "up" : "missing")}");
                    yield return new WaitForSecondsRealtime(3f);
                    AccessTools.Method(typeof(Interaction_FinalBossAltar), "Refuse").Invoke(Altar, null);
                    Plugin.Logger?.LogWarning($"{Tag} Final test: refused the crown");
                }
            }
            Run("FinalGuardian", GuardianWatch());
            yield return WalkInAndWatch("Final test");
        }

        /* The first guardian's side of the fight every 5 s: whether its object woke and joined the enemies */
        private static IEnumerator GuardianWatch()
        {
            DeathCatController Fight = UnityEngine.Object.FindObjectOfType<DeathCatController>();
            if (Fight == null)
                yield break;
            var Guardian = AccessTools.Field(typeof(DeathCatController), "guardian1");
            var Parts = AccessTools.Field(typeof(DeathCatController), "guardian1Components");
            for (int Look = 0; Look < 24; Look++)
            {
                yield return new WaitForSecondsRealtime(5f);
                var Unit = (UnitObject)Guardian.GetValue(Fight);
                var List = (System.Collections.Generic.List<BaseMonoBehaviour>)Parts.GetValue(Fight);
                Health Life = Unit == null ? null : Unit.GetComponent<Health>();
                Plugin.Logger?.LogWarning($"{Tag} Final test guardian {Look * 5}s: unit={(Unit == null ? "-" : Unit.name)} " +
                                          $"active={(Unit != null && Unit.gameObject.activeInHierarchy)} " +
                                          $"parts={(List == null ? -1 : List.Count(Part => Part != null && Part.enabled))}/{List?.Count ?? -1} " +
                                          $"hp={(Life == null ? "-" : $"{Life.HP}/{Life.totalHP}")} team2={Health.team2.Count}");
                var WidgetOne = (UnityEngine.UI.Image)AccessTools.Field(typeof(HUD_Manager), "_playerOneWidget").GetValue(HUD_Manager.Instance);
                var WidgetTwo = (UnityEngine.UI.Image)AccessTools.Field(typeof(HUD_Manager), "_playerTwoWidget").GetValue(HUD_Manager.Instance);
                Plugin.Logger?.LogWarning($"{Tag} Final test markers {Look * 5}s: count={PlayerFarming.playersCount} " +
                                          $"players={string.Join(",", PlayerFarming.players.Select(Player => $"{Player.name}:lamb={Player.isLamb}:goatlook={COTLMP.Game.CoopHud.IsGoatPlayer(Player)}:puppet={COTLMP.Game.PlayerManager.IsPuppet(Player.health)}:us={Player == PlayerFarming.Instance}"))} " +
                                          $"one={(WidgetOne == null ? "-" : $"{(WidgetOne.gameObject.activeSelf ? "on" : "off")}:{WidgetOne.sprite?.name}")} " +
                                          $"two={(WidgetTwo == null ? "-" : $"{(WidgetTwo.gameObject.activeSelf ? "on" : "off")}:{WidgetTwo.sprite?.name}")} " +
                                          $"wearegoat={COTLMP.Game.Appearance.WeAreGoat} ingame={Plugin.GlobalsInternal.InGameSession}");
                var Talk = MMTools.MMConversation.CURRENT_CONVERSATION;
                Plugin.Logger?.LogWarning($"{Tag} Final test scenes {Look * 5}s: talk={(Talk == null ? "-" : $"{Talk.Entries?.Count ?? -1}lines")}:{MMTools.MMConversation.isPlaying} " +
                                          $"letterbox={LetterBox.IsPlaying} ts={Time.timeScale:0.00} " +
                                          $"held={COTLMP.Game.SharedScenes.TalksHeld}/{COTLMP.Game.SharedScenes.TalksReleased} gated={COTLMP.Game.SharedScenes.Gated} pulled={COTLMP.Game.SharedScenes.Pulled}");
            }
        }

        private static IEnumerator DieOnce()
        {
            /* Counted from the first moment both stand in one dungeon room */
            while (SceneManager.GetActiveScene().name != DungeonScene || PlayerFarming.Instance == null || !AnyRemotePlayer())
                yield return null;
            yield return new WaitForSecondsRealtime(AutoDie);

            PlayerFarming Player = PlayerFarming.Instance;
            Plugin.Logger?.LogWarning($"{Tag} Dying on purpose, hp {Player.health.HP}");
            Player.health.invincible = false;
            Player.health.untouchable = false;
            Player.health.HP = 0.5f;
            Player.health.DealDamage(5f, Player.gameObject, Player.transform.position, false, Health.AttackTypes.Melee, true, Health.AttackFlags.ForceKill);
            yield return new WaitForSecondsRealtime(2f);
            Plugin.Logger?.LogWarning($"{Tag} After dying: knocked out {Player.IsKnockedOut}, state {Player.state.CURRENT_STATE}, down {COTLMP.Game.CoopLife.WeAreDown}");
        }

        /// <summary>
        /// The host's hearts go down to two, then one, then back up, a few seconds each (the game
        /// bruises the lamb at two and gives it a black eye at one); nothing else hurts it meanwhile
        /// </summary>
        private static IEnumerator HurtTest()
        {
            while (SceneManager.GetActiveScene().name != DungeonScene || PlayerFarming.Instance == null || !AnyRemotePlayer())
                yield return null;
            /* Hosting starts after the switches are read */
            if (!Plugin.GlobalsInternal.IsServerCreator)
            {
                yield return WatchLooks();
                yield break;
            }
            yield return new WaitForSecondsRealtime(AutoHurt);

            PlayerFarming Player = PlayerFarming.Instance;
            float Full = Player.health.HP;
            Player.health.invincible = true;
            foreach (float Hearts in new[] { 2f, 1f, Full })
            {
                Player.health.HP = Hearts;
                Plugin.Logger?.LogWarning($"{Tag} Hurt test: our hp {Player.health.HP}, our look {COTLMP.Game.Appearance.Look:X}");
                yield return new WaitForSecondsRealtime(6f);
            }
            Player.health.invincible = false;
        }

        /// <summary>
        /// Logs and frames each look the partner's puppet takes on
        /// </summary>
        private static IEnumerator WatchLooks()
        {
            int Seen = -1, Frames = 0;
            while (true)
            {
                for (uint Id = 0; Id < InternalData.MaxPlayersPerServerInternal; Id++)
                {
                    int Look = PlayerManager.GetLook(Id);
                    if (Look < 0 || Look == Seen)
                        continue;
                    Seen = Look;
                    Plugin.Logger?.LogWarning($"{Tag} Hurt test: player {Id}'s puppet looks {Look:X} (bruises {COTLMP.Game.Appearance.LookHurt(Look)})");
                    if (Frames++ < 4)
                    {
                        yield return new WaitForSecondsRealtime(0.5f);
                        PlayerFarming Puppet = PlayerManager.GetPuppet(Id);
                        Vector3 OnScreen = Puppet != null && Camera.main != null ? Camera.main.WorldToScreenPoint(Puppet.transform.position) : Vector3.zero;
                        Plugin.Logger?.LogWarning($"{Tag} Hurt test: the puppet stands at {OnScreen.x:F0},{Screen.height - OnScreen.y:F0} of {Screen.width}x{Screen.height}");
                        Shot($"puppet-hurt{COTLMP.Game.Appearance.LookHurt(Look)}");
                    }
                }
                yield return new WaitForSecondsRealtime(0.2f);
            }
        }

        /// <summary>
        /// Stands next to the downed partner until the bar fills, as a player would
        /// </summary>
        private static IEnumerator Rescue()
        {
            while (true)
            {
                yield return null;
                if (!COTLMP.Game.CoopLife.PartnerDown || PlayerFarming.Instance == null || MMTransition.IsPlaying)
                    continue;
                Vector3? Downed = COTLMP.Game.PlayerManager.GetAnyPuppetPosition();
                if (!Downed.HasValue)
                    continue;
                PlayerFarming.Instance.transform.position = Downed.Value + new Vector3(0.6f, 0f, 0f);
                if (Time.frameCount % 60 == 0)
                    Plugin.Logger?.LogWarning($"{Tag} Reviving the partner, bar {COTLMP.Game.CoopLife.ReviveProgress:F2}");
                /* -cotlmp-dieonrevive: we fall halfway through getting them up */
                if (DieOnRevive && COTLMP.Game.CoopLife.ReviveProgress > 0.5f)
                {
                    DieOnRevive = false;
                    PlayerFarming Player = PlayerFarming.Instance;
                    Plugin.Logger?.LogWarning($"{Tag} Falling while getting the partner up");
                    Player.health.invincible = false;
                    Player.health.untouchable = false;
                    Player.health.HP = 0.5f;
                    Player.health.DealDamage(5f, Player.gameObject, Player.transform.position, false, Health.AttackTypes.Melee, true, Health.AttackFlags.ForceKill);
                }
            }
        }

        /// <summary>
        /// The joining copy clicks the first way on whenever a map comes up (the host's puppet at
        /// the door brings it up too), before the host's pick is taken for it
        /// </summary>
        private static IEnumerator ClickMaps()
        {
            Lamb.UI.UIAdventureMapOverlayController Clicked = null;
            while (true)
            {
                yield return new WaitForSecondsRealtime(0.25f);
                Lamb.UI.UIAdventureMapOverlayController Overlay = MapSync.Open;
                if (Overlay == null || Overlay == Clicked)
                    continue;
                yield return new WaitForSecondsRealtime(0.5f);
                Clicked = Overlay;
                global::Map.Node Current = Overlay.Map?.GetCurrentNode();
                if (MapSync.Open != Overlay || Current == null || Current.outgoing.Count == 0)
                    continue;
                global::Map.Point Choice = Current.outgoing[0];
                Lamb.UI.AdventureMapNode Shown = Overlay.NodeFromPoint(Choice);
                Plugin.Logger?.LogWarning($"{Tag} Clicking node {Choice.x},{Choice.y} ({Shown?.MapNode?.nodeType}) of {Current.outgoing.Count} ways on, map from the host {MapSync.FollowingHost}");
                if (Shown != null)
                    Overlay.OnNodeChosen(Shown);
                yield return new WaitForSecondsRealtime(0.5f);
                Shot($"map-client-{GameManager.CurrentDungeonFloor}-{Time.frameCount}");
            }
        }

        private static IEnumerator PlayAttacks()
        {
            int Next = 0;
            while (true)
            {
                yield return new WaitForSecondsRealtime(3f);
                PlayerFarming Player = PlayerFarming.Instance;
                if (Player?.Spine?.AnimationState == null || MMTransition.IsPlaying || !AnyRemotePlayer())
                    continue;
                var Attacks = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(System.Linq.Enumerable.Where(Player.Spine.Skeleton.Data.Animations.Items,
                    Item => Item != null && Item.Name.IndexOf("attack", StringComparison.OrdinalIgnoreCase) >= 0), Item => Item.Name));
                if (Attacks.Count == 0)
                    continue;
                string Name = Attacks[Next++ % Attacks.Count];
                Player.Spine.AnimationState.SetAnimation(0, Name, false);
                Player.Spine.AnimationState.AddAnimation(0, "idle", true, 0f);
                Plugin.Logger?.LogWarning($"{Tag} Playing {Name}");
                yield return new WaitForSecondsRealtime(0.25f);
                Shot($"anim-{(Plugin.GlobalsInternal.IsServerCreator ? "host" : "client")}-{Next}");
            }
        }

        /// <summary>
        /// A reader in the room the two first share deals two cards, to the host first
        /// </summary>
        private static IEnumerator DrawTarot()
        {
            while (PlayerFarming.Instance == null || !CoopSync.InSession || CoopSync.CurrentRun == 0 || !AnyRemotePlayer() ||
                   MMBiomeGeneration.BiomeGenerator.Instance?.CurrentRoom == null || MMTransition.IsPlaying)
                yield return new WaitForSecondsRealtime(1f);
            yield return new WaitForSecondsRealtime(Plugin.GlobalsInternal.IsServerCreator ? 3f : 8f);

            GameObject Holder = new("COTLMP Test Tarot Reader");
            Holder.SetActive(false);
            Holder.transform.position = new Vector3(1.5f, 2.5f, 0f);
            Interaction_Tarot Reader = Holder.AddComponent<Interaction_Tarot>();
            Reader.playerFarming = PlayerFarming.Instance;
            var GetCard = typeof(Interaction_Tarot).GetMethod("GetCard", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var First = (TarotCards.TarotCard)GetCard.Invoke(Reader, new object[] { true });
            var Second = (TarotCards.TarotCard)GetCard.Invoke(Reader, new object[] { First == null || Array.IndexOf(TarotCards.CorruptedCards, First.CardType) < 0 });
            Plugin.Logger?.LogWarning($"{Tag} Tarot test at {CoopSync.CurrentPlace()}: {First?.CardType} {First?.UpgradeIndex}, {Second?.CardType} {Second?.UpgradeIndex}");
            UnityEngine.Object.Destroy(Holder);
        }

        /// <summary>
        /// Cuts the nearest neutral thing (grass mostly) as a hit would, while the partner is in the room
        /// </summary>
        private static IEnumerator CutThings()
        {
            var Wait = new WaitForSecondsRealtime(2f);
            while (true)
            {
                yield return Wait;
                if (PlayerFarming.Instance == null || MMTransition.IsPlaying || !AnyRemotePlayer() || !CoopSync.InSession)
                    continue;
                Health Nearest = null;
                float Best = float.MaxValue;
                foreach (Health Thing in Health.neutralTeam)
                {
                    if (Thing == null || !Thing.enabled || Thing.HP <= 0f || Thing.isPlayer || !Thing.gameObject.activeInHierarchy)
                        continue;
                    float Distance = Vector2.Distance(Thing.transform.position, PlayerFarming.Instance.transform.position);
                    if (Distance < Best)
                    {
                        Best = Distance;
                        Nearest = Thing;
                    }
                }
                if (Nearest == null)
                    continue;
                Plugin.Logger?.LogWarning($"{Tag} Cutting {Nearest.gameObject.name} at {Format(Nearest.transform.position)}");
                Nearest.DealDamage(Nearest.HP + 1f, PlayerFarming.Instance.gameObject, Nearest.transform.position, false, Health.AttackTypes.Melee, false, (Health.AttackFlags)0);
            }
        }

        private static bool HasNextLayer(MMBiomeGeneration.BiomeRoom Room)
        {
            foreach (var Link in new[] { Room.N_Room, Room.E_Room, Room.S_Room, Room.W_Room })
            {
                if (Link != null && Link.ConnectionType == MMRoomGeneration.GenerateRoom.ConnectionTypes.NextLayer)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// One step towards the floor's exit, or through it: the map comes up, the host picks the
        /// last way on, the joining copy clicks the first (which only waits for the host's pick)
        /// </summary>
        private static IEnumerator GoToNextFloor(MMBiomeGeneration.BiomeRoom Room, bool Host)
        {
            MMBiomeGeneration.BiomeRoom Exit = MMBiomeGeneration.BiomeGenerator.Instance?.Rooms?.Find(HasNextLayer);
            if (Exit == null)
            {
                Plugin.Logger?.LogWarning($"{Tag} No way to the next floor on this one");
                yield return new WaitForSecondsRealtime(5f);
                yield break;
            }
            if (Exit != Room)
            {
                Vector2Int? Step = NextStep(Room, new Vector2Int(Exit.x, Exit.y));
                if (!Step.HasValue)
                    yield break;
                Vector2Int Gap = Step.Value;
                Plugin.Logger?.LogWarning($"{Tag} Heading for the exit at {Exit.x},{Exit.y}: {Gap} from {Room.x},{Room.y}");
                MMTransition.Play(MMTransition.TransitionType.ChangeRoom, MMTransition.Effect.BlackFade, MMTransition.NO_SCENE, 0.5f, "", () => MMBiomeGeneration.BiomeGenerator.ChangeRoom(Gap));
                yield break;
            }

            Door Way = Door.Doors.Find(Item => Item != null && Item.isActiveAndEnabled && Item.ConnectionType == MMRoomGeneration.GenerateRoom.ConnectionTypes.NextLayer);
            if (Way == null || PlayerFarming.Instance == null)
            {
                Plugin.Logger?.LogWarning($"{Tag} No door to the next floor in the exit room");
                yield return new WaitForSecondsRealtime(5f);
                yield break;
            }
            int Floor = GameManager.CurrentDungeonFloor;
            Plugin.Logger?.LogWarning($"{Tag} Through the door to the next floor from floor {Floor}");
            typeof(Door).GetMethod("OnTriggerEnter2D", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                .Invoke(Way, new object[] { PlayerFarming.Instance.circleCollider2D });

            float Waited = 0f;
            while (MapSync.Open == null && Waited < 20f)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                Waited += 0.5f;
            }
            Lamb.UI.UIAdventureMapOverlayController Overlay = MapSync.Open;
            if (Overlay == null)
            {
                Plugin.Logger?.LogWarning($"{Tag} No map came up");
                yield break;
            }

            yield return new WaitForSecondsRealtime(3f);
            global::Map.Node Current = Overlay.Map?.GetCurrentNode();
            if (Host && !NoMapClick && MapSync.Open == Overlay && Current != null && Current.outgoing.Count > 0)
            {
                global::Map.Point Choice = Host ? Current.outgoing[Current.outgoing.Count - 1] : Current.outgoing[0];
                Lamb.UI.AdventureMapNode Shown = Overlay.NodeFromPoint(Choice);
                Plugin.Logger?.LogWarning($"{Tag} Clicking node {Choice.x},{Choice.y} ({Shown?.MapNode?.nodeType}) of {Current.outgoing.Count} ways on, map from the host {MapSync.FollowingHost}");
                if (Shown != null)
                    Overlay.OnNodeChosen(Shown);
            }

            Waited = 0f;
            while (GameManager.CurrentDungeonFloor == Floor && Waited < 60f)
            {
                yield return new WaitForSecondsRealtime(1f);
                Waited += 1f;
            }
            while (MMTransition.IsPlaying && Waited < 90f)
            {
                yield return new WaitForSecondsRealtime(1f);
                Waited += 1f;
            }
            yield return new WaitForSecondsRealtime(3f);
            global::Map.Node Now = global::Map.MapManager.Instance?.CurrentNode;
            Plugin.Logger?.LogWarning($"{Tag} Next floor: floor {GameManager.CurrentDungeonFloor} node {Now?.point.x},{Now?.point.y} {Now?.nodeType} modifier {(Now?.Modifier != null ? Now.Modifier.name : "-")} seed {MMBiomeGeneration.BiomeGenerator.Instance?.Seed} place {CoopSync.CurrentPlace()}");
        }

        /// <summary>
        /// The first door on the shortest way through the floor's rooms to a target room
        /// </summary>
        private static Vector2Int? NextStep(MMBiomeGeneration.BiomeRoom From, Vector2Int To)
        {
            var Seen = new System.Collections.Generic.HashSet<MMBiomeGeneration.BiomeRoom> { From };
            var Queue = new System.Collections.Generic.Queue<(MMBiomeGeneration.BiomeRoom, Vector2Int)>();
            Queue.Enqueue((From, Vector2Int.zero));

            while (Queue.Count > 0)
            {
                var (Room, First) = Queue.Dequeue();
                if (Room.x == To.x && Room.y == To.y)
                    return First;

                foreach (var (Link, Dir) in new[] { (Room.N_Room, new Vector2Int(0, 1)), (Room.E_Room, new Vector2Int(1, 0)), (Room.S_Room, new Vector2Int(0, -1)), (Room.W_Room, new Vector2Int(-1, 0)) })
                {
                    if (Link == null || !Link.Connected || Link.Room == null || !Seen.Add(Link.Room))
                        continue;
                    Queue.Enqueue((Link.Room, Room == From ? Dir : First));
                }
            }
            return null;
        }
    }
}

/* EOF */
