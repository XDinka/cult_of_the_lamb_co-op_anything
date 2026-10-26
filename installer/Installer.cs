/*
 * PROJECT:     Cult of the Lamb Co-op Anything installer
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Finds the game, puts the mod in, takes it out again
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace CotlCoopInstaller
{
    /// <summary>
    /// The install itself, the same as packaging/install.ps1 and uninstall.ps1 do it: what the mod
    /// overwrites is copied aside first, and COTLMP-installed.txt lists what was put in, so either
    /// this installer or uninstall.ps1 can take it out again.
    /// </summary>
    internal static class Installer
    {
        internal const string AppId = "1313140";
        internal const string ExpectedBuild = "25223484";
        internal const string ExpectedVersion = "1.5.26";
        internal const string GameExe = "Cult Of The Lamb.exe";
        internal const string ManifestName = "COTLMP-installed.txt";

        internal sealed class FoundGame
        {
            public string Dir;
            public string Build;
        }

        /// <summary>
        /// The game in any of the Steam libraries, or null
        /// </summary>
        internal static FoundGame FindGame()
        {
            List<string> Libraries = new();
            foreach (string Steam in SteamFolders())
            {
                Libraries.Add(Steam);
                string Vdf = Path.Combine(Steam, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(Vdf))
                    continue;
                foreach (Match Found in Regex.Matches(File.ReadAllText(Vdf), "\"path\"\\s+\"([^\"]+)\""))
                    Libraries.Add(Found.Groups[1].Value.Replace("\\\\", "\\"));
            }
            foreach (string Library in Libraries.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string Acf = Path.Combine(Library, "steamapps", "appmanifest_" + AppId + ".acf");
                if (!File.Exists(Acf))
                    continue;
                string Text = File.ReadAllText(Acf);
                string InstallDir = Regex.Match(Text, "\"installdir\"\\s+\"([^\"]+)\"").Groups[1].Value;
                string Dir = Path.Combine(Library, "steamapps", "common", InstallDir);
                if (File.Exists(Path.Combine(Dir, GameExe)))
                    return new FoundGame { Dir = Dir, Build = Regex.Match(Text, "\"buildid\"\\s+\"([^\"]+)\"").Groups[1].Value };
            }
            return null;
        }

        private static IEnumerable<string> SteamFolders()
        {
            string[] Keys =
            {
                @"HKEY_CURRENT_USER\Software\Valve\Steam|SteamPath",
                @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam|InstallPath",
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam|InstallPath"
            };
            foreach (string Key in Keys)
            {
                string[] Parts = Key.Split('|');
                string Value = null;
                try
                {
                    Value = Registry.GetValue(Parts[0], Parts[1], null) as string;
                }
                catch
                {
                }
                if (!string.IsNullOrEmpty(Value) && Directory.Exists(Value))
                    yield return Path.GetFullPath(Value.Replace('/', '\\'));
            }
        }

        internal static bool IsGameFolder(string Dir) =>
            !string.IsNullOrEmpty(Dir) && File.Exists(Path.Combine(Dir, GameExe));

        internal static bool IsInstalled(string Dir) =>
            !string.IsNullOrEmpty(Dir) && File.Exists(Path.Combine(Dir, ManifestName));

        /// <summary>
        /// The game started from this folder is running
        /// </summary>
        internal static bool GameRunning(string Dir)
        {
            string Exe = Path.GetFullPath(Path.Combine(Dir, GameExe));
            foreach (Process Running in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(GameExe)))
            {
                try
                {
                    if (string.Equals(Path.GetFullPath(Running.MainModule.FileName), Exe, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch
                {
                    /* Not ours to look into: assume it may be this game */
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// We may write into the folder without administrator rights
        /// </summary>
        internal static bool CanWrite(string Dir)
        {
            try
            {
                string Probe = Path.Combine(Dir, ".cotlmp-write-test");
                File.WriteAllText(Probe, "");
                File.Delete(Probe);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
        }

        private static Stream Payload() =>
            Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip")
            ?? throw new InvalidOperationException("The installer has no payload");

        /// <summary>
        /// What was in the manifest of an earlier install: its files and backup folder
        /// </summary>
        private static (List<string> Files, string Backup) ReadManifest(string Dir)
        {
            List<string> Files = new();
            string Backup = null;
            string Manifest = Path.Combine(Dir, ManifestName);
            if (!File.Exists(Manifest))
                return (Files, null);
            foreach (string Line in File.ReadAllLines(Manifest))
            {
                string Text = Line.Trim('\uFEFF', ' ');
                if (Text.StartsWith("#backup ", StringComparison.Ordinal))
                    Backup = Text.Substring(8);
                else if (Text.Length > 0 && !Text.StartsWith("#", StringComparison.Ordinal))
                    Files.Add(Text);
            }
            return (Files, Backup);
        }

        /// <summary>
        /// Puts the mod into the game folder. An earlier install is updated in place and keeps
        /// its backup of what was there before it.
        /// </summary>
        internal static int Install(string Dir, Action<int, int, string> Progress)
        {
            var (Earlier, Backup) = ReadManifest(Dir);
            HashSet<string> Ours = new(Earlier, StringComparer.OrdinalIgnoreCase);
            Backup ??= "COTLMP-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string BackupDir = Path.Combine(Dir, Backup);
            bool BackedUp = Directory.Exists(BackupDir);

            /* Before 0.3.6 the mod itself was the plugin; now the loader is, and the mod is in Mod\ (both would load) */
            foreach (string Old in new[] { "COTLMP.dll", "COTLMP.pdb", "COTLMPServer.dll", "COTLMPServer.pdb" })
            {
                string Before = Path.Combine(Dir, "BepInEx", "plugins", "COTLMP", Old);
                if (File.Exists(Before))
                    File.Delete(Before);
            }
            /* Before 0.3.6 the mod had asset bundles for COTLMP's server browser, which is gone */
            string OldAssets = Path.Combine(Dir, "BepInEx", "plugins", "COTLMP", "Assets");
            if (Directory.Exists(OldAssets))
                Directory.Delete(OldAssets, true);

            List<string> Installed = new();
            using (ZipArchive Zip = new(Payload(), ZipArchiveMode.Read))
            {
                List<ZipArchiveEntry> Entries = Zip.Entries.Where(Entry => !Entry.FullName.EndsWith("/", StringComparison.Ordinal)).ToList();
                for (int Index = 0; Index < Entries.Count; Index++)
                {
                    ZipArchiveEntry Entry = Entries[Index];
                    string Relative = Entry.FullName.Replace('/', '\\');
                    if (Relative.Contains("..") || Path.IsPathRooted(Relative))
                        continue;
                    Progress?.Invoke(Index, Entries.Count, Relative);
                    string Target = Path.Combine(Dir, Relative);

                    /* Someone else's file is copied aside, ours from the last install is just replaced */
                    if (File.Exists(Target) && !Ours.Contains(Relative))
                    {
                        string Copy = Path.Combine(BackupDir, Relative);
                        Directory.CreateDirectory(Path.GetDirectoryName(Copy));
                        File.Copy(Target, Copy, true);
                        BackedUp = true;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(Target));
                    using (Stream From = Entry.Open())
                    using (FileStream To = new(Target, FileMode.Create, FileAccess.Write))
                        From.CopyTo(To);
                    Installed.Add(Relative);
                }
                Progress?.Invoke(Entries.Count, Entries.Count, "");
            }

            /* Files an earlier version put in and this one doesn't have stay listed, to be removed later */
            foreach (string Old in Earlier)
            {
                if (!Installed.Contains(Old, StringComparer.OrdinalIgnoreCase) && File.Exists(Path.Combine(Dir, Old)))
                    Installed.Add(Old);
            }

            StringBuilder Manifest = new();
            Manifest.AppendLine("# Files installed by Cult of the Lamb Co-op Anything. The installer or uninstall.ps1 removes them.");
            if (BackedUp)
                Manifest.AppendLine("#backup " + Backup);
            foreach (string Relative in Installed)
                Manifest.AppendLine(Relative);
            File.WriteAllText(Path.Combine(Dir, ManifestName), Manifest.ToString(), new UTF8Encoding(true));
            return Installed.Count;
        }

        /// <summary>
        /// Takes the mod out and puts back what it replaced
        /// </summary>
        internal static int Uninstall(string Dir, Action<int, int, string> Progress)
        {
            var (Files, Backup) = ReadManifest(Dir);
            if (!IsInstalled(Dir))
                throw new InvalidOperationException("The mod isn't installed in " + Dir);

            for (int Index = 0; Index < Files.Count; Index++)
            {
                Progress?.Invoke(Index, Files.Count, Files[Index]);
                string Target = Path.Combine(Dir, Files[Index]);
                if (File.Exists(Target))
                    File.Delete(Target);
            }

            /* Folders the install made that are empty now, deepest first */
            HashSet<string> Folders = new(StringComparer.OrdinalIgnoreCase);
            foreach (string Relative in Files)
            {
                for (string Folder = Path.GetDirectoryName(Relative); !string.IsNullOrEmpty(Folder); Folder = Path.GetDirectoryName(Folder))
                    Folders.Add(Folder);
            }
            foreach (string Folder in Folders.OrderByDescending(Item => Item.Length))
            {
                string Full = Path.Combine(Dir, Folder);
                if (Directory.Exists(Full) && !Directory.EnumerateFileSystemEntries(Full).Any())
                    Directory.Delete(Full);
            }

            if (Backup != null)
            {
                string BackupDir = Path.Combine(Dir, Backup);
                if (Directory.Exists(BackupDir))
                {
                    foreach (string Saved in Directory.GetFiles(BackupDir, "*", SearchOption.AllDirectories))
                    {
                        string Target = Path.Combine(Dir, Saved.Substring(BackupDir.Length + 1));
                        Directory.CreateDirectory(Path.GetDirectoryName(Target));
                        File.Copy(Saved, Target, true);
                    }
                    Directory.Delete(BackupDir, true);
                }
            }
            File.Delete(Path.Combine(Dir, ManifestName));
            Progress?.Invoke(Files.Count, Files.Count, "");
            return Files.Count;
        }

        /// <summary>
        /// Starts the game through Steam, so the Steam overlay and invites work
        /// </summary>
        internal static void LaunchGame(string Dir = null, string Arguments = null)
        {
            /* Started as it was before an update (its launch options), else as Steam starts it */
            string Exe = Dir == null ? null : Path.Combine(Dir, "Cult Of The Lamb.exe");
            if (!string.IsNullOrEmpty(Arguments) && File.Exists(Exe))
            {
                /* Started from a modded game, we may carry its loader's marks: the game would start without mods */
                foreach (string Name in Environment.GetEnvironmentVariables().Keys.Cast<string>().Where(Name => Name.StartsWith("DOORSTOP", StringComparison.OrdinalIgnoreCase)).ToList())
                    Environment.SetEnvironmentVariable(Name, null);
                Process.Start(new ProcessStartInfo(Exe, Arguments) { UseShellExecute = true, WorkingDirectory = Dir });
                return;
            }
            Process.Start(new ProcessStartInfo("steam://rungameid/" + AppId) { UseShellExecute = true });
        }
    }
}
