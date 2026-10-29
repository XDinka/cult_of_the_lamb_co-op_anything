/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Command line switches that let two copies of the game test the mod unattended
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP.Data;
using COTLMP.Game;
using COTLMP.Ui;
using MMTools;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Debug
{
    /// <summary>
    /// Test-only automation. Nothing here runs unless one of its switches is on the command line:
    /// <list type="bullet">
    /// <item><c>-cotlmp-autohost</c>: go from the main menu into the cult and start a server</item>
    /// <item><c>-cotlmp-autoenter</c>: go from the main menu into the cult (for the joining copy)</item>
    /// <item><c>-cotlmp-autowalk</c>: once connected, walk the local lamb in a circle</item>
    /// <item><c>-cotlmp-report</c>: log the local and remote lamb positions every 2 seconds</item>
    /// <item><c>-cotlmp-quitafter=N</c>: quit the game after N seconds</item>
    /// <item><c>-cotlmp-endafter=N</c>: leave or stop hosting N seconds after the session starts</item>
    /// <item><c>-cotlmp-rejoin=HOST</c>: with -cotlmp-endafter, join the loopback host again after leaving</item>
    /// <item><c>-cotlmp-autodungeon=N</c>: N seconds after the session starts, start a run and enter the first dungeon</item>
    /// <item><c>-cotlmp-autokill</c>: in a dungeon, hit the nearest enemy every second (shared enemy health test)</item>
    /// <item><c>-cotlmp-autodie=N</c>: N seconds after entering a dungeon, die once (knockout test); the other copy walks over to revive</item>
    /// <item><c>-cotlmp-window=X,Y,W,H</c>: keep the game in a W x H window at X,Y (the game's own settings would make it full screen again on load)</item>
    /// <item><c>-cotlmp-tarottest</c>: in the first dungeon room both have, a tarot reader deals two cards to the host, then to the joining copy (shared tarot test)</item>
    /// <item><c>-cotlmp-autocut</c>: in a dungeon room shared with the partner, cut the nearest grass or other neutral thing every 2 s (prop sync test)</item>
    /// <item><c>-cotlmp-nextfloor</c>: in a dungeon, once no unvisited room is left, go to the floor's exit and through it; on the map the host clicks the last way out after 3 s, the joining copy (which walks through the exit itself once both are in that room) the first after 0.5 s; whoever is first, the other is taken there (map sync test)</item>
    /// <item><c>-cotlmp-lobby=SLOT|new</c>: open the lobby from the main menu on that save slot (or a new game in the hidden slot 6) and press Play a while after a friend stands in it</item>
    /// <item><c>-cotlmp-lobbyplay=N</c>: with -cotlmp-lobby, press Play N seconds after the friend came (6 by default)</item>
    /// <item><c>-cotlmp-lobbyjoin</c>: join the first friend's lobby from the online menu</item>
    /// <item><c>-cotlmp-lobbyshots=DIR</c>: frames of the online menu, the lobby and the opening's crown</item>
    /// <item><c>-cotlmp-skipintro=N</c>: N seconds into a new game's opening, go straight to the crown</item>
    /// <item><c>-cotlmp-leaveintro=N</c>: with -cotlmp-skipintro, leave the opening for the cult N seconds after coming back to life</item>
    /// <item><c>-cotlmp-pretendversion=0.2.6</c>: the update check takes this mod for that version</item>
    /// <item><c>-cotlmp-updatefrom=URL</c>: the release is read from there instead of GitHub (a file:/// one for tests)</item>
    /// <item><c>-cotlmp-versionchoice=update|play|suggest</c>: what the joining player picks when the mod versions differ</item>
    /// <item><c>-cotlmp-versionshots=DIR</c>: frames of the version windows</item>
    /// <item><c>-cotlmp-takeupdate</c>: an update offered is taken at once, the installer runs without its window</item>
    /// <item><c>-cotlmp-nohotupdate</c>: an update goes through the installer even when it could go in the running game</item>
    /// <item><c>-cotlmp-updatebarshot=FILE</c>: a frame of the update's screen with its bar</item>
    /// <item><c>-cotlmp-ingameupdate=N</c>, <c>-cotlmp-updatechoice=lobby|game|later</c>: N s into a game the update (from -cotlmp-updatefrom) is taken and that way of putting it in picked</item>
    /// <item><c>-cotlmp-foxtest</c>: both go to the shore at night; the friend tries to talk to the night fox (refused), then the host makes its deal</item>
    /// <item><c>-cotlmp-loadslot=6</c>: no session, load the hidden save slot 6 from the main menu as Continue does (other slots are refused); <c>-cotlmp-savelater=N</c>: N s in the cult, save</item>
    /// <item><c>-cotlmp-dlcprobe</c>: in the cult, every 20 s log what the save has of the DLCs (flags, the land, marriages, decorations); it only reads</item>
    /// <item><c>-cotlmp-followthrow</c>: the enemy follow loop's next round throws once (it must be logged, counted and go on)</item>
    /// <item><c>-cotlmp-campprobe</c>: in the cult, every 15 s log the faith circle's and the hunger bar's state (container, fade), the flags and the flock, and save a frame</item>
    /// <item><c>-cotlmp-culttest</c>: in the cult, each side changes it (the host: wood, a bed, a new follower; the friend: stone, a torch, a follower's name, the bed gone) for the other to show</item>
    /// <item><c>-cotlmp-hudtrace</c>: log every time the HUD is hidden or shown, with who asked</item>
    /// <item><c>-cotlmp-dieonrevive</c>: with the other copy's -cotlmp-autodie, we die halfway through getting them up (both down test)</item>
    /// <item><c>-cotlmp-bosslate=N</c>, <c>-cotlmp-bosslayer=N</c>: with -cotlmp-bosstest, come N s later; go to that layer's leader (4: the Leshy)</item>
    /// <item><c>-cotlmp-bosstest</c>: from the first dungeon room both go to the land's leader (the floor made again with the leader's room), the host a moment first; both read its talk on (the friend faster) and log the scene and the leader's health</item>
    /// <item><c>-cotlmp-recruittest</c>: a newcomer at the gate for both; the host recruits it while the friend tries to talk to it; both log how many of that follower they have</item>
    /// <item><c>-cotlmp-endtest</c>: in a dungeon room both copies play a scene, the host's ends 6 s first and must wait for the friend's (scene ends test); <c>-cotlmp-endtest=friend</c>: the friend's ends first</item>
    /// <item><c>-cotlmp-readydelay=N</c>: the host tells the friend its world is up N s late (the friend's sync wait)</item>
    /// <item><c>-cotlmp-filmtest[=NAME]</c>: in a dungeon room both copies play a film (Intro by default), the friend's 5 s later; the host's must wait for the friend's</item>
    /// <item><c>-cotlmp-filmskip=N</c>: with -cotlmp-filmtest, press skip N s into our film (a black screen until the partner's film is over)</item>
    /// <item><c>-cotlmp-scenetest</c>: in a dungeon room both copies play the same conversation, the host reads on slowly and answers the second answer, the friend tries to rush it and answer the first (shared scenes test)</item>
    /// <item><c>-cotlmp-templetest</c>: the host builds a temple and gets three followers and bones; the friend goes in, preaches a sermon, holds a holiday and closes the altar through its menus</item>
    /// <item><c>-cotlmp-animtest</c>: every 3 s, while the partner is in the room, play one of the lamb's attack animations (puppet animation test)</item>
    /// <item><c>-cotlmp-pausetest</c>: every 8 s open the pause menu and log whether it is still open 2 s later; <c>-cotlmp-pausenoplayer</c> does it with no lamb in the player list, as in the game's opening</item>
    /// <item><c>-cotlmp-nomapclick</c>: with -cotlmp-nextfloor, never click on the map, only wait for the partner's pick</item>
    /// <item><c>-cotlmp-solo</c>: the host fights and moves on without waiting for the partner (cleared-room test); the joining copy walks the host's trail</item>
    /// <item><c>-cotlmp-autotalk=N</c>: N seconds after both stand in one dungeon room, hold a short conversation (dialog sync test)</item>
    /// <item><c>-cotlmp-autoshoot</c>: while running a shared room, fire an enemy projectile at the partner every 3 s (projectile sync test)</item>
    /// <item><c>-cotlmp-autochoice=N</c>: N s after both stand in the cult, answer together: the host asks, sees which answer the partner points at and picks it; then the joining player asks, clicks an answer (it only points) and gets the host's pick (dialog choice test)</item>
    /// <item><c>-cotlmp-netshots</c>: with -cotlmp-shots, save a frame the first time each kind of bad connection icon shows</item>
    /// <item><c>-cotlmp-netflood</c>: every 3 s send the partner a 20 KB packet and a burst of 300 small ones of a kind they ignore (network stress test; pair it with -cotlmp-netchaos=LOSS,DELAY,JITTER,EVERY,OUTAGE for a bad connection)</item>
    /// <item><c>-cotlmp-drawtest</c>: in each shared dungeon room draw 3 tarot cards the way a chest would and log them (host pools test); the host also gets extra unlocks in memory so the two saves differ</item>
    /// <item><c>-cotlmp-hurt=N</c>: N seconds after both stand in one dungeon room, the host's hearts go to two, one and back (bruises test); the joining copy logs and frames the looks the host's puppet takes on</item>
    /// <item><c>-cotlmp-runcount=N</c>: our save has had N runs (the grass of the first land grows from it), so two copies' saves differ; <c>-cotlmp-nograssfix</c> grows the grass from our own save as before</item>
    /// <item><c>-cotlmp-shots=DIR</c>: save the game's own frame to DIR a few seconds into every room shared with the partner</item>
    /// <item><c>-cotlmp-sessions=N</c>, <c>-cotlmp-sessionlen=S</c>: N sessions in one running game, the host stopping
    /// S s after the friend came and opening online again, the friend joining again; after each both log what of the
    /// mod's state is still set (TestHarness.Sessions.cs)</item>
    /// <item><c>-cotlmp-chattest</c>: in the cult together, the host types a line (the chat opens, the text goes in, a frame,
    /// Enter) and checks the game got its input back; the friend answers what it got; frames of both chats</item>
    /// <item><c>-cotlmp-steamselftest</c>: after hosting, send a packet to our own SteamID over real Steam</item>
    /// </list>
    /// </summary>
    internal static partial class TestHarness
    {
        private static bool AutoHost, AutoEnter, AutoWalk, Report, SteamSelfTest, AutoKill, LobbyJoin, FoxTest;
        private static float QuitAfter, EndAfter, AutoDungeon;
        private static ulong RejoinHost;
        private static string ShotsDir;
        private static float AutoDie;
        private static float AutoHurt;
        private static bool Solo;
        private static bool NextFloor;
        private static bool NoMapClick;
        private static bool AnimTest, CultTest, TempleTest, SceneTest, RecruitTest, BossTest, DieOnRevive;
        private static bool EndTest, FilmTest, FriendEndsFirst;
        private static string FilmName = "Intro";
        private static float FilmSkipAt;
        private static bool PauseTest, PauseNoPlayer;
        private static bool TarotTest;
        private static bool AutoCut;
        private static int[] WindowRect;
        private static float AutoTalk;
        private static bool AutoShoot;
        private static bool ChatTest, FinalTest;
        private static bool CampProbe;
        private static bool DlcProbe;
        private static int LoadSlot = -1;
        private static float SaveLater;
        /* -cotlmp-scenetest=chain|scene|leave: at its second line the host's talk is replaced by the next one
           (never closed), or the host goes to another scene, or the host ends the session; =hold: the host's
           talk starts in the next room before the friend is in */
        private static string SceneBreak;
        /* -cotlmp-scenequiet=N: in -cotlmp-scenetest the host's conversation steps reach the friend N s late from its second line (a lagging host) */
        private static float SceneQuiet;
        /* -cotlmp-hudbreak: after the scene test the hearts' bar goes out under a shown HUD (the INV-HUD probe must catch it) */
        private static bool HudBreak;
        /* -cotlmp-fleece=N: the run starts with that fleece (4 or 6: four tarot cards in the first room) */
        private static int Fleece;
        /* -cotlmp-menutest: a new thing's card stands 5 s in the dungeon (the HUD watchdog must leave the game's hide be) */
        private static bool MenuTesting;
        /* -cotlmp-kbtest: the knucklebones tables are asked for a match for two with the puppet in the room */
        private static bool KnucklebonesTesting;
        /* -cotlmp-nopress: no press on the title (the lobby test's host stays there: run-pair's retry is tried so) */
        private static bool NoPress;
        /* -cotlmp-rescue: we walk to a fallen partner and get them up for a heart, with no killing of the room (it would wake them first) */
        private static bool Rescuing;
        /* -cotlmp-floorrush: the host goes straight to the floor's way down (once), the friend after it on the map;
           -cotlmp-diefloor=N: -cotlmp-autodie only from the N-th floor on */
        private static bool FloorRush;
        private static int DieFloor;
        /* -cotlmp-deathat=N: both fall for good on the N-th floor, the friend first (death screen test);
           -cotlmp-deathruns=K: K runs in a row, each ended by falls for good (alone too) */
        private static int DeathAt, DeathRuns;
        /* -cotlmp-hostfirst: in -cotlmp-deathruns the host falls first, the friend at its side */
        private static bool HostFirst;
        /* -cotlmp-alone: -cotlmp-deathruns without a partner (one copy, no session) */
        private static bool Alone;
        /* -cotlmp-escape: in -cotlmp-deathruns we leave the run (going home) instead of falling; -cotlmp-escapeother: the partner does */
        private static bool Escape, EscapeOther;
        /* -cotlmp-afterdown, -cotlmp-waitend, -cotlmp-walkout, -cotlmp-nextfloordown, -cotlmp-nodeathfor=N: other ends of a run in -cotlmp-deathruns */
        private static bool AfterDown, WaitEnd, WalkOut, NextFloorDown;
        private static float NoDeathFor;
        /* -cotlmp-quietstate=N: for N s after each room comes alive our state isn't told to the partner (a player standing still) */
        private static float QuietState;
        /* -cotlmp-invincible: nothing fells our lamb all along (tests about other things than falling);
           -cotlmp-forcepull: in a leader's room, a pull to a scene's start comes while the room's own walk is on */
        private static bool Invincible, ForcePull;
        /* -cotlmp-fatepick=N: in the final test's ending the host takes answer N of the fallen one's fate (0 spare, 1 kill) */
        private static int FatePick;
        /* -cotlmp-lobbyclose=N: the host closes its lobby N s after opening it (the friend must land back in the menu) */
        private static float LobbyClose = -1f;

        /* -cotlmp-land=N: the land the dungeon tests go to (1 to 4, 5 and 6 for the DLC's) */
        private static int Land = 1;
        private static string DungeonScene => $"Dungeon{Land}";
        private static FollowerLocation DungeonStart => (FollowerLocation)Enum.Parse(typeof(FollowerLocation), $"Dungeon{Land}_1");
        private static bool DrawTest;
        private static int RunCount = -1;
        private static float BossLate;
        private static int BossLayer;
        private static bool NetFlood, NetShots;
        private static float AutoChoice;
        private static int FloodSent;

        private static string Tag => $"[QA {(Plugin.GlobalsInternal.IsServerCreator ? "host" : "client")}]";

        /* Counters for the report: test steps that threw */
        internal static int Failed;

        /// <summary>
        /// Starts a test step. One that throws is logged with its name and counted (qafail in the report),
        /// and the other steps go on.
        /// </summary>
        private static Coroutine Run(string Name, IEnumerator Step) => Plugin.MonoInstance.StartCoroutine(Guard(Name, Step));

        /* Test steps refused because they would change a player's save, each told once */
        private static readonly HashSet<string> RefusedOnSave = new();

        /// <summary>
        /// A test step that changes the save (gives items, unlocks, builds, moves the story on) goes ahead only on a
        /// test save: our own game in the hidden slot 6, or a friend's game on the loopback stand-in for Steam, whose
        /// host is held to slot 6 there (PauseMenuPatches.StartHosting). A player's own save is never touched.
        /// </summary>
        internal static bool OnTestSave(string Switch)
        {
            bool Test = COTLMP.Game.HostSave.Playing ? COTLMP.Network.SteamBridge.IsFake : SaveAndLoad.SAVE_SLOT == 6;
            if (!Test && RefusedOnSave.Add(Switch))
                Plugin.Logger?.LogError($"{Tag} {Switch} changes the save: only the hidden test slot 6 (or a friend on the loopback stand-in), not slot {SaveAndLoad.SAVE_SLOT}: left out");
            return Test;
        }

        private static IEnumerator Guard(string Name, IEnumerator Step)
        {
            while (true)
            {
                object Current;
                try
                {
                    if (!Step.MoveNext())
                        yield break;
                    Current = Step.Current;
                }
                catch (Exception e)
                {
                    ++Failed;
                    Plugin.Logger?.LogError($"[QA] Test step {Name} failed: {e}");
                    yield break;
                }
                yield return Current;
            }
        }

        /// <summary>
        /// Reads the switches and starts the coroutines they ask for.
        /// </summary>
        public static void Initialize()
        {
            foreach (string Arg in Environment.GetCommandLineArgs())
            {
                if (Arg.Equals("-cotlmp-autohost", StringComparison.OrdinalIgnoreCase))
                    AutoHost = true;
                else if (Arg.Equals("-cotlmp-autoenter", StringComparison.OrdinalIgnoreCase))
                    AutoEnter = true;
                else if (Arg.StartsWith("-cotlmp-rejoin=", StringComparison.OrdinalIgnoreCase))
                    ulong.TryParse(Arg.Substring("-cotlmp-rejoin=".Length), out RejoinHost);
                else if (Arg.StartsWith("-cotlmp-autodungeon=", StringComparison.OrdinalIgnoreCase))
                    float.TryParse(Arg.Substring("-cotlmp-autodungeon=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out AutoDungeon);
                else if (Arg.StartsWith("-cotlmp-autotalk=", StringComparison.OrdinalIgnoreCase))
                    float.TryParse(Arg.Substring("-cotlmp-autotalk=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out AutoTalk);
                else if (Arg.Equals("-cotlmp-netflood", StringComparison.OrdinalIgnoreCase))
                    NetFlood = true;
                else if (Arg.Equals("-cotlmp-netshots", StringComparison.OrdinalIgnoreCase))
                    NetShots = true;
                else if (Arg.StartsWith("-cotlmp-autochoice=", StringComparison.OrdinalIgnoreCase))
                    float.TryParse(Arg.Substring("-cotlmp-autochoice=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out AutoChoice);
                else if (Arg.Equals("-cotlmp-drawtest", StringComparison.OrdinalIgnoreCase))
                    DrawTest = true;
                else if (Arg.StartsWith("-cotlmp-runcount=", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(Arg.Substring("-cotlmp-runcount=".Length), out RunCount);
                else if (Arg.Equals("-cotlmp-nograssfix", StringComparison.OrdinalIgnoreCase))
                    COTLMP.Game.RunGrass.Off = true;
                else if (Arg.StartsWith("-cotlmp-sessions=", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(Arg.Substring("-cotlmp-sessions=".Length), out Sessions);
                else if (Arg.StartsWith("-cotlmp-sessionlen=", StringComparison.OrdinalIgnoreCase))
                    float.TryParse(Arg.Substring("-cotlmp-sessionlen=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out SessionLength);
                else if (Arg.StartsWith("-cotlmp-land=", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(Arg.Substring("-cotlmp-land=".Length), out Land);
                else if (Arg.Equals("-cotlmp-pickuptrace", StringComparison.OrdinalIgnoreCase))
                    PickupTrace = true;
                else if (Arg.Equals("-cotlmp-floorrush", StringComparison.OrdinalIgnoreCase))
                    FloorRush = NextFloor = true;
                else if (Arg.StartsWith("-cotlmp-diefloor=", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(Arg.Substring("-cotlmp-diefloor=".Length), out DieFloor);
                else if (Arg.StartsWith("-cotlmp-deathat=", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(Arg.Substring("-cotlmp-deathat=".Length), out DeathAt);
                else if (Arg.StartsWith("-cotlmp-fatepick=", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(Arg.Substring("-cotlmp-fatepick=".Length), out FatePick);
                else if (Arg.Equals("-cotlmp-nogotofix", StringComparison.OrdinalIgnoreCase))
                    COTLMP.Game.SceneTrace.WalksUnguarded = true;
                else if (Arg.Equals("-cotlmp-invincible", StringComparison.OrdinalIgnoreCase))
                    Invincible = true;
                else if (Arg.Equals("-cotlmp-forcepull", StringComparison.OrdinalIgnoreCase))
                    ForcePull = true;
                else if (Arg.StartsWith("-cotlmp-quietstate=", StringComparison.OrdinalIgnoreCase))
                    float.TryParse(Arg.Substring("-cotlmp-quietstate=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out QuietState);
                else if (Arg.Equals("-cotlmp-afterdown", StringComparison.OrdinalIgnoreCase))
                    AfterDown = true;
                else if (Arg.Equals("-cotlmp-waitend", StringComparison.OrdinalIgnoreCase))
                    WaitEnd = true;
                else if (Arg.Equals("-cotlmp-walkout", StringComparison.OrdinalIgnoreCase))
                    WalkOut = true;
                else if (Arg.Equals("-cotlmp-nextfloordown", StringComparison.OrdinalIgnoreCase))
                    NextFloorDown = true;
                else if (Arg.StartsWith("-cotlmp-nodeathfor=", StringComparison.OrdinalIgnoreCase))
                    float.TryParse(Arg.Substring("-cotlmp-nodeathfor=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out NoDeathFor);
                else if (Arg.Equals("-cotlmp-escape", StringComparison.OrdinalIgnoreCase))
                    Escape = true;
                else if (Arg.Equals("-cotlmp-escapeother", StringComparison.OrdinalIgnoreCase))
                    EscapeOther = true;
                else if (Arg.Equals("-cotlmp-alone", StringComparison.OrdinalIgnoreCase))
                    Alone = true;
                else if (Arg.Equals("-cotlmp-hostfirst", StringComparison.OrdinalIgnoreCase))
                    HostFirst = true;
                else if (Arg.StartsWith("-cotlmp-deathruns=", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(Arg.Substring("-cotlmp-deathruns=".Length), out DeathRuns);
                else if (Arg.StartsWith("-cotlmp-stuckafter=", StringComparison.OrdinalIgnoreCase))
                    float.TryParse(Arg.Substring("-cotlmp-stuckafter=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out COTLMP.Game.SceneTrace.StuckAfter);
                else if (Arg.Equals("-cotlmp-finaltest", StringComparison.OrdinalIgnoreCase))
                    FinalTest = true;
                else if (Arg.StartsWith("-cotlmp-lobbyclose=", StringComparison.OrdinalIgnoreCase))
                    float.TryParse(Arg.Substring("-cotlmp-lobbyclose=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out LobbyClose);
                else if (Arg.Equals("-cotlmp-chattest", StringComparison.OrdinalIgnoreCase))
                    ChatTest = true;
                else if (Arg.Equals("-cotlmp-autoshoot", StringComparison.OrdinalIgnoreCase))
                    AutoShoot = true;
                else if (Arg.Equals("-cotlmp-solo", StringComparison.OrdinalIgnoreCase))
                    Solo = true;
                else if (Arg.Equals("-cotlmp-nextfloor", StringComparison.OrdinalIgnoreCase))
                    NextFloor = true;
                else if (Arg.Equals("-cotlmp-nomapclick", StringComparison.OrdinalIgnoreCase))
                    NoMapClick = true;
                else if (Arg.Equals("-cotlmp-animtest", StringComparison.OrdinalIgnoreCase))
                    AnimTest = true;
                else if (Arg.StartsWith("-cotlmp-pretendversion=", StringComparison.OrdinalIgnoreCase))
                    COTLMP.Network.Updater.PretendVersion = Arg.Substring("-cotlmp-pretendversion=".Length);
                else if (Arg.StartsWith("-cotlmp-versionchoice=", StringComparison.OrdinalIgnoreCase))
                    COTLMP.Game.VersionCheck.AutoChoice = Arg.Substring("-cotlmp-versionchoice=".Length);
                else if (Arg.StartsWith("-cotlmp-versionshots=", StringComparison.OrdinalIgnoreCase))
                    COTLMP.Game.VersionCheck.ShotsDir = Arg.Substring("-cotlmp-versionshots=".Length);
                else if (Arg.StartsWith("-cotlmp-lobby=", StringComparison.OrdinalIgnoreCase))
                    COTLMP.Game.Lobby.AutoLobby = Arg.Substring("-cotlmp-lobby=".Length);
                else if (Arg.StartsWith("-cotlmp-lobbyplay=", StringComparison.OrdinalIgnoreCase))
                    float.TryParse(Arg.Substring("-cotlmp-lobbyplay=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out COTLMP.Game.Lobby.AutoPlay);
                else if (Arg.Equals("-cotlmp-lobbyjoin", StringComparison.OrdinalIgnoreCase))
                    LobbyJoin = true;
                else if (Arg.StartsWith("-cotlmp-lobbyshots=", StringComparison.OrdinalIgnoreCase))
                    LobbyShots.Dir = Arg.Substring("-cotlmp-lobbyshots=".Length);
                else if (Arg.StartsWith("-cotlmp-skipintro=", StringComparison.OrdinalIgnoreCase))
                    float.TryParse(Arg.Substring("-cotlmp-skipintro=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out COTLMP.Game.IntroCoop.SkipTo);
                else if (Arg.StartsWith("-cotlmp-leaveintro=", StringComparison.OrdinalIgnoreCase))
                    float.TryParse(Arg.Substring("-cotlmp-leaveintro=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out COTLMP.Game.IntroCoop.LeaveAfter);
                else if (Arg.StartsWith("-cotlmp-updateshot=", StringComparison.OrdinalIgnoreCase))
                    COTLMP.Network.Updater.OfferShot = Arg.Substring("-cotlmp-updateshot=".Length);
                else if (Arg.StartsWith("-cotlmp-updatefrom=", StringComparison.OrdinalIgnoreCase))
                    COTLMP.Network.Updater.ReleaseFrom = Arg.Substring("-cotlmp-updatefrom=".Length);
                else if (Arg.Equals("-cotlmp-takeupdate", StringComparison.OrdinalIgnoreCase))
                    COTLMP.Network.Updater.TakeOffer = COTLMP.Network.Updater.QuietInstaller = true;
                else if (Arg.Equals("-cotlmp-nohotupdate", StringComparison.OrdinalIgnoreCase))
                    COTLMP.Network.Updater.NoHotUpdate = true;
                else if (Arg.StartsWith("-cotlmp-ingameupdate=", StringComparison.OrdinalIgnoreCase))
                    float.TryParse(Arg.Substring("-cotlmp-ingameupdate=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out COTLMP.Game.Relobby.TestAfter);
                else if (Arg.StartsWith("-cotlmp-updatechoice=", StringComparison.OrdinalIgnoreCase))
                    COTLMP.Game.Relobby.TestChoice = Arg.Substring("-cotlmp-updatechoice=".Length);
                else if (Arg.StartsWith("-cotlmp-updatebarshot=", StringComparison.OrdinalIgnoreCase))
                    COTLMP.Network.Updater.ProgressShot = Arg.Substring("-cotlmp-updatebarshot=".Length);
                else if (Arg.Equals("-cotlmp-foxtest", StringComparison.OrdinalIgnoreCase))
                    FoxTest = true;
                else if (Arg.Equals("-cotlmp-culttest", StringComparison.OrdinalIgnoreCase))
                    CultTest = true;
                else if (Arg.Equals("-cotlmp-templetest", StringComparison.OrdinalIgnoreCase))
                    TempleTest = true;
                else if (Arg.Equals("-cotlmp-rescue", StringComparison.OrdinalIgnoreCase))
                    Rescuing = true;
                else if (Arg.Equals("-cotlmp-nopress", StringComparison.OrdinalIgnoreCase))
                    NoPress = true;
                else if (Arg.Equals("-cotlmp-kbtest", StringComparison.OrdinalIgnoreCase))
                    KnucklebonesTesting = true;
                else if (Arg.Equals("-cotlmp-menutest", StringComparison.OrdinalIgnoreCase))
                    MenuTesting = true;
                else if (Arg.StartsWith("-cotlmp-fleece=", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(Arg.Substring("-cotlmp-fleece=".Length), out Fleece);
                else if (Arg.Equals("-cotlmp-hudbreak", StringComparison.OrdinalIgnoreCase))
                    HudBreak = true;
                else if (Arg.StartsWith("-cotlmp-loadslot=", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(Arg.Substring("-cotlmp-loadslot=".Length), out LoadSlot);
                else if (Arg.StartsWith("-cotlmp-savelater=", StringComparison.OrdinalIgnoreCase))
                    float.TryParse(Arg.Substring("-cotlmp-savelater=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out SaveLater);
                else if (Arg.Equals("-cotlmp-dlcprobe", StringComparison.OrdinalIgnoreCase))
                    DlcProbe = true;
                else if (Arg.Equals("-cotlmp-followthrow", StringComparison.OrdinalIgnoreCase))
                    COTLMP.Game.RoomSync.FollowThrow = true;
                else if (Arg.Equals("-cotlmp-campprobe", StringComparison.OrdinalIgnoreCase))
                    CampProbe = true;
                else if (Arg.StartsWith("-cotlmp-scenequiet=", StringComparison.OrdinalIgnoreCase))
                    float.TryParse(Arg.Substring("-cotlmp-scenequiet=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out SceneQuiet);
                else if (Arg.StartsWith("-cotlmp-scenetest", StringComparison.OrdinalIgnoreCase))
                {
                    SceneTest = true;
                    if (Arg.StartsWith("-cotlmp-scenetest=", StringComparison.OrdinalIgnoreCase))
                        SceneBreak = Arg.Substring("-cotlmp-scenetest=".Length).ToLowerInvariant();
                }
                else if (Arg.StartsWith("-cotlmp-endtest", StringComparison.OrdinalIgnoreCase))
                {
                    EndTest = true;
                    FriendEndsFirst = Arg.Equals("-cotlmp-endtest=friend", StringComparison.OrdinalIgnoreCase);
                }
                else if (Arg.StartsWith("-cotlmp-readydelay=", StringComparison.OrdinalIgnoreCase))
                    float.TryParse(Arg.Substring("-cotlmp-readydelay=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out COTLMP.Game.SceneEnds.ReadyDelay);
                else if (Arg.StartsWith("-cotlmp-filmskip=", StringComparison.OrdinalIgnoreCase))
                    float.TryParse(Arg.Substring("-cotlmp-filmskip=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out FilmSkipAt);
                else if (Arg.StartsWith("-cotlmp-filmtest", StringComparison.OrdinalIgnoreCase))
                {
                    FilmTest = true;
                    if (Arg.StartsWith("-cotlmp-filmtest=", StringComparison.OrdinalIgnoreCase))
                        FilmName = Arg.Substring("-cotlmp-filmtest=".Length);
                }
                else if (Arg.Equals("-cotlmp-recruittest", StringComparison.OrdinalIgnoreCase))
                    RecruitTest = true;
                else if (Arg.Equals("-cotlmp-bosstest", StringComparison.OrdinalIgnoreCase))
                    BossTest = true;
                else if (Arg.StartsWith("-cotlmp-bosslate=", StringComparison.OrdinalIgnoreCase))
                    float.TryParse(Arg.Substring("-cotlmp-bosslate=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out BossLate);
                else if (Arg.StartsWith("-cotlmp-bosslayer=", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(Arg.Substring("-cotlmp-bosslayer=".Length), out BossLayer);
                else if (Arg.Equals("-cotlmp-dieonrevive", StringComparison.OrdinalIgnoreCase))
                    DieOnRevive = true;
                else if (Arg.Equals("-cotlmp-hudtrace", StringComparison.OrdinalIgnoreCase))
                    TraceHud();
                else if (Arg.Equals("-cotlmp-pausetest", StringComparison.OrdinalIgnoreCase))
                    PauseTest = true;
                else if (Arg.Equals("-cotlmp-pausenoplayer", StringComparison.OrdinalIgnoreCase))
                    PauseTest = PauseNoPlayer = true;
                else if (Arg.Equals("-cotlmp-tarottest", StringComparison.OrdinalIgnoreCase))
                    TarotTest = true;
                else if (Arg.Equals("-cotlmp-autocut", StringComparison.OrdinalIgnoreCase))
                    AutoCut = true;
                else if (Arg.StartsWith("-cotlmp-window=", StringComparison.OrdinalIgnoreCase))
                {
                    string[] Parts = Arg.Substring("-cotlmp-window=".Length).Split(',');
                    if (Parts.Length == 4)
                        WindowRect = Array.ConvertAll(Parts, Part => int.TryParse(Part, out int Value) ? Value : 0);
                }
                else if (Arg.StartsWith("-cotlmp-autodie=", StringComparison.OrdinalIgnoreCase))
                    float.TryParse(Arg.Substring("-cotlmp-autodie=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out AutoDie);
                else if (Arg.StartsWith("-cotlmp-hurt=", StringComparison.OrdinalIgnoreCase))
                    float.TryParse(Arg.Substring("-cotlmp-hurt=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out AutoHurt);
                else if (Arg.StartsWith("-cotlmp-shots=", StringComparison.OrdinalIgnoreCase))
                    ShotsDir = Arg.Substring("-cotlmp-shots=".Length);
                else if (Arg.Equals("-cotlmp-autokill", StringComparison.OrdinalIgnoreCase))
                    AutoKill = true;
                else if (Arg.Equals("-cotlmp-steamselftest", StringComparison.OrdinalIgnoreCase))
                    SteamSelfTest = true;
                else if (Arg.Equals("-cotlmp-autowalk", StringComparison.OrdinalIgnoreCase))
                    AutoWalk = true;
                else if (Arg.Equals("-cotlmp-report", StringComparison.OrdinalIgnoreCase))
                    Report = true;
                else if (Arg.StartsWith("-cotlmp-quitafter=", StringComparison.OrdinalIgnoreCase))
                    float.TryParse(Arg.Substring("-cotlmp-quitafter=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out QuitAfter);
                else if (Arg.StartsWith("-cotlmp-endafter=", StringComparison.OrdinalIgnoreCase))
                    float.TryParse(Arg.Substring("-cotlmp-endafter=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out EndAfter);
            }

            if (!AutoHost && !AutoEnter && !AutoWalk && !Report && QuitAfter <= 0 && EndAfter <= 0)
                return;

            /* Two copies side by side: the one without focus must keep running */
            Application.runInBackground = true;
            Plugin.Logger?.LogWarning($"[QA] Test switches on: autohost={AutoHost} autoenter={AutoEnter} autowalk={AutoWalk} report={Report} quitafter={QuitAfter}");

            if (WindowRect != null)
                Run("KeepWindow", KeepWindow());
            if (Fleece > 0)
                Run("Fleece", FleeceTest());
            if (MenuTesting)
                Run("MenuTest", MenuTest());
            if (KnucklebonesTesting)
                Run("Knucklebones", KnucklebonesTest());
            if (AutoDungeon > 0f)
                Run("StrayCards", CloseStrayCards());
            if (AutoDungeon > 0f)
                Run("LostHud", ShootLostHud());
            if (AnimTest)
                Run("PlayAttacks", PlayAttacks());
            if (CultTest)
                Run("ChangeCult", ChangeCult());
            if (CampProbe)
                Run("WatchCamp", WatchCamp());
            if (DlcProbe)
                Run("WatchDlc", WatchDlc());
            if (LoadSlot >= 0)
                Run("LoadSlot", LoadOwnSlot());
            if (SaveLater > 0f)
                Run("SaveLater", SaveInCultLater());
            if (TempleTest)
                Run("TempleRun", TempleRun());
            if (SceneTest)
                Run("SceneTogether", SceneTogether());
            if (EndTest)
                Run("SceneEndTogether", SceneEndTogether());
            if (FilmTest)
                Run("FilmEndTogether", FilmEndTogether());
            if (RecruitTest)
                Run("RecruitTogether", RecruitTogether());
            if (BossTest)
                Run("FightLeader", FightLeader());
            if (PauseTest)
                Run("OpenPauseMenus", OpenPauseMenus());
            if (NextFloor && AutoEnter && !NoMapClick)
                Run("ClickMaps", ClickMaps());
            if (TarotTest)
                Run("DrawTarot", DrawTarot());
            if (AutoCut)
                Run("CutThings", CutThings());
            /* Back up after an update put in during a game: the lobby opens again, not a game */
            if ((AutoHost || AutoEnter) && !COTLMP.Network.HotUpdate.Restarted && !COTLMP.Game.Relobby.Marked)
                Plugin.MonoInstance.StartCoroutine(EnterCult(AutoHost));
            /* Past the title by itself for the tests that start in the main menu (others may have asked for it too) */
            PressStart.On |= COTLMP.Game.Lobby.AutoLobby != null || LobbyJoin || COTLMP.Network.Updater.TakeOffer || LoadSlot >= 0;
            PressStart.On &= !NoPress;
            if (COTLMP.Game.Lobby.AutoLobby != null)
                Run("AutoHost", COTLMP.Game.Lobby.AutoHost());
            if (LobbyJoin)
                Run("AutoJoin", COTLMP.Ui.OnlineMenu.AutoJoin());
            if (FoxTest)
                Run("MeetFox", MeetFox());
            if (COTLMP.Game.IntroCoop.SkipTo >= 0f)
                Run("AutoCrown", COTLMP.Game.IntroCoop.AutoCrown());
            if ((COTLMP.Game.Lobby.AutoLobby != null || LobbyJoin || COTLMP.Game.IntroCoop.SkipTo >= 0f) && !(AutoHost || AutoEnter))
                Run("AdvanceConversations", AdvanceConversations());
            if (AutoHost || AutoEnter)
                Run("AdvanceConversations", AdvanceConversations());
            if (AutoWalk)
                Run("Walk", Walk());
            if (Report)
                Run("ReportLoop", ReportLoop());
            if (QuitAfter > 0)
                Run("QuitLater", QuitLater());
            if (EndAfter > 0)
                Run("EndLater", EndLater());
            if (AutoDungeon > 0)
                Run("EnterDungeon", EnterDungeon());
            if (AutoKill)
                Run("HitEnemies", HitEnemies());
            /* The voting test takes its own frames */
            if (!string.IsNullOrEmpty(ShotsDir) && NetShots)
                Run("NetIconShots", NetIconShots());
            else if (!string.IsNullOrEmpty(ShotsDir) && AutoChoice <= 0)
                Run("Shots", Shots());
            if (!string.IsNullOrEmpty(ShotsDir))
                Run("MapShots", MapShots());
            if (!string.IsNullOrEmpty(ShotsDir))
                Run("DeathScreenShots", DeathScreenShots());
            if (AutoDie > 0)
                Run("DieOnce", DieOnce());
            /* -cotlmp-deathruns has its own falls (-cotlmp-deathat is only its floor then) */
            if (DeathAt > 0 && DeathRuns == 0)
                Run("DieTogether", DieTogether());
            if (DeathRuns > 0)
                Run("RunsInARow", RunsInARow());
            if (Invincible)
            {
                Guarded = true;
                Run("GuardLamb", GuardLamb());
            }
            if (ForcePull)
                Run("PullDuringWalk", PullDuringWalk());
            if (QuietState > 0f)
                MMBiomeGeneration.BiomeGenerator.OnRoomActive += () =>
                {
                    COTLMP.Network.Network.MuteStateUntil = Time.realtimeSinceStartup + QuietState;
                    Plugin.Logger?.LogWarning($"{Tag} Quiet for {QuietState:F0} s: our state isn't told");
                };
            if (AutoHurt > 0)
                Run("HurtTest", HurtTest());
            if (AutoTalk > 0)
                Run("TalkOnce", TalkOnce());
            if (ChatTest)
                Run("ChatTogether", ChatTogether());
            if (FinalTest)
                Run("FinalTogether", FinalTogether());
            if (LobbyClose >= 0f)
                Run("CloseLobbyLater", CloseLobbyLater());
            if (Sessions > 0)
                Run("SessionsInARow", SessionsInARow());
            if (AutoShoot)
                Run("Shoot", Shoot());
            if (DrawTest)
                Run("Draw", Draw());
            if (NetFlood)
                Run("Flood", Flood());
            if (AutoChoice > 0)
                Run("VoteOnChoices", VoteOnChoices());
            if (AutoKill || DieOnRevive || Rescuing)
                Run("Rescue", Rescue());
        }

        private static IEnumerator EnterCult(bool Host)
        {
            while (SceneManager.GetActiveScene().name != "Main Menu" || MMTransition.IsPlaying)
                yield return null;
            yield return new WaitForSecondsRealtime(3f);

            Plugin.Logger?.LogWarning($"{Tag} Entering the cult without a save");

            /* Whatever the game saves goes to a slot no menu shows (the save folder is the player's own) */
            SaveAndLoad.SAVE_SLOT = 6;
            AudioManager.Instance?.StopCurrentMusic();
            MMTransition.Play(MMTransition.TransitionType.ChangeRoomWaitToResume, MMTransition.Effect.BlackFade, "Base Biome 1", 2, "", null, null);

            while (PlayerFarming.Instance == null)
                yield return null;
            yield return new WaitForSecondsRealtime(2f);

            /*
             * Without a loaded save the game never unpauses the scene (BiomeBaseManager
             * resumes play only after loading one). The test copies have no saves, so
             * unpause by hand. Real players always join from their own loaded save.
             */
            MMTransition.CanResume = true;
            MMTransition.ResumePlay();
            Run("KeepTimeRunning", KeepTimeRunning());

            /*
             * A fresh save shows the dodge tutorial on the first hits and refuses the damage while
             * it does. Real saves are past it; mark the test copies the same way.
             */
            DataManager.Instance.ShownDodgeTutorial = true;
            DataManager.Instance.ShownDodgeTutorialCount = 3;

            /* The same for the difficulty a fresh save asks for on arrival (it covers the screen) */
            DataManager.Instance.DifficultyChosen = true;
            Run("SkipDifficulty", SkipDifficulty());
            /* As loading a save does (the friend's copy gets it with the host's save) */
            DifficultyManager.ForceDifficulty(DataManager.Instance.MetaData.Difficulty);
            if (RunCount >= 0)
            {
                DataManager.Instance.dungeonRun = RunCount;
                Plugin.Logger?.LogWarning($"{Tag} Our save has had {RunCount} runs");
            }
            yield return new WaitForSecondsRealtime(1f);

            if (Host)
            {
                bool Started = PauseMenuPatches.StartHosting();
                Plugin.Logger?.LogWarning($"[QA host] StartHosting returned {Started}, server port {PauseMenuPatches.Server?.Port}");

                if (SteamSelfTest)
                {
                    yield return new WaitForSecondsRealtime(3f);
                    COTLMP.Network.SteamBridge.SendSelfTest();
                }
            }
        }

        /// <summary>
        /// Something keeps a save-less scene at timeScale 0, so hold it at 1 for the test copies
        /// </summary>
        private static IEnumerator KeepTimeRunning()
        {
            while (true)
            {
                /* Leave the pause menu its pause, so it opens in the test copies too */
            /* (conversations are advanced by AdvanceConversations, nobody presses a button here) */
                /*
                 * Only in the save-less cult, where nothing would unpause it. Elsewhere a stopped clock is the
                 * game's or the mod's on purpose and the test must see it: a run's game over and its death
                 * screen, a scene's wait for the partner (lifting those woke the fallen behind their death screen)
                 */
                if (Time.timeScale == 0f && SceneManager.GetActiveScene().name == "Base Biome 1" &&
                    !Lamb.UI.UIMenuBase.ActiveMenus.Exists(Menu => Menu is Lamb.UI.PauseMenu.UIPauseMenuController) &&
                    Lamb.UI.DeathScreen.UIDeathScreenOverlayController.Instance == null && !COTLMP.Game.SceneEnds.Waiting)
                    Time.timeScale = 1f;
                yield return null;
            }
        }

        private static IEnumerator Walk()
        {
            Vector3 Center = Vector3.zero;
            PlayerFarming Walker = null;
            float Started = 0;

            while (true)
            {
                yield return null;

                if (!Plugin.GlobalsInternal.InGameSession || PlayerFarming.Instance == null || MMTransition.IsPlaying)
                {
                    Walker = null;
                    continue;
                }

                if (Walker != PlayerFarming.Instance)
                {
                    Walker = PlayerFarming.Instance;
                    Center = Walker.transform.position;
                    Started = Time.realtimeSinceStartup;
                    Plugin.Logger?.LogWarning($"{Tag} Walking a circle around {Center}");
                }

                /* One lap every 6 seconds, 2 units wide */
                float Angle = (Time.realtimeSinceStartup - Started) * Mathf.PI / 3f;
                Walker.transform.position = Center + new Vector3(Mathf.Cos(Angle), Mathf.Sin(Angle), 0f) * 2f;
            }
        }

        /// <summary>
        /// Picks the medium difficulty on the selector a fresh save shows when the player arrives, if it is up
        /// </summary>
        private static IEnumerator SkipDifficulty()
        {
            float Start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - Start < 20f)
            {
                var Selector = UnityEngine.Object.FindObjectOfType<Lamb.UI.UIDifficultySelectorOverlayController>();
                if (Selector != null)
                {
                    HarmonyLib.Traverse.Create(Selector).Method("SelectDifficulty", 1).GetValue();
                    Plugin.Logger?.LogWarning($"{Tag} Closed the difficulty selector");
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.5f);
            }
        }

        private static void Shot(string Name)
        {
            if (string.IsNullOrEmpty(ShotsDir))
                return;
            string File = System.IO.Path.Combine(ShotsDir, $"{(Plugin.GlobalsInternal.IsServerCreator ? "host" : "client")}-{Name}.png");
            System.IO.Directory.CreateDirectory(ShotsDir);
            ScreenCapture.CaptureScreenshot(File);
            Plugin.Logger?.LogWarning($"{Tag} Saved {File}, HUD {COTLMP.Game.SceneTrace.HudNow()}");
        }

        private static IEnumerator Until(Func<bool> Condition, float Seconds)
        {
            float Start = Time.realtimeSinceStartup;
            while (!Condition() && Time.realtimeSinceStartup - Start < Seconds)
                yield return null;
        }

        private static string Format(Vector3 Value)
        {
            return string.Format(CultureInfo.InvariantCulture, "({0:F2},{1:F2})", Value.x, Value.y);
        }

        private static IEnumerator EndLater()
        {
            /* A host counts from the moment a friend shows up */
            while (!Plugin.GlobalsInternal.InGameSession || (Plugin.GlobalsInternal.IsServerCreator && !AnyRemotePlayer()))
                yield return null;
            yield return new WaitForSecondsRealtime(EndAfter);

            bool Host = Plugin.GlobalsInternal.IsServerCreator;
            Plugin.Logger?.LogWarning($"{Tag} Ending the session as the player would");
            PauseMenuPatches.EndSession(Host ? "Multiplayer/Steam/Stopped" : "Multiplayer/Steam/Left");
            Plugin.Logger?.LogWarning($"[QA] Session ended, still in scene {SceneManager.GetActiveScene().name}");

            /* The joining copy comes back once, to show leaving doesn't break the next join */
            if (!Host && RejoinHost != 0)
            {
                yield return new WaitForSecondsRealtime(3f);
                Plugin.Logger?.LogWarning("[QA client] Joining again");
                COTLMP.Network.SteamBridge.QueueFakeRejoin(RejoinHost);
                RejoinHost = 0;
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr Window, IntPtr After, int X, int Y, int Width, int Height, uint Flags);

        /// <summary>
        /// Puts the game back in its small window whenever its settings made it bigger, and where it belongs on the desktop
        /// </summary>
        private static IEnumerator KeepWindow()
        {
            const uint NoSize = 0x1, NoZOrder = 0x4, NoActivate = 0x10;
            var Wait = new WaitForSecondsRealtime(2f);
            bool Placed = false;

            while (true)
            {
                if (Screen.fullScreenMode != FullScreenMode.Windowed || Screen.width != WindowRect[2] || Screen.height != WindowRect[3])
                {
                    Screen.SetResolution(WindowRect[2], WindowRect[3], FullScreenMode.Windowed);
                    Placed = false;
                    yield return null;
                }
                if (!Placed)
                {
                    IntPtr Window = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                    if (Window != IntPtr.Zero)
                        Placed = SetWindowPos(Window, IntPtr.Zero, WindowRect[0], WindowRect[1], 0, 0, NoSize | NoZOrder | NoActivate);
                }
                yield return Wait;
            }
        }

        /// <summary>
        /// Nobody presses a button in the test copies: move conversations on as a player would,
        /// picking the first answer when there is a choice
        /// </summary>
        private static IEnumerator AdvanceConversations()
        {
            var Next = HarmonyLib.AccessTools.Method(typeof(MMTools.MMConversation), "ShowNextLine");
            var Answer = HarmonyLib.AccessTools.Method(typeof(MMTools.MMConversation), "OnGiveAnswer");
            var Wait = new WaitForSecondsRealtime(2f);

            while (true)
            {
                yield return Wait;
                var Conversation = MMTools.MMConversation.mmConversation;
                if (!MMTools.MMConversation.isPlaying || Conversation == null || MMTools.MMConversation.isBark)
                    continue;
                /* The scene test reads on as a player does (the button), never past the mod's hold */
                if (SceneTest)
                    continue;
                try
                {
                    var Current = MMTools.MMConversation.CURRENT_CONVERSATION;
                    bool Last = Current?.Entries != null && MMTools.MMConversation.Position >= Current.Entries.Count - 1;
                    if (Last && Current.Responses != null && Current.Responses.Count > 0)
                    {
                        /* The voting test answers on its own */
                        if (AutoChoice > 0)
                            continue;
                        Answer.Invoke(Conversation, new object[] { 0 });
                    }
                    else
                        Next.Invoke(Conversation, new object[] { true });
                    Plugin.Logger?.LogWarning($"{Tag} Advanced a conversation to line {MMTools.MMConversation.Position}");
                }
                catch (Exception e)
                {
                    Plugin.Logger?.LogWarning($"{Tag} Advancing a conversation failed: {e.Message}");
                }
            }
        }

        private static int CountVisibleEnemies()
        {
            int Count = 0;
            foreach (Health Enemy in Health.team2)
            {
                if (Enemy == null || Enemy.HP <= 0f || !Enemy.gameObject.activeInHierarchy || Enemy.GetComponent<UnitObject>() == null)
                    continue;
                var Spine = Enemy.GetComponentInChildren<Spine.Unity.SkeletonAnimation>();
                var Renderer = Spine != null ? Spine.GetComponent<MeshRenderer>() : null;
                if (Renderer == null || Renderer.enabled)
                    ++Count;
            }
            return Count;
        }

        private static bool AnyRemotePlayer()
        {
            for (uint Id = 0; Id < InternalData.MaxPlayersPerServerInternal; Id++)
            {
                if (PlayerManager.DoesPlayerExist(Id))
                    return true;
            }
            return false;
        }

        private static IEnumerator QuitLater()
        {
            yield return new WaitForSecondsRealtime(QuitAfter);
            Plugin.Logger?.LogWarning("[QA] Quitting after the test time limit");
            Application.Quit();
        }

    }
}

/* EOF */
