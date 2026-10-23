/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Back into the shared run after the game crashed
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP.Debug;
using COTLMP.Network;
using MMBiomeGeneration;
using MMTools;
using src.UINavigator;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// Brings a player whose game crashed back into the run they shared with the host.
    /// </summary>
    /// <remarks>
    /// The game doesn't save in a dungeon, so a crash loses the run. While the joining player is
    /// in a shared run, a small snapshot is kept on disk: which host and run, the floor and room,
    /// and what they carry (weapon, curse, relic, tarot cards, health). When the game starts with a
    /// snapshot, it reads it and deletes the file (a second crash on the way back doesn't loop),
    /// loads the same save, joins the same host, and if the host is still in that run, enters it
    /// (same run id, so the same floors), puts the equipment back and teleports to the host's
    /// room. The host sends the cleared rooms and enemy health again when we say hello.
    /// The snapshot is deleted as soon as the run ends or the session is left on purpose.
    /// </remarks>
    internal static class RunRecovery
    {
        [Serializable]
        private sealed class Snapshot
        {
            public int Version = 1;
            public long SavedAt;
            public ulong Host, Lobby;
            public int SaveSlot = -1;
            public int RunId, Location, Layer, Floor, RoomX, RoomY;
            public string Scene;
            public int Weapon, WeaponLevel, Curse, CurseLevel, Relic;
            public float HP, BlueHearts, BlackHearts;
            public List<int> Cards = new(), CardLevels = new();
        }

        /// <summary>
        /// An older snapshot is of a run that is surely over
        /// </summary>
        private const float MaxAgeMinutes = 20f;
        private const float WriteEvery = 2f;

        private static string FilePath => Path.Combine(BepInEx.Paths.ConfigPath, "cotlmp-rejoin.json");
        private static Snapshot Pending;
        private static bool Written;

        /* For the test harness report */
        internal static string Status = "none";

        public static void Initialize()
        {
            ReadPending();
            /* Quitting the game is leaving, not a crash (a crash never gets here) */
            Application.quitting += Forget;
            Plugin.MonoInstance.StartCoroutine(Keep());
            if (Pending != null)
                Plugin.MonoInstance.StartCoroutine(Restore(Pending));
        }

        /// <summary>
        /// Forgets the snapshot: the run ended, or the player left the session.
        /// </summary>
        internal static void Forget()
        {
            if (!Written)
                return;
            Written = false;
            try
            {
                File.Delete(FilePath);
            }
            catch (Exception e)
            {
                PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Deleting the run snapshot failed: {e.Message}");
            }
        }

        /* WRITING ************************************************************/

        private static IEnumerator Keep()
        {
            var Wait = new WaitForSecondsRealtime(WriteEvery);
            while (true)
            {
                yield return Wait;
                try
                {
                    KeepOnce();
                }
                catch (Exception e)
                {
                    PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Writing the run snapshot failed: {e.Message}");
                }
            }
        }

        private static void KeepOnce()
        {
            /* Only the joining player: the host's crash ends the session for everyone */
            if (!CoopSync.InSession || Plugin.GlobalsInternal.IsServerCreator || SteamBridge.JoinedHost == 0)
                return;
            if (MMTransition.IsPlaying || PlayerFarming.Instance == null)
                return;

            BiomeGenerator Generator = BiomeGenerator.Instance;
            string Place = CoopSync.CurrentPlace();
            bool SharedRun = Place != null && Place.Contains("#") && CoopSync.CurrentRun != 0 && CoopSync.CurrentRun == CoopSync.PartnerRun;

            /* Back in the base, or in a run of our own: nothing to come back to */
            if (!SharedRun || Generator == null || Generator.CurrentRoom == null)
            {
                if (Place != null && !Place.Contains("#"))
                    Forget();
                return;
            }

            PlayerFarming Player = PlayerFarming.Instance;
            Snapshot Value = new()
            {
                SavedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Host = SteamBridge.JoinedHost,
                Lobby = SteamBridge.JoinedLobby,
                SaveSlot = SaveAndLoad.SAVE_SLOT,
                RunId = CoopSync.CurrentRun,
                Location = (int)Generator.DungeonLocation,
                Scene = SceneManager.GetActiveScene().name,
                Layer = GameManager.CurrentDungeonLayer,
                Floor = GameManager.CurrentDungeonFloor,
                RoomX = Generator.CurrentRoom.x,
                RoomY = Generator.CurrentRoom.y,
                Weapon = (int)Player.currentWeapon,
                WeaponLevel = Player.currentWeaponLevel,
                Curse = (int)Player.currentCurse,
                CurseLevel = Player.currentCurseLevel,
                Relic = (int)Player.currentRelicType
            };
            if (Player.health != null)
            {
                Value.HP = Player.health.HP;
                Value.BlueHearts = Player.health.BlueHearts;
                Value.BlackHearts = Player.health.BlackHearts;
            }
            foreach (TarotCards.TarotCard Card in Player.RunTrinkets)
            {
                Value.Cards.Add((int)Card.CardType);
                Value.CardLevels.Add(Card.UpgradeIndex);
            }

            /* Write aside and swap, so a crash mid-write leaves the last good one */
            string Temp = FilePath + ".tmp";
            File.WriteAllText(Temp, JsonUtility.ToJson(Value));
            if (File.Exists(FilePath))
                File.Replace(Temp, FilePath, null);
            else
                File.Move(Temp, FilePath);
            if (!Written)
                Plugin.Logger?.LogInfo($"[COOP] Keeping a snapshot of run {Value.RunId} in case the game crashes");
            Written = true;
            if (Status != "restored")
                Status = "kept";
        }

        /* RESTORING **********************************************************/

        private static void ReadPending()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return;
                Snapshot Value = JsonUtility.FromJson<Snapshot>(File.ReadAllText(FilePath));

                /* Used once: if this attempt crashes too, the next start is a normal one */
                File.Delete(FilePath);

                double Age = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - Value.SavedAt) / 60.0;
                if (Value.Version != 1 || Value.RunId == 0 || Value.Host == 0 || Age > MaxAgeMinutes || Age < 0)
                {
                    Plugin.Logger?.LogInfo($"[COOP] Ignoring a run snapshot from {Age:F0} minutes ago");
                    return;
                }

                Pending = Value;
                Status = "pending";
                Plugin.Logger?.LogInfo($"[COOP] The game stopped during run {Value.RunId} {Age:F1} minutes ago, going back to it");
            }
            catch (Exception e)
            {
                PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Reading the run snapshot failed: {e.Message}");
            }
        }

        private static IEnumerator Restore(Snapshot Value)
        {
            /* 1. Our save, the one we played the run from */
            while (SceneManager.GetActiveScene().name != "Main Menu" && PlayerFarming.Instance == null)
                yield return null;
            if (SceneManager.GetActiveScene().name == "Main Menu")
            {
                while (MMTransition.IsPlaying)
                    yield return null;
                yield return new WaitForSecondsRealtime(2f);
                ContinueSave(Value.SaveSlot);
            }

            /* 2. The same host, once the save is loaded */
            SteamBridge.QueueRejoin(Value.Lobby, Value.Host);
            Status = "joining";
            yield return Until(() => CoopSync.InSession, 120f);
            if (!CoopSync.InSession)
            {
                Give("couldn't join the host again", "Multiplayer/Recovery/Failed");
                yield break;
            }

            /* 3. Is the host still in that run? It says so after our hello */
            yield return Until(() => CoopSync.PartnerRun == Value.RunId, 15f);
            if (CoopSync.PartnerRun != Value.RunId)
            {
                Give($"the host is no longer in run {Value.RunId}", "Multiplayer/Recovery/RunOver");
                yield break;
            }

            /* 4. Into the dungeon: a new run there adopts the host's run id, so the floors are the same */
            while (MMTransition.IsPlaying || PlayerFarming.Instance == null)
                yield return null;
            Status = "entering";
            Notify("Multiplayer/Recovery/Returning");
            Plugin.Logger?.LogInfo($"[COOP] Going back into run {Value.RunId}: {Value.Scene}, layer {Value.Layer} floor {Value.Floor}");
            GameManager.NewRun("", false, (FollowerLocation)Value.Location);
            GameManager.CurrentDungeonLayer = Value.Layer;
            GameManager.CurrentDungeonFloor = Value.Floor;
            MMTransition.Play(MMTransition.TransitionType.ChangeRoomWaitToResume, MMTransition.Effect.BlackFade, Value.Scene, 1f, "", null);

            yield return Until(() => BiomeGenerator.Instance != null && BiomeGenerator.Instance.CurrentRoom != null &&
                                     PlayerFarming.Instance != null && !MMTransition.IsPlaying, 90f);
            if (BiomeGenerator.Instance?.CurrentRoom == null)
            {
                Give("the dungeon didn't load", "Multiplayer/Recovery/Failed");
                yield break;
            }
            yield return new WaitForSecondsRealtime(1.5f);

            /* 5. Where the host is (or where we were) */
            int X = Value.RoomX, Y = Value.RoomY;
            if (TryParseRoom(RoomSync.PartnerLastPlace, out int Layer, out int Floor, out int PartnerX, out int PartnerY) &&
                Layer == GameManager.CurrentDungeonLayer && Floor == GameManager.CurrentDungeonFloor)
            {
                X = PartnerX;
                Y = PartnerY;
            }
            BiomeRoom Here = BiomeGenerator.Instance.CurrentRoom;
            if ((Here.x != X || Here.y != Y) && BiomeRoom.GetRoom(X, Y) != null)
            {
                Plugin.Logger?.LogInfo($"[COOP] Teleporting to room {X},{Y}");
                BiomeGenerator.Instance.IsTeleporting = true;
                MMTransition.Play(MMTransition.TransitionType.ChangeRoom, MMTransition.Effect.BlackFade, MMTransition.NO_SCENE, 0.3f, "", () => BiomeGenerator.ChangeRoom(X, Y));
                yield return Until(() => !MMTransition.IsPlaying && BiomeGenerator.Instance?.CurrentRoom != null &&
                                         BiomeGenerator.Instance.CurrentRoom.x == X && BiomeGenerator.Instance.CurrentRoom.y == Y, 20f);
                yield return new WaitForSecondsRealtime(1f);
            }

            /*
             * 6. What we carried. After the teleport: the game may set the lamb up again on arrival
             * (a fresh run's first room does), which drops what was handed over before. Checked
             * again a little later, and handed over once more if it was lost.
             */
            for (int Attempt = 0; Attempt < 3; Attempt++)
            {
                try
                {
                    Equip(Value);
                }
                catch (Exception e)
                {
                    PrintLogger.Print(DebugLevel.ERROR_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Putting the equipment back failed: {e}");
                }
                yield return new WaitForSecondsRealtime(3f);
                if (Equipped(Value))
                    break;
                Plugin.Logger?.LogInfo("[COOP] The equipment didn't stay, handing it over again");
            }

            Status = "restored";
            Pending = null;
            Notify("Multiplayer/Recovery/Restored");
            Plugin.Logger?.LogInfo($"[COOP] Back in run {Value.RunId} at {CoopSync.CurrentPlace()}");
        }

        private static bool Equipped(Snapshot Value)
        {
            PlayerFarming Player = PlayerFarming.Instance;
            return Player != null && (int)Player.currentWeapon == Value.Weapon && (int)Player.currentCurse == Value.Curse &&
                   Player.RunTrinkets.Count >= Value.Cards.Count;
        }

        private static void Equip(Snapshot Value)
        {
            PlayerFarming Player = PlayerFarming.Instance;

            if (Value.Weapon != (int)EquipmentType.None)
                Player.playerWeapon.SetWeapon((EquipmentType)Value.Weapon, Value.WeaponLevel);
            if (Value.Curse != (int)EquipmentType.None)
                Player.playerSpells.SetSpell((EquipmentType)Value.Curse, Value.CurseLevel);
            if (Value.Relic != (int)RelicType.None)
                Player.playerRelic.EquipRelic(EquipmentManager.GetRelicData((RelicType)Value.Relic));

            for (int Index = 0; Index < Value.Cards.Count; Index++)
            {
                var Card = new TarotCards.TarotCard((TarotCards.Card)Value.Cards[Index], Index < Value.CardLevels.Count ? Value.CardLevels[Index] : 0);
                if (!Player.RunTrinkets.Exists(Have => Have.CardType == Card.CardType))
                    TrinketManager.AddTrinket(Card, Player);
            }

            /* Last: the cards change how much health there can be */
            if (Player.health != null && Value.HP > 0f)
            {
                Player.health.HP = Mathf.Min(Value.HP, Player.health.totalHP);
                Player.health.BlueHearts = Value.BlueHearts;
                Player.health.BlackHearts = Value.BlackHearts;
            }

            Plugin.Logger?.LogInfo($"[COOP] Equipment back: weapon {(EquipmentType)Value.Weapon} {Value.WeaponLevel}, curse {(EquipmentType)Value.Curse} {Value.CurseLevel}, relic {(RelicType)Value.Relic}, {Value.Cards.Count} cards, {Value.HP} HP");
        }

        /// <summary>
        /// Loads a save slot the way Continue in the load menu does.
        /// </summary>
        private static void ContinueSave(int Slot)
        {
            if (Slot < 0 || !SaveAndLoad.SaveExist(Slot))
            {
                Plugin.Logger?.LogInfo("[COOP] No save to load for the way back, waiting for the player to load one");
                return;
            }

            Plugin.Logger?.LogInfo($"[COOP] Loading save slot {Slot} to go back to the run");
            MonoSingleton<UINavigatorNew>.Instance.Clear();
            SaveAndLoad.SAVE_SLOT = Slot;
            AudioManager.Instance?.StopCurrentMusic();
            MMTransition.Play(MMTransition.TransitionType.ChangeRoomWaitToResume, MMTransition.Effect.BlackFade, "Base Biome 1", 3f, "", () =>
            {
                AudioManager.Instance?.StopCurrentMusic();
                SaveAndLoad.Load(SaveAndLoad.SAVE_SLOT);
                MonoSingleton<UINavigatorNew>.Instance.LockInput = MonoSingleton<UINavigatorNew>.Instance.LockNavigation = false;
            });
        }

        /// <summary>
        /// Reads "Scene#layer.floor@x,y".
        /// </summary>
        private static bool TryParseRoom(string Place, out int Layer, out int Floor, out int X, out int Y)
        {
            Layer = Floor = X = Y = 0;
            if (string.IsNullOrEmpty(Place))
                return false;
            int Hash = Place.IndexOf('#'), At = Place.IndexOf('@');
            if (Hash < 0 || At < Hash)
                return false;
            string[] Levels = Place.Substring(Hash + 1, At - Hash - 1).Split('.');
            string[] Room = Place.Substring(At + 1).Split(',');
            return Levels.Length == 2 && Room.Length == 2 &&
                   int.TryParse(Levels[0], out Layer) && int.TryParse(Levels[1], out Floor) &&
                   int.TryParse(Room[0], out X) && int.TryParse(Room[1], out Y);
        }

        private static void Give(string Why, string LocKey)
        {
            Status = "gave up";
            Pending = null;
            Plugin.Logger?.LogWarning($"[COOP] Not going back to the run: {Why}");
            Notify(LocKey);
        }

        private static void Notify(string LocKey)
        {
            try
            {
                NotificationCentre.Instance?.PlayGenericNotification(LocKey);
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Can't show notification {LocKey}: {e.Message}");
            }
        }

        private static IEnumerator Until(Func<bool> Condition, float Seconds)
        {
            float Start = Time.realtimeSinceStartup;
            while (!Condition() && Time.realtimeSinceStartup - Start < Seconds)
                yield return null;
        }
    }
}

/* EOF */
