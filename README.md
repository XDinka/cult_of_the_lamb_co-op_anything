<p align="center">
  <img alt="Cult of the Lamb × Co-op Anything" src="Media/cover.png">
</p>

<p align="center"><sub>Fan-made mod, not affiliated with or endorsed by Massive Monster or Devolver Digital.</sub></p>

# Cult of the Lamb Co-op Anything

**Version 0.2.0, pre-release.**

Online co-op for **Cult of the Lamb** over Steam. You invite a friend through the Steam overlay.
Each of you keeps playing your own save, and you see each other in the cult and on dungeon runs.
Dungeon runs are shared: same floors, same rooms, and damage to enemies counts on both sides.

> **Pre-release 0.2.0.** Tested with two copies of the game on one PC and a loopback stand-in for
> Steam, including a simulated bad connection. Real two-account Steam play has not been tested yet.
> Expect bugs, and back up your saves.

Based on [COTLMP](https://github.com/GeoB99/COTLMP) by GeoB99, Neco-Arc and contributors (MIT),
which provides the player puppets, the UDP protocol and the integrated server.

## What works

| Feature | State |
|---|---|
| Join through Steam (invite, "Join game", `+connect_lobby`) | works on loopback, real Steam: lobby and messaging checked, two accounts untested |
| See the other player: walking, scene changes, and every animation they play (attacks, dodges, spells, hits, knockouts) with their weapon | works in the automated test (attack animations repeated on the puppet) |
| The other player shows up in a room only once it has loaded for them, already in their look | works in the automated test |
| Lamb and Goat, like local co-op (setting "Character": Auto / Lamb / Goat), with the co-op 1/2 indicators | works |
| Shared dungeon layout (same seed for both players' run) | works in the automated test |
| Room owner: whoever enters a room first runs its enemies, the other player's enemies follow them | works in the automated test |
| Enemies attack both players, hits on your lamb come from the owner's game (a dodge in time still saves you) | works in the automated test |
| Shared enemy health and kills | works in the automated test |
| Shared pickups: what one picks up, both get | works in the automated test |
| Knocked out instead of dead while the partner fights on; stand next to them for 3 s to revive, or clear the room | works in the automated test |
| Game over only when both are down | works in the automated test |
| The map between floors is the host's for both; either player picks the way, whoever picks first, and the other is taken there. The next floor gets the same seed | works in the automated test (two floors, the joining player picked, the host followed) |
| Each player walks a floor on their own (the partner's puppet at a door takes nobody along); rooms one player opened show on the other's minimap | works in the automated test |
| Rooms one player cleared are cleared for the other (no second fight), wounds and kills carry over | works in the automated test |
| The partner's conversations show at the bottom of your screen, camera on them, you keep control | works in the automated test |
| Dialog choices: both see the answers, the game's co-op icons (red Lamb, blue Goat) show which answer each player is on, the host's pick counts for both (the joining player answers alone only when the host isn't in the room) | works in the automated test (both directions) |
| Enemy attacks of the room owner (shots incl. homing, beams, bombs, warning circles, other pooled attacks) show for the other player and move exactly as the owner's | works in the automated test (forced shots) |
| Rewards roll from the host's unlocks (weapons, curses, tarot, relics) for both, without touching the joining player's save | works in the automated test (tarot: host 85 cards, joiner 15, same draws) |
| Chests, weapon/relic podiums, tarot draws and interactions roll the same for both; each player opens and picks on their own | chests checked in the automated test |
| The tarot reader in a dungeon deals both players the same two cards | works in the automated test |
| Time of day is the host's for both (the joining player's clock runs faster, waits or skips forward, never back) | works in the automated test (clocks within a few game minutes) |
| Rooms come up with the same grass, bushes and breakable decorations (barrel, crate or bone pile) for both | works in the automated test |
| Grass, bushes, pots, barrels, rocks, critters and enemy bodies broken by either player break for the other, also in rooms entered later | works in the automated test (both players cutting at once) |
| Followers, base, rituals | not synced, each player has their own (the clock follows the host) |
| Leaving / host stopping keeps you in your own game | works |
| Leaving and joining again mid-run: same run, same rooms and rolls | works in the automated test |
| Back into the run after a crash: the game loads your save, joins the host, enters the same run, puts back weapon, curse, relic, tarot cards and health, and teleports you to the host's room | works in the automated test (first floor; later floors untested) |
| Bad connection icon: ping 120+ ms yellow bars, 200+ ms red; packet loss 5 %+ (15 %+ red); unplugged cable when the partner stops answering | works in the automated test (simulated bad connection) |
| Holds up on a bad connection: 5 % loss, 80±40 ms delay, 4 s drop-outs every 25 s, floods of large and many small messages | works in the automated test (200 s dungeon run, no disconnects, 0 errors) |

## Requirements

- Cult of the Lamb on Steam for both players, version **1.5.26** (Steam build 25223484).
- [BepInEx 5.4.23.5](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5) and
  [COTL API 0.3.4](https://github.com/xhayper/COTL_API/releases/tag/v0.3.4).
  The release zip contains both and an installer.
- At least one save on each side. You join from your own loaded game.

## Playing

1. Both players run `CotlCoopInstaller.exe` and press **Install** (it finds the game in Steam, backs up
   what it replaces, and **Remove mod** puts it back). Windows may warn about an unknown publisher:
   "More info", then "Run anyway". Without the installer: `install.cmd` from the release zip.
2. Host: load a save, press Esc, then **Open to friends**, and invite the friend in the Steam overlay.
3. Friend: accept the invite (or "Join game" in the friends list) and load your own save.
4. Dungeons: the **host enters the dungeon first** and the friend follows within 10 minutes, through
   the same door. Then you both get the same run. Each of you walks the floor freely; enemies are
   shared in a room you are both in.

## Building

`packaging/build-installer.sh` builds the release package (see `build-package.sh` for the paths it
needs) and then `CotlCoopInstaller.exe`, a one-file installer with the package inside
(`installer/`, .NET Framework 4.7.2, which every Windows 10/11 has). It also runs silently:
`CotlCoopInstaller.exe --quiet [--dir "<game folder>"] [--auto uninstall]`.

```
dotnet build COTLMP/COTLMP.csproj -c Release "-p:CotlManagedDir=<game dir>\Cult Of The Lamb_Data\Managed"
```

.NET SDK 10 (the code uses C# 14 null-conditional assignment). `CotlManagedDir` points at the
game's `Managed` folder for `com.steamworks.net.dll`. `packaging/build-package.sh` builds the
release zip, and `tools/run-pair.sh` runs the automated two-copy test. Both scripts read their
paths from environment variables (see the top of each).

## How it works

- **Transport.** The game already runs Steam under its own AppID. On the host every friend gets a
  loopback UDP socket into the integrated COTLMP server, and on the friend's side a local proxy
  forwards to the host's SteamID over `ISteamNetworkingMessages` (Valve relays, no ports, works
  behind VPN/NAT). See `COTLMP/Network/SteamBridge.cs` and `PeerLink.cs`.
- **Places.** Players send their place (scene, or scene + floor + room in a dungeon) and the server
  only shows players to each other in the same place.
- **Shared runs.** When a run starts, a run id goes to the partner, who adopts it if they start a run
  in the same dungeon soon after. Each floor seed is derived from that id, and `UnityEngine.Random` is
  reset from the room seed when a room is generated or entered. See `COTLMP/Game/CoopSync.cs`.
- **Enemies.** Enemies are matched by place, name and spawn order. Health changes go over as a
  fraction of max health, so different difficulty multipliers still line up. The first player in a
  room owns it: their game runs the AI (targeting both lambs) and streams positions and animations;
  the other game stops its copies' AI once they have spawned in and removes any the owner doesn't
  have. See `COTLMP/Game/RoomSync.cs`.
- **Knockout.** Uses the game's own co-op knockout (`CoopManager.KnockOutPlayer`, woken on room
  clear), plus a 3 second stand-next-to-revive bar. See `COTLMP/Game/CoopLife.cs`.
- **Cleared rooms.** A completed room is sent to the partner (per run); when they walk in, their
  copy is completed the way the game marks a finished room, without a second fight or a second
  reward. Remote kills drop no loot, since the killer's loot is shared. See `ClearedRooms.cs`.
- **Conversations.** Lines go as localization terms; the partner sees them and their camera
  follows the speaker. See `Dialogs.cs`.
- **Enemy attacks.** Everything the owner's game takes from the object pool that is an enemy attack
  is sent once by prefab name, then its position, rotation and scale ten times a second; the follower
  takes the same prefab from its own pool and moves the copy to match. Copies hurt nobody. See
  `HazardSync.cs`. Attacks created outside the pool (a few addressable beams) are not covered.
- **Host's pools.** The host sends its weapon, curse, tarot and relic pools and a few counters; the
  joining player's game uses them only for the length of each reward roll, then gets its own back
  (anything added during a roll is kept). See `HostPools.cs`.
- **The map between floors.** The game builds a run's map from a seed and from the player's save
  (objectives, unlocks and inventory decide some nodes), so two saves never get the same one. The
  host builds it when the run's first room is up and sends every node; the joining player's game
  takes it instead of building its own. Whoever picks a way first sends it; the other's map takes
  that pick as soon as it is open (or at their click), with the node as the picker had it (a
  shuffle is carried over). The next floor's seed is set from the run and the floor. See `MapSync.cs`.
- **Walking a floor.** Doors and exits only react to the local player, so the partner's puppet
  walking through one takes nobody along. Rooms the partner walks into are marked visited on the
  minimap. See `PlayerManager.cs` (`PuppetDoors`) and `MiniMapSync.cs`.
- **The partner's lamb.** Ten times a second, and at once when an animation starts, each game sends
  what its lamb's skeleton plays on every track and how far in, its facing, skin, weapon, position
  and place. The puppet plays exactly that (its own animator is switched off). That message is also
  the word that the partner's room has loaded: the puppet is made only then, in their look, and goes
  when they stop sending from the room. See `PuppetSync.cs`.
- **Same rolls.** Before each chest, podium, tarot or interaction roll, and before each spot of a
  room picks its grass, bush or breakable decoration, Unity's random state is set from the room seed,
  the run and what is rolled where. Chest drops stay with whoever opened that chest. See `RoomRandom.cs`.
- **Bad connections.** Events (kills, pickups, cleared rooms, conversations) go reliable and in
  order; enemy and attack positions and the pings go unreliable, so a lost position doesn't hold up
  the events behind it and is replaced 0.1 s later. Messages up to 60 KB (a room full of enemies),
  an 8 MB Steam send buffer for drop-outs, 4 MB buffers on the local sockets, the game keeps running
  without focus during a session (exclusive full screen would stop the relay), and a message that
  can't be read is skipped and logged instead of ending the session. Sends don't wait for the next
  frame, so a fight's traffic can't pile up into lag. See `Network.cs`, `SteamBridge.cs`, `PeerLink.cs`.
- **Connection icon.** Pings go to the partner twice a second the way the co-op data goes; the round
  trip is the ping, the ones that don't come back are the loss. The icon only shows while the
  connection has been bad for 2 s, or at once when the partner stops answering. See `ConnectionQuality.cs`.
- **Choices.** When the answer wheel comes up, its answers go to the partner, who sees them above
  the line and points with the mouse or the 1-4 keys. Each player's co-op icon (the game's red Lamb
  and blue Goat rings) stands above the answer they are on, in the wheel and in the overlay. The
  host's pick is the answer in either player's conversation: in the joining player's own
  conversation a click only points, and the host's pick is clicked for them. See `Dialogs.cs`.
- **Rejoining.** A player who (re)joins says hello; the host answers with its run (even after the
  run is too old to be adopted by a newcomer), every room cleared in it and what is known of its
  enemies' health. A handshake from an address already in the game replaces the old connection at
  once, so a restarted game doesn't wait for the 30 s timeout.
- **Crash recovery.** While the joining player is in a shared run, `BepInEx/config/cotlmp-rejoin.json`
  keeps the host, lobby, save slot, run, floor, room and equipment (rewritten every 2 s). On the next
  start it is read and deleted at once, then used as above. Leaving the session, quitting the game
  or finishing the run deletes it, so only a crash brings you back. See `RunRecovery.cs`.
- **Tarot readings.** A reader's cards come from the player's save (cards found, held or met this run,
  fleece, season) and a save without a reading yet gets the Lovers first, so a seed can't make them
  match. Whoever draws first sends the two cards and the other reading deals those. See `SharedTarot.cs`.
- **Time of day.** The host sends its clock every 2 s; the joining player's clock runs twice as fast
  when a little behind, stands still when a little ahead and skips forward like sleeping when far off.
  Their cult lives through the skipped hours; the day count stays each save's own. See `TimeSync.cs`.
- **Broken things.** Whatever with health dies in a dungeon room (grass, pots, critters, enemy
  bodies) and rubble walked into is sent by name, where it stood when the room came up and its number
  among the things of that name in the room; the other game breaks the one that stood there (else the
  same number, else the nearest of the name) without loot. Kept per run, so a room entered later
  comes up broken the same way. See `PropSync.cs`.
- **Pickups and looks.** `SharedPickups.cs` and `Appearance.cs` (fleece number in the handshake,
  1003 is the Goat as in the game).

## Testing

`COTLMP/Debug/TestHarness.cs` has command line switches (`-cotlmp-fakesteam=N`, `-cotlmp-autohost`,
`-cotlmp-join=N`, `-cotlmp-autodungeon=N`, `-cotlmp-autokill`, `-cotlmp-report`, ...) that let two
copies of the game on one PC host, join, walk, enter a dungeon and fight unattended.
`-cotlmp-netchaos=LOSS,DELAY,JITTER,EVERY,OUTAGE` makes the loopback link behave like a bad
connection (reliable packets delayed for resends and drop-outs, unreliable ones lost),
`-cotlmp-netflood` floods the partner with large and small messages, and `-cotlmp-netshots` with
`-cotlmp-shots=DIR` saves a frame of each connection icon. `-cotlmp-autochoice=N` runs the dialog choice test, `-cotlmp-nextfloor` takes both through the floor's
exit and the map to the next floor (`-cotlmp-nomapclick` only waits for the partner's pick), `-cotlmp-animtest`
plays attack animations for the partner's puppet to repeat, `-cotlmp-tarottest` deals tarot to both, `-cotlmp-autocut` cuts grass
next to the host, `-cotlmp-window=X,Y,W,H` keeps a copy in a small window (`run-pair.sh` puts them side by side),
`-cotlmp-crashafter=N` kills the joining copy mid-run (with `CLIENT_RESTART="-cotlmp-autoenter"`
`run-pair.sh` starts it again), and `-cotlmp-endafter=N -cotlmp-rejoin=1` leaves and joins again.
`dotnet run --project tests/ServerChecks` checks the server alone: large relays, junk packets and a
player coming back on the same address.

## License

MIT, see [LICENSE](LICENSE). Original COTLMP credits are in [CREDITS](CREDITS). Cult of the Lamb is
© Massive Monster / Devolver Digital. This project is not affiliated with them and ships no game files. The cover uses the game's key art.

---

## По-русски коротко

Версия 0.2.0, предрелиз.

Онлайн-кооператив для Cult of the Lamb через Steam. Каждый играет на своём сохранении, вы видите
друг друга в культе и в забегах. Подземелье общее: враги одни на двоих и атакуют обоих, лут
получают оба, павшего друга можно поднять (постоять рядом 3 секунды) или зачистить комнату.
Хост выглядит Ягнёнком, друг Козлом, как в оригинальном кооперативе. Установка: `CotlCoopInstaller.exe` у обоих игроков (или `install.cmd` из релизного архива). Хост: Esc, затем «Open to
friends», затем пригласить друга. В подземелье хост заходит первым, друг идёт следом через ту же дверь.
Если связь плохая, в правом верхнем углу появляется значок: три палочки с восклицательным знаком
(пинг от 120 мс жёлтые, от 200 мс красные), пакеты с пропущенным (потери от 5 %) или выдернутый кабель
(друг не отвечает).
Карты у гадалки одинаковые, время суток как у хоста, сломанная трава, горшки и камни ломаются у обоих.
Карта между этажами общая (карта хоста), путь выбирает хост: друг видит карту и ждёт, потом
попадает туда же. Выбор в диалогах: варианты видят оба, значок игрока (красный Ягнёнок, синий Козёл) стоит над
вариантом, на который он указывает, выбирает хост. Если игра у друга вылетела посреди забега, после перезапуска
она сама загрузит сохранение, подключится к хосту и вернёт его в тот же забег с тем же снаряжением.
