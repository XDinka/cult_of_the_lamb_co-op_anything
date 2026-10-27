/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     German localization file
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP;
using COTLMP.Api;
using COTLMP.Data;

/* TRANSLATION ****************************************************************/

namespace COTLMP.Language
{
    public class German
    {
        public static LocalizationTable[] StringsTable =
        [
            new("UI/DLC", "Mehrspieler", true),
            new("UI/Banner", $"Mehrspieler-Edition v{COTLMP.Data.Version.CotlMpVer}", false),
            new("Multiplayer/UI/TitleDialog", "Mehrspieler", false),
            new("Multiplayer/UI/Settings", "Mehrspieler-Einstellungen", false),
            new("Multiplayer/UI/Settings/DisableMod", "Mod an/aus", false),
            new("Multiplayer/UI/Settings/PlayerName", "Spielername", false),
            new("Multiplayer/UI/StartServer", "Online öffnen", false),
            new("Multiplayer/UI/SteamInviteHint", "Steam-Freunde können über Einladungen (Umschalt+Tab) oder „Spiel beitreten“ mitspielen", false),
            new("Multiplayer/UI/ServerStarted", "Hosten beenden", false),
            new("Multiplayer/UI/ServerConfirm", "Hosten beenden? Deine Freunde werden getrennt, und du spielst dein Spiel weiter.", false),
            new("Multiplayer/UI/LeaveServer", "Server verlassen", false),
            new("Multiplayer/UI/LeaveConfirm", "Willst du den Server verlassen?", false),
            new("Multiplayer/UI/Disconnected", "Getrennt", false),
            new("Multiplayer/UI/DisconnectedError", "Ein Fehler ist aufgetreten (siehe Konsole)", false),
            new("Multiplayer/UI/Players", "Spieler", false),
            new("Multiplayer/UI/Host", "Host", false),
            new("Multiplayer/UI/You", "du", false),
            new("Multiplayer/UI/Ms", "ms", false),
            new("Multiplayer/Steam/AfterPrologue", "Online-Spiel ist nach dem Prolog in deinem Kult möglich", false),
            new("Multiplayer/Steam/LoadingHostSave", "Das Spiel deines Freundes wird geladen", false),
            new("Multiplayer/Steam/HostSaveFailed", "Das Spiel deines Freundes konnte nicht geladen werden", false),
            new("Multiplayer/Steam/Joined", "Du spielst jetzt mit deinem Freund", false),
            new("Multiplayer/Steam/JoinFailed", "Dein Freund ist nicht erreichbar", false),
            new("Multiplayer/Steam/HostLeft", "Dein Freund hostet nicht mehr", false),
            new("Multiplayer/Reconnect/Connecting", "Verbinde {0}/{1}", false),
            new("Multiplayer/Reconnect/Failed", "Verbindung zu deinem Freund verloren: in 3 Versuchen keine neue Verbindung möglich", false),
            new("Multiplayer/UI/PatchesFailedTitle", "Co-op Anything", false),
            new("Multiplayer/UI/PatchesFailed", "{0} Einhängepunkte der Mod passen nicht zu dieser Spielversion: Das Spiel wurde nach der Mod aktualisiert. Koop funktioniert eventuell nicht richtig, bis eine korrigierte Version erscheint; die Mod bietet sie hier an, sobald es sie gibt", false),
            new("Multiplayer/UI/PatchesUpdate", "{0} Einhängepunkte der Mod passen nicht zu dieser Spielversion: Das Spiel wurde nach der Mod aktualisiert. Version {1} ist erschienen (du hast {2}). Jetzt aktualisieren?", false),
            new("Multiplayer/UI/PatchesOurs", "{0} Einhängepunkte der Mod passen nicht zu deiner Spielversion: Koop funktioniert eventuell nicht richtig", false),
            new("Multiplayer/UI/PatchesPartner", "{0}: {1} Einhängepunkte der Mod passen nicht zu seiner Spielversion, Koop funktioniert eventuell nicht richtig", false),
            new("Multiplayer/UI/CopyLog", "Log kopieren", false),
            new("Multiplayer/UI/LogCopied", "Log kopiert", false),
            new("Multiplayer/Online/Discord", "Fehler und Ideen", false),
            new("Multiplayer/Chat/Tip", "Drücke T, um mit deinem Freund zu chatten", false),
            new("Multiplayer/Chat/Hint", "Nachricht… Enter sendet, Esc schließt", false),
            new("Multiplayer/Steam/Left", "Du hast das Spiel deines Freundes verlassen", false),
            new("Multiplayer/Steam/Stopped", "Hosten beendet", false),
            new("Multiplayer/Steam/LeaveFirst", "Beende das Hosten, um deinem Freund beizutreten", false),
            new("Multiplayer/Cult/FollowerBusy", "Jemand spricht schon mit diesem Anhänger", false),
            new("Multiplayer/Scene/WaitPartner", "Warte darauf, dass dein Mitspieler hereinkommt", false),
            new("Multiplayer/Scene/WaitScene", "Warte, bis dein Mitspieler fertig zugeschaut hat", false),
            new("Multiplayer/Scene/WaitSync", "Warte auf die Bestätigung der Synchronisierung", false),
            new("Multiplayer/Version/Title", "Unterschiedliche Mod-Versionen", false),
            new("Multiplayer/Version/Lines", "Version des Hosts: {0}\nDeine Version: {1}", false),
            new("Multiplayer/Version/FriendLines", "Version deines Freundes: {0}\nDeine Version: {1}", false),
            new("Multiplayer/Version/Unknown", "unbekannt (0.2.8 oder älter)", false),
            new("Multiplayer/Version/Risk", "Du kannst spielen, aber unterschiedliche Versionen können Fehler verursachen oder sogar den Spielstand beschädigen.", false),
            new("Multiplayer/Version/UpdateToHost", "Auf {0} aktualisieren? Das Update kommt direkt im Hauptmenü, danach trittst du dem Host von selbst wieder bei.", false),
            new("Multiplayer/Version/HostOlder", "Der Host hat eine ältere Version. Du kannst den Host bitten, zu aktualisieren.", false),
            new("Multiplayer/Version/HostUpdate", "Jetzt aktualisieren? Es lädt herunter, während du spielst; dann wählst du, wie es hineinkommt: zurück in die Lobby mit diesem Spielstand oder Neustart des Spiels.", false),
            new("Multiplayer/Version/FriendAsks", "Dein Freund bittet dich, die Mod zu aktualisieren", false),
            new("Multiplayer/Version/FriendHas", "Die Mod deines Freundes ist {0}, deine ist {1}", false),
            new("Multiplayer/Version/Update", "Aktualisieren", false),
            new("Multiplayer/Version/PlayAnyway", "Trotzdem spielen", false),
            new("Multiplayer/Version/Suggest", "Host um Update bitten", false),
            new("Multiplayer/Version/NotNow", "Nicht jetzt", false),
            new("Multiplayer/Online/Title", "Online-Spiel", false),
            new("Multiplayer/Online/CreateLobby", "Lobby erstellen", false),
            new("Multiplayer/Online/FriendsLobbies", "Lobbys von Freunden", false),
            new("Multiplayer/Online/NoLobbies", "Kein Freund hat eine Lobby offen", false),
            new("Multiplayer/Online/Joining", "Trete {0} bei...", false),
            new("Multiplayer/Online/PickSave", "Spielstand für die Lobby", false),
            new("Multiplayer/Online/Back", "Zurück", false),
            new("Multiplayer/Lobby/Title", "Lobby", false),
            new("Multiplayer/Lobby/NewGame", "Neues Spiel", false),
            new("Multiplayer/Lobby/HostSave", "Spiel von {0}: {1}", false),
            new("Multiplayer/Lobby/Play", "Spielen", false),
            new("Multiplayer/Lobby/Invite", "Freund einladen", false),
            new("Multiplayer/Lobby/Leave", "Lobby verlassen", false),
            new("Multiplayer/Lobby/Close", "Lobby schließen", false),
            new("Multiplayer/Lobby/WaitingFriend", "Warte auf einen Freund...", false),
            new("Multiplayer/Lobby/WaitingHost", "Warte, bis der Host startet", false),
            new("Multiplayer/Lobby/Failed", "Die Lobby konnte nicht geöffnet werden", false),
            new("Multiplayer/Intro/WaitCrown", "Warte, bis dein Freund die Krone erhält...", false),
            new("Multiplayer/UI/OnlineOpen", "Online-Spiel ist offen", false),
            new("Multiplayer/UI/OnlineFailed", "Online-Spiel konnte nicht geöffnet werden", false),
            new("Multiplayer/Steam/GameVersion", "Das Spiel deines Freundes hat Version {0}, deines {1}. Aktualisiert das Spiel auf beiden Seiten in Steam.", false),
            new("Multiplayer/Fox/HostDeals", "Der Host macht die Geschäfte mit dem Fuchs", false),
            new("Multiplayer/Update/Title", "Eine neue Version der Koop-Mod", false),
            new("Multiplayer/Update/Body", "Version {0} ist erschienen, du hast {1}. Jetzt aktualisieren? Das Update kommt direkt hier, ohne das Spiel zu verlassen. Nur eine Version, die ein neues BepInEx oder COTL API braucht, startet das Spiel für den Installer neu.", false),
            new("Multiplayer/Update/Downloading", "Update wird heruntergeladen…", false),
            new("Multiplayer/Update/Screen", "Koop-Mod wird auf {0} aktualisiert", false),
            new("Multiplayer/Update/Progress", "Heruntergeladen: {0} von {1} KB", false),
            new("Multiplayer/Update/Checking", "Download wird geprüft", false),
            new("Multiplayer/Update/Starting", "Neue Version wird gestartet", false),
            new("Multiplayer/Update/Restart", "Diese Version braucht den Installer: Das Spiel startet neu", false),
            new("Multiplayer/Update/Ready", "Das Update ist bereit: Es startet im Hauptmenü", false),
            new("Multiplayer/Update/Done", "Die Koop-Mod ist aktualisiert", false),
            new("Multiplayer/Update/ReadyTitle", "Das Update ist bereit", false),
            new("Multiplayer/Update/ReadyHost", "Version {0} ist heruntergeladen. Dafür wird das Spiel gespeichert, du gehst ins Hauptmenü, und die Lobby öffnet sich mit diesem Spielstand erneut; dein Freund tritt ihr wie gewohnt bei. Gespielt wird erst, wenn du auf Spielen drückst.", false),
            new("Multiplayer/Update/ReadyFriend", "Version {0} ist heruntergeladen. Dafür gehst du ins Hauptmenü und trittst dem Host erneut bei.", false),
            new("Multiplayer/Update/RestartLobby", "Lobby neu starten", false),
            new("Multiplayer/Update/OtherWays", "Andere Möglichkeiten", false),
            new("Multiplayer/Update/RestartGameBody", "Oder das Spiel wird gespeichert, startet neu und öffnet dann dieselbe Lobby. Später: Das Update kommt, wenn du das nächste Mal im Hauptmenü bist oder das Spiel startest.", false),
            new("Multiplayer/Update/RestartGame", "Spiel neu starten", false),
            new("Multiplayer/Update/Later", "Später", false),
            new("Multiplayer/Update/Saving", "Spiel wird gespeichert...", false),
            new("Multiplayer/Rejoin/Going", "Zurück zum Host...", false),
            new("Multiplayer/Scene/GameSpeaker", "Spiel", false),
            new("Multiplayer/Scene/HoldTalk", "Warte auf deinen Mitspieler: Das Gespräch beginnt, sobald er diesen Raum betritt. Bis dahin kannst du dich im Raum bewegen, seine Türen öffnen sich danach.", false),
            new("Multiplayer/Scene/PartnerWaits", "Dein Mitspieler wartet auf dich: Ein Gespräch beginnt, sobald du zu ihm kommst", false),
            new("Multiplayer/Update/Failed", "Konnte nicht von selbst aktualisieren", false),
            new("Multiplayer/Update/OpenPage", "Die Seite von Version {0} zum Herunterladen öffnen?", false)
        ];
    }
}

/* EOF */
