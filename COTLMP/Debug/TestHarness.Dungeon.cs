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
        /// <summary>
        /// The DLC's lands (5 and 6) only for a game that owns the DLC: a test switch opens nothing paid
        /// </summary>
        private static bool OwnsLand(string Switch)
        {
            if (Land < 5 || GameManager.AuthenticateMajorDLC())
                return true;
            Plugin.Logger?.LogError($"{Tag} {Switch}: land {Land} is the DLC's, and this game doesn't own it: left out");
            return false;
        }

        private static IEnumerator EnterDungeon()
        {
            while (!Plugin.GlobalsInternal.InGameSession || PlayerFarming.Instance == null || MMTransition.IsPlaying)
                yield return null;
            yield return new WaitForSecondsRealtime(AutoDungeon);
            if (!OnTestSave("-cotlmp-autodungeon") || !OwnsLand("-cotlmp-autodungeon"))
                yield break;

            /* Already taken into the partner's run on the way (going back to the host): no base door to go through */
            if (SceneManager.GetActiveScene().name == DungeonScene)
                Plugin.Logger?.LogWarning($"{Tag} Already in {DungeonScene} with the partner, no new run");
            else
            {
                Plugin.Logger?.LogWarning($"{Tag} Starting a run into {DungeonScene} like the base door does");
                GameManager.NewRun("", false, DungeonStart);
                MMTransition.Play(MMTransition.TransitionType.ChangeRoomWaitToResume, MMTransition.Effect.BlackFade, DungeonScene, 1f, "", null);
            }

            while (SceneManager.GetActiveScene().name != DungeonScene || PlayerFarming.Instance == null)
                yield return null;
            yield return new WaitForSecondsRealtime(3f);
            MMTransition.CanResume = true;
            MMTransition.ResumePlay();
            Plugin.Logger?.LogWarning($"{Tag} In the dungeon at {COTLMP.Game.CoopSync.CurrentPlace()}");
            /* The floors test is about the maps: nobody dies on the way to the exit */
            if (NextFloor && PlayerFarming.Instance != null)
                PlayerFarming.Instance.health.invincible = true;

            /* Walk the same way through the rooms on both sides: the first unvisited door, N, E, S, W */
            while (SceneManager.GetActiveScene().name == DungeonScene)
            {
                yield return new WaitForSecondsRealtime(Plugin.GlobalsInternal.IsServerCreator ? 5f : 1f);
                MMBiomeGeneration.BiomeRoom Room = MMBiomeGeneration.BiomeGenerator.Instance?.CurrentRoom;
                if (Room == null || MMTransition.IsPlaying || COTLMP.Game.SceneEnds.Waiting)
                    continue;
                /* A fallen lamb goes nowhere: its run is over, the rooms stay as they are */
                if (PlayerFarming.Instance.state != null && (PlayerFarming.Instance.state.CURRENT_STATE == StateMachine.State.GameOver ||
                                                             PlayerFarming.Instance.state.CURRENT_STATE == StateMachine.State.Dead))
                    continue;

                /* The joining copy just follows the host into the next room */
                if (!Plugin.GlobalsInternal.IsServerCreator)
                {
                    string Mine = COTLMP.Game.CoopSync.CurrentPlace();
                    string Partner = COTLMP.Game.RoomSync.PartnerLastPlace;

                    /* In the test apart, linger so the host is rooms ahead */
                    if (Solo && Partner != Mine)
                    {
                        yield return new WaitForSecondsRealtime(5f);
                        /* Left alone for a while: what our HUD shows then */
                        Shot("alone");
                        yield return new WaitForSecondsRealtime(1f);
                    }

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
                    yield return NotInATalk();
                    Plugin.Logger?.LogWarning($"{Tag} Following the host {Gap} from {Room.x},{Room.y}");
                    MMTransition.Play(MMTransition.TransitionType.ChangeRoom, MMTransition.Effect.BlackFade, MMTransition.NO_SCENE, 0.5f, "", () => MMBiomeGeneration.BiomeGenerator.ChangeRoom(Gap));
                    continue;
                }

                /* -cotlmp-floorrush: straight to the first floor's way down (the floors test is about the maps, not the walk) */
                if (FloorRush && GameManager.CurrentDungeonFloor < 2 && AnyRemotePlayer())
                {
                    MMBiomeGeneration.BiomeRoom Exit = MMBiomeGeneration.BiomeGenerator.Instance?.Rooms?.Find(HasNextLayer);
                    if (Exit != null && Exit != Room)
                    {
                        Plugin.Logger?.LogWarning($"{Tag} Floor rush: to the way down at {Exit.x},{Exit.y}");
                        MMBiomeGeneration.BiomeGenerator.ChangeRoom(Exit.x, Exit.y);
                        continue;
                    }
                    yield return new WaitForSecondsRealtime(3f);
                    yield return GoToNextFloor(Room, true);
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
                yield return NotInATalk();

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

                /* Collect what the fight dropped, as walking over it would (not while tracing pickups: there
                   the game's own magnet decides who gets it, the nearest lamb, which may be the partner's) */
                foreach (PickUp Item in PickupTrace ? new System.Collections.Generic.List<PickUp>() : new System.Collections.Generic.List<PickUp>(PickUp.PickUps))
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
                if (!Unlocked && Plugin.GlobalsInternal.IsServerCreator && Plugin.GlobalsInternal.InGameSession && OnTestSave("-cotlmp-drawtest"))
                {
                    Unlocked = true;
                    /* Woolhaven's cards only for a game that owns it */
                    bool Owned = GameManager.AuthenticateMajorDLC();
                    foreach (TarotCards.Card Card in DataManager.AllTrinkets)
                    {
                        if (!DataManager.Instance.PlayerFoundTrinkets.Contains(Card) && (Owned || !TarotCards.MajorDLCCards.Contains(Card)))
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
            if (!OnTestSave("-cotlmp-bosstest"))
                yield break;
            if (!OwnsLand("-cotlmp-bosstest"))
                yield break;
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
            {
                /* As a door does (the puppet goes with the transition, the room's walk is ours alone) */
                MMBiomeGeneration.BiomeRoom To = Lair;
                MMTransition.Play(MMTransition.TransitionType.ChangeRoom, MMTransition.Effect.BlackFade, MMTransition.NO_SCENE, 0.5f, "", () => MMBiomeGeneration.BiomeGenerator.ChangeRoom(To.x, To.y));
            }
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
            /* The room's own walk in first: a walk of ours over it would drop what the room waits for */
            yield return Until(() => !COTLMP.Game.SceneTrace.WalkedAndAwaitedAny(PlayerFarming.Instance), 10f);
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
                               $"leader={Leader?.name}:{Leader?.HP:F0}/{Leader?.totalHP:F0} enemies={Health.team2.Count} boss={BossNow} own={COTLMP.Game.RoomSync.Owning} follow={COTLMP.Game.RoomSync.Following} state={PlayerFarming.Instance?.state?.CURRENT_STATE} " +
                               $"roomactive={GameManager.RoomActive} doors={(RoomLockController.DoorsOpen ? "open" : "shut")} gated={COTLMP.Game.SharedScenes.Gated} pulled={COTLMP.Game.SharedScenes.Pulled}";
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
            if (!OnTestSave("-cotlmp-finaltest"))
                yield break;
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
            Run("FinalChoices", PickChoices());
            Run("FinalFate", FateAfter());
            yield return WalkInAndWatch("Final test");
        }

        /// <summary>
        /// A choice wheel the game puts up later in the fight (the fallen one's fate, after the crown's at the
        /// altar): picked as a player would, its first answer, a few seconds after it shows
        /// </summary>
        private static IEnumerator PickChoices()
        {
            var Click = AccessTools.Method(typeof(ChoiceIndicator), "OnOptionClicked");
            var First = AccessTools.Field(typeof(ChoiceIndicator), "_choice1");
            ChoiceIndicator Done = null;
            float Since = Time.unscaledTime;
            while (Time.unscaledTime - Since < 600f)
            {
                yield return new WaitForSecondsRealtime(1f);
                /* A new thing's card (the fallen one's trophy): closed after a look, as a player does */
                var Card = UnityEngine.Object.FindObjectOfType<Lamb.UI.UINewItemOverlayController>();
                if (Card != null && Card.isActiveAndEnabled)
                {
                    yield return new WaitForSecondsRealtime(2f);
                    Plugin.Logger?.LogWarning($"{Tag} Final test: closing a new thing's card");
                    PressAccept.Now();
                    continue;
                }
                ChoiceIndicator Wheel = UnityEngine.Object.FindObjectOfType<ChoiceIndicator>();
                if (Wheel == null || Wheel == Done || !Wheel.isActiveAndEnabled || UnityEngine.Object.FindObjectOfType<Interaction_FinalBossAltar>() is Interaction_FinalBossAltar Altar &&
                    AccessTools.Field(typeof(Interaction_FinalBossAltar), "c").GetValue(Altar) as ChoiceIndicator == Wheel)
                    continue;
                yield return new WaitForSecondsRealtime(3f);
                if (Wheel == null || !Wheel.isActiveAndEnabled)
                    continue;
                Done = Wheel;
                /* The host points at its answer and takes it; the friend only points at the other (a vote) */
                bool Host = Plugin.GlobalsInternal.IsServerCreator;
                int Ours = Host ? FatePick : 1 - FatePick;
                var Option = AccessTools.Field(typeof(ChoiceIndicator), Ours == 0 ? "_choice1" : "_choice2").GetValue(Wheel);
                MMButton Button = (MMButton)AccessTools.Field(Option.GetType(), "Button").GetValue(Option);
                AccessTools.Method(typeof(ChoiceIndicator), "OnOptionSelected").Invoke(Wheel, new object[] { Button });
                Plugin.Logger?.LogWarning($"{Tag} Final test: a choice is up, we point at answer {Ours}");
                yield return new WaitForSecondsRealtime(2f);
                Shot("fate-wheel");
                if (!Host)
                    continue;
                yield return new WaitForSecondsRealtime(1f);
                Plugin.Logger?.LogWarning($"{Tag} Final test: we take answer {Ours}");
                Click.Invoke(Wheel, new object[] { Option });
            }
        }

        /// <summary>
        /// A player in a conversation or before a menu of the game's (a new thing's card) doesn't walk out of the room: the
        /// walker waits for its end, 60 s at most
        /// </summary>
        private static IEnumerator NotInATalk()
        {
            float Since = Time.unscaledTime;
            while ((MMTools.MMConversation.isPlaying || LetterBox.IsPlaying || Lamb.UI.UIMenuBase.ActiveMenus.Exists(Menu => Menu != null && Menu.gameObject.activeInHierarchy)) &&
                   Time.unscaledTime - Since < 60f)
                yield return null;
            if (Time.unscaledTime - Since > 0.5f)
                Plugin.Logger?.LogWarning($"{Tag} The walker waited {Time.unscaledTime - Since:F1} s for a conversation or a menu to end");
        }

        /// <summary>
        /// A player closes a new thing's card after a look: one left open stands the run still (the walker presses accept on
        /// such cards after 3 s, the menu test on its own after its 5 s, as a player does)
        /// </summary>
        private static Lamb.UI.UINewItemOverlayController OwnCard;

        /// <summary>
        /// -cotlmp-kbtest: with the partner's puppet in our room, the knucklebones tables (Ratau's kind and the cult's)
        /// are asked what they offer: a match for two would seat the puppet
        /// </summary>
        private static IEnumerator KnucklebonesTest()
        {
            yield return Until(() => SceneManager.GetActiveScene().name == DungeonScene && PlayerFarming.Instance != null && COTLMP.Game.GhostLoot.PartnerHere() != null && !MMTransition.IsPlaying, 300f);
            yield return new WaitForSecondsRealtime(3f);
            foreach (Type Kind in new[] { typeof(Interaction_Knucklebones), typeof(Interaction_KnucklebonesBuilding) })
            {
                GameObject Table = new($"COTLMP test {Kind.Name}");
                Table.transform.position = PlayerFarming.Instance.transform.position + new Vector3(1f, 0f, 0f);
                Interaction Seat = null;
                try
                {
                    Seat = (Interaction)Table.AddComponent(Kind);
                    Seat.GetLabel();
                    Seat.GetSecondaryLabel();
                    Plugin.Logger?.LogWarning($"{Tag} Knucklebones test: {Kind.Name} with {PlayerFarming.playersCount} players offers a match for two: {Seat.HasSecondaryInteraction}, label '{Seat.SecondaryLabel}'");
                    if (!Seat.HasSecondaryInteraction)
                    {
                        int Before = COTLMP.Game.LocalMiniGames.MatchesForTwoRefused;
                        Seat.OnSecondaryInteract(PlayerFarming.Instance.state);
                        Plugin.Logger?.LogWarning($"{Tag} Knucklebones test: {Kind.Name} pressed for two anyway: refused {COTLMP.Game.LocalMiniGames.MatchesForTwoRefused - Before}");
                    }
                    else
                    {
                        ++Failed;
                        Plugin.Logger?.LogError($"[QA] Knucklebones test: {Kind.Name} offers a match for two with the partner's puppet in the second seat");
                    }
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogWarning($"{Tag} Knucklebones test: {Kind.Name} stumbled: {e.GetType().Name}: {e.Message}");
                }
                UnityEngine.Object.Destroy(Table);
                yield return new WaitForSecondsRealtime(1f);
            }
        }

        /* A frame of every INV-HUD as it is told: what the player had on screen then */
        private static IEnumerator ShootLostHud()
        {
            int Seen = 0;
            while (true)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                if (COTLMP.Game.SceneTrace.HudsOff == Seen)
                    continue;
                Seen = COTLMP.Game.SceneTrace.HudsOff;
                Shot($"hud-lost-{Seen}");
            }
        }

        private static IEnumerator CloseStrayCards()
        {
            var Seen = new System.Collections.Generic.Dictionary<Lamb.UI.UINewItemOverlayController, float>();
            while (true)
            {
                yield return new WaitForSecondsRealtime(1f);
                foreach (var Card in UnityEngine.Object.FindObjectsOfType<Lamb.UI.UINewItemOverlayController>())
                {
                    if (Card == null || !Card.isActiveAndEnabled || Card == OwnCard)
                        continue;
                    if (!Seen.TryGetValue(Card, out float Since))
                        Seen[Card] = Since = Time.unscaledTime;
                    if (Time.unscaledTime - Since < 3f)
                        continue;
                    Plugin.Logger?.LogWarning($"{Tag} Closing a new thing's card after a look ({Card.MyTypeOfCard})");
                    Seen.Remove(Card);
                    PressAccept.Now();
                }
            }
        }

        /// <summary>
        /// -cotlmp-menutest: in the dungeon a new thing's card (a gift, as from a chest) stands 5 s and is closed:
        /// the game's own hide under it is no lost HUD
        /// </summary>
        private static IEnumerator MenuTest()
        {
            yield return Until(() => SceneManager.GetActiveScene().name == DungeonScene && PlayerFarming.Instance != null && !MMTransition.IsPlaying, 300f);
            yield return new WaitForSecondsRealtime(25f);
            yield return NotInATalk();
            OwnCard = MonoSingleton<Lamb.UI.UIManager>.Instance.ShowNewItemOverlay();
            OwnCard.Show(Lamb.UI.UINewItemOverlayController.TypeOfCard.Gift, PlayerFarming.Instance.transform.position, InventoryItem.ITEM_TYPE.GIFT_MEDIUM);
            Plugin.Logger?.LogWarning($"{Tag} Menu test: a gift's card is up");
            yield return new WaitForSecondsRealtime(2.5f);
            Plugin.Logger?.LogWarning($"{Tag} Menu test: under the card our lamb is {PlayerFarming.Instance?.state?.CURRENT_STATE}, HUD {COTLMP.Game.SceneTrace.HudNow()}");
            Shot("menu-card");
            yield return new WaitForSecondsRealtime(2.5f);
            PressAccept.Now();
            yield return new WaitForSecondsRealtime(6f);
            Plugin.Logger?.LogWarning($"{Tag} Menu test: the card closed, our lamb {PlayerFarming.Instance?.state?.CURRENT_STATE}, watchdog heals {COTLMP.Game.CoopHud.HudHeals}, HUD {COTLMP.Game.SceneTrace.HudNow()}");
            Shot("menu-after");
            OwnCard = null;
        }

        /// <summary>
        /// -cotlmp-fleece=N: the run starts with fleece N (4 and 6 deal four tarot cards in its first room). In the
        /// dungeon every tarot overlay is counted with who holds its input, and closed after a look as a player does
        /// </summary>
        private static IEnumerator FleeceTest()
        {
            yield return Until(() => SceneManager.GetActiveScene().name == "Base Biome 1" && DataManager.Instance != null && PlayerFarming.Instance != null, 300f);
            if (SceneManager.GetActiveScene().name != "Base Biome 1" || DataManager.Instance == null)
            {
                Plugin.Logger?.LogError($"[QA] Fleece test: never in the cult, the fleece is left as it was");
                ++Failed;
                yield break;
            }
            if (!OnTestSave("-cotlmp-fleece"))
                yield break;
            /* Only the game's own fleeces: none of a DLC's, none of the outfits past them */
            if (Fleece > (int)PlayerFleeceManager.FleeceType.BlindFaith || PlayerFleeceManager.IS_DLC.Contains((PlayerFleeceManager.FleeceType)Fleece))
            {
                Plugin.Logger?.LogError($"{Tag} -cotlmp-fleece={Fleece} is a DLC's or an outfit, not the game's own: left out");
                yield break;
            }
            DataManager.Instance.PlayerFleece = Fleece;
            Plugin.Logger?.LogWarning($"{Tag} Fleece test: fleece {Fleece}, free tarot cards {PlayerFleeceManager.GetFreeTarotCards()}");
            yield return Until(() => SceneManager.GetActiveScene().name == DungeonScene && PlayerFarming.Instance != null, 300f);
            float Entered = Time.unscaledTime;
            int Overlays = 0;
            bool ActiveTold = false;
            Lamb.UI.UIFleeceTarotRewardOverlayController Seen = null;
            while (Time.unscaledTime - Entered < 90f)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                var Room = MMBiomeGeneration.BiomeGenerator.Instance?.CurrentRoom;
                if (!ActiveTold && Room != null && Room.Active)
                {
                    ActiveTold = true;
                    Plugin.Logger?.LogWarning($"{Tag} Fleece test: the first room is on {Time.unscaledTime - Entered:F1} s in, {Overlays} tarot overlays so far");
                }
                var Overlay = UnityEngine.Object.FindObjectOfType<Lamb.UI.UIFleeceTarotRewardOverlayController>();
                if (Overlay == null || !Overlay.isActiveAndEnabled || Overlay == Seen)
                    continue;
                Seen = Overlay;
                ++Overlays;
                var Owner = MonoSingleton<src.UINavigator.UINavigatorNew>.Instance.AllowInputOnlyFromPlayer;
                Plugin.Logger?.LogWarning($"{Tag} Fleece test: tarot overlay {Overlays} up, its input {(Owner == null ? "nobody's" : Owner == PlayerFarming.Instance ? "our lamb's" : COTLMP.Game.PlayerManager.IsPuppet(Owner.health) ? "the PUPPET's" : Owner.name)}, letterbox {LetterBox.IsPlaying}");
                yield return new WaitForSecondsRealtime(2f);
                Shot($"fleece-tarot-{Overlays}");
                yield return new WaitForSecondsRealtime(1f);
                Overlay.Hide();
            }
            Plugin.Logger?.LogWarning($"{Tag} Fleece test: {Overlays} tarot overlays in the first 90 s, room on {ActiveTold}, letterbox {LetterBox.IsPlaying}, HUD {COTLMP.Game.SceneTrace.HudNow()}");
            Shot("fleece-after");
        }

        /// <summary>
        /// After the ending, back in the cult: who the fallen one is now, on this side (the host's save, the friend's copy of it)
        /// </summary>
        private static IEnumerator FateAfter()
        {
            yield return Until(() => SceneManager.GetActiveScene().name == "Base Biome 1" && PlayerFarming.Instance != null && !MMTransition.IsPlaying, 600f);
            yield return new WaitForSecondsRealtime(15f);
            string Names(System.Collections.Generic.IEnumerable<FollowerInfo> List) => string.Join(",", List.Where(Info => Info != null).Select(Info => $"{Info.Name}#{Info.ID}"));
            Plugin.Logger?.LogWarning($"{Tag} Final test fate: recruits [{Names(DataManager.Instance.Followers_Recruit)}] followers {DataManager.Instance.Followers.Count} " +
                                      $"[{Names(DataManager.Instance.Followers.Where(Info => Info != null && Info.ID >= 0).OrderByDescending(Info => Info.ID).Take(4))}] " +
                                      $"deathcat beaten {DataManager.Instance.DeathCatBeaten}");
            Shot("fate-cult");
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
                /* The fight's scene is gone (its ending, the quote screen): nothing more to watch */
                if (HUD_Manager.Instance == null || Fight == null)
                    yield break;
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

        /// <summary>
        /// Each death screen, once its rooms are all out: a frame and how many rooms and what time it shows
        /// </summary>
        private static IEnumerator DeathScreenShots()
        {
            Lamb.UI.DeathScreen.UIDeathScreenOverlayController Seen = null;
            while (true)
            {
                yield return new WaitForSecondsRealtime(1f);
                var Screen = UnityEngine.Object.FindObjectOfType<Lamb.UI.DeathScreen.UIDeathScreenOverlayController>();
                if (Screen == null || Screen == Seen || !Screen.isActiveAndEnabled)
                    continue;
                Seen = Screen;
                yield return new WaitForSecondsRealtime(8f);
                int Rooms = Screen.GetComponentsInChildren<UIDeathScreenLevelNode>(false).Length;
                string Time = string.Join(" | ", Screen.GetComponentsInChildren<TMPro.TMP_Text>(false).Select(Text => Text.text).Where(Text => Text != null && Text.Contains(":")).Take(3));
                Plugin.Logger?.LogWarning($"{Tag} Death screen: {Rooms} rooms shown, texts with a time: {Time}");
                Shot("death-screen");
            }
        }

        /// <summary>
        /// -cotlmp-deathruns=K: K runs in a row from the base, each ended by a fall for good (in a session the friend
        /// first, the host at their side; alone just us) after a while on the first floor; each death screen
        /// is framed and its time put next to the run's time as measured here; then on at once as a player
        /// would (continue, and through the base door again)
        /// </summary>
        private static bool Guarded;

        /// <summary>
        /// -cotlmp-forcepull: in a leader's room, while the room's own walk brings our lamb in, the pull to a
        /// scene's start comes (as the partner's scene would bring it): the pull must wait for the walk
        /// </summary>
        private static IEnumerator PullDuringWalk()
        {
            var At = AccessTools.Field(typeof(COTLMP.Game.SharedScenes), "PullAt");
            var To = AccessTools.Field(typeof(COTLMP.Game.SharedScenes), "PullTo");
            while (true)
            {
                yield return null;
                PlayerFarming Player = PlayerFarming.Instance;
                if (Player == null || MMBiomeGeneration.BiomeGenerator.Instance?.CurrentRoom?.IsBoss != true || !COTLMP.Game.SceneTrace.WalkedAndAwaitedAny(Player))
                    continue;
                Plugin.Logger?.LogWarning($"{Tag} Pull test: a pull comes during the room's walk, at {Format(Player.transform.position)}");
                To.SetValue(null, Player.transform.position + new Vector3(3f, 0f, 0f));
                At.SetValue(null, Time.unscaledTime);
                yield break;
            }
        }

        /* When the room we stand in came alive: the game gets the fallen up 1 s after that, and again 3 s later */
        private static float RoomActiveAt;

        private static IEnumerator GuardLamb()
        {
            MMBiomeGeneration.BiomeGenerator.OnRoomActive += () => RoomActiveAt = Time.unscaledTime;
            while (true)
            {
                yield return null;
                if (Guarded && PlayerFarming.Instance != null && PlayerFarming.Instance.health != null)
                {
                    PlayerFarming.Instance.health.invincible = true;
                    PlayerFarming.Instance.health.untouchable = true;
                }
            }
        }

        private static IEnumerator RunsInARow()
        {
            Run("GuardLamb", GuardLamb());
            var ContinueButton = AccessTools.Field(typeof(Lamb.UI.DeathScreen.UIDeathScreenOverlayController), "_continueButton");
            for (int Run = 1; Run <= DeathRuns; Run++)
            {
                /* A pair (the session up and the partner in) unless -cotlmp-alone */
                bool Session = !Alone;
                if (Session)
                    yield return Until(() => Plugin.GlobalsInternal.InGameSession && AnyRemotePlayer(), 240f);
                bool Host = Plugin.GlobalsInternal.IsServerCreator;
                /* Through the base door as players go: the friend standing in the cult, then after the host */
                yield return Until(() => SceneManager.GetActiveScene().name == "Base Biome 1" && PlayerFarming.Instance != null && !MMTransition.IsPlaying &&
                                         (!Session || AnyRemotePlayer()), 240f);
                yield return new WaitForSecondsRealtime(!Session || Host ? 3f : 8f);
                if (!OnTestSave("-cotlmp-deathruns") || !OwnsLand("-cotlmp-deathruns"))
                    yield break;
                Plugin.Logger?.LogWarning($"{Tag} Runs in a row: run {Run} of {DeathRuns} into {DungeonScene}{(Session ? "" : ", alone")}");
                GameManager.NewRun("", false, DungeonStart);
                float Started = Time.time;
                MMTransition.Play(MMTransition.TransitionType.ChangeRoomWaitToResume, MMTransition.Effect.BlackFade, DungeonScene, 1f, "", null);
                yield return Until(() => SceneManager.GetActiveScene().name == DungeonScene && PlayerFarming.Instance != null, 90f);
                yield return new WaitForSecondsRealtime(3f);
                MMTransition.CanResume = true;
                MMTransition.ResumePlay();

                /* To the floor of the fall (the host straight to the way down, the friend after it), then into a fight;
                   nothing fells us on the way (each frame: a new floor's arrival and its first hits come fast) */
                Guarded = true;
                float Since = Time.unscaledTime;
                while (Time.unscaledTime - Since < 240f)
                {
                    yield return new WaitForSecondsRealtime(1f);
                    PlayerFarming.Instance.health.invincible = true;
                    var Generator = MMBiomeGeneration.BiomeGenerator.Instance;
                    MMBiomeGeneration.BiomeRoom Room = Generator?.CurrentRoom;
                    if (Room == null || MMTransition.IsPlaying || LetterBox.IsPlaying || COTLMP.Game.SceneEnds.Waiting)
                        continue;
                    bool InFight = !RoomLockController.DoorsOpen && CountVisibleEnemies() > 0;
                    if (GameManager.CurrentDungeonFloor >= Math.Max(1, DeathAt) && (!Session || InFight))
                        break;
                    string Mine = COTLMP.Game.CoopSync.CurrentPlace(), Theirs = COTLMP.Game.RoomSync.PartnerLastPlace;
                    if (!Host && Session)
                    {
                        if (Theirs == null || Mine == null || Theirs == Mine || !Theirs.Contains("@") || !Mine.Contains("@"))
                            continue;
                        if (Theirs.Substring(0, Theirs.IndexOf('@')) != Mine.Substring(0, Mine.IndexOf('@')))
                            yield return GoToNextFloor(Room, false);
                        else
                        {
                            string[] At = Theirs.Substring(Theirs.IndexOf('@') + 1).Split(',');
                            MMBiomeGeneration.BiomeGenerator.ChangeRoom(int.Parse(At[0]), int.Parse(At[1]));
                        }
                        continue;
                    }
                    if (Session && !AnyRemotePlayer())
                        continue;
                    if (GameManager.CurrentDungeonFloor < DeathAt)
                    {
                        MMBiomeGeneration.BiomeRoom Exit = Generator.Rooms.Find(HasNextLayer);
                        if (Exit != null && Exit != Room)
                            MMBiomeGeneration.BiomeGenerator.ChangeRoom(Exit.x, Exit.y);
                        else
                            yield return GoToNextFloor(Room, true);
                        continue;
                    }
                    /* The partner in with us first, then on to a room not seen yet: rooms of a floor are fights mostly */
                    if (Session && Theirs != Mine)
                        continue;
                    MMBiomeGeneration.BiomeRoom Next = Generator.Rooms.Find(Each => Each != null && !Each.Visited && !Each.IsBoss && !HasNextLayer(Each));
                    if (Next != null)
                        MMBiomeGeneration.BiomeGenerator.ChangeRoom(Next.x, Next.y);
                    yield return new WaitForSecondsRealtime(4f);
                }

                /*
                 * The end of the run. Alone: a fall after a while. Together: the first falls in the fight, the second
                 * once the first lies there; or, by switch, instead of falling: -cotlmp-escape goes home (with
                 * -cotlmp-afterdown once the partner lies there), -cotlmp-walkout goes back to the cult and
                 * -cotlmp-nextfloordown down a floor once the partner lies there, -cotlmp-waitend only waits for the
                 * partner's end to come here
                 */
                bool First = !Session || (HostFirst ? Host : !Host);
                bool Leaves = WalkOut || NextFloorDown || (Escape && AfterDown);
                float Stay = 12f + Run * 8f;
                while (Time.unscaledTime - Since < 400f && !WaitEnd)
                {
                    yield return new WaitForSecondsRealtime(0.5f);
                    if (MMTransition.IsPlaying || LetterBox.IsPlaying)
                        continue;
                    if (!Session)
                    {
                        if (Time.time - Started > Stay)
                            break;
                        continue;
                    }
                    if (Escape && !AfterDown && Time.time - Started > Stay)
                        break;
                    bool Together = COTLMP.Game.RoomSync.PartnerLastPlace == COTLMP.Game.CoopSync.CurrentPlace();
                    /* Past the game's own wake for a room come alive (1 s, and 3 s more), so the fall stands */
                    if (First && !Leaves && !Escape && Together && !RoomLockController.DoorsOpen && CountVisibleEnemies() > 0 && Time.unscaledTime - RoomActiveAt > 6f)
                        break;
                    if ((!First || Leaves) && Together && COTLMP.Game.CoopLife.PartnerDown)
                    {
                        yield return new WaitForSecondsRealtime(0.5f);
                        if (COTLMP.Game.CoopLife.PartnerDown)
                            break;
                    }
                }
                Guarded = false;
                PlayerFarming Player = PlayerFarming.Instance;
                if (WaitEnd)
                    Plugin.Logger?.LogWarning($"{Tag} Runs in a row: run {Run} waits for the partner's end of it");
                else if (Escape)
                {
                    Plugin.Logger?.LogWarning($"{Tag} Runs in a row: run {Run} escapes after {Time.time - Started:F1} s of our own run time");
                    MonoSingleton<Lamb.UI.UIManager>.Instance.ShowDeathScreenOverlay(Lamb.UI.DeathScreen.UIDeathScreenOverlayController.Results.Escaped);
                }
                else if (WalkOut)
                {
                    Plugin.Logger?.LogWarning($"{Tag} Runs in a row: run {Run}: the partner lies there, we go back to the cult (no going home)");
                    MMTransition.Play(MMTransition.TransitionType.ChangeSceneAutoResume, MMTransition.Effect.BlackFade, "Base Biome 1", 1f, "", null);
                    yield break;
                }
                else if (NextFloorDown)
                {
                    Plugin.Logger?.LogWarning($"{Tag} Runs in a row: run {Run}: the partner lies there, we go down a floor");
                    MMBiomeGeneration.BiomeRoom Here = MMBiomeGeneration.BiomeGenerator.Instance?.CurrentRoom;
                    MMBiomeGeneration.BiomeRoom Exit = MMBiomeGeneration.BiomeGenerator.Instance?.Rooms?.Find(HasNextLayer);
                    if (Exit != null && Exit != Here)
                    {
                        MMTransition.Play(MMTransition.TransitionType.ChangeRoom, MMTransition.Effect.BlackFade, MMTransition.NO_SCENE, 0.5f, "", () => MMBiomeGeneration.BiomeGenerator.ChangeRoom(Exit.x, Exit.y));
                        yield return new WaitForSecondsRealtime(3f);
                        while (MMTransition.IsPlaying)
                            yield return null;
                    }
                    yield return GoToNextFloor(MMBiomeGeneration.BiomeGenerator.Instance?.CurrentRoom, true);
                    yield break;
                }
                else
                {
                    Plugin.Logger?.LogWarning($"{Tag} Runs in a row: run {Run} falls {(First ? "first" : "second")} on floor {GameManager.CurrentDungeonFloor} at {COTLMP.Game.CoopSync.CurrentPlace()} after {Time.time - Started:F1} s of our own run time");
                    Player.health.invincible = false;
                    Player.health.untouchable = false;
                    Player.health.HP = 0.5f;
                    Player.health.DealDamage(5f, Player.gameObject, Player.transform.position, false, Health.AttackTypes.Melee, true, Health.AttackFlags.ForceKill);
                }

                /* -cotlmp-nodeathfor=N: no end may come here for N s (the partner's long way down must not end ours) */
                if (NoDeathFor > 0f)
                {
                    float Watched = Time.unscaledTime;
                    yield return Until(() => Lamb.UI.DeathScreen.UIDeathScreenOverlayController.Instance != null, NoDeathFor);
                    bool Came = Lamb.UI.DeathScreen.UIDeathScreenOverlayController.Instance != null;
                    Plugin.Logger?.LogWarning($"{Tag} Runs in a row: run {Run} {(Came ? "ENDED" : "still on")} {Time.unscaledTime - Watched:F0} s after, down {COTLMP.Game.CoopLife.WeAreDown}, partner at {COTLMP.Game.RoomSync.PartnerPlace ?? "a load"}");
                    if (Came)
                    {
                        ++Failed;
                        Plugin.Logger?.LogError($"[QA] Runs in a row: run {Run} ended while the partner was on their way");
                    }
                    yield break;
                }

                /* What the game does once we fall: a way back (the save's resurrection) or the death screen, every 15 s */
                Plugin.Logger?.LogWarning($"{Tag} Runs in a row: after the fall, resurrection {ResurrectOnHud.ResurrectionType}, the deal {TrinketManager.HasTrinket(TarotCards.Card.TheDeal, PlayerFarming.Instance)}, fleece {DataManager.Instance.PlayerFleece}, followers {DataManager.Instance.Followers.Count}");
                for (int Look = 0; Look < 8 && Lamb.UI.DeathScreen.UIDeathScreenOverlayController.Instance == null; Look++)
                {
                    yield return new WaitForSecondsRealtime(15f);
                    PlayerFarming Now = PlayerFarming.Instance;
                    Plugin.Logger?.LogWarning($"{Tag} Runs in a row: {15 * (Look + 1)} s after the fall in {SceneManager.GetActiveScene().name} at {COTLMP.Game.CoopSync.CurrentPlace()}: " +
                                              $"lamb {(Now == null ? "none" : $"{Now.name}#{Now.GetInstanceID()} state {Now.state?.CURRENT_STATE} hp {Now.health?.HP}/{Now.health?.totalHP} anim {Now.Spine?.AnimationState?.GetCurrent(0)?.Animation?.Name} active {Now.gameObject.activeInHierarchy}")}, " +
                                              $"players {PlayerFarming.players.Count}, respawning {RespawnRoomManager.RespawnPlayInProgress}, respawn room {(RespawnRoomManager.Instance != null && RespawnRoomManager.Instance.gameObject.activeInHierarchy)}, transition {MMTransition.IsPlaying}, ts {Time.timeScale:0.00}");
                    Shot($"after-fall-{15 * (Look + 1)}");
                    /* The corrupted monolith's room (the save's way back): a player goes to the monolith and calls on it until it gives */
                    Interaction_CorruptedMonolith Monolith = RespawnRoomManager.Instance != null && RespawnRoomManager.Instance.gameObject.activeInHierarchy ? UnityEngine.Object.FindObjectOfType<Interaction_CorruptedMonolith>() : null;
                    GameObject Temp = RespawnRoomManager.Instance != null ? AccessTools.Field(typeof(RespawnRoomManager), "Player")?.GetValue(RespawnRoomManager.Instance) as GameObject : null;
                    if (Look == 0 && Monolith != null && Temp != null)
                    {
                        Plugin.Logger?.LogWarning($"{Tag} Runs in a row: the monolith's room, its player {Temp.name} active {Temp.activeInHierarchy} state {Temp.GetComponent<StateMachine>()?.CURRENT_STATE}, the game's lamb is it: {PlayerFarming.Instance != null && PlayerFarming.Instance.gameObject == Temp}");
                        Temp.transform.position = Monolith.transform.position + new Vector3(0f, -1f, 0f);
                        for (int Call = 0; Call < 9; Call++)
                        {
                            try
                            {
                                Monolith.OnInteract(Temp.GetComponent<StateMachine>());
                            }
                            catch (Exception e)
                            {
                                Plugin.Logger?.LogWarning($"{Tag} Runs in a row: calling on the monolith stumbled: {e.GetType().Name}: {e.Message}");
                            }
                            yield return new WaitForSecondsRealtime(1.5f);
                        }
                        Plugin.Logger?.LogWarning($"{Tag} Runs in a row: called on the monolith 9 times");
                        /* Its gift: the corrupted cards and relics, each a menu the player closes after a look */
                        float Gifts = Time.unscaledTime;
                        int Closed = 0;
                        while (Closed < 2 && Time.unscaledTime - Gifts < 60f)
                        {
                            yield return new WaitForSecondsRealtime(1f);
                            Lamb.UI.UIMenuBase Gift = (Lamb.UI.UIMenuBase)UnityEngine.Object.FindObjectOfType<Lamb.UI.UITarotCardsMenuController>() ?? UnityEngine.Object.FindObjectOfType<Lamb.UI.UIRelicMenuController>();
                            if (Gift == null || !Gift.isActiveAndEnabled || Gift.IsHiding)
                                continue;
                            yield return new WaitForSecondsRealtime(2f);
                            Shot($"monolith-gift-{Closed + 1}");
                            Plugin.Logger?.LogWarning($"{Tag} Runs in a row: the monolith's gift {Gift.GetType().Name} up, closed after a look");
                            /* As the player's cancel does: the menu closes itself once it is up (interactable) */
                            Gift.OnCancelButtonInput();
                            ++Closed;
                            yield return new WaitForSecondsRealtime(1.5f);
                        }
                        Plugin.Logger?.LogWarning($"{Tag} Runs in a row: {Closed} gift menus closed, corrupted cards and relics unlocked {DataManager.Instance.UnlockedCorruptedRelicsAndTarots}");
                    }
                }
                yield return Until(() => Lamb.UI.DeathScreen.UIDeathScreenOverlayController.Instance != null, 10f);
                var Screen = Lamb.UI.DeathScreen.UIDeathScreenOverlayController.Instance;
                Plugin.Logger?.LogWarning($"{Tag} Runs in a row: run {Run} death screen {(Screen == null ? "never came" : "up")}, our own run time {Time.time - Started:F1} s");
                if (Screen == null)
                {
                    ++Failed;
                    Plugin.Logger?.LogError($"[QA] Runs in a row: no death screen in run {Run}");
                    yield break;
                }
                yield return new WaitForSecondsRealtime(10f);
                /* Behind its death screen the lamb stays dead: up again is a run that never ended */
                /* (gone home is the run's end too, not a death: the lamb stands or lies as it was) */
                if (Session && Lamb.UI.DeathScreen.UIDeathScreenOverlayController.Result != Lamb.UI.DeathScreen.UIDeathScreenOverlayController.Results.Escaped && !COTLMP.Game.CoopLife.WeAreDead)
                {
                    ++Failed;
                    Plugin.Logger?.LogError($"[QA] Runs in a row: run {Run}'s lamb is not dead behind its death screen (down {COTLMP.Game.CoopLife.WeAreDown}, state {Player?.state?.CURRENT_STATE})");
                }
                string Shown = string.Join(" | ", Screen.GetComponentsInChildren<TMPro.TMP_Text>(false).Select(Text => Text.text).Where(Text => Text != null && Text.Contains(":")).Take(2));
                int Rooms = Screen.GetComponentsInChildren<UIDeathScreenLevelNode>(false).Length;
                Plugin.Logger?.LogWarning($"{Tag} Runs in a row: run {Run} shows {Shown}, {Rooms} rooms (our own run time {Time.time - Started:F1} s, clock sent/applied {COTLMP.Game.RunClock.Sent}/{COTLMP.Game.RunClock.Applied})");
                Shot($"run{Run}-death-screen");
                (ContinueButton.GetValue(Screen) as UnityEngine.UI.Button)?.onClick.Invoke();
            }
        }

        /// <summary>
        /// -cotlmp-deathat=N: nobody falls on the way; on the N-th floor, once both stand in one room, the friend
        /// takes a killing hit (the game's own damage path), and the host a second after it sees them down,
        /// sooner than the game would get them up by itself: the run ends for both, a real death screen each
        /// </summary>
        private static IEnumerator DieTogether()
        {
            bool Host = Plugin.GlobalsInternal.IsServerCreator;
            while (true)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                PlayerFarming Player = PlayerFarming.Instance;
                if (Player == null || SceneManager.GetActiveScene().name != DungeonScene)
                    continue;
                if (GameManager.CurrentDungeonFloor < DeathAt)
                {
                    Player.health.invincible = true;
                    continue;
                }
                bool Together = !MMTransition.IsPlaying && !LetterBox.IsPlaying && COTLMP.Game.RoomSync.PartnerLastPlace == COTLMP.Game.CoopSync.CurrentPlace();
                if (!Together || (Host && !COTLMP.Game.CoopLife.PartnerDown))
                    continue;
                yield return new WaitForSecondsRealtime(1f);
                Plugin.Logger?.LogWarning($"{Tag} Death test: falling on floor {GameManager.CurrentDungeonFloor} at {COTLMP.Game.CoopSync.CurrentPlace()}, hp {Player.health.HP}");
                Player.health.invincible = false;
                Player.health.untouchable = false;
                Player.health.HP = 0.5f;
                Player.health.DealDamage(5f, Player.gameObject, Player.transform.position, false, Health.AttackTypes.Melee, true, Health.AttackFlags.ForceKill);
                yield break;
            }
        }

        private static IEnumerator DieOnce()
        {
            /* Counted from the first moment both stand in one dungeon room */
            while (SceneManager.GetActiveScene().name != DungeonScene || PlayerFarming.Instance == null || !AnyRemotePlayer() ||
                   GameManager.CurrentDungeonFloor < DieFloor)
                yield return null;
            yield return new WaitForSecondsRealtime(AutoDie);
            /* Fall standing in one room with the partner (their side has our body to kneel at), not mid-transition,
               and in a fight as players fall: in a cleared room the game wakes the fallen by itself */
            yield return Until(() => PlayerFarming.Instance != null && !MMTransition.IsPlaying && !COTLMP.Game.SceneEnds.Waiting &&
                                     COTLMP.Game.RoomSync.PartnerLastPlace == COTLMP.Game.CoopSync.CurrentPlace() &&
                                     !RoomLockController.DoorsOpen && CountVisibleEnemies() > 0, 150f);
            yield return new WaitForSecondsRealtime(1f);

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
        /// Walks to the downed partner and pays the game's own price (two hearts at their puppet's
        /// revive interaction) to get them up, as a player would
        /// </summary>
        private static IEnumerator Rescue()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                if (!COTLMP.Game.CoopLife.PartnerDown || PlayerFarming.Instance == null || MMTransition.IsPlaying)
                    continue;
                PlayerFarming Downed = COTLMP.Game.PlayerManager.GetAnyPuppet();
                if (Downed == null)
                    continue;
                PlayerFarming.Instance.transform.position = Downed.transform.position + new Vector3(0.6f, 0f, 0f);
                yield return new WaitForSecondsRealtime(1f);
                /* -cotlmp-dieonrevive: we fall at their side instead of helping (both down test) */
                if (DieOnRevive)
                {
                    DieOnRevive = false;
                    PlayerFarming Player = PlayerFarming.Instance;
                    Plugin.Logger?.LogWarning($"{Tag} Falling while getting the partner up");
                    Player.health.invincible = false;
                    Player.health.untouchable = false;
                    Player.health.HP = 0.5f;
                    Player.health.DealDamage(5f, Player.gameObject, Player.transform.position, false, Health.AttackTypes.Melee, true, Health.AttackFlags.ForceKill);
                    continue;
                }
                var Revive = Downed.GetComponent<Interaction_CoopRevive>();
                if (Revive == null || !Revive.enabled || !COTLMP.Game.CoopLife.PartnerDown)
                    continue;
                float Hearts = PlayerFarming.Instance.health.CurrentHP;
                Revive.playerFarming = PlayerFarming.Instance;
                Plugin.Logger?.LogWarning($"{Tag} Reviving the partner for the game's price, our hearts {Hearts}");
                Revive.OnInteract(PlayerFarming.Instance.state);
                yield return new WaitForSecondsRealtime(2f);
                Plugin.Logger?.LogWarning($"{Tag} After the revive: our hearts {PlayerFarming.Instance.health.CurrentHP} (were {Hearts}), partner down {COTLMP.Game.CoopLife.PartnerDown}");
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
