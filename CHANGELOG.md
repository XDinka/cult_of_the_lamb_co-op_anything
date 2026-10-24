# Changelog

Every release is on the [releases page](https://github.com/XDinka/cult_of_the_lamb_co-op_anything/releases)
with its installer. Both players need the same version.

## 0.3.3 (2026-10-25)

Fixes for what came up in the first game on two real Steam accounts.

**Fixed: the hearts and stats panel went blank.** The partner's lamb is a second lamb in the game's
player list, so the game builds its two-player HUD; but it was always "the Lamb", took the Lamb's
panel, and as it was made again on every room change the HUD was rebuilt around it, and the
two-player panels were only brought on screen in the game's own local co-op. Only for a moment after
a load did ours show. Setting itself up it also reset the save's blue, black and spirit hearts in a
dungeon. Now both panels show as in the game's co-op: the Lamb's in red, the Goat's in purple with
the Goat's icon, each with its own player's real hearts (sent with the partner's moves). See
`CoopHud.cs`.

![The host's screen: its Lamb, and the friend's Goat below](Media/changelog/0.3.3-hud-host.png)
![The friend's screen: the host's Lamb, and its own Goat in purple](Media/changelog/0.3.3-hud-friend.png)

**Both crowns on the map between floors.** The partner's crown stands on their stop, the eye and the
co-op ring in their colour (the Lamb's red, the Goat's purple), and a small faint crown of theirs on
the way they are pointing at. See `MapCrowns.cs`.

![The friend's Goat crown beside the host's Lamb crown, and where the friend points](Media/changelog/0.3.3-map-crowns.png)

**Fixed: enemies gone before the follower's altar in the opening.** The opening's stops, and a
dungeon's resource and follower stops, keep the floor number of the floor before, so their rooms had
the same names as rooms already cleared: a room counted as cleared and lost its enemies, and the
follower's altar never started. A room's name now carries the stop of the run's map:

```
before: [COOP] Game Biome Intro#1.1@0,0 was cleared by the partner, opening it   (the follower's stop)
now:    Dungeon1#1.1.0@0,0 ... Dungeon1#1.1.1@-1,0 ... (the way taken on the map is part of it)
```

**Fixed: the same follower twice.** A newcomer waits at the gate in both games with the same id; when
one player recruited it, it came to the other's cult and stood next to the newcomer still waiting
there, which could be recruited again. Now the one who starts recruiting has it (the other gets
"Someone is already talking to this follower") and the waiting copy goes when the follower comes. See
`CultRecruits.cs`.

```
[COOP] The partner is recruiting follower 3
[QA client] Recruit test: we try to talk to it while the host recruits: refused
[COOP] Follower 3 was recruited by the partner: our newcomer goes
[QA client] Recruit test: follower 3: 1 in the cult, 0 waiting, 1 listed, 0 newcomers
```

**Scenes go at the host's pace.** A conversation both games play in one place (a boss's words before
the fight, the story's talks) is one scene, as in the game's co-op: the friend's copy follows the
host's lines, takes the host's answer and ends with the host's, so what comes after (a boss waking up)
starts in both games together. A talk only the friend has is the friend's after a few seconds. See
`SharedScenes.cs`.

```
[COOP] The host leads the conversation in Dungeon1#1.1.0@3,0 (4 lines)
[COOP] The host answered 1, so do we              (the friend tried to rush it and pick answer 0)
[QA client] Scene test: over after 3.7 s, answer 1
```

**The partner's loot.** What the partner breaks (pots, grass, trees, rocks, berry bushes) drops its loot
on your screen too, and it flies to the partner; it is theirs, never yours. See `GhostLoot.cs`.

**Not tested yet:** the Leshy fight itself with the new scene pacing (the automated test has no way
to the boss yet), the first follower's whole tutorial after a real opening.

## 0.3.2 (2026-10-24)

**Fixed: bruises and the black eye.** The game dresses the lamb by its hearts in a dungeon: bruises
at two hearts or less, a black eye at one (and black and white when there is no faith left for a
curse, and the mop of its chore level). The partner's puppet only wore the fleece and the weapon, so
it always looked unhurt. Now each game sends how it dressed its own lamb with its moves, and the
puppet wears the same. See `Appearance.Look` and `PuppetSync.cs`.

![The host's lamb on the friend's screen: healed, then on one heart](Media/changelog/0.3.2-bruises.png)

```
[QA host] Hurt test: our hp 2, our look 101          (bruises)
[QA host] Hurt test: our hp 1, our look 102          (black eye)
[QA host] Hurt test: our hp 6, our look 100
[QA client] Hurt test: player 0's puppet looks 101 (bruises 1)
[QA client] Hurt test: player 0's puppet looks 102 (bruises 2)
[QA client] Hurt test: player 0's puppet looks 100 (bruises 0)
```

**Tested: the friend's sermon and ritual in a real temple.** The host builds a temple; the friend goes
in, preaches a sermon (with the game's first-sermon tutorial and its upgrade) and holds a holiday.
The followers gather in the friend's game and go back to the host's after. On the host's side: the
sermon's day, the followers' devotion and thoughts (watched a sermon, holiday), the holiday and its
cooldown, the 75 bones it cost, and faith that moves the same on both sides.

![The friend (the Goat) holds a holiday in the host's temple](Media/changelog/0.3.2-holiday-friend.png)

```
[QA host] Temple: sermonday=1 day=1 holiday=True cooldown=0.90 bones=25 adoration=30.0
          thoughts=3:WatchedSermon+DanceCircleLed+BadChat,4:WatchedSermon+Holiday+...  hands=0
```

**Fixed: the grass of the first runs.** A room's grass rolled the same in both games, but its odds
come from the save: the first three runs have plain grass in the first land, and a flower quest or few
flowers in the inventory raise the flower odds. Two saves apart by a run grew different rooms (plain
grass for one, flowers for the other). Whoever starts a run now sends those numbers with it and both
games grow the run's grass from them. See `RunGrass.cs`.

```
[COOP] Starting run 1733995097 in Dungeon1_1 (run count 2)
[COOP] Joining the partner's run 1733995097 in Dungeon1_1 (their run count 2, ours 11)
before: the same room, host plain grass 14/0/0, friend 0/21/0; now both plain grass
```

**Fixed: a follower's quest talk starting on the friend's side.** Every half a minute the game picks a
follower to come up with a quest or a story talk (the first follower's tutorial talks too). The
friend's game picked its own, at random, and told the host's game about it as cult data. Now the host's
game picks for both; either player can take the talk, as with any follower.

**Checked again:** enemies of the room's owner go for both players and hit the partner through the
network (in the test: 11 hits on the friend's puppet, 5 taken by the friend), and the partner's puppet
at a door takes nobody along (since 0.2.0).

**Not tested yet:** two real Steam accounts.

## 0.3.1 (2026-10-24)

**Fixed: the night fox with both players there.** Each game had its own fox, so both players could
start a talk with it at once, the deal counted in one game only, and the place and the talk went apart.
A deal with the fox changes the save (what it takes, what it gives), and the save is the host's, so
now the host makes it: the friend gets "The host makes deals with the fox". When the host talks to
it, the friend's fox comes out of the dark as the host's does, the talk and its answers show at the
bottom of the friend's screen, and the outcome (the fox's encounter, its introduction, the place it
is done with) is the friend's too. See `FoxSync.cs`.

![The host's talk with the fox, on the friend's screen](Media/changelog/0.3.1-fox-friend.png)

```
[QA client] Talking to the fox                       (refused: the host makes deals with the fox)
[COOP] Talking to the fox in HubShore                (the host)
[COOP] The host's fox talk in HubShore starts        (the friend's fox comes out)
[COOP] The host's fox talk in HubShore is over (done there: False)
[QA client] Fox after: encounter=0 done= intro=HubShore
```

**Fixed: the shared opening after the crown ran as "no run".** Both games took a run numbered 0,
which the mod treats as none, so part of what is shared in a run (who joined it, what to send on a
rejoin) didn't work there. Now each starts its own, and at the crown the friend takes the host's:

```
[COOP] Taking the partner's run -301648455 (ours was 897309048)
[COOP] Room Game Biome Intro#1.1@0,0 cleared in run -301648455 (ours -301648455)
```

**Fixed:** the game's text animation tags (`<wave>` and the like) showed as text in the partner's
conversation at the bottom of the screen.

## 0.3.0 (2026-10-24)

**A lobby in the main menu**
- The main menu's Multiplayer button opens an online menu built from the game's own menu (its header,
  buttons, highlight and fonts, on its canvas), in place of the old server browser: **Create Lobby**,
  and the friends who have a lobby open. See `OnlineMenu.cs`.
- Create Lobby shows the game's own save slots: a save, or an empty slot for a new game (with the
  game's new game options). The lobby opens online play; the Lamb of the menu's key art steps aside for
  the Goat (a copy of it in its goat skin), and each wears its player's Steam name and avatar. The Goat
  is faint until a friend stands in the lobby. **Invite a Friend** opens the Steam overlay; **Play**
  starts both. See `Lobby.cs`.

  ![The host's lobby with a friend in it](Media/changelog/0.3.0-lobby-host.png)
  ![The friend's online menu](Media/changelog/0.3.0-online-menu.png)
  ![The friend in the host's lobby](Media/changelog/0.3.0-lobby-friend.png)

- It scales with the game: checked at 800x450, 1024x768, 1680x720 and 1920x1080 (on a narrow screen
  the two stand closer).

  ![1024x768](Media/changelog/0.3.0-lobby-1024x768.png)
  ![1680x720](Media/changelog/0.3.0-lobby-1680x720.png)

**A new game together**
- Play on a new game: each player plays the opening alone, the Lamb led to the altar in chains, until
  the One Who Waits gives the crown. Whoever has it first waits; then both come back to life at once in
  the same place, the friend as the Goat, and the rest of the opening is a shared run. On its way to the
  cult the friend's game takes the host's save, as for any join. The friend's own opening is never
  saved. See `IntroCoop.cs`.

  ![The host waits for the friend's crown](Media/changelog/0.3.0-crown-wait.png)
  ![Back to life together: the host's Lamb and the friend's Goat](Media/changelog/0.3.0-reborn-together.png)

- Tested on two copies, the crown reached by a shortcut and the end of the opening by a jump to the
  cult:

  ```
  [LOBBY] Play: NewGame with 1 friend(s)
  [INTRO] We have the crown, the partner not yet
  [INTRO] The partner has the crown, going on together
  [INTRO] The opening is over: into the host's cult, on its save
  [COOP] Got the host's save (4102 bytes, 0 followers), going to its cult
  ```
- On a save: Play loads it on the host; the friend's request for the save waits until the host's game
  is up, then the friend goes to the cult.

**Also**
- English and Russian for everything new, and for the last texts of the mod that were English only
  (opening online play, a different game version).
- The release's README is in English too.

## 0.2.9 (2026-10-24)

**Both players' mod versions compared on joining**
- The joining player and the host send their mod versions. If they differ, the joining player sees
  both, "Host's version" and "Your version", before the host's save is loaded. Playing on is allowed,
  with a warning that different versions may cause errors or damage the save. See `VersionCheck.cs`.
- The host on a newer version: **Update** takes exactly the host's version (its release by tag), the
  game restarts and joins the host again by itself.
- The host on an older version: **Ask the host to update** sends the host a window with both versions;
  the host updates to the friend's version, the game restarts, and the host opens the game online
  again and picks the save.
- The host hears of a friend on another version as a short note. If updating doesn't work, the join
  goes on and the release page is offered.

  ![The friend joining a host on an older version](Media/changelog/0.2.9-versions-friend.png)
  ![The host asked to update](Media/changelog/0.2.9-versions-host.png)

- Tested on two copies: the friend updated to the host's (fake) newer release, restarted and joined
  again by itself:

  ```
  [COOP] Mod versions differ: the host's 0.2.10, ours 0.2.9
  [COOP] Mod versions differ, the joining player chose: update
  ... the installer waits, installs, starts the game again ...
  [UPDATE] Updated to the host's version, joining host 1 again
  [STEAM] Joined host 1 in scene Main Menu
  [COOP] Got the host's save (4192 bytes, 0 followers), going to its cult
  ```

**Fixed:** the installer started by the mod inherited the game's open sockets and files, and so did
the game it started again (that game couldn't open its connection); it is now started through the
shell. A host now answers every join with its version, not only the first one of a session.

**Known:** Windows Smart App Control can block a new installer it doesn't know yet (the installer
isn't code-signed); then the mod offers the release page.

## 0.2.8 (2026-10-24)

**The mod updates itself**
- Once a game start the mod asks GitHub for the latest release. If it is newer, the main menu offers
  it in the game's own window. Taking it downloads the release's installer, checks it against the
  SHA-256 GitHub gives for it, closes the game, installs the new version and starts the game again
  with the same launch options. If anything fails, it offers the release page instead. See
  `COTLMP/Network/Updater.cs`.

  ![The update offer in the main menu](Media/changelog/0.2.8-update-in-game.png)

- The installer checks too: an older installer offers the newest one, downloads it, checks it and
  starts it for the same folder. New switches: `--wait <game process>` and `--relaunch`, used by the mod.

  ![The installer offering a newer version](Media/changelog/0.2.8-update-installer.png)

- Tested end to end on a test copy (a fake release 0.2.8 offered to a mod that took itself for 0.2.7):

  ```
  [UPDATE] Latest release 0.2.8, this mod 0.2.7: an update is offered
  [UPDATE] Installer 0.2.8 (9db8ca0d...) started, closing the game for it
  ... the installer waits, installs, starts the game again ...
  [UPDATE] Started again after taking 0.2.8
  ```

**Fixed on the way:** a game started again by the installer from a modded game came up without mods
(BepInEx's loader marks are inherited through the environment); the mod and the installer clear them.

**Note:** the self-update needs the new installer on the release it takes, so it works from 0.2.8 to
the next version on. From 0.2.7 to 0.2.8, update once with the installer as before.

## 0.2.7 (2026-10-24)

**Fixes**
- Items duplicated in the cult: a pickup was counted twice, by the shared pickups made for dungeons
  and by the cult's one inventory. In the cult only the inventory counts it now; the shared pickups
  just take the other side's copy off the floor.
- Items could get lost: the host's inventory arriving at the friend's overwrote what the friend had
  picked up and not sent yet. That now stays and goes with the friend's next look.
- What the friend's game changes on its own at a new part of the day (the weather's timing) is no
  longer sent to the host as the friend's doing.

**A sync check on top of the sync**
- Every 4 s the owner of a part of the cult (the host, or the friend for the followers in their hands)
  sends one hash per group: followers, structures, the cult's data, the inventory. The other side
  hashes its own, 2 ms a frame at most; a group that differs gets the owner's list of hashes per
  object; an object that still differs on the next check is asked for, and the owner sends it whole
  or says it's gone. Changes still on their way differ once and aren't touched. See `CultCheck.cs`.
- Every fix is logged with what differed. From the test, after a rename, the sermon day, 3 stone and
  a torch were lost on the way on purpose:

  ```
  [COOP] Sync check 22: F3 Differs, S96 MissingHere, S97 ExtraHere, D291 Differs two checks in a row, asking the host
  [COOP] Sync check: F3 differed from the partner's in _name, took theirs
  [COOP] The partner built DECORATION_TORCH (96)
  [COOP] The partner's DECORATION_TORCH (97) is gone, ours too
  [COOP] Sync check: the cult's data differed from the partner's in PreviousSermonDayIndex, took theirs
  ```
- It also found real differences: a follower arriving at the friend's had its needs (hunger, rest,
  social) and clothes set by the friend's game when it appeared; now fixed by the check.

**Load, measured and lowered**
- Once a minute the log says what the sync cost: milliseconds a second, the worst frame, KB/s, the
  game's garbage collections, checks and fixes; a frame over 50 ms is logged with what it was doing.
- Simple cult data (numbers, flags, text) is compared without packing it, and structures and the
  cult's data are looked at every other second: from about 11 to 3.3 to 4.4 ms of work a second
  (~0.06 ms a frame), the worst frame from 6 to 22 ms down to 2.5 ms, about 0.5 KB/s. The 230 to 270 ms
  frames were garbage collections landing in the sync's work; none since.

  ```
  [COOP] Cult sync, last minute: 3.45 ms a second, worst frame 2.41 ms, 0.47 KB/s sent, 1 garbage collections in the game; checks 64 sent, ...
  ```

**Seen in the game:** the host chops a tree; this is the friend's screen (the host is the Lamb by the
tree, the tree goes down to its stump):

![The host's hits on a tree, on the friend's screen](Media/changelog/0.2.7-tree-hits.gif)

**Known:** on a new save the game's own first-follower conversation also starts on the friend's side
for a moment; to be fixed with the shared opening in 0.3.0.

## 0.2.6 (2026-10-24)

**The friend's hands in the cult**
- A follower the friend talks to, or that the friend's temple gathers for a sermon or a ritual, runs in
  the friend's game for that while; the host's copy stands still and shows what it does there. When the
  friend's game is done with it, it goes back to the host's game, which carries on from the orders it
  got (sleep, a job, a meal, prison). So the friend's commands, sermons and rituals now take effect in
  the host's world. See `CultHands.cs`.
- One player talks to a follower at a time: the other gets "Someone is already talking to this
  follower".
- The rest of the cult's data is shared too: doctrines, rituals done and their cooldowns, faith and
  the hunger bar, the day of the last sermon, quests and the like (DataManager, member by member).
  What is each player's own (hearts, looks) or a run's stays apart, and what a run changed isn't sent
  back to the cult.
- Followers move between places (the temple and the cult) on both sides as the game moves them.
- A changed outfit or necklace shows on the other side at once.

**Fixes**
- Trees, rocks and berry bushes: every hit shows for the player who watches as it lands (shake,
  progress bar, the stump, smoke and sound), not a jump once a second and then a vanished tree.
- A rock broken by the other player no longer drops a second lot of stone for the one who watched.
- New followers and structures no longer flicker in and out: an object that arrived from the partner
  while the cult was being compared was taken as lost, and went back and forth (also in 0.2.5).
- No interaction prompt over the other player's head: their puppet no longer looks for things to use.

**Not tested yet:** a real sermon and ritual in a temple (the automated test's cult has no temple; the
gathering itself is tested), and two real Steam accounts.

## 0.2.5 (2026-10-24)

- One cult for both: the host's game runs it (followers, structures, time); the friend's game shows it.
  Followers walk and animate as the host's, new and gone followers and structures appear and go, the
  inventory is shared. What the friend builds, removes, spends or changes reaches the host.
- The old time sync (the joining player's clock running faster or waiting) is gone: the clock is the host's.

## 0.2.4 (2026-10-24)

- The friend plays on the host's save, as in the original co-op: joins from the main menu without a
  save of their own; leaving takes them back to the main menu and the host's save is forgotten.
- A room the partner cleared opens even if its doors shut on you; a shut room with no enemy left opens
  after 8 s.
- Cleanup: the host's reward pools, crash recovery and the pause menu guard went.

## 0.2.3 (2026-10-24)

- Dialogs: whoever talks answers, as in local co-op; the other player sees the choices.
- Player list in the pause menu (icon, Steam name, host, ping).
- No online play in the prologue, as the game keeps local co-op out of it.

## 0.2.2 (2026-10-24)

- Pause menu works in the opening, before the crown.

## 0.2.1 (2026-10-24)

- Russian strings for the mod; the button is "Open Online" / «Онлайн игра».
- Accepting a friend's invite while hosting alone joins the friend.
- The installer and the README in the game's style.

## 0.1.x (2026-10-22 to 2026-10-23)

- Online co-op over Steam on top of COTLMP: invites, the Lamb and the Goat, shared dungeon runs (same
  floors and rooms, room owners, shared enemies, loot and kills), knockout and revive, partner
  animations, the map between floors, tarot readings, broken things, a bad connection icon, the installer.
