/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Checking that both cults are the same, and fixing what isn't from its owner
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP.Debug;
using HarmonyLib;
using MMTools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <remarks>
    /// The check, on top of sending what changed (as checksums in lockstep games and anti-entropy in
    /// databases): every fourth look at the cult, the owner of a part of it sends one hash per group
    /// (followers, structures, the cult's data, the inventory) of what it just sent. The other side
    /// hashes its own, a few objects a frame; a group that differs gets the owner's list of hashes per
    /// object; an object that differs two checks in a row (not a change still on its way) is asked
    /// for, and the owner sends it whole, or says it's gone. The host owns the cult, the friend the
    /// followers in their hands (see <see cref="CultHands"/>). Every fix is logged with what differed.
    /// </remarks>
    internal static partial class CultSync
    {
        private enum Group : byte
        {
            Followers = 0,
            Structures = 1,
            Data = 2,
            Inventory = 3
        }

        private enum Why : byte
        {
            /* Both have it, it isn't the same */
            Differs = 0,
            /* The owner has it, we don't */
            MissingHere = 1,
            /* We have it, the owner doesn't (or doesn't think it's its to run) */
            ExtraHere = 2
        }

        /* Every this many looks at the cult (even, so a check's look is a full one) */
        private const int CheckEvery = 4;

        /* Time a frame may spend hashing our side, in milliseconds */
        private const double FrameBudget = 2.0;

        private static int Passes, Round;

        /* This look compares everything: every other one, the rest only followers (less garbage) */
        private static bool Full;

        /* The owner's side: hashes of what this look sent, and the lists of the last check sent */
        private static bool Checking;
        private static readonly Dictionary<Group, SortedDictionary<string, ulong>> Recording = new();
        private static int SentRound = -1;
        private static Dictionary<Group, SortedDictionary<string, ulong>> SentLists;

        /* The other side: our hashes for the owner's last check, and the check each key last differed in */
        private static int TheirRound = -1;
        private static Dictionary<Group, SortedDictionary<string, ulong>> Mine;
        private static readonly Dictionary<string, int> Strikes = new();

        /* Counters for the log and the test harness report */
        internal static int ChecksSent, ChecksTaken, GroupsDiffered, FixesAsked, FixesSent, FixesTaken;

        /* Time spent on the cult's sync: this minute, and the worst frame */
        private static long LoadTicks, WorstTicks, LastBytes;
        private static int LastCollections;
        internal static double LoadMsPerSecond, WorstFrameMs, KBPerSecond;

        private static void ResetCheck()
        {
            Passes = 0;
            Checking = false;
            Recording.Clear();
            SentRound = TheirRound = -1;
            SentLists = Mine = null;
            Strikes.Clear();
        }

        /* HASHES *************************************************************/

        private static ulong Hash(byte[] Data)
        {
            ulong Value = 14695981039346656037UL;
            foreach (byte Byte in Data)
            {
                Value ^= Byte;
                Value *= 1099511628211UL;
            }
            return Value;
        }

        private static ulong Mix(ulong Value, string Key, ulong Item)
        {
            foreach (char Char in Key)
            {
                Value ^= Char;
                Value *= 1099511628211UL;
            }
            for (int Shift = 0; Shift < 64; Shift += 8)
            {
                Value ^= (Item >> Shift) & 0xFF;
                Value *= 1099511628211UL;
            }
            return Value;
        }

        private static ulong GroupHash(SortedDictionary<string, ulong> List)
        {
            ulong Value = 14695981039346656037UL;
            foreach (var Pair in List)
                Value = Mix(Value, Pair.Key, Pair.Value);
            return Value;
        }

        /* Made when first needed: the partial class's fields in the other file may not be set up yet */
        private static List<MemberInfo> FollowerOwnMade;

        private static List<MemberInfo> FollowerOwn => FollowerOwnMade ??= OwnMembers
            .Select(Name => (MemberInfo)AccessTools.Field(typeof(FollowerInfo), Name) ?? AccessTools.Property(typeof(FollowerInfo), Name))
            .Where(Member => Member != null).ToList();

        /// <summary>
        /// A follower as both sides should have it: without where it stands (each side's own)
        /// </summary>
        private static ulong FollowerHash(FollowerInfo Info)
        {
            object[] Kept = FollowerOwn.Select(Member => Get(Member, Info)).ToArray();
            try
            {
                foreach (MemberInfo Member in FollowerOwn)
                {
                    Type Of = MemberType(Member);
                    Set(Member, Info, Of.IsValueType ? Activator.CreateInstance(Of) : null);
                }
                return Hash(HostSave.Pack(typeof(FollowerInfo), Info));
            }
            finally
            {
                for (int Index = 0; Index < FollowerOwn.Count; Index++)
                    Set(FollowerOwn[Index], Info, Kept[Index]);
            }
        }

        private static ulong InventoryHash()
        {
            ulong Value = 14695981039346656037UL;
            foreach (var Pair in Inventory().Where(Pair => Pair.Value != 0).OrderBy(Pair => Pair.Key))
                Value = Mix(Value, Pair.Key.ToString(), (ulong)Pair.Value);
            return Value;
        }

        /// <summary>
        /// Whose a follower is to check: the friend's while in the friend's hands, else the host's
        /// </summary>
        private static bool FriendsToCheck(int Id) => CultHands.FriendRuns(Id);

        /* THE OWNER'S SIDE ***************************************************/

        /// <summary>
        /// A look at the cult starts: every fourth one in the cult records what it sends
        /// </summary>
        private static void BeginPass()
        {
            Recording.Clear();
            Full = ++Passes % 2 == 0;
            Checking = Passes % CheckEvery == 0 && SceneManager.GetActiveScene().name == BaseScene;
        }

        private static void Record(Group Of, string Key, ulong Value)
        {
            if (!Recording.TryGetValue(Of, out var List))
                Recording[Of] = List = new SortedDictionary<string, ulong>(StringComparer.Ordinal);
            List[Key] = Value;
        }

        /// <summary>
        /// What this look sent, for an object of ours
        /// </summary>
        private static void RecordObject(Item Kind, int Id, object Value, byte[] Whole)
        {
            if (!Checking)
                return;
            if (Kind == Item.Follower)
            {
                if (FriendsToCheck(Id) == Mirror)
                    Record(Group.Followers, Key(Kind, Id), FollowerHash((FollowerInfo)Value));
            }
            else if (Hosting)
            {
                Record(Group.Structures, Key(Kind, Id), Hash(Whole));
            }
        }

        private static void RecordData(int Index, byte[] Packed)
        {
            if (Checking && Hosting)
                Record(Group.Data, $"D{Index}", Hash(Packed));
        }

        /// <summary>
        /// The look is done and what changed is sent: the hashes of it go after, in the same stream
        /// </summary>
        private static void EndPass(bool Back)
        {
            if (!Checking || Back || MMTransition.IsPlaying)
                return;
            Checking = false;
            if (Hosting)
            {
                Record(Group.Inventory, "I", InventoryHash());
                foreach (Group Of in new[] { Group.Followers, Group.Structures, Group.Data })
                {
                    if (!Recording.ContainsKey(Of))
                        Recording[Of] = new SortedDictionary<string, ulong>(StringComparer.Ordinal);
                }
            }
            if (Recording.Count == 0)
                return;

            SentRound = ++Round;
            SentLists = Recording.ToDictionary(Pair => Pair.Key, Pair => Pair.Value);
            Recording.Clear();
            ++ChecksSent;
            CoopSync.Send(CoopSync.Kind.CultCheck, Writer =>
            {
                Writer.Write(SentRound);
                Writer.Write((byte)SentLists.Count);
                foreach (var Pair in SentLists)
                {
                    Writer.Write((byte)Pair.Key);
                    Writer.Write(GroupHash(Pair.Value));
                }
            });
        }

        internal static void OnCheckAsk(BinaryReader Reader)
        {
            int Asked = Reader.ReadInt32();
            int Count = Reader.ReadByte();
            List<Group> Groups = new();
            for (int Index = 0; Index < Count; Index++)
                Groups.Add((Group)Reader.ReadByte());
            if (Asked != SentRound || SentLists == null)
                return;
            CoopSync.Send(CoopSync.Kind.CultCheckList, Writer =>
            {
                Writer.Write(Asked);
                Writer.Write((byte)Groups.Count);
                foreach (Group Of in Groups)
                {
                    SortedDictionary<string, ulong> List = SentLists.TryGetValue(Of, out var Found) ? Found : new SortedDictionary<string, ulong>();
                    Writer.Write((byte)Of);
                    Writer.Write(List.Count);
                    foreach (var Pair in List)
                    {
                        Writer.Write(Pair.Key);
                        Writer.Write(Pair.Value);
                    }
                }
            });
        }

        /// <summary>
        /// The other side asks for what differs there: ours goes whole, or word that it's gone
        /// </summary>
        internal static void OnFixAsk(BinaryReader Reader)
        {
            int Count = Reader.ReadUInt16();
            List<(string, Why)> Asked = new();
            for (int Index = 0; Index < Count; Index++)
                Asked.Add((Reader.ReadString(), (Why)Reader.ReadByte()));
            if (!Ready || DataManager.Instance == null)
                return;

            List<(string, byte[])> Data = new();
            List<MemberInfo> DataList = DataMembers();
            foreach (var (Name, Reason) in Asked)
            {
                try
                {
                    ++FixesSent;
                    if (Name == "I")
                    {
                        LastInventory = null;
                        ScanInventory();
                    }
                    else if (Name[0] == 'D')
                    {
                        int Index = int.Parse(Name.Substring(1));
                        if (Index >= 0 && Index < DataList.Count)
                            Data.Add((DataList[Index].Name, HostSave.Pack(MemberType(DataList[Index]), Get(DataList[Index], DataManager.Instance))));
                    }
                    else
                    {
                        FixObject(Name[0] == 'F' ? Item.Follower : Item.Structure, int.Parse(Name.Substring(1)), Reason);
                    }
                }
                catch (Exception e)
                {
                    PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Fixing {Name} for the partner failed: {e.Message}");
                }
            }
            SendMembers(Item.Data, 0, Data, Op.Fix);
        }

        private static void FixObject(Item Kind, int Id, Why Reason)
        {
            object Value = Find(Kind, Id);
            if (Value == null)
            {
                Plugin.Logger?.LogInfo($"[COOP] Sync check: the partner still has {Key(Kind, Id)}, gone here; telling them");
                Agreed.Remove(Key(Kind, Id));
                CoopSync.Send(CoopSync.Kind.CultData, Writer =>
                {
                    Writer.Write((byte)Op.Gone);
                    Writer.Write((byte)Kind);
                    Writer.Write(Id);
                });
                return;
            }

            /* A follower whose hands both sides don't agree on: the one running it says so again */
            if (Kind == Item.Follower)
                CultHands.Settle(Id, Reason == Why.ExtraHere);

            if (Reason == Why.MissingHere)
            {
                SendWhole(Kind, Id, HostSave.Pack(TypeOf(Kind), Value));
                return;
            }
            SendMembers(Kind, Id, Members(TypeOf(Kind)).Select(Member => (Member.Name, HostSave.Pack(MemberType(Member), Get(Member, Value)))).ToList(), Op.Fix);
        }

        /* THE OTHER SIDE *****************************************************/

        internal static void OnCheck(BinaryReader Reader)
        {
            int Their = Reader.ReadInt32();
            int Count = Reader.ReadByte();
            Dictionary<Group, ulong> Hashes = new();
            for (int Index = 0; Index < Count; Index++)
                Hashes[(Group)Reader.ReadByte()] = Reader.ReadUInt64();
            if (!Ready || !(Mirror || Hosting) || DataManager.Instance == null || SceneManager.GetActiveScene().name != BaseScene || MMTransition.IsPlaying)
                return;
            ++ChecksTaken;
            Plugin.MonoInstance.StartCoroutine(HashOurs(Their, Hashes));
        }

        /// <summary>
        /// Our side of the owner's groups, a few milliseconds a frame
        /// </summary>
        private static IEnumerator HashOurs(int Their, Dictionary<Group, ulong> Hashes)
        {
            bool OwnerIsFriend = Plugin.GlobalsInternal.IsServerCreator;
            Dictionary<Group, SortedDictionary<string, ulong>> Ours = new();
            long Slice = Stopwatch.GetTimestamp();

            foreach (Group Of in Hashes.Keys)
            {
                SortedDictionary<string, ulong> List = new(StringComparer.Ordinal);
                Ours[Of] = List;
                List<(string, Func<ulong>)> Work = new();
                switch (Of)
                {
                    case Group.Followers:
                        foreach (FollowerInfo Info in DataManager.Instance.Followers.ToList())
                        {
                            if (Info != null && FriendsToCheck(Info.ID) == OwnerIsFriend)
                                Work.Add((Key(Item.Follower, Info.ID), () => FollowerHash(Info)));
                        }
                        break;
                    case Group.Structures:
                        foreach (StructuresData Data in StructureManager.StructuresDataAtLocation(FollowerLocation.Base).ToList())
                        {
                            if (Data != null)
                                Work.Add((Key(Item.Structure, Data.ID), () => Hash(HostSave.Pack(typeof(StructuresData), Data))));
                        }
                        break;
                    case Group.Data:
                        List<MemberInfo> DataList = DataMembers();
                        for (int Index = 0; Index < DataList.Count; Index++)
                        {
                            MemberInfo Member = DataList[Index];
                            Work.Add(($"D{Index}", () => Hash(DataBytes(Member, false))));
                        }
                        break;
                    case Group.Inventory:
                        Work.Add(("I", InventoryHash));
                        break;
                }

                foreach (var (Name, Make) in Work)
                {
                    if (!Ready || DataManager.Instance == null)
                        yield break;
                    Doing = $"checking {Name}";
                    try
                    {
                        List[Name] = Make();
                    }
                    catch (Exception e)
                    {
                        PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Checking {Name} failed: {e.Message}");
                    }
                    if (Elapsed(Slice) > FrameBudget)
                    {
                        Spent(Slice);
                        yield return null;
                        Slice = Stopwatch.GetTimestamp();
                    }
                }
            }
            Spent(Slice);

            List<Group> Ask = new();
            foreach (var Pair in Hashes)
            {
                if (GroupHash(Ours[Pair.Key]) == Pair.Value)
                    continue;
                ++GroupsDiffered;
                if (Pair.Key == Group.Inventory)
                    Strike(Their, new List<(string, Why)> { ("I", Why.Differs) });
                else
                    Ask.Add(Pair.Key);
            }
            TheirRound = Their;
            Mine = Ours;
            if (Ask.Count == 0)
                yield break;
            CoopSync.Send(CoopSync.Kind.CultCheckAsk, Writer =>
            {
                Writer.Write(Their);
                Writer.Write((byte)Ask.Count);
                foreach (Group Of in Ask)
                    Writer.Write((byte)Of);
            });
        }

        internal static void OnCheckList(BinaryReader Reader)
        {
            int Their = Reader.ReadInt32();
            int Count = Reader.ReadByte();
            List<(string, Why)> Differ = new();
            for (int Index = 0; Index < Count; Index++)
            {
                Group Of = (Group)Reader.ReadByte();
                int Items = Reader.ReadInt32();
                Dictionary<string, ulong> Theirs = new(Items);
                for (int Item = 0; Item < Items; Item++)
                    Theirs[Reader.ReadString()] = Reader.ReadUInt64();
                if (Their != TheirRound || Mine == null || !Mine.TryGetValue(Of, out var Ours))
                    continue;
                foreach (var Pair in Theirs)
                {
                    if (!Ours.TryGetValue(Pair.Key, out ulong Value))
                        Differ.Add((Pair.Key, Why.MissingHere));
                    else if (Value != Pair.Value)
                        Differ.Add((Pair.Key, Why.Differs));
                }
                foreach (string Name in Ours.Keys)
                {
                    if (!Theirs.ContainsKey(Name))
                        Differ.Add((Name, Why.ExtraHere));
                }
            }
            if (Ready && Differ.Count > 0)
                Strike(Their, Differ);
        }

        /// <summary>
        /// What differs two checks in a row isn't on its way: it's asked for
        /// </summary>
        private static void Strike(int Their, List<(string, Why)> Differ)
        {
            List<(string, Why)> Ask = new();
            foreach (var (Name, Reason) in Differ)
            {
                if (Strikes.TryGetValue(Name, out int Last) && Last == Their - 1)
                {
                    Ask.Add((Name, Reason));
                    Strikes.Remove(Name);
                }
                else
                {
                    Strikes[Name] = Their;
                }
            }
            if (Ask.Count == 0)
                return;
            FixesAsked += Ask.Count;
            string Owner = Plugin.GlobalsInternal.IsServerCreator ? "friend" : "host";
            Plugin.Logger?.LogInfo($"[COOP] Sync check {Their}: {string.Join(", ", Ask.Select(Pair => $"{Pair.Item1} {Pair.Item2}"))} two checks in a row, asking the {Owner}");
            CoopSync.Send(CoopSync.Kind.CultFixAsk, Writer =>
            {
                Writer.Write((ushort)Ask.Count);
                foreach (var (Name, Reason) in Ask)
                {
                    Writer.Write(Name);
                    Writer.Write((byte)Reason);
                }
            });
        }

        /* LOAD ***************************************************************/

        private static double Elapsed(long Since) => (Stopwatch.GetTimestamp() - Since) * 1000.0 / Stopwatch.Frequency;

        /* What the sync work was at last, for a slow frame in the log, and the garbage collections so far */
        private static string Doing = "";
        private static int Collections;

        /// <summary>
        /// A frame's slice of the sync work is done
        /// </summary>
        private static void Spent(long Since)
        {
            long Ticks = Stopwatch.GetTimestamp() - Since;
            LoadTicks += Ticks;
            if (Ticks > WorstTicks)
                WorstTicks = Ticks;
            double Ms = Ticks * 1000.0 / Stopwatch.Frequency;
            int Collected = GC.CollectionCount(0);
            if (Ms > 50.0)
                Plugin.Logger?.LogInfo($"[COOP] Cult sync: a slow frame, {Ms:F0} ms, last at {Doing}{(Collected != Collections ? ", with a garbage collection" : "")}");
            Collections = Collected;
        }

        /// <summary>
        /// Once a minute, in the log: what the sync cost
        /// </summary>
        private static IEnumerator LogLoad()
        {
            var Wait = new WaitForSecondsRealtime(60f);
            while (true)
            {
                yield return Wait;
                LoadMsPerSecond = LoadTicks * 1000.0 / Stopwatch.Frequency / 60.0;
                WorstFrameMs = WorstTicks * 1000.0 / Stopwatch.Frequency;
                KBPerSecond = (CoopSync.CultBytes - LastBytes) / 1024.0 / 60.0;
                int Collected = GC.CollectionCount(0) - LastCollections;
                LoadTicks = WorstTicks = 0;
                LastBytes = CoopSync.CultBytes;
                LastCollections = GC.CollectionCount(0);
                if (Ready && (Mirror || Hosting))
                    Plugin.Logger?.LogInfo($"[COOP] Cult sync, last minute: {LoadMsPerSecond:F2} ms a second, worst frame {WorstFrameMs:F2} ms, {KBPerSecond:F2} KB/s sent, {Collected} garbage collections in the game; " +
                                           $"checks {ChecksSent} sent, {ChecksTaken} taken, {GroupsDiffered} groups differed, {FixesAsked} fixes asked, {FixesSent} sent, {FixesTaken} taken");
            }
        }

        /// <summary>
        /// The test harness: takes the cult as it is as agreed, as if what changed got lost on the way
        /// </summary>
        internal static void LoseChangesForTest()
        {
            Agreed.Clear();
            DataLast.Clear();
            Baseline();
        }
    }
}

/* EOF */
