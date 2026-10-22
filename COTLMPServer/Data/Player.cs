/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Define the Player class
 * COPYRIGHT:	Copyright 2025 Neco-Arc <neco-arc@inbox.ru>
 */

/* IMPORTS ********************************************************************/

using COTLMPServer.Messages;
using System.Threading;

/* CLASSES & CODE *************************************************************/

/**
 * @brief
 * Contains the classes/structs/enums for the server
 */
namespace COTLMPServer.Data
{
    /// <summary>
    /// The server-side representation of a player
    /// </summary>
    internal class Player
    {
        public uint ID;
        public int Skin;
        public string Username;
        public string Biome;
        public PlayerState State;
        public CancellationTokenSource Cancellation;
        /// <summary>
        /// This lock protects Sequence and Lag
        /// </summary>
        public readonly object Lock;
        public bool Lag;
        /// <summary>
        /// The sequence number that should be used for the <b>next</b> message
        /// </summary>
        public uint Sequence;
        /// <summary>
        /// The lowest sequence number still accepted from this player
        /// </summary>
        /// <remarks>
        /// Kept apart from Sequence: sharing one counter for both directions made
        /// every message older than the last one sent look stale, so the side that
        /// sent less lost most of what it received.
        /// </remarks>
        public uint RecvSequence;

        public Player(uint id, int skin, string username, string biome, PlayerState state, CancellationTokenSource cancellation)
        {
            ID = id;
            Skin = skin;
            State = state;
            Biome = biome;
            Username = username;
            Cancellation = cancellation;
            Lock = new object();
            Lag = false;
            Sequence = 3; // start at 3 because of handshake
        }
    }
}
