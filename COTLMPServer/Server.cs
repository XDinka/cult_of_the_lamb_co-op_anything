/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Define COTLMP server class
 * COPYRIGHT:	Copyright 2025 Neco-Arc <neco-arc@inbox.ru>
 */

/* IMPORTS ********************************************************************/

using COTLMPServer.Data;
using COTLMPServer.Messages;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/* CLASSES & CODE *************************************************************/

namespace COTLMPServer
{
    /// <summary>
    /// This class implements the functionality of a server
    /// </summary>
    public sealed class Server : IDisposable
    {
        public readonly int Port;

        /// <summary>
        /// How many players are connected, the host's own game included
        /// </summary>
        public int PlayerCount => players.Count;
        public event EventHandler<ServerStoppedArgs> ServerStopped;

        private volatile int running;
        private volatile bool disposedValue;
        private readonly UdpClient client;
        private readonly CancellationToken token;
        private readonly ILogger logger;
        private readonly string gameVersion;
        private readonly ConcurrentDictionary<IPEndPoint, Player> players;
        private readonly SemaphoreSlim sendLock;
        private readonly bool[] ids;
        private readonly object idLock;
        private int handlerErrors;

        public Server(string ver, int maxPlayers, IPEndPoint endPoint, CancellationToken? cancellationToken = null, ILogger log = null)
        {
            if (endPoint == null)
            {
                throw new ArgumentNullException(nameof(endPoint));
            }

            client = new UdpClient(endPoint);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                client.Client.IOControl(-1744830452, new byte[] { 0 }, null);
            /* Bursts from a player who just got their connection back must not overflow it */
            client.Client.ReceiveBufferSize = 4 << 20;

            logger = log;
            running = 0;
            players = new ConcurrentDictionary<IPEndPoint, Player>();

            Port = (client.Client.LocalEndPoint as IPEndPoint)?.Port ?? 0;

            gameVersion = ver;

            if (cancellationToken == null)
                token = CancellationToken.None;
            else
                token = cancellationToken.Value;

            sendLock = new SemaphoreSlim(1, 1);
            ids = new bool[maxPlayers];
            idLock = new object();
        }

        /// <summary>
        /// A method responsible for disconnecting inresponsive players
        /// </summary>
        /// <param name="plr">
        /// The player that should be monitored
        /// </param>
        /// <param name="endpoint">
        /// The endpoint of the player
        /// </param>
        private async Task PlayerHeartBeat(Player plr, IPEndPoint endpoint)
        {
            try
            {
                while (!plr.Cancellation.Token.IsCancellationRequested)
                {
                    bool disconnect = false;
                    lock (plr.Lock)
                    {
                        if (plr.Lag)
                        {
                            disconnect = true;
                        }
                        plr.Lag = true;
                    }
                    if (disconnect)
                    {
                        await DisconnectPlayer(endpoint, "Timed out");
                        break;
                    }
                    await Task.Delay(30000, plr.Cancellation.Token);
                }
            }
            finally
            {
                plr.Cancellation.Dispose();
            }
        }

        /// <summary>
        /// Safely concurrently send bytes to an endpoint
        /// </summary>
        /// <param name="endPoint">
        /// The endpoint to send the bytes to
        /// </param>
        /// <param name="data">
        /// The bytes to send
        /// </param>
        private async Task Send(IPEndPoint endPoint, byte[] data)
        {
            await sendLock.WaitAsync(token);
            try
            {
                await client.SendAsync(data, data.Length, endPoint);
            } // don't catch anything, let the caller do it
            finally
            {
                sendLock.Release();
            }
        }

