/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Define the MessageTypes enum
 * COPYRIGHT:	Copyright 2025 Neco-Arc <neco-arc@inbox.ru>
 */

/* CLASSES & CODE *************************************************************/

namespace COTLMPServer.Messages
{
    /// <summary>
    /// Message type enum
    /// </summary>
    public enum MessageType
    {
        Ping,
        Handshake,
        Disconnect,
        PositionUpdate,
        StateUpdate,
        Transition,
        CustomAnimation,
        PlayerLeft,
        ServerInfo,
        /// <summary>
        /// Opaque co-op sync data, forwarded to every other player with the sender's ID in front
        /// </summary>
        Relay
    }
}

/* EOF */
