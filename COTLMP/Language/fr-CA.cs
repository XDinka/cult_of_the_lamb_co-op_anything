/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     French (Canadian) localization file
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP;
using COTLMP.Api;
using COTLMP.Data;

/* TRANSLATION ****************************************************************/

namespace COTLMP.Language
{
    public class FrenchCanadian
    {
        public static LocalizationTable[] StringsTable =
        [
            new("UI/DLC", "Multijoueur", true),
            new("UI/Banner", $"Édition multijoueur v{COTLMP.Data.Version.CotlMpVer}", false),
            new("Multiplayer/UI/TitleDialog", "Multijoueur", false),
            new("Multiplayer/UI/Settings", "Paramètres multijoueur", false),
            new("Multiplayer/UI/Settings/DisableMod", "Activer le mod", false),
            new("Multiplayer/UI/Settings/PlayerName", "Nom du joueur", false),
            new("Multiplayer/UI/StartServer", "Ouvrir en ligne", false),
            new("Multiplayer/UI/SteamInviteHint", "Vos amis Steam peuvent vous rejoindre par invitation (Maj+Tab) ou avec « Rejoindre la partie »", false),
            new("Multiplayer/UI/ServerStarted", "Arrêter l'hébergement", false),
            new("Multiplayer/UI/ServerConfirm", "Arrêter l'hébergement ? Vos amis seront déconnectés et vous continuerez votre partie.", false),
            new("Multiplayer/UI/LeaveServer", "Quitter le serveur", false),
            new("Multiplayer/UI/LeaveConfirm", "Voulez-vous quitter le serveur ?", false),
            new("Multiplayer/UI/Disconnected", "Déconnecté", false),
            new("Multiplayer/UI/DisconnectedError", "Une erreur est survenue (voir la console)", false),
            new("Multiplayer/UI/Players", "Joueurs", false),
            new("Multiplayer/UI/Host", "hôte", false),
            new("Multiplayer/UI/You", "vous", false),
            new("Multiplayer/UI/Ms", "ms", false),
            new("Multiplayer/Steam/AfterPrologue", "Le jeu en ligne s'ouvre après le prologue, dans votre culte", false),
            new("Multiplayer/Steam/LoadingHostSave", "Chargement de la partie de votre ami", false),
            new("Multiplayer/Steam/HostSaveFailed", "Impossible de charger la partie de votre ami", false),
            new("Multiplayer/Steam/Joined", "Vous avez rejoint votre ami", false),
            new("Multiplayer/Steam/JoinFailed", "Impossible de joindre votre ami", false),
            new("Multiplayer/Steam/HostLeft", "Votre ami a arrêté l'hébergement", false),
            new("Multiplayer/Reconnect/Connecting", "Connexion {0}/{1}", false),
            new("Multiplayer/Reconnect/Failed", "Connexion avec votre ami perdue : impossible de se reconnecter en 3 tentatives", false),
            new("Multiplayer/UI/PatchesFailedTitle", "Co-op Anything", false),
            new("Multiplayer/UI/PatchesFailed", "{0} points d'accroche du mod ne correspondent pas à cette version du jeu : le jeu a été mis à jour après le mod. La coop peut mal fonctionner jusqu'à la sortie d'une version corrigée ; le mod la proposera ici dès qu'elle sera disponible", false),
            new("Multiplayer/UI/PatchesUpdate", "{0} points d'accroche du mod ne correspondent pas à cette version du jeu : le jeu a été mis à jour après le mod. La version {1} est sortie (vous avez la {2}). Mettre à jour maintenant ?", false),
            new("Multiplayer/UI/PatchesOurs", "{0} points d'accroche du mod ne correspondent pas à votre version du jeu : la coop peut mal fonctionner", false),
            new("Multiplayer/UI/PatchesPartner", "{0} : {1} points d'accroche du mod ne correspondent pas à sa version du jeu, la coop peut mal fonctionner", false),
            new("Multiplayer/UI/CopyLog", "Copier le journal", false),
            new("Multiplayer/UI/LogCopied", "Journal copié", false),
            new("Multiplayer/Online/Discord", "Bugs et idées", false),
            new("Multiplayer/Chat/Tip", "Appuyez sur T pour discuter avec votre ami", false),
            new("Multiplayer/Chat/Hint", "Message… Entrée pour envoyer, Échap pour fermer", false),
            new("Multiplayer/Steam/Left", "Vous avez quitté la partie de votre ami", false),
            new("Multiplayer/Steam/Stopped", "Hébergement arrêté", false),
            new("Multiplayer/Steam/LeaveFirst", "Arrêtez l'hébergement pour rejoindre votre ami", false),
            new("Multiplayer/Cult/FollowerBusy", "Quelqu'un parle déjà à ce fidèle", false),
            new("Multiplayer/Scene/WaitPartner", "En attente de l'arrivée de votre partenaire", false),
            new("Multiplayer/Scene/WaitScene", "En attente de la fin de la scène chez votre partenaire", false),
            new("Multiplayer/Scene/WaitSync", "En attente de la confirmation de synchronisation", false),
            new("Multiplayer/Version/Title", "Versions du mod différentes", false),
            new("Multiplayer/Version/Lines", "Version de l'hôte : {0}\nVotre version : {1}", false),
            new("Multiplayer/Version/FriendLines", "Version de votre ami : {0}\nVotre version : {1}", false),
            new("Multiplayer/Version/Unknown", "inconnue (0.2.8 ou plus ancienne)", false),
            new("Multiplayer/Version/Risk", "Vous pouvez jouer, mais des versions différentes peuvent provoquer des erreurs, voire endommager la sauvegarde.", false),
            new("Multiplayer/Version/UpdateToHost", "Mettre à jour vers {0} ? La mise à jour s'installe directement dans le menu principal, puis vous rejoignez l'hôte automatiquement.", false),
            new("Multiplayer/Version/HostOlder", "L'hôte a une version plus ancienne. Vous pouvez lui demander de mettre à jour.", false),
            new("Multiplayer/Version/HostUpdate", "Mettre à jour maintenant ? Le téléchargement se fait pendant que vous jouez ; ensuite vous choisissez comment l'installer : retour au salon sur cette sauvegarde, ou redémarrage du jeu.", false),
            new("Multiplayer/Version/FriendAsks", "Votre ami vous demande de mettre le mod à jour", false),
            new("Multiplayer/Version/FriendHas", "Le mod de votre ami est en {0}, le vôtre en {1}", false),
            new("Multiplayer/Version/Update", "Mettre à jour", false),
            new("Multiplayer/Version/PlayAnyway", "Jouer quand même", false),
            new("Multiplayer/Version/Suggest", "Demander à l'hôte de mettre à jour", false),
            new("Multiplayer/Version/NotNow", "Pas maintenant", false),
            new("Multiplayer/Online/Title", "Jeu en ligne", false),
            new("Multiplayer/Online/CreateLobby", "Créer un salon", false),
            new("Multiplayer/Online/FriendsLobbies", "Salons des amis", false),
            new("Multiplayer/Online/NoLobbies", "Aucun ami n'a de salon ouvert", false),
            new("Multiplayer/Online/Joining", "Connexion à {0}...", false),
            new("Multiplayer/Online/PickSave", "Sauvegarde du salon", false),
            new("Multiplayer/Online/Back", "Retour", false),
            new("Multiplayer/Lobby/Title", "Salon", false),
            new("Multiplayer/Lobby/NewGame", "Nouvelle partie", false),
            new("Multiplayer/Lobby/HostSave", "Partie de {0} : {1}", false),
            new("Multiplayer/Lobby/Play", "Jouer", false),
            new("Multiplayer/Lobby/Invite", "Inviter un ami", false),
            new("Multiplayer/Lobby/Leave", "Quitter le salon", false),
            new("Multiplayer/Lobby/Close", "Fermer le salon", false),
            new("Multiplayer/Lobby/WaitingFriend", "En attente d'un ami...", false),
            new("Multiplayer/Lobby/WaitingHost", "En attente du lancement par l'hôte", false),
            new("Multiplayer/Lobby/Failed", "Impossible d'ouvrir le salon", false),
            new("Multiplayer/Intro/WaitCrown", "En attente que votre ami reçoive la couronne...", false),
            new("Multiplayer/UI/OnlineOpen", "Le jeu en ligne est ouvert", false),
            new("Multiplayer/UI/OnlineFailed", "Impossible d'ouvrir le jeu en ligne", false),
            new("Multiplayer/Steam/GameVersion", "Le jeu de votre ami est en version {0}, le vôtre en {1}. Mettez le jeu à jour dans Steam des deux côtés.", false),
            new("Multiplayer/Fox/HostDeals", "C'est l'hôte qui négocie avec le renard", false),
            new("Multiplayer/Update/Title", "Une nouvelle version du mod coop", false),
            new("Multiplayer/Update/Body", "La version {0} est sortie, vous avez la {1}. Mettre à jour maintenant ? Elle s'installe ici même, sans quitter le jeu. Seule une version qui demande un nouveau BepInEx ou COTL API redémarre le jeu pour l'installateur.", false),
            new("Multiplayer/Update/Downloading", "Téléchargement de la mise à jour…", false),
            new("Multiplayer/Update/Screen", "Mise à jour du mod coop vers {0}", false),
            new("Multiplayer/Update/Progress", "Téléchargé : {0} sur {1} Ko", false),
            new("Multiplayer/Update/Checking", "Vérification du téléchargement", false),
            new("Multiplayer/Update/Starting", "Lancement de la nouvelle version", false),
            new("Multiplayer/Update/Restart", "Cette version demande l'installateur : le jeu va redémarrer", false),
            new("Multiplayer/Update/Ready", "La mise à jour est prête : elle s'installe au menu principal", false),
            new("Multiplayer/Update/Done", "Le mod coop est à jour", false),
            new("Multiplayer/Update/ReadyTitle", "La mise à jour est prête", false),
            new("Multiplayer/Update/ReadyHost", "La version {0} est téléchargée. Pour l'installer, le jeu est sauvegardé, vous passez au menu principal et le salon se rouvre sur cette sauvegarde ; votre ami le rejoint comme d'habitude. Rien ne commence avant que vous appuyiez sur Jouer.", false),
            new("Multiplayer/Update/ReadyFriend", "La version {0} est téléchargée. Pour l'installer, vous passez au menu principal et rejoignez l'hôte à nouveau.", false),
            new("Multiplayer/Update/RestartLobby", "Relancer le salon", false),
            new("Multiplayer/Update/OtherWays", "Autres options", false),
            new("Multiplayer/Update/RestartGameBody", "Ou le jeu est sauvegardé, redémarre et rouvre le même salon. Plus tard : la mise à jour s'installera la prochaine fois que vous serez au menu principal ou lancerez le jeu.", false),
            new("Multiplayer/Update/RestartGame", "Redémarrer le jeu", false),
            new("Multiplayer/Update/Later", "Plus tard", false),
            new("Multiplayer/Update/Saving", "Sauvegarde de la partie...", false),
            new("Multiplayer/Rejoin/Going", "Retour auprès de l'hôte...", false),
            new("Multiplayer/Scene/GameSpeaker", "Jeu", false),
            new("Multiplayer/Scene/HoldTalk", "En attente de votre partenaire : la conversation commence dès qu'il entre dans cette salle. En attendant, vous pouvez vous déplacer dans la salle ; ses portes s'ouvriront après.", false),
            new("Multiplayer/Scene/PartnerWaits", "Votre partenaire vous attend : une conversation commence dès que vous le rejoignez", false),
            new("Multiplayer/Update/Failed", "Impossible de mettre à jour automatiquement", false),
            new("Multiplayer/Update/OpenPage", "Ouvrir la page de la version {0} pour la télécharger ?", false)
        ];
    }
}

/* EOF */
