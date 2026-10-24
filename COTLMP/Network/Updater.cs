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
    /// again from Steam, and closes the game: a loaded mod can't be replaced while the game runs.
    /// If that can't be done, the release page opens instead.
    /// </remarks>
    [HarmonyPatch]
    internal static class Updater
    {
        private const string Repo = "XDinka/cult_of_the_lamb_co-op_anything";
        private const string Api = "https://api.github.com/repos/" + Repo + "/releases/latest";
        internal const string Page = "https://github.com/" + Repo + "/releases/latest";
        private const string InstallerName = "CotlCoopInstaller.exe";

        /* The latest release, once known */
        internal static string Latest;
        private static string InstallerUrl, InstallerSha256;
        private static bool Asked, Offered;

        /* The test harness: the version we pretend to be, taking the offer by itself, the installer quiet */
        internal static string PretendVersion, ReleaseFrom, OfferShot;
        internal static bool TakeOffer, QuietInstaller;

        /* The test harness's mark that it took an update already: the game it starts again doesn't */
        private static string TakenMark => Path.Combine(Paths.ConfigPath, "cotlmp-update-taken");

        private static string Ours => PretendVersion ?? Data.Version.CotlMpVer;

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

        private static IEnumerator Ask(MainMenu Menu)
        {
            using UnityWebRequest Request = UnityWebRequest.Get(ReleaseFrom ?? Api);
            Request.SetRequestHeader("User-Agent", "CotlCoopAnything/" + Data.Version.CotlMpVer);
            Request.SetRequestHeader("Accept", "application/vnd.github+json");
            Request.timeout = 15;
            yield return Request.SendWebRequest();
            if (Request.result != UnityWebRequest.Result.Success)
            {
                Plugin.Logger?.LogInfo($"[UPDATE] Can't ask GitHub for the latest release: {Request.error}");
                yield break;
            }
            try
            {
                JObject Release = JObject.Parse(Request.downloadHandler.text);
                Latest = ((string)Release["tag_name"])?.TrimStart('v', 'V');
                JToken Asset = (Release["assets"] as JArray)?.FirstOrDefault(Item => (string)Item["name"] == InstallerName);
                InstallerUrl = (string)Asset?["browser_download_url"];
                string Digest = (string)Asset?["digest"];
                InstallerSha256 = Digest != null && Digest.StartsWith("sha256:") ? Digest.Substring(7).ToLowerInvariant() : null;
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[UPDATE] GitHub's answer didn't read: {e.Message}");
                yield break;
            }

            bool Behind = Newer(Latest, Ours);
            Plugin.Logger?.LogInfo($"[UPDATE] Latest release {Latest}, this mod {Ours}{(Behind ? ": an update is offered" : "")}");
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
                string.Format(LocalizationManager.GetTranslation("Multiplayer/Update/Body"), Latest, Ours));
            Window.OnConfirm += () => Plugin.MonoInstance.StartCoroutine(Install(Menu));
            if (OfferShot != null)
                Plugin.MonoInstance.StartCoroutine(ShootOffer());
            if (TakeOffer && !File.Exists(TakenMark))
            {
                File.WriteAllText(TakenMark, Latest);
                Plugin.MonoInstance.StartCoroutine(Install(Menu));
            }
            else if (TakeOffer)
            {
                Plugin.Logger?.LogInfo($"[UPDATE] Started again after taking {File.ReadAllText(TakenMark)}");
                File.Delete(TakenMark);
            }
        }

        /// <summary>
        /// The new installer, checked, started to wait for us, and the game closed for it
        /// </summary>
        private static IEnumerator Install(MainMenu Menu)
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
                    string File = Path.Combine(Folder, $"CotlCoopInstaller-{Latest}.exe");
                    System.IO.File.WriteAllBytes(File, Bytes);
                    /* The game starts again with the arguments it has now (its launch options) */
                    string Arguments = string.Join(" ", Environment.GetCommandLineArgs().Skip(1).Select(Arg => Arg.Contains(" ") ? $"\"{Arg}\"" : Arg));
                    string Passed = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Arguments));
                    ProcessStartInfo Start = new(File, $"--dir \"{Paths.GameRootPath}\" --auto install --wait {Process.GetCurrentProcess().Id} --relaunch" +
                                                       $"{(Arguments.Length > 0 ? $" --relaunch-args {Passed}" : "")}{(QuietInstaller ? " --quiet" : "")}")
                    {
                        UseShellExecute = false
                    };

                    /* BepInEx's loader marks our process as loaded; the game the installer starts again
                       would inherit that and start without mods */
                    foreach (string Name in Start.EnvironmentVariables.Keys.Cast<string>().Where(Name => Name.StartsWith("DOORSTOP", StringComparison.OrdinalIgnoreCase)).ToList())
                        Start.EnvironmentVariables.Remove(Name);
                    Process.Start(Start);
                    Plugin.Logger?.LogInfo($"[UPDATE] Installer {Latest} ({Got}) started, closing the game for it");
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

            /* The last way: the release page */
            Plugin.Logger?.LogWarning($"[UPDATE] Updating didn't work: {Failure}");
            if (Menu == null)
            {
                Application.OpenURL(Page);
                yield break;
            }
            UIMenuConfirmationWindow Window = Menu.Push<UIMenuConfirmationWindow>(MonoSingleton<UIManager>.Instance.ConfirmationWindowTemplate);
            Window.Configure(LocalizationManager.GetTranslation("Multiplayer/Update/Failed"), string.Format(LocalizationManager.GetTranslation("Multiplayer/Update/OpenPage"), Latest));
            Window.OnConfirm += () => Application.OpenURL(Page);
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
            else if (Newer(Latest, Ours))
            {
                Offer(__instance);
            }
        }
    }
}

/* EOF */
