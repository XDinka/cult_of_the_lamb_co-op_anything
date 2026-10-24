/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Steam avatars as textures
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using Steamworks;
using System;
using System.Collections.Generic;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Network
{
    /// <summary>
    /// The players' Steam avatars, for the lobby.
    /// </summary>
    /// <remarks>
    /// Steam loads a friend's avatar in the background, so a missing one is asked for again on the
    /// next call. The loopback stand-in for Steam has no avatars: a test player gets a plain square
    /// in the colour of the player's co-op ring (red for the Lamb, blue for the Goat).
    /// </remarks>
    internal static class SteamAvatars
    {
        private static readonly Dictionary<ulong, Texture2D> Known = new();

        /// <summary>
        /// The player's avatar, or null while Steam is still loading it
        /// </summary>
        internal static Texture2D Of(ulong Id, bool Lamb)
        {
            if (Id == 0)
                return null;
            if (Known.TryGetValue(Id, out Texture2D Found) && Found != null)
                return Found;

            Texture2D Made = SteamBridge.IsFake ? Plain(Lamb ? new Color(0.85f, 0.16f, 0.12f) : new Color(0.2f, 0.45f, 0.85f)) : FromSteam(Id);
            if (Made != null)
                Known[Id] = Made;
            return Made;
        }

        private static Texture2D FromSteam(ulong Id)
        {
            try
            {
                if (!SteamManager.Initialized)
                    return null;
                int Image = SteamFriends.GetLargeFriendAvatar(new CSteamID(Id));
                if (Image <= 0 || !SteamUtils.GetImageSize(Image, out uint Width, out uint Height) || Width == 0 || Height == 0)
                    return null;

                byte[] Pixels = new byte[Width * Height * 4];
                if (!SteamUtils.GetImageRGBA(Image, Pixels, Pixels.Length))
                    return null;

                /* Steam's rows go top down, a texture's bottom up */
                int Row = (int)Width * 4;
                byte[] Flipped = new byte[Pixels.Length];
                for (int Y = 0; Y < Height; Y++)
                    Buffer.BlockCopy(Pixels, Y * Row, Flipped, ((int)Height - 1 - Y) * Row, Row);

                Texture2D Texture = new((int)Width, (int)Height, TextureFormat.RGBA32, false);
                Texture.LoadRawTextureData(Flipped);
                Texture.Apply();
                return Texture;
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[STEAM] Can't read the avatar of {Id}: {e.Message}");
                return null;
            }
        }

        private static Texture2D Plain(Color Colour)
        {
            Texture2D Texture = new(8, 8, TextureFormat.RGBA32, false);
            Color[] Pixels = new Color[64];
            for (int Index = 0; Index < Pixels.Length; Index++)
                Pixels[Index] = Colour;
            Texture.SetPixels(Pixels);
            Texture.Apply();
            return Texture;
        }
    }
}

/* EOF */
