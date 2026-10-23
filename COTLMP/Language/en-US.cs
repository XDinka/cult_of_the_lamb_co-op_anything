/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     English (United States) localization file
 * COPYRIGHT:	Copyright 2025-2026 GeoB99 <geobman1999@gmail.com>
 */

/* IMPORTS ********************************************************************/

using COTLMP;
using COTLMP.Api;
using COTLMP.Data;

/* TRANSLATION ****************************************************************/

namespace COTLMP.Language
{
    public class English
    {
        public static LocalizationTable[] StringsTable =
        [
            /* Main Multiplayer header resource strings */
            new("UI/DLC", "Multiplayer", true),
            new("UI/Banner", $"Multiplayer Edition v{COTLMP.Data.Version.CotlMpVer}", false),
            new("Multiplayer/UI/TitleDialog", "Multiplayer", false),

            /* Multiplayer settings resource strings */
            new("Multiplayer/UI/Settings", "Multiplayer Settings", false),
            new("Multiplayer/UI/Settings/DisableMod", "Mod Toggle", false),
            new("Multiplayer/UI/Settings/PlayerName", "Player Name", false),
            new("Multiplayer/UI/Settings/ServerName", "Server Name", false),
            new("Multiplayer/UI/Settings/GameMode", "Game Mode", false),
            new("Multiplayer/UI/Settings/PlayerCount", "Number of maximum players to join", false),
            new("Multiplayer/UI/Settings/VoiceChat", "Enable Voice Chat", false),
            new("Multiplayer/UI/Settings/ProtectServer", "Protect the server with a password", false),

            /* Servers browser UI resource strings */
            new("Multiplayer/UI/ServerList/BackButton", "Back", false),
            new("Multiplayer/UI/ServerList/PlayerNameDescription", "Enter your player name", false),
            new("Multiplayer/UI/ServerList/ServerNameDescription", "Enter the server name", false),
            new("Multiplayer/UI/ServerList/MainDescription", "Browse the servers list and click Connect to join a server", false),
            new("Multiplayer/UI/ServerList/NoneFound", "No servers could be found", false),
            new("Multiplayer/UI/ServerList/MasterFail", "Failed to connect to the masterserver", false),
            new("Multiplayer/UI/ServerList/ScanProgress", "Searching for reachable servers in progress...", false),
            new("Multiplayer/UI/ServerList/ConnectButton", "Connect", false),

            /* Server password prompt UI resource strings */
            new("Multiplayer/UI/PasswordPrompt/OkButton", "OK", false),
            new("Multiplayer/UI/PasswordPrompt/CancelButton", "Cancel", false),
            new("Multiplayer/UI/PasswordPrompt/PromptDescription", "This server is password protected. Type the password in order to join.", false),
            new("Multiplayer/UI/PasswordPrompt/InvalidPassword", "The submitted password is not valid!", false),

            /* Connect by IP dialog box resource strings */
            new("Multiplayer/UI/JoinByIP/Connect", "Connect", false),
            new("Multiplayer/UI/JoinByIP/Cancel", "Cancel", false),
            new("Multiplayer/UI/JoinByIP/Description", "Type the IP address of the server that you want to join. The address must follow the following format as in the example below.\n\nExample: 111.55.222.333:36963", false),

            /* Game mod resource strings */
            new("Multiplayer/Game/Join", "{0} has joined the server", false),
            new("Multiplayer/Game/Left", "{0} has left the server", false),

            /* General UI resource strings */
            new("Multiplayer/UI/StartServer", "Open to friends", false),
            new("Multiplayer/UI/SteamInviteHint", "Steam friends can join with Shift+Tab invites or Join Game", false),
            new("Multiplayer/UI/ServerStarted", "Stop hosting", false),
            new("Multiplayer/UI/ServerConfirm", "Stop hosting? Your friends get disconnected and you keep playing your game.", false),
            new("Multiplayer/UI/LeaveServer", "Leave server", false),
            new("Multiplayer/UI/LeaveConfirm", "Do you want to leave the server?", false),
            new("Multiplayer/UI/Disconnected", "Disconnected", false),
            new("Multiplayer/UI/DisconnectedError", "An error has ocurred (check console)", false),

            /* Steam invites */
            new("Multiplayer/Steam/JoinAfterLoad", "Load your save to join your friend", false),
            new("Multiplayer/Steam/Joined", "Joined your friend", false),
            new("Multiplayer/Steam/JoinFailed", "Couldn't reach your friend", false),
            new("Multiplayer/Steam/HostLeft", "Your friend stopped hosting", false),
            new("Multiplayer/Steam/Left", "You left your friend's game", false),
            new("Multiplayer/Steam/Stopped", "Stopped hosting", false),

            /* Back into the run after a crash */
            new("Multiplayer/Recovery/Returning", "Going back to your friend's run", false),
            new("Multiplayer/Recovery/Restored", "Back in the run", false),
            new("Multiplayer/Recovery/RunOver", "Your friend's run is over", false),
            new("Multiplayer/Recovery/Failed", "Couldn't go back to your friend's run", false)
        ];
    }
}

/* EOF */
