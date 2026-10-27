/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     "Copy the log" in the mod's error windows, for a bug report
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using BepInEx;
using HarmonyLib;
using Lamb.UI;
using src.UI;
using System;
using System.IO;
using TMPro;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Ui
{
    /// <summary>
    /// The log for a bug report, from the window that tells of the error.
    /// </summary>
    /// <remarks>
    /// The mod's error windows (disconnected, connecting again failed, patches that don't fit, an update that
    /// didn't go in, online that didn't open) get one more button next to the game's own: "Copy the log". It
    /// puts BepInEx's LogOutput.log on the clipboard and opens the mod's Discord, where bugs and ideas go: the
    /// log is pasted into a message there (Discord makes a long one a file). The window stays open.
    /// </remarks>
    internal static class LogButton
    {
        /* Counters for the test harness report */
        internal static int Added, Copied;

        /// <summary>
        /// The test harness's copies: no Explorer window over the player's screen
        /// </summary>
        internal static bool Quiet;

        internal static string LogFile => Path.Combine(Paths.BepInExRootPath, "LogOutput.log");

        /// <summary>
        /// The mod's Discord: bug reports and ideas
        /// </summary>
        internal const string Discord = "https://discord.gg/52azdsYMD";

        /// <summary>
        /// The mod's Discord in the browser (not on the test harness's copies)
        /// </summary>
        internal static void OpenDiscord()
        {
            Plugin.Logger?.LogInfo("[COOP] The player opens the mod's Discord");
            if (!Quiet)
                Application.OpenURL(Discord);
        }

        /// <summary>
        /// The button, after the window's own (a copy of its confirm button, with its look)
        /// </summary>
        internal static void Add(UIMenuConfirmationWindow Window)
        {
            try
            {
                MMButton Confirm = Traverse.Create(Window).Field("_confirmButton").GetValue<MMButton>();
                if (Window == null || Confirm == null)
                    return;
                MMButton Copy = UnityEngine.Object.Instantiate(Confirm.gameObject, Confirm.transform.parent, false).GetComponent<MMButton>();
                Copy.name = "COTLMP Copy Log";
                Copy.transform.SetAsLastSibling();
                Copy.onClick.RemoveAllListeners();
                /* Its label is ours: the copied button's localisation would put "Accept" back */
                foreach (I2.Loc.Localize Localize in Copy.GetComponentsInChildren<I2.Loc.Localize>(true))
                    UnityEngine.Object.Destroy(Localize);
                TextMeshProUGUI Label = Copy.GetComponentInChildren<TextMeshProUGUI>(true);
                if (Label != null)
                    Label.text = I2.Loc.LocalizationManager.GetTranslation("Multiplayer/UI/CopyLog");
                Copy.onClick.AddListener(() =>
                {
                    if (CopyLog() && Label != null)
                        Label.text = I2.Loc.LocalizationManager.GetTranslation("Multiplayer/UI/LogCopied");
                });
                ++Added;
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Can't add the log button: {e.Message}");
            }
        }

        /// <summary>
        /// The log on the clipboard and the Discord open to paste it
        /// </summary>
        internal static bool CopyLog()
        {
            try
            {
                Plugin.Logger?.LogInfo("[COOP] The player copies the log for a report");
                /* BepInEx keeps it open for writing */
                using (FileStream Stream = new(LogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (StreamReader Reader = new(Stream))
                    GUIUtility.systemCopyBuffer = Reader.ReadToEnd();
                ++Copied;
                OpenDiscord();
                return true;
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[COOP] Can't copy the log: {e.Message}");
                return false;
            }
        }
    }
}

/* EOF */
