/*
 * PROJECT:     Cult of the Lamb Co-op Anything installer
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Entry point: the window, or a silent install for scripts
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;

namespace CotlCoopInstaller
{
    internal static class Program
    {
        /// <summary>
        /// CotlCoopInstaller.exe [--dir "game folder"] [--auto install|uninstall] [--quiet] [--shot window.png] [--lang ru|en]
        /// [--wait game-process-id] [--relaunch]
        /// </summary>
        /// <remarks>
        /// The mod updating itself starts a new installer with --wait (the game it closes) and --relaunch
        /// (start the game again from Steam once installed).
        /// </remarks>
        [STAThread]
        private static int Main(string[] Args)
        {
            string Dir = null, Auto = null, Shot = null;
            bool Quiet = false, Relaunch = false;
            string RelaunchArgs = null;
            int Wait = 0;
            for (int Index = 0; Index < Args.Length; Index++)
            {
                string Next() => Index + 1 < Args.Length ? Args[++Index] : null;
                switch (Args[Index].ToLowerInvariant())
                {
                    case "--dir": Dir = Next(); break;
                    case "--auto": Auto = Next()?.ToLowerInvariant(); break;
                    case "--quiet": Quiet = true; break;
                    case "--shot": Shot = Next(); break;
                    case "--lang": Words.Russian = Next() == "ru"; break;
                    case "--wait": int.TryParse(Next(), out Wait); break;
                    case "--relaunch": Relaunch = true; break;
                    case "--pretend-version": InstallerForm.PretendVersion = Next(); break;
                    case "--relaunch-args": RelaunchArgs = Decode(Next()); break;
                }
            }

            /* The game that started us closes first: its mod files are in use until then */
            if (Wait > 0)
            {
                try
                {
                    Process.GetProcessById(Wait).WaitForExit(120000);
                }
                catch (ArgumentException)
                {
                    /* Gone already */
                }
            }

            /* Without the window: exit code 0 when done, the error on stderr otherwise */
            if (Quiet)
            {
                try
                {
                    Dir ??= Installer.FindGame()?.Dir;
                    if (!Installer.IsGameFolder(Dir))
                        throw new InvalidOperationException("No game folder: " + Dir);
                    if (Installer.GameRunning(Dir))
                        throw new InvalidOperationException("The game is running");
                    int Count = Auto == "uninstall" ? Installer.Uninstall(Dir, null) : Installer.Install(Dir, null);
                    Console.WriteLine($"{(Auto == "uninstall" ? "Removed" : "Installed")} {Count} files in {Dir}");
                    if (Relaunch && Auto != "uninstall")
                        Installer.LaunchGame(Dir, RelaunchArgs);
                    return 0;
                }
                catch (Exception Error)
                {
                    Console.Error.WriteLine(Error.Message);
                    return 1;
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using InstallerForm Window = new(Dir, Auto, Relaunch, RelaunchArgs);
            if (Shot != null)
            {
                Window.SaveShot(Shot);
                return 0;
            }
            Application.Run(Window);
            return 0;
        }

        /// <summary>
        /// The game's own arguments, as the mod sent them (base64 of the command line after the exe)
        /// </summary>
        private static string Decode(string Text)
        {
            try
            {
                return string.IsNullOrEmpty(Text) ? null : System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(Text));
            }
            catch (FormatException)
            {
                return null;
            }
        }

        /// <summary>
        /// Runs this installer again with administrator rights for the folder and the step chosen
        /// </summary>
        internal static bool Elevate(string Dir, string Auto, bool Relaunch = false)
        {
            try
            {
                Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location, $"--dir \"{Dir}\" --auto {Auto}{(Relaunch ? " --relaunch" : "")}")
                {
                    UseShellExecute = true,
                    Verb = "runas"
                });
                return true;
            }
            catch (Win32Exception)
            {
                /* The prompt was turned down */
                return false;
            }
        }
    }
}
