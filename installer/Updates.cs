/*
 * PROJECT:     Cult of the Lamb Co-op Anything installer
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     The latest release on GitHub, and its installer downloaded and checked
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace CotlCoopInstaller
{
    internal static class Updates
    {
        private const string Repo = "XDinka/cult_of_the_lamb_co-op_anything";
        private const string Api = "https://api.github.com/repos/" + Repo + "/releases/latest";
        internal const string Page = "https://github.com/" + Repo + "/releases/latest";
        private const string InstallerName = "CotlCoopInstaller.exe";

        internal sealed class Release
        {
            internal string Version, Url, Sha256;
        }

        private static WebClient Client()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            WebClient Web = new();
            Web.Headers[HttpRequestHeader.UserAgent] = "CotlCoopInstaller";
            Web.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
            return Web;
        }

        /// <summary>
        /// The latest release and its installer, or null when GitHub can't be asked
        /// </summary>
        internal static Release Latest()
        {
            try
            {
                using WebClient Web = Client();
                string Json = Web.DownloadString(Api);
                string Tag = Regex.Match(Json, "\"tag_name\"\\s*:\\s*\"v?([^\"]+)\"").Groups[1].Value;
                int Asset = Json.IndexOf($"\"name\":\"{InstallerName}\"", StringComparison.Ordinal);
                if (Tag.Length == 0 || Asset < 0)
                    return null;
                Match Url = new Regex("\"browser_download_url\"\\s*:\\s*\"([^\"]+)\"").Match(Json, Asset);
                if (!Url.Success)
                    return null;
                Match Digest = new Regex("\"digest\"\\s*:\\s*\"sha256:([0-9a-fA-F]{64})\"").Match(Json.Substring(0, Url.Index), Asset);
                return new Release { Version = Tag, Url = Url.Groups[1].Value, Sha256 = Digest.Success ? Digest.Groups[1].Value.ToLowerInvariant() : null };
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// A newer version than ours ("0.2.10" is newer than "0.2.9")
        /// </summary>
        internal static bool Newer(string Theirs, string Ours)
        {
            int[] Parse(string Text) => (Text ?? "").TrimStart('v', 'V').Split('.', '-').Select(Part => int.TryParse(Part, out int Number) ? Number : 0).ToArray();
            int[] A = Parse(Theirs), B = Parse(Ours);
            for (int Index = 0; Index < Math.Max(A.Length, B.Length); Index++)
            {
                int Left = Index < A.Length ? A[Index] : 0, Right = Index < B.Length ? B[Index] : 0;
                if (Left != Right)
                    return Left > Right;
            }
            return false;
        }

        /// <summary>
        /// The release's installer in the temp folder, checked against the checksum GitHub gives for it
        /// </summary>
        internal static string Download(Release Of)
        {
            using WebClient Web = Client();
            byte[] Bytes = Web.DownloadData(Of.Url);
            using (SHA256 Hasher = SHA256.Create())
            {
                string Got = BitConverter.ToString(Hasher.ComputeHash(Bytes)).Replace("-", "").ToLowerInvariant();
                if (Of.Sha256 != null && Got != Of.Sha256)
                    throw new InvalidDataException("the download doesn't match the release's checksum");
            }
            string Folder = Path.Combine(Path.GetTempPath(), "CotlCoopUpdate");
            Directory.CreateDirectory(Folder);
            string File = Path.Combine(Folder, $"CotlCoopInstaller-{Of.Version}.exe");
            System.IO.File.WriteAllBytes(File, Bytes);
            return File;
        }
    }
}
