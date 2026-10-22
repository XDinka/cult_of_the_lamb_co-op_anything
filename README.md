<p align="center">
  <img alt="Cult of the Lamb" src="https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/1313140/19f32bb477a6e9d058e05193b4e282149bd70644/header.jpg?t=1786554901">
</p>

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
| See the other lamb: walking, animations, scene changes | works |
| Same place grouping: cult, buildings, each dungeon room | works |
| Shared dungeon layout (same seed for both players' run) | works in the automated test |
| Shared enemy health (hit on one side lowers it on the other, kills sync) | in progress |
| Enemies chasing / hitting the other player, shared loot, followers, base | not synced, each player has their own |
| Leaving / host stopping keeps you in your own game | works |

The friend's lamb is a "ghost" on your screen: your enemies can't hurt it, and it doesn't open
doors or pick up loot for you.

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
  fraction of max health, so different difficulty multipliers still line up.

## Testing

`COTLMP/Debug/TestHarness.cs` has command line switches (`-cotlmp-fakesteam=N`, `-cotlmp-autohost`,
`-cotlmp-join=N`, `-cotlmp-autodungeon=N`, `-cotlmp-autokill`, `-cotlmp-report`, ...) that let two
copies of the game on one PC host, join, walk, enter a dungeon and fight unattended.

## License

MIT, see [LICENSE](LICENSE). Original COTLMP credits are in [CREDITS](CREDITS). Cult of the Lamb is
© Massive Monster / Devolver Digital. This project is not affiliated with them and ships no game files. The header image is the game's Steam store art.

---

## По-русски коротко

Онлайн-кооператив для Cult of the Lamb через Steam. Каждый играет на своём сохранении, вы видите
друг друга в культе и в забегах. Раскладка подземелья общая, урон по врагам синхронизируется
(в работе). Установка: `install.cmd` из релизного архива у обоих игроков. Хост: Esc, затем «Open to
friends», затем пригласить друга. В подземелье хост заходит первым, друг идёт следом через ту же дверь.
