/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Russian localization file
 * COPYRIGHT:	Copyright 2025-2026 GeoB99 <geobman1999@gmail.com>
 */

/* IMPORTS ********************************************************************/

using COTLMP;
using COTLMP.Api;
using COTLMP.Data;

/* TRANSLATION ****************************************************************/

namespace COTLMP.Language
{
    public class Russian
    {
        public static LocalizationTable[] StringsTable =
        [
            /* Main Multiplayer header resource strings */
            new("UI/DLC", "Мультиплеер", true),
            new("UI/Banner", $"Мультиплеер v{COTLMP.Data.Version.CotlMpVer}", false),
            new("Multiplayer/UI/TitleDialog", "Мультиплеер", false),

            /* Multiplayer settings resource strings */
            new("Multiplayer/UI/Settings", "Настройки мультиплеера", false),
            new("Multiplayer/UI/Settings/DisableMod", "Мод включён", false),
            new("Multiplayer/UI/Settings/PlayerName", "Имя игрока", false),
            new("Multiplayer/UI/Settings/ServerName", "Название сервера", false),
            new("Multiplayer/UI/Settings/GameMode", "Режим игры", false),
            new("Multiplayer/UI/Settings/PlayerCount", "Наибольшее число игроков", false),
            new("Multiplayer/UI/Settings/VoiceChat", "Голосовой чат", false),
            new("Multiplayer/UI/Settings/ProtectServer", "Защитить сервер паролем", false),

            /* Servers browser UI resource strings */
            new("Multiplayer/UI/ServerList/BackButton", "Назад", false),
            new("Multiplayer/UI/ServerList/PlayerNameDescription", "Введите имя игрока", false),
            new("Multiplayer/UI/ServerList/ServerNameDescription", "Введите название сервера", false),
            new("Multiplayer/UI/ServerList/MainDescription", "Выберите сервер в списке и нажмите «Подключиться»", false),
            new("Multiplayer/UI/ServerList/NoneFound", "Серверы не найдены", false),
            new("Multiplayer/UI/ServerList/MasterFail", "Не удалось связаться с главным сервером", false),
            new("Multiplayer/UI/ServerList/ScanProgress", "Ищем доступные серверы...", false),
            new("Multiplayer/UI/ServerList/ConnectButton", "Подключиться", false),

            /* Server password prompt UI resource strings */
            new("Multiplayer/UI/PasswordPrompt/OkButton", "ОК", false),
            new("Multiplayer/UI/PasswordPrompt/CancelButton", "Отмена", false),
            new("Multiplayer/UI/PasswordPrompt/PromptDescription", "Сервер защищён паролем. Введите пароль, чтобы подключиться.", false),
            new("Multiplayer/UI/PasswordPrompt/InvalidPassword", "Неверный пароль!", false),

            /* Connect by IP dialog box resource strings */
            new("Multiplayer/UI/JoinByIP/Connect", "Подключиться", false),
            new("Multiplayer/UI/JoinByIP/Cancel", "Отмена", false),
            new("Multiplayer/UI/JoinByIP/Description", "Введите IP-адрес сервера, к которому хотите подключиться, как в примере ниже.\n\nПример: 111.55.222.333:36963", false),

            /* Game mod resource strings */
            new("Multiplayer/Game/Join", "{0} подключился", false),
            new("Multiplayer/Game/Left", "{0} вышел", false),

            /* General UI resource strings */
            new("Multiplayer/UI/StartServer", "Онлайн игра", false),
            new("Multiplayer/UI/SteamInviteHint", "Друзья из Steam заходят по приглашению (Shift+Tab) или через «Присоединиться к игре»", false),
            new("Multiplayer/UI/ServerStarted", "Закрыть онлайн игру", false),
            new("Multiplayer/UI/ServerConfirm", "Закрыть онлайн игру? Друзья отключатся, а вы продолжите свою игру.", false),
            new("Multiplayer/UI/LeaveServer", "Покинуть игру друга", false),
            new("Multiplayer/UI/LeaveConfirm", "Покинуть игру друга?", false),
            new("Multiplayer/UI/Disconnected", "Отключено", false),
            new("Multiplayer/UI/DisconnectedError", "Произошла ошибка (подробности в консоли)", false),
            new("Multiplayer/UI/Players", "Игроки", false),
            new("Multiplayer/UI/Host", "хост", false),
            new("Multiplayer/UI/You", "вы", false),
            new("Multiplayer/UI/Ms", "мс", false),

            /* Steam invites */
            new("Multiplayer/Steam/AfterPrologue", "Онлайн игра откроется после пролога, в вашем культе", false),
            new("Multiplayer/Steam/JoinAfterLoad", "Загрузите сохранение, чтобы присоединиться к другу", false),
            new("Multiplayer/Steam/Joined", "Вы в игре друга", false),
            new("Multiplayer/Steam/JoinFailed", "Не удалось связаться с другом", false),
            new("Multiplayer/Steam/HostLeft", "Друг закрыл онлайн игру", false),
            new("Multiplayer/Steam/Left", "Вы вышли из игры друга", false),
            new("Multiplayer/Steam/Stopped", "Онлайн игра закрыта", false),
            new("Multiplayer/Steam/LeaveFirst", "Закройте свою онлайн игру, чтобы зайти к другу", false),

            /* The map between floors */
            new("Multiplayer/Map/WaitingForHost", "Ждём, пока хост выберет путь", false),

            /* Back into the run after a crash */
            new("Multiplayer/Recovery/Returning", "Возвращаемся в забег друга", false),
            new("Multiplayer/Recovery/Restored", "Вы снова в забеге", false),
            new("Multiplayer/Recovery/RunOver", "Забег друга закончился", false),
            new("Multiplayer/Recovery/Failed", "Не удалось вернуться в забег друга", false)
        ];
    }
}

/* EOF */
