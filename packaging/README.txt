Cult of the Lamb Co-op Anything 0.9.2: онлайн-кооператив через Steam (предрелиз)
==================================================================================
(English below)

github.com/XDinka/cult_of_the_lamb_co-op_anything
Основан на моде COTLMP (github.com/GeoB99/COTLMP, MIT), добавлено
подключение через Steam: без IP-адресов, без проброса портов, работает
с включённым VPN или WARP. Трафик идёт через сеть ретрансляторов Valve.

ОШИБКИ И ИДЕИ: Discord https://discord.gg/52azdsYMD (в игре: «Мультиплеер» →
«Ошибки и идеи»; в окне ошибки «Скопировать лог» копирует лог и открывает его).

ЧТО ЭТА СБОРКА УМЕЕТ
  + Пропала связь (30 с от хоста ничего): друг не вылетает, а за чёрным
    экраном загрузки («Подключение 1/3») сам заходит к хосту снова и
    возвращается в его комнату со своими сердцами и снаряжением. После трёх
    неудачных попыток: главное меню с ошибкой.
  + Чат: T открыть, Enter отправить, Esc закрыть. Пока пишете, игра не
    слушает клавиатуру и геймпад.
  + Мод обновляется прямо в игре, без перезапуска; битое обновление
    откатывается. Обновление посреди игры: перезапуск лобби, игры или позже.
  + Если обновление игры сломало часть мода, остальное работает, главное меню
    скажет об этом (и предложит обновиться, если есть версия новее), в сессии
    узнают оба.
  + Вернувшийся друг (вышел, потерял связь, вылетел) попадает в комнату хоста
    со своими вещами; у каждого друга свой персонаж в сохранении хоста.
  + Разговор ждёт напарника, если тот в другой комнате; двери закрыты.
  + Головы игроков на мини-карте подземелья, панели как в кооперативе игры.
  + Все 15 языков игры; арабский в окнах мода справа налево, с соединёнными
    буквами, даже внутри латинской строки.
  + Сцены и ролики кончаются у обоих вместе: кто досмотрел или пропустил
    первым, ждёт за чёрным экраном («Ждём, когда напарник досмотрит»);
    друг продолжает, когда у хоста прогрузился мир («Ждём подтверждения
    синхронизации», если это затянулось). Ролики можно пропускать.
  + У лидера земли первый вошедший ждёт напарника у двери, сцена и бой
    начинаются у обоих вместе.
  + Сердца обоих игроков на экране, как в кооперативе игры (Козёл фиолетовый).
  + На карте между этажами видны короны обоих и куда каждый смотрит.
  + Разговоры и сцены в одном месте идут в темпе хоста, у обоих вместе.
  + Новобранец вербуется один раз, без двойников.
  + Синяки и фингал напарника видны (при двух сердцах и меньше, при одном).
  + Проповедь и ритуал друга в храме проверены: итог у хоста.
  + Трава и цветы в первых забегах одинаковые у обоих.
  + Ночной лис: сделки заключает хост, друг видит лиса и разговор, итог
    засчитывается обоим.
  + Лобби в главном меню, в стиле игры: «Мультиплеер» → «Создать лобби» →
    сохранение или пустой слот. Ягнёнок и Козёл с именами и аватарками
    из Steam, друзья с открытым лобби видны списком, «Играть» запускает обоих.
  + Новая игра вдвоём: каждый проходит пролог сам до короны, кто получил её
    первым, ждёт; дальше оба оживают вместе (друг Козлом) и идут в культ.
  + Приглашение друга через Steam (Shift+Tab или «Присоединиться к игре»).
  + Вы видите друг друга: ходьба, анимации, переходы между локациями,
    зданиями и комнатами подземелья.
  + Как в оригинальном кооперативе: хост выглядит Ягнёнком (в своём руне),
    друг Козлом, над головами метки 1 и 2. Меняется в настройках мода:
    Settings > Multiplayer > Character (Auto / Lamb / Goat).
  + Общий забег: одинаковые этажи и комнаты.
  + Общие враги: кто первым вошёл в комнату, у того «живут» враги, у второго
    они повторяют их движения. Враги атакуют обоих. Урон и убийства общие.
  + Общий лут: что подобрал один, получают оба.
  + Смерть: если друг жив, вы падаете без сознания. Друг поднимает вас,
    подойдя и отдав вам одно своё сердце (как в кооперативе игры), или зачистив комнату.
    Экран смерти только когда пали оба.
  + Значок плохой связи в правом верхнем углу (появляется, только если
    связь плохая дольше 2 секунд):
      три палочки и «!»   пинг до друга от 120 мс (жёлтые) или от 200 мс (красные);
      пакеты с пропуском  теряется от 5 % пакетов (от 15 % красный);
      выдернутый кабель   друг не отвечает больше 3 секунд.
    Короткие обрывы и потери игра переживает: события (убийства, лут,
    зачистки) доходят после восстановления связи.
  + Диалоги: отвечает тот, кто говорит. Второй видит варианты и может
    указать на один (значок красного Ягнёнка или синего Козла).
  + В меню паузы список игроков: значок, имя в Steam, хост, пинг.
  + Анимации: удары, перекаты, заклинания и падения партнёра видны как у
    него. Партнёр появляется в комнате только когда она у него загрузилась.
  + Карта между этажами общая: кто первым выбрал путь, туда идут оба.
  + Каждый ходит по этажу сам; комнаты, в которых побывал партнёр,
    открываются и на вашей мини-карте.
  + Гадалка даёт обоим одни и те же карты.
  + Сломанная трава, кусты, бочки, ящики, камни и прочее ломаются у обоих,
    обстановка комнат одинаковая.
  + Друг играет на сохранении хоста, как в обычном кооперативе: заходит
    из главного меню, своё сохранение не нужно. У друга сохранение хоста
    не записывается, его собственные сохранения не трогаются. После выхода
    друг возвращается в главное меню.
  + Если связь оборвалась, подключитесь снова через Steam.
  + Культ общий: мир ведёт игра хоста. Последователи у друга ходят как у
    хоста, постройки, новые последователи, ресурсы и время общие. Что друг
    строит, сносит, тратит или меняет, появляется у хоста.
  + Команды последователям, проповеди и ритуалы друга действуют и у хоста.
    С последователем говорит кто-то один: второй увидит «Кто-то уже
    разговаривает с этим последователем».
  + Доктрины, ритуалы и их откаты, вера, квесты общие.
  + Деревья, камни и кусты: каждый удар партнёра виден, лут только у того,
    кто бил.
  + При подключении версии мода сравниваются: если разные, видны обе, играть
    можно. Друг может обновиться до версии хоста (игра перезапустится и сама
    подключится снова) или предложить обновиться хосту.
  + Мод обновляется сам: в главном меню предложит новую версию, скачает,
    проверит, поставит и перезапустит игру (начиная со следующей версии).
  + Предметы в культе больше не дюпаются и не теряются.
  + Раз в 4 секунды игры сверяют культ и чинят расхождения. Что разошлось и
    сколько стоит синхронизация, пишется в BepInEx\LogOutput.log.
  - Не проверено: два разных аккаунта Steam. Что изменилось в версиях: CHANGELOG.md на GitHub.

