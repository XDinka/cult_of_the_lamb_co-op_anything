/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Portuguese (Brazil) localization file
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP;
using COTLMP.Api;
using COTLMP.Data;

/* TRANSLATION ****************************************************************/

namespace COTLMP.Language
{
    public class PortugueseBrazil
    {
        public static LocalizationTable[] StringsTable =
        [
            new("UI/DLC", "Multijogador", true),
            new("UI/Banner", $"Edição multijogador v{COTLMP.Data.Version.CotlMpVer}", false),
            new("Multiplayer/UI/TitleDialog", "Multijogador", false),
            new("Multiplayer/UI/Settings", "Configurações de multijogador", false),
            new("Multiplayer/UI/Settings/DisableMod", "Ativar o mod", false),
            new("Multiplayer/UI/Settings/PlayerName", "Nome do jogador", false),
            new("Multiplayer/UI/StartServer", "Abrir online", false),
            new("Multiplayer/UI/SteamInviteHint", "Amigos da Steam podem entrar por convite (Shift+Tab) ou em \"Entrar no jogo\"", false),
            new("Multiplayer/UI/ServerStarted", "Parar de hospedar", false),
            new("Multiplayer/UI/ServerConfirm", "Parar de hospedar? Seus amigos serão desconectados e você continua jogando sua partida.", false),
            new("Multiplayer/UI/LeaveServer", "Sair do servidor", false),
            new("Multiplayer/UI/LeaveConfirm", "Deseja sair do servidor?", false),
            new("Multiplayer/UI/Disconnected", "Desconectado", false),
            new("Multiplayer/UI/DisconnectedError", "Ocorreu um erro (veja o console)", false),
            new("Multiplayer/UI/Players", "Jogadores", false),
            new("Multiplayer/UI/Host", "anfitrião", false),
            new("Multiplayer/UI/You", "você", false),
            new("Multiplayer/UI/Ms", "ms", false),
            new("Multiplayer/Steam/AfterPrologue", "O jogo online abre depois do prólogo, no seu culto", false),
            new("Multiplayer/Steam/LoadingHostSave", "Carregando a partida do seu amigo", false),
            new("Multiplayer/Steam/HostSaveFailed", "Não foi possível carregar a partida do seu amigo", false),
            new("Multiplayer/Steam/Joined", "Você entrou na partida do seu amigo", false),
            new("Multiplayer/Steam/JoinFailed", "Não foi possível alcançar seu amigo", false),
            new("Multiplayer/Steam/HostLeft", "Seu amigo parou de hospedar", false),
            new("Multiplayer/Reconnect/Connecting", "Conectando {0}/{1}", false),
            new("Multiplayer/Reconnect/Failed", "A conexão com seu amigo caiu: não foi possível reconectar em 3 tentativas", false),
            new("Multiplayer/UI/PatchesFailedTitle", "Co-op Anything", false),
            new("Multiplayer/UI/PatchesFailed", "{0} ganchos do mod não combinam com esta versão do jogo: o jogo foi atualizado depois do mod. O cooperativo pode não funcionar direito até sair uma versão corrigida; o mod vai oferecê-la aqui assim que existir", false),
            new("Multiplayer/UI/PatchesUpdate", "{0} ganchos do mod não combinam com esta versão do jogo: o jogo foi atualizado depois do mod. Saiu a versão {1} (você tem a {2}). Atualizar agora?", false),
            new("Multiplayer/UI/PatchesOurs", "{0} ganchos do mod não combinam com a sua versão do jogo: o cooperativo pode não funcionar direito", false),
            new("Multiplayer/UI/PatchesPartner", "{0}: {1} ganchos do mod não combinam com a versão do jogo dele, o cooperativo pode não funcionar direito", false),
            new("Multiplayer/UI/CopyLog", "Copiar o log", false),
            new("Multiplayer/UI/LogCopied", "Log copiado", false),
            new("Multiplayer/Online/Discord", "Bugs e ideias", false),
            new("Multiplayer/Chat/Tip", "Aperte T para conversar com seu amigo", false),
            new("Multiplayer/Chat/Hint", "Mensagem… Enter envia, Esc fecha", false),
            new("Multiplayer/Steam/Left", "Você saiu da partida do seu amigo", false),
            new("Multiplayer/Steam/Stopped", "Você parou de hospedar", false),
            new("Multiplayer/Steam/LeaveFirst", "Pare de hospedar para entrar na partida do seu amigo", false),
            new("Multiplayer/Cult/FollowerBusy", "Alguém já está falando com este seguidor", false),
            new("Multiplayer/Scene/WaitPartner", "Esperando seu parceiro entrar", false),
            new("Multiplayer/Scene/WaitScene", "Esperando seu parceiro terminar de assistir", false),
            new("Multiplayer/Scene/WaitSync", "Esperando a confirmação da sincronização", false),
            new("Multiplayer/Version/Title", "Versões diferentes do mod", false),
            new("Multiplayer/Version/Lines", "Versão do anfitrião: {0}\nSua versão: {1}", false),
            new("Multiplayer/Version/FriendLines", "Versão do seu amigo: {0}\nSua versão: {1}", false),
            new("Multiplayer/Version/Unknown", "desconhecida (0.2.8 ou anterior)", false),
            new("Multiplayer/Version/Risk", "Você pode jogar, mas versões diferentes podem causar erros ou até danificar o save.", false),
            new("Multiplayer/Version/UpdateToHost", "Atualizar para {0}? A atualização é instalada aqui mesmo, no menu principal, e depois você volta para o anfitrião automaticamente.", false),
            new("Multiplayer/Version/HostOlder", "O anfitrião tem uma versão mais antiga. Você pode pedir que ele atualize.", false),
            new("Multiplayer/Version/HostUpdate", "Atualizar agora? O download acontece enquanto você joga; depois você escolhe como instalar: voltar ao lobby com este save ou reiniciar o jogo.", false),
            new("Multiplayer/Version/FriendAsks", "Seu amigo pede que você atualize o mod", false),
            new("Multiplayer/Version/FriendHas", "O mod do seu amigo é {0}, o seu é {1}", false),
            new("Multiplayer/Version/Update", "Atualizar", false),
            new("Multiplayer/Version/PlayAnyway", "Jogar mesmo assim", false),
            new("Multiplayer/Version/Suggest", "Pedir ao anfitrião para atualizar", false),
            new("Multiplayer/Version/NotNow", "Agora não", false),
            new("Multiplayer/Online/Title", "Jogo online", false),
            new("Multiplayer/Online/CreateLobby", "Criar lobby", false),
            new("Multiplayer/Online/FriendsLobbies", "Lobbies dos amigos", false),
            new("Multiplayer/Online/NoLobbies", "Nenhum amigo tem um lobby aberto", false),
            new("Multiplayer/Online/Joining", "Entrando em {0}...", false),
            new("Multiplayer/Online/PickSave", "Save para o lobby", false),
            new("Multiplayer/Online/Back", "Voltar", false),
            new("Multiplayer/Lobby/Title", "Lobby", false),
            new("Multiplayer/Lobby/NewGame", "Novo jogo", false),
            new("Multiplayer/Lobby/HostSave", "Jogo de {0}: {1}", false),
            new("Multiplayer/Lobby/Play", "Jogar", false),
            new("Multiplayer/Lobby/Invite", "Convidar um amigo", false),
            new("Multiplayer/Lobby/Leave", "Sair do lobby", false),
            new("Multiplayer/Lobby/Close", "Fechar o lobby", false),
            new("Multiplayer/Lobby/WaitingFriend", "Esperando um amigo...", false),
            new("Multiplayer/Lobby/WaitingHost", "Esperando o anfitrião começar", false),
            new("Multiplayer/Lobby/Failed", "Não foi possível abrir o lobby", false),
            new("Multiplayer/Intro/WaitCrown", "Esperando seu amigo receber a coroa...", false),
            new("Multiplayer/UI/OnlineOpen", "O jogo online está aberto", false),
            new("Multiplayer/UI/OnlineFailed", "Não foi possível abrir o jogo online", false),
            new("Multiplayer/Steam/GameVersion", "O jogo do seu amigo está na versão {0}, o seu na {1}. Atualizem o jogo na Steam dos dois lados.", false),
            new("Multiplayer/Fox/HostDeals", "Quem negocia com a raposa é o anfitrião", false),
            new("Multiplayer/Update/Title", "Uma nova versão do mod cooperativo", false),
            new("Multiplayer/Update/Body", "A versão {0} saiu, você tem a {1}. Atualizar agora? Ela é instalada aqui mesmo, sem sair do jogo. Só uma versão que precise de um novo BepInEx ou COTL API reinicia o jogo para o instalador.", false),
            new("Multiplayer/Update/Downloading", "Baixando a atualização…", false),
            new("Multiplayer/Update/Screen", "Atualizando o mod cooperativo para {0}", false),
            new("Multiplayer/Update/Progress", "Baixado: {0} de {1} KB", false),
            new("Multiplayer/Update/Checking", "Verificando o download", false),
            new("Multiplayer/Update/Starting", "Iniciando a nova versão", false),
            new("Multiplayer/Update/Restart", "Esta versão precisa do instalador: o jogo será reiniciado", false),
            new("Multiplayer/Update/Ready", "A atualização está pronta: ela começa no menu principal", false),
            new("Multiplayer/Update/Done", "O mod cooperativo foi atualizado", false),
            new("Multiplayer/Update/ReadyTitle", "A atualização está pronta", false),
            new("Multiplayer/Update/ReadyHost", "A versão {0} foi baixada. Para instalá-la, o jogo é salvo, você vai ao menu principal e o lobby abre de novo com este save; seu amigo entra como sempre. Nada começa até você apertar Jogar.", false),
            new("Multiplayer/Update/ReadyFriend", "A versão {0} foi baixada. Para instalá-la, você vai ao menu principal e entra de novo na partida do anfitrião.", false),
            new("Multiplayer/Update/RestartLobby", "Reiniciar o lobby", false),
            new("Multiplayer/Update/OtherWays", "Outras opções", false),
            new("Multiplayer/Update/RestartGameBody", "Ou o jogo é salvo, reinicia e abre o mesmo lobby. Mais tarde: a atualização é instalada na próxima vez que você estiver no menu principal ou iniciar o jogo.", false),
            new("Multiplayer/Update/RestartGame", "Reiniciar o jogo", false),
            new("Multiplayer/Update/Later", "Mais tarde", false),
            new("Multiplayer/Update/Saving", "Salvando o jogo...", false),
            new("Multiplayer/Rejoin/Going", "Voltando para o anfitrião...", false),
            new("Multiplayer/Scene/GameSpeaker", "Jogo", false),
            new("Multiplayer/Scene/HoldTalk", "Esperando seu parceiro: a conversa começa assim que ele entrar nesta sala. Enquanto isso, você pode andar pela sala; as portas abrem depois.", false),
            new("Multiplayer/Scene/PartnerWaits", "Seu parceiro está esperando você: uma conversa começa assim que você chegar até ele", false),
            new("Multiplayer/Update/Failed", "Não foi possível atualizar sozinho", false),
            new("Multiplayer/Update/OpenPage", "Abrir a página da versão {0} para baixá-la?", false)
        ];
    }
}

/* EOF */
