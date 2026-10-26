/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     The test harness: frames of the game, the pause menu and the HUD trace
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
    internal static partial class TestHarness
    {
        /// <summary>
        /// Captures only this game's frame (Unity's ScreenCapture), never the desktop
        /// </summary>
        private static IEnumerator Shots()
        {
            string Last = null;
            int Count = 0;

            while (Count < 6)
            {
                yield return new WaitForSecondsRealtime(1f);
                string Place = COTLMP.Game.CoopSync.CurrentPlace();
                if (Place == null || Place == Last || !Place.Contains("#") || !AnyRemotePlayer())
                    continue;

                Last = Place;
                yield return new WaitForSecondsRealtime(5f);
                string File = System.IO.Path.Combine(ShotsDir, $"{(Plugin.GlobalsInternal.IsServerCreator ? "host" : "client")}-{++Count}.png");
                System.IO.Directory.CreateDirectory(ShotsDir);
                ScreenCapture.CaptureScreenshot(File);
                Plugin.Logger?.LogWarning($"{Tag} Saved {File} in {Place}");
            }
        }

        /// <summary>
        /// One frame for each kind of connection icon, once it has been up for a second.
        /// </summary>
        private static IEnumerator NetIconShots()
        {
            HashSet<string> Taken = new();
            string Seen = null;
            float Since = 0f;

            while (Taken.Count < 6)
            {
                yield return new WaitForSecondsRealtime(0.25f);
                float Ping = COTLMP.Game.ConnectionQuality.PingMs, Loss = COTLMP.Game.ConnectionQuality.Loss;
                string State = COTLMP.Game.ConnectionQuality.Broken ? "broken" :
                               (Ping >= COTLMP.Game.ConnectionQuality.RedPing ? "pingred" : Ping >= COTLMP.Game.ConnectionQuality.YellowPing ? "pingyellow" : "pingok") +
                               (Loss >= COTLMP.Game.ConnectionQuality.RedLoss ? "-lossred" : Loss >= COTLMP.Game.ConnectionQuality.YellowLoss ? "-lossyellow" : "");
                if (State != Seen)
                {
                    Seen = State;
                    Since = Time.realtimeSinceStartup;
                    continue;
                }
                if (Taken.Contains(State) || Time.realtimeSinceStartup - Since < 1f || !COTLMP.Game.CoopSync.InSession)
                    continue;

                Taken.Add(State);
                string File = System.IO.Path.Combine(ShotsDir, $"{(Plugin.GlobalsInternal.IsServerCreator ? "host" : "client")}-net-{State}.png");
                ScreenCapture.CaptureScreenshot(File);
                Plugin.Logger?.LogWarning($"{Tag} Saved {File} (ping {Ping:F0} ms, loss {Loss:P0})");
            }
        }

        /// <summary>
        /// Opens the pause menu over and over and logs whether it stays up
        /// </summary>
        private static IEnumerator OpenPauseMenus()
        {
            int Count = 0;
            while (true)
            {
                yield return new WaitForSecondsRealtime(8f);
                Lamb.UI.UIManager UI = MonoSingleton<Lamb.UI.UIManager>.Instance;
                if (PlayerFarming.Instance == null || MMTransition.IsPlaying || UI == null || UI.IsPaused || UI.MenusBlocked)
                    continue;
                ++Count;

                /* As in the game's opening, before the crown: no lamb in the player list */
                List<PlayerFarming> Hidden = null;
                if (PauseNoPlayer)
                {
                    Hidden = new List<PlayerFarming>(PlayerFarming.players);
                    PlayerFarming.players.Clear();
                    PlayerFarming.playersCount = 0;
                }
                try
                {
                    UI.ShowPauseMenu(PauseNoPlayer ? null : PlayerFarming.Instance);
                }
                catch (Exception Error)
                {
                    Plugin.Logger?.LogError($"{Tag} Pause {Count} threw: {Error}");
                }
                if (Hidden != null)
                {
                    yield return new WaitForSecondsRealtime(0.5f);
                    PlayerFarming.players.AddRange(Hidden);
                    PlayerFarming.playersCount = PlayerFarming.players.Count;
                }

                yield return new WaitForSecondsRealtime(2f);
                var Menu = Lamb.UI.UIMenuBase.ActiveMenus.Find(Item => Item is Lamb.UI.PauseMenu.UIPauseMenuController);
                Plugin.Logger?.LogWarning($"{Tag} Pause {Count} after 2 s: {(Menu != null && !Menu.IsHiding ? "open" : "CLOSED")}, timeScale {Time.timeScale}");
                Shot($"pause-{(Plugin.GlobalsInternal.IsServerCreator ? "host" : "client")}-{Count}");
                Menu?.OnCancelButtonInput();
            }
        }

        /// <summary>
        /// Both sides of the cult test, by role
        /// </summary>
        /// <summary>
        /// A frame of every map between floors, with both crowns on it
        /// </summary>
        private static IEnumerator MapShots()
        {
            for (int Count = 1; Count <= 4; Count++)
            {
                while (COTLMP.Game.MapSync.Shown == null)
                    yield return new WaitForSecondsRealtime(0.2f);
                yield return new WaitForSecondsRealtime(1.6f);
                Plugin.Logger?.LogWarning($"{Tag} Map {Count}: partner {COTLMP.Game.MapCrowns.PartnerState}, crowns shown {COTLMP.Game.MapCrowns.Shown}, sent {COTLMP.Game.MapCrowns.Sent}, got {COTLMP.Game.MapCrowns.Received}");
                Shot($"map-{Count}");
                while (COTLMP.Game.MapSync.Shown != null)
                    yield return new WaitForSecondsRealtime(0.2f);
            }
        }

        /* THE HUD TRACE ******************************************************/

        private static void TraceHud()
        {
            var Patcher = new Harmony("cotlmp.hudtrace");
            foreach (string Name in new[] { "Hide", "Show" })
                Patcher.Patch(AccessTools.Method(typeof(HUD_Manager), Name), prefix: new HarmonyMethod(typeof(TestHarness), nameof(HudCalled)));
            Patcher.Patch(AccessTools.Method(typeof(Health_Manager), nameof(Health_Manager.Init)), prefix: new HarmonyMethod(typeof(TestHarness), nameof(HudCalled)));
            foreach (string Name in new[] { "Hide", "HideBars", "Show" })
                Patcher.Patch(AccessTools.Method(typeof(Health_Manager), Name), prefix: new HarmonyMethod(typeof(TestHarness), nameof(HudCalled)));
            foreach (string Name in new[] { "MoveBackOutFunction", "hideBar" })
                Patcher.Patch(AccessTools.Method(typeof(UI_Transitions), Name), prefix: new HarmonyMethod(typeof(TestHarness), nameof(HudTransition)));
            Run("LogHud", LogHud());
        }

        /* Only the hearts panels' own moves */
        private static void HudTransition(UI_Transitions __instance, MethodBase __originalMethod)
        {
            if (__instance == null || __instance.GetComponentInParent<Health_Manager>() == null)
                return;
            string Stack = string.Join(" < ", new System.Diagnostics.StackTrace(2, false).GetFrames()?.Take(6)
                .Select(Frame => $"{Frame.GetMethod()?.DeclaringType?.Name}.{Frame.GetMethod()?.Name}") ?? Enumerable.Empty<string>());
            Plugin.Logger?.LogWarning($"{Tag} HUD panel {__instance.name} {__originalMethod.Name}: {Stack}");
        }

        private static IEnumerator LogHud()
        {
            string Last = null;
            while (true)
            {
                yield return new WaitForSecondsRealtime(3f);
                HUD_Manager Hud = HUD_Manager.Instance;
                if (Hud == null)
                    continue;
                Health_Manager Health = Hud.healthManager;
                string Hearts(HUD_Hearts Panel) => Panel == null ? "-" :
                    $"{(Panel.gameObject.activeInHierarchy ? "on" : "off")}:{(Traverse.Create(Panel).Field("playerFarming").GetValue() is PlayerFarming Bound && Bound != null ? (Bound == PlayerFarming.Instance ? "us" : PlayerManager.IsPuppet(Bound.health) ? "puppet" : "other") : "none")}";
                CanvasGroup Group = Hud.GetComponentInParent<CanvasGroup>();
                string State = $"hidden={Hud.Hidden} active={Hud.gameObject.activeInHierarchy} alpha={(Group != null ? Group.alpha : -1f):F2} container={Hud.healthContainer?.activeInHierarchy} " +
                               $"singleDungeon={Hearts(Health?.health_SingleDungeon)} singleBase={Hearts(Health?.health_SingleBase)} first={Hearts(Health?.health_firstPlayer)} second={Hearts(Health?.health_secondPlayer)} players={PlayerFarming.players.Count}";
                if (State != Last)
                    Plugin.Logger?.LogWarning($"{Tag} HUD state in {SceneManager.GetActiveScene().name}: {State}");
                Last = State;
            }
        }

        private static void HudCalled(MethodBase __originalMethod)
        {
            string Stack = string.Join(" < ", new System.Diagnostics.StackTrace(2, false).GetFrames()?.Take(6)
                .Select(Frame => $"{Frame.GetMethod()?.DeclaringType?.Name}.{Frame.GetMethod()?.Name}") ?? Enumerable.Empty<string>());
            Plugin.Logger?.LogWarning($"{Tag} HUD {__originalMethod.Name} in {SceneManager.GetActiveScene().name}: {Stack}");
        }
    }
}

/* EOF */
