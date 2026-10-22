/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Peer to peer packet links (Steam relay and a loopback stand-in for tests)
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using Steamworks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Network
{
    /// <summary>
    /// Moves raw packets between this game and another player, addressed by a 64-bit id.
    /// </summary>
    internal interface IPeerLink
    {
        /// <summary>
        /// The id other players use to reach us.
        /// </summary>
        ulong LocalId { get; }

        /// <summary>
        /// Decides which remote peers may open a session with us.
        /// </summary>
        Func<ulong, bool> AllowPeer { get; set; }

        /// <summary>
        /// Sends a packet to a peer.
        /// </summary>
        /// <param name = "Peer">The id of the peer.</param>
        /// <param name = "Data">The packet bytes.</param>
        /// <param name = "Reliable">False for a packet that may be lost (the latest position of something).</param>
        void Send(ulong Peer, byte[] Data, bool Reliable = true);

        /// <summary>
        /// Fetches one received packet, if there is one.
        /// </summary>
        /// <param name = "Peer">The id of the sender.</param>
        /// <param name = "Data">The packet bytes.</param>
        /// <returns>Returns true if a packet was fetched.</returns>
        /// <remarks>Must be called from the Unity main thread.</remarks>
        bool TryReceive(out ulong Peer, out byte[] Data);

        /// <summary>
        /// Closes the session with a peer.
        /// </summary>
        /// <param name = "Peer">The id of the peer.</param>
        void Close(ulong Peer);

        /// <summary>
        /// Closes every session and releases the link.
        /// </summary>
        void Shutdown();
    }

    /// <summary>
    /// A link over ISteamNetworkingMessages. Traffic goes by SteamID through
    /// the Valve relay network, so there are no IP addresses, ports or NAT issues.
    /// </summary>
    internal sealed class SteamPeerLink : IPeerLink
    {
        private const int Channel = 0;
        private const int MaxBatch = 64;
        private readonly IntPtr[] Batch = new IntPtr[MaxBatch];
        private readonly ConcurrentQueue<(ulong, byte[])> Pending = new();
        private Callback<SteamNetworkingMessagesSessionRequest_t> SessionRequest;
        private Callback<SteamNetworkingMessagesSessionFailed_t> SessionFailed;
        private int SendFailures;

        /// <summary>
        /// How much Steam may hold for a peer that can't keep up before it refuses to queue more.
        /// The default (512 KB) fills up within seconds of a Wi-Fi drop-out at co-op traffic rates.
        /// </summary>
        private const int SendBufferSize = 8 << 20;

        public ulong LocalId { get; }
        public Func<ulong, bool> AllowPeer { get; set; } = _ => false;

        public SteamPeerLink()
        {
            LocalId = SteamUser.GetSteamID().m_SteamID;

            /* Start connecting to the relay network now, so the first packet to a friend isn't held up */
            SteamNetworkingUtils.InitRelayNetworkAccess();

            SessionRequest = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(OnSessionRequest);
            SessionFailed = Callback<SteamNetworkingMessagesSessionFailed_t>.Create(OnSessionFailed);
            SetSendBuffer();
        }

        private static void SetSendBuffer()
        {
            IntPtr Value = Marshal.AllocHGlobal(sizeof(int));
            try
            {
                Marshal.WriteInt32(Value, SendBufferSize);
                if (!SteamNetworkingUtils.SetConfigValue(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendBufferSize,
                                                         ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Global,
                                                         IntPtr.Zero,
                                                         ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
                                                         Value))
                    Plugin.Logger?.LogWarning("[STEAM] Can't raise the send buffer, keeping Steam's default");
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[STEAM] Can't raise the send buffer: {e.Message}");
            }
            finally
            {
                Marshal.FreeHGlobal(Value);
            }
        }

        /// <summary>
        /// A stranger sent us a first packet. Accept it only from the players we expect.
        /// </summary>
        private void OnSessionRequest(SteamNetworkingMessagesSessionRequest_t Request)
        {
            ulong Remote = Request.m_identityRemote.GetSteamID64();

            if (Remote != LocalId && !AllowPeer(Remote))
            {
                Plugin.Logger?.LogWarning($"[STEAM] Ignoring a session request from {Remote}, it isn't in our lobby");
                return;
            }

            SteamNetworkingMessages.AcceptSessionWithUser(ref Request.m_identityRemote);
        }

        private void OnSessionFailed(SteamNetworkingMessagesSessionFailed_t Failed)
        {
            Plugin.Logger?.LogWarning($"[STEAM] Session with {Failed.m_info.m_identityRemote.GetSteamID64()} failed: {Failed.m_info.m_eEndReason} {Failed.m_info.m_szEndDebug}");
        }

        public void Send(ulong Peer, byte[] Data, bool Reliable = true)
        {
            SteamNetworkingIdentity Identity = new();
            GCHandle Pin;
            EResult Result;

            Identity.SetSteamID64(Peer);
            Pin = GCHandle.Alloc(Data, GCHandleType.Pinned);
            try
            {
                /*
                 * Events (handshake, kills, pickups, disconnect) go reliable and in order. The latest
                 * positions go unreliable: a lost one is replaced by the next a tenth of a second later,
                 * and it doesn't hold up the events behind it while Steam resends it.
                 */
                Result = SteamNetworkingMessages.SendMessageToUser(ref Identity,
                                                                   Pin.AddrOfPinnedObject(),
                                                                   (uint)Data.Length,
                                                                   (Reliable ? Constants.k_nSteamNetworkingSend_ReliableNoNagle : Constants.k_nSteamNetworkingSend_UnreliableNoNagle) |
                                                                   Constants.k_nSteamNetworkingSend_AutoRestartBrokenSession,
                                                                   Channel);
            }
            finally
            {
                Pin.Free();
            }

            /* A peer that can't keep up fails every send for a while, log the first few */
            if (Result != EResult.k_EResultOK && (++SendFailures <= 10 || SendFailures % 500 == 0))
                Plugin.Logger?.LogWarning($"[STEAM] SendMessageToUser({Peer}) returned {Result} ({SendFailures} failed sends so far)");
        }

        public bool TryReceive(out ulong Peer, out byte[] Data)
        {
            int Count, Index;
            SteamNetworkingMessage_t Message;

            if (Pending.IsEmpty)
            {
                Count = SteamNetworkingMessages.ReceiveMessagesOnChannel(Channel, Batch, MaxBatch);
                for (Index = 0; Index < Count; Index++)
                {
                    Message = Marshal.PtrToStructure<SteamNetworkingMessage_t>(Batch[Index]);
                    byte[] Copy = new byte[Message.m_cbSize];
                    Marshal.Copy(Message.m_pData, Copy, 0, Message.m_cbSize);
                    Pending.Enqueue((Message.m_identityPeer.GetSteamID64(), Copy));
                    SteamNetworkingMessage_t.Release(Batch[Index]);
                }
            }

            if (Pending.TryDequeue(out var Item))
            {
                (Peer, Data) = Item;
                return true;
            }

            Peer = 0;
            Data = null;
            return false;
        }

        public void Close(ulong Peer)
        {
            SteamNetworkingIdentity Identity = new();
            Identity.SetSteamID64(Peer);
            SteamNetworkingMessages.CloseSessionWithUser(ref Identity);
        }

        public void Shutdown()
        {
            SessionRequest?.Dispose();
            SessionFailed?.Dispose();
            SessionRequest = null;
            SessionFailed = null;
        }
    }

    /// <summary>
    /// A stand-in for the Steam link that lets two copies of the game on one PC
    /// talk through localhost. Peer N listens on 127.0.0.1:(BasePort + N).
    /// </summary>
    /// <remarks>
    /// Enabled with the -cotlmp-fakesteam=N command line switch, for tests only.
    /// It goes through the same bridge code as the Steam link does.
    /// </remarks>
    internal sealed class LoopbackPeerLink : IPeerLink
    {
        internal const int BasePort = 47110;
        private readonly UdpClient Socket;
        private readonly Chaos Faults;
        private readonly ConcurrentQueue<(ulong, byte[])> Received = new();
        private volatile bool Running = true;

        public ulong LocalId { get; }
        public Func<ulong, bool> AllowPeer { get; set; } = _ => true;

        public LoopbackPeerLink(ulong Id)
        {
            LocalId = Id;
            Socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, BasePort + (int)Id));
            /* Don't let an ICMP "port unreachable" from a closed peer kill the receive loop */
            Socket.Client.IOControl(-1744830452, new byte[] { 0 }, null);
            Socket.Client.ReceiveBufferSize = SteamBridge.LocalBufferSize;
            _ = System.Threading.Tasks.Task.Run(ReceiveLoop);

            Faults = Chaos.FromCommandLine(this);
        }

        private async System.Threading.Tasks.Task ReceiveLoop()
        {
            while (Running)
            {
                try
                {
                    UdpReceiveResult Result = await Socket.ReceiveAsync();
                    ulong Peer = (ulong)(Result.RemoteEndPoint.Port - BasePort);

                    if (AllowPeer(Peer))
                        Received.Enqueue((Peer, Result.Buffer));
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException)
                {
                    /* A peer went away, keep listening */
                }
            }
        }

        public void Send(ulong Peer, byte[] Data, bool Reliable = true)
        {
            if (Faults != null)
                Faults.Send(Peer, Data, Reliable);
            else
                SendNow(Peer, Data);
        }

        private void SendNow(ulong Peer, byte[] Data)
        {
            try
            {
                Socket.Send(Data, Data.Length, new IPEndPoint(IPAddress.Loopback, BasePort + (int)Peer));
            }
            catch (ObjectDisposedException)
            {
            }
        }

        /// <summary>
        /// A bad connection for tests: -cotlmp-netchaos=LOSS,DELAY,JITTER[,EVERY,OUTAGE]
        /// (lost packets in percent, delay and jitter in milliseconds, and a drop-out
        /// of OUTAGE seconds every EVERY seconds).
        /// </summary>
        /// <remarks>
        /// It behaves the way the Steam link does: reliable packets are never lost and keep their
        /// order, but wait for resends (and for the end of a drop-out); unreliable ones may be lost
        /// or overtake each other.
        /// </remarks>
        private sealed class Chaos
        {
            private readonly LoopbackPeerLink Link;
            private readonly double Loss, Delay, Jitter, Every, Outage;
            private readonly Stopwatch Clock = Stopwatch.StartNew();
            private readonly System.Random Dice = new(7);
            private readonly List<(double At, long Order, ulong Peer, byte[] Data)> Queue = new();
            private readonly Dictionary<ulong, double> LastReliable = new();
            private long Order;

            /* Counters for the test harness report */
            internal static int Lost, Resent;

            private Chaos(LoopbackPeerLink Owner, double LossPercent, double DelayMs, double JitterMs, double EverySec, double OutageSec)
            {
                Link = Owner;
                Loss = LossPercent / 100.0;
                Delay = DelayMs;
                Jitter = JitterMs;
                Every = EverySec * 1000.0;
                Outage = OutageSec * 1000.0;
                new System.Threading.Thread(Deliver) { IsBackground = true, Name = "COTLMP net chaos" }.Start();
                Plugin.Logger?.LogWarning($"[STEAM] Bad connection on: loss {LossPercent}% delay {DelayMs} ms jitter {JitterMs} ms, {OutageSec} s drop-out every {EverySec} s");
            }

            public static Chaos FromCommandLine(LoopbackPeerLink Owner)
            {
                foreach (string Arg in Environment.GetCommandLineArgs())
                {
                    if (!Arg.StartsWith("-cotlmp-netchaos=", StringComparison.OrdinalIgnoreCase))
                        continue;
                    string[] Parts = Arg.Substring("-cotlmp-netchaos=".Length).Split(',');
                    double Get(int Index) => Index < Parts.Length && double.TryParse(Parts[Index], NumberStyles.Float, CultureInfo.InvariantCulture, out double Value) ? Value : 0.0;
                    return new Chaos(Owner, Get(0), Get(1), Get(2), Get(3), Get(4));
                }
                return null;
            }

            public void Send(ulong Peer, byte[] Data, bool Reliable)
            {
                lock (Queue)
                {
                    double Now = Clock.Elapsed.TotalMilliseconds;
                    double At = Now + Delay + Dice.NextDouble() * Jitter;

                    /* Inside a drop-out nothing gets through until it ends */
                    if (Every > 0 && Now % Every < Outage)
                    {
                        if (!Reliable)
                        {
                            ++Lost;
                            return;
                        }
                        At = Math.Max(At, Now - Now % Every + Outage + Delay);
                    }

                    if (Reliable)
                    {
                        /* A lost reliable packet comes after a resend, and holds up the ones behind it */
                        if (Dice.NextDouble() < Loss)
                        {
                            At += 2 * Delay + 100;
                            ++Resent;
                        }
                        if (LastReliable.TryGetValue(Peer, out double Last) && At < Last)
                            At = Last;
                        LastReliable[Peer] = At;
                    }
                    else if (Dice.NextDouble() < Loss)
                    {
                        ++Lost;
                        return;
                    }

                    Queue.Add((At, Order++, Peer, Data));
                }
            }

            private void Deliver()
            {
                List<(double At, long Order, ulong Peer, byte[] Data)> Due = new();
                while (Link.Running)
                {
                    lock (Queue)
                    {
                        double Now = Clock.Elapsed.TotalMilliseconds;
                        for (int Index = Queue.Count - 1; Index >= 0; Index--)
                        {
                            if (Queue[Index].At <= Now)
                            {
                                Due.Add(Queue[Index]);
                                Queue.RemoveAt(Index);
                            }
                        }
                    }

                    Due.Sort((A, B) => A.At != B.At ? A.At.CompareTo(B.At) : A.Order.CompareTo(B.Order));
                    foreach (var Item in Due)
                        Link.SendNow(Item.Peer, Item.Data);
                    Due.Clear();
                    System.Threading.Thread.Sleep(1);
                }
            }
        }

        /// <summary>
        /// Packets the bad connection lost and reliable ones it held back for a resend, for the test report.
        /// </summary>
        internal static string ChaosStats => $"{Chaos.Lost}/{Chaos.Resent}";

        public bool TryReceive(out ulong Peer, out byte[] Data)
        {
            if (Received.TryDequeue(out var Item))
            {
                (Peer, Data) = Item;
                return true;
            }

            Peer = 0;
            Data = null;
            return false;
        }

        public void Close(ulong Peer)
        {
        }

        public void Shutdown()
        {
            Running = false;
            Socket.Dispose();
        }
    }
}

/* EOF */
