/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Spanish localization file
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP;
using COTLMP.Api;
using COTLMP.Data;

/* TRANSLATION ****************************************************************/

namespace COTLMP.Language
{
    public class Spanish
    {
        public static LocalizationTable[] StringsTable =
        [
            new("UI/DLC", "Multijugador", true),
            new("UI/Banner", $"Edición multijugador v{COTLMP.Data.Version.CotlMpVer}", false),
            new("Multiplayer/UI/TitleDialog", "Multijugador", false),
            new("Multiplayer/UI/Settings", "Ajustes multijugador", false),
            new("Multiplayer/UI/Settings/DisableMod", "Activar el mod", false),
            new("Multiplayer/UI/Settings/PlayerName", "Nombre del jugador", false),
            new("Multiplayer/UI/StartServer", "Abrir en línea", false),
            new("Multiplayer/UI/SteamInviteHint", "Tus amigos de Steam pueden unirse con invitaciones (Mayús+Tab) o con «Unirse a la partida»", false),
            new("Multiplayer/UI/ServerStarted", "Dejar de alojar", false),
            new("Multiplayer/UI/ServerConfirm", "¿Dejar de alojar? Tus amigos se desconectarán y tú seguirás jugando tu partida.", false),
            new("Multiplayer/UI/LeaveServer", "Salir del servidor", false),
            new("Multiplayer/UI/LeaveConfirm", "¿Quieres salir del servidor?", false),
            new("Multiplayer/UI/Disconnected", "Desconectado", false),
            new("Multiplayer/UI/DisconnectedError", "Se ha producido un error (revisa la consola)", false),
            new("Multiplayer/UI/Players", "Jugadores", false),
            new("Multiplayer/UI/Host", "anfitrión", false),
            new("Multiplayer/UI/You", "tú", false),
            new("Multiplayer/UI/Ms", "ms", false),
            new("Multiplayer/Steam/AfterPrologue", "El juego en línea se abre tras el prólogo, en tu culto", false),
            new("Multiplayer/Steam/LoadingHostSave", "Cargando la partida de tu amigo", false),
            new("Multiplayer/Steam/HostSaveFailed", "No se pudo cargar la partida de tu amigo", false),
            new("Multiplayer/Steam/Joined", "Te has unido a tu amigo", false),
            new("Multiplayer/Steam/JoinFailed", "No se pudo contactar con tu amigo", false),
            new("Multiplayer/Steam/HostLeft", "Tu amigo ha dejado de alojar", false),
            new("Multiplayer/Reconnect/Connecting", "Conectando {0}/{1}", false),
            new("Multiplayer/Reconnect/Failed", "Se perdió la conexión con tu amigo: no se pudo reconectar en 3 intentos", false),
            new("Multiplayer/UI/PatchesFailedTitle", "Co-op Anything", false),
            new("Multiplayer/UI/PatchesFailed", "{0} enganches del mod no encajan con esta versión del juego: el juego se actualizó después del mod. El cooperativo puede fallar hasta que salga una versión corregida; el mod la ofrecerá aquí en cuanto exista", false),
            new("Multiplayer/UI/PatchesUpdate", "{0} enganches del mod no encajan con esta versión del juego: el juego se actualizó después del mod. Ha salido la versión {1} (tienes la {2}). ¿Actualizar ahora?", false),
            new("Multiplayer/UI/PatchesOurs", "{0} enganches del mod no encajan con tu versión del juego: el cooperativo puede fallar", false),
            new("Multiplayer/UI/PatchesPartner", "{0}: {1} enganches del mod no encajan con su versión del juego, el cooperativo puede fallar", false),
            new("Multiplayer/UI/CopyLog", "Copiar el registro", false),
            new("Multiplayer/UI/LogCopied", "Registro copiado", false),
            new("Multiplayer/Online/Discord", "Errores e ideas", false),
            new("Multiplayer/Chat/Tip", "Pulsa T para chatear con tu amigo", false),
            new("Multiplayer/Chat/Hint", "Mensaje… Intro para enviar, Esc para cerrar", false),
            new("Multiplayer/Steam/Left", "Has salido de la partida de tu amigo", false),
            new("Multiplayer/Steam/Stopped", "Has dejado de alojar", false),
            new("Multiplayer/Steam/LeaveFirst", "Deja de alojar para unirte a tu amigo", false),
            new("Multiplayer/Cult/FollowerBusy", "Alguien ya está hablando con este seguidor", false),
            new("Multiplayer/Scene/WaitPartner", "Esperando a que entre tu compañero", false),
            new("Multiplayer/Scene/WaitScene", "Esperando a que tu compañero termine de ver la escena", false),
            new("Multiplayer/Scene/WaitSync", "Esperando la confirmación de la sincronización", false),
            new("Multiplayer/Version/Title", "Versiones del mod distintas", false),
            new("Multiplayer/Version/Lines", "Versión del anfitrión: {0}\nTu versión: {1}", false),
            new("Multiplayer/Version/FriendLines", "Versión de tu amigo: {0}\nTu versión: {1}", false),
            new("Multiplayer/Version/Unknown", "desconocida (0.2.8 o anterior)", false),
            new("Multiplayer/Version/Risk", "Puedes jugar, pero versiones distintas pueden causar errores o incluso dañar la partida guardada.", false),
            new("Multiplayer/Version/UpdateToHost", "¿Actualizar a {0}? Se instala aquí mismo, en el menú principal, y después vuelves a unirte al anfitrión automáticamente.", false),
            new("Multiplayer/Version/HostOlder", "El anfitrión tiene una versión más antigua. Puedes pedirle que actualice.", false),
            new("Multiplayer/Version/HostUpdate", "¿Actualizar ahora? Se descarga mientras juegas; después eliges cómo instalarla: volver a la sala con esta partida o reiniciar el juego.", false),
            new("Multiplayer/Version/FriendAsks", "Tu amigo te pide que actualices el mod", false),
            new("Multiplayer/Version/FriendHas", "El mod de tu amigo es {0}, el tuyo es {1}", false),
            new("Multiplayer/Version/Update", "Actualizar", false),
            new("Multiplayer/Version/PlayAnyway", "Jugar igualmente", false),
            new("Multiplayer/Version/Suggest", "Pedir al anfitrión que actualice", false),
            new("Multiplayer/Version/NotNow", "Ahora no", false),
            new("Multiplayer/Online/Title", "Juego en línea", false),
            new("Multiplayer/Online/CreateLobby", "Crear sala", false),
            new("Multiplayer/Online/FriendsLobbies", "Salas de amigos", false),
            new("Multiplayer/Online/NoLobbies", "Ningún amigo tiene una sala abierta", false),
            new("Multiplayer/Online/Joining", "Uniéndote a {0}...", false),
            new("Multiplayer/Online/PickSave", "Partida para la sala", false),
            new("Multiplayer/Online/Back", "Volver", false),
            new("Multiplayer/Lobby/Title", "Sala", false),
            new("Multiplayer/Lobby/NewGame", "Nueva partida", false),
            new("Multiplayer/Lobby/HostSave", "Partida de {0}: {1}", false),
            new("Multiplayer/Lobby/Play", "Jugar", false),
            new("Multiplayer/Lobby/Invite", "Invitar a un amigo", false),
            new("Multiplayer/Lobby/Leave", "Salir de la sala", false),
            new("Multiplayer/Lobby/Close", "Cerrar la sala", false),
            new("Multiplayer/Lobby/WaitingFriend", "Esperando a un amigo...", false),
            new("Multiplayer/Lobby/WaitingHost", "Esperando a que el anfitrión empiece", false),
            new("Multiplayer/Lobby/Failed", "No se pudo abrir la sala", false),
            new("Multiplayer/Intro/WaitCrown", "Esperando a que tu amigo reciba la corona...", false),
            new("Multiplayer/UI/OnlineOpen", "El juego en línea está abierto", false),
            new("Multiplayer/UI/OnlineFailed", "No se pudo abrir el juego en línea", false),
            new("Multiplayer/Steam/GameVersion", "El juego de tu amigo es la versión {0}, el tuyo la {1}. Actualizad el juego en Steam los dos.", false),
            new("Multiplayer/Fox/HostDeals", "El anfitrión es quien trata con el zorro", false),
            new("Multiplayer/Update/Title", "Una nueva versión del mod cooperativo", false),
            new("Multiplayer/Update/Body", "Ha salido la versión {0}, tienes la {1}. ¿Actualizar ahora? Se instala aquí mismo, sin salir del juego. Solo una versión que necesite un BepInEx o COTL API nuevos reinicia el juego para el instalador.", false),
            new("Multiplayer/Update/Downloading", "Descargando la actualización…", false),
            new("Multiplayer/Update/Screen", "Actualizando el mod cooperativo a {0}", false),
            new("Multiplayer/Update/Progress", "Descargado: {0} de {1} KB", false),
            new("Multiplayer/Update/Checking", "Comprobando la descarga", false),
            new("Multiplayer/Update/Starting", "Iniciando la nueva versión", false),
            new("Multiplayer/Update/Restart", "Esta versión necesita el instalador: el juego se reiniciará", false),
            new("Multiplayer/Update/Ready", "La actualización está lista: se instalará en el menú principal", false),
            new("Multiplayer/Update/Done", "El mod cooperativo está actualizado", false),
            new("Multiplayer/Update/ReadyTitle", "La actualización está lista", false),
            new("Multiplayer/Update/ReadyHost", "La versión {0} se ha descargado. Para instalarla, se guarda la partida, vas al menú principal y la sala vuelve a abrirse con esta partida; tu amigo se une como siempre. No se juega nada hasta que pulses Jugar.", false),
            new("Multiplayer/Update/ReadyFriend", "La versión {0} se ha descargado. Para instalarla, vas al menú principal y vuelves a unirte al anfitrión.", false),
            new("Multiplayer/Update/RestartLobby", "Reiniciar la sala", false),
            new("Multiplayer/Update/OtherWays", "Otras opciones", false),
            new("Multiplayer/Update/RestartGameBody", "O la partida se guarda, el juego se reinicia y abre la misma sala. Más tarde: la actualización se instalará la próxima vez que estés en el menú principal o inicies el juego.", false),
            new("Multiplayer/Update/RestartGame", "Reiniciar el juego", false),
            new("Multiplayer/Update/Later", "Más tarde", false),
            new("Multiplayer/Update/Saving", "Guardando la partida...", false),
            new("Multiplayer/Rejoin/Going", "Volviendo con el anfitrión...", false),
            new("Multiplayer/Scene/GameSpeaker", "Juego", false),
            new("Multiplayer/Scene/HoldTalk", "Esperando a tu compañero: la conversación empieza en cuanto entre en esta sala. Mientras tanto puedes moverte por la sala; sus puertas se abrirán después.", false),
            new("Multiplayer/Scene/PartnerWaits", "Tu compañero te espera: una conversación empieza en cuanto llegues a su lado", false),
            new("Multiplayer/Update/Failed", "No se pudo actualizar automáticamente", false),
            new("Multiplayer/Update/OpenPage", "¿Abrir la página de la versión {0} para descargarla?", false)
        ];
    }
}

/* EOF */
