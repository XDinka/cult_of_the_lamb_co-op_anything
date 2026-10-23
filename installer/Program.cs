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
        /// </summary>
        [STAThread]
        private static int Main(string[] Args)
        {
            string Dir = null, Auto = null, Shot = null;
            bool Quiet = false;
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
            using InstallerForm Window = new(Dir, Auto);
            if (Shot != null)
            {
                Window.SaveShot(Shot);
                return 0;
            }
            Application.Run(Window);
            return 0;
        }

        /// <summary>
        /// Runs this installer again with administrator rights for the folder and the step chosen
        /// </summary>
        internal static bool Elevate(string Dir, string Auto)
        {
            try
            {
                Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location, $"--dir \"{Dir}\" --auto {Auto}")
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
