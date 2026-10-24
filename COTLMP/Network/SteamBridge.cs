/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Steam lobbies and a relay that carries the COTLMP UDP protocol over Steam
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP.Ui;
using COTLMPServer.Messages;
using MMTools;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Network
{
    /// <summary>
    /// Lets players host and join through Steam instead of IP addresses.
    /// </summary>
    /// <remarks>
    /// The server and the network client are left as they are. On the host every
    /// remote player gets a local UDP socket connected to the integrated server, so
    /// the server sees each friend as a separate 127.0.0.1 client. On the joining side
    /// the network client connects to a local proxy socket that forwards everything
    /// to the host's SteamID. This is the relay pattern from the Co-op Anything
    /// playbook (networking-steam-spacewar.md), run inside the game process, which
    /// already has Steam initialised under the game's own AppID.
    /// </remarks>
    internal static class SteamBridge
    {
        private sealed class HostPeer
        {
            public UdpClient Socket;
            public readonly ConcurrentQueue<byte[]> ToRemote = new();
        }

        private const int HandshakeTimeoutMs = 15000;

        /// <summary>
        /// Receive buffer of the local sockets between the game, the bridge and the server.
        /// A burst (a room full of enemies after a drop-out) overflows the default one, and a full
        /// UDP buffer drops packets silently, events included.
        /// </summary>
        internal const int LocalBufferSize = 4 << 20;

        /*
         * Sent between the bridges when a player leaves, so the other side learns at once
         * instead of after the 30 second timeouts. COTLMP messages start with the magic
         * number 0x173495, so a packet starting with 0xFF can't be one of them.
         */
        private static readonly byte[] Goodbye = Encoding.ASCII.GetBytes("\u00ffCOTLMP-BYE");
        private static readonly byte[] SelfTest = Encoding.ASCII.GetBytes("\u00ffCOTLMP-SELFTEST");
        private static float SelfTestSentAt = -1f;

        private static IPeerLink Link;
        private static bool IsHost;
        private static int ServerPort;

        /* Host side */
        private static readonly Dictionary<ulong, HostPeer> Peers = new();

        /* Client side */
        private static ulong HostId;
        private static UdpClient Proxy;
        private static volatile IPEndPoint GameClient;
        private static readonly ConcurrentQueue<byte[]> ToHost = new();

        /* Steam lobby state */
        private static CSteamID Lobby = CSteamID.Nil;
        private static Callback<GameLobbyJoinRequested_t> JoinRequested;
        private static Callback<LobbyChatUpdate_t> LobbyChanged;
        private static CallResult<LobbyCreated_t> LobbyCreatedResult;
        private static CallResult<LobbyEnter_t> LobbyEnterResult;

        /* Test switches and joins waiting for the main menu */
        private static ulong FakeSteamId;
        private static ulong PendingFakeHost;

        /// <summary>
        /// Joins the loopback host again, for tests of leaving and coming back.
        /// </summary>
        public static void QueueFakeRejoin(ulong Host)
        {
            if (IsFake)
                PendingFakeHost = Host;
        }

        /// <summary>
        /// The host we joined, 0 while hosting or alone
        /// </summary>
        internal static ulong JoinedHost => Active && !IsHost ? HostId : 0;

        /// <summary>
        /// We host (in the lobby or in the game)
        /// </summary>
        internal static bool Hosting => Active && IsHost;

        /// <summary>
        /// Our own Steam ID (the stand-in's number in a test)
        /// </summary>
        internal static ulong LocalId
        {
            get
            {
                try
                {
                    return IsFake ? FakeSteamId : SteamManager.Initialized ? SteamUser.GetSteamID().m_SteamID : 0;
                }
                catch (Exception)
                {
                    return 0;
                }
            }
        }

        /// <summary>
        /// The other players' Steam IDs: the friends who joined us, or the host we joined
        /// </summary>
        internal static List<ulong> PartnerIds()
        {
            return IsHost ? new List<ulong>(Peers.Keys) : HostId != 0 ? new List<ulong> { HostId } : new List<ulong>();
        }

        /// <summary>
        /// A player's Steam name
        /// </summary>
        internal static string NameOf(ulong Id)
        {
            if (Id == LocalId)
                return LocalName();
            try
            {
                return IsFake ? $"Player {Id}" : SteamFriends.GetFriendPersonaName(new CSteamID(Id));
            }
            catch (Exception)
            {
                return Id.ToString();
            }
        }

        /// <summary>
        /// Friends in this game with a lobby open to join (in a test, the stand-in's host 1)
        /// </summary>
        internal static List<(ulong Friend, ulong Lobby)> FriendLobbies()
        {
            List<(ulong, ulong)> Found = new();
            if (IsFake)
            {
                if (FakeSteamId != 1)
                    Found.Add((1, 0));
                return Found;
            }
            try
            {
                if (!SteamManager.Initialized)
                    return Found;
                AppId_t Ours = SteamUtils.GetAppID();
                int Count = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
                for (int Index = 0; Index < Count; Index++)
                {
                    CSteamID Friend = SteamFriends.GetFriendByIndex(Index, EFriendFlags.k_EFriendFlagImmediate);
                    if (SteamFriends.GetFriendGamePlayed(Friend, out FriendGameInfo_t Game) && Game.m_gameID.AppID() == Ours && Game.m_steamIDLobby.IsValid())
                        Found.Add((Friend.m_SteamID, Game.m_steamIDLobby.m_SteamID));
                }
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[STEAM] Can't list the friends' games: {e.Message}");
            }
            return Found;
        }

        /// <summary>
        /// Joins a friend's lobby, as accepting their invite would
        /// </summary>
        internal static void JoinFriend(ulong Friend, ulong FriendLobby)
        {
            Plugin.Logger?.LogInfo($"[STEAM] Joining friend {Friend} (lobby {FriendLobby})");
            if (IsFake)
                PendingFakeHost = Friend;
            else
                PendingLobby = FriendLobby;
        }

        /// <summary>
        /// Opens the Steam overlay's invite dialog as soon as the lobby is open (off for the menu's lobby,
        /// which has its own button for it)
        /// </summary>
        internal static bool InviteOnOpen = true;

        /// <summary>
        /// Joins this host again once the game can (after updating to the host's version): through
        /// the lobby of the game the friend is in, as accepting their invite would
        /// </summary>
        internal static void JoinAgain(ulong Host)
        {
            if (IsFake)
            {
                PendingFakeHost = Host;
                return;
            }
            Plugin.MonoInstance.StartCoroutine(FindHostLobby(Host));
        }

        private static IEnumerator FindHostLobby(ulong Host)
        {
            float Until = Time.realtimeSinceStartup + 120f;
            while (Time.realtimeSinceStartup < Until)
            {
                yield return new WaitForSecondsRealtime(2f);
                try
                {
                    if (SteamFriends.GetFriendGamePlayed(new CSteamID(Host), out FriendGameInfo_t Game) && Game.m_steamIDLobby.IsValid())
                    {
                        Plugin.Logger?.LogInfo($"[STEAM] Joining host {Host} again in lobby {Game.m_steamIDLobby.m_SteamID}");
                        PendingLobby = Game.m_steamIDLobby.m_SteamID;
                        yield break;
                    }
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogWarning($"[STEAM] Can't see the host's game yet: {e.Message}");
                }
            }
            Plugin.Logger?.LogWarning($"[STEAM] Host {Host} has no game open to join again");
        }
        private static ulong PendingLobby;
        private static bool ToldToLeave;
        private static bool Joining;

        /// <summary>
        /// True while players are connected through the bridge.
        /// </summary>
        public static bool Active => Link != null;

        /// <summary>
        /// True if the loopback stand-in for Steam is in use (two copies on one PC).
        /// </summary>
        public static bool IsFake => FakeSteamId != 0;

        /// <summary>
        /// Our own name in Steam (the mod's player name in a test without Steam)
        /// </summary>
        internal static string LocalName()
        {
            try
            {
                return IsFake || !SteamManager.Initialized ? $"Player {FakeSteamId}" : SteamFriends.GetPersonaName();
            }
            catch (Exception)
            {
                return Plugin.Globals.PlayerName;
            }
        }

        /// <summary>
        /// The Steam names of the other players in the session
        /// </summary>
        internal static List<string> PartnerNames()
        {
            List<ulong> Ids = IsHost ? new List<ulong>(Peers.Keys) : HostId != 0 ? new List<ulong> { HostId } : new List<ulong>();
            List<string> Names = new();
            foreach (ulong Id in Ids)
            {
                try
                {
                    Names.Add(IsFake ? $"Player {Id}" : SteamFriends.GetFriendPersonaName(new CSteamID(Id)));
                }
                catch (Exception)
                {
                    Names.Add(Id.ToString());
                }
            }
            return Names;
        }

        /// <summary>
        /// True while we are a player who joined someone through the bridge from our own loaded save.
        /// </summary>
        public static bool IsJoinedClient { get; private set; }

        /// <summary>
        /// Reads the command line, registers the Steam callbacks and starts the pump.
        /// </summary>
        public static void Initialize()
        {
            string[] Args = Environment.GetCommandLineArgs();

            for (int Index = 0; Index < Args.Length; Index++)
            {
                string Arg = Args[Index];

                if (Arg.StartsWith("-cotlmp-fakesteam=", StringComparison.OrdinalIgnoreCase))
                    ulong.TryParse(Arg.Substring("-cotlmp-fakesteam=".Length), out FakeSteamId);
                else if (Arg.StartsWith("-cotlmp-join=", StringComparison.OrdinalIgnoreCase))
                    ulong.TryParse(Arg.Substring("-cotlmp-join=".Length), out PendingFakeHost);
                /* Steam starts the game with "+connect_lobby <id>" when an invite is accepted while it isn't running */
                else if (Arg.Equals("+connect_lobby", StringComparison.OrdinalIgnoreCase) && Index + 1 < Args.Length)
                    ulong.TryParse(Args[Index + 1], out PendingLobby);
            }

            if (!IsFake)
            {
                try
                {
                    JoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
                    LobbyChanged = Callback<LobbyChatUpdate_t>.Create(OnLobbyChanged);
                    LobbyCreatedResult = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);
                    LobbyEnterResult = CallResult<LobbyEnter_t>.Create(OnLobbyEntered);

                    /* Friends know each other by their Steam names, use it unless the player picked one */
                    if (Plugin.Globals.PlayerName == "The Player")
                        Plugin.Globals.PlayerName = SteamFriends.GetPersonaName();
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogError($"[STEAM] Steam isn't available, Steam invites are disabled: {e.Message}");
                }
            }
            else
            {
                Plugin.Logger?.LogWarning($"[STEAM] Using the loopback stand-in for Steam as peer {FakeSteamId}");
            }

            /* A joined player that lost the host goes on with their own game */
            Network.OnDisconnect += () =>
            {
                if (IsJoinedClient)
                    PauseMenuPatches.EndSession("Multiplayer/Steam/HostLeft");
                else if (!IsHost)
                    Shutdown();
            };

            Plugin.MonoInstance.StartCoroutine(Pump());
        }

        private static IPeerLink CreateLink()
        {
            if (IsFake)
                return new LoopbackPeerLink(FakeSteamId);
            return new SteamPeerLink();
        }

        /* HOST ***************************************************************/

        /// <summary>
        /// Opens the integrated server to Steam friends.
        /// </summary>
        /// <param name = "Port">The UDP port of the integrated server.</param>
        public static void StartHost(int Port)
        {
            Shutdown();

            try
            {
                Link = CreateLink();
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogError($"[STEAM] Can't open the Steam link, friends can't join: {e}");
                return;
            }

            IsHost = true;
            ServerPort = Port;
            Link.AllowPeer = IsLobbyMember;

            if (!IsFake)
            {
                LobbyCreatedResult.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, Math.Max(2, Plugin.Globals.MaxNumPlayers)));
            }

            Plugin.Logger?.LogInfo($"[STEAM] Hosting as {Link.LocalId}, relaying to server port {Port}");
        }

        private static void OnLobbyCreated(LobbyCreated_t Created, bool IOFailure)
        {
            if (IOFailure || Created.m_eResult != EResult.k_EResultOK || !IsHost || Link == null)
            {
                Plugin.Logger?.LogError($"[STEAM] Creating the lobby failed: {Created.m_eResult} (IO failure {IOFailure})");
                return;
            }

            Lobby = new CSteamID(Created.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(Lobby, "cotlmp", Application.version);
            SteamMatchmaking.SetLobbyJoinable(Lobby, true);
            SteamFriends.SetRichPresence("connect", "+connect_lobby " + Lobby.m_SteamID);
            Plugin.Logger?.LogInfo($"[STEAM] Lobby {Lobby.m_SteamID} is open");
            if (InviteOnOpen)
                SteamFriends.ActivateGameOverlayInviteDialog(Lobby);
        }

        /// <summary>
        /// Opens the Steam overlay invite dialog for the current lobby.
        /// </summary>
        public static void OpenInviteDialog()
        {
            if (!IsFake && Lobby != CSteamID.Nil)
                SteamFriends.ActivateGameOverlayInviteDialog(Lobby);
        }

        private static bool IsLobbyMember(ulong Peer)
        {
            if (IsFake)
                return true;
            if (Lobby == CSteamID.Nil)
                return false;

            int Count = SteamMatchmaking.GetNumLobbyMembers(Lobby);
            for (int Index = 0; Index < Count; Index++)
            {
                if (SteamMatchmaking.GetLobbyMemberByIndex(Lobby, Index).m_SteamID == Peer)
                    return true;
            }

            return false;
        }

        private static void OnLobbyChanged(LobbyChatUpdate_t Update)
        {
            const uint Gone = (uint)(EChatMemberStateChange.k_EChatMemberStateChangeLeft |
                                     EChatMemberStateChange.k_EChatMemberStateChangeDisconnected |
                                     EChatMemberStateChange.k_EChatMemberStateChangeKicked |
                                     EChatMemberStateChange.k_EChatMemberStateChangeBanned);

            if (Update.m_ulSteamIDLobby != Lobby.m_SteamID || (Update.m_rgfChatMemberStateChange & Gone) == 0)
                return;

            Plugin.Logger?.LogInfo($"[STEAM] {Update.m_ulSteamIDUserChanged} left the lobby");
            if (IsHost)
                DropPeer(Update.m_ulSteamIDUserChanged);
        }

        private static HostPeer GetPeer(ulong Id)
        {
            if (Peers.TryGetValue(Id, out HostPeer Peer))
                return Peer;

            Peer = new HostPeer { Socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)) };
            Peer.Socket.Client.IOControl(-1744830452, new byte[] { 0 }, null);
            Peer.Socket.Client.ReceiveBufferSize = LocalBufferSize;
            Peer.Socket.Connect(IPAddress.Loopback, ServerPort);
            Peers[Id] = Peer;
            _ = System.Threading.Tasks.Task.Run(() => HostPeerLoop(Peer));
            Plugin.Logger?.LogInfo($"[STEAM] Player {Id} connected through Steam");
            return Peer;
        }

        private static async System.Threading.Tasks.Task HostPeerLoop(HostPeer Peer)
        {
            while (true)
            {
                try
                {
                    UdpReceiveResult Result = await Peer.Socket.ReceiveAsync();
                    Peer.ToRemote.Enqueue(Result.Buffer);
                    System.Threading.Interlocked.Increment(ref FromLocal);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException)
                {
                }
            }
        }

        private static void DropPeer(ulong Id)
        {
            if (!Peers.TryGetValue(Id, out HostPeer Peer))
                return;

            Peer.Socket.Dispose();
            Peers.Remove(Id);
            Link?.Close(Id);
        }

        /* CLIENT *************************************************************/

        private static void OnJoinRequested(GameLobbyJoinRequested_t Request)
        {
            Plugin.Logger?.LogInfo($"[STEAM] Join requested for lobby {Request.m_steamIDLobby.m_SteamID} (friend {Request.m_steamIDFriend.m_SteamID})");
            PendingLobby = Request.m_steamIDLobby.m_SteamID;
            if (InPrologue)
                Notify("Multiplayer/Steam/AfterPrologue");
        }

        /// <summary>
        /// The game's opening (before the crown and the cult): scripted rooms, enemies and
        /// talks the shared runs don't fit, and the game keeps local co-op out of it too
        /// </summary>
        internal static bool InPrologue => SceneManager.GetActiveScene().name == "Game Biome Intro";

        /// <summary>
        /// A join waits until the player stands in their own loaded game.
        /// </summary>
        /// <remarks>
        /// The game only unpauses a scene after a save is loaded into it, so joining
        /// from the main menu straight into the cult leaves the game frozen. The
        /// joining player loads their own save and the join goes ahead from there.
        /// </remarks>
        private static bool ReadyToJoin()
        {
            /* From the main menu straight into the host's game, as the game's co-op */
            if (SceneManager.GetActiveScene().name == "Main Menu")
                return !Plugin.GlobalsInternal.InGameSession && !MMTransition.IsPlaying;
            return PlayerFarming.Instance != null &&
                   !InPrologue &&
                   !Plugin.GlobalsInternal.InGameSession &&
                   !MMTransition.IsPlaying &&
                   Time.timeScale > 0f;
        }

        private static void Notify(string LocKey)
        {
            try
            {
                NotificationCentre.Instance?.PlayGenericNotification(LocKey);
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[STEAM] Can't show notification {LocKey}: {e.Message}");
            }
        }

        private static void OnLobbyEntered(LobbyEnter_t Entered, bool IOFailure)
        {
            if (IOFailure || Entered.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                Plugin.Logger?.LogError($"[STEAM] Joining the lobby failed: response {Entered.m_EChatRoomEnterResponse} (IO failure {IOFailure})");
                Joining = false;
                return;
            }

            Lobby = new CSteamID(Entered.m_ulSteamIDLobby);
            string Version = SteamMatchmaking.GetLobbyData(Lobby, "cotlmp");
            if (Version != Application.version)
            {
                Plugin.Logger?.LogError($"[STEAM] The host runs game version '{Version}', we run '{Application.version}'");
                PauseMenuPatches.Message = string.Format(I2.Loc.LocalizationManager.GetTranslation("Multiplayer/Steam/GameVersion"), Version, Application.version);
                SteamMatchmaking.LeaveLobby(Lobby);
                Lobby = CSteamID.Nil;
                Joining = false;
                return;
            }

            ConnectToHost(SteamMatchmaking.GetLobbyOwner(Lobby).m_SteamID);
        }

        private static async void ConnectToHost(ulong Host)
        {
            bool Connected;

            try
            {
                Link = CreateLink();
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogError($"[STEAM] Can't open the Steam link: {e}");
                Joining = false;
                return;
            }

            IsHost = false;
            HostId = Host;
            Link.AllowPeer = Peer => Peer == HostId;
            GameClient = null;
            Proxy = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            Proxy.Client.IOControl(-1744830452, new byte[] { 0 }, null);
            Proxy.Client.ReceiveBufferSize = LocalBufferSize;
            _ = System.Threading.Tasks.Task.Run(ProxyLoop);

            int ProxyPort = ((IPEndPoint)Proxy.Client.LocalEndPoint).Port;
            Plugin.Logger?.LogInfo($"[STEAM] Connecting to host {Host} through local proxy port {ProxyPort}");

            var Connecting = Network.Connect(new IPEndPoint(IPAddress.Loopback, ProxyPort), PauseMenuPatches.tokenSource.Token);
            if (await System.Threading.Tasks.Task.WhenAny(Connecting, System.Threading.Tasks.Task.Delay(HandshakeTimeoutMs)) != Connecting)
            {
                /* Cancelling the token disposes the network client, which ends the pending handshake */
                PauseMenuPatches.tokenSource.Cancel();
                PauseMenuPatches.tokenSource = new();
            }

            Connected = await Connecting;
            Joining = false;
            if (!Connected)
            {
                Plugin.Logger?.LogError($"[STEAM] The host {Host} didn't answer the handshake");
                Notify("Multiplayer/Steam/JoinFailed");
                Shutdown();
                return;
            }

            /* We are already in our own loaded game, so stay in this scene and tell the server where we are */
            IsJoinedClient = true;
            Plugin.GlobalsInternal.InGameSession = true;
            COTLMP.Ui.SayChat.StartSayChat();
            Network.AnnounceCurrentScene();
            COTLMP.Game.Appearance.Refresh();
            COTLMP.Game.CoopSync.SayHello();
            COTLMP.Game.VersionCheck.Announce();
            Notify("Multiplayer/Steam/Joined");
            Plugin.Logger?.LogInfo($"[STEAM] Joined host {Host} in scene {SceneManager.GetActiveScene().name}");

            /* Then into the host's game, on its save */
            COTLMP.Game.HostSave.BeginJoin();
        }

        private static async System.Threading.Tasks.Task ProxyLoop()
        {
            UdpClient Socket = Proxy;

            while (true)
            {
                try
                {
                    UdpReceiveResult Result = await Socket.ReceiveAsync();
                    GameClient = Result.RemoteEndPoint;
                    ToHost.Enqueue(Result.Buffer);
                    System.Threading.Interlocked.Increment(ref FromLocal);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException)
                {
                }
            }
        }

        /* COMMON *************************************************************/

        /// <summary>
        /// Moves packets between Steam and the local sockets once per frame,
        /// so every Steam call happens on the main thread.
        /// </summary>
        private static IEnumerator Pump()
        {
            while (true)
            {
                yield return null;

                try
                {
                    StartPendingJoin();
                    KeepRunningInBackground();
                    PumpOnce();
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogError($"[STEAM] Relay error: {e}");
                }
            }
        }

        private static void StartPendingJoin()
        {
            if (Joining || (PendingLobby == 0 && PendingFakeHost == 0))
                return;

            /*
             * The invite came while we had our own game open to friends with nobody in it:
             * close it and go to the friend. With a partner in it, the player leaves first.
             */
            if (IsHost && Plugin.GlobalsInternal.IsServerCreator)
            {
                if ((COTLMP.Ui.PauseMenuPatches.Server?.PlayerCount ?? 0) >= 2)
                {
                    if (!ToldToLeave)
                        Notify("Multiplayer/Steam/LeaveFirst");
                    ToldToLeave = true;
                    return;
                }
                Plugin.Logger?.LogInfo("[STEAM] Invite accepted while hosting alone: closing our own game to join the friend");
                COTLMP.Ui.PauseMenuPatches.EndSession(null);
            }

            if (Active || !ReadyToJoin())
                return;
            ToldToLeave = false;

            Joining = true;
            if (IsFake)
            {
                ulong Host = PendingFakeHost;
                PendingFakeHost = 0;
                ConnectToHost(Host);
            }
            else if (LobbyEnterResult != null)
            {
                Plugin.Logger?.LogInfo($"[STEAM] Joining lobby {PendingLobby}");
                LobbyEnterResult.Set(SteamMatchmaking.JoinLobby(new CSteamID(PendingLobby)));
                PendingLobby = 0;
            }
            else
            {
                PendingLobby = 0;
                Joining = false;
            }
        }

        private static void PumpOnce()
        {
            if (Link == null)
                return;

            while (Link.TryReceive(out ulong From, out byte[] Data))
            {
                if (Same(Data, SelfTest))
                {
                    Plugin.Logger?.LogInfo($"[STEAM] Self-test packet came back from {From} after {(Time.realtimeSinceStartup - SelfTestSentAt) * 1000f:F0} ms, Steam messaging works");
                    continue;
                }

                if (IsGoodbye(Data))
                {
                    OnGoodbye(From);
                    if (Link == null)
                        return;
                    continue;
                }

                ++FromLink;
                if (IsHost)
                {
                    GetPeer(From).Socket.Send(Data, Data.Length);
                }
                else if (From == HostId && GameClient != null)
                {
                    Proxy.Send(Data, Data.Length, GameClient);
                }
            }

            /* From the server a relayed message has the sender's ID before the co-op data */
            if (IsHost)
            {
                foreach (var Pair in Peers)
                {
                    while (Pair.Value.ToRemote.TryDequeue(out byte[] Out))
                        Link.Send(Pair.Key, Out, !IsStream(Out, sizeof(uint)));
                }
            }
            else
            {
                while (ToHost.TryDequeue(out byte[] Out))
                    Link.Send(HostId, Out, !IsStream(Out, 0));
            }
        }

        /// <summary>
        /// Whether a COTLMP packet only carries the latest state of something and may be lost.
        /// </summary>
        /// <param name = "Packet">The serialized message.</param>
        /// <param name = "KindOffset">Where the co-op sync kind lies in the data of a relay message.</param>
        private static bool IsStream(byte[] Packet, int KindOffset)
        {
            /* Big ones stay reliable: Steam would drop all of an unreliable one if any fragment got lost */
            if (Packet.Length <= Message.SerializedSize || Packet.Length > UnreliableMaxSize ||
                BitConverter.ToInt32(Packet, 0) != Message.MagicNumber)
                return false;
            return Message.IsStream((MessageType)BitConverter.ToInt32(Packet, sizeof(int)), Packet, Message.SerializedSize + sizeof(int) + KindOffset);
        }

        private const int UnreliableMaxSize = 1100;

        /* Counters for the test harness report: packets from the local sockets and from the link */
        internal static int FromLocal, FromLink;

        /// <summary>
        /// Keeps the game running while it has no focus during a session.
        /// </summary>
        /// <remarks>
        /// In exclusive full screen the game stops when it loses focus (SettingsManager), and a stopped
        /// game stops relaying, so the partner would be dropped 30 s after an Alt+Tab.
        /// </remarks>
        private static void KeepRunningInBackground()
        {
            if (Link != null && !Application.runInBackground)
            {
                if (!ForcedBackground)
                    Plugin.Logger?.LogInfo("[STEAM] Keeping the game running without focus during the session");
                ForcedBackground = true;
                Application.runInBackground = true;
            }
        }

        private static bool ForcedBackground;

        private static bool IsGoodbye(byte[] Data)
        {
            return Same(Data, Goodbye);
        }

        private static bool Same(byte[] Data, byte[] Marker)
        {
            if (Data.Length != Marker.Length)
                return false;
            for (int Index = 0; Index < Data.Length; Index++)
            {
                if (Data[Index] != Marker[Index])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Sends a marker packet to our own SteamID through the open link, for tests.
        /// Steam loops it back, which exercises the send and receive paths without a second account.
        /// </summary>
        public static void SendSelfTest()
        {
            if (Link == null)
            {
                Plugin.Logger?.LogWarning("[STEAM] Self-test skipped, no link is open");
                return;
            }

            SelfTestSentAt = Time.realtimeSinceStartup;
            Link.Send(Link.LocalId, SelfTest);
            Plugin.Logger?.LogInfo($"[STEAM] Self-test packet sent to {Link.LocalId}");
        }

        /// <summary>
        /// The other side left: hand our game a Disconnect as if it came over the wire.
        /// </summary>
        /// <remarks>
        /// Both the server and the client accept a Disconnect whatever its sequence number.
        /// </remarks>
        private static void OnGoodbye(ulong From)
        {
            byte[] Disconnect = new Message(MessageType.Disconnect, 0, Encoding.UTF8.GetBytes("Disconnected")).Serialize();

            Plugin.Logger?.LogInfo($"[STEAM] {From} said goodbye");
            if (IsHost)
            {
                if (Peers.TryGetValue(From, out HostPeer Peer))
                    Peer.Socket.Send(Disconnect, Disconnect.Length);
                DropPeer(From);
            }
            else if (From == HostId && GameClient != null)
            {
                Proxy.Send(Disconnect, Disconnect.Length, GameClient);
            }
        }

        /// <summary>
        /// Closes the lobby, the relay sockets and the Steam sessions.
        /// </summary>
        public static void Shutdown()
        {
            if (Link == null && Lobby == CSteamID.Nil)
                return;

            Plugin.Logger?.LogInfo("[STEAM] Shutting down the Steam relay");

            /*
             * Say goodbye first. The Steam sessions aren't closed here because closing
             * one drops what is still queued on it; idle sessions close by themselves.
             */
            try
            {
                if (IsHost)
                {
                    foreach (ulong Id in Peers.Keys)
                        Link?.Send(Id, Goodbye);
                }
                else if (HostId != 0)
                {
                    Link?.Send(HostId, Goodbye);
                }
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[STEAM] Couldn't say goodbye: {e.Message}");
            }

            foreach (ulong Id in new List<ulong>(Peers.Keys))
            {
                Peers[Id].Socket.Dispose();
                Peers.Remove(Id);
            }

            Proxy?.Dispose();
            Proxy = null;
            GameClient = null;
            while (ToHost.TryDequeue(out _)) ;

            if (!IsFake && Lobby != CSteamID.Nil)
            {
                SteamMatchmaking.LeaveLobby(Lobby);
                SteamFriends.ClearRichPresence();
            }

            Lobby = CSteamID.Nil;
            Link?.Shutdown();
            Link = null;

            /* Back to the game's own choice */
            if (ForcedBackground)
            {
                ForcedBackground = false;
                Application.runInBackground = ScreenUtilities.GetFullScreenMode() != FullScreenMode.ExclusiveFullScreen;
            }
            HostId = 0;
            IsHost = false;
            IsJoinedClient = false;
        }
    }
}

/* EOF */