ЧТО НУЖНО
  - Игра Cult of the Lamb в Steam у обоих, версия 1.5.26 (Steam build 25223484).
    Если Steam обновит игру, сборку, возможно, придётся пересобрать.
  - Сохранение нужно только хосту.

УСТАНОВКА (у каждого игрока)
  1. Закройте игру.
  2. Запустите CotlCoopInstaller.exe и нажмите «Установить». Он сам найдёт
     игру в Steam (или укажите папку через «Обзор…»). Windows может
     предупредить о неизвестном издателе: «Подробнее» → «Выполнить в любом
     случае». Если файлы других модов будут заменены, установщик сохранит
     их копию и при удалении вернёт всё как было.
     Без установщика: распакуйте архив и дважды щёлкните install.cmd.
  3. Запускайте игру из Steam как обычно.

КАК ИГРАТЬ
  Хост:
    1. Главное меню → «Мультиплеер» → «Создать лобби».
    2. Выберите сохранение (или пустой слот: новая игра вдвоём).
    3. «Пригласить друга», дождитесь его в лобби и нажмите «Играть».
    Из своей игры тоже можно: Esc → «Онлайн игра».
  Друг:
    1. Главное меню → «Мультиплеер» → имя друга в «Лобби друзей»,
       или примите приглашение в Steam.
    2. Вы стоите в лобби, пока хост не нажмёт «Играть». Если хост уже
       играет, игра сама загрузит его игру и отправит вас в его культ.
  Подземелье:
    Хост заходит в подземелье первым, друг следом (в течение 10 минут)
    через ту же дверь. По этажу каждый ходит сам. В одной комнате враги
    общие; если разойтись по разным комнатам, каждый дерётся со своими.
  Выход:
    Esc → «Покинуть игру друга» (у друга) или «Закрыть онлайн игру» (у хоста).
    Хост продолжает свою игру, друг возвращается в главное меню.

ЕСЛИ ЧТО-ТО НЕ ТАК
  Пришлите файл <папка игры>\BepInEx\LogOutput.log с обеих сторон.
  Если друг не подключается за 15 секунд, появится «Couldn't reach your friend».

УДАЛЕНИЕ
  Закройте игру, запустите CotlCoopInstaller.exe и нажмите «Удалить мод»
  (или дважды щёлкните uninstall.cmd).

ЛИЦЕНЗИИ
  Этот мод и COTLMP: MIT. COTL_API: MIT. BepInEx: LGPL-2.1. Тексты лицензий лежат в LICENSES.


