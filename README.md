<p align="center">
  <img alt="Cult of the Lamb × Co-op Anything" src="Media/cover.png">
</p>

<p align="center"><sub>Fan-made mod, not affiliated with or endorsed by Massive Monster or Devolver Digital.</sub></p>

# Cult of the Lamb Co-op Anything

Online co-op for **Cult of the Lamb** over Steam. You invite a friend through the Steam overlay.
Each of you keeps playing your own save, and you see each other in the cult and on dungeon runs.
Dungeon runs are shared: same floors, same rooms, and damage to enemies counts on both sides.

> **Early alpha.** Tested with two copies of the game on one PC and a loopback stand-in for Steam.
> Real two-account Steam play has not been tested yet. Expect bugs, and back up your saves.

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
| Enemy attacks of the room owner (shots incl. homing, beams, bombs, warning circles, other pooled attacks) show for the other player and move exactly as the owner's | works in the automated test (forced shots) |
| Rewards roll from the host's unlocks (weapons, curses, tarot, relics) for both, without touching the joining player's save | works in the automated test (tarot: host 85 cards, joiner 15, same draws) |
| Chests, weapon/relic podiums, tarot draws and interactions roll the same for both; each player opens and picks on their own | chests checked in the automated test |
| Followers, base, rituals | not synced, each player has their own |
| Leaving / host stopping keeps you in your own game | works |

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
- **Pickups and looks.** `SharedPickups.cs` and `Appearance.cs` (fleece number in the handshake,
  1003 is the Goat as in the game).

## Testing

`COTLMP/Debug/TestHarness.cs` has command line switches (`-cotlmp-fakesteam=N`, `-cotlmp-autohost`,
`-cotlmp-join=N`, `-cotlmp-autodungeon=N`, `-cotlmp-autokill`, `-cotlmp-report`, ...) that let two
copies of the game on one PC host, join, walk, enter a dungeon and fight unattended.

## License

MIT, see [LICENSE](LICENSE). Original COTLMP credits are in [CREDITS](CREDITS). Cult of the Lamb is
© Massive Monster / Devolver Digital. This project is not affiliated with them and ships no game files. The cover uses the game's key art.

---

## По-русски коротко

Онлайн-кооператив для Cult of the Lamb через Steam. Каждый играет на своём сохранении, вы видите
друг друга в культе и в забегах. Подземелье общее: враги одни на двоих и атакуют обоих, лут
получают оба, павшего друга можно поднять (постоять рядом 3 секунды) или зачистить комнату.
Хост выглядит Ягнёнком, друг Козлом, как в оригинальном кооперативе. Установка: `install.cmd` из релизного архива у обоих игроков. Хост: Esc, затем «Open to
friends», затем пригласить друга. В подземелье хост заходит первым, друг идёт следом через ту же дверь.
