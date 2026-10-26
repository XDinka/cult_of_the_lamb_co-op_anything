/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     A newer mod taking this one's place while the game runs
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using BepInEx;
using BepInEx.Bootstrap;
using COTLMP.Game;
using COTLMP.Ui;
using MMTools;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Network
{
    /// <summary>
    /// Updates the mod in the running game.
    /// </summary>
    /// <remarks>
    /// The mod is loaded by our loader (COTLMP.Loader.dll) from memory, so its files can be written
    /// while it runs. A release's CotlCoopMod.zip carries the mod's files and what they need (the loader's
    /// contract, BepInEx's and COTL_API's versions); when those are what this game has, the files are put
    /// in place and, in the main menu, the loader lets this mod go and starts the new one, which brings
    /// the main menu up again. In a game the files wait: the swap is made in the main menu, or the next
    /// game start loads them. Anything else goes through the installer and a restart.
    /// </remarks>
    internal static class HotUpdate
    {
        internal const int Contract = 1;
        internal const string PackageName = "CotlCoopMod.zip";
        internal const string MenuScene = "Main Menu";

        /* Our files in the loader's Mod folder */
        private static readonly string[] Files = { "COTLMP.bin", "COTLMP.pdb", "COTLMPServer.bin", "COTLMPServer.pdb" };

        /// <summary>
        /// The version whose files are written and wait for the main menu, or null
        /// </summary>
        internal static string Staged { get; private set; }

        /// <summary>
        /// The version we came in place of (in this game), or null if the game started with us
        /// </summary>
        internal static string CameFrom { get; private set; }

        /// <summary>
        /// We started in a running game (in place of an older mod, or again after a newer one didn't start)
        /// </summary>
        internal static bool Restarted { get; private set; }

        /* The game's types whose static events the mod hooks into: what of ours is left in them is taken out */
        private static readonly Type[] Hooked =
        {
            typeof(MMBiomeGeneration.BiomeGenerator), typeof(MMTransition), typeof(MMConversation), typeof(SaveAndLoad),
            typeof(Health), typeof(SceneManager), typeof(Application)
        };

        private static Type Loader
        {
            get
            {
                foreach (Assembly Item in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (Item.GetName().Name != "COTLMP.Loader")
                        continue;
                    Type Found = Item.GetType("COTLMP.Loader.ModLoader", false);
                    if (Found != null)
                        return Found;
                }
                return null;
            }
        }

        /// <summary>
        /// We run under the loader we were built for
        /// </summary>
        internal static bool UnderLoader => AppDomain.CurrentDomain.GetData("COTLMP.LoaderContract") is int Theirs && Theirs == Contract && Loader != null;

        private static string ModFolder => Path.Combine(Plugin.CotlmpPathLocation, "Mod");

        /// <summary>
        /// At the plugin's start: did we take an older mod's place?
        /// </summary>
        internal static void Initialize()
        {
            CameFrom = AppDomain.CurrentDomain.GetData("COTLMP.SwappedFrom") as string;
            AppDomain.CurrentDomain.SetData("COTLMP.SwappedFrom", null);
            if (CameFrom != null)
                Plugin.Logger?.LogInfo($"[UPDATE] Updated from {CameFrom} without leaving the game");
            if (AppDomain.CurrentDomain.GetData("COTLMP.SwapFailed") is string Failed)
            {
                AppDomain.CurrentDomain.SetData("COTLMP.SwapFailed", null);
                Plugin.Logger?.LogWarning($"[UPDATE] The new mod didn't start, back on {Failed}");
                Restarted = true;
            }
            Restarted |= CameFrom != null;
        }

        /// <summary>
        /// Can this package's mod be taken in this game? If not, why not
        /// </summary>
        internal static string Unfit(JObject Manifest)
        {
            if (!UnderLoader)
                return "the mod wasn't started by the loader";
            if ((int?)Manifest["loader"] != Contract)
                return $"it needs loader {Manifest["loader"]}, this game has {Contract}";
            string BepInEx = typeof(Paths).Assembly.GetName().Version.ToString();
            if ((string)Manifest["bepinex"] != BepInEx)
                return $"it needs BepInEx {Manifest["bepinex"]}, this game has {BepInEx}";
            string Api = Chainloader.PluginInfos.TryGetValue("io.github.xhayper.COTL_API", out PluginInfo Info) ? Info.Metadata.Version.ToString() : null;
            if ((string)Manifest["cotlapi"] != Api)
                return $"it needs COTL_API {Manifest["cotlapi"]}, this game has {Api ?? "none"}";
            return null;
        }

        /// <summary>
        /// The package's manifest and files, or null when it isn't one
        /// </summary>
        internal static (JObject Manifest, Dictionary<string, byte[]> Files) Open(byte[] Package)
        {
            using ZipArchive Zip = new(new MemoryStream(Package), ZipArchiveMode.Read);
            ZipArchiveEntry Entry = Zip.GetEntry("manifest.json") ?? throw new InvalidDataException("the package has no manifest");
            JObject Manifest;
            using (StreamReader Reader = new(Entry.Open()))
                Manifest = JObject.Parse(Reader.ReadToEnd());

            Dictionary<string, byte[]> Found = new();
            foreach (string Name in Files)
            {
                ZipArchiveEntry Item = Zip.GetEntry(Name);
                if (Item == null)
                    continue;
                using Stream From = Item.Open();
                using MemoryStream To = new();
                From.CopyTo(To);
                Found[Name] = To.ToArray();
            }
            if (!Found.ContainsKey("COTLMP.bin") || !Found.ContainsKey("COTLMPServer.bin"))
                throw new InvalidDataException("the package has no mod in it");
            return (Manifest, Found);
        }

        /// <summary>
        /// Writes the new mod's files in place of ours (each whole or not at all)
        /// </summary>
        internal static void Stage(string Version, Dictionary<string, byte[]> New)
        {
            Directory.CreateDirectory(ModFolder);
            foreach (string Name in Files)
            {
                string Target = Path.Combine(ModFolder, Name);
                if (!New.TryGetValue(Name, out byte[] Bytes))
                {
                    if (File.Exists(Target))
                        File.Delete(Target);
                    continue;
                }
                string Part = Target + ".part";
                File.WriteAllBytes(Part, Bytes);
                if (File.Exists(Target))
                    File.Delete(Target);
                File.Move(Part, Target);
            }
            Staged = Version;
            Plugin.Logger?.LogInfo($"[UPDATE] Mod {Version} written, waiting for the main menu to start it");
        }

        /// <summary>
        /// Somewhere the mod can be swapped now: the main menu, nobody in a game with us
        /// </summary>
        internal static bool CanSwapNow => SceneManager.GetActiveScene().name == MenuScene && !MMTransition.IsPlaying;

        /// <summary>
        /// Asks the loader to start the written mod in our place; false if it can't
        /// </summary>
        internal static bool Swap()
        {
            if (Staged == null)
                return false;
            try
            {
                MethodInfo Method = Loader?.GetMethod("Swap", BindingFlags.Static | BindingFlags.Public);
                if (Method != null && (bool)Method.Invoke(null, new object[] { Updater.Version }))
                {
                    Plugin.Logger?.LogInfo($"[UPDATE] Swapping to mod {Staged}");
                    return true;
                }
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[UPDATE] The loader can't swap: {e.Message}");
            }
            return false;
        }

        /// <summary>
        /// Started in a running game: the main menu is brought up again, so all of it is ours
        /// </summary>
        internal static IEnumerator Arrive()
        {
            yield return null;
            Plugin.Logger?.LogInfo("[UPDATE] Bringing the main menu up again for the new mod");
            MMTransition.Play(MMTransition.TransitionType.ChangeSceneAutoResume, MMTransition.Effect.BlackFade, MenuScene, 0.5f, "", null);

            /* Up and standing: the update's screen goes (the menu's own start may come before we are here) */
            yield return new WaitForSecondsRealtime(1f);
            float Until = Time.realtimeSinceStartup + 30f;
            while ((SceneManager.GetActiveScene().name != MenuScene || MMTransition.IsPlaying) && Time.realtimeSinceStartup < Until)
                yield return null;
            Updater.Welcome();
        }

        /// <summary>
        /// Called by the loader before it lets us go: closes what we have open and takes out what
        /// we put into the game, so nothing of ours runs any more
        /// </summary>
        internal static void Release()
        {
            Plugin.Logger?.LogInfo($"[UPDATE] Mod {Updater.Version} lets go");
            Assembly Ours = typeof(HotUpdate).Assembly;

            Try("the session", () =>
            {
                if (Lobby.Open)
                    Lobby.Close();
                PauseMenuPatches.EndSession(null);
                PauseMenuPatches.Quitting = true;
                PauseMenuPatches.StopServer();
                PauseMenuPatches.Quitting = false;
                OnlineMenu.Wait(null);
            });
            Try("Steam", SteamBridge.Release);
            Try("the log counter", COTLMP.Debug.Faults.Release);
            Try("the settings", () =>
            {
                Type Manager = typeof(COTL_API.CustomSettings.CustomSettingsManager);
                if (Manager.GetProperty("SettingsElements", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(null) is IList Elements)
                {
                    string Title = I2.Loc.MultiplayerModLocalization.UI.Settings.MultiplayerSettings_Title;
                    for (int Index = Elements.Count - 1; Index >= 0; Index--)
                    {
                        if (Elements[Index] is COTL_API.CustomSettings.Elements.ISettingsElement Element && Element.Category == Title)
                            Elements.RemoveAt(Index);
                    }
                }
            });
            Try("the game's events", () =>
            {
                int Taken = 0;
                foreach (Type Type in Hooked)
                    Taken += Unhook(Type, Ours);
                Plugin.Logger?.LogInfo($"[UPDATE] {Taken} of our hooks taken out of the game's events");
            });
            Try("our objects", () =>
            {
                int Gone = 0;
                foreach (MonoBehaviour Item in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
                {
                    if (Item == null || Item == Plugin.MonoInstance || Item.GetType().Assembly != Ours || !Item.gameObject.scene.IsValid())
                        continue;
                    GameObject Root = Item.transform.root.gameObject;
                    if (Root.scene.name == "DontDestroyOnLoad" && Root.name.StartsWith("COTLMP ", StringComparison.Ordinal))
                        UnityEngine.Object.Destroy(Root);
                    else
                        UnityEngine.Object.Destroy(Item);
                    ++Gone;
                }
                /* Ours that hold nothing of ours (the update's screen, the waiting screen: the game's pictures only) */
                GameObject Probe = new("COTLMP Probe");
                UnityEngine.Object.DontDestroyOnLoad(Probe);
                foreach (GameObject Root in Probe.scene.GetRootGameObjects())
                {
                    if (Root != Probe && Root.name.StartsWith("COTLMP ", StringComparison.Ordinal))
                    {
                        UnityEngine.Object.Destroy(Root);
                        ++Gone;
                    }
                }
                UnityEngine.Object.Destroy(Probe);
                Plugin.Logger?.LogInfo($"[UPDATE] {Gone} of our objects taken out");
            });

            BepInEx.Logging.Logger.Sources.Remove(Plugin.Logger);
        }

        private static void Try(string What, Action Work)
        {
            try
            {
                Work();
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[UPDATE] Letting go of {What} failed: {e}");
            }
        }

        /// <summary>
        /// Takes our handlers out of this type's static events
        /// </summary>
        private static int Unhook(Type Type, Assembly Ours)
        {
            int Taken = 0;
            foreach (FieldInfo Field in Type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!typeof(Delegate).IsAssignableFrom(Field.FieldType) || Field.IsLiteral || Field.IsInitOnly || Field.GetValue(null) is not Delegate Value)
                    continue;
                Delegate Kept = null;
                bool Changed = false;
                foreach (Delegate Part in Value.GetInvocationList())
                {
                    if (Part.Method?.DeclaringType?.Assembly == Ours || Part.Target?.GetType().Assembly == Ours)
                    {
                        Changed = true;
                        ++Taken;
                    }
                    else
                    {
                        Kept = Delegate.Combine(Kept, Part);
                    }
                }
                if (Changed)
                    Field.SetValue(null, Kept);
            }
            return Taken;
        }
    }
}

/* EOF */
