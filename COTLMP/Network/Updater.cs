/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     A newer release on GitHub: offered in the main menu, installed by the installer
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using BepInEx;
using COTLMP.Debug;
using HarmonyLib;
using I2.Loc;
using Lamb.UI;
using Lamb.UI.MainMenu;
using Newtonsoft.Json.Linq;
using src.Extensions;
using src.UI;
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Network
{
    /// <summary>
    /// Keeps the mod up to date.
    /// </summary>
    /// <remarks>
    /// Once a game start the latest release on GitHub is asked for. If it is newer than this mod, the
    /// main menu offers it; taking it downloads the release's installer (checked against the SHA-256
    /// GitHub gives for it), starts it to wait for the game to close, install and start the game
    /// again, and closes the game: a loaded mod can't be replaced while the game runs. A partner's
    /// version can be taken the same way (see <see cref="Game.VersionCheck"/>), joining the host again
    /// once back up. If that can't be done, the release page opens instead.
    /// </remarks>
    [HarmonyPatch]
    internal static class Updater
    {
        private const string Repo = "XDinka/cult_of_the_lamb_co-op_anything";
        private const string Api = "https://api.github.com/repos/" + Repo + "/releases";
        internal const string Page = "https://github.com/" + Repo + "/releases/latest";
        private const string InstallerName = "CotlCoopInstaller.exe";

        /* The latest release, once known, and the installer of the release being taken */
        internal static string Latest;
        private static string InstallerUrl, InstallerSha256;
        private static bool Asked, Offered;

        /* The test harness: the version we pretend to be, taking the offer by itself, the installer quiet */
        internal static string PretendVersion, ReleaseFrom, OfferShot;
        internal static bool TakeOffer, QuietInstaller;

        /* The test harness's mark that it took an update already: the game it starts again doesn't */
        internal static string TakenMark => Path.Combine(Paths.ConfigPath, "cotlmp-update-taken");

        /* The host to join again once the game is back up after updating to the host's version */
        private static string RejoinMark => Path.Combine(Paths.ConfigPath, "cotlmp-rejoin-host");

        /// <summary>
        /// This mod's version (or the one the test harness has it pretend)
        /// </summary>
        internal static string Version => PretendVersion ?? Data.Version.CotlMpVer;

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
        /// A release from GitHub, the latest or the one of a version: its tag, its installer's address and checksum
        /// </summary>
        private static IEnumerator Read(string Tag, Action<string> Done)
        {
            using UnityWebRequest Request = UnityWebRequest.Get(ReleaseFrom ?? (Tag == null ? Api + "/latest" : $"{Api}/tags/v{Tag}"));
            Request.SetRequestHeader("User-Agent", "CotlCoopAnything/" + Data.Version.CotlMpVer);
            Request.SetRequestHeader("Accept", "application/vnd.github+json");
            Request.timeout = 15;
            yield return Request.SendWebRequest();
            if (Request.result != UnityWebRequest.Result.Success)
            {
                Plugin.Logger?.LogInfo($"[UPDATE] Can't ask GitHub for release {Tag ?? "latest"}: {Request.error}");
                Done(null);
                yield break;
            }
            string Found = null;
            try
            {
                JObject Release = JObject.Parse(Request.downloadHandler.text);
                JToken Asset = (Release["assets"] as JArray)?.FirstOrDefault(Item => (string)Item["name"] == InstallerName);
                InstallerUrl = (string)Asset?["browser_download_url"];
                string Digest = (string)Asset?["digest"];
                InstallerSha256 = Digest != null && Digest.StartsWith("sha256:") ? Digest.Substring(7).ToLowerInvariant() : null;
                Found = ((string)Release["tag_name"])?.TrimStart('v', 'V');
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[UPDATE] GitHub's answer didn't read: {e.Message}");
            }
            Done(Found);
        }

        private static IEnumerator Ask(MainMenu Menu)
        {
            yield return Read(null, Found => Latest = Found);
            if (Latest == null)
                yield break;
            bool Behind = Newer(Latest, Version);
            Plugin.Logger?.LogInfo($"[UPDATE] Latest release {Latest}, this mod {Version}{(Behind ? ": an update is offered" : "")}");
            if (Behind && Menu != null)
                Offer(Menu);
        }

        private static void Offer(MainMenu Menu)
        {
            if (Offered || Menu == null || !Menu.isActiveAndEnabled)
                return;
            Offered = true;
            UIMenuConfirmationWindow Window = Menu.Push<UIMenuConfirmationWindow>(MonoSingleton<UIManager>.Instance.ConfirmationWindowTemplate);
            Window.Configure(LocalizationManager.GetTranslation("Multiplayer/Update/Title"),
                string.Format(LocalizationManager.GetTranslation("Multiplayer/Update/Body"), Latest, Version));
            Window.OnConfirm += () => Plugin.MonoInstance.StartCoroutine(Install(Latest, 0));
            if (OfferShot != null)
                Plugin.MonoInstance.StartCoroutine(ShootOffer());
            if (TakeOffer && !File.Exists(TakenMark))
            {
                File.WriteAllText(TakenMark, Latest);
                Plugin.MonoInstance.StartCoroutine(Install(Latest, 0));
            }
            else if (TakeOffer)
            {
                Plugin.Logger?.LogInfo($"[UPDATE] Started again after taking {File.ReadAllText(TakenMark)}");
                File.Delete(TakenMark);
            }
        }

        /// <summary>
        /// Updates to exactly this version (the partner's), and joins this host again once back up (0 for none)
        /// </summary>
        internal static void TakeVersion(string Wanted, ulong Rejoin, Action Failed = null)
        {
            Plugin.MonoInstance.StartCoroutine(TakeVersionNow(Wanted, Rejoin, Failed));
        }

        private static IEnumerator TakeVersionNow(string Wanted, ulong Rejoin, Action Failed)
        {
            string Found = null;
            yield return Read(Wanted, Tag => Found = Tag);
            if (Found == null)
                InstallerUrl = null;
            yield return Install(Wanted, Rejoin, Failed);
        }

        /// <summary>
        /// The new installer, checked, started to wait for us, and the game closed for it
        /// </summary>
        private static IEnumerator Install(string Taking, ulong Rejoin, Action Failed = null)
        {
            string Failure = null;
            if (string.IsNullOrEmpty(InstallerUrl))
                Failure = "the release has no installer";

            byte[] Bytes = null;
            if (Failure == null)
            {
                Tell("Multiplayer/Update/Downloading");
                using UnityWebRequest Request = UnityWebRequest.Get(InstallerUrl);
                Request.SetRequestHeader("User-Agent", "CotlCoopAnything/" + Data.Version.CotlMpVer);
                Request.timeout = 120;
                yield return Request.SendWebRequest();
                if (Request.result != UnityWebRequest.Result.Success)
                    Failure = Request.error;
                else
                    Bytes = Request.downloadHandler.data;
            }

            if (Failure == null)
            {
                try
                {
                    using SHA256 Hasher = SHA256.Create();
                    string Got = BitConverter.ToString(Hasher.ComputeHash(Bytes)).Replace("-", "").ToLowerInvariant();
                    if (InstallerSha256 != null && Got != InstallerSha256)
                        throw new InvalidDataException("the download doesn't match the release's checksum");
                    string Folder = Path.Combine(Path.GetTempPath(), "CotlCoopUpdate");
                    Directory.CreateDirectory(Folder);
                    string File = Path.Combine(Folder, $"CotlCoopInstaller-{Taking}.exe");
                    System.IO.File.WriteAllBytes(File, Bytes);

                    /* The game starts again with the arguments it has now (its launch options) */
                    string Arguments = string.Join(" ", Environment.GetCommandLineArgs().Skip(1).Select(Arg => Arg.Contains(" ") ? $"\"{Arg}\"" : Arg));
                    string Passed = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Arguments));
                    /* Started through the shell, so it doesn't inherit our open sockets and files (the game it
                       starts again would get them too). BepInEx's loader marks our process as loaded in its
                       environment, which would start that game without mods: they go first, we are leaving. */
                    ProcessStartInfo Start = new(File, $"--dir \"{Paths.GameRootPath}\" --auto install --wait {Process.GetCurrentProcess().Id} --relaunch" +
                                                       $"{(Arguments.Length > 0 ? $" --relaunch-args {Passed}" : "")}{(QuietInstaller ? " --quiet" : "")}")
                    {
                        UseShellExecute = true
                    };
                    foreach (string Name in Environment.GetEnvironmentVariables().Keys.Cast<string>().Where(Name => Name.StartsWith("DOORSTOP", StringComparison.OrdinalIgnoreCase)).ToList())
                        Environment.SetEnvironmentVariable(Name, null);
                    if (Rejoin != 0)
                        System.IO.File.WriteAllText(RejoinMark, Rejoin.ToString());
                    Process.Start(Start);
                    Plugin.Logger?.LogInfo($"[UPDATE] Installer {Taking} ({Got}) started, closing the game for it{(Rejoin != 0 ? $", host {Rejoin} to join again" : "")}");
                }
                catch (Exception e)
                {
                    Failure = e.Message;
                }
            }

            if (Failure == null)
            {
                Application.Quit();
                yield break;
            }

            /* The last way: the release page (and whatever we were doing goes on) */
            Plugin.Logger?.LogWarning($"[UPDATE] Updating to {Taking} didn't work: {Failure}");
            if (Rejoin != 0)
                File.Delete(RejoinMark);
            Failed?.Invoke();
            UIMenuConfirmationWindow Window = MonoSingleton<UIManager>.Instance.ConfirmationWindowTemplate.Instantiate();
            Window.Show();
            Window.Configure(LocalizationManager.GetTranslation("Multiplayer/Update/Failed"), string.Format(LocalizationManager.GetTranslation("Multiplayer/Update/OpenPage"), Taking));
            Window.OnConfirm += () => Application.OpenURL($"https://github.com/{Repo}/releases/tag/v{Taking}");
        }

        private static IEnumerator ShootOffer()
        {
            yield return new WaitForSecondsRealtime(2f);
            ScreenCapture.CaptureScreenshot(OfferShot);
        }

        private static void Tell(string Key)
        {
            try
            {
                NotificationCentre.Instance?.PlayGenericNotification(Key);
            }
            catch (Exception e)
            {
                PrintLogger.Print(DebugLevel.WARNING_LEVEL, DebugComponent.NETWORK_STACK_COMPONENT, $"Can't show {Key}: {e.Message}");
            }
        }

        /// <summary>
        /// Back up after updating to the host's version: the host is joined again (once the game can)
        /// </summary>
        internal static void Initialize() => JoinAgain();

        private static void JoinAgain()
        {
            try
            {
                if (!File.Exists(RejoinMark))
                    return;
                bool Fresh = DateTime.Now - File.GetLastWriteTime(RejoinMark) < TimeSpan.FromMinutes(15);
                string Text = File.ReadAllText(RejoinMark);
                File.Delete(RejoinMark);
                if (Fresh && ulong.TryParse(Text, out ulong Host) && Host != 0)
                {
                    Plugin.Logger?.LogInfo($"[UPDATE] Updated to the host's version, joining host {Host} again");
                    SteamBridge.JoinAgain(Host);
                }
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[UPDATE] Can't join the host again: {e.Message}");
            }
        }

        /// <summary>
        /// The main menu: ask once a game start, offer whenever we're back in it and haven't been offered yet
        /// </summary>
        [HarmonyPatch(typeof(MainMenu), "Start")]
        [HarmonyPostfix]
        private static void MenuShown(MainMenu __instance)
        {
            if (!Asked)
            {
                Asked = true;
                Plugin.MonoInstance.StartCoroutine(Ask(__instance));
            }
            else if (Newer(Latest, Version))
            {
                Offer(__instance);
            }
        }
    }
}

/* EOF */
