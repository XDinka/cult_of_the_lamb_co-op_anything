/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Define the network class
 * COPYRIGHT:	Copyright 2025 Neco-Arc <neco-arc@inbox.ru>
 */

/* IMPORTS ********************************************************************/

using COTLMP.Debug;
using COTLMP.Game;
using COTLMP.Ui;
using COTLMPServer.Messages;
using HarmonyLib;
using MMTools;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using COTLMP.Data;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Network
{
    /// <summary>
    /// The main class for network features
    /// </summary>
    internal static class Network
    {
        public static event Action OnDisconnect;
        public static bool IsConnected => online != 0;

        /* Counters for the test harness report */
        internal static int SentPositions, ReceivedPositions;
        internal static bool HasLocalPlayer => localPlayer != null;

        private static UdpClient client;
        private static PlayerFarming localPlayer;
        private static CancellationToken cancelToken;
        private static SemaphoreSlim sendLock;
        /// <summary>
        /// Whether the game is online or not
        /// </summary>
        private static int online;
        /// <summary>
        /// The cancellation token registration for disposing of the UdpClient once the token is cancelled
        /// </summary>
        private static CancellationTokenRegistration registration;
        private static object seqLock;
        private static uint sequence;
        /// <summary>
        /// The lowest sequence number still accepted from the server, kept apart from the outgoing one
        /// </summary>
        private static uint recvSequence;
        private static bool transitionHappened;
        /// <summary>
        /// The last place sent to the server, see CoopSync.CurrentPlace
        /// </summary>
        private static string lastPlace;
        /// <summary>
        /// A queue of messages to be processed by the main thread
        /// </summary>
        private static ConcurrentQueue<Message> messageQueue;

        /// <summary>
        /// Safely concurrently send messages
        /// </summary>
        /// <param name="msg">
        /// The message to send
        /// </param>
        /// <remarks>
        /// The sequence number in msg is ignored and is overwritten
        /// </remarks>
        private static async System.Threading.Tasks.Task Send(Message msg)
        {
            if (online == 0)
                return;

            await sendLock.WaitAsync(cancelToken);
            try
            {
                lock (seqLock)
                    msg.Sequence = sequence++;
                byte[] bytes = msg.Serialize();
                await client.SendAsync(bytes, bytes.Length);
            }
            finally
            {
                sendLock.Release();
            }
        }

        /// <summary>
        /// Sends co-op sync data that the server forwards to every other player
        /// </summary>
        /// <param name="data">The payload, read by CoopSync on the other side</param>
        internal static void SendRelay(byte[] data)
        {
            if (online == 0 || data == null || data.Length == 0)
                return;
            _ = Send(new Message(MessageType.Relay, 0, data));
        }

        /// <summary>
        /// Monitor if the server has stopped responding and send pings at regular intervals
        /// </summary>
        private static async System.Threading.Tasks.Task HeartBeat()
        {
            var ping = new Message(MessageType.Ping, 0);
            while (!cancelToken.IsCancellationRequested && online != 0)
            {
                _ = Send(ping);
                await System.Threading.Tasks.Task.Delay(10000, cancelToken);
            }
        }

        /// <summary>
        /// Run in a loop recieving messages from the client
        /// </summary>
        private static async System.Threading.Tasks.Task Recv()
        {
            try
            {
                while (!cancelToken.IsCancellationRequested && online != 0)
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);

                    var limit = System.Threading.Tasks.Task.Delay(30000, cts.Token);
                    var recv = client.ReceiveAsync();

                    if (await System.Threading.Tasks.Task.WhenAny(limit, recv) == limit)
                    {
                        /* Leaving cancels the delay too, that is not a timeout */
                        cancelToken.ThrowIfCancellationRequested();
                        throw new TimeoutException("Timed out");
                    }
                    cts.Cancel();

                    var result = await recv;

                    messageQueue.Enqueue(Message.Deserialize(result.Buffer));
                }
            }
            catch (OperationCanceledException)
            {
                PrintLogger.Print(DebugLevel.INFO_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, "Disconnecting...");
            }
            catch (Exception e)
            {
                PrintLogger.Print(DebugLevel.ERROR_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Recieve error type {e.GetType().Name} message {e.Message}");
                while (messageQueue.TryDequeue(out _)) ;
                messageQueue.Enqueue(new Message(MessageType.Disconnect, 0, Encoding.UTF8.GetBytes(e.Message)));
            }
        }

        /// <summary>
        /// Send position updates
        /// </summary>
        public static IEnumerator SendUpdates()
        {
            var wait = new WaitForSecondsRealtime(InternalData.updateFrequencySec);
            while (!cancelToken.IsCancellationRequested && online != 0)
            {
                /* One bad frame (a player destroyed by a scene change) must not end the updates for good */
                try
                {
                    SendUpdateOnce();
                }
                catch (Exception e)
                {
                    PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Sending an update failed: {e.Message}");
                    localPlayer = null;
                }
                yield return wait;
            }
        }

        private static Vector3 lastPosition;
        private static float lastPositionSentAt;

        /// <summary>
        /// Sends where we are (place and position) if it changed, and the position at least once a second
        /// </summary>
        private static void SendUpdateOnce()
        {
            /*
             * Unity objects are compared with ==, which knows about destroyed objects. The ?. operator
             * doesn't, and the static PlayerFarming.Instance can point at a destroyed player during a load.
             */
            if (localPlayer == null)
            {
                localPlayer = null;
                if (MMTransition.IsPlaying || PlayerFarming.Instance == null)
                    return;
                localPlayer = PlayerFarming.Instance;
                if (localPlayer.state != null)
                    localPlayer.state.OnStateChange += OnStateChanged;
            }

            if (MMTransition.IsPlaying)
                return;

            /*
             * Tell the server where we are whenever that changes. In a dungeon every room
             * shares one scene, so the room is part of the place, otherwise players in
             * different rooms would see each other at the same coordinates.
             */
            string place = CoopSync.CurrentPlace();
            if (place != null && (transitionHappened || place != lastPlace))
            {
                lastPlace = place;
                transitionHappened = false;
                _ = Send(new Message(MessageType.Transition, 0, Encoding.UTF8.GetBytes(place)));
                Plugin.Logger?.LogInfo($"[COOP] Now in {place}");
                lastPositionSentAt = 0f;
            }

            Vector3 position = localPlayer.transform.position;
            if ((lastPosition - position).sqrMagnitude > 0.0001f || Time.realtimeSinceStartup - lastPositionSentAt > 1f)
            {
                lastPosition = position;
                lastPositionSentAt = Time.realtimeSinceStartup;
                _ = Send(new Message(MessageType.PositionUpdate, 0, position.ToNetwork().Serialize()));
                ++SentPositions;
            }
        }

        /// <summary>
        /// Main message processing loop
        /// </summary>
        public static IEnumerator PollServer()
        {
            var wait = new WaitForTask(null);
            bool keepLooping = true;
            int processedThisFrame = 0;
            while (!cancelToken.IsCancellationRequested && keepLooping)
            {
                if (processedThisFrame == InternalData.maxProcessPerFrame)
                {
                    if (messageQueue.TryPeek(out _))
                        PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, "Can't keep up! Is the game overloaded?");
                    processedThisFrame = 0;
                    yield return null;
                }

                Message msg;
                while (!messageQueue.TryDequeue(out msg))
                {
                    /* Leaving cancels the token while nothing is arriving, notice it here */
                    if (cancelToken.IsCancellationRequested)
                        break;
                    processedThisFrame = 0;
                    yield return null;
                }

                if (msg == null)
                    break;

                ++processedThisFrame;

                lock (seqLock)
                {
                    if (msg.Sequence < recvSequence && msg.Type != MessageType.Disconnect)
                        continue;
                    if (msg.Sequence >= recvSequence)
                        recvSequence = msg.Sequence + 1;
                }

                try
                {
                    switch (msg.Type)
                    {
                        case MessageType.PositionUpdate:
                        {
                            if (msg.Data.Length < sizeof(uint) + COTLMPServer.Vector3.SerializedSize)
                                throw new InvalidDataException("data too small!");

                            uint id = BitConverter.ToUInt32(msg.Data, 0);
                            if (!BitConverter.IsLittleEndian) // the data in the message is in little endian, convert
                                id = COTLMPServer.Utils.ReverseEndianness(id);

                            var pos = COTLMPServer.Vector3.Deserialize(msg.Data, sizeof(uint), out _);
                            ++ReceivedPositions;

                            if (!PlayerManager.DoesPlayerExist(id) && PlayerFarming.Instance != null)
                                PlayerManager.CreatePlayer(id, pos.ToUnity());
                            else
                                PlayerManager.MovePlayer(id, pos.ToUnity(), 1); // just use 1 as a timeout to get smooth animation

                            break;
                        }

                        case MessageType.StateUpdate:
                        {
                            var plrinfo = PlayerInfo.Deserialize(msg.Data);

                            if (!PlayerManager.DoesPlayerExist(plrinfo.ID) && PlayerFarming.Instance != null)
                            {
                                PlayerManager.CreatePlayer(plrinfo.ID, plrinfo.State.Position.ToUnity(), plrinfo.Skin);
                                PlayerManager.SetPlayerState(plrinfo.ID, plrinfo.State.ToUnity());
                            }
                            else
                            {
                                /* A puppet first seen through a position update has the default skin */
                                PlayerManager.SetPlayerSkin(plrinfo.ID, plrinfo.Skin);
                                PlayerManager.MovePlayer(plrinfo.ID, plrinfo.State.Position.ToUnity(), 0);
                                PlayerManager.SetPlayerState(plrinfo.ID, plrinfo.State.ToUnity());
                            }

                            break;
                        }

                        case MessageType.PlayerLeft:
                        {
                            if (msg.Data.Length < sizeof(uint))
                                throw new InvalidDataException("data too small!");

                            uint id = BitConverter.ToUInt32(msg.Data, 0);
                            if (!BitConverter.IsLittleEndian)
                                id = COTLMPServer.Utils.ReverseEndianness(id);

                            PlayerManager.DeletePlayer(id);
                            break;
                        }

                        case MessageType.CustomAnimation:
                        {
                            var info = CustomAnimationInfo.Deserialize(msg.Data);

                            if (!PlayerManager.DoesPlayerExist(info.ID) && PlayerFarming.Instance != null)
                                PlayerManager.CreatePlayer(info.ID, info.Position.ToUnity());
                            else
                                PlayerManager.MovePlayerNow(info.ID, info.Position.ToUnity());

                            PlayerManager.SetPlayerState(info.ID, null, true, info.Name, info.Loop);
                            break;
                        }

                        case MessageType.Relay:
                        {
                            if (msg.Data == null || msg.Data.Length <= sizeof(uint))
                                throw new InvalidDataException("data too small!");

                            uint id = BitConverter.ToUInt32(msg.Data, 0);
                            if (!BitConverter.IsLittleEndian)
                                id = COTLMPServer.Utils.ReverseEndianness(id);

                            byte[] payload = new byte[msg.Data.Length - sizeof(uint)];
                            Array.Copy(msg.Data, sizeof(uint), payload, 0, payload.Length);
                            CoopSync.OnRelay(id, payload);
                            break;
                        }

                        case MessageType.Disconnect:
                        {
                            keepLooping = false;
                            if (msg.Data != null)
                                PauseMenuPatches.Message = Encoding.UTF8.GetString(msg.Data);
                            break;
                        }
                    }
                }
                catch (InvalidDataException e)
                {
                    PrintLogger.Print(DebugLevel.ERROR_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Server sent invalid data: {e.Message}");
                    PauseMenuPatches.Message = e.Message;
                    break;
                }
                catch (Exception e) when ((e is OperationCanceledException) || (e is ObjectDisposedException && cancelToken.IsCancellationRequested))
                {
                    PrintLogger.Print(DebugLevel.INFO_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, "Disconnected.");
                }
                catch (Exception e)
                {
                    PrintLogger.Print(DebugLevel.ERROR_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Unknown error {e.GetType().Name} {e}");
                    PauseMenuPatches.Message = e.Message;
                    break;
                }
            }

            Message disconnect = new Message(MessageType.Disconnect, 0, PauseMenuPatches.Message != null ? Encoding.UTF8.GetBytes(PauseMenuPatches.Message) : null);
            wait.what = Send(disconnect);
            yield return wait;

            client.Dispose();
            client = null;
            registration.Dispose();
            OnDisconnect?.Invoke();
            Interlocked.Exchange(ref online, 0);
            yield break;
        }

        /// <summary>
        /// Attempt to connect to a server
        /// </summary>
        /// <param name="server">
        /// The server's endpoint
        /// </param>
        /// <param name="token">
        /// The cancellation token to use for this connection
        /// </param>
        /// <returns>
        /// true if the connection succeeded, false otherwise
        /// </returns>
        public async static Task<bool> Connect(IPEndPoint server, CancellationToken token)
        {
            if (Interlocked.CompareExchange(ref online, 1, 0) != 0)
                return false;

            cancelToken = token;
            sequence = 3;
            recvSequence = 3;
            sendLock = new(1, 1);
            client = new();
            seqLock = new();
            messageQueue = new();
            transitionHappened = false;
            lastPlace = null;
            localPlayer = null;
            lastPositionSentAt = 0f;
            client.Connect(server);

            registration = cancelToken.Register(client.Dispose);

            byte[] handshakeBytes = new Message(MessageType.Handshake, 1, new HandshakeClient(Plugin.Globals.PlayerName, Application.version, Appearance.LocalSkin()).Serialize()).Serialize();

            try
            {
                await client.SendAsync(handshakeBytes, handshakeBytes.Length);

                Task<UdpReceiveResult> resultTask = client.ReceiveAsync();

                UdpReceiveResult result = await resultTask;
                if (result.Buffer.Length > 1500)
                    throw new Exception();

                Message msg = Message.Deserialize(result.Buffer);
                if (msg.Type != MessageType.Handshake || msg.Sequence != 2 || msg.Data.Length < sizeof(uint))
                {
                    if (msg.Type == MessageType.Disconnect && msg.Data != null)
                        PauseMenuPatches.Message = Encoding.UTF8.GetString(msg.Data);
                    throw new Exception();
                }

                Plugin.MonoInstance.StartCoroutine(PollServer());
                Plugin.MonoInstance.StartCoroutine(SendUpdates());
                _ = System.Threading.Tasks.Task.Run(Recv);
                _ = System.Threading.Tasks.Task.Run(HeartBeat);
                return true;
            }
            catch
            {
                client.Dispose();
                registration.Dispose();
                Interlocked.Exchange(ref online, 0);
                return false;
            }
        }

        /// <summary>
        /// If the scene loaded is main menu, stop the integrated server
        /// </summary>
        private static void OnSceneLoaded(Scene scene, LoadSceneMode _)
        {
            if (scene.name.Equals("Main Menu"))
            {
                // set the quitting flag temporarily so it doesn't try to transition to the main menu on server stop
                PauseMenuPatches.Quitting = true;
                PauseMenuPatches.StopServer();
                PauseMenuPatches.Quitting = false;
            }
        }

        /// <summary>
        /// On game quitting, stop the integrated server
        /// </summary>
        private static void OnQuitting()
        {
            PauseMenuPatches.Quitting = true;
            PauseMenuPatches.StopServer();
        }

        /// <summary>
        /// When the local player state changes, update the server
        /// </summary>
        /// <param name="newState">
        /// The new player state
        /// </param>
        private async static void OnStateChanged(StateMachine.State newState, StateMachine.State _)
        {
            if (newState == StateMachine.State.CustomAnimation || newState == StateMachine.State.Moving || newState == StateMachine.State.Idle || localPlayer == null)
                return;
            await Send(new Message(MessageType.StateUpdate, 0, localPlayer.state.ToNetwork(localPlayer.transform.position.ToNetwork()).Serialize()));
        }

        /// <summary>
        /// When a transition completes, update the server
        /// </summary>
        private static void OnTransitionComplete()
        {
            if (online == 0)
            {
                return;
            }

            string sceneName = SceneManager.GetActiveScene().name;
            if (sceneName != "Main Menu")
            {
                transitionHappened = true;
            }
            else
            {
                while (messageQueue.TryDequeue(out _)) ;
                messageQueue.Enqueue(new Message(MessageType.Disconnect,
                    0,
                    Encoding.UTF8.GetBytes("Disconnected")));
            }
        }

        /// <summary>
        /// When a transition begins, remove the localPlayer reference
        /// </summary>
        private static void OnBeginTransition()
        {
            if (online == 0)
            {
                return;
            }

            if (localPlayer != null && localPlayer.state != null)
                localPlayer.state.OnStateChange -= OnStateChanged;
            localPlayer = null;
        }

        /// <summary>
        /// Send the current scene to the server once the local player is known,
        /// for a player that connected while already standing in a scene
        /// </summary>
        internal static void AnnounceCurrentScene()
        {
            transitionHappened = true;
        }

        /// <summary>
        /// Initialize the network components
        /// </summary>
        public static void Initialize()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            Application.quitting += OnQuitting;
            MMTransition.OnTransitionCompelte += OnTransitionComplete;
            MMTransition.OnBeginTransition += OnBeginTransition;
        }

        /// <summary>
        /// Game patches related to networking
        /// </summary>
        [HarmonyPatch]
        private static class NetworkPatches
        {
            [HarmonyPatch(typeof(PlayerFarming), nameof(PlayerFarming.CustomAnimation))]
            [HarmonyPrefix]
            private static void CustomAnimation(string Animation, bool Loop, PlayerFarming __instance)
            {
                if (__instance != PlayerFarming.Instance)
                    return;

                var msg = new Message(MessageType.CustomAnimation,
                    0,
                    new CustomAnimationInfo(Animation,
                    loop: Loop,
                    pos: __instance.gameObject.transform.position.ToNetwork()).Serialize());

                _ = Send(msg);
            }

            [HarmonyPatch(typeof(PlayerFarming), nameof(PlayerFarming.CustomAnimationRoutine))]
            [HarmonyPrefix]
            private static void CustomAnimationRoutine(string Animation, bool Loop, PlayerFarming __instance)
            {
                if (__instance != PlayerFarming.Instance)
                    return;

                var msg = new Message(MessageType.CustomAnimation,
                    0,
                    new CustomAnimationInfo(Animation,
                    loop: Loop,
                    pos: __instance.gameObject.transform.position.ToNetwork()).Serialize());

                _ = Send(msg);
            }

            [HarmonyPatch(typeof(PlayerFarming), nameof(PlayerFarming.CustomAnimationWithCallback))]
            [HarmonyPrefix]
            private static void CustomAnimationWithCallback(string Animation, bool Loop, PlayerFarming __instance)
            {
                if (__instance != PlayerFarming.Instance)
                    return;

                var msg = new Message(MessageType.CustomAnimation,
                    0,
                    new CustomAnimationInfo(Animation,
                    loop: Loop,
                    pos: __instance.gameObject.transform.position.ToNetwork()).Serialize());

                _ = Send(msg);
            }

            [HarmonyPatch(typeof(PlayerFarming), nameof(PlayerFarming.CustomAnimationWithCallBackRoutine))]
            [HarmonyPrefix]
            private static void CustomAnimationWithCallbackRoutine(string Animation, bool Loop, PlayerFarming __instance)
            {
                if (__instance != PlayerFarming.Instance)
                    return;

                var msg = new Message(MessageType.CustomAnimation,
                    0,
                    new CustomAnimationInfo(Animation,
                    loop: Loop,
                    pos: __instance.gameObject.transform.position.ToNetwork()).Serialize());

                _ = Send(msg);
            }
        }
    }
}

/* EOF */
