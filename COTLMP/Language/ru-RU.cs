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
            new("Multiplayer/Steam/LoadingHostSave", "Загружаем игру друга", false),
            new("Multiplayer/Steam/HostSaveFailed", "Не удалось загрузить игру друга", false),
            new("Multiplayer/Steam/Joined", "Вы в игре друга", false),
            new("Multiplayer/Steam/JoinFailed", "Не удалось связаться с другом", false),
            new("Multiplayer/Steam/HostLeft", "Друг закрыл онлайн игру", false),
            new("Multiplayer/Steam/Left", "Вы вышли из игры друга", false),
            new("Multiplayer/Steam/Stopped", "Онлайн игра закрыта", false),
            new("Multiplayer/Steam/LeaveFirst", "Закройте свою онлайн игру, чтобы зайти к другу", false),
            new("Multiplayer/Cult/FollowerBusy", "Кто-то уже разговаривает с этим последователем", false),

            /* Mod versions of the two players */
            new("Multiplayer/Version/Title", "Разные версии мода", false),
            new("Multiplayer/Version/Lines", "Версия хоста: {0}\nВаша версия: {1}", false),
            new("Multiplayer/Version/FriendLines", "Версия друга: {0}\nВаша версия: {1}", false),
            new("Multiplayer/Version/Unknown", "неизвестна (0.2.8 или старее)", false),
            new("Multiplayer/Version/Risk", "Играть можно, но разные версии могут вызвать ошибки или даже повредить сохранение.", false),
            new("Multiplayer/Version/UpdateToHost", "Обновиться до {0}? Игра перезапустится и сама снова подключится к хосту.", false),
            new("Multiplayer/Version/HostOlder", "У хоста версия старее. Можно предложить хосту обновиться.", false),
            new("Multiplayer/Version/HostUpdate", "Обновиться сейчас? Игра перезапустится; потом снова откройте онлайн игру и выберите сохранение.", false),
            new("Multiplayer/Version/FriendAsks", "Друг предлагает обновить мод", false),
            new("Multiplayer/Version/FriendHas", "У друга мод {0}, у вас {1}", false),
            new("Multiplayer/Version/Update", "Обновиться", false),
            new("Multiplayer/Version/PlayAnyway", "Играть так", false),
            new("Multiplayer/Version/Suggest", "Предложить хосту обновиться", false),
            new("Multiplayer/Version/NotNow", "Не сейчас", false),

            /* Updates */
            new("Multiplayer/Update/Title", "Вышла новая версия кооп-мода", false),
            new("Multiplayer/Update/Body", "Вышла версия {0}, у вас {1}. Обновить сейчас? Игра закроется, установщик поставит новую версию и снова запустит игру.", false),
            new("Multiplayer/Update/Downloading", "Скачиваю обновление…", false),
            new("Multiplayer/Update/Failed", "Не получилось обновить автоматически", false),
            new("Multiplayer/Update/OpenPage", "Открыть страницу версии {0}, чтобы скачать её вручную?", false),

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
