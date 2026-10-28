/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     The test harness: the report every 2 s, and the network stress test
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
        private static string Gear()
        {
            PlayerFarming Player = PlayerFarming.Instance;
            if (Player == null)
                return "gear=-";
            string Hearts(HealthPlayer Health) => Health == null ? "-" : $"{Health.HP}/{Health.totalHP}+b{Health.BlueHearts}+k{Health.BlackHearts}+s{Health.SpiritHearts}";
            PlayerFarming Puppet = Enumerable.Range(0, InternalData.MaxPlayersPerServerInternal).Select(Id => PlayerManager.GetPuppet((uint)Id)).FirstOrDefault(Each => Each != null);
            return $"gear={Player.currentWeapon}:{Player.currentWeaponLevel}/{Player.currentCurse}/{Player.RunTrinkets.Count}cards/{Player.health?.HP}hp hearts={Hearts(Player.health)} puppethearts={Hearts(Puppet?.health)}";
        }

        /// <summary>
        /// Floods the partner with data they don't use: one big packet and a burst of small ones.
        /// </summary>
        private static IEnumerator Flood()
        {
            var Wait = new WaitForSecondsRealtime(3f);
            byte[] Big = new byte[20000];
            new System.Random(5).NextBytes(Big);

            while (true)
            {
                yield return Wait;
                if (!COTLMP.Game.CoopSync.InSession)
                    continue;

                COTLMP.Game.CoopSync.Send((COTLMP.Game.CoopSync.Kind)250, Writer => Writer.Write(Big));
                for (int Index = 0; Index < 300; Index++)
                    COTLMP.Game.CoopSync.Send((COTLMP.Game.CoopSync.Kind)251, Writer => Writer.Write(Index));
                FloodSent += 301;
            }
        }

        private static IEnumerator ReportLoop()
        {
            var Wait = new WaitForSecondsRealtime(2f);
            StringBuilder Line = new();

            while (true)
            {
                yield return Wait;

                Line.Clear();
                Line.Append($"{Tag} place={COTLMP.Game.CoopSync.CurrentPlace()} seed={COTLMP.Game.CoopSync.LastSeed}/{MMBiomeGeneration.BiomeGenerator.Instance?.Seed} enemies={Health.team2.Count} seen={COTLMP.Game.CoopSync.EnemiesSeen} hpsent={COTLMP.Game.CoopSync.HitsSent} hpapplied={COTLMP.Game.CoopSync.HitsApplied} killsapplied={COTLMP.Game.CoopSync.KillsApplied}");
                Line.Append($" own={COTLMP.Game.RoomSync.Owning} follow={COTLMP.Game.RoomSync.Following} snapsent={COTLMP.Game.RoomSync.SnapshotsSent} snapapplied={COTLMP.Game.RoomSync.SnapshotsApplied} puppethits={COTLMP.Game.RoomSync.PuppetHitsSent} hitstaken={COTLMP.Game.RoomSync.HitsTaken} removed={COTLMP.Game.RoomSync.EnemiesRemoved} followed={COTLMP.Game.RoomSync.FollowedCount} visible={CountVisibleEnemies()} pickups={COTLMP.Game.SharedPickups.Sent}/{COTLMP.Game.SharedPickups.Received}/{COTLMP.Game.SharedPickups.Removed}");
                Line.Append($" knockouts={COTLMP.Game.CoopLife.KnockOuts} revives={COTLMP.Game.CoopLife.Revives} revivesgiven={COTLMP.Game.CoopLife.RevivesGiven} scenestuck={COTLMP.Game.SceneTrace.Stuck} down={COTLMP.Game.CoopLife.WeAreDown} partnerdown={COTLMP.Game.CoopLife.PartnerDown}");
                Line.Append($" cleared={COTLMP.Game.ClearedRooms.Sent}/{COTLMP.Game.ClearedRooms.Applied}/{COTLMP.Game.ClearedRooms.EnemiesHidden}");
                Line.Append($" dialog={COTLMP.Game.Dialogs.LinesSent}/{COTLMP.Game.Dialogs.LinesShown} camtargets={GameManager.GetInstance()?.CamFollowTarget?.targets.Count}:{(GameManager.GetInstance()?.CamFollowTarget?.targets.Count > 0 ? GameManager.GetInstance().CamFollowTarget.targets[0].gameObject?.name : "-")}");
                Line.Append($" hazards={COTLMP.Game.HazardSync.Sent}/{COTLMP.Game.HazardSync.Shown}/{COTLMP.Game.HazardSync.Missing} moved={COTLMP.Game.HazardSync.Moved}");
                Line.Append($" {Gear()}");
                Line.Append($" flood={FloodSent}/{COTLMP.Game.CoopSync.UnknownReceived} chaos={COTLMP.Network.LoopbackPeerLink.ChaosStats} ping={COTLMP.Game.ConnectionQuality.PingMs:F0} loss={COTLMP.Game.ConnectionQuality.Loss:F2} broken={COTLMP.Game.ConnectionQuality.Broken} hops={COTLMP.Network.Network.PacketsOut}>{COTLMP.Network.SteamBridge.FromLocal}|{COTLMP.Network.SteamBridge.FromLink}>{COTLMP.Network.Network.PacketsIn}");
                Line.Append($" seeded={COTLMP.Game.RoomRandom.Seeded} chest={Interaction_Chest.Instance != null} ghosts={COTLMP.Game.GhostLoot.Made} gold={Inventory.GetItemQuantity(InventoryItem.ITEM_TYPE.BLACK_GOLD)} grass={COTLMP.Game.RunGrass.SpotsGrown}:{COTLMP.Game.RunGrass.Run?.DungeonRun}/{DataManager.Instance?.dungeonRun}:{COTLMP.Game.RunGrass.Grown()}");
                Line.Append($" props={PropSync.Sent}/{PropSync.Received}/{PropSync.Applied}/{PropSync.Missing}/{PropSync.AppliedOnEntry}/{PropSync.AlreadyBroken} propmatch={PropSync.ByNumber}/{PropSync.BySpawn}/{PropSync.ByNearest}");
                Line.Append($" tarot={SharedTarot.Sent}/{SharedTarot.Received}/{SharedTarot.Applied} heart={LeaderHeart.Sent}/{LeaderHeart.Received}/{LeaderHeart.Applied} time={TimeManager.CurrentDay}:{TimeManager.CurrentGameTime:F0}:{TimeManager.CurrentPhase} cult={CultSync.MembersSent}/{CultSync.MembersApplied}/{CultSync.WholeSent}/{CultSync.WholeApplied}/{CultSync.GoneSent}/{CultSync.GoneApplied}/{CultSync.ItemsSent}/{CultSync.ItemsApplied}/{CultSync.PosesApplied}/{CultSync.TimeApplied} {CultState()}");
                Line.Append($" map={MapSync.LayoutsSent}/{MapSync.LayoutsAdopted}/{MapSync.PicksSent}/{MapSync.PicksApplied}/{MapSync.Conflicts} minimap={MiniMapSync.Marked} qafail={Failed} chat={COTLMP.Ui.TextChat.Sent}/{COTLMP.Ui.TextChat.Received} patchpartner={PatchCheck.PartnerFailed} faults={Faults.Warnings}/{Faults.Errors}/{Faults.Unhandled}/{Faults.Noted}/{PatchCheck.Failed.Count} reconnect={COTLMP.Network.Reconnect.Started}/{COTLMP.Network.Reconnect.Succeeded}/{COTLMP.Network.Reconnect.Failed}/{COTLMP.Network.Reconnect.Try} hostsave={COTLMP.Game.HostSave.Playing}/{COTLMP.Game.HostSave.Loads}/{COTLMP.Game.HostSave.ReceivedBytes}/{COTLMP.Game.HostSave.SentBytes} pose={PuppetSync.Sent}/{PuppetSync.Received}/{PuppetSync.Applied}/{PuppetSync.Appeared}/{PuppetSync.Vanished} enemyanims={RoomSync.EnemyAnimations} puppetanim={(COTLMP.Game.PlayerManager.GetPuppet(Plugin.GlobalsInternal.IsServerCreator ? 1u : 0u)?.Spine?.AnimationState?.GetCurrent(0)?.Animation?.Name)} myanim={(PlayerFarming.Instance?.Spine?.AnimationState?.GetCurrent(0)?.Animation?.Name)} node={(global::Map.MapManager.Instance?.CurrentNode?.point.x)},{(global::Map.MapManager.Instance?.CurrentNode?.point.y)}:{(global::Map.MapManager.Instance?.CurrentNode?.nodeType)}");
                Line.Append($" myskin={COTLMP.Game.Appearance.LocalSkin()} goat={(PlayerFarming.Instance != null && PlayerFarming.Instance.IsGoat)} puppetskin={COTLMP.Game.PlayerManager.GetSkin(Plugin.GlobalsInternal.IsServerCreator ? 1u : 0u)}");
                Line.Append($" scene={SceneManager.GetActiveScene().name} session={Plugin.GlobalsInternal.InGameSession} online={COTLMP.Network.Network.IsConnected} relay={COTLMP.Network.SteamBridge.Active} ts={Time.timeScale:F2} netlocal={COTLMP.Network.Network.HasLocalPlayer} sent={COTLMP.Network.Network.SentPositions} recv={COTLMP.Network.Network.ReceivedPositions}");
                if (PlayerFarming.Instance != null)
                    Line.Append($" local={Format(PlayerFarming.Instance.transform.position)}");

                for (uint Id = 0; Id < InternalData.MaxPlayersPerServerInternal; Id++)
                {
                    Vector3? Position = PlayerManager.GetPlayerPosition(Id);
                    if (Position.HasValue)
                    {
                        Line.Append($" remote{Id}={Format(Position.Value)}");
                        Vector3? Target = PlayerManager.GetPlayerTarget(Id);
                        if (Target.HasValue)
                            Line.Append($"->{Format(Target.Value)}");
                    }
                }

                Plugin.Logger?.LogWarning(Line.ToString());
            }
        }

        private static string CultState()
        {
            if (DataManager.Instance == null)
                return "";
            var Followers = DataManager.Instance.Followers;
            return $"wood={Inventory.GetItemQuantity(InventoryItem.ITEM_TYPE.LOG)} stone={Inventory.GetItemQuantity(InventoryItem.ITEM_TYPE.STONE)} " +
                   $"structures={StructureManager.StructuresDataAtLocation(FollowerLocation.Base).Count} beds={StructureManager.GetAllStructuresOfType(StructureBrain.TYPES.BED).Count} " +
                   $"torches={StructureManager.GetAllStructuresOfType(StructureBrain.TYPES.DECORATION_TORCH).Count} followers={Followers.Count}:{string.Join(",", Followers.Select(Info => Info?.Name))} " +
                   $"bodies={FollowerManager.FollowersAtLocation(FollowerLocation.Base).Count} temple={Interaction_Temple.Instance != null} " +
                   $"mushrooms={Inventory.GetItemQuantity(InventoryItem.ITEM_TYPE.MUSHROOM_SMALL)} trees={StructureManager.GetAllStructuresOfType<Structures_Tree>(FollowerLocation.Base).Count} rocks={StructureManager.GetAllStructuresOfType<Structures_Rubble>(FollowerLocation.Base).Count} hits={CultResources.HitsSent}/{CultResources.HitsApplied} " +
                   $"sermonday={DataManager.Instance.PreviousSermonDayIndex} feast={DataManager.Instance.LastFeastDeclared:F0} " +
                   $"checks={CultSync.ChecksSent}/{CultSync.ChecksTaken}/{CultSync.GroupsDiffered}/{CultSync.FixesAsked}/{CultSync.FixesSent}/{CultSync.FixesTaken} load={CultSync.LoadMsPerSecond:F2}/{CultSync.WorstFrameMs:F2}/{CultSync.KBPerSecond:F2} " +
                   $"fox={COTLMP.Game.FoxSync.Talks}/{COTLMP.Game.FoxSync.Mirrored}/{COTLMP.Game.FoxSync.Refused} lobby={COTLMP.Game.Lobby.Opened}/{COTLMP.Game.Lobby.Started}/{COTLMP.Game.Lobby.Stood}/{COTLMP.Game.Lobby.Heard} intro={COTLMP.Game.IntroCoop.State} " +
                   $"versions={COTLMP.Game.VersionCheck.Partner}/{COTLMP.Network.Updater.Version}/{COTLMP.Game.VersionCheck.Warned}/{COTLMP.Game.VersionCheck.Suggested}/{COTLMP.Game.VersionCheck.SuggestionsTaken} " +
                   $"hands={CultHands.Taken}/{CultHands.Given}/{CultHands.TakenBack}/{CultHands.Ticked}/{CultHands.Moved}/{CultHands.FriendCount}/{CultHands.PicksLeftToHost} data={CultSync.DataApplied} " +
                   $"tasks={string.Join(",", Followers.Select(Info => $"{Info?.ID}:{FollowerBrain.FindBrainByID(Info?.ID ?? 0)?.CurrentTaskType}:{Info?.Location}:{Info?.CurrentOverrideTaskType}"))}";
        }
    }
}

/* EOF */
