/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Checks of the server's behaviour that the two-copy game test can't reach
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMPServer;
using COTLMPServer.Data;
using COTLMPServer.Messages;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

/* CLASSES & CODE *************************************************************/

namespace ServerChecks
{
    /// <summary>
    /// Runs the server in this process and talks to it over UDP like the game does.
    /// </summary>
    /// <remarks>
    /// Run with: dotnet run --project tests/ServerChecks. Exit code 0 when every check passes.
    /// </remarks>
    internal static class Program
    {
        private const string Version = "1.5.26.1058";
        private static int Failures;

        private static async Task<int> Main()
        {
            using CancellationTokenSource Stop = new();
            using Server Host = new(Version, 2, "checks", GameModes.GameMode.Standard, new IPEndPoint(IPAddress.Loopback, 0), Stop.Token);
            _ = Task.Run(Host.Run);
            IPEndPoint At = new(IPAddress.Loopback, Host.Port);

            using UdpClient A = new(new IPEndPoint(IPAddress.Loopback, 0));
            using UdpClient B = new(new IPEndPoint(IPAddress.Loopback, 0));
            A.Connect(At);
            B.Connect(At);

            /* Two players join */
            Check("player A is accepted", (await Handshake(A))?.Type == MessageType.Handshake);
            Check("player B is accepted", (await Handshake(B))?.Type == MessageType.Handshake);

            /* A relay bigger than one network frame reaches the other player */
            byte[] Big = new byte[20000];
            Big[0] = 250;
            Send(A, new Message(MessageType.Relay, 3, Big));
            Message Relayed = await Receive(B, MessageType.Relay);
            Check("a 20 KB relay arrives whole", Relayed != null && Relayed.Data.Length == Big.Length + sizeof(uint));

            /* Junk doesn't stop the server */
            A.Send(new byte[] { 1, 2, 3 }, 3);
            A.Send(new byte[70000 / 2], 35000);
            Send(A, new Message(MessageType.Relay, 4, new byte[] { 251 }));
            Check("the server still relays after junk", await Receive(B, MessageType.Relay) != null);

            /*
             * A's game restarts (a crash: no goodbye) and greets the server again from the same
             * address, as the Steam bridge makes it. It must be let in at once, not turned away
             * until the old connection times out.
             */
            Message Again = await Handshake(A);
            Check("a returning player is accepted again", Again?.Type == MessageType.Handshake);
            Check("B hears that the old A left", await Receive(B, MessageType.PlayerLeft) != null);

            /* And plays on: its next messages count from the start again */
            Send(A, new Message(MessageType.Relay, 3, new byte[] { 251, 1 }));
            Check("the returning player's relays arrive", await Receive(B, MessageType.Relay) != null);

            Stop.Cancel();
            Console.WriteLine(Failures == 0 ? "ALL PASSED" : $"{Failures} FAILED");
            return Failures == 0 ? 0 : 1;
        }

        private static void Check(string What, bool Passed)
        {
            Console.WriteLine($"{(Passed ? "ok  " : "FAIL")} {What}");
            if (!Passed)
                ++Failures;
        }

        private static void Send(UdpClient Client, Message Value)
        {
            byte[] Bytes = Value.Serialize();
            Client.Send(Bytes, Bytes.Length);
        }

        private static async Task<Message> Handshake(UdpClient Client)
        {
            Send(Client, new Message(MessageType.Handshake, 1, new HandshakeClient("checker", Version, 0).Serialize()));
            return await Receive(Client, MessageType.Handshake, MessageType.Disconnect);
        }

        /// <summary>
        /// The next message of one of the types, skipping others, or null after 3 s.
        /// </summary>
        private static async Task<Message> Receive(UdpClient Client, params MessageType[] Types)
        {
            DateTime Until = DateTime.UtcNow.AddSeconds(3);
            while (DateTime.UtcNow < Until)
            {
                Task<UdpReceiveResult> Next = Client.ReceiveAsync();
                if (await Task.WhenAny(Next, Task.Delay(Until - DateTime.UtcNow)) != Next)
                    return null;
                Message Value = Message.Deserialize((await Next).Buffer);
                if (Array.IndexOf(Types, Value.Type) >= 0)
                    return Value;
            }
            return null;
        }
    }
}

/* EOF */
