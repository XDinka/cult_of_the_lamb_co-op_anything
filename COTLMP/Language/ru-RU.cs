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

            /* Servers browser UI resource strings */

            /* Server password prompt UI resource strings */

            /* Connect by IP dialog box resource strings */

            /* Game mod resource strings */

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
            new("Multiplayer/Reconnect/Connecting", "Подключение {0}/{1}", false),
            new("Multiplayer/Reconnect/Failed", "Связь с другом потеряна: не удалось переподключиться за 3 попытки", false),
            new("Multiplayer/Reconnect/CancelHint", "Esc: отменить подключение", false),
            new("Multiplayer/Reconnect/Cancel", "Отменить переподключение", false),
            new("Multiplayer/Reconnect/Cancelled", "Переподключение отменено", false),
            new("Multiplayer/UI/PatchesFailedTitle", "Co-op Anything", false),
            new("Multiplayer/UI/PatchesFailed", "{0} точек подключения мода не подходят к этой версии игры: игра обновилась после мода. Кооператив может работать неправильно, пока не выйдет исправленная версия; мод предложит её здесь, как только она появится", false),
            new("Multiplayer/UI/PatchesUpdate", "{0} точек подключения мода не подходят к этой версии игры: игра обновилась после мода. Вышла версия {1} (у вас {2}). Обновить сейчас?", false),
            new("Multiplayer/UI/PatchesOurs", "{0} точек подключения мода не подходят к вашей версии игры: кооператив может работать неправильно", false),
            new("Multiplayer/UI/PatchesPartner", "{0}: {1} точек подключения мода не подходят к его версии игры, кооператив может работать неправильно", false),
            new("Multiplayer/UI/CopyLog", "Скопировать лог", false),
            new("Multiplayer/UI/LogCopied", "Лог скопирован", false),
            new("Multiplayer/Online/Discord", "Ошибки и идеи", false),
            new("Multiplayer/Chat/Tip", "Нажмите T, чтобы написать другу", false),
            new("Multiplayer/Chat/Hint", "Сообщение… Enter — отправить, Esc — закрыть", false),
            new("Multiplayer/Steam/Left", "Вы вышли из игры друга", false),
            new("Multiplayer/Steam/Stopped", "Онлайн игра закрыта", false),
            new("Multiplayer/Steam/LeaveFirst", "Закройте свою онлайн игру, чтобы зайти к другу", false),
            new("Multiplayer/Cult/FollowerBusy", "Кто-то уже разговаривает с этим последователем", false),
            new("Multiplayer/Scene/WaitPartner", "Ждём, когда войдёт напарник", false),
            new("Multiplayer/Scene/WaitScene", "Ждём, когда напарник досмотрит", false),
            new("Multiplayer/Scene/WaitSync", "Ждём подтверждения синхронизации", false),

            /* Mod versions of the two players */
            new("Multiplayer/Version/Title", "Разные версии мода", false),
            new("Multiplayer/Version/Lines", "Версия хоста: {0}\nВаша версия: {1}", false),
            new("Multiplayer/Version/FriendLines", "Версия друга: {0}\nВаша версия: {1}", false),
            new("Multiplayer/Version/Unknown", "неизвестна (0.2.8 или старее)", false),
            new("Multiplayer/Version/Risk", "Играть можно, но разные версии могут вызвать ошибки или даже повредить сохранение.", false),
            new("Multiplayer/Version/UpdateToHost", "Обновиться до {0}? Обновление встанет прямо в главном меню, потом вы сами снова подключитесь к хосту.", false),
            new("Multiplayer/Version/HostOlder", "У хоста версия старее. Можно предложить хосту обновиться.", false),
            new("Multiplayer/Version/HostUpdate", "Обновиться сейчас? Обновление скачается, пока вы играете; потом выберете, как его поставить: вернуться в лобби на этом сохранении или перезапустить игру.", false),
            new("Multiplayer/Version/FriendAsks", "Друг предлагает обновить мод", false),
            new("Multiplayer/Version/FriendHas", "У друга мод {0}, у вас {1}", false),
            new("Multiplayer/Version/Update", "Обновиться", false),
            new("Multiplayer/Version/PlayAnyway", "Играть так", false),
            new("Multiplayer/Version/Suggest", "Предложить хосту обновиться", false),
            new("Multiplayer/Version/NotNow", "Не сейчас", false),

            /* Online menu and lobby */
            new("Multiplayer/Online/Title", "Онлайн игра", false),
            new("Multiplayer/Online/CreateLobby", "Создать лобби", false),
            new("Multiplayer/Online/FriendsLobbies", "Лобби друзей", false),
            new("Multiplayer/Online/NoLobbies", "Открытых лобби друзей нет", false),
            new("Multiplayer/Online/Joining", "Подключаемся к {0}...", false),
            new("Multiplayer/Online/PickSave", "Сохранение для лобби", false),
            new("Multiplayer/Online/Back", "Вернуться", false),
            new("Multiplayer/Lobby/Title", "Лобби", false),
            new("Multiplayer/Lobby/NewGame", "Новая игра", false),
            new("Multiplayer/Lobby/HostSave", "Игра {0}: {1}", false),
            new("Multiplayer/Lobby/Play", "Играть", false),
            new("Multiplayer/Lobby/Invite", "Пригласить друга", false),
            new("Multiplayer/Lobby/Leave", "Покинуть лобби", false),
            new("Multiplayer/Lobby/Close", "Закрыть лобби", false),
            new("Multiplayer/Lobby/WaitingFriend", "Ждём друга...", false),
            new("Multiplayer/Lobby/WaitingHost", "Ждём, когда хост начнёт", false),
            new("Multiplayer/Lobby/Failed", "Не удалось открыть лобби", false),
            new("Multiplayer/Online/JoinFailed", "Не удалось подключиться к лобби друга", false),
            new("Multiplayer/Intro/WaitCrown", "Ждём, пока друг получит корону...", false),

            /* Opening online play */
            new("Multiplayer/UI/OnlineOpen", "Онлайн открыт", false),
            new("Multiplayer/UI/OnlineFailed", "Не удалось открыть онлайн", false),
            new("Multiplayer/Steam/GameVersion", "У друга игра версии {0}, у вас {1}. Обновите игру в Steam у обоих.", false),

            /* The night fox */
            new("Multiplayer/Fox/HostDeals", "Сделки с лисом заключает хост", false),

            /* Updates */
            new("Multiplayer/Update/Title", "Вышла новая версия кооп-мода", false),
            new("Multiplayer/Update/Body", "Вышла версия {0}, у вас {1}. Обновить сейчас? Обновление встанет прямо в игре, выходить не нужно. Только если новой версии нужен новый BepInEx или COTL API, игра перезапустится через установщик.", false),
            new("Multiplayer/Update/Downloading", "Скачиваю обновление…", false),
            new("Multiplayer/Update/Screen", "Обновляю кооп-мод до версии {0}", false),
            new("Multiplayer/Update/Progress", "Скачано {0} из {1} КБ", false),
            new("Multiplayer/Update/Checking", "Проверяю загрузку", false),
            new("Multiplayer/Update/Starting", "Запускаю новую версию", false),
            new("Multiplayer/Update/Restart", "Этой версии нужен установщик: игра перезапустится", false),
            new("Multiplayer/Update/Ready", "Обновление готово: оно запустится в главном меню", false),
            new("Multiplayer/Update/Done", "Кооп-мод обновлён", false),
            new("Multiplayer/Update/ReadyTitle", "Обновление готово", false),
            new("Multiplayer/Update/ReadyHost", "Версия {0} скачана. Чтобы её поставить, игра сохранится, вы выйдете в главное меню, и лобби на этом сохранении откроется снова; друг заходит в него как обычно. Игра не начнётся, пока вы не нажмёте «Играть».", false),
            new("Multiplayer/Update/ReadyFriend", "Версия {0} скачана. Чтобы её поставить, вы выйдете в главное меню и снова зайдёте к хосту.", false),
            new("Multiplayer/Update/RestartLobby", "Перезапустить лобби", false),
            new("Multiplayer/Update/OtherWays", "Другие варианты", false),
            new("Multiplayer/Update/RestartGameBody", "Или игра сохранится и перезапустится, а потом откроет то же лобби. «Позже» — обновление встанет, когда вы в следующий раз окажетесь в главном меню или запустите игру.", false),
            new("Multiplayer/Update/RestartGame", "Перезапустить игру", false),
            new("Multiplayer/Update/Later", "Позже", false),
            new("Multiplayer/Update/Saving", "Сохраняем игру...", false),

            /* Coming back to the host */
            new("Multiplayer/Rejoin/Going", "Возвращаемся к хосту...", false),

            /* Waiting for the partner in a room */
            new("Multiplayer/Scene/GameSpeaker", "Игра", false),
            new("Multiplayer/Scene/HoldTalk", "Ждём напарника: разговор начнётся, когда он войдёт в эту комнату. Пока можно ходить по комнате, двери откроются после разговора.", false),
            new("Multiplayer/Scene/PartnerWaits", "Напарник ждёт вас: разговор начнётся, когда вы придёте к нему", false),
            new("Multiplayer/Scene/WaitHostLead", "Игра хоста отстаёт: разговор продолжится, как только она догонит", false),
            new("Multiplayer/Update/Failed", "Не получилось обновить автоматически", false),
            new("Multiplayer/Update/OpenPage", "Открыть страницу версии {0}, чтобы скачать её вручную?", false)

            /* The map between floors */

            /* Back into the run after a crash */
        ];
    }
}

/* EOF */
