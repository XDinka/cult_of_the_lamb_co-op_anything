<p align="center">
  <img alt="Cult of the Lamb × Co-op Anything" src="Media/cover.png">
</p>

<p align="center"><sub>Fan-made mod, not affiliated with or endorsed by Massive Monster or Devolver Digital.</sub></p>

# Cult of the Lamb Co-op Anything

**Version 0.1.0, pre-release.**

Online co-op for **Cult of the Lamb** over Steam. You invite a friend through the Steam overlay.
Each of you keeps playing your own save, and you see each other in the cult and on dungeon runs.
Dungeon runs are shared: same floors, same rooms, and damage to enemies counts on both sides.

> **Pre-release 0.1.0.** Tested with two copies of the game on one PC and a loopback stand-in for
> Steam, including a simulated bad connection. Real two-account Steam play has not been tested yet.
> Expect bugs, and back up your saves.

Based on [COTLMP](https://github.com/GeoB99/COTLMP) by GeoB99, Neco-Arc and contributors (MIT),
which provides the player puppets, the UDP protocol and the integrated server.

## What works

| Feature | State |
|---|---|
| Join through Steam (invite, "Join game", `+connect_lobby`) | works on loopback, real Steam: lobby and messaging checked, two accounts untested |
| See the other player: walking, animations, scene changes | works |
| Lamb and Goat, like local co-op (setting "Character": Auto / Lamb / Goat), with the co-op 1/2 indicators | works |
| Shared dungeon layout (same seed for both players' run) | works in the automated test |
| Room owner: whoever enters a room first runs its enemies, the other player's enemies follow them | works in the automated test |
| Enemies attack both players, hits on your lamb come from the owner's game | works in the automated test |
| Shared enemy health and kills | works in the automated test |
| Shared pickups: what one picks up, both get | works in the automated test |
| Knocked out instead of dead while the partner fights on; stand next to them for 3 s to revive, or clear the room | works in the automated test |
| Game over only when both are down | works in the automated test |
| Rooms one player cleared are cleared for the other (no second fight), wounds and kills carry over | works in the automated test |
| The partner's conversations show at the bottom of your screen, camera on them, you keep control | works in the automated test |
| Dialog choices are voted on: the partner's answers show for you too, each player's mark (1 or 2) next to the answer they picked, the host decides (the joining player answers alone if the host isn't in the room or doesn't pick within 30 s) | works in the automated test (both directions) |
| Enemy attacks of the room owner (shots incl. homing, beams, bombs, warning circles, other pooled attacks) show for the other player and move exactly as the owner's | works in the automated test (forced shots) |
| Rewards roll from the host's unlocks (weapons, curses, tarot, relics) for both, without touching the joining player's save | works in the automated test (tarot: host 85 cards, joiner 15, same draws) |
| Chests, weapon/relic podiums, tarot draws and interactions roll the same for both; each player opens and picks on their own | chests checked in the automated test |
| Followers, base, rituals | not synced, each player has their own |
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

1. Both players run `install.cmd` from the release zip (it finds the game in Steam, backs up what it
   replaces, and `uninstall.cmd` restores it).
2. Host: load a save, press Esc, then **Open to friends**, and invite the friend in the Steam overlay.
3. Friend: accept the invite (or "Join game" in the friends list) and load your own save.
4. Dungeons: the **host enters the dungeon first** and the friend follows within 10 minutes, through
   the same door. Then you both get the same run. Go through the rooms together.

## Building

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
- **Same rolls.** Before each chest, podium, tarot or interaction roll, Unity's random state is set
  from the room seed, the run and what is rolled. Chest drops stay with whoever opened that chest.
  See `RoomRandom.cs`.
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
  the line and votes with the mouse or the 1-4 keys. Votes show as the player's number next to the
  answer. In the joining player's conversation a click is a vote until the host picks, and the host's
  pick is clicked for them. See `Dialogs.cs`.
- **Rejoining.** A player who (re)joins says hello; the host answers with its run (even after the
  run is too old to be adopted by a newcomer), every room cleared in it and what is known of its
  enemies' health. A handshake from an address already in the game replaces the old connection at
  once, so a restarted game doesn't wait for the 30 s timeout.
- **Crash recovery.** While the joining player is in a shared run, `BepInEx/config/cotlmp-rejoin.json`
  keeps the host, lobby, save slot, run, floor, room and equipment (rewritten every 2 s). On the next
  start it is read and deleted at once, then used as above. Leaving the session, quitting the game
  or finishing the run deletes it, so only a crash brings you back. See `RunRecovery.cs`.
- **Pickups and looks.** `SharedPickups.cs` and `Appearance.cs` (fleece number in the handshake,
  1003 is the Goat as in the game).

## Testing

`COTLMP/Debug/TestHarness.cs` has command line switches (`-cotlmp-fakesteam=N`, `-cotlmp-autohost`,
`-cotlmp-join=N`, `-cotlmp-autodungeon=N`, `-cotlmp-autokill`, `-cotlmp-report`, ...) that let two
copies of the game on one PC host, join, walk, enter a dungeon and fight unattended.
`-cotlmp-netchaos=LOSS,DELAY,JITTER,EVERY,OUTAGE` makes the loopback link behave like a bad
connection (reliable packets delayed for resends and drop-outs, unreliable ones lost),
`-cotlmp-netflood` floods the partner with large and small messages, and `-cotlmp-netshots` with
`-cotlmp-shots=DIR` saves a frame of each connection icon. `-cotlmp-autochoice=N` runs the voting test,
`-cotlmp-crashafter=N` kills the joining copy mid-run (with `CLIENT_RESTART="-cotlmp-autoenter"`
`run-pair.sh` starts it again), and `-cotlmp-endafter=N -cotlmp-rejoin=1` leaves and joins again.
`dotnet run --project tests/ServerChecks` checks the server alone: large relays, junk packets and a
player coming back on the same address.

## License

MIT, see [LICENSE](LICENSE). Original COTLMP credits are in [CREDITS](CREDITS). Cult of the Lamb is
© Massive Monster / Devolver Digital. This project is not affiliated with them and ships no game files. The cover uses the game's key art.

---

## По-русски коротко

Версия 0.1.0, предрелиз.

Онлайн-кооператив для Cult of the Lamb через Steam. Каждый играет на своём сохранении, вы видите
друг друга в культе и в забегах. Подземелье общее: враги одни на двоих и атакуют обоих, лут
получают оба, павшего друга можно поднять (постоять рядом 3 секунды) или зачистить комнату.
Хост выглядит Ягнёнком, друг Козлом, как в оригинальном кооперативе. Установка: `install.cmd` из релизного архива у обоих игроков. Хост: Esc, затем «Open to
friends», затем пригласить друга. В подземелье хост заходит первым, друг идёт следом через ту же дверь.
Если связь плохая, в правом верхнем углу появляется значок: три палочки с восклицательным знаком
(пинг от 120 мс жёлтые, от 200 мс красные), пакеты с пропущенным (потери от 5 %) или выдернутый кабель
(друг не отвечает).
Выбор в диалогах решается голосованием: варианты видят оба, метка игрока (1 или 2) стоит у его
варианта, окончательно решает хост. Если игра у друга вылетела посреди забега, после перезапуска
она сама загрузит сохранение, подключится к хосту и вернёт его в тот же забег с тем же снаряжением.
