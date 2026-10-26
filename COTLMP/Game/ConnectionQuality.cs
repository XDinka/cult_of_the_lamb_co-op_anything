/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Ping, packet loss and a bad connection icon
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using MMTools;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// Measures the connection to the partner and shows an icon when it is bad.
    /// </summary>
    /// <remarks>
    /// Twice a second a ping goes to the partner the way the co-op data goes (through the bridge
    /// and the server, unreliable like the enemy positions), and the partner sends it back. The
    /// round trip is the ping; pings that never come back are the packet loss; no answer for a few
    /// seconds is a broken connection. Nothing is shown while the connection is good:
    /// <list type="bullet">
    /// <item>ping 120 ms or more: three yellow bars with an exclamation mark, 200 ms or more: red;</item>
    /// <item>5 % of packets or more lost: three packets with one missing and the percentage, 15 % or more: red;</item>
    /// <item>no answer for 3 s: an unplugged cable.</item>
    /// </list>
    /// A loading screen stops a game for seconds, which would look like a dead connection. So a
    /// player about to load tells the other one, the answers a frozen game gives late say so, and
    /// such pings count neither as lost nor towards the ping.
    /// </remarks>
    internal static class ConnectionQuality
    {
        private const float PingEvery = 0.5f;
        private const float LossWindow = 10f;
        /// <summary>
        /// A ping that took longer than this is counted as lost
        /// </summary>
        private const float LostAfter = 2f;
        private const float BrokenAfter = 3f;

        internal const float YellowPing = 120f, RedPing = 200f;
        internal const float YellowLoss = 0.05f, RedLoss = 0.15f;

        private sealed class Sent
        {
            public uint Number;
            public float At;
            public bool Answered;
            /* Held up by a loading screen on either side: not the connection's fault */
            public bool Excused;
        }

        /// <summary>
        /// A frame this long is a loading screen or a hitch, not the network
        /// </summary>
        private const float StallFrame = 0.25f;
        private const float StallGrace = 3f;
        private const float BusyFor = 20f;

        /* When our game last stood still, and until when the partner said they are loading */
        private static float LastStall = -100f;
        private static float PartnerBusyUntil = -1f;

        private static bool WeStalledRecently => Time.realtimeSinceStartup - LastStall < StallGrace;
        private static bool PartnerBusy => Time.realtimeSinceStartup < PartnerBusyUntil || PartnerLoading;

        /* The partner said they are between places: a long load (the host's save, a new floor) isn't a lost connection, up to a minute */
        private static bool PartnerLoading =>
            RoomSync.PartnerPlace == null && RoomSync.PartnerLoadingSince >= 0f && Time.realtimeSinceStartup - RoomSync.PartnerLoadingSince < 60f;

        private static readonly List<Sent> Pings = new();
        private static readonly float[] Samples = new float[5];
        private static int SampleCount, NextSample;
        private static uint NextNumber;
        private static float LastAnswer = -1f;
        /* Not UnityEngine.Random: its state is what makes both games roll the same (RoomRandom) */
        private static readonly uint Nonce = (uint)new System.Random().Next(1, int.MaxValue);

        /// <summary>
        /// The round trip to the partner in milliseconds (the mean of the last few), or -1 before the first answer
        /// </summary>
        internal static float PingMs { get; private set; } = -1f;

        /// <summary>
        /// The share of pings of the last 10 s that didn't come back in time
        /// </summary>
        internal static float Loss { get; private set; }

        /// <summary>
        /// True when the partner answered before but hasn't for a few seconds
        /// </summary>
        internal static bool Broken { get; private set; }

        public static void Initialize()
        {
            Plugin.MonoInstance.StartCoroutine(Loop());
            GameObject Icon = new("COTLMP Connection Icon");
            Object.DontDestroyOnLoad(Icon);
            Icon.AddComponent<ConnectionIcon>();
            MMTransition.OnBeginTransition += SayBusy;
        }

        /// <summary>
        /// We are about to load: the partner shouldn't take our silence for a broken connection.
        /// Sent twice, since pings go unreliable.
        /// </summary>
        /// <summary>
        /// We are about to stand still a while (loading): the partner's icon doesn't take it for a broken connection
        /// </summary>
        internal static void SayBusy()
        {
            if (!CoopSync.InSession)
                return;
            LastStall = Time.realtimeSinceStartup;
            for (int Copy = 0; Copy < 2; Copy++)
                CoopSync.Send(CoopSync.Kind.NetPing, Writer =>
                {
                    Writer.Write(0u);
                    Writer.Write(0u);
                });
        }

        private static IEnumerator Loop()
        {
            var Wait = new WaitForSecondsRealtime(PingEvery);
            while (true)
            {
                yield return Wait;

                /* Nothing to measure while hosting alone (nobody joined yet, or the partner left) */
                if (!CoopSync.InSession || !PartnerConnected)
                {
                    Reset();
                    continue;
                }

                float Now = Time.realtimeSinceStartup;
                uint Number = NextNumber++;
                Pings.Add(new Sent { Number = Number, At = Now });
                CoopSync.Send(CoopSync.Kind.NetPing, Writer =>
                {
                    Writer.Write(Nonce);
                    Writer.Write(Number);
                });

                Update(Now);
            }
        }

        /// <summary>
        /// Someone else is in the session. On the host the server knows; a joined player has the host.
        /// </summary>
        /// <remarks>
        /// A partner whose game crashed stays connected until the server's timeout, so their silence
        /// shows as a broken connection until then, which is what it is.
        /// </remarks>
        private static bool PartnerConnected =>
            !Plugin.GlobalsInternal.IsServerCreator || (COTLMP.Ui.PauseMenuPatches.Server?.PlayerCount ?? 0) >= 2;

        private static void Reset()
        {
            Pings.Clear();
            SampleCount = NextSample = 0;
            LastAnswer = -1f;
            PingMs = -1f;
            Loss = 0f;
            Broken = false;
        }

        private static void Update(float Now)
        {
            Pings.RemoveAll(Ping => Now - Ping.At > LossWindow + LostAfter);

            /* Only pings old enough to have come back count */
            int Due = 0, Lost = 0;
            foreach (Sent Ping in Pings)
            {
                if (Now - Ping.At < LostAfter || Ping.Excused || Mathf.Abs(Ping.At - LastStall) < LostAfter + 1f)
                    continue;
                ++Due;
                if (!Ping.Answered)
                    ++Lost;
            }
            /* Before the first answer there is no partner to lose packets to */
            Loss = Due > 0 && LastAnswer >= 0f ? (float)Lost / Due : 0f;

            /* A partner that never answered (an older mod, or nobody there yet) isn't a broken connection */
            Broken = LastAnswer >= 0f && Now - LastAnswer > BrokenAfter && !PartnerBusy && !WeStalledRecently;
        }

        /// <summary>
        /// A ping from the partner, or our ping coming back.
        /// </summary>
        public static void OnRelay(CoopSync.Kind Type, BinaryReader Reader)
        {
            uint From = Reader.ReadUInt32();
            uint Number = Reader.ReadUInt32();

            if (Type == CoopSync.Kind.NetPing)
            {
                /* No sender: the partner is about to load */
                if (From == 0)
                {
                    PartnerBusyUntil = Time.realtimeSinceStartup + BusyFor;
                    return;
                }

                /* Say if our game stood still, so the late answer isn't blamed on the connection */
                bool Stalled = WeStalledRecently;
                CoopSync.Send(CoopSync.Kind.NetPong, Writer =>
                {
                    Writer.Write(From);
                    Writer.Write(Number);
                    Writer.Write(Stalled);
                });
                return;
            }

            /* Older mods send no flag */
            bool TheyStalled = Reader.BaseStream.Position < Reader.BaseStream.Length && Reader.ReadBoolean();

            /* Someone else's ping coming back */
            if (From != Nonce)
                return;

            float Now = Time.realtimeSinceStartup;
            Sent Ping = Pings.Find(Item => Item.Number == Number);
            if (Ping == null || Ping.Answered)
                return;

            LastAnswer = Now;
            Broken = false;
            PartnerBusyUntil = -1f;

            if (TheyStalled || WeStalledRecently)
            {
                Ping.Excused = true;
                return;
            }
            Ping.Answered = Now - Ping.At < LostAfter;

            Samples[NextSample] = (Now - Ping.At) * 1000f;
            NextSample = (NextSample + 1) % Samples.Length;
            SampleCount = Mathf.Min(SampleCount + 1, Samples.Length);
            float Sum = 0f;
            for (int Index = 0; Index < SampleCount; Index++)
                Sum += Samples[Index];
            PingMs = Sum / SampleCount;
        }

        /// <summary>
        /// The icon in the top right corner, drawn from rectangles so it needs no textures.
        /// </summary>
        private sealed class ConnectionIcon : MonoBehaviour
        {
            private static readonly Color Yellow = new(1f, 0.8f, 0.15f, 1f);
            private static readonly Color Red = new(0.95f, 0.2f, 0.17f, 1f);
            private static readonly Color Shadow = new(0f, 0f, 0f, 0.7f);
            private static readonly Color Dim = new(1f, 1f, 1f, 0.5f);
            private static Texture2D Pixel;
            private static GUIStyle Label;

            /* One pixel of the icon, in screen pixels */
            private float U;

            /*
             * When the ping or loss went bad. A loading screen on either side holds up a ping or two,
             * so the icon waits until it has stayed bad for a while.
             */
            private float BadSince = -1f;
            private const float ShowAfter = 2f;

            private void Update()
            {
                if (Time.unscaledDeltaTime > StallFrame || MMTransition.IsPlaying)
                    LastStall = Time.realtimeSinceStartup;

                /* Notice a drop-out between pings too */
                if (CoopSync.InSession && PartnerConnected && LastAnswer >= 0f && Time.realtimeSinceStartup - LastAnswer > BrokenAfter && !PartnerBusy && !WeStalledRecently)
                    Broken = true;
            }

            private void OnGUI()
            {
                if (!CoopSync.InSession || !PartnerConnected || Event.current.type != EventType.Repaint)
                    return;

                bool HighPing = !Broken && PingMs >= YellowPing;
                bool HighLoss = !Broken && Loss >= YellowLoss;
                if (!HighPing && !HighLoss)
                    BadSince = -1f;
                else if (BadSince < 0f)
                    BadSince = Time.realtimeSinceStartup;
                if (!Broken && (BadSince < 0f || Time.realtimeSinceStartup - BadSince < ShowAfter))
                    return;

                if (Pixel == null)
                {
                    Pixel = new Texture2D(1, 1);
                    Pixel.SetPixel(0, 0, Color.white);
                    Pixel.Apply();
                }
                if (Label == null)
                {
                    Label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold };
                }

                U = Mathf.Max(2f, Screen.height / 270f);
                Label.fontSize = Mathf.RoundToInt(U * 4f);
                Color Old = GUI.color;

                /* Right to left from the corner */
                float Right = Screen.width - U * 6f, Top = U * 6f;
                if (Broken)
                {
                    Panel(Right - U * 24f, Top, 24f, false);
                    DrawCable(Right - U * 24f, Top);
                }
                else
                {
                    if (HighPing)
                    {
                        Color Tint = PingMs >= RedPing ? Red : Yellow;
                        Panel(Right - U * 16f, Top, 16f, true);
                        DrawBars(Right - U * 16f, Top, Tint);
                        Text(Right - U * 16f, Top + U * 13f, 16f, $"{PingMs:F0} ms", Tint);
                        Right -= U * 24f;
                    }
                    if (HighLoss)
                    {
                        Color Tint = Loss >= RedLoss ? Red : Yellow;
                        Panel(Right - U * 20f, Top, 20f, true);
                        DrawPackets(Right - U * 20f, Top, Tint);
                        Text(Right - U * 20f, Top + U * 13f, 20f, $"{Loss * 100f:F0}%", Tint);
                    }
                }

                GUI.color = Old;
            }

            /// <summary>
            /// A dark plate under an icon (and its label), so it reads on any background
            /// </summary>
            private void Panel(float X, float Y, float Width, bool WithLabel)
            {
                GUI.color = new Color(0.05f, 0.03f, 0.03f, 0.65f);
                GUI.DrawTexture(new Rect(X - U * 3f, Y - U * 3f, (Width + 6f) * U, (WithLabel ? 23f : 18f) * U), Pixel);
            }

            /// <summary>
            /// A rectangle in icon pixels with a shadow under it
            /// </summary>
            private void Box(float X, float Y, float W, float H, Color Tint)
            {
                GUI.color = Shadow;
                GUI.DrawTexture(new Rect(X - U * 0.5f, Y - U * 0.5f, W * U + U, H * U + U), Pixel);
                GUI.color = Tint;
                GUI.DrawTexture(new Rect(X, Y, W * U, H * U), Pixel);
            }

            private void Text(float X, float Y, float Width, string Value, Color Tint)
            {
                Rect Area = new(X - U * 4f, Y, (Width + 8f) * U, U * 7f);
                GUI.color = Shadow;
                GUI.Label(new Rect(Area.x + U * 0.5f, Area.y + U * 0.5f, Area.width, Area.height), Value, Label);
                GUI.color = Tint;
                GUI.Label(Area, Value, Label);
            }

            /// <summary>
            /// Three bars of growing height and an exclamation mark, 16 x 12
            /// </summary>
            private void DrawBars(float X, float Y, Color Tint)
            {
                Box(X, Y + U * 8f, 3f, 4f, Tint);
                Box(X + U * 4f, Y + U * 4f, 3f, 8f, Tint);
                Box(X + U * 8f, Y, 3f, 12f, Tint);
                DrawMark(X + U * 13f, Y, Tint);
            }

            private void DrawMark(float X, float Y, Color Tint)
            {
                Box(X, Y, 2f, 8f, Tint);
                Box(X, Y + U * 10f, 2f, 2f, Tint);
            }

            /// <summary>
            /// Three packets with the middle one lost (a dashed outline) and an exclamation mark, 20 x 12
            /// </summary>
            private void DrawPackets(float X, float Y, Color Tint)
            {
                float Middle = Y + U * 4f;
                Box(X, Middle, 4f, 4f, Tint);
                Box(X + U * 12f, Middle, 4f, 4f, Tint);

                /* The lost one: corners only, no shadow */
                GUI.color = Dim;
                foreach (var (Dx, Dy) in new[] { (0f, 0f), (3f, 0f), (0f, 3f), (3f, 3f) })
                    GUI.DrawTexture(new Rect(X + U * (6f + Dx), Middle + U * Dy, U, U), Pixel);

                DrawMark(X + U * 18f, Y, Tint);
            }

            /// <summary>
            /// An unplugged cable: a plug and a socket apart with a spark between them, 24 x 12
            /// </summary>
            private void DrawCable(float X, float Y)
            {
                float Mid = Y + U * 6f;

                /* The plug on the left, its prongs pointing right */
                Box(X, Mid - U, 4f, 2f, Red);
                Box(X + U * 4f, Mid - U * 4f, 5f, 8f, Red);
                Box(X + U * 9f, Mid - U * 3f, 3f, 1.5f, Red);
                Box(X + U * 9f, Mid + U * 1.5f, 3f, 1.5f, Red);

                /* The socket on the right with its two holes */
                Box(X + U * 15f, Mid - U * 4f, 5f, 8f, Red);
                GUI.color = Shadow;
                GUI.DrawTexture(new Rect(X + U * 15f, Mid - U * 3f, U * 2f, U * 1.5f), Pixel);
                GUI.DrawTexture(new Rect(X + U * 15f, Mid + U * 1.5f, U * 2f, U * 1.5f), Pixel);
                Box(X + U * 20f, Mid - U, 4f, 2f, Red);

                /* The spark in the gap */
                Box(X + U * 13f, Y - U, 1f, 3f, Yellow);
                Box(X + U * 12f, Y + U * 2f, 2f, 1f, Yellow);
                Box(X + U * 13f, Y + U * 10f, 1f, 3f, Yellow);
                Box(X + U * 13f, Y + U * 9f, 2f, 1f, Yellow);
            }
        }
    }
}

/* EOF */
