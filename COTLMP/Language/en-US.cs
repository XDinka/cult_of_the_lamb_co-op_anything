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
            new("Multiplayer/UI/StartServer", "Open Online", false),
            new("Multiplayer/UI/SteamInviteHint", "Steam friends can join with Shift+Tab invites or Join Game", false),
            new("Multiplayer/UI/ServerStarted", "Stop hosting", false),
            new("Multiplayer/UI/ServerConfirm", "Stop hosting? Your friends get disconnected and you keep playing your game.", false),
            new("Multiplayer/UI/LeaveServer", "Leave server", false),
            new("Multiplayer/UI/LeaveConfirm", "Do you want to leave the server?", false),
            new("Multiplayer/UI/Disconnected", "Disconnected", false),
            new("Multiplayer/UI/DisconnectedError", "An error has ocurred (check console)", false),
            new("Multiplayer/UI/Players", "Players", false),
            new("Multiplayer/UI/Host", "host", false),
            new("Multiplayer/UI/You", "you", false),
            new("Multiplayer/UI/Ms", "ms", false),

            /* Steam invites */
            new("Multiplayer/Steam/AfterPrologue", "Online play opens after the prologue, in your cult", false),
            new("Multiplayer/Steam/LoadingHostSave", "Loading your friend's game", false),
            new("Multiplayer/Steam/HostSaveFailed", "Couldn't load your friend's game", false),
            new("Multiplayer/Steam/Joined", "Joined your friend", false),
            new("Multiplayer/Steam/JoinFailed", "Couldn't reach your friend", false),
            new("Multiplayer/Steam/HostLeft", "Your friend stopped hosting", false),
            new("Multiplayer/Steam/Left", "You left your friend's game", false),
            new("Multiplayer/Steam/Stopped", "Stopped hosting", false),
            new("Multiplayer/Steam/LeaveFirst", "Stop hosting to join your friend", false),
            new("Multiplayer/Cult/FollowerBusy", "Someone is already talking to this follower", false),

            /* Mod versions of the two players */
            new("Multiplayer/Version/Title", "Different mod versions", false),
            new("Multiplayer/Version/Lines", "Host's version: {0}\nYour version: {1}", false),
            new("Multiplayer/Version/FriendLines", "Your friend's version: {0}\nYour version: {1}", false),
            new("Multiplayer/Version/Unknown", "unknown (0.2.8 or older)", false),
            new("Multiplayer/Version/Risk", "You can play, but different versions may cause errors or even damage the save.", false),
            new("Multiplayer/Version/UpdateToHost", "Update to {0}? The game restarts and joins the host again by itself.", false),
            new("Multiplayer/Version/HostOlder", "The host has an older version. You can ask the host to update.", false),
            new("Multiplayer/Version/HostUpdate", "Update now? The game restarts; then open the game online again and pick your save.", false),
            new("Multiplayer/Version/FriendAsks", "Your friend asks you to update the mod", false),
            new("Multiplayer/Version/FriendHas", "Your friend's mod is {0}, yours is {1}", false),
            new("Multiplayer/Version/Update", "Update", false),
            new("Multiplayer/Version/PlayAnyway", "Play anyway", false),
            new("Multiplayer/Version/Suggest", "Ask the host to update", false),
            new("Multiplayer/Version/NotNow", "Not now", false),

            /* Online menu and lobby */
            new("Multiplayer/Online/Title", "Online Play", false),
            new("Multiplayer/Online/CreateLobby", "Create Lobby", false),
            new("Multiplayer/Online/FriendsLobbies", "Friends' lobbies", false),
            new("Multiplayer/Online/NoLobbies", "No friend has a lobby open", false),
            new("Multiplayer/Online/Joining", "Joining {0}...", false),
            new("Multiplayer/Online/PickSave", "Save for the lobby", false),
            new("Multiplayer/Online/Back", "Back", false),
            new("Multiplayer/Lobby/Title", "Lobby", false),
            new("Multiplayer/Lobby/NewGame", "New game", false),
            new("Multiplayer/Lobby/HostSave", "{0}'s game: {1}", false),
            new("Multiplayer/Lobby/Play", "Play", false),
            new("Multiplayer/Lobby/Invite", "Invite a Friend", false),
            new("Multiplayer/Lobby/Leave", "Leave Lobby", false),
            new("Multiplayer/Lobby/Close", "Close Lobby", false),
            new("Multiplayer/Lobby/WaitingFriend", "Waiting for a friend...", false),
            new("Multiplayer/Lobby/WaitingHost", "Waiting for the host to start", false),
            new("Multiplayer/Lobby/Failed", "Couldn't open the lobby", false),
            new("Multiplayer/Intro/WaitCrown", "Waiting for your friend to receive the crown...", false),

            /* Opening online play */
            new("Multiplayer/UI/OnlineOpen", "Online play is open", false),
            new("Multiplayer/UI/OnlineFailed", "Couldn't open online play", false),
            new("Multiplayer/Steam/GameVersion", "Your friend's game is version {0}, yours is {1}. Update the game in Steam on both sides.", false),

            /* The night fox */
            new("Multiplayer/Fox/HostDeals", "The host makes deals with the fox", false),

            /* Updates */
            new("Multiplayer/Update/Title", "A new version of the co-op mod", false),
            new("Multiplayer/Update/Body", "Version {0} is out, you have {1}. Update now? The game will close, the installer will put in the new version and start the game again.", false),
            new("Multiplayer/Update/Downloading", "Downloading the update…", false),
            new("Multiplayer/Update/Failed", "Couldn't update by itself", false),
            new("Multiplayer/Update/OpenPage", "Open the page of version {0} to download it?", false),

            /* The map between floors */
            new("Multiplayer/Map/WaitingForHost", "Waiting for the host to choose the way", false),

            /* Back into the run after a crash */
            new("Multiplayer/Recovery/Returning", "Going back to your friend's run", false),
            new("Multiplayer/Recovery/Restored", "Back in the run", false),
            new("Multiplayer/Recovery/RunOver", "Your friend's run is over", false),
            new("Multiplayer/Recovery/Failed", "Couldn't go back to your friend's run", false)
        ];
    }
}

/* EOF */
