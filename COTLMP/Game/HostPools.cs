/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     In a shared run, rewards come from the host's unlocks for both players
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP.Debug;
using HarmonyLib;
using MMBiomeGeneration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// Gives the joining player the host's reward pools while rolling dungeon rewards.
    /// </summary>
    /// <remarks>
    /// RoomRandom makes both games roll with the same random state, but a roll also depends on
    /// what each save has unlocked: weapons and curses, tarot cards, relics, and a few counters.
    /// The host sends those; in a dungeon the joining player's game uses them for exactly the length
    /// of each roll (the methods below), then gets its own back. A save can't happen in between
    /// (it runs on the main thread, after the call), so the host's unlocks never end up in the
    /// joining player's save. Anything the game adds to a pool during a roll (a card found, say) is
    /// added to the joining player's own pool as well, so nothing they earn is lost.
    /// </remarks>
    internal static class HostPools
    {
        private sealed class Pools
        {
            public List<EquipmentType> Weapons = new(), Curses = new();
            public List<TarotCards.Card> Cards = new();
            public List<RelicType> Relics = new();
            public int DungeonRun;
            public bool OnboardedRelics, EnabledSpells;
        }

        private static Pools Host;
        private static Pools Saved;
        private static int Depth;
        private static int WeaponsBefore, CursesBefore, CardsBefore, RelicsBefore;
        private static readonly Harmony Hooks = new("cotlmp.hostpools");

        /* Counters for the test harness report */
        internal static int Received, Scopes;

        /// <summary>
        /// The methods that roll rewards from the pools. Whole classes are wrapped where every
        /// method may read them; coroutines are wrapped step by step.
        /// </summary>
        private static readonly (Type, string)[] Rollers =
        {
            (typeof(TarotCards), nameof(TarotCards.DrawRandomCard)),
            (typeof(TarotCards), nameof(TarotCards.DrawRandomCorruptedCard)),
            (typeof(TarotCards), nameof(TarotCards.GetUnusedFoundTrinkets)),
            (typeof(EquipmentManager), nameof(EquipmentManager.PickRandomRelicData)),
            (typeof(EquipmentManager), nameof(EquipmentManager.GetRandomRelicData)),
            (typeof(BiomeGenerator), "Start")
        };

        private static readonly Type[] RollingClasses =
        {
            typeof(Interaction_Chest),
            typeof(Interaction_WeaponSelectionPodium),
            typeof(Interaction_WeaponItem)
        };

        public static void Initialize()
        {
            Plugin.MonoInstance.StartCoroutine(Announce());
            HarmonyMethod Enter = new(typeof(HostPools).GetMethod(nameof(EnterScope), BindingFlags.NonPublic | BindingFlags.Static));
            HarmonyMethod Exit = new(typeof(HostPools).GetMethod(nameof(ExitScope), BindingFlags.NonPublic | BindingFlags.Static));

            List<MethodBase> Targets = new();
            foreach (var (Owner, Name) in Rollers)
            {
                foreach (MethodInfo Method in Owner.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (Method.Name == Name)
                        Targets.Add(Method);
                }
            }
            Targets.Add(AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(BiomeGenerator), "GenerateRoutine")));

            foreach (Type Owner in RollingClasses)
            {
                foreach (MethodInfo Method in Owner.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (Method.IsAbstract || Method.IsGenericMethod || Method.Name == "Update" || Method.Name == "LateUpdate" || Method.Name == "FixedUpdate")
                        continue;
                    if (Method.ReturnType == typeof(IEnumerator))
                    {
                        MethodInfo Step = AccessTools.EnumeratorMoveNext(Method);
                        if (Step != null)
                            Targets.Add(Step);
                    }
                    else if (Method.GetMethodBody() != null)
                    {
                        Targets.Add(Method);
                    }
                }
            }

            int Patched = 0;
            foreach (MethodBase Target in Targets.Where(Target => Target != null).Distinct())
            {
                try
                {
                    Hooks.Patch(Target, prefix: Enter, finalizer: Exit);
                    ++Patched;
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogWarning($"[COOP] Can't wrap {Target.DeclaringType?.Name}.{Target.Name}: {e.Message}");
                }
            }
            Plugin.Logger?.LogInfo($"[COOP] Reward rolls wrapped: {Patched}");
        }

        /* HOST ***************************************************************/

        /// <summary>
        /// The host sends its pools now and then (they change as it unlocks things)
        /// </summary>
        private static IEnumerator Announce()
        {
            var Wait = new WaitForSecondsRealtime(5f);
            while (true)
            {
                yield return Wait;
                if (!CoopSync.InSession || !Plugin.GlobalsInternal.IsServerCreator || DataManager.Instance == null || Depth > 0)
                    continue;
                try
                {
                    DataManager Data = DataManager.Instance;
                    CoopSync.Send(CoopSync.Kind.HostPools, Writer =>
                    {
                        WriteList(Writer, Data.WeaponPool.Select(Value => (int)Value));
                        WriteList(Writer, Data.CursePool.Select(Value => (int)Value));
                        WriteList(Writer, Data.PlayerFoundTrinkets.Select(Value => (int)Value));
                        WriteList(Writer, Data.PlayerFoundRelics.Select(Value => (int)Value));
                        Writer.Write(Data.dungeonRun);
                        Writer.Write(Data.OnboardedRelics);
                        Writer.Write(Data.EnabledSpells);
                    });
                }
                catch (Exception e)
                {
                    PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Sending the reward pools failed: {e.Message}");
                }
            }
        }

        private static void WriteList(BinaryWriter Writer, IEnumerable<int> Values)
        {
            List<int> All = Values.ToList();
            Writer.Write((ushort)All.Count);
            foreach (int Value in All)
                Writer.Write(Value);
        }

        private static List<int> ReadList(BinaryReader Reader)
        {
            int Count = Reader.ReadUInt16();
            List<int> Values = new(Count);
            for (int Index = 0; Index < Count; Index++)
                Values.Add(Reader.ReadInt32());
            return Values;
        }

        /// <summary>
        /// The host's pools arrived.
        /// </summary>
        public static void OnRelay(BinaryReader Reader)
        {
            Pools Value = new()
            {
                Weapons = ReadList(Reader).Select(Item => (EquipmentType)Item).ToList(),
                Curses = ReadList(Reader).Select(Item => (EquipmentType)Item).ToList(),
                Cards = ReadList(Reader).Select(Item => (TarotCards.Card)Item).ToList(),
                Relics = ReadList(Reader).Select(Item => (RelicType)Item).ToList(),
                DungeonRun = Reader.ReadInt32(),
                OnboardedRelics = Reader.ReadBoolean(),
                EnabledSpells = Reader.ReadBoolean()
            };

            if (Plugin.GlobalsInternal.IsServerCreator || Depth > 0)
                return;
            if (Received++ == 0)
                Plugin.Logger?.LogInfo($"[COOP] The host's reward pools: {Value.Weapons.Count} weapons, {Value.Curses.Count} curses, {Value.Cards.Count} cards, {Value.Relics.Count} relics");
            Host = Value;
        }

        /* JOINING PLAYER *****************************************************/

        private static bool Applies =>
            Host != null && CoopSync.InSession && !Plugin.GlobalsInternal.IsServerCreator && DataManager.Instance != null &&
            BiomeGenerator.Instance != null;

        private static void EnterScope()
        {
            if (Depth++ > 0 || !Applies)
                return;
            try
            {
                DataManager Data = DataManager.Instance;
                Saved = new Pools
                {
                    Weapons = Data.WeaponPool,
                    Curses = Data.CursePool,
                    Cards = Data.PlayerFoundTrinkets,
                    Relics = Data.PlayerFoundRelics,
                    DungeonRun = Data.dungeonRun,
                    OnboardedRelics = Data.OnboardedRelics,
                    EnabledSpells = Data.EnabledSpells
                };

                Data.WeaponPool = new List<EquipmentType>(Host.Weapons);
                Data.CursePool = new List<EquipmentType>(Host.Curses);
                Data.PlayerFoundTrinkets = new List<TarotCards.Card>(Host.Cards);
                Data.PlayerFoundRelics = new List<RelicType>(Host.Relics);
                Data.dungeonRun = Host.DungeonRun;
                Data.OnboardedRelics = Host.OnboardedRelics;
                Data.EnabledSpells = Host.EnabledSpells;

                /* The game adds to the end of these lists, so what was added is what lies past these */
                WeaponsBefore = Data.WeaponPool.Count;
                CursesBefore = Data.CursePool.Count;
                CardsBefore = Data.PlayerFoundTrinkets.Count;
                RelicsBefore = Data.PlayerFoundRelics.Count;
                ++Scopes;
            }
            catch (Exception e)
            {
                PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Using the host's pools failed: {e.Message}");
                Saved = null;
            }
        }

        private static void KeepAdded<T>(List<T> During, int Before, List<T> Ours)
        {
            for (int Index = Before; Index < During.Count; Index++)
            {
                if (!Ours.Contains(During[Index]))
                    Ours.Add(During[Index]);
            }
        }

        private static Exception ExitScope(Exception __exception)
        {
            if (Depth > 0)
                --Depth;
            if (Depth > 0 || Saved == null)
                return __exception;

            try
            {
                DataManager Data = DataManager.Instance;

                /* Whatever was added during the roll is ours to keep */
                KeepAdded(Data.WeaponPool, WeaponsBefore, Saved.Weapons);
                KeepAdded(Data.CursePool, CursesBefore, Saved.Curses);
                KeepAdded(Data.PlayerFoundTrinkets, CardsBefore, Saved.Cards);
                KeepAdded(Data.PlayerFoundRelics, RelicsBefore, Saved.Relics);

                Data.WeaponPool = Saved.Weapons;
                Data.CursePool = Saved.Curses;
                Data.PlayerFoundTrinkets = Saved.Cards;
                Data.PlayerFoundRelics = Saved.Relics;
                Data.dungeonRun = Saved.DungeonRun;
                Data.OnboardedRelics = Saved.OnboardedRelics;
                Data.EnabledSpells = Saved.EnabledSpells;
            }
            catch (Exception e)
            {
                PrintLogger.Print(DebugLevel.ERROR_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Putting our own pools back failed: {e}");
            }
            finally
            {
                Saved = null;
            }
            return __exception;
        }
    }
}

/* EOF */
