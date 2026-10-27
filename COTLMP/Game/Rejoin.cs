/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     A friend who comes back goes to the host, with the hearts and gear they had
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP.Network;
using COTLMP.Ui;
using MMBiomeGeneration;
using MMTools;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// Coming back to the host's run.
    /// </summary>
    /// <remarks>
    /// In a run the friend tells the host every 2 s how they stand: hearts of every kind, weapon,
    /// curse, relic, tarot cards, where and at which spot. The host keeps the last of it for that
    /// Steam account, so it is there however the friend went: left on purpose, lost the connection
    /// or the game closed.
    ///
    /// A friend who joins (again) while the host is in a dungeon run is taken to the host behind a
    /// black screen ("Going back to the host"): into the host's run (the same one, as a friend who
    /// follows through the door within 10 minutes gets), floor after floor along the way the host
    /// picked on the run's maps, into the host's room; then their hearts and gear are put back,
    /// at their spot if it was this room, else next to the host. A friend the host knows nothing of
    /// in this run (new, or the run is another one) gets the host's weapon and curse, as the game's
    /// co-op gives its second player.
    ///
    /// Each friend is a character of their own in the host's save, by Steam account: their extra
    /// hearts, their chores' level and experience (the mop), whether they ate today. The friend's
    /// lamb reads these from the Lamb's fields of its copy of the host's save (which is never written
    /// and doesn't send them to the host's cult), so they are filled with this friend's character as
    /// the save arrives: a friend the save has never seen starts from nothing. The friend tells the
    /// host their character every 5 s; the host keeps every friend's (and the run they were in) in
    /// saves\coop_players_SLOT.dat next to its save, written whenever the game saves, so a later game
    /// on this save goes on with any of them.
    /// </remarks>
    internal static class Rejoin
    {
        /* How a player stands in a run */
        private sealed class Standing
        {
            internal ulong Steam;
            internal int Run;
            internal string Place = "";
            internal Vector3 At;
            internal float[] Hearts = new float[8];
            internal int Weapon, WeaponLevel, Curse, CurseLevel, Relic;
            internal List<(int Card, int Upgrade)> Cards = new();
            internal float Time;

            internal void Write(BinaryWriter Writer)
            {
                Writer.Write(Steam);
                Writer.Write(Run);
                Writer.Write(Place ?? "");
                Writer.Write(At.x);
                Writer.Write(At.y);
                Writer.Write(At.z);
                foreach (float Value in Hearts)
                    Writer.Write(Value);
                Writer.Write(Weapon);
                Writer.Write(WeaponLevel);
                Writer.Write(Curse);
                Writer.Write(CurseLevel);
                Writer.Write(Relic);
                Writer.Write((byte)Cards.Count);
                foreach (var (Card, Upgrade) in Cards)
                {
                    Writer.Write(Card);
                    Writer.Write(Upgrade);
                }
            }

            internal static Standing Read(BinaryReader Reader)
            {
                Standing Item = new()
                {
                    Steam = Reader.ReadUInt64(),
                    Run = Reader.ReadInt32(),
                    Place = Reader.ReadString(),
                    At = new Vector3(Reader.ReadSingle(), Reader.ReadSingle(), Reader.ReadSingle())
                };
                for (int Index = 0; Index < Item.Hearts.Length; Index++)
                    Item.Hearts[Index] = Reader.ReadSingle();
                Item.Weapon = Reader.ReadInt32();
                Item.WeaponLevel = Reader.ReadInt32();
                Item.Curse = Reader.ReadInt32();
                Item.CurseLevel = Reader.ReadInt32();
                Item.Relic = Reader.ReadInt32();
                int Count = Reader.ReadByte();
                for (int Index = 0; Index < Count; Index++)
                    Item.Cards.Add((Reader.ReadInt32(), Reader.ReadInt32()));
                return Item;
            }

            /// <summary>
            /// Our own lamb, now
            /// </summary>
            internal static Standing Ours()
            {
                PlayerFarming Player = PlayerFarming.Instance;
                if (Player == null)
                    return null;
                Standing Item = new()
                {
                    Steam = SteamBridge.LocalId,
                    Run = CoopSync.CurrentRun,
                    Place = CoopSync.CurrentPlace() ?? "",
                    At = Player.transform.position,
                    Weapon = (int)Player.currentWeapon,
                    WeaponLevel = Player.currentWeaponLevel,
                    Curse = (int)Player.currentCurse,
                    CurseLevel = Player.currentCurseLevel,
                    Relic = (int)Player.currentRelicType
                };
                using (MemoryStream Stream = new())
                using (BinaryWriter Writer = new(Stream))
                {
                    CoopHud.WriteHearts(Writer);
                    Writer.Flush();
                    Stream.Position = 0;
                    Item.Hearts = CoopHud.ReadHearts(new BinaryReader(Stream));
                }
                foreach (TarotCards.TarotCard Card in Player.RunTrinkets)
                    Item.Cards.Add(((int)Card.CardType, Card.UpgradeIndex));
                return Item;
            }
        }

        /* The host: what each friend had when last heard of, by Steam account */
        private static readonly Dictionary<ulong, Standing> Kept = new();

        /* A friend's own character in the host's save: these fields of the save, the Lamb's on the friend's lamb */
        private static readonly string[] CharacterFields =
        {
            "PLAYER_BLUE_HEARTS", "PLAYER_BLACK_HEARTS", "PLAYER_FIRE_HEARTS", "PLAYER_ICE_HEARTS", "PLAYER_REMOVED_HEARTS",
            "PLAYER_SPIRIT_HEARTS", "PLAYER_SPIRIT_TOTAL_HEARTS", "ChoreXP", "ChoreXPLevel", "PlayerEaten"
        };

        private sealed class Character
        {
            internal ulong Steam;
            internal string Name = "";
            internal int Day;
            internal Dictionary<string, float> Values = new();
        }

        /* The host: every friend's character, by Steam account, as kept with this save */
        private static readonly Dictionary<ulong, Character> Characters = new();
        private static int CharactersSlot = -1;

        /// <summary>
        /// We are on our way to the host: the partner doesn't hear where we are until we are there
        /// </summary>
        internal static bool Going { get; private set; }

        /* Counters for the test harness report */
        internal static int KeepsSent, KeepsTaken, Restores, Arrived, GearPut, CharactersSent, CharactersTaken, CharactersPut;

        private static bool IsHost => Plugin.GlobalsInternal.IsServerCreator;

        private static string T(string Key) => I2.Loc.LocalizationManager.GetTranslation(Key);

        public static void Initialize()
        {
            Plugin.MonoInstance.StartCoroutine(KeepTelling());
            Plugin.MonoInstance.StartCoroutine(SayBack());
            Plugin.MonoInstance.StartCoroutine(TellCharacter());
            SaveAndLoad.OnLoadComplete += ReadCharacters;
            SaveAndLoad.OnSaveCompleted += WriteCharacters;
        }

        /* CHARACTERS *********************************************************/

        private static float Get(string Field)
        {
            object Value = AccessTools_Field(Field)?.GetValue(DataManager.Instance);
            return Value switch
            {
                float F => F,
                int I => I,
                bool B => B ? 1f : 0f,
                _ => 0f
            };
        }

        private static void Set(string Field, float Value)
        {
            System.Reflection.FieldInfo Info = AccessTools_Field(Field);
            if (Info == null)
                return;
            if (Info.FieldType == typeof(float))
                Info.SetValue(DataManager.Instance, Value);
            else if (Info.FieldType == typeof(int))
                Info.SetValue(DataManager.Instance, (int)Value);
            else if (Info.FieldType == typeof(bool))
                Info.SetValue(DataManager.Instance, Value != 0f);
        }

        private static System.Reflection.FieldInfo AccessTools_Field(string Field) => HarmonyLib.AccessTools.Field(typeof(DataManager), Field);

        /// <summary>
        /// The friend: the host's save has just come; our lamb is a character of its own, a new one until the host says which
        /// </summary>
        internal static void OnHostSave()
        {
            foreach (string Field in CharacterFields)
                Set(Field, 0f);
            Plugin.Logger?.LogInfo("[COOP] Our character on the host's save starts as a new one until the host tells us ours");
        }

        /// <summary>
        /// The friend: our character, every 5 s, for the host to keep
        /// </summary>
        private static IEnumerator TellCharacter()
        {
            WaitForSecondsRealtime Wait = new(5f);
            while (true)
            {
                yield return Wait;
                if (IsHost || !CoopSync.InSession || !HostSave.Playing || DataManager.Instance == null || MMTransition.IsPlaying)
                    continue;
                try
                {
                    ulong Me = SteamBridge.LocalId;
                    if (Me == 0)
                        continue;
                    CoopSync.Send(CoopSync.Kind.RejoinCharacter, Writer =>
                    {
                        Writer.Write(Me);
                        Writer.Write(SteamBridge.LocalName() ?? "");
                        Writer.Write(TimeManager.CurrentDay);
                        Writer.Write((byte)CharacterFields.Length);
                        foreach (string Field in CharacterFields)
                        {
                            Writer.Write(Field);
                            Writer.Write(Get(Field));
                        }
                    });
                    ++CharactersSent;
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogWarning($"[COOP] Can't tell the host our character: {e.Message}");
                }
            }
        }

        private static Character ReadCharacter(BinaryReader Reader)
        {
            Character Item = new() { Steam = Reader.ReadUInt64(), Name = Reader.ReadString(), Day = Reader.ReadInt32() };
            int Count = Reader.ReadByte();
            for (int Index = 0; Index < Count; Index++)
                Item.Values[Reader.ReadString()] = Reader.ReadSingle();
            return Item;
        }

        /// <summary>
        /// The friend: the host says which character we are (none: a new one)
        /// </summary>
        private static void OnYou(BinaryReader Reader)
        {
            if (IsHost)
                return;
            Character Ours = Reader.ReadBoolean() ? ReadCharacter(Reader) : null;
            foreach (string Field in CharacterFields)
                Set(Field, Ours != null && Ours.Values.TryGetValue(Field, out float Value) ? Value : 0f);
            ++CharactersPut;
            try
            {
                if (PlayerFarming.Instance?.health != null && !GameManager.IsDungeon(PlayerFarming.Location))
                {
                    PlayerFarming.Instance.health.InitHP();
                    CoopHud.Rebuild();
                }
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Can't count our hearts for our character: {e.Message}");
            }
            Plugin.Logger?.LogInfo(Ours == null ? "[COOP] The host's save has no character of ours: a new one" :
                $"[COOP] Our character on the host's save: chores level {Get("ChoreXPLevel")} ({Get("ChoreXP")} xp), blue hearts {Get("PLAYER_BLUE_HEARTS")}, ate today {Get("PlayerEaten") != 0f}");
        }

        private static string CharactersFile(int Slot) => System.IO.Path.Combine(Application.persistentDataPath, "saves", $"coop_players_{Slot}.dat");

        /// <summary>
        /// The host's save loaded: the friends' characters kept with it
        /// </summary>
        private static void ReadCharacters()
        {
            try
            {
                if (HostSave.Playing)
                    return;
                Characters.Clear();
                Kept.Clear();
                CharactersSlot = SaveAndLoad.SAVE_SLOT;
                string File = CharactersFile(CharactersSlot);
                if (!System.IO.File.Exists(File))
                    return;
                JObject All = JObject.Parse(System.IO.File.ReadAllText(File));
                foreach (JObject Each in (All["players"] as JArray ?? new JArray()).OfType<JObject>())
                {
                    Character Item = new() { Steam = (ulong)Each["steam"], Name = (string)Each["name"] ?? "", Day = (int?)Each["day"] ?? 0 };
                    foreach (JProperty Value in (Each["values"] as JObject ?? new JObject()).Properties())
                        Item.Values[Value.Name] = (float)Value.Value;
                    Characters[Item.Steam] = Item;
                    if (Each["run"] is JValue Run && Run.Type == JTokenType.String)
                    {
                        Standing Last = Standing.Read(new BinaryReader(new MemoryStream(Convert.FromBase64String((string)Run))));
                        Kept[Last.Steam] = Last;
                    }
                }
                Plugin.Logger?.LogInfo($"[COOP] {Characters.Count} friends' characters kept with save slot {CharactersSlot}");
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Can't read the friends' characters: {e.Message}");
                /* Kept aside as it is: the next save would write a new file over it */
                try
                {
                    string File = CharactersFile(CharactersSlot);
                    if (System.IO.File.Exists(File))
                        System.IO.File.Move(File, $"{File}.bad-{DateTime.Now:yyyyMMdd-HHmmss}");
                    Characters.Clear();
                    Kept.Clear();
                }
                catch (Exception Moving)
                {
                    Plugin.Logger?.LogWarning($"[COOP] Can't keep the unreadable characters file aside: {Moving.Message}");
                }
            }
        }

        /// <summary>
        /// The host's game saved: the friends' characters are written with it
        /// </summary>
        private static void WriteCharacters()
        {
            try
            {
                if (HostSave.Playing || Characters.Count == 0 || CharactersSlot != SaveAndLoad.SAVE_SLOT)
                    return;
                JArray Players = new();
                foreach (Character Item in Characters.Values)
                {
                    JObject Values = new();
                    foreach (var Pair in Item.Values)
                        Values[Pair.Key] = Pair.Value;
                    JObject Each = new() { ["steam"] = Item.Steam, ["name"] = Item.Name, ["day"] = Item.Day, ["values"] = Values };
                    if (Kept.TryGetValue(Item.Steam, out Standing Last))
                    {
                        using MemoryStream Stream = new();
                        using BinaryWriter Writer = new(Stream);
                        Last.Write(Writer);
                        Writer.Flush();
                        Each["run"] = Convert.ToBase64String(Stream.ToArray());
                    }
                    Players.Add(Each);
                }
                string File = CharactersFile(CharactersSlot);
                System.IO.File.WriteAllText(File + ".part", new JObject { ["players"] = Players }.ToString());
                if (System.IO.File.Exists(File))
                    System.IO.File.Delete(File);
                System.IO.File.Move(File + ".part", File);
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Can't write the friends' characters: {e.Message}");
            }
        }

        /// <summary>
        /// The friend, in a run: how we stand, every 2 s
        /// </summary>
        private static IEnumerator KeepTelling()
        {
            WaitForSecondsRealtime Wait = new(2f);
            while (true)
            {
                yield return Wait;
                if (IsHost || Going || !CoopSync.InSession || !HostSave.Playing || CoopSync.CurrentRun == 0 || !GameManager.IsDungeon(PlayerFarming.Location) || MMTransition.IsPlaying)
                    continue;
                try
                {
                    Standing Now = Standing.Ours();
                    if (Now == null || Now.Steam == 0)
                        continue;
                    CoopSync.Send(CoopSync.Kind.RejoinKeep, Now.Write);
                    ++KeepsSent;
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogWarning($"[COOP] Can't tell the host how we stand: {e.Message}");
                }
            }
        }

        /// <summary>
        /// The friend, once on the host's save and in its cult: we are back (or new), says who we are
        /// </summary>
        private static IEnumerator SayBack()
        {
            bool Said = false;
            while (true)
            {
                yield return new WaitForSecondsRealtime(1f);
                if (!CoopSync.InSession || !HostSave.Playing)
                {
                    Said = false;
                    continue;
                }
                if (Said || IsHost || PlayerFarming.Instance == null || MMTransition.IsPlaying || GameManager.IsDungeon(PlayerFarming.Location))
                    continue;
                Said = true;
                ulong Me = SteamBridge.LocalId;
                Plugin.Logger?.LogInfo($"[COOP] In the host's cult as {Me}: asking where the host is");
                CoopSync.Send(CoopSync.Kind.RejoinBack, Writer => Writer.Write(Me));
            }
        }

        /// <summary>
        /// Rejoin data from the partner
        /// </summary>
        internal static void OnRelay(CoopSync.Kind Type, BinaryReader Reader)
        {
            switch (Type)
            {
                case CoopSync.Kind.RejoinKeep:
                {
                    Standing Item = Standing.Read(Reader);
                    Item.Time = Time.realtimeSinceStartup;
                    if (IsHost && Item.Steam != 0)
                    {
                        Kept[Item.Steam] = Item;
                        ++KeepsTaken;
                    }
                    break;
                }

                case CoopSync.Kind.RejoinBack:
                    OnBack(Reader.ReadUInt64());
                    break;

                case CoopSync.Kind.RejoinCharacter:
                {
                    Character Item = ReadCharacter(Reader);
                    if (IsHost && Item.Steam != 0)
                    {
                        Characters[Item.Steam] = Item;
                        if (CharactersSlot < 0)
                            CharactersSlot = SaveAndLoad.SAVE_SLOT;
                        ++CharactersTaken;
                    }
                    break;
                }

                case CoopSync.Kind.RejoinYou:
                    OnYou(Reader);
                    break;

                case CoopSync.Kind.RejoinRestore:
                {
                    int Run = Reader.ReadInt32();
                    int Location = Reader.ReadInt32();
                    string HostPlace = Reader.ReadString();
                    Standing Ours = Reader.ReadBoolean() ? Standing.Read(Reader) : null;
                    int Weapon = Reader.ReadInt32(), WeaponLevel = Reader.ReadInt32(), Curse = Reader.ReadInt32(), CurseLevel = Reader.ReadInt32();
                    if (Going || IsHost)
                        break;
                    ++Restores;
                    Plugin.MonoInstance.StartCoroutine(GoBack(Run, Location, HostPlace, Ours, Weapon, WeaponLevel, Curse, CurseLevel));
                    break;
                }
            }
        }

        /* HOST ***************************************************************/

        /// <summary>
        /// A friend is in our cult: if we are in a dungeon run, they come to us, with what they had in it
        /// </summary>
        private static void OnBack(ulong Steam)
        {
            if (!IsHost)
                return;

            /* Their character in our save first, so they come with it */
            Characters.TryGetValue(Steam, out Character Theirs0);
            Plugin.Logger?.LogInfo(Theirs0 == null ? $"[COOP] Friend {Steam} is new to this save: a new character" :
                $"[COOP] Friend {Steam} ({Theirs0.Name}) has a character in this save, sending it");
            CoopSync.Send(CoopSync.Kind.RejoinYou, Writer =>
            {
                Writer.Write(Theirs0 != null);
                if (Theirs0 == null)
                    return;
                Writer.Write(Theirs0.Steam);
                Writer.Write(Theirs0.Name ?? "");
                Writer.Write(Theirs0.Day);
                Writer.Write((byte)Theirs0.Values.Count);
                foreach (var Pair in Theirs0.Values)
                {
                    Writer.Write(Pair.Key);
                    Writer.Write(Pair.Value);
                }
            });

            string Here = CoopSync.CurrentPlace();
            PlayerFarming Player = PlayerFarming.Instance;
            if (CoopSync.CurrentRun == 0 || Here == null || !Here.Contains("#") || Player == null)
            {
                Plugin.Logger?.LogInfo($"[COOP] Friend {Steam} is in the cult, we aren't in a run: nothing to take them to");
                return;
            }
            Kept.TryGetValue(Steam, out Standing Theirs);
            if (Theirs != null && Theirs.Run != CoopSync.CurrentRun)
                Theirs = null;
            Plugin.Logger?.LogInfo($"[COOP] Friend {Steam} is back: taking them to {Here}" +
                                   (Theirs != null ? $", with what they had {Time.realtimeSinceStartup - Theirs.Time:F0} s ago in {Theirs.Place}" : ", nothing kept for them in this run"));
            CoopSync.Send(CoopSync.Kind.RejoinRestore, Writer =>
            {
                Writer.Write(CoopSync.CurrentRun);
                Writer.Write(CoopSync.CurrentRunLocation);
                Writer.Write(Here);
                Writer.Write(Theirs != null);
                Theirs?.Write(Writer);
                Writer.Write((int)Player.currentWeapon);
                Writer.Write(Player.currentWeaponLevel);
                Writer.Write((int)Player.currentCurse);
                Writer.Write(Player.currentCurseLevel);
            });
        }

        /* FRIEND *************************************************************/

        private static string Floor(string Place) => Place != null && Place.Contains("@") ? Place.Substring(0, Place.IndexOf('@')) : Place;

        private static Vector2Int? Room(string Place)
        {
            if (Place == null || !Place.Contains("@"))
                return null;
            string[] Parts = Place.Substring(Place.IndexOf('@') + 1).Split(',');
            return Parts.Length == 2 && int.TryParse(Parts[0], out int X) && int.TryParse(Parts[1], out int Y) ? new Vector2Int(X, Y) : null;
        }

        /// <summary>
        /// Where the host is now (they may go on while we come)
        /// </summary>
        private static string HostNow(string Told) => RoomSync.PartnerLastPlace != null && RoomSync.PartnerLastPlace.Contains("#") ? RoomSync.PartnerLastPlace : Told;

        private static IEnumerator GoBack(int Run, int Location, string HostPlace, Standing Ours, int Weapon, int WeaponLevel, int Curse, int CurseLevel)
        {
            Going = true;
            float Until = Time.realtimeSinceStartup + 180f;
            string Scene = HostPlace.Substring(0, HostPlace.IndexOf('#'));
            Plugin.Logger?.LogInfo($"[COOP] Going back to the host in {HostPlace} (run {Run}){(Ours != null ? $", our things from {Ours.Place}" : "")}");
            OnlineMenu.Wait("Multiplayer/Rejoin/Going");
            ConnectionQuality.SayBusy();
            PlayerFarming Player = PlayerFarming.Instance;
            bool WasInvincible = Player != null && Player.health.invincible;

            /* Into the host's run, as through the door after them */
            CoopSync.AdoptNext(Run, Location);
            GameManager.NewRun("", false, (FollowerLocation)Location);
            MMTransition.Play(MMTransition.TransitionType.ChangeRoomWaitToResume, MMTransition.Effect.BlackFade, Scene, 1f, "", null);
            while ((SceneManager.GetActiveScene().name != Scene || PlayerFarming.Instance == null || BiomeGenerator.Instance?.CurrentRoom == null) && Time.realtimeSinceStartup < Until)
                yield return null;
            yield return new WaitForSecondsRealtime(2f);
            MMTransition.CanResume = true;
            MMTransition.ResumePlay();
            Player = PlayerFarming.Instance;
            if (Player != null)
                Player.health.invincible = true;
            yield return new WaitForSecondsRealtime(1f);

            /* Floor after floor, the way the host went (their picks come in on the maps) */
            int Hops = 0;
            while (Floor(CoopSync.CurrentPlace()) != Floor(HostNow(HostPlace)) && Hops < 8 && Time.realtimeSinceStartup < Until)
            {
                /* The host left the run meanwhile: nobody to go to */
                if (RoomSync.PartnerLastPlace != null && !RoomSync.PartnerLastPlace.Contains("#"))
                {
                    Plugin.Logger?.LogWarning("[COOP] Going back: the host left the run on the way");
                    break;
                }
                ++Hops;
                yield return NextFloor();
            }

            /* Into the host's room */
            string Target = HostNow(HostPlace);
            Vector2Int? Spot = Room(Target);
            Going = false;
            BiomeRoom Here = BiomeGenerator.Instance?.CurrentRoom;
            if (Floor(CoopSync.CurrentPlace()) == Floor(Target) && Spot.HasValue && Here != null && (Here.x != Spot.Value.x || Here.y != Spot.Value.y))
            {
                Vector2Int To = Spot.Value;
                MMTransition.Play(MMTransition.TransitionType.ChangeRoom, MMTransition.Effect.BlackFade, MMTransition.NO_SCENE, 0.5f, "", () => BiomeGenerator.ChangeRoom(To.x, To.y));
                yield return new WaitForSecondsRealtime(1f);
                while (MMTransition.IsPlaying && Time.realtimeSinceStartup < Until)
                    yield return null;
                yield return new WaitForSecondsRealtime(1f);
            }

            Player = PlayerFarming.Instance;
            string Now = CoopSync.CurrentPlace();
            bool There = Now != null && Now == HostNow(HostPlace);
            if (Player != null)
            {
                /* Our spot if it was this room, else next to the host */
                PlayerFarming Host = PlayerFarming.players.FirstOrDefault(Each => Each != null && PlayerManager.IsPuppet(Each.health));
                Vector3? Stand = Ours != null && Ours.Place == Now ? Ours.At : Host != null ? Host.transform.position + new Vector3(1f, 0f, 0f) : null;
                if (Stand.HasValue)
                    Player.transform.position = Stand.Value;
                PutBack(Player, Ours, Weapon, WeaponLevel, Curse, CurseLevel);
            }
            ++Arrived;
            Plugin.Logger?.LogInfo($"[COOP] Back with the host: {(There ? "in their room" : $"at {Now}, the host at {HostNow(HostPlace)}")} after {Hops} floors");
            OnlineMenu.Wait(null);
            yield return new WaitForSecondsRealtime(2f);
            if (Player != null)
                Player.health.invincible = WasInvincible;
        }

        /// <summary>
        /// Through this floor's way out, to where the host went on the map
        /// </summary>
        private static IEnumerator NextFloor()
        {
            BiomeGenerator Generator = BiomeGenerator.Instance;
            BiomeRoom Exit = Generator?.Rooms?.Find(Item => Item != null && new[] { Item.N_Room, Item.E_Room, Item.S_Room, Item.W_Room }
                .Any(Link => Link != null && Link.ConnectionType == MMRoomGeneration.GenerateRoom.ConnectionTypes.NextLayer));
            if (Exit == null)
            {
                Plugin.Logger?.LogWarning("[COOP] Going back: no way to the next floor on this one");
                yield return new WaitForSecondsRealtime(2f);
                yield break;
            }
            if (Generator.CurrentRoom != Exit)
            {
                MMTransition.Play(MMTransition.TransitionType.ChangeRoom, MMTransition.Effect.BlackFade, MMTransition.NO_SCENE, 0.3f, "", () => BiomeGenerator.ChangeRoom(Exit.x, Exit.y));
                yield return new WaitForSecondsRealtime(1f);
                while (MMTransition.IsPlaying)
                    yield return null;
                yield return new WaitForSecondsRealtime(1f);
            }

            Door Way = Door.Doors.Find(Item => Item != null && Item.isActiveAndEnabled && Item.ConnectionType == MMRoomGeneration.GenerateRoom.ConnectionTypes.NextLayer);
            if (Way == null || PlayerFarming.Instance == null)
            {
                Plugin.Logger?.LogWarning("[COOP] Going back: no door to the next floor in the exit room");
                yield return new WaitForSecondsRealtime(2f);
                yield break;
            }
            int Was = GameManager.CurrentDungeonFloor;
            string Before = CoopSync.CurrentPlace();
            typeof(Door).GetMethod("OnTriggerEnter2D", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                ?.Invoke(Way, new object[] { PlayerFarming.Instance.circleCollider2D });

            /* The map comes up and takes the host's pick by itself; then the next floor loads */
            float Waited = 0f;
            while (Floor(CoopSync.CurrentPlace()) == Floor(Before) && Waited < 60f)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                Waited += 0.5f;
            }
            while (MMTransition.IsPlaying && Waited < 90f)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                Waited += 0.5f;
            }
            yield return new WaitForSecondsRealtime(1f);
            Plugin.Logger?.LogInfo($"[COOP] Going back: floor {Was} -> {GameManager.CurrentDungeonFloor}, now {CoopSync.CurrentPlace()}");
        }

        /// <summary>
        /// Our gear and hearts as they were (or the host's weapon and curse, as the game's co-op gives)
        /// </summary>
        private static void PutBack(PlayerFarming Player, Standing Ours, int Weapon, int WeaponLevel, int Curse, int CurseLevel)
        {
            try
            {
                if (Ours != null)
                {
                    Weapon = Ours.Weapon;
                    WeaponLevel = Ours.WeaponLevel;
                    Curse = Ours.Curse;
                    CurseLevel = Ours.CurseLevel;
                }
                if (Weapon != (int)EquipmentType.None)
                    Player.playerWeapon.SetWeapon((EquipmentType)Weapon, WeaponLevel);
                if (Curse != (int)EquipmentType.None)
                    Player.playerSpells.SetSpell((EquipmentType)Curse, CurseLevel);
                if (Ours == null)
                {
                    ++GearPut;
                    Plugin.Logger?.LogInfo($"[COOP] Given the host's weapon {(EquipmentType)Weapon} and curse {(EquipmentType)Curse}");
                    return;
                }

                if (Ours.Relic != (int)RelicType.None)
                    Player.playerRelic.EquipRelic(EquipmentManager.GetRelicData((RelicType)Ours.Relic), false);
                foreach (var (Card, Upgrade) in Ours.Cards)
                {
                    if (!Player.RunTrinkets.Exists(Each => (int)Each.CardType == Card))
                        TrinketManager.AddTrinket(new TarotCards.TarotCard((TarotCards.Card)Card, Upgrade), Player);
                }

                /* Hearts last: some cards give hearts when taken, what we had is what counts */
                HealthPlayer Health = Player.health;
                float[] H = Ours.Hearts;
                if (H[1] > 0f)
                {
                    Health.totalHP = H[1];
                    Health.TotalSpiritHearts = H[5];
                    Health.BlueHearts = H[2];
                    Health.BlackHearts = H[3];
                    Health.SpiritHearts = H[4];
                    Health.FireHearts = H[6];
                    Health.IceHearts = H[7];
                    /* Down when we went: up again with half a heart, the run goes on */
                    Health.HP = Mathf.Max(H[0], H[0] + H[2] + H[3] + H[4] + H[6] + H[7] > 0f ? H[0] : 1f);
                }
                ++GearPut;
                Plugin.Logger?.LogInfo($"[COOP] Our things back: {(EquipmentType)Weapon} {WeaponLevel}, {(EquipmentType)Curse} {CurseLevel}, relic {(RelicType)Ours.Relic}, " +
                                       $"{Ours.Cards.Count} cards, hearts {Health.HP}/{Health.totalHP} blue {Health.BlueHearts} black {Health.BlackHearts} spirit {Health.SpiritHearts}/{Health.TotalSpiritHearts}");
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Can't put our things back: {e}");
            }
        }
    }
}

/* EOF */
