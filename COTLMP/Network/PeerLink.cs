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
        void Send(ulong Peer, byte[] Data);

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

        public ulong LocalId { get; }
        public Func<ulong, bool> AllowPeer { get; set; } = _ => false;

        public SteamPeerLink()
        {
            LocalId = SteamUser.GetSteamID().m_SteamID;

            /* Start connecting to the relay network now, so the first packet to a friend isn't held up */
            SteamNetworkingUtils.InitRelayNetworkAccess();

            SessionRequest = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(OnSessionRequest);
            SessionFailed = Callback<SteamNetworkingMessagesSessionFailed_t>.Create(OnSessionFailed);
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

        public void Send(ulong Peer, byte[] Data)
        {
            SteamNetworkingIdentity Identity = new();
            GCHandle Pin;
            EResult Result;

            Identity.SetSteamID64(Peer);
            Pin = GCHandle.Alloc(Data, GCHandleType.Pinned);
            try
            {
                /*
                 * COTLMP drops any message older than the newest one it has seen,
                 * so reliable ordered delivery is what keeps the handshake and
                 * the disconnect from getting lost on a bad connection.
                 */
                Result = SteamNetworkingMessages.SendMessageToUser(ref Identity,
                                                                   Pin.AddrOfPinnedObject(),
                                                                   (uint)Data.Length,
                                                                   Constants.k_nSteamNetworkingSend_ReliableNoNagle | Constants.k_nSteamNetworkingSend_AutoRestartBrokenSession,
                                                                   Channel);
            }
            finally
            {
                Pin.Free();
            }

            if (Result != EResult.k_EResultOK)
                Plugin.Logger?.LogWarning($"[STEAM] SendMessageToUser({Peer}) returned {Result}");
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
            _ = System.Threading.Tasks.Task.Run(ReceiveLoop);
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

        public void Send(ulong Peer, byte[] Data)
        {
            Socket.Send(Data, Data.Length, new IPEndPoint(IPAddress.Loopback, BasePort + (int)Peer));
        }

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