        /// <summary>
        /// Disconnect a player
        /// </summary>
        /// <param name="message">
        /// The message to attach to the disconnect message
        /// </param>
        /// <param name="endPoint">
        /// The endpoint of the player
        /// </param>
        /// <param name="tell">
        /// Whether to send the endpoint the disconnect message (not to a player coming back on it)
        /// </param>
        private async Task DisconnectPlayer(IPEndPoint endPoint, string message = null, bool tell = true)
        {
            Message msg = new Message(MessageType.Disconnect, 1, message == null ? null : Encoding.UTF8.GetBytes(message));
            if (players.TryRemove(endPoint, out var removed))
            {
                logger?.LogInfo($"{removed.Username} ({endPoint}) disconnected: {message ?? "No reason provided"}");
                removed.Cancellation.Cancel();
                msg.Sequence = removed.Sequence;
                lock (idLock)
                {
                    ids[removed.ID] = false;
                }
            }
            if (tell)
                await Send(endPoint, msg.Serialize());
            if (removed == null)
                return;
            await SendToBiome(removed.Biome, MessageType.PlayerLeft, BitConverter.GetBytes(BitConverter.IsLittleEndian ? removed.ID : Utils.ReverseEndianness(removed.ID)), null);
        }

        /// <summary>
        /// Send a message to all players within a biome
        /// </summary>
        /// <param name="name">
        /// The name of the biome
        /// </param>
        /// <param name="type">
        /// The message type
        /// </param>
        /// <param name="data">
        /// The data that should be included in the message
        /// </param>
        /// <param name="except">
        /// The player in the biome to which the message shouldn't be sent
        /// </param>
        private async Task SendToBiome(string name, MessageType type, byte[] data, Player except)
        {
            var pairs = players.ToArray().Where(p => p.Value.Biome == name);

            Message msg = new Message(type, 0, data);
            var tasks = new List<Task>();
            foreach (var pair in pairs)
            {
                if (pair.Value == except)
                    continue;

                byte[] bytes;
                lock (pair.Value.Lock)
                {
                    msg.Sequence = pair.Value.Sequence++;
                    bytes = msg.Serialize();
                }
                tasks.Add(Send(pair.Key, bytes));
            }
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Get all player objects within a given biome.
        /// </summary>
        /// <param name="biome">The name of the biome</param>
        /// <returns>An IEnumerable object of all players within the biome.</returns>
        private IEnumerable<Player> GetPlayersFromBiome(string biome) => players.Values.Where((plr) => plr.Biome == biome);

        /// <summary>
        /// Main server logic
        /// </summary>
        /// <remarks>
        /// Only one instance of this method can run at a time
        /// </remarks>
        public async Task Run()
        {
            if (disposedValue)
                throw new ObjectDisposedException(nameof(Server));

            if (Interlocked.CompareExchange(ref running, 1, 0) != 0)
            {
                return;
            }

            var args = new ServerStoppedArgs(ServerStopReason.NormalShutdown, "");
            CancellationTokenRegistration registration = token.Register(client.Dispose);
            logger?.LogInfo("Started server at port " + Port);

            try
            {
                while (true)
                {
                    token.ThrowIfCancellationRequested();

                    UdpReceiveResult result = await client.ReceiveAsync();

                    /* Nothing we send is this big, it is junk */
                    if (result.Buffer.Length > Message.MaxSize)
                        continue;

                    try
                    {
                        Message message = Message.Deserialize(result.Buffer);

                        if (players.TryGetValue(result.RemoteEndPoint, out var plr))
                        {
                            /* A late position is worthless next to a newer one, but a late event still happened */
                            if (message.Sequence < plr.RecvSequence && Message.IsStream(message.Type, message.Data, 0))
                                continue;
                            lock (plr.Lock)
                            {
                                if (message.Sequence >= plr.RecvSequence)
                                    plr.RecvSequence = message.Sequence + 1;
                                plr.Lag = false;
                            }
                        }
                        else if (message.Type != MessageType.Ping && message.Type != MessageType.Handshake)
                            continue;

                        switch (message.Type)
                        {
                            case MessageType.Handshake:
                            {
                                if (message.Sequence != 1)
                                    throw new InvalidDataException();

                                /*
                                 * The same address greeting us again: that player's game restarted (a crash
                                 * says no goodbye) and the Steam bridge gives a returning SteamID the same
                                 * socket. Let the old one go at once instead of after the 30 s timeout,
                                 * which would hold its slot and turn the new handshake away.
                                 */
                                if (plr != null)
                                {
                                    logger?.LogInfo($"{plr.Username} ({result.RemoteEndPoint}) is back, dropping the old connection");
                                    await DisconnectPlayer(result.RemoteEndPoint, "Reconnected", tell: false);
                                    plr = null;
                                }

                                var data = HandshakeClient.Deserialize(message.Data);

                                if (data.GameVersion != gameVersion)
                                {
                                    await DisconnectPlayer(result.RemoteEndPoint, $"Game version mismatch! server {gameVersion} client {data.GameVersion}");
                                    break;
                                }

                                uint id = 9999;

                                lock (idLock)
                                {
                                    for (uint i = 0; i < ids.Length; ++i)
                                    {
                                        if (!ids[i])
                                        {
                                            id = i;
                                            ids[i] = true;
                                            break;
                                        }
                                    }
                                }

                                if (id == 9999)
                                {
                                    await DisconnectPlayer(result.RemoteEndPoint, "The server is full!");
                                    break;
                                }

                                byte[] acceptBytes = new Message(
                                    MessageType.Handshake,
                                    2,
                                    (BitConverter.IsLittleEndian) ? BitConverter.GetBytes(id) : BitConverter.GetBytes(id).Reverse().ToArray()
                                    ).Serialize();

                                try
                                {
                                    await Send(result.RemoteEndPoint, acceptBytes);
                                }
                                catch
                                {
                                    lock (idLock)
                                        ids[id] = false;
                                    break;
                                }

                                var player = new Player(
                                    id,
                                    data.Skin,
                                    data.Username,
                                    "Base Biome 1", // the main cult
                                    new PlayerState(PlayerState.State.Idle, 0, 0, false, 0, new Vector3()),
                                    CancellationTokenSource.CreateLinkedTokenSource(token));
                                if (!players.TryAdd(result.RemoteEndPoint, player))
                                {
                                    lock (idLock)
                                        ids[id] = false;
                                    await DisconnectPlayer(result.RemoteEndPoint, "The server is full!");
                                    player.Cancellation.Dispose();
                                    break;
                                }

                                _ = PlayerHeartBeat(player, result.RemoteEndPoint);

                                logger?.LogInfo($"{player.Username} ({result.RemoteEndPoint}) joined the game");
                                break;
                            }

                            case MessageType.Transition:
                            {
                                await SendToBiome(plr.Biome, MessageType.PlayerLeft, BitConverter.GetBytes(BitConverter.IsLittleEndian ? plr.ID : Utils.ReverseEndianness(Utils.ReverseEndianness(plr.ID))), plr);

                                plr.Biome = Encoding.UTF8.GetString(message.Data);

                                await SendToBiome(plr.Biome, MessageType.StateUpdate, PlayerInfo.FromInternal(plr).Serialize(), plr);

                                Message msg;
                                lock (plr.Lock)
                                {
                                    msg = new Message(MessageType.StateUpdate, plr.Sequence++);
                                }

                                foreach (var inbiome in GetPlayersFromBiome(plr.Biome))
                                {
                                    /* The player already knows its own state, sending it makes the client spawn a copy of itself */
                                    if (inbiome == plr)
                                        continue;

                                    msg.Data = PlayerInfo.FromInternal(inbiome).Serialize();
                                    await Send(result.RemoteEndPoint, msg.Serialize());
                                    lock (plr.Lock)
                                    {
                                        msg.Sequence = plr.Sequence++;
                                    }
                                }

                                break;
                            }

                            case MessageType.PositionUpdate:
                            {
                                plr.State.Position = Vector3.Deserialize(message.Data, 0, out _);
                                byte[] bytes = new byte[sizeof(uint) + Vector3.SerializedSize];
                                Array.Copy(BitConverter.GetBytes(BitConverter.IsLittleEndian ? plr.ID : Utils.ReverseEndianness(plr.ID)), bytes, sizeof(uint));
                                Array.Copy(message.Data, 0, bytes, sizeof(uint), Vector3.SerializedSize);

                                await SendToBiome(plr.Biome, MessageType.PositionUpdate, bytes, plr);
                                break;
                            }

                            case MessageType.StateUpdate:
                            {
                                var info = PlayerState.Deserialize(message.Data);
                                plr.State = info;
                                await SendToBiome(plr.Biome, MessageType.StateUpdate, PlayerInfo.FromInternal(plr).Serialize(), plr);
                                break;
                            }

                            case MessageType.CustomAnimation:
                            {
                                CustomAnimationInfo.Deserialize(message.Data); // make sure the format is right, if its corrupt, it'll throw.
                                plr.State.Current = PlayerState.State.CustomAnimation;
                                await SendToBiome(plr.Biome, MessageType.CustomAnimation, message.Data, plr);
                                break;
                            }

                            case MessageType.Relay:
                            {
                                if (message.Data == null || message.Data.Length == 0)
                                    throw new InvalidDataException();

                                byte[] bytes = new byte[sizeof(uint) + message.Data.Length];
                                Array.Copy(BitConverter.GetBytes(BitConverter.IsLittleEndian ? plr.ID : Utils.ReverseEndianness(plr.ID)), bytes, sizeof(uint));
                                Array.Copy(message.Data, 0, bytes, sizeof(uint), message.Data.Length);

                                Message relay = new Message(MessageType.Relay, 0, bytes);
                                foreach (var pair in players.ToArray())
                                {
                                    if (pair.Value == plr)
                                        continue;

                                    byte[] outBytes;
                                    lock (pair.Value.Lock)
                                    {
                                        relay.Sequence = pair.Value.Sequence++;
                                        outBytes = relay.Serialize();
                                    }
                                    await Send(pair.Key, outBytes);
                                }
                                break;
                            }

                            case MessageType.Disconnect:
                            {
                                await DisconnectPlayer(result.RemoteEndPoint, "Disconnected");
                                break;
                            }

                            case MessageType.Ping:
                            {
                                uint seq = 0;
                                if (plr != null)
                                    lock (plr.Lock)
                                        seq = plr.Sequence++;
                                await Send(result.RemoteEndPoint, new Message(MessageType.Ping, seq).Serialize());
                                break;
                            }

                            default:
                            {
                                await DisconnectPlayer(result.RemoteEndPoint, "invalid message");
                                break;
                            }
                        }
                    }
                    catch (Exception e) when (e is InvalidDataException || e is ArgumentNullException)
                    {
                        /*
                         * A packet we can't read is skipped, not a reason to drop the player: one bad
                         * packet must not end a session. Strangers that never joined are ignored anyway.
                         */
                        if (++handlerErrors <= 20)
                            logger?.LogWarning($"Skipped invalid data from {result.RemoteEndPoint}: {e.Message}");
                    }
                    catch (SocketException e)
                    {
                        if (players.TryRemove(result.RemoteEndPoint, out var removed))
                        {
                            removed.Cancellation.Cancel();
                            lock (idLock)
                                ids[removed.ID] = false;
                            logger?.LogInfo($"{result.RemoteEndPoint} ({removed.Username}) disconnected: {e.Message}");
                        }
                    }
                    catch (Exception e) when (!(e is OperationCanceledException) && !token.IsCancellationRequested)
                    {
                        /* One bad message must not stop the server for everyone */
                        if (++handlerErrors <= 20)
                            logger?.LogError($"Handling a message from {result.RemoteEndPoint} failed: {e}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                logger?.LogInfo("Stopping server...");
            }
            catch (ObjectDisposedException) when (token.IsCancellationRequested)
            {
                logger?.LogInfo("Stopping server...");
            }
            catch (Exception e)
            {
                logger?.LogFatal(e.ToString());
                args.Reason = ServerStopReason.Error;
                args.What = e.ToString();
            }
            finally
            {
                registration.Dispose();
                client.Dispose();
                ServerStopped?.Invoke(this, args);
            }
        }

        /// <summary>
        /// Dispose of unmanaged resources
        /// </summary>
        /// <param name="disposing">
        /// Whether if the Dispose() method was called manually
        /// </param>
        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    client.Dispose();
                }
                disposedValue = true;
            }
        }

        ~Server()
        {
            Dispose(false);
        }

        /// <summary>
        /// Dispose of unmanaged resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}

/* EOF */
