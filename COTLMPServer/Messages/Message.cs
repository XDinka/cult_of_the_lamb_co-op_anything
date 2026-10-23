/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Define the Message class
 * COPYRIGHT:	Copyright 2025 Neco-Arc <neco-arc@inbox.ru>
 */

/* IMPORTS ********************************************************************/

using System;
using System.IO;

/* CLASSES & CODE *************************************************************/

namespace COTLMPServer.Messages
{
    /// <summary>
    /// Message header
    /// </summary>
    public sealed class Message
    {
        public MessageType Type;
        public uint Sequence;
        /// <summary>
        /// Actual message data (depends on the message type)
        /// </summary>
        public byte[] Data;
        /// <summary>
        /// The magic number to be used for verification when sent over the network
        /// </summary>
        public const int MagicNumber = 0x173495;
        public const int SerializedSize = sizeof(int) * 3;
        /// <summary>
        /// The biggest message accepted. Co-op sync data (a room full of enemies) can be larger than
        /// one network frame; Steam and UDP split it up, and a dropped player is far worse than a big packet.
        /// </summary>
        public const int MaxSize = 60000;
        /// <summary>
        /// Co-op sync kinds that only carry the latest state (enemy and attack positions) or measure
        /// the connection (pings, which must be lost the way positions are), see CoopSync.Kind
        /// </summary>
        private const byte EnemySnapshotKind = 4, HazardMoveKind = 13, PingKind = 15, PongKind = 16, PuppetPoseKind = 22;

        /// <summary>
        /// Whether a message only carries the latest state of something, so a newer one makes it worthless.
        /// </summary>
        /// <param name="type">The message type</param>
        /// <param name="data">The message data</param>
        /// <param name="kindOffset">Where the co-op sync kind lies in the data of a relay message</param>
        /// <remarks>
        /// Only these may be dropped when they arrive late or get lost; everything else is an event
        /// (a kill, a pickup, a cleared room) that must arrive even if it comes after a newer message.
        /// </remarks>
        public static bool IsStream(MessageType type, byte[] data, int kindOffset)
        {
            if (type == MessageType.PositionUpdate)
                return true;
            return type == MessageType.Relay && data != null && data.Length > kindOffset &&
                   (data[kindOffset] == EnemySnapshotKind || data[kindOffset] == HazardMoveKind ||
                    data[kindOffset] == PingKind || data[kindOffset] == PongKind || data[kindOffset] == PuppetPoseKind);
        }

        /// <summary>
        /// Serialize the message into a byte array
        /// </summary>
        /// <returns>
        /// A byte array that represents the message
        /// </returns>
        /// <remarks>
        /// Type must be a value that the MessageType enum defines, Data can be null or an empty array
        /// </remarks>
        /// <exception cref="InvalidCastException">
        /// If any of the data in the class is invalid
        /// </exception>
        public byte[] Serialize()
        {
            if (!Enum.IsDefined(typeof(MessageType), Type))
                throw new InvalidDataException("Message type is not defined in the enum");
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream)) // ensure the stream and writer are disposed of once no longer needed
            {
                writer.Write(MagicNumber);
                writer.Write((int)Type);
                writer.Write(Sequence);
                if (Data?.Length > 0) // check if data is null or zero length
                {
                    writer.Write(Data.Length);
                    writer.Write(Data);
                }
                else
                    writer.Write(-1);
                return stream.ToArray();
            }
        }

        public Message(MessageType type, uint sequence, byte[] data = null)
        {
            Type = type;
            Data = data;
            Sequence = sequence;
        }

        /// <summary>
        /// Deserialize byte array back into a Message object
        /// </summary>
        /// <param name="data">
        /// The byte array
        /// </param>
        /// <returns>
        /// The resulting Message object
        /// </returns>
        /// <exception cref="InvalidDataException">
        /// If the data in the array is invalid
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// If any of the arguments are null
        /// </exception>
        public static Message Deserialize(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (data.Length < SerializedSize)
                throw new InvalidDataException("data is too small!");
            if (data.Length > MaxSize)
                throw new InvalidDataException("data is too big!");

            using (MemoryStream stream = new MemoryStream(data, false))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                if (reader.ReadInt32() != MagicNumber)
                    throw new InvalidDataException("data is not a message");
                MessageType type = (MessageType)reader.ReadInt32();
                if (!Enum.IsDefined(typeof(MessageType), type))
                    throw new InvalidDataException("Invalid message type");
                return new Message(type, reader.ReadUInt32(), Utils.ReadBytes(reader));
            }
        }
    }
}

/* EOF */
