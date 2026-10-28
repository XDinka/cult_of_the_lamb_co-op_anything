/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Dutch localization file
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP;
using COTLMP.Api;
using COTLMP.Data;

/* TRANSLATION ****************************************************************/

namespace COTLMP.Language
{
    public class Dutch
    {
        public static LocalizationTable[] StringsTable =
        [
            new("UI/DLC", "Multiplayer", true),
            new("UI/Banner", $"Multiplayer-editie v{COTLMP.Data.Version.CotlMpVer}", false),
            new("Multiplayer/UI/TitleDialog", "Multiplayer", false),
            new("Multiplayer/UI/Settings", "Multiplayer-instellingen", false),
            new("Multiplayer/UI/Settings/DisableMod", "Mod aan/uit", false),
            new("Multiplayer/UI/Settings/PlayerName", "Spelersnaam", false),
            new("Multiplayer/UI/StartServer", "Online openen", false),
            new("Multiplayer/UI/SteamInviteHint", "Steam-vrienden kunnen meedoen via uitnodigingen (Shift+Tab) of met 'Deelnemen aan spel'", false),
            new("Multiplayer/UI/ServerStarted", "Stoppen met hosten", false),
            new("Multiplayer/UI/ServerConfirm", "Stoppen met hosten? Je vrienden worden losgekoppeld en jij speelt je spel verder.", false),
            new("Multiplayer/UI/LeaveServer", "Server verlaten", false),
            new("Multiplayer/UI/LeaveConfirm", "Wil je de server verlaten?", false),
            new("Multiplayer/UI/Disconnected", "Verbinding verbroken", false),
            new("Multiplayer/UI/DisconnectedError", "Er is een fout opgetreden (zie de console)", false),
            new("Multiplayer/UI/Players", "Spelers", false),
            new("Multiplayer/UI/Host", "host", false),
            new("Multiplayer/UI/You", "jij", false),
            new("Multiplayer/UI/Ms", "ms", false),
            new("Multiplayer/Steam/AfterPrologue", "Online spelen kan na de proloog, in je sekte", false),
            new("Multiplayer/Steam/LoadingHostSave", "Het spel van je vriend wordt geladen", false),
            new("Multiplayer/Steam/HostSaveFailed", "Het spel van je vriend kon niet worden geladen", false),
            new("Multiplayer/Steam/Joined", "Je speelt nu met je vriend", false),
            new("Multiplayer/Steam/JoinFailed", "Je vriend is niet bereikbaar", false),
            new("Multiplayer/Steam/HostLeft", "Je vriend is gestopt met hosten", false),
            new("Multiplayer/Reconnect/Connecting", "Verbinden {0}/{1}", false),
            new("Multiplayer/Reconnect/Failed", "Verbinding met je vriend verbroken: in 3 pogingen niet opnieuw verbonden", false),
            new("Multiplayer/Reconnect/CancelHint", "Esc: stoppen met proberen", false),
            new("Multiplayer/Reconnect/Cancel", "Stoppen met opnieuw verbinden", false),
            new("Multiplayer/Reconnect/Cancelled", "Opnieuw verbinden is afgebroken", false),
            new("Multiplayer/UI/PatchesFailedTitle", "Co-op Anything", false),
            new("Multiplayer/UI/PatchesFailed", "{0} haken van de mod passen niet bij deze spelversie: het spel is na de mod bijgewerkt. Co-op werkt misschien niet goed tot er een verbeterde versie is; de mod biedt die hier aan zodra die er is", false),
            new("Multiplayer/UI/PatchesUpdate", "{0} haken van de mod passen niet bij deze spelversie: het spel is na de mod bijgewerkt. Versie {1} is uit (jij hebt {2}). Nu bijwerken?", false),
            new("Multiplayer/UI/PatchesOurs", "{0} haken van de mod passen niet bij jouw spelversie: co-op werkt misschien niet goed", false),
            new("Multiplayer/UI/PatchesPartner", "{0}: {1} haken van de mod passen niet bij diens spelversie, co-op werkt misschien niet goed", false),
            new("Multiplayer/UI/CopyLog", "Log kopiëren", false),
            new("Multiplayer/UI/LogCopied", "Log gekopieerd", false),
            new("Multiplayer/Online/Discord", "Bugs en ideeën", false),
            new("Multiplayer/Chat/Tip", "Druk op T om met je vriend te chatten", false),
            new("Multiplayer/Chat/Hint", "Bericht… Enter verstuurt, Esc sluit", false),
            new("Multiplayer/Steam/Left", "Je hebt het spel van je vriend verlaten", false),
            new("Multiplayer/Steam/Stopped", "Gestopt met hosten", false),
            new("Multiplayer/Steam/LeaveFirst", "Stop met hosten om met je vriend mee te doen", false),
            new("Multiplayer/Cult/FollowerBusy", "Iemand praat al met deze volgeling", false),
            new("Multiplayer/Scene/WaitPartner", "Wachten tot je partner binnenkomt", false),
            new("Multiplayer/Scene/WaitScene", "Wachten tot je partner klaar is met kijken", false),
            new("Multiplayer/Scene/WaitSync", "Wachten op bevestiging van de synchronisatie", false),
            new("Multiplayer/Version/Title", "Verschillende modversies", false),
            new("Multiplayer/Version/Lines", "Versie van de host: {0}\nJouw versie: {1}", false),
            new("Multiplayer/Version/FriendLines", "Versie van je vriend: {0}\nJouw versie: {1}", false),
            new("Multiplayer/Version/Unknown", "onbekend (0.2.8 of ouder)", false),
            new("Multiplayer/Version/Risk", "Je kunt spelen, maar verschillende versies kunnen fouten geven of zelfs de opslag beschadigen.", false),
            new("Multiplayer/Version/UpdateToHost", "Bijwerken naar {0}? De update komt er hier in het hoofdmenu in, daarna doe je vanzelf weer mee met de host.", false),
            new("Multiplayer/Version/HostOlder", "De host heeft een oudere versie. Je kunt de host vragen bij te werken.", false),
            new("Multiplayer/Version/HostUpdate", "Nu bijwerken? Het downloadt terwijl je speelt; daarna kies je hoe het erin komt: terug naar de lobby met deze opslag of het spel opnieuw starten.", false),
            new("Multiplayer/Version/FriendAsks", "Je vriend vraagt je de mod bij te werken", false),
            new("Multiplayer/Version/FriendHas", "De mod van je vriend is {0}, die van jou {1}", false),
            new("Multiplayer/Version/Update", "Bijwerken", false),
            new("Multiplayer/Version/PlayAnyway", "Toch spelen", false),
            new("Multiplayer/Version/Suggest", "Host vragen bij te werken", false),
            new("Multiplayer/Version/NotNow", "Niet nu", false),
            new("Multiplayer/Online/Title", "Online spelen", false),
            new("Multiplayer/Online/CreateLobby", "Lobby maken", false),
            new("Multiplayer/Online/FriendsLobbies", "Lobby's van vrienden", false),
            new("Multiplayer/Online/NoLobbies", "Geen enkele vriend heeft een lobby open", false),
            new("Multiplayer/Online/Joining", "Deelnemen aan {0}...", false),
            new("Multiplayer/Online/PickSave", "Opslag voor de lobby", false),
            new("Multiplayer/Online/Back", "Terug", false),
            new("Multiplayer/Lobby/Title", "Lobby", false),
            new("Multiplayer/Lobby/NewGame", "Nieuw spel", false),
            new("Multiplayer/Lobby/HostSave", "Spel van {0}: {1}", false),
            new("Multiplayer/Lobby/Play", "Spelen", false),
            new("Multiplayer/Lobby/Invite", "Vriend uitnodigen", false),
            new("Multiplayer/Lobby/Leave", "Lobby verlaten", false),
            new("Multiplayer/Lobby/Close", "Lobby sluiten", false),
            new("Multiplayer/Lobby/WaitingFriend", "Wachten op een vriend...", false),
            new("Multiplayer/Lobby/WaitingHost", "Wachten tot de host begint", false),
            new("Multiplayer/Lobby/Failed", "De lobby kon niet worden geopend", false),
            new("Multiplayer/Online/JoinFailed", "Kan de lobby van je vriend niet bereiken", false),
            new("Multiplayer/Intro/WaitCrown", "Wachten tot je vriend de kroon krijgt...", false),
            new("Multiplayer/UI/OnlineOpen", "Online spelen is open", false),
            new("Multiplayer/UI/OnlineFailed", "Online spelen kon niet worden geopend", false),
            new("Multiplayer/Steam/GameVersion", "Het spel van je vriend heeft versie {0}, het jouwe {1}. Werk het spel aan beide kanten bij in Steam.", false),
            new("Multiplayer/Fox/HostDeals", "De host sluit de deals met de vos", false),
            new("Multiplayer/Update/Title", "Een nieuwe versie van de co-opmod", false),
            new("Multiplayer/Update/Body", "Versie {0} is uit, jij hebt {1}. Nu bijwerken? Het komt er hier in, zonder het spel te verlaten. Alleen een versie die een nieuwe BepInEx of COTL API nodig heeft, start het spel opnieuw voor de installer.", false),
            new("Multiplayer/Update/Downloading", "Update wordt gedownload…", false),
            new("Multiplayer/Update/Screen", "Co-opmod bijwerken naar {0}", false),
            new("Multiplayer/Update/Progress", "Gedownload: {0} van {1} KB", false),
            new("Multiplayer/Update/Checking", "Download controleren", false),
            new("Multiplayer/Update/Starting", "Nieuwe versie starten", false),
            new("Multiplayer/Update/Restart", "Deze versie heeft de installer nodig: het spel start opnieuw", false),
            new("Multiplayer/Update/Ready", "De update staat klaar: hij start in het hoofdmenu", false),
            new("Multiplayer/Update/Done", "De co-opmod is bijgewerkt", false),
            new("Multiplayer/Update/ReadyTitle", "De update staat klaar", false),
            new("Multiplayer/Update/ReadyHost", "Versie {0} is gedownload. Daarvoor wordt het spel opgeslagen, ga je naar het hoofdmenu en gaat de lobby opnieuw open met deze opslag; je vriend doet mee zoals altijd. Er wordt niets gespeeld tot je op Spelen drukt.", false),
            new("Multiplayer/Update/ReadyFriend", "Versie {0} is gedownload. Daarvoor ga je naar het hoofdmenu en doe je opnieuw mee met de host.", false),
            new("Multiplayer/Update/RestartLobby", "Lobby herstarten", false),
            new("Multiplayer/Update/OtherWays", "Andere opties", false),
            new("Multiplayer/Update/RestartGameBody", "Of het spel wordt opgeslagen, start opnieuw en opent dan dezelfde lobby. Later: de update komt erin zodra je weer in het hoofdmenu bent of het spel start.", false),
            new("Multiplayer/Update/RestartGame", "Spel herstarten", false),
            new("Multiplayer/Update/Later", "Later", false),
            new("Multiplayer/Update/Saving", "Spel wordt opgeslagen...", false),
            new("Multiplayer/Rejoin/Going", "Terug naar de host...", false),
            new("Multiplayer/Scene/GameSpeaker", "Spel", false),
            new("Multiplayer/Scene/HoldTalk", "Wachten op je partner: het gesprek begint zodra hij deze kamer binnenkomt. Intussen kun je door de kamer lopen; de deuren gaan daarna open.", false),
            new("Multiplayer/Scene/PartnerWaits", "Je partner wacht op je: er begint een gesprek zodra je bij hem bent", false),
            new("Multiplayer/Scene/WaitHostLead", "Het spel van de host loopt achter: het gesprek gaat verder zodra het bijloopt", false),
            new("Multiplayer/Update/Failed", "Kon niet vanzelf bijwerken", false),
            new("Multiplayer/Update/OpenPage", "De pagina van versie {0} openen om die te downloaden?", false)
        ];
    }
}

/* EOF */
