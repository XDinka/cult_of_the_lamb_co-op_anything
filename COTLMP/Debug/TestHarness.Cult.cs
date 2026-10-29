/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     The test harness: the cult, the temple, the fox and the recruits
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
        /* The cult's HUD as each side has it: the faith circle and the hunger bar (shown when their container is up and their
           canvas not faded: the game fades the circle out while there are no followers), the flags and the flock; a frame each time */
        private static IEnumerator WatchCamp()
        {
            /* With runs in a row, the cult is looked at once back from a run */
            bool Back = DeathRuns == 0;
            float Since = Time.unscaledTime;
            for (int Look = 0; Look < 12 && Time.unscaledTime - Since < 600f;)
            {
                yield return new WaitForSecondsRealtime(15f);
                Back |= GameManager.IsDungeon(PlayerFarming.Location);
                if (!Back || PlayerFarming.Location != FollowerLocation.Base || PlayerFarming.Instance == null || HUD_Manager.Instance == null || MMTransition.IsPlaying)
                    continue;
                ++Look;
                /* A notice of the game's own on top (as the one-life mode's, after the end) is read and accepted, as a player would */
                foreach (src.UI.UIMenuConfirmationWindow Notice in UnityEngine.Object.FindObjectsOfType<src.UI.UIMenuConfirmationWindow>())
                {
                    string Header = (AccessTools.Field(typeof(src.UI.UIMenuConfirmationWindow), "_headerText")?.GetValue(Notice) as TMPro.TextMeshProUGUI)?.text;
                    Plugin.Logger?.LogWarning($"{Tag} Camp: the game's notice '{Header}' up, accepting it");
                    Shot($"camp-notice-{Look}");
                    (AccessTools.Field(typeof(src.UI.UIMenuConfirmationWindow), "_confirmButton")?.GetValue(Notice) as MMButton)?.onClick.Invoke();
                }
                CultFaithManager Faith = UnityEngine.Object.FindObjectOfType<CultFaithManager>(true);
                HungerBar Hunger = UnityEngine.Object.FindObjectOfType<HungerBar>(true);
                Plugin.Logger?.LogWarning($"{Tag} Camp {Look}: hud hidden {HUD_Manager.Instance.Hidden}" +
                    $", faith circle {(Faith == null ? "none" : $"container {Faith.Container?.activeInHierarchy} alpha {Faith.CanvasGroup?.alpha:F2}")} flag {DataManager.Instance.ShowCultFaith}" +
                    $", hunger {(Hunger == null ? "none" : $"container {Hunger.Container?.activeInHierarchy} alpha {Hunger.CanvasGroup?.alpha:F2}")} flag {DataManager.Instance.ShowCultHunger}" +
                    $", followers {DataManager.Instance.Followers.Count}, players {PlayerFarming.players.Count}");
                Shot($"camp-{Look}");
            }
        }

        /* -cotlmp-nowoolhaven: a friend without Woolhaven, only while in the host's game (there the game's own DLC check
           runs on the host's save); off again at once anywhere else, so the player's own save is never checked so */
        private static IEnumerator WithoutWoolhaven()
        {
            bool Told = false;
            while (true)
            {
                bool InHosts = HostSave.Playing && !Plugin.GlobalsInternal.IsServerCreator;
                if (CheatConsole.MAJOR_DLC_DISABLED != InHosts)
                {
                    CheatConsole.MAJOR_DLC_DISABLED = InHosts;
                    Plugin.Logger?.LogWarning($"{Tag} Woolhaven taken as not owned: {InHosts}");
                }
                if (!InHosts && !Told && (Plugin.GlobalsInternal.IsServerCreator || PlayerFarming.Instance != null))
                {
                    Told = true;
                    Plugin.Logger?.LogError($"{Tag} -cotlmp-nowoolhaven works only for a friend in the host's game: not here");
                }
                yield return null;
            }
        }

        /* What the save has of the DLCs, the things the game takes away from a save whose DLC isn't owned
           (DataManager.Deactivate*DLC); it only reads */
        private static IEnumerator WatchDlc()
        {
            while (PlayerFarming.Location != FollowerLocation.Base || PlayerFarming.Instance == null || MMTransition.IsPlaying)
                yield return null;
            yield return new WaitForSecondsRealtime(8f);
            DataManager Data = DataManager.Instance;
            for (int Look = 0; Look < 15; Look++)
            {
                int Decorations = Interaction_DLCShrine.Decorations.Count(Data.UnlockedStructures.Contains);
                int Wed = Data.Followers.Count(Follower => Follower.SpouseFollowerID != -1);
                Plugin.Logger?.LogWarning($"{Tag} DLC {Look}: major {Data.MAJOR_DLC} heretic {Data.DLC_Heretic_Pack} cultist {Data.DLC_Cultist_Pack} " +
                    $"sinful {Data.DLC_Sinful_Pack} pilgrim {Data.DLC_Pilgrim_Pack} pre {Data.DLC_Pre_Purchase}, land {Data.LandPurchased} expansion {Data.OnboardedBaseExpansion}, " +
                    $"decorations {Decorations}, wed {Wed}, wedding {Data.UnlockedUpgrades.Contains(UpgradeSystem.Type.Ritual_FollowerWedding)}, followers {Data.Followers.Count}, " +
                    $"convo {Data.LandConvoProgress} yngya {Data.YngyaOffering} day {Data.CurrentDayIndex}, expand base failed {ExpandFailed(Data)}");
                yield return new WaitForSecondsRealtime(20f);
            }
        }

        /* One game, no session: the hidden slot 6 loaded as the menu's Continue does (the player's own slots never) */
        private static IEnumerator LoadOwnSlot()
        {
            if (LoadSlot != 6)
            {
                Plugin.Logger?.LogError($"{Tag} -cotlmp-loadslot only loads the hidden slot 6, not {LoadSlot}");
                yield break;
            }
            while (SceneManager.GetActiveScene().name != "Main Menu" || MMTransition.IsPlaying || Lamb.UI.MainMenu.UIMainMenuController.Instance?.LoadMenu == null)
                yield return null;
            yield return new WaitForSecondsRealtime(5f);
            Plugin.Logger?.LogWarning($"{Tag} Loading save slot {LoadSlot} on our own");
            AccessTools.Method(typeof(Lamb.UI.MainMenu.LoadMenu), "ContinueGame").Invoke(Lamb.UI.MainMenu.UIMainMenuController.Instance.LoadMenu, new object[] { LoadSlot });
        }

        /* -cotlmp-savelater=N: N s in the cult the game saves (the host's or our own game; the hidden slot 6 only) */
        private static IEnumerator SaveInCultLater()
        {
            while (PlayerFarming.Location != FollowerLocation.Base || PlayerFarming.Instance == null || MMTransition.IsPlaying)
                yield return null;
            yield return new WaitForSecondsRealtime(SaveLater);
            if (HostSave.Playing || SaveAndLoad.SAVE_SLOT != 6)
            {
                Plugin.Logger?.LogError($"{Tag} -cotlmp-savelater only saves the hidden slot 6 of our own game, not slot {SaveAndLoad.SAVE_SLOT} (host's save: {HostSave.Playing})");
                yield break;
            }
            SaveAndLoad.Save();
            Plugin.Logger?.LogWarning($"{Tag} Saved slot {SaveAndLoad.SAVE_SLOT} after {SaveLater:F0} s in the cult, followers {DataManager.Instance.Followers.Count}");
        }

        /* What the game's taking Woolhaven away leaves (DataManager.DeactivateWoolhavenDLC fails this objective group) */
        private static string ExpandFailed(DataManager Data)
        {
            const string Group = "Objectives/GroupTitles/ExpandBase";
            int Open = Data.Objectives?.Count(Item => Item != null && Item.GroupId == Group && Item.IsFailed) ?? 0;
            int Failed = Data.FailedObjectives?.Count(Item => Item != null && Item.GroupId == Group) ?? 0;
            int History = Data.FailedObjectivesHistory?.Count(Item => Item != null && Item.GroupId == Group) ?? 0;
            return $"open {Open}, failed {Failed}, history {History}";
        }

        private static IEnumerator MeetFox()
        {
            while (SceneManager.GetActiveScene().name != "Base Biome 1" || PlayerFarming.Instance == null || !AnyRemotePlayer() || MMTransition.IsPlaying)
                yield return null;
            yield return new WaitForSecondsRealtime(10f);

            if (!OnTestSave("-cotlmp-foxtest"))
                yield break;
            /* Night first (the friend's clock follows the host's), with followers for a deal */
            if (Plugin.GlobalsInternal.IsServerCreator)
            {
                HarmonyLib.AccessTools.Method(typeof(TimeManager), "StartNewPhase").Invoke(null, new object[] { DayPhase.Night });
                Plugin.Logger?.LogWarning($"{Tag} Night now: {TimeManager.CurrentPhase}");
            }
            yield return new WaitForSecondsRealtime(4f);
            Plugin.Logger?.LogWarning($"{Tag} To the shore at {TimeManager.CurrentPhase}");
            MMTransition.Play(MMTransition.TransitionType.ChangeRoomWaitToResume, MMTransition.Effect.BlackFade, "Hub-Shore", 1f, "", null);
            while (SceneManager.GetActiveScene().name != "Hub-Shore" || PlayerFarming.Instance == null || MMTransition.IsPlaying)
                yield return null;
            yield return new WaitForSecondsRealtime(8f);

            Interaction_NightFox Fox = UnityEngine.Object.FindObjectOfType<Interaction_NightFox>(true);
            Plugin.Logger?.LogWarning($"{Tag} Fox here: {Fox != null} active={Fox?.gameObject.activeInHierarchy} location={PlayerFarming.Location} phase={TimeManager.CurrentPhase}");
            if (Fox == null)
                yield break;
            PlayerFarming.Instance.transform.position = Fox.transform.position + Vector3.down * 2f;
            yield return new WaitForSecondsRealtime(3f);
            if (!string.IsNullOrEmpty(ShotsDir))
                Shot($"fox-before-{(Plugin.GlobalsInternal.IsServerCreator ? "host" : "client")}");

            /* The friend tries first, then the host talks */
            yield return new WaitForSecondsRealtime(Plugin.GlobalsInternal.IsServerCreator ? 8f : 2f);
            Plugin.Logger?.LogWarning($"{Tag} Talking to the fox");
            Fox.OnInteract(PlayerFarming.Instance.state);
            yield return new WaitForSecondsRealtime(Plugin.GlobalsInternal.IsServerCreator ? 6f : 14f);
            if (!string.IsNullOrEmpty(ShotsDir))
                Shot($"fox-talk-{(Plugin.GlobalsInternal.IsServerCreator ? "host" : "client")}");
            yield return new WaitForSecondsRealtime(40f);
            if (!string.IsNullOrEmpty(ShotsDir))
                Shot($"fox-after-{(Plugin.GlobalsInternal.IsServerCreator ? "host" : "client")}");
            Plugin.Logger?.LogWarning($"{Tag} Fox after: encounter={DataManager.Instance.CurrentFoxEncounter} done={string.Join(",", DataManager.Instance.FoxCompleted)} intro={string.Join(",", DataManager.Instance.FoxIntroductions)}");
        }

        /// <summary>
        /// A newcomer at the gate for both: the host starts recruiting it, the friend tries to talk to
        /// it meanwhile (refused), the host recruits it; each logs how many of that follower it has
        /// </summary>
        private static IEnumerator RecruitTogether()
        {
            while (SceneManager.GetActiveScene().name != "Base Biome 1" || PlayerFarming.Instance == null || !AnyRemotePlayer() ||
                   (!Plugin.GlobalsInternal.IsServerCreator && !HostSave.Playing))
                yield return null;
            bool Host = Plugin.GlobalsInternal.IsServerCreator;
            yield return new WaitForSecondsRealtime(Host ? 8f : 2f);
            if (!OnTestSave("-cotlmp-recruittest"))
                yield break;
            Vector3 Gate = PlayerFarming.Instance.transform.position + new Vector3(2.5f, 0f);
            FollowerRecruit Recruit = null;
            if (Host)
            {
                Recruit = FollowerManager.CreateNewRecruit(FollowerLocation.Base, Gate);
                Plugin.Logger?.LogWarning($"{Tag} Recruit test: a newcomer {Recruit?.Follower?.Brain?.Info?.ID} at the gate");
            }
            else
            {
                /* As the base comes up with a newcomer in the save's list */
                yield return Until(() => DataManager.Instance.Followers_Recruit.Count > 0, 40f);
                Recruit = FollowerManager.SpawnExistingRecruit(Gate);
                Plugin.Logger?.LogWarning($"{Tag} Recruit test: the newcomer {Recruit?.Follower?.Brain?.Info?.ID} stands here too");
            }
            int Id = Recruit?.Follower?.Brain?.Info?.ID ?? -1;
            yield return new WaitForSecondsRealtime(Host ? 6f : 7f);
            if (Host)
            {
                Plugin.Logger?.LogWarning($"{Tag} Recruit test: recruiting, ours to do {COTLMP.Game.CultRecruits.Start(Recruit)}");
                yield return new WaitForSecondsRealtime(3f);
                Recruit?.InstantRecruit();
            }
            else
            {
                bool Allowed = Recruit != null && COTLMP.Game.CultRecruits.Start(Recruit);
                Plugin.Logger?.LogWarning($"{Tag} Recruit test: we try to talk to it while the host recruits: {(Allowed ? "allowed" : "refused")}");
            }
            for (int Round = 0; Round < 6; Round++)
            {
                yield return new WaitForSecondsRealtime(4f);
                int Bodies = Follower.Followers.Count(Each => Each?.Brain?.Info != null && Each.Brain.Info.ID == Id);
                int Waiting = UnityEngine.Object.FindObjectsOfType<FollowerRecruit>().Count(Each => Each?.Follower?.Brain?.Info != null && Each.Follower.Brain.Info.ID == Id);
                int Listed = DataManager.Instance.Followers.Count(Info => Info?.ID == Id);
                Plugin.Logger?.LogWarning($"{Tag} Recruit test: follower {Id}: {Bodies} in the cult, {Waiting} waiting, {Listed} listed, {DataManager.Instance.Followers_Recruit.Count} newcomers, taken away {COTLMP.Game.CultRecruits.TakenAway}, refused {COTLMP.Game.CultRecruits.Refused}");
            }
        }

        /* THE TEMPLE TEST ****************************************************/

        /// <summary>
        /// The cult as the temple test sees it: the sermon day, the holiday, its cooldown, faith,
        /// what the followers think of us and how many are at the temple
        /// </summary>
        private static string TempleState()
        {
            var Followers = DataManager.Instance.Followers;
            return $"sermonday={DataManager.Instance.PreviousSermonDayIndex} day={TimeManager.CurrentDay} holiday={FollowerBrainStats.IsHoliday} " +
                   $"cooldown={UpgradeSystem.GetCoolDownNormalised(UpgradeSystem.Type.Ritual_Holiday):F2} faith={CultFaithManager.CurrentFaith:F1} " +
                   $"bones={Inventory.GetItemQuantity(InventoryItem.ITEM_TYPE.BONE)} adoration={Followers.Sum(Info => Info?.Adoration ?? 0f):F1} " +
                   $"thoughts={string.Join(",", Followers.Select(Info => $"{Info?.ID}:{string.Join("+", (Info?.Thoughts ?? new List<ThoughtData>()).Select(T => T.ThoughtType))}"))} " +
                   $"church={FollowerManager.FollowersAtLocation(FollowerLocation.Church).Count} where={PlayerFarming.Location} hands={CultHands.FriendCount}";
        }

        /// <summary>
        /// The host builds a temple and gets three followers and the bones for a holiday; the
        /// friend goes into the temple, preaches a sermon, holds the holiday ritual and closes the
        /// altar, as a player would through its menus. Both log the cult as it goes.
        /// </summary>
        private static IEnumerator TempleRun()
        {
            while (SceneManager.GetActiveScene().name != "Base Biome 1" || PlayerFarming.Instance == null || !AnyRemotePlayer() ||
                   (!Plugin.GlobalsInternal.IsServerCreator && !HostSave.Playing))
                yield return null;
            bool Host = Plugin.GlobalsInternal.IsServerCreator;
            if (!OnTestSave("-cotlmp-templetest"))
                yield break;
            Run("LogTemple", LogTemple());
            Run("CloseTutorials", CloseTutorials());

            if (Host)
            {
                yield return new WaitForSecondsRealtime(8f);
                Vector3 Here = PlayerFarming.Instance.transform.position;
                UpgradeSystem.UnlockAbility(UpgradeSystem.Type.Building_Temple, true);
                UpgradeSystem.UnlockAbility(UpgradeSystem.Type.Ritual_Holiday, true);
                StructuresData Temple = StructuresData.GetInfoByType(StructureBrain.TYPES.TEMPLE, 0);
                StructureManager.BuildStructure(FollowerLocation.Base, Temple, Here + new Vector3(0f, 9f), Temple.Bounds);
                Plugin.Logger?.LogWarning($"{Tag} Temple test: built a temple ({Temple.ID})");
                for (int Count = 0; Count < 3; Count++)
                {
                    FollowerInfo New = FollowerInfo.NewCharacter(FollowerLocation.Base);
                    DataManager.Instance.Followers.Add(New);
                    FollowerManager.SpawnFollower(New, Here + new Vector3(-2f - Count, 1f));
                }
                Inventory.ChangeItemQuantity(InventoryItem.ITEM_TYPE.BONE, 100);
                DataManager.Instance.PreviousSermonDayIndex = -1;
                Plugin.Logger?.LogWarning($"{Tag} Temple test: ready: {TempleState()}");
                yield break;
            }

            float Since = Time.realtimeSinceStartup;
            while ((Interaction_Temple.Instance == null || DataManager.Instance.Followers.Count < 3 ||
                    Inventory.GetItemQuantity(InventoryItem.ITEM_TYPE.BONE) < 75 || Time.realtimeSinceStartup - Since < 30f) && Time.realtimeSinceStartup - Since < 90f)
                yield return null;
            yield return new WaitForSecondsRealtime(3f);
            Plugin.Logger?.LogWarning($"{Tag} Temple test: going in, temple {Interaction_Temple.Instance != null}: {TempleState()}");
            if (Interaction_Temple.Instance == null)
                yield break;

            /* Through the door, as walking into it does */
            EnterBuilding Door = Interaction_Temple.Instance.Entrance;
            AccessTools.Method(typeof(EnterBuilding), "OnTriggerEnter2D").Invoke(Door, new object[] { PlayerFarming.Instance.GetComponent<Collider2D>() });
            yield return Until(() => PlayerFarming.Location == FollowerLocation.Church && !MMTransition.IsPlaying && Interaction_TempleAltar.Instance != null, 20f);
            yield return new WaitForSecondsRealtime(3f);
            Plugin.Logger?.LogWarning($"{Tag} Temple test: inside ({PlayerFarming.Location}): {TempleState()}");
            Shot("temple-inside");

            Interaction_TempleAltar.Instance.OnInteract(PlayerFarming.Instance.state);
            yield return Plugin.MonoInstance.StartCoroutine(PressAltar("OnSermonButtonClicked", "sermon"));
            Plugin.MonoInstance.StartCoroutine(TempleShots("sermon", 6));

            /* The altar's menu comes back once the sermon is over */
            yield return Plugin.MonoInstance.StartCoroutine(PressAltar("OnRitualsButtonClicked", "rituals", 150f));
            yield return Until(() => UnityEngine.Object.FindObjectOfType<Lamb.UI.Rituals.UIRitualsMenuController>() != null, 15f);
            yield return new WaitForSecondsRealtime(1.5f);
            var Rituals = UnityEngine.Object.FindObjectOfType<Lamb.UI.Rituals.UIRitualsMenuController>();
            Plugin.Logger?.LogWarning($"{Tag} Temple test: ritual menu {Rituals != null}, holding the holiday: {TempleState()}");
            if (Rituals != null)
                AccessTools.Method(Rituals.GetType(), "RitualItemSelected").Invoke(Rituals, new object[] { UpgradeSystem.Type.Ritual_Holiday });
            Plugin.MonoInstance.StartCoroutine(TempleShots("ritual", 6));

            yield return Until(() => AltarMenu() != null, 90f);
            yield return new WaitForSecondsRealtime(1.5f);
            Plugin.Logger?.LogWarning($"{Tag} Temple test: ritual over, closing the altar: {TempleState()}");
            AltarMenu()?.OnCancelButtonInput();
            yield return new WaitForSecondsRealtime(15f);
            Plugin.Logger?.LogWarning($"{Tag} Temple test: done: {TempleState()}");
        }

        private static Lamb.UI.AltarMenu.UIAltarMenuController AltarMenu()
        {
            var Menu = UnityEngine.Object.FindObjectOfType<Lamb.UI.AltarMenu.UIAltarMenuController>();
            return Menu != null && Menu.isActiveAndEnabled ? Menu : null;
        }

        /// <summary>
        /// Waits for the altar's menu and clicks one of its buttons; tutorials that come up meanwhile are closed
        /// </summary>
        private static IEnumerator PressAltar(string Button, string What, float Seconds = 20f)
        {
            float Since = Time.realtimeSinceStartup;
            string Seen = null;
            /* The menu last clicked takes a moment to go */
            while (AltarMenu() != null && Time.realtimeSinceStartup - Since < 10f)
                yield return null;
            while (AltarMenu() == null && Time.realtimeSinceStartup - Since < Seconds)
            {
                string Open = string.Join(",", UnityEngine.Object.FindObjectsOfType<Lamb.UI.UIMenuBase>().Where(Menu => Menu.isActiveAndEnabled).Select(Menu => Menu.GetType().Name));
                if (Open != Seen)
                {
                    Seen = Open;
                    Plugin.Logger?.LogWarning($"{Tag} Temple test: menus up: {Open}");
                }
                yield return new WaitForSecondsRealtime(0.5f);
            }
            yield return new WaitForSecondsRealtime(1.5f);
            var Altar = AltarMenu();
            Plugin.Logger?.LogWarning($"{Tag} Temple test: altar menu {Altar != null} after {Time.realtimeSinceStartup - Since:F0} s, choosing {What}: {TempleState()}");
            if (Altar != null)
                AccessTools.Method(Altar.GetType(), Button).Invoke(Altar, null);
        }

        private static IEnumerator TempleShots(string Name, int Count)
        {
            for (int Index = 1; Index <= Count; Index++)
            {
                yield return new WaitForSecondsRealtime(4f);
                Shot($"temple-{Name}-{Index}");
            }
        }

        /// <summary>
        /// The game's tutorial windows (the first sermon, the first ritual) are read and closed, as a player would
        /// </summary>
        private static IEnumerator CloseTutorials()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(1f);

                /* The first sermon's reward: the first upgrade on offer, then the tree closes */
                var Tree = UnityEngine.Object.FindObjectOfType<Lamb.UI.UIUpgradePlayerTreeMenuController>();
                if (Tree != null && Tree.isActiveAndEnabled)
                {
                    yield return new WaitForSecondsRealtime(2f);
                    var Nodes = Traverse.Create(Tree).Field("_treeNodes").GetValue<List<Lamb.UI.UpgradeTreeNode>>();
                    Lamb.UI.UpgradeTreeNode Node = Nodes?.FirstOrDefault(Each => Each != null && Each.State == Lamb.UI.UpgradeTreeNode.NodeState.Available);
                    Plugin.Logger?.LogWarning($"{Tag} Taking the upgrade {Node?.Upgrade}");
                    if (Node != null)
                        AccessTools.Method(typeof(Lamb.UI.UIUpgradeTreeMenuBase<Lamb.UI.UIPlayerUpgradeUnlockOverlayController>), "PerformUnlock").Invoke(Tree, new object[] { Node });
                    yield return new WaitForSecondsRealtime(8f);
                    if (Tree != null && Tree.isActiveAndEnabled)
                        Tree.Hide();
                    continue;
                }

                var Tutorial = UnityEngine.Object.FindObjectOfType<src.UI.Overlays.TutorialOverlay.UITutorialOverlayController>();
                if (Tutorial == null || !Tutorial.isActiveAndEnabled)
                    continue;
                yield return new WaitForSecondsRealtime(1.5f);
                if (Tutorial == null)
                    continue;
                Plugin.Logger?.LogWarning($"{Tag} Closing a tutorial window");
                Tutorial.Hide();
            }
        }

        private static IEnumerator LogTemple()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(10f);
                Plugin.Logger?.LogWarning($"{Tag} Temple: {TempleState()}");
            }
        }

        private static IEnumerator ChangeCult()
        {
            /* Who hosts is known once the session is up */
            while (SceneManager.GetActiveScene().name != "Base Biome 1" || PlayerFarming.Instance == null || !AnyRemotePlayer() ||
                   (!Plugin.GlobalsInternal.IsServerCreator && !HostSave.Playing))
                yield return null;
            bool Host = Plugin.GlobalsInternal.IsServerCreator;
            yield return new WaitForSecondsRealtime(Host ? 10f : 25f);
            if (!OnTestSave("-cotlmp-culttest"))
                yield break;
            Vector3 Here = PlayerFarming.Instance.transform.position;

            if (Host)
            {
                Inventory.ChangeItemQuantity(InventoryItem.ITEM_TYPE.LOG, 10);
                Plugin.Logger?.LogWarning($"{Tag} Cult test: +10 wood");
                yield return new WaitForSecondsRealtime(3f);
                StructuresData Bed = StructuresData.GetInfoByType(StructureBrain.TYPES.BED, 0);
                StructureManager.BuildStructure(FollowerLocation.Base, Bed, Here + new Vector3(3f, 0f), Bed.Bounds);
                Plugin.Logger?.LogWarning($"{Tag} Cult test: built a bed ({Bed.ID})");
                yield return new WaitForSecondsRealtime(3f);
                for (int Count = 0; Count < 2; Count++)
                {
                    FollowerInfo New = FollowerInfo.NewCharacter(FollowerLocation.Base);
                    DataManager.Instance.Followers.Add(New);
                    FollowerManager.SpawnFollower(New, Here + new Vector3(-2f - Count, 1f));
                    Plugin.Logger?.LogWarning($"{Tag} Cult test: new follower {New.Name} ({New.ID})");
                }
                yield return new WaitForSecondsRealtime(3f);
                yield return Plugin.MonoInstance.StartCoroutine(PickUpAtFeet(2));

                /* A tree by the friend, hit by us: the friend's frames show how it looks to the one who watches */
                yield return new WaitForSecondsRealtime(5f);
                Vector3 Friend = COTLMP.Game.PlayerManager.GetAnyPuppetPosition() ?? Here;
                Structures_Tree Chopped = StructureManager.GetAllStructuresOfType<Structures_Tree>(FollowerLocation.Base)
                    .Where(Brain => !Brain.TreeChopped && !Brain.Data.IsSapling).OrderBy(Brain => (Brain.Data.Position - Friend).sqrMagnitude).FirstOrDefault();
                for (int Swing = 0; Chopped != null && !Chopped.TreeChopped && Swing < 30; Swing++)
                {
                    Chopped.TreeHit(1f);
                    yield return new WaitForSecondsRealtime(0.6f);
                }
                Plugin.Logger?.LogWarning($"{Tag} Cult test: chopped tree {Chopped?.Data?.ID} by the friend");
            }
            else
            {
                Run("WatchTree", WatchTree());
                Inventory.ChangeItemQuantity(InventoryItem.ITEM_TYPE.STONE, 4);
                Plugin.Logger?.LogWarning($"{Tag} Cult test: +4 stone");
                yield return new WaitForSecondsRealtime(3f);
                StructuresData Torch = StructuresData.GetInfoByType(StructureBrain.TYPES.DECORATION_TORCH, 0);
                StructureManager.BuildStructure(FollowerLocation.Base, Torch, Here + new Vector3(-3f, -1f), Torch.Bounds);
                Plugin.Logger?.LogWarning($"{Tag} Cult test: built a torch ({Torch.ID})");
                yield return new WaitForSecondsRealtime(3f);
                FollowerInfo Named = DataManager.Instance.Followers.LastOrDefault();
                if (Named != null)
                {
                    Named.Name = "Synced";
                    Plugin.Logger?.LogWarning($"{Tag} Cult test: renamed follower {Named.ID} to Synced");
                }
                yield return new WaitForSecondsRealtime(3f);
                StructureBrain Bed = StructureManager.GetAllStructuresOfType(StructureBrain.TYPES.BED).LastOrDefault();
                if (Bed != null)
                {
                    foreach (Structure Built in Structure.Structures.ToList())
                    {
                        if (Built != null && Built.Brain == Bed)
                            UnityEngine.Object.Destroy(Built.gameObject);
                    }
                    Bed.Remove();
                    Plugin.Logger?.LogWarning($"{Tag} Cult test: removed the bed ({Bed.Data.ID})");
                }
                yield return new WaitForSecondsRealtime(3f);
                yield return Run("UseHands", UseHands());
            }

            /* Frames of both cults at the same moments (both copies run on one clock), named by the time */
            for (int Frame = 1; Frame <= (Host ? 8 : 3); Frame++)
            {
                while (DateTime.Now.Second % 15 != 0)
                    yield return null;
                Shot($"cult-{(Host ? "host" : "client")}-{DateTime.Now:mmss}");
                yield return new WaitForSecondsRealtime(1.5f);
            }
        }

        /// <summary>
        /// The friend's hands in the host's cult: a talk ending in an order to sleep, the cult's data,
        /// a follower gathered for a sermon and let go
        /// </summary>
        /// <summary>
        /// Items dropped at our feet and picked up as the game picks them up: counted once for both
        /// </summary>
        private static IEnumerator PickUpAtFeet(int Count)
        {
            for (int Index = 0; Index < Count; Index++)
            {
                InventoryItem.Spawn(InventoryItem.ITEM_TYPE.MUSHROOM_SMALL, 1, PlayerFarming.Instance.transform.position, 0f);
                yield return new WaitForSecondsRealtime(1f);
            }
            Plugin.Logger?.LogWarning($"{Tag} Cult test: dropped {Count} mushrooms at our feet, now {Inventory.GetItemQuantity(InventoryItem.ITEM_TYPE.MUSHROOM_SMALL)}");
        }

        /// <summary>
        /// The friend's frames of the host's hits on a tree: we stand by it and watch
        /// </summary>
        private static IEnumerator WatchTree()
        {
            Structures_Tree Hit = null;
            while (Hit == null)
            {
                Hit = StructureManager.GetAllStructuresOfType<Structures_Tree>(FollowerLocation.Base).FirstOrDefault(Brain => Brain.Data.Progress > 0f && !Brain.TreeChopped);
                yield return null;
            }
            PlayerFarming.Instance.transform.position = Hit.Data.Position + new Vector3(1.5f, -1.5f);
            for (int Frame = 0; Frame < 30; Frame++)
            {
                Shot($"tree-{Frame:D2}");
                yield return new WaitForSecondsRealtime(0.3f);
            }
        }

        private static IEnumerator UseHands()
        {
            yield return Plugin.MonoInstance.StartCoroutine(PickUpAtFeet(3));
            /* A tree and a rock, hit by hand: the host sees each hit and the stump */
            Vector3 Here = PlayerFarming.Instance.transform.position;
            /* The tree nearest the host, for the host's frames of it */
            Vector3 Near = COTLMP.Game.PlayerManager.GetAnyPuppetPosition() ?? Here;
            Structures_Tree Tree = StructureManager.GetAllStructuresOfType<Structures_Tree>(FollowerLocation.Base)
                .Where(Brain => !Brain.TreeChopped && !Brain.Data.IsSapling).OrderBy(Brain => (Brain.Data.Position - Near).sqrMagnitude).FirstOrDefault();
            for (int Swing = 0; Tree != null && !Tree.TreeChopped && Swing < 30; Swing++)
            {
                Tree.TreeHit(1f);
                yield return new WaitForSecondsRealtime(0.7f);
            }
            Plugin.Logger?.LogWarning($"{Tag} Cult test: chopped tree {Tree?.Data?.ID}");
            Structures_Rubble Rock = StructureManager.GetAllStructuresOfType<Structures_Rubble>(FollowerLocation.Base)
                .OrderBy(Brain => (Brain.Data.Position - Here).sqrMagnitude).FirstOrDefault();
            for (int Swing = 0; Rock != null && !Rock.ProgressFinished && Swing < 30; Swing++)
            {
                Rock.RemovalProgress += Rock.RemovalDurationInGameMinutes / 6f;
                Rock.UpdateProgress();
                yield return new WaitForSecondsRealtime(0.7f);
            }
            Plugin.Logger?.LogWarning($"{Tag} Cult test: broke rock {Rock?.Data?.ID}");
            yield return new WaitForSecondsRealtime(3f);

            Follower Talked = FollowerManager.FindFollowerByID(DataManager.Instance.Followers.LastOrDefault()?.ID ?? 0);
            interaction_FollowerInteraction Talk = Talked?.GetComponentInChildren<interaction_FollowerInteraction>(true);
            if (Talk != null)
            {
                Talk.OnInteract(PlayerFarming.Instance.state);
                Plugin.Logger?.LogWarning($"{Tag} Cult test: talking to {Talked.Brain.Info.Name} ({Talked.Brain.Info.ID}), in our hands: {CultHands.FriendRuns(Talked.Brain.Info.ID)}");
                yield return new WaitForSecondsRealtime(4f);
                AccessTools.Method(typeof(interaction_FollowerInteraction), "OnFollowerCommandFinalized")
                    ?.Invoke(Talk, new object[] { new[] { FollowerCommands.Sleep } });

                /* The command wheel closes the talk when a player picks; picked here, we close it */
                yield return new WaitForSecondsRealtime(1f);
                var Wheel = Lamb.UI.UIManager.GetActiveMenu<Lamb.UI.FollowerInteractionWheel.UIFollowerInteractionWheelOverlayController>();
                Wheel?.Hide();
                if (AccessTools.Field(typeof(interaction_FollowerInteraction), "Activated")?.GetValue(Talk) is true)
                    AccessTools.Method(typeof(interaction_FollowerInteraction), "Close", new[] { typeof(bool), typeof(bool), typeof(bool) })
                        ?.Invoke(Talk, new object[] { true, true, false });
                Plugin.Logger?.LogWarning($"{Tag} Cult test: told {Talked.Brain.Info.ID} to sleep, override {Talked.Brain.CurrentOverrideTaskType}");
            }
            else
            {
                Plugin.Logger?.LogWarning($"{Tag} Cult test: no follower to talk to");
            }
            yield return new WaitForSecondsRealtime(6f);

            DataManager.Instance.PreviousSermonDayIndex = TimeManager.CurrentDay;
            DataManager.Instance.LastFeastDeclared = 777f;
            Plugin.Logger?.LogWarning($"{Tag} Cult test: sermon day {TimeManager.CurrentDay}, feast at 777");
            yield return new WaitForSecondsRealtime(3f);

            FollowerBrain Gathered = DataManager.Instance.Followers.Where(Info => Info != null && Info.ID != Talked?.Brain?.Info?.ID)
                .Select(Info => FollowerBrain.FindBrainByID(Info.ID)).FirstOrDefault(Brain => Brain != null);
            if (Gathered == null)
                yield break;
            try
            {
                Gathered.HardSwapToTask(new FollowerTask_AttendTeaching());
            }
            catch (Exception e)
            {
                /* The test's cult has no temple to go to */
                Plugin.Logger?.LogWarning($"{Tag} Cult test: gathering without a temple: {e.Message}");
            }
            Plugin.Logger?.LogWarning($"{Tag} Cult test: {Gathered.Info.Name} ({Gathered.Info.ID}) gathered for a sermon, in our hands: {CultHands.FriendRuns(Gathered.Info.ID)}");
            yield return new WaitForSecondsRealtime(25f);
            try
            {
                Gathered.CompleteCurrentTask();
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"{Tag} Cult test: letting go without a temple: {e.Message}");
            }
            Plugin.Logger?.LogWarning($"{Tag} Cult test: {Gathered.Info.ID} let go at {Gathered.Location}");
            yield return new WaitForSecondsRealtime(5f);

            /* Changes lost on the way: the sync check has to find each and take the host's */
            FollowerInfo Renamed = DataManager.Instance.Followers.FirstOrDefault(Info => Info?.Name == "Synced");
            if (Renamed != null)
                Renamed.Name = "Diverged";
            DataManager.Instance.PreviousSermonDayIndex = 99;
            Inventory.ChangeItemQuantity(InventoryItem.ITEM_TYPE.STONE, 3);
            StructureBrain Torch = StructureManager.GetAllStructuresOfType(StructureBrain.TYPES.DECORATION_TORCH).FirstOrDefault();
            if (Torch != null)
            {
                foreach (Structure Built in Structure.Structures.ToList())
                {
                    if (Built != null && Built.Brain == Torch)
                        UnityEngine.Object.Destroy(Built.gameObject);
                }
                Torch.Remove();
            }
            StructuresData Extra = StructuresData.GetInfoByType(StructureBrain.TYPES.DECORATION_TORCH, 0);
            StructureManager.BuildStructure(FollowerLocation.Base, Extra, Here + new Vector3(4f, 2f), Extra.Bounds);
            yield return null;
            CultSync.LoseChangesForTest();
            Plugin.Logger?.LogWarning($"{Tag} Cult test: lost a rename, the sermon day, 3 stone, torch {Torch?.Data?.ID} and an extra torch {Extra.ID} on the way");
        }
    }
}

/* EOF */
