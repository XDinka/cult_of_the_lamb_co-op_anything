/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     A newer release on GitHub: offered in the main menu, put in while the game runs
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using BepInEx;
using COTLMP.Debug;
using COTLMP.Ui;
using HarmonyLib;
using I2.Loc;
using Lamb.UI;
using Lamb.UI.MainMenu;
using MMTools;
using Newtonsoft.Json.Linq;
using src.Extensions;
using src.UI;
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Network
{
    /// <summary>
    /// Keeps the mod up to date.
    /// </summary>
    /// <remarks>
    /// Once a game start the latest release on GitHub is asked for. If it is newer than this mod, the
    /// main menu offers it. Taking it shows a screen with a bar over the game while the release's mod
    /// package (CotlCoopMod.zip, checked against the SHA-256 GitHub gives for it) comes down; when it
    /// fits this game, the new mod takes this one's place without leaving the game (see
    /// <see cref="HotUpdate"/>). A release that needs a new BepInEx, COTL_API or loader, or a game
    /// whose mod wasn't started by the loader, takes the old way: the release's installer is started
    /// to wait for the game to close, install and start the game again, and the game closes. A
    /// partner's version can be taken the same way (see <see cref="Game.VersionCheck"/>), joining the
    /// host again once back up. If none of that can be done, the release page opens instead.
    /// </remarks>
    [HarmonyPatch]
    internal static class Updater
    {
        private const string Repo = "XDinka/cult_of_the_lamb_co-op_anything";
        private const string Api = "https://api.github.com/repos/" + Repo + "/releases";
        private const string InstallerName = "CotlCoopInstaller.exe";

        /* The latest release, once known, and the files of the release being taken */
        internal static string Latest;
        private static string InstallerUrl, InstallerSha256, PackageUrl, PackageSha256;
        private static bool Asked, Offered, Welcomed;

        /* The test harness: the version we pretend to be, taking the offer by itself, the installer quiet,
           the installer's way even when the mod could go in the running game */
        internal static string PretendVersion, ReleaseFrom, OfferShot, ProgressShot;
        internal static bool TakeOffer, QuietInstaller, NoHotUpdate;

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
        /// A release from GitHub, the latest or the one of a version: its tag, its files' addresses and checksums
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
                (InstallerUrl, InstallerSha256) = AssetOf(Release, InstallerName);
                (PackageUrl, PackageSha256) = AssetOf(Release, HotUpdate.PackageName);
                Found = ((string)Release["tag_name"])?.TrimStart('v', 'V');
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[UPDATE] GitHub's answer didn't read: {e.Message}");
            }
            Done(Found);
        }

        private static (string Url, string Sha256) AssetOf(JObject Release, string Name)
        {
            JToken Asset = (Release["assets"] as JArray)?.FirstOrDefault(Item => (string)Item["name"] == Name);
            string Digest = (string)Asset?["digest"];
            return ((string)Asset?["browser_download_url"], Digest != null && Digest.StartsWith("sha256:") ? Digest.Substring(7).ToLowerInvariant() : null);
        }

        private static IEnumerator Ask(MainMenu Menu)
        {
            yield return Read(null, Found => Latest = Found);
            bool Behind = Latest != null && Newer(Latest, Version);
            if (Latest != null)
                Plugin.Logger?.LogInfo($"[UPDATE] Latest release {Latest}, this mod {Version}{(Behind ? ": an update is offered" : "")}");
            /* Patches this game version didn't take: the window says so, with the update if there is one */
            if (Debug.PatchCheck.Failed.Count > 0 && !Debug.PatchCheck.Told && Menu != null)
            {
                OfferForPatches(Menu, Behind);
                yield break;
            }
            if (Behind && Menu != null)
                Offer(Menu);
        }

        /// <summary>
        /// Some of the mod's patches don't fit this game version (the game was updated after the mod): a newer
        /// mod is offered right here, or the player hears that the fix is coming and is offered here when it is out
        /// </summary>
        private static void OfferForPatches(MainMenu Menu, bool Behind)
        {
            if (Menu == null || !Menu.isActiveAndEnabled)
                return;
            Debug.PatchCheck.Told = true;
            int Count = Debug.PatchCheck.Failed.Count;
            UIMenuConfirmationWindow Window = Menu.Push<UIMenuConfirmationWindow>(MonoSingleton<UIManager>.Instance.ConfirmationWindowTemplate);
            if (Behind && HotUpdate.Staged == null && !Game.Relobby.Pending)
            {
                Offered = true;
                Window.Configure(LocalizationManager.GetTranslation("Multiplayer/UI/PatchesFailedTitle"),
                    string.Format(LocalizationManager.GetTranslation("Multiplayer/UI/PatchesUpdate"), Count, Latest, Version));
                Window.OnConfirm += () => Plugin.MonoInstance.StartCoroutine(Install(Latest, 0));
            }
            else
            {
                Window.Configure(LocalizationManager.GetTranslation("Multiplayer/UI/PatchesFailedTitle"),
                    string.Format(LocalizationManager.GetTranslation("Multiplayer/UI/PatchesFailed"), Count), true);
            }
            Plugin.Logger?.LogInfo($"[UPDATE] {Count} patches left out: told the player{(Behind ? $", with version {Latest} to update to" : ", no newer version yet")}");
        }

        private static void Offer(MainMenu Menu)
        {
            if (Offered || Menu == null || !Menu.isActiveAndEnabled || HotUpdate.Staged != null || Game.Relobby.Pending)
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
                Window.Hide();
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
                InstallerUrl = PackageUrl = null;
            yield return Install(Wanted, Rejoin, Failed);
        }

        /// <summary>
        /// A file of the release, with the bar going as it comes; null and why on failure
        /// </summary>
        private static IEnumerator Download(string Url, string Sha256, int Timeout, Action<byte[], string> Done)
        {
            using UnityWebRequest Request = UnityWebRequest.Get(Url);
            Request.SetRequestHeader("User-Agent", "CotlCoopAnything/" + Data.Version.CotlMpVer);
            Request.timeout = Timeout;
            UnityWebRequestAsyncOperation Going = Request.SendWebRequest();
            while (!Going.isDone)
            {
                ulong.TryParse(Request.GetResponseHeader("Content-Length"), out ulong Total);
                ProgressScreen.Downloading(Request.downloadedBytes, Total, Request.downloadProgress);
                yield return null;
            }
            if (Request.result != UnityWebRequest.Result.Success)
            {
                Done(null, Request.error);
                yield break;
            }

            byte[] Bytes = Request.downloadHandler.data;
            ProgressScreen.Set(1f, T("Multiplayer/Update/Checking"));
            yield return null;
            using SHA256 Hasher = SHA256.Create();
            string Got = BitConverter.ToString(Hasher.ComputeHash(Bytes)).Replace("-", "").ToLowerInvariant();
            if (Sha256 != null && Got != Sha256)
                Done(null, "the download doesn't match the release's checksum");
            else
                Done(Bytes, null);
        }

        /// <summary>
        /// The new version: into the running game if it can go there, else through its installer and a restart
        /// </summary>
        private static IEnumerator Install(string Taking, ulong Rejoin, Action Failed = null)
        {
            ProgressScreen.Show(Taking);
            if (ProgressShot != null)
                Plugin.MonoInstance.StartCoroutine(ShootProgress());

            /* The running game's way */
            string Unfit = NoHotUpdate ? "the test asks for the installer" : PackageUrl == null ? "the release has no mod package" : null;
            if (Unfit == null)
            {
                byte[] Package = null;
                yield return Download(PackageUrl, PackageSha256, 60, (Bytes, Why) => (Package, Unfit) = (Bytes, Why));
                if (Package != null)
                {
                    try
                    {
                        var (Manifest, Files) = HotUpdate.Open(Package);
                        Unfit = HotUpdate.Unfit(Manifest);
                        if (Unfit == null)
                            HotUpdate.Stage(Taking, Files);
                    }
                    catch (Exception e)
                    {
                        Unfit = e.Message;
                    }
                }
            }
            if (Unfit == null)
            {
                if (Rejoin != 0)
                    File.WriteAllText(RejoinMark, Rejoin.ToString());
                if (HotUpdate.CanSwapNow)
                {
                    ProgressScreen.Set(1f, T("Multiplayer/Update/Starting"));
                    yield return new WaitForSecondsRealtime(0.3f);
                    if (HotUpdate.Swap())
                        yield break;
                }

                /* In a game: the player picks how it goes in (back to the lobby, or the game restarted), or later */
                ProgressScreen.Hide();
                Game.Relobby.Ask(Taking);
                yield break;
            }
            Plugin.Logger?.LogInfo($"[UPDATE] {Taking} can't go in the running game ({Unfit}): the installer puts it in");

            /* The installer's way */
            string Failure = null;
            if (string.IsNullOrEmpty(InstallerUrl))
                Failure = "the release has no installer";

            byte[] Installer = null;
            if (Failure == null)
            {
                ProgressScreen.Set(0f, T("Multiplayer/Update/Restart"));
                yield return new WaitForSecondsRealtime(1f);
                yield return Download(InstallerUrl, InstallerSha256, 120, (Bytes, Why) => (Installer, Failure) = (Bytes, Why));
            }

            if (Failure == null)
            {
                try
                {
                    string Folder = Path.Combine(Path.GetTempPath(), "CotlCoopUpdate");
                    Directory.CreateDirectory(Folder);
                    string File = Path.Combine(Folder, $"CotlCoopInstaller-{Taking}.exe");
                    System.IO.File.WriteAllBytes(File, Installer);

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
                    Plugin.Logger?.LogInfo($"[UPDATE] Installer {Taking} ({InstallerSha256 ?? "no checksum"}) started, closing the game for it{(Rejoin != 0 ? $", host {Rejoin} to join again" : "")}");
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
            ProgressScreen.Hide();
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

        /* The bar part way: once it is moving (a file:// download in a test is over in a frame or two) */
        private static IEnumerator ShootProgress()
        {
            yield return null;
            yield return null;
            ScreenCapture.CaptureScreenshot(ProgressShot);
        }

        private static string T(string Key) => LocalizationManager.GetTranslation(Key);

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
        internal static void Initialize()
        {
            /* Come in place of the old mod: its screen goes on until the main menu is up again */
            if (HotUpdate.CameFrom != null)
            {
                ProgressScreen.Show(Data.Version.CotlMpVer);
                ProgressScreen.Set(1f, T("Multiplayer/Update/Starting"));
                return;
            }
            JoinAgain();
        }

        /// <summary>
        /// The update waits for the main menu (or the next game start)
        /// </summary>
        internal static void TellReady() => Tell("Multiplayer/Update/Ready");

        /// <summary>
        /// This host is joined again once the game is back up (after it starts again, or the new mod is in)
        /// </summary>
        internal static void MarkRejoin(ulong Host)
        {
            if (Host != 0)
                File.WriteAllText(RejoinMark, Host.ToString());
        }

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
        /// The new mod is in and the main menu up again: the update's screen goes, the player is told
        /// </summary>
        internal static void Welcome()
        {
            if (HotUpdate.CameFrom == null || Welcomed)
                return;
            Welcomed = true;
            ProgressScreen.Hide();
            Tell("Multiplayer/Update/Done");
            JoinAgain();
        }

        /* A written update started once the main menu stands still */
        private static IEnumerator SwapInMenu()
        {
            float Until = Time.realtimeSinceStartup + 60f;
            while (!HotUpdate.CanSwapNow && Time.realtimeSinceStartup < Until)
                yield return null;
            if (!HotUpdate.CanSwapNow)
            {
                Plugin.Logger?.LogWarning("[UPDATE] The main menu didn't stand still for a minute: the update waits for the next one");
                yield break;
            }
            ProgressScreen.Show(HotUpdate.Staged);
            ProgressScreen.Set(1f, T("Multiplayer/Update/Starting"));
            yield return new WaitForSecondsRealtime(0.3f);
            if (!HotUpdate.Swap())
                ProgressScreen.Hide();
        }

        /// <summary>
        /// The main menu: ask once a game start, offer whenever we're back in it and haven't been offered yet
        /// </summary>
        [HarmonyPatch(typeof(MainMenu), "Start")]
        [HarmonyPostfix]
        private static void MenuShown(MainMenu __instance)
        {
            /* The main menu is up again after the new mod took the old one's place */
            Welcome();
            if (HotUpdate.Staged != null)
            {
                Plugin.MonoInstance.StartCoroutine(SwapInMenu());
                return;
            }

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

        /// <summary>
        /// A dark screen over the game with what the update is doing and a bar
        /// </summary>
        private static class ProgressScreen
        {
            private static GameObject Root;
            private static RectTransform Fill;
            private static TextMeshProUGUI Line;

            private static readonly Color Cream = new(0.961f, 0.929f, 0.835f, 1f);
            private static readonly Color Red = new(0.78f, 0.13f, 0.16f, 1f);

            internal static void Show(string Taking)
            {
                if (Root == null)
                    Build();
                Root.transform.Find("Title").GetComponent<TMP_Text>().text = string.Format(T("Multiplayer/Update/Screen"), Taking);
                Set(0f, T("Multiplayer/Update/Downloading"));
            }

            internal static void Set(float Part, string Text)
            {
                if (Root == null)
                    return;
                Fill.anchorMax = new Vector2(Mathf.Clamp01(Part), 1f);
                Line.text = Text;
            }

            internal static void Downloading(ulong Got, ulong Total, float Part)
            {
                if (Total > 0)
                    Set((float)Got / Total, string.Format(T("Multiplayer/Update/Progress"), Got / 1024, Total / 1024));
                else
                    Set(Part, T("Multiplayer/Update/Downloading"));
            }

            internal static void Hide()
            {
                if (Root != null)
                    UnityEngine.Object.Destroy(Root);
                Root = null;
            }

            private static RectTransform Box(string Name, Transform Parent, Vector2 Min, Vector2 Max)
            {
                RectTransform Rect = new GameObject(Name, typeof(RectTransform)).GetComponent<RectTransform>();
                Rect.SetParent(Parent, false);
                Rect.anchorMin = Min;
                Rect.anchorMax = Max;
                Rect.offsetMin = Rect.offsetMax = Vector2.zero;
                return Rect;
            }

            private static TextMeshProUGUI Words(string Name, Transform Parent, Vector2 Min, Vector2 Max, float Size)
            {
                TextMeshProUGUI Text = Box(Name, Parent, Min, Max).gameObject.AddComponent<TextMeshProUGUI>();
                if (OnlineMenu.Font != null)
                {
                    Text.font = OnlineMenu.Font;
                    Text.fontSharedMaterial = OnlineMenu.FontMaterial;
                }
                else if (UnityEngine.Object.FindObjectOfType<TMP_Text>() is TMP_Text Game && Game.font != null)
                {
                    /* Our menu isn't built yet (a new mod just started): the game's own words show the font */
                    Text.font = Game.font;
                }
                Text.fontSize = Size;
                Text.alignment = TextAlignmentOptions.Center;
                Text.color = Cream;
                return Text;
            }

            private static void Build()
            {
                Root = new GameObject("COTLMP Update");
                UnityEngine.Object.DontDestroyOnLoad(Root);
                Canvas Paper = Root.AddComponent<Canvas>();
                Paper.renderMode = RenderMode.ScreenSpaceOverlay;
                Paper.sortingOrder = 32001;
                CanvasScaler Scaler = Root.AddComponent<CanvasScaler>();
                Scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                Scaler.referenceResolution = new Vector2(1920f, 1080f);
                Scaler.matchWidthOrHeight = 0.5f;
                Root.AddComponent<GraphicRaycaster>();

                Box("Dark", Root.transform, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);
                Words("Title", Root.transform, new Vector2(0.1f, 0.52f), new Vector2(0.9f, 0.62f), 44f);

                /* The bar: a cream frame, a dark inside, the red part done */
                RectTransform Frame = Box("Frame", Root.transform, new Vector2(0.3f, 0.475f), new Vector2(0.7f, 0.5f));
                Frame.gameObject.AddComponent<Image>().color = Cream;
                RectTransform Inside = Box("Inside", Frame, Vector2.zero, Vector2.one);
                Inside.offsetMin = new Vector2(3f, 3f);
                Inside.offsetMax = new Vector2(-3f, -3f);
                Inside.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.09f, 0.09f, 1f);
                Fill = Box("Done", Inside, Vector2.zero, new Vector2(0f, 1f));
                Fill.gameObject.AddComponent<Image>().color = Red;

                Line = Words("Line", Root.transform, new Vector2(0.1f, 0.38f), new Vector2(0.9f, 0.46f), 30f);
            }
        }
    }
}

/* EOF */
