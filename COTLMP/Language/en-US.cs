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

            /* Servers browser UI resource strings */

            /* Server password prompt UI resource strings */

            /* Connect by IP dialog box resource strings */

            /* Game mod resource strings */

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
            new("Multiplayer/Reconnect/Connecting", "Connecting {0}/{1}", false),
            new("Multiplayer/Reconnect/Failed", "Lost the connection to your friend: couldn't connect again in 3 tries", false),
            new("Multiplayer/Reconnect/CancelHint", "Esc: stop trying", false),
            new("Multiplayer/Reconnect/Cancel", "Stop reconnecting", false),
            new("Multiplayer/Reconnect/Cancelled", "Connecting again was called off", false),
            new("Multiplayer/UI/PatchesFailedTitle", "Co-op Anything", false),
            new("Multiplayer/UI/PatchesFailed", "{0} of the mod's hooks don't fit this game version: the game was updated after the mod. Co-op may not work right until a fixed version is out; the mod offers it here as soon as it is", false),
            new("Multiplayer/UI/PatchesUpdate", "{0} of the mod's hooks don't fit this game version: the game was updated after the mod. Version {1} is out (you have {2}). Update now?", false),
            new("Multiplayer/UI/PatchesOurs", "{0} of the mod's hooks don't fit your game version: co-op may not work right", false),
            new("Multiplayer/UI/PatchesPartner", "{0}: {1} of the mod's hooks don't fit their game version, co-op may not work right", false),
            new("Multiplayer/UI/CopyLog", "Copy the log", false),
            new("Multiplayer/UI/LogCopied", "Log copied", false),
            new("Multiplayer/Online/Discord", "Bugs and ideas", false),
            new("Multiplayer/Chat/Tip", "Press T to chat with your friend", false),
            new("Multiplayer/Chat/Hint", "Message… Enter sends, Esc closes", false),
            new("Multiplayer/Steam/Left", "You left your friend's game", false),
            new("Multiplayer/Steam/Stopped", "Stopped hosting", false),
            new("Multiplayer/Steam/LeaveFirst", "Stop hosting to join your friend", false),
            new("Multiplayer/Cult/FollowerBusy", "Someone is already talking to this follower", false),
            new("Multiplayer/Scene/WaitPartner", "Waiting for your partner to come in", false),
            new("Multiplayer/Scene/WaitScene", "Waiting for your partner to finish watching", false),
            new("Multiplayer/Scene/WaitSync", "Waiting for the sync to be confirmed", false),

            /* Mod versions of the two players */
            new("Multiplayer/Version/Title", "Different mod versions", false),
            new("Multiplayer/Version/Lines", "Host's version: {0}\nYour version: {1}", false),
            new("Multiplayer/Version/FriendLines", "Your friend's version: {0}\nYour version: {1}", false),
            new("Multiplayer/Version/Unknown", "unknown (0.2.8 or older)", false),
            new("Multiplayer/Version/Risk", "You can play, but different versions may cause errors or even damage the save.", false),
            new("Multiplayer/Version/UpdateToHost", "Update to {0}? It goes in right here in the main menu, then you join the host again by itself.", false),
            new("Multiplayer/Version/HostOlder", "The host has an older version. You can ask the host to update.", false),
            new("Multiplayer/Version/HostUpdate", "Update now? It downloads while you play; then you pick how it goes in: back to the lobby on this save, or the game restarted.", false),
            new("Multiplayer/Version/FriendAsks", "Your friend asks you to update the mod", false),
            new("Multiplayer/Version/FriendHas", "Your friend's mod is {0}, yours is {1}", false),
            new("Multiplayer/Version/FriendRefused", "Your friend's mod is {0}. Versions before 0.9.4 take what the DLCs give out of your save, so your friend can't join. Ask them to update in the game's main menu.", false),
            new("Multiplayer/Version/HostStrips", "The host's mod {0} takes what the DLCs give out of the host's own save.", false),
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
            new("Multiplayer/Online/JoinFailed", "Couldn't reach the friend's lobby", false),
            new("Multiplayer/Intro/WaitCrown", "Waiting for your friend to receive the crown...", false),

            /* Opening online play */
            new("Multiplayer/UI/OnlineOpen", "Online play is open", false),
            new("Multiplayer/UI/OnlineFailed", "Couldn't open online play", false),
            new("Multiplayer/Steam/GameVersion", "Your friend's game is version {0}, yours is {1}. Update the game in Steam on both sides.", false),

            /* The night fox */
            new("Multiplayer/Fox/HostDeals", "The host makes deals with the fox", false),

            /* Updates */
            new("Multiplayer/Update/Title", "A new version of the co-op mod", false),
            new("Multiplayer/Update/Body", "Version {0} is out, you have {1}. Update now? It goes in right here, without leaving the game. Only a version that needs a new BepInEx or COTL API restarts the game for the installer.", false),
            new("Multiplayer/Update/Downloading", "Downloading the update…", false),
            new("Multiplayer/Update/Screen", "Updating the co-op mod to {0}", false),
            new("Multiplayer/Update/Progress", "Downloading: {0} of {1} KB", false),
            new("Multiplayer/Update/Checking", "Checking the download", false),
            new("Multiplayer/Update/Starting", "Starting the new version", false),
            new("Multiplayer/Update/Restart", "This version needs the installer: the game will start again", false),
            new("Multiplayer/Update/Ready", "The update is ready: it starts in the main menu", false),
            new("Multiplayer/Update/Done", "The co-op mod is updated", false),
            new("Multiplayer/Update/ReadyTitle", "The update is ready", false),
            new("Multiplayer/Update/ReadyHost", "Version {0} is downloaded. To put it in, the game is saved, you go to the main menu and the lobby opens again on this save; your friend joins it as before. Nothing is played until you press Play.", false),
            new("Multiplayer/Update/ReadyFriend", "Version {0} is downloaded. To put it in, you go to the main menu and join the host again.", false),
            new("Multiplayer/Update/RestartLobby", "Restart the lobby", false),
            new("Multiplayer/Update/OtherWays", "Other ways", false),
            new("Multiplayer/Update/RestartGameBody", "Or the game is saved and starts again, then opens the same lobby. Later: the update goes in the next time you are in the main menu or start the game.", false),
            new("Multiplayer/Update/RestartGame", "Restart the game", false),
            new("Multiplayer/Update/Later", "Later", false),
            new("Multiplayer/Update/Saving", "Saving the game...", false),

            /* Coming back to the host */
            new("Multiplayer/Rejoin/Going", "Going back to the host...", false),

            /* Waiting for the partner in a room */
            new("Multiplayer/Scene/GameSpeaker", "Game", false),
            new("Multiplayer/Scene/HoldTalk", "Waiting for your partner: the conversation starts once they come into this room. You can move about the room meanwhile, its doors open after it.", false),
            new("Multiplayer/Scene/PartnerWaits", "Your partner is waiting for you: a conversation starts once you join them", false),
            new("Multiplayer/Scene/WaitHostLead", "The host's game lags behind: the conversation goes on as soon as it catches up", false),
            new("Multiplayer/Update/Failed", "Couldn't update by itself", false),
            new("Multiplayer/Update/OpenPage", "Open the page of version {0} to download it?", false)

            /* The map between floors */

            /* Back into the run after a crash */
        ];
    }
}

/* EOF */
