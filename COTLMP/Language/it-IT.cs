/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Italian localization file
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP;
using COTLMP.Api;
using COTLMP.Data;

/* TRANSLATION ****************************************************************/

namespace COTLMP.Language
{
    public class Italian
    {
        public static LocalizationTable[] StringsTable =
        [
            new("UI/DLC", "Multigiocatore", true),
            new("UI/Banner", $"Edizione multigiocatore v{COTLMP.Data.Version.CotlMpVer}", false),
            new("Multiplayer/UI/TitleDialog", "Multigiocatore", false),
            new("Multiplayer/UI/Settings", "Impostazioni multigiocatore", false),
            new("Multiplayer/UI/Settings/DisableMod", "Attiva la mod", false),
            new("Multiplayer/UI/Settings/PlayerName", "Nome del giocatore", false),
            new("Multiplayer/UI/StartServer", "Apri online", false),
            new("Multiplayer/UI/SteamInviteHint", "Gli amici di Steam possono unirsi con un invito (Maiusc+Tab) o con \"Unisciti alla partita\"", false),
            new("Multiplayer/UI/ServerStarted", "Smetti di ospitare", false),
            new("Multiplayer/UI/ServerConfirm", "Smettere di ospitare? I tuoi amici verranno disconnessi e tu continuerai la tua partita.", false),
            new("Multiplayer/UI/LeaveServer", "Lascia il server", false),
            new("Multiplayer/UI/LeaveConfirm", "Vuoi lasciare il server?", false),
            new("Multiplayer/UI/Disconnected", "Disconnesso", false),
            new("Multiplayer/UI/DisconnectedError", "Si è verificato un errore (controlla la console)", false),
            new("Multiplayer/UI/Players", "Giocatori", false),
            new("Multiplayer/UI/Host", "host", false),
            new("Multiplayer/UI/You", "tu", false),
            new("Multiplayer/UI/Ms", "ms", false),
            new("Multiplayer/Steam/AfterPrologue", "Il gioco online si apre dopo il prologo, nel tuo culto", false),
            new("Multiplayer/Steam/LoadingHostSave", "Caricamento della partita del tuo amico", false),
            new("Multiplayer/Steam/HostSaveFailed", "Impossibile caricare la partita del tuo amico", false),
            new("Multiplayer/Steam/Joined", "Sei entrato nella partita del tuo amico", false),
            new("Multiplayer/Steam/JoinFailed", "Impossibile raggiungere il tuo amico", false),
            new("Multiplayer/Steam/HostLeft", "Il tuo amico ha smesso di ospitare", false),
            new("Multiplayer/Reconnect/Connecting", "Connessione {0}/{1}", false),
            new("Multiplayer/Reconnect/Failed", "Connessione con il tuo amico persa: impossibile riconnettersi in 3 tentativi", false),
            new("Multiplayer/Reconnect/CancelHint", "Esc: smetti di provare", false),
            new("Multiplayer/Reconnect/Cancel", "Interrompi la riconnessione", false),
            new("Multiplayer/Reconnect/Cancelled", "La riconnessione è stata annullata", false),
            new("Multiplayer/UI/PatchesFailedTitle", "Co-op Anything", false),
            new("Multiplayer/UI/PatchesFailed", "{0} agganci della mod non sono adatti a questa versione del gioco: il gioco è stato aggiornato dopo la mod. La co-op potrebbe non funzionare bene finché non esce una versione corretta; la mod la proporrà qui appena disponibile", false),
            new("Multiplayer/UI/PatchesUpdate", "{0} agganci della mod non sono adatti a questa versione del gioco: il gioco è stato aggiornato dopo la mod. È uscita la versione {1} (hai la {2}). Aggiornare ora?", false),
            new("Multiplayer/UI/PatchesOurs", "{0} agganci della mod non sono adatti alla tua versione del gioco: la co-op potrebbe non funzionare bene", false),
            new("Multiplayer/UI/PatchesPartner", "{0}: {1} agganci della mod non sono adatti alla sua versione del gioco, la co-op potrebbe non funzionare bene", false),
            new("Multiplayer/UI/CopyLog", "Copia il registro", false),
            new("Multiplayer/UI/LogCopied", "Registro copiato", false),
            new("Multiplayer/Online/Discord", "Bug e idee", false),
            new("Multiplayer/Chat/Tip", "Premi T per chattare con il tuo amico", false),
            new("Multiplayer/Chat/Hint", "Messaggio… Invio per inviare, Esc per chiudere", false),
            new("Multiplayer/Steam/Left", "Hai lasciato la partita del tuo amico", false),
            new("Multiplayer/Steam/Stopped", "Hai smesso di ospitare", false),
            new("Multiplayer/Steam/LeaveFirst", "Smetti di ospitare per unirti al tuo amico", false),
            new("Multiplayer/Cult/FollowerBusy", "Qualcuno sta già parlando con questo seguace", false),
            new("Multiplayer/Scene/WaitPartner", "In attesa che entri il tuo compagno", false),
            new("Multiplayer/Scene/WaitScene", "In attesa che il tuo compagno finisca di guardare", false),
            new("Multiplayer/Scene/WaitSync", "In attesa della conferma della sincronizzazione", false),
            new("Multiplayer/Version/Title", "Versioni della mod diverse", false),
            new("Multiplayer/Version/Lines", "Versione dell'host: {0}\nLa tua versione: {1}", false),
            new("Multiplayer/Version/FriendLines", "Versione del tuo amico: {0}\nLa tua versione: {1}", false),
            new("Multiplayer/Version/Unknown", "sconosciuta (0.2.8 o precedente)", false),
            new("Multiplayer/Version/Risk", "Puoi giocare, ma versioni diverse possono causare errori o persino danneggiare il salvataggio.", false),
            new("Multiplayer/Version/UpdateToHost", "Aggiornare a {0}? L'aggiornamento si installa qui nel menu principale, poi rientri dall'host automaticamente.", false),
            new("Multiplayer/Version/HostOlder", "L'host ha una versione più vecchia. Puoi chiedergli di aggiornare.", false),
            new("Multiplayer/Version/HostUpdate", "Aggiornare ora? Si scarica mentre giochi; poi scegli come installarlo: tornare alla lobby con questo salvataggio o riavviare il gioco.", false),
            new("Multiplayer/Version/FriendAsks", "Il tuo amico ti chiede di aggiornare la mod", false),
            new("Multiplayer/Version/FriendHas", "La mod del tuo amico è {0}, la tua è {1}", false),
            new("Multiplayer/Version/FriendRefused", "La mod del tuo amico è {0}. Le versioni precedenti alla 0.9.4 tolgono dal tuo salvataggio ciò che danno i DLC, quindi il tuo amico non può unirsi. Chiedigli di aggiornare dal menu principale del gioco.", false),
            new("Multiplayer/Version/HostStrips", "La mod dell'host ({0}) toglie dal suo stesso salvataggio ciò che danno i DLC.", false),
            new("Multiplayer/Version/Update", "Aggiorna", false),
            new("Multiplayer/Version/PlayAnyway", "Gioca comunque", false),
            new("Multiplayer/Version/Suggest", "Chiedi all'host di aggiornare", false),
            new("Multiplayer/Version/NotNow", "Non ora", false),
            new("Multiplayer/Online/Title", "Gioco online", false),
            new("Multiplayer/Online/CreateLobby", "Crea lobby", false),
            new("Multiplayer/Online/FriendsLobbies", "Lobby degli amici", false),
            new("Multiplayer/Online/NoLobbies", "Nessun amico ha una lobby aperta", false),
            new("Multiplayer/Online/Joining", "Ingresso in {0}...", false),
            new("Multiplayer/Online/PickSave", "Salvataggio per la lobby", false),
            new("Multiplayer/Online/Back", "Indietro", false),
            new("Multiplayer/Lobby/Title", "Lobby", false),
            new("Multiplayer/Lobby/NewGame", "Nuova partita", false),
            new("Multiplayer/Lobby/HostSave", "Partita di {0}: {1}", false),
            new("Multiplayer/Lobby/Play", "Gioca", false),
            new("Multiplayer/Lobby/Invite", "Invita un amico", false),
            new("Multiplayer/Lobby/Leave", "Lascia la lobby", false),
            new("Multiplayer/Lobby/Close", "Chiudi la lobby", false),
            new("Multiplayer/Lobby/WaitingFriend", "In attesa di un amico...", false),
            new("Multiplayer/Lobby/WaitingHost", "In attesa che l'host inizi", false),
            new("Multiplayer/Lobby/Failed", "Impossibile aprire la lobby", false),
            new("Multiplayer/Online/JoinFailed", "Impossibile raggiungere la lobby dell'amico", false),
            new("Multiplayer/Intro/WaitCrown", "In attesa che il tuo amico riceva la corona...", false),
            new("Multiplayer/UI/OnlineOpen", "Il gioco online è aperto", false),
            new("Multiplayer/UI/OnlineFailed", "Impossibile aprire il gioco online", false),
            new("Multiplayer/Steam/GameVersion", "Il gioco del tuo amico è alla versione {0}, il tuo alla {1}. Aggiornate il gioco su Steam entrambi.", false),
            new("Multiplayer/Fox/HostDeals", "È l'host a trattare con la volpe", false),
            new("Multiplayer/Update/Title", "Una nuova versione della mod cooperativa", false),
            new("Multiplayer/Update/Body", "È uscita la versione {0}, tu hai la {1}. Aggiornare ora? Si installa qui, senza uscire dal gioco. Solo una versione che richiede un nuovo BepInEx o COTL API riavvia il gioco per l'installer.", false),
            new("Multiplayer/Update/Downloading", "Download dell'aggiornamento…", false),
            new("Multiplayer/Update/Screen", "Aggiornamento della mod cooperativa a {0}", false),
            new("Multiplayer/Update/Progress", "Scaricati: {0} di {1} KB", false),
            new("Multiplayer/Update/Checking", "Verifica del download", false),
            new("Multiplayer/Update/Starting", "Avvio della nuova versione", false),
            new("Multiplayer/Update/Restart", "Questa versione richiede l'installer: il gioco verrà riavviato", false),
            new("Multiplayer/Update/Ready", "L'aggiornamento è pronto: si avvierà nel menu principale", false),
            new("Multiplayer/Update/Done", "La mod cooperativa è aggiornata", false),
            new("Multiplayer/Update/ReadyTitle", "L'aggiornamento è pronto", false),
            new("Multiplayer/Update/ReadyHost", "La versione {0} è stata scaricata. Per installarla, la partita viene salvata, vai al menu principale e la lobby si riapre con questo salvataggio; il tuo amico entra come sempre. Non si gioca finché non premi Gioca.", false),
            new("Multiplayer/Update/ReadyFriend", "La versione {0} è stata scaricata. Per installarla, vai al menu principale e rientri dall'host.", false),
            new("Multiplayer/Update/RestartLobby", "Riavvia la lobby", false),
            new("Multiplayer/Update/OtherWays", "Altre opzioni", false),
            new("Multiplayer/Update/RestartGameBody", "Oppure la partita viene salvata, il gioco si riavvia e riapre la stessa lobby. Più tardi: l'aggiornamento si installerà la prossima volta che sarai nel menu principale o avvierai il gioco.", false),
            new("Multiplayer/Update/RestartGame", "Riavvia il gioco", false),
            new("Multiplayer/Update/Later", "Più tardi", false),
            new("Multiplayer/Update/Saving", "Salvataggio in corso...", false),
            new("Multiplayer/Rejoin/Going", "Ritorno dall'host...", false),
            new("Multiplayer/Scene/GameSpeaker", "Gioco", false),
            new("Multiplayer/Scene/HoldTalk", "In attesa del tuo compagno: la conversazione inizia appena entra in questa stanza. Nel frattempo puoi muoverti nella stanza; le porte si apriranno dopo.", false),
            new("Multiplayer/Scene/PartnerWaits", "Il tuo compagno ti aspetta: una conversazione inizia appena lo raggiungi", false),
            new("Multiplayer/Scene/WaitHostLead", "La partita dell'host è in ritardo: la conversazione continua appena si rimette in pari", false),
            new("Multiplayer/Update/Failed", "Impossibile aggiornare da solo", false),
            new("Multiplayer/Update/OpenPage", "Aprire la pagina della versione {0} per scaricarla?", false)
        ];
    }
}

/* EOF */