==================================================================================
Cult of the Lamb Co-op Anything 0.9.2: online co-op over Steam (pre-release)
==================================================================================

github.com/XDinka/cult_of_the_lamb_co-op_anything
Based on the COTLMP mod (github.com/GeoB99/COTLMP, MIT), with joining over
Steam added: no IP addresses, no port forwarding, works behind a VPN or WARP.
Traffic goes through Valve's relays.

BUGS AND IDEAS: Discord https://discord.gg/52azdsYMD (in the game: Multiplayer >
Bugs and ideas; an error window's "Copy the log" copies the log and opens it).

WHAT IT DOES
  + A lost connection (nothing from the host for 30 s) is taken up again: the
    friend waits behind the loading screen ("Connecting 1/3"), joins the host
    again by itself and is back in the host's room with their hearts and gear.
    After three tries: the main menu with the error.
  + Text chat: T opens it, Enter sends, Esc closes. While typing the game
    doesn't hear the keyboard or the pad.
  + The mod updates without leaving the game; a broken update is rolled back.
    An update during a game: restart the lobby, restart the game, or later.
  + A game update that breaks part of the mod: the rest works, the main menu
    says so (with an update when a newer mod is out), both hear it in a session.
  + A friend who comes back (left, lost the connection, crashed) is taken to
    the host's room with their things; each friend is a character of their own
    in the host's save.
  + A conversation waits for a partner in another room; the doors stay shut.
  + The players' heads on the dungeon's minimap, panels as in the game's co-op.
  + All 15 languages of the game; Arabic right to left with joined letters in
    the mod's own boxes, also inside a Latin line.
  + Scenes and films end for both together: whoever is done or skips first
    waits behind a black screen ("Waiting for your partner to finish
    watching"); the friend goes on once the host's world is up ("Waiting for
    the sync to be confirmed" if that takes a while). Films can be skipped.
  + At a land's leader the first in waits by the door for the partner; the
    scene and the fight start for both together.
  + Both players' hearts on screen as in the game's co-op (the Goat in purple).
  + Fallen in battle while the partner fights on: you are knocked down. The
    partner gets you up by giving you one of their hearts at your side, or
    clears the room. The death screen comes only when both fall, the same
    rooms and run time for both.
  + The map between floors shows both crowns and where each points.
  + Conversations and scenes in one place go at the host's pace, for both.
  + A newcomer is recruited once, never twice.
  + The partner's bruises and black eye show (two hearts or less, one heart).
  + The friend's sermon and ritual in a temple are tested: the host gets them.
  + The grass and flowers of the first runs are the same for both.
  + A lobby in the main menu, in the game's own look: Multiplayer, Create
    Lobby, a save or an empty slot. The Lamb and the Goat with Steam names and
    avatars; friends' open lobbies are listed; Play starts both.
  + A new game together: each plays the opening alone until the crown, whoever
    gets it first waits; then both come back to life at once (the friend as
    the Goat) and go on to the cult.
  + The friend plays on the host's save, as in the game's own co-op: nothing
    of it is written on their side, their own saves stay as they were.
  + Shared dungeon runs: same floors and rooms, shared enemies, loot, kills,
    knockout and revive, the map between floors, tarot, broken things.
  + One cult for both: followers, structures, resources, doctrines, rituals and
    time are the host's; what the friend builds, spends or orders reaches it.
  + Mod versions are compared on joining; the mod updates itself.
  + A bad connection icon in the top right corner.
  - Not tested yet: two real Steam accounts. What changed in each version: CHANGELOG.md on GitHub.

WHAT YOU NEED
  - Cult of the Lamb on Steam for both, version 1.5.26 (Steam build 25223484).

INSTALLING (each player)
  1. Close the game.
  2. Run CotlCoopInstaller.exe and press Install. It finds the game in Steam
     (or pick its folder with Browse). Windows may warn about an unknown
     publisher: "More info", then "Run anyway". Without the installer: unpack
     the zip and double-click install.cmd.
  3. Start the game from Steam as usual.

PLAYING
  Host: main menu, Multiplayer, Create Lobby, pick a save (or an empty slot for
    a new game together), Invite a Friend, then Play. From your own game:
    Esc, Open Online.
  Friend: main menu, Multiplayer, your friend's name under "Friends' lobbies",
    or accept the Steam invite. You wait in the lobby until the host's Play.
  Dungeons: the host goes in first, the friend follows (within 10 minutes)
    through the same door.

IF SOMETHING GOES WRONG
  Send <game folder>\BepInEx\LogOutput.log from both sides.

REMOVING
  Close the game, run CotlCoopInstaller.exe and press Remove mod (or
  double-click uninstall.cmd).

LICENSES
  This mod and COTLMP: MIT. COTL_API: MIT. BepInEx: LGPL-2.1. See LICENSES.
