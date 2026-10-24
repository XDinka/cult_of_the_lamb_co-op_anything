/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     The joining player plays on the host's save, in memory only
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP.Debug;
using HarmonyLib;
using MMTools;
using src.UINavigator;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// As in the game's own co-op, the second player plays in the host's game.
    /// </summary>
    /// <remarks>
    /// A player who joins (from the main menu, or from their own game, which is saved first)
    /// asks the host for its save. The host sends its DataManager the way the game writes a save
    /// file (MessagePack with the game's options), in pieces. The joining game takes it in
    /// memory the way "Continue" loads a slot (into the cult scene), with saving off: nothing of it
    /// is written, and the player's own slots stay as they were. Leaving the session (or the host
    /// stopping) goes back to the main menu, and the host's save is forgotten.
    /// </remarks>
    [HarmonyPatch]
    internal static class HostSave
    {
        /// <summary>
        /// We play on the host's save
        /// </summary>
        internal static bool Playing { get; private set; }

        /// <summary>
        /// We asked for the host's save and wait for it
        /// </summary>
        internal static bool Waiting { get; private set; }

        /* Counters for the test harness report */
        internal static int SentBytes, ReceivedBytes, Loads;

        private const int PieceSize = 32 * 1024;

        /* A slot no menu shows: anything that still asks for the slot gets this one, never the player's */
        private const int NoSlot = 7;

        private static byte[][] Pieces;
        private static int Transfer;
        private static float AskedAt;

        /*
         * The game's own MessagePack and options, as it writes a save file. Reached by reflection:
         * its memory types live in the game's mscorlib, not in the framework the mod builds against.
         */
        private static readonly Type Serializer = AccessTools.TypeByName("MessagePack.MessagePackSerializer");

        private static object Options => AccessTools.Field(AccessTools.TypeByName("Data.Serialization.MPSerialization"), "options").GetValue(null);

        private static readonly MethodInfo SerializeOf = Serializer?.GetMethods().First(Item => Item.Name == "Serialize" && Item.IsGenericMethodDefinition &&
            Item.ReturnType == typeof(byte[]) && Item.GetParameters().Length == 3);
        private static readonly MethodInfo DeserializeOf = Serializer?.GetMethods().First(Item => Item.Name == "Deserialize" && Item.IsGenericMethodDefinition &&
            Item.GetParameters().Length == 3 && Item.GetParameters()[0].ParameterType.Name == "ReadOnlyMemory`1");
        private static readonly Dictionary<Type, (MethodInfo Serialize, MethodInfo Deserialize)> Made = new();
        private static object CachedOptions;

        private static (MethodInfo Serialize, MethodInfo Deserialize) For(Type Of)
        {
            if (!Made.TryGetValue(Of, out var Pair))
                Made[Of] = Pair = (SerializeOf.MakeGenericMethod(Of), DeserializeOf.MakeGenericMethod(Of));
            CachedOptions ??= Options;
            return Pair;
        }

        internal static byte[] Pack(Type Of, object Value)
        {
            return (byte[])For(Of).Serialize.Invoke(null, new[] { Value, CachedOptions, CancellationToken.None });
        }

        internal static object Unpack(Type Of, byte[] Data)
        {
            MethodInfo Method = For(Of).Deserialize;
            Type Memory = Method.GetParameters()[0].ParameterType;
            object Buffer = Activator.CreateInstance(Memory, Data);
            return Method.Invoke(null, new[] { Buffer, CachedOptions, CancellationToken.None });
        }

        /// <summary>
        /// Connected to the host: save our own game if one is up, then ask for the host's
        /// </summary>
        /// <summary>
        /// The join went on hold for an update that didn't happen: the host's save after all
        /// </summary>
        internal static void ContinueJoin()
        {
            if (!CoopSync.InSession || Playing)
                return;
            Waiting = true;
            Notify("Multiplayer/Steam/LoadingHostSave");
            Ask();
        }

        internal static void BeginJoin()
        {
            Plugin.MonoInstance.StartCoroutine(Join());
        }

        private static IEnumerator Join()
        {
            Waiting = true;
            Pieces = null;

            /* Our own game first, as its own pause menu would, and only once that write is done */
            if (PlayerFarming.Instance != null && MMBiomeGeneration.BiomeGenerator.Instance == null && DataManager.Instance != null &&
                DataManager.Instance.AllowSaving && !DataManager.Instance.DisableSaving)
            {
                bool Saved = false;
                Action Done = () => Saved = true;
                SaveAndLoad.OnSaveCompleted += Done;
                Plugin.Logger?.LogInfo("[COOP] Saving our own game before taking the host's");
                SaveAndLoad.Save();
                float Until = Time.realtimeSinceStartup + 8f;
                while (!Saved && Time.realtimeSinceStartup < Until)
                    yield return null;
                SaveAndLoad.OnSaveCompleted -= Done;
            }

            if (!Waiting || !CoopSync.InSession)
                yield break;

            /* Both mods' versions first: a difference is shown, and the player may update instead */
            bool GoOn = true;
            yield return VersionCheck.Gate(Answer => GoOn = Answer);
            if (!GoOn || !Waiting || !CoopSync.InSession)
            {
                Waiting = false;
                yield break;
            }
            Notify("Multiplayer/Steam/LoadingHostSave");
            Ask();
        }

        private static void Ask()
        {
            AskedAt = Time.realtimeSinceStartup;
            Plugin.Logger?.LogInfo("[COOP] Asking the host for its save");
            CoopSync.Send(CoopSync.Kind.SaveRequest, Writer => Writer.Write((byte)1));
        }

        public static void Initialize()
        {
            Plugin.MonoInstance.StartCoroutine(AskAgain());
        }

        /// <summary>
        /// Asks again if the save didn't come (a lost request while the host was loading)
        /// </summary>
        private static IEnumerator AskAgain()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(2f);
                if (Waiting && CoopSync.InSession && AskedAt > 0f && Time.realtimeSinceStartup - AskedAt > 20f && Pieces == null)
                    Ask();
            }
        }

        /// <summary>
        /// The host: a player asks for our save
        /// </summary>
        internal static void OnRequest(uint From)
        {
            if (!Plugin.GlobalsInternal.IsServerCreator || DataManager.Instance == null)
                return;

            byte[] Save, Meta;
            CultSync.OnSaveSent();
            try
            {
                Save = Pack(typeof(DataManager), DataManager.Instance);
                Meta = Pack(typeof(MetaData), DataManager.Instance.MetaData);
            }
            catch (Exception Error)
            {
                Plugin.Logger?.LogError($"[COOP] Can't pack our save for player {From}: {Error}");
                return;
            }

            /* Both in one stream: the meta data (difficulty) is a file of its own in the game */
            using MemoryStream Stream = new();
            using (BinaryWriter Writer = new(Stream, System.Text.Encoding.UTF8, true))
            {
                Writer.Write(Meta.Length);
                Writer.Write(Meta);
                Writer.Write(Save);
            }
            byte[] All = Stream.ToArray();

            int Count = (All.Length + PieceSize - 1) / PieceSize;
            int Id = UnityEngine.Random.Range(1, int.MaxValue);
            for (int Index = 0; Index < Count; Index++)
            {
                int Offset = Index * PieceSize, Length = Math.Min(PieceSize, All.Length - Offset);
                CoopSync.Send(CoopSync.Kind.SaveChunk, Writer =>
                {
                    Writer.Write(Id);
                    Writer.Write(Count);
                    Writer.Write(Index);
                    Writer.Write(Length);
                    Writer.Write(All, Offset, Length);
                });
            }
            SentBytes += All.Length;
            Plugin.Logger?.LogInfo($"[COOP] Sent our save to player {From}: {All.Length} bytes in {Count} pieces");
        }

        /// <summary>
        /// The joining player: a piece of the host's save
        /// </summary>
        internal static void OnPiece(BinaryReader Reader)
        {
            int Id = Reader.ReadInt32(), Count = Reader.ReadInt32(), Index = Reader.ReadInt32(), Length = Reader.ReadInt32();
            byte[] Piece = Reader.ReadBytes(Length);
            if (!Waiting || Count <= 0 || Index < 0 || Index >= Count)
                return;

            if (Pieces == null || Transfer != Id || Pieces.Length != Count)
            {
                Transfer = Id;
                Pieces = new byte[Count][];
            }
            Pieces[Index] = Piece;
            foreach (byte[] Part in Pieces)
            {
                if (Part == null)
                    return;
            }

            using MemoryStream All = new();
            foreach (byte[] Part in Pieces)
                All.Write(Part, 0, Part.Length);
            Pieces = null;
            ReceivedBytes += (int)All.Length;
            Take(All.ToArray());
        }

        private static void Take(byte[] Data)
        {
            DataManager Save;
            MetaData? Meta;
            try
            {
                using MemoryStream Stream = new(Data);
                using BinaryReader Reader = new(Stream);
                byte[] MetaBytes = Reader.ReadBytes(Reader.ReadInt32());
                byte[] SaveBytes = Reader.ReadBytes((int)(Stream.Length - Stream.Position));
                Save = (DataManager)Unpack(typeof(DataManager), SaveBytes);
                Meta = MetaBytes.Length == 0 ? null : (MetaData)Unpack(typeof(MetaData), MetaBytes);
            }
            catch (Exception Error)
            {
                Plugin.Logger?.LogError($"[COOP] The host's save didn't unpack: {Error}");
                Waiting = false;
                Notify("Multiplayer/Steam/HostSaveFailed");
                COTLMP.Ui.PauseMenuPatches.EndSession(null);
                return;
            }

            Plugin.Logger?.LogInfo($"[COOP] Got the host's save ({Data.Length} bytes, {Save.Followers?.Count ?? 0} followers), going to its cult");
            Waiting = false;
            Playing = true;
            ++Loads;

            /* As "Continue" does: into the cult scene, and the save goes in on the way */
            SaveAndLoad.SAVE_SLOT = NoSlot;
            MonoSingleton<UINavigatorNew>.Instance?.Clear();
            AudioManager.Instance?.StopCurrentMusic();
            Time.timeScale = 1f;
            MMTransition.Play(MMTransition.TransitionType.ChangeRoomWaitToResume, MMTransition.Effect.BlackFade, "Base Biome 1", 3f, "", () => Load(Save, Meta));
        }

        /// <summary>
        /// What the game does with a save file it read (SaveAndLoad's read callback), without the
        /// slot's own meta file
        /// </summary>
        private static void Load(DataManager Save, MetaData? Meta)
        {
            Save.DisableSaving = true;
            DataManager.Instance = Save;
            Save.Followers?.Sort((A, B) => A.ID.CompareTo(B.ID));
            Save.Objectives?.RemoveAll(Item => Item == null);
            Save.CompletedObjectives?.RemoveAll(Item => Item == null);
            Save.FailedObjectives?.RemoveAll(Item => Item == null);
            Save.CompletedObjectivesHistory?.RemoveAll(Item => Item == null);
            Save.FailedObjectivesHistory?.RemoveAll(Item => Item == null);
            global::Data.ReadWrite.Conversion.COTLDataConversion.ConvertObjectiveIDs(Save);
            global::Data.ReadWrite.Conversion.COTLDataConversion.UpgradeTierMismatchFix(Save);
            Save.MetaData = Meta ?? MetaData.Default(Save);
            DifficultyManager.ForceDifficulty(Save.MetaData.Difficulty);
            SaveAndLoad.OnLoadComplete?.Invoke();
            SaveAndLoad.Loaded = true;
            MonoSingleton<Lamb.UI.UIManager>.Instance.ForceDisableSaving = true;
            MonoSingleton<UINavigatorNew>.Instance.LockInput = MonoSingleton<UINavigatorNew>.Instance.LockNavigation = false;
            Appearance.Refresh();
            CultSync.OnLoaded();
            Plugin.Logger?.LogInfo("[COOP] Playing on the host's save");
        }

        /// <summary>
        /// The session ended: back to the main menu, the host's save is dropped
        /// </summary>
        internal static void Forget()
        {
            bool Was = Playing || Waiting;
            Playing = Waiting = false;
            CultSync.Reset();
            Pieces = null;
            AskedAt = 0f;
            if (!Was)
                return;
            Plugin.Logger?.LogInfo("[COOP] Leaving the host's save, back to the main menu");
            MMTransition.Play(MMTransition.TransitionType.ChangeSceneAutoResume, MMTransition.Effect.BlackFade, "Main Menu", 1f, "", null);
        }

        private static void Notify(string LocKey)
        {
            try
            {
                NotificationCentre.Instance?.PlayGenericNotification(LocKey);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Nothing of the host's save is ever written on our side
        /// </summary>
        [HarmonyPatch(typeof(SaveAndLoad), nameof(SaveAndLoad.Save), new Type[0])]
        [HarmonyPrefix]
        private static bool Save() => !Playing;

        [HarmonyPatch(typeof(SaveAndLoad), "Saving")]
        [HarmonyPrefix]
        private static bool Saving()
        {
            if (Playing)
                PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, "A save of the host's game was stopped");
            return !Playing;
        }

        [HarmonyPatch(typeof(SaveAndLoad), nameof(SaveAndLoad.SaveMetaData))]
        [HarmonyPrefix]
        private static bool SaveMetaData() => !Playing;
    }
}

/* EOF */
