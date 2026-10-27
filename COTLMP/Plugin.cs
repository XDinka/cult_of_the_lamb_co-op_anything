/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Main plugin startup code file
 * COPYRIGHT:	Copyright 2025-2026 GeoB99 <geobman1999@gmail.com>
 */

/* IMPORTS ********************************************************************/

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using COTLMP.Api;
using COTLMP.Data;
using COTLMP.Ui;
using HarmonyLib;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

/* NAMESPACES *****************************************************************/

/* Main COTL MP plugin startup namespace */
namespace COTLMP;

/* CLASSES & CODE *************************************************************/

/* Initialize the base BepInEx attributes of the mod plug-in */
[BepInPlugin(COTLMP.Data.Version.CotlMpGuid, COTLMP.Data.Version.CotlMpName, COTLMP.Data.Version.CotlMpVer)]

/* COTL API is mandatorily required for the mod to run */
[BepInDependency("io.github.xhayper.COTL_API", BepInDependency.DependencyFlags.HardDependency)]

/// <summary>
/// Creates the COTL MP plug-in instance and initializes the critical COTL MP data.
/// </summary>
public class Plugin : BaseUnityPlugin
{
    internal static string CotlmpPathLocation;
    internal static new ManualLogSource Logger;
    internal static new ConfigFile Config;
    internal static ModDataGlobals Globals;
    internal static InternalData GlobalsInternal;
    internal static MonoBehaviour MonoInstance;
    private Harmony HarmonyInstance;

