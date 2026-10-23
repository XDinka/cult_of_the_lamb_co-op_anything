/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     The partner's puppet moves exactly as they do, and only shows once they are there
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using MMTools;
using Spine;
using Spine.Unity;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// Streams the local player's animations to the partner's puppet.
    /// </summary>
    /// <remarks>
    /// The puppet is a second PlayerFarming that the network only moves about. Its own state
    /// machine never runs the partner's attacks, dodges, spells or hits, so it slid around in its
    /// idle pose. Ten times a second (and at once when an animation starts) each game sends what
    /// its lamb's skeleton plays on every track, how far in, which way it faces, its skin and
    /// weapon, where it stands, and the place it is in. That message is also the partner's word
    /// that their room is loaded and they stand in it: the puppet is made only when such a message
    /// names our place, already dressed and armed as theirs, and goes away when they stop sending
    /// from here (a door, a loading screen). The position stream alone never makes a puppet, so
    /// no stand-in of the default look shows up in a room before its player does.
    /// </remarks>
    internal static class PuppetSync
    {
        private sealed class Track
        {
            public int Index;
            public string Name;
            public bool Loop;
            public float Time;
        }

        private sealed class Pose
        {
            public string Place;
            public int Skin;
            public string Weapon;
            public Vector3 Position;
            public float ScaleX;
            public readonly List<Track> Tracks = new();
            public float At;
        }

        private const float SendEvery = 0.1f;
        /* How long after a room change or load our own lamb counts as standing in it */
        private const float SettleTime = 0.35f;
        /* Without word from the partner for this long, their puppet goes */
        private const float GoneAfter = 1.5f;
        private const int MaxTracks = 4;

        private static readonly Dictionary<uint, Pose> Poses = new();
        private static readonly HashSet<uint> Driven = new();

        /* What we last sent */
        private static string SentPlace;
        private static string SentFirst;
        private static float SentAt, PlaceSince;

        /* Counters for the test harness report */
        internal static int Sent, Received, Applied, Appeared, Vanished;

        public static void Initialize()
        {
            GameObject Host = new("COTLMP Puppet Sync");
            UnityEngine.Object.DontDestroyOnLoad(Host);
            Host.AddComponent<Runner>();
        }

        /// <summary>
        /// True while a puppet follows its player's own animations: nothing else should change its state
        /// </summary>
        internal static bool Driving(uint Id) => Driven.Contains(Id);

        /* OURS ***************************************************************/

        private static string WeaponSkin(PlayerFarming Player)
        {
            try
            {
                if (Player.currentWeapon != EquipmentType.None && EquipmentManager.GetWeaponData(Player.currentWeapon) is WeaponData Data && Data != null)
                    return Data.Skin.ToString();
            }
            catch
            {
            }
            return WeaponData.Skins.Normal.ToString();
        }

        private static void SendOurs()
        {
            PlayerFarming Player = PlayerFarming.Instance;
            if (!CoopSync.InSession || Player == null || Player.Spine == null || MMTransition.IsPlaying || !Player.gameObject.activeInHierarchy)
            {
                SentPlace = null;
                return;
            }
            string Place = CoopSync.CurrentPlace();
            if (Place == null)
                return;
            float Now = Time.realtimeSinceStartup;
            if (Place != SentPlace)
            {
                SentPlace = Place;
                PlaceSince = Now;
                SentFirst = null;
            }
            if (Now - PlaceSince < SettleTime)
                return;

            Spine.AnimationState State = Player.Spine.AnimationState;
            TrackEntry First = State?.GetCurrent(0);
            string FirstName = First?.Animation?.Name;
            if (FirstName == SentFirst && Now - SentAt < SendEvery)
                return;
            SentFirst = FirstName;
            SentAt = Now;

            CoopSync.Send(CoopSync.Kind.PuppetPose, Writer =>
            {
                Writer.Write(Place);
                Writer.Write(Appearance.LocalSkin());
                Writer.Write(WeaponSkin(Player));
                Vector3 At = Player.transform.position;
                Writer.Write(At.x);
                Writer.Write(At.y);
                Writer.Write(At.z);
                Writer.Write(Player.Spine.Skeleton?.ScaleX ?? 1f);
                List<TrackEntry> Playing = new();
                for (int Index = 0; State != null && Index < MaxTracks; Index++)
                {
                    TrackEntry Entry = State.GetCurrent(Index);
                    if (Entry?.Animation != null && Entry.Animation.Name != "<empty>")
                        Playing.Add(Entry);
                }
                Writer.Write((byte)Playing.Count);
                foreach (TrackEntry Entry in Playing)
                {
                    Writer.Write((byte)Entry.TrackIndex);
                    Writer.Write(Entry.Animation.Name);
                    Writer.Write(Entry.Loop);
                    Writer.Write(Entry.TrackTime);
                }
            });
            ++Sent;
        }

        /* THEIRS *************************************************************/

        /// <summary>
        /// The partner's lamb as it is now
        /// </summary>
        public static void OnRelay(uint From, BinaryReader Reader)
        {
            Pose Item = new()
            {
                Place = Reader.ReadString(),
                Skin = Reader.ReadInt32(),
                Weapon = Reader.ReadString(),
                Position = new Vector3(Reader.ReadSingle(), Reader.ReadSingle(), Reader.ReadSingle()),
                ScaleX = Reader.ReadSingle(),
                At = Time.realtimeSinceStartup
            };
            int Count = Reader.ReadByte();
            for (int Index = 0; Index < Count; Index++)
                Item.Tracks.Add(new Track { Index = Reader.ReadByte(), Name = Reader.ReadString(), Loop = Reader.ReadBoolean(), Time = Reader.ReadSingle() });
            Poses[From] = Item;
            ++Received;
        }

        private static void ApplyTheirs()
        {
            float Now = Time.realtimeSinceStartup;
            string Here = CoopSync.InSession && !MMTransition.IsPlaying && PlayerFarming.Instance != null ? CoopSync.CurrentPlace() : null;
            Driven.Clear();

            foreach (var Pair in Poses)
            {
                uint Id = Pair.Key;
                Pose Item = Pair.Value;
                bool There = Here != null && Item.Place == Here && Now - Item.At < GoneAfter;
                PlayerFarming Puppet = PlayerManager.GetPuppet(Id);

                if (!There)
                {
                    /* Not during our own loading: the transition clears the puppets anyway */
                    if (Puppet != null && Here != null)
                    {
                        PlayerManager.DeletePlayer(Id);
                        ++Vanished;
                        Plugin.Logger?.LogInfo($"[COOP] Player {Id} left {Here}, their puppet goes");
                    }
                    continue;
                }

                if (Puppet == null)
                {
                    PlayerManager.CreatePlayer(Id, Item.Position, Item.Skin);
                    PlayerManager.SetPuppetWeapon(Id, Item.Weapon);
                    Puppet = PlayerManager.GetPuppet(Id);
                    if (Puppet == null)
                        continue;
                    ++Appeared;
                    Plugin.Logger?.LogInfo($"[COOP] Player {Id} is in {Here} now, their puppet shows");
                }
                else
                {
                    PlayerManager.SetPlayerSkin(Id, Item.Skin);
                    PlayerManager.SetPuppetWeapon(Id, Item.Weapon);
                }

                /* Its own animator would put idle or running back every frame */
                if (Driven.Add(Id) && Puppet.simpleSpineAnimator != null && Puppet.simpleSpineAnimator.enabled)
                {
                    Puppet.simpleSpineAnimator.UpdateAnimsOnStateChange = false;
                    Puppet.simpleSpineAnimator.enabled = false;
                }
                Animate(Puppet, Item, Now);
            }
        }

        /// <summary>
        /// Puts the partner's animations on the puppet's skeleton, at the point they are in theirs
        /// </summary>
        private static void Animate(PlayerFarming Puppet, Pose Item, float Now)
        {
            SkeletonAnimation Spine = Puppet.Spine;
            if (Spine == null || Spine.AnimationState == null || Spine.Skeleton == null)
                return;
            Spine.Skeleton.ScaleX = Item.ScaleX;

            float Late = Mathf.Min(Now - Item.At, 0.5f);
            bool[] Used = new bool[MaxTracks];
            foreach (Track Wanted in Item.Tracks)
            {
                if (Wanted.Index >= MaxTracks)
                    continue;
                Used[Wanted.Index] = true;
                TrackEntry Current = Spine.AnimationState.GetCurrent(Wanted.Index);
                if (Current?.Animation != null && Current.Animation.Name == Wanted.Name)
                    continue;
                Spine.Animation Animation = Spine.Skeleton.Data.FindAnimation(Wanted.Name);
                if (Animation == null)
                    continue;
                TrackEntry Started = Spine.AnimationState.SetAnimation(Wanted.Index, Animation, Wanted.Loop);
                Started.TrackTime = Wanted.Time + Late;
                if (++Applied <= 40)
                    Plugin.Logger?.LogInfo($"[COOP] Puppet plays {Wanted.Name} on track {Wanted.Index}{(Wanted.Loop ? " (loop)" : "")}");
            }
            for (int Index = 1; Index < MaxTracks; Index++)
            {
                TrackEntry Current = Spine.AnimationState.GetCurrent(Index);
                if (!Used[Index] && Current?.Animation != null && Current.Animation.Name != "<empty>")
                    Spine.AnimationState.SetEmptyAnimation(Index, 0.1f);
            }
        }

        private sealed class Runner : MonoBehaviour
        {
            private void Update()
            {
                try
                {
                    SendOurs();
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogWarning($"[COOP] Sending our pose failed: {e.Message}");
                    SentPlace = null;
                }
            }

            /* After the puppet's own animator had its say this frame */
            private void LateUpdate()
            {
                try
                {
                    ApplyTheirs();
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogWarning($"[COOP] Posing the partner's puppet failed: {e.Message}");
                }
            }
        }
    }
}

/* EOF */
