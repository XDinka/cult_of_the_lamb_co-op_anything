/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     The mod's loader: loads the mod from memory and lets a newer one take its place in a running game
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using BepInEx;
using Mono.Cecil;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Loader
{
    /// <summary>
    /// The one plugin BepInEx loads for the mod.
    /// </summary>
    /// <remarks>
    /// BepInEx loads a plugin from its file, and a loaded assembly can neither be replaced nor taken
    /// out of a running game. So the mod (Mod\COTLMP.bin and Mod\COTLMPServer.bin, not .dll so BepInEx
    /// leaves them to us) is read into memory and loaded from there, under a name of its own each time:
    /// Mono gives back an assembly already loaded under the same name instead of loading it again.
    /// When the mod has written a newer version of its files, <see cref="Swap"/> lets it go (it closes
    /// its session and takes back what it hooked into the game), loads the new one and starts it. If the
    /// new one doesn't start, the old one's files are written back and it starts again. This loader
    /// stays as it is between releases: a release that needs a different one goes through the installer.
    /// </remarks>
    [BepInPlugin(Guid, "Cult of the Lamb Co-op Anything", "1.0.0")]
    [BepInDependency("io.github.xhayper.COTL_API", BepInDependency.DependencyFlags.HardDependency)]
    public class ModLoader : BaseUnityPlugin
    {
        public const string Guid = "xdinka.cotl.coopanything.loader";

        /// <summary>
        /// What the mod and this loader agree on; a mod built for another contract is installed by the installer
        /// </summary>
        public const int Contract = 1;

        private static ModLoader Instance;
        private static GameObject Holder;
        private static Assembly Mod;
        private static byte[][] Running;
        private static bool Swapping;
        private static int Loads;

        private static string Folder, ModFolder;
        private static readonly string[] Files = { "COTLMP.bin", "COTLMP.pdb", "COTLMPServer.bin", "COTLMPServer.pdb" };

        private void Awake()
        {
            Instance = this;
            Folder = Path.GetDirectoryName(Info.Location);
            ModFolder = Path.Combine(Folder, "Mod");

            /* What the mod reads from us without a reference to this assembly */
            AppDomain.CurrentDomain.SetData("COTLMP.Folder", Folder);
            AppDomain.CurrentDomain.SetData("COTLMP.LoaderContract", Contract);

            byte[][] Mine;
            try
            {
                Mine = Read();
            }
            catch (Exception e)
            {
                Logger.LogError($"The mod's files in {ModFolder} don't read: {e.Message}");
                return;
            }
            if (!TryStart(Mine))
                Logger.LogError("The mod didn't start");
        }

        /// <summary>
        /// The mod's files as they are on disk now
        /// </summary>
        private static byte[][] Read()
        {
            return Files.Select(Name =>
            {
                string Path = System.IO.Path.Combine(ModFolder, Name);
                if (Name.EndsWith(".pdb", StringComparison.Ordinal))
                    return File.Exists(Path) ? File.ReadAllBytes(Path) : null;
                return File.ReadAllBytes(Path);
            }).ToArray();
        }

        private static void Write(byte[][] Bytes)
        {
            for (int Index = 0; Index < Files.Length; Index++)
            {
                string Path = System.IO.Path.Combine(ModFolder, Files[Index]);
                if (Bytes[Index] != null)
                    File.WriteAllBytes(Path, Bytes[Index]);
                else if (File.Exists(Path))
                    File.Delete(Path);
            }
        }

        /// <summary>
        /// The assembly under a name of its own (and its references to the other one renamed the same way)
        /// </summary>
        private static Assembly Load(byte[] Image, byte[] Symbols, string Suffix)
        {
            byte[] Renamed, RenamedSymbols = null;
            try
            {
                (Renamed, RenamedSymbols) = Rename(Image, Symbols, Suffix);
            }
            catch (Exception e) when (Symbols != null)
            {
                Instance.Logger.LogWarning($"Loading without line numbers: {e.Message}");
                (Renamed, _) = Rename(Image, null, Suffix);
            }
            if (RenamedSymbols != null)
            {
                try
                {
                    return Assembly.Load(Renamed, RenamedSymbols);
                }
                catch (Exception e)
                {
                    Instance.Logger.LogWarning($"Loading without line numbers: {e.Message}");
                }
            }
            return Assembly.Load(Renamed);
        }

        private static (byte[], byte[]) Rename(byte[] Image, byte[] Symbols, string Suffix)
        {
            ReaderParameters Reading = new() { ReadSymbols = Symbols != null };
            if (Symbols != null)
                Reading.SymbolStream = new MemoryStream(Symbols);
            using AssemblyDefinition Definition = AssemblyDefinition.ReadAssembly(new MemoryStream(Image), Reading);
            Definition.Name.Name += Suffix;
            foreach (AssemblyNameReference Reference in Definition.MainModule.AssemblyReferences)
            {
                if (Reference.Name == "COTLMP" || Reference.Name == "COTLMPServer")
                    Reference.Name += Suffix;
            }

            MemoryStream Out = new(), OutSymbols = Symbols != null ? new MemoryStream() : null;
            WriterParameters Writing = new() { WriteSymbols = Symbols != null };
            if (Symbols != null)
                Writing.SymbolStream = OutSymbols;
            Definition.Write(Out, Writing);
            return (Out.ToArray(), OutSymbols?.ToArray());
        }

        /// <summary>
        /// Loads the mod from these files and starts it; true once it said it's up
        /// </summary>
        private static bool TryStart(byte[][] Bytes)
        {
            try
            {
                string Suffix = "-" + (++Loads);
                Load(Bytes[2], Bytes[3], Suffix);
                Mod = Load(Bytes[0], Bytes[1], Suffix);
                Type Entry = Mod.GetType("COTLMP.Plugin", true);

                AppDomain.CurrentDomain.SetData("COTLMP.Ready", null);
                Holder = new GameObject("COTLMP");
                DontDestroyOnLoad(Holder);
                Holder.hideFlags = HideFlags.HideAndDontSave;
                Holder.AddComponent(Entry);
            }
            catch (Exception e)
            {
                Instance.Logger.LogError($"Loading the mod failed: {e}");
                return false;
            }

            /* The mod's Awake doesn't throw out to here: it says it's ready at its end */
            if (AppDomain.CurrentDomain.GetData("COTLMP.Ready") is not string Version)
                return false;
            Running = Bytes;
            Instance.Logger.LogInfo($"Mod {Version} loaded{(Loads > 1 ? " in place of the old one" : "")}");
            return true;
        }

        /// <summary>
        /// Lets the running mod go: it closes what it has open and takes its hooks back
        /// </summary>
        private static void Stop()
        {
            try
            {
                Mod?.GetType("COTLMP.Network.HotUpdate")?.GetMethod("Release", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(null, null);
            }
            catch (Exception e)
            {
                Instance.Logger.LogWarning($"The old mod didn't let go cleanly: {e}");
            }
            if (Holder != null)
                DestroyImmediate(Holder);
            Holder = null;
            Mod = null;
        }

        /// <summary>
        /// Called by the mod (through reflection) once it has written a newer version of its files:
        /// the next frame it is let go and the new one started. False if that can't be done now.
        /// </summary>
        /// <param name = "From">The running mod's version, told to the new one.</param>
        public static bool Swap(string From)
        {
            if (Instance == null || Swapping)
                return false;
            Swapping = true;
            Instance.StartCoroutine(SwapNow(From));
            return true;
        }

        private static IEnumerator SwapNow(string From)
        {
            /* Out of the mod's own frame: it is going away under its own feet otherwise */
            yield return null;

            byte[][] Old = Running, New;
            try
            {
                New = Read();
            }
            catch (Exception e)
            {
                Instance.Logger.LogError($"The new mod's files don't read, keeping the running one: {e.Message}");
                Swapping = false;
                yield break;
            }

            Instance.Logger.LogInfo($"Letting mod {From} go for the new one");
            Stop();
            yield return null;

            AppDomain.CurrentDomain.SetData("COTLMP.SwappedFrom", From);
            if (!TryStart(New))
            {
                Instance.Logger.LogError($"The new mod didn't start: back to {From}");
                Stop();
                yield return null;
                try
                {
                    Write(Old);
                }
                catch (Exception e)
                {
                    Instance.Logger.LogError($"Can't write mod {From}'s files back: {e.Message}");
                }
                AppDomain.CurrentDomain.SetData("COTLMP.SwappedFrom", null);
                AppDomain.CurrentDomain.SetData("COTLMP.SwapFailed", From);
                TryStart(Old);
            }
            Swapping = false;
        }
    }
}

/* EOF */