    /// <summary>
    /// Executes initialization code as the mod is being loaded by BepInEx.
    /// </summary>
    private void Awake()
    {
        System.Object SettingData;
        string PlayerName;
        bool ToggleMod;
        bool Success;

        /*
         * Start patching the game assembly with our code
         * and cache a harmony instance of this mod.
         */
        HarmonyInstance = COTLMP.Debug.PatchCheck.Apply(Assembly.GetExecutingAssembly());

        /*
         * Cache the path location of the COTLMP mod: the loader's folder (it loads us from memory,
         * so we have no file of our own), else our file's.
         */
        CotlmpPathLocation = AppDomain.CurrentDomain.GetData("COTLMP.Folder") as string ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        /*
         * Cache the plugin class methods and the Mono instance so that the
         * COTL MP mod can use them across different modules.
         */
        Logger = base.Logger;
        Config = base.Config;
        MonoInstance = this;
        COTLMP.Debug.PatchCheck.Report();
        COTLMP.Debug.Faults.Initialize();

        /* Fetch the flags and switches of the mod, reserved for internal use */
        GlobalsInternal = new InternalData();

        /* Load the localizations of the mod */
        foreach (string Language in new[] { "English", "Russian", "Japanese", "French", "German", "Spanish", "Portuguese (Brazil)",
                                            "Chinese (Simplified)", "Chinese (Traditional)", "Korean", "Italian", "Dutch", "Turkish",
                                            "French (Canadian)", "Arabic" })
            COTLMP.Api.Localization.LoadLocale(Language);

        /*
         * Initialize the settings of the mod to defaults in following
         * order, first the "Toggle Mod" setting. If the user configuration
         * file has already been created the function will simply load
         * the configuration entry of each setting.
         */
        SettingData = COTLMP.Api.Configuration.CreateSetting(CONFIGURATION_SECTION.ModSettings,
                                                             "Toggle Mod",
                                                             "Enables or Disables the Multiplayer mod. This setting has to be changed manually here!",
                                                             true);
        if (SettingData == null)
        {
            Logger.LogFatal("Failed to set default or load the \"Mod Toggle\" setting!");
            HarmonyInstance.UnpatchSelf();
            return;
        }

        /* Retrieve the setting data from the setting object */
        ToggleMod = COTLMP.Api.Configuration.GetSettingData<bool>(SettingData, false);

        /* Initialize the "Player Name" setting */
        SettingData = COTLMP.Api.Configuration.CreateSetting(CONFIGURATION_SECTION.ModSettings,
                                                             "Player Name",
                                                             "The name of the player in-game",
                                                             "The Player");
        if (SettingData == null)
        {
            Logger.LogFatal("Failed to set default or load the \"Player Name\" setting!");
            HarmonyInstance.UnpatchSelf();
            return;
        }

        /* Retrieve the setting data from the setting object */
        PlayerName = COTLMP.Api.Configuration.GetSettingData<string>(SettingData, false);

        /* Now store all the cached settings into the globals data store */
        Globals = new ModDataGlobals(ToggleMod,
                                     PlayerName);

        /* Initialize the network features */
        COTLMP.Game.MainThread.Initialize();
        COTLMP.Network.Network.Initialize();
        COTLMP.Network.SteamBridge.Initialize();
        COTLMP.Network.Reconnect.Initialize();
        COTLMP.Ui.TextChat.Initialize();
        COTLMP.Game.CoopSync.Initialize();
        COTLMP.Game.RoomSync.Initialize();
        COTLMP.Game.Appearance.Initialize();
        COTLMP.Game.CoopLife.Initialize();
        COTLMP.Game.CoopHud.Initialize();
        COTLMP.Game.Rejoin.Initialize();
        COTLMP.Game.ClearedRooms.Initialize();
        COTLMP.Game.Dialogs.Initialize();
        COTLMP.Game.HazardSync.Initialize();
        COTLMP.Game.RoomRandom.Initialize();
        COTLMP.Game.MapSync.Initialize();
        COTLMP.Game.MiniMapSync.Initialize();
        COTLMP.Game.MiniMapHeads.Initialize();
        COTLMP.Game.MapCrowns.Initialize();
        COTLMP.Game.SharedScenes.Initialize();
        COTLMP.Game.SceneEnds.Initialize();
        COTLMP.Game.PuppetSync.Initialize();
        COTLMP.Game.HostSave.Initialize();
        COTLMP.Game.CultSync.Initialize();
        COTLMP.Game.CultHands.Initialize();
        COTLMP.Game.CultResources.Initialize();
        COTLMP.Network.HotUpdate.Initialize();
        COTLMP.Network.Updater.Initialize();
        COTLMP.Game.PropSync.Initialize();
        COTLMP.Game.ConnectionQuality.Initialize();
        COTLMP.Debug.TestHarness.Initialize();
        COTLMP.Debug.HudProbe.Initialize();
        COTLMP.Game.Relobby.Initialize();

        /* Log to the debugger that our mod is loaded, and tell the loader we are up */
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        AppDomain.CurrentDomain.SetData("COTLMP.Ready", COTLMP.Data.Version.CotlMpVer);

        /* Started in a running game (an update): the main menu comes up again for us */
        if (COTLMP.Network.HotUpdate.Restarted)
            StartCoroutine(COTLMP.Network.HotUpdate.Arrive());
    }

    /// <summary>
    /// Performs additional tasks after the successful initialization of the mod.
    /// </summary>
    private void OnEnable()
    {
        /*
         * The user wants the mod to be disabled so stop the execution
         * of all the patches that have been loaded previously.
         */
        if (!Globals.EnableMod)
        {
            Logger.LogWarning("The user disabled the mod, stopping execution of the mod...");
            HarmonyInstance.UnpatchSelf();
            return;
        }

        Logger.LogMessage($"{HarmonyInstance.GetPatchedMethods().Count()} patches have been applied!");
    }

    /// <summary>
    /// Unloads all the patches hooked into game on quit event of the game.
    /// </summary>
    private void OnDisable()
    {
        Logger.LogMessage($"Unloading {MyPluginInfo.PLUGIN_GUID}");
        HarmonyInstance.UnpatchSelf();
        Logger.LogMessage("Mod has been unloaded!");
    }
}

/* EOF */
