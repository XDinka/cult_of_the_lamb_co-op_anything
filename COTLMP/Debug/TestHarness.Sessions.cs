/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     The test harness: sessions one after another in one running game, and what each leaves behind
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP.Game;
using COTLMP.Ui;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Debug
{
    internal static partial class TestHarness
    {
        private static int Sessions;
        private static float SessionLength = 60f;
        private static Dictionary<string, string> Baseline;

        /// <summary>
        /// -cotlmp-sessions=N (-cotlmp-sessionlen=S): N sessions in one running game. The host stops hosting S s after the
        /// friend came and opens online again from where it stands; the friend, back in the main menu, joins again.
        /// After each session both log what of the mod's state is still set that wasn't before the first one.
        /// </summary>
        private static IEnumerator SessionsInARow()
        {
            /* What is set before any session: the drivers, caches and settings every game has */
            yield return new WaitForSecondsRealtime(5f);
            Baseline = Snapshot();
            bool Host = AutoHost;
            for (int Number = 1; Number <= Sessions; Number++)
            {
                yield return Until(() => Plugin.GlobalsInternal.InGameSession && AnyRemotePlayer() && PlayerFarming.Instance != null, 400f);
                Plugin.Logger?.LogWarning($"{Tag} Session {Number} of {Sessions} is on in {CoopSync.CurrentPlace()}");
                if (Host)
                {
                    yield return new WaitForSecondsRealtime(SessionLength);
                    Plugin.Logger?.LogWarning($"{Tag} Session {Number}: stopping hosting as the player would");
                    PauseMenuPatches.EndSession("Multiplayer/Steam/Stopped");
                }
                else
                {
                    yield return Until(() => !Plugin.GlobalsInternal.InGameSession, SessionLength + 120f);
                    /* Back in the main menu with the host's save gone */
                    yield return Until(() => SceneManager.GetActiveScene().name == "Main Menu" && !MMTools.MMTransition.IsPlaying, 60f);
                }
                yield return new WaitForSecondsRealtime(4f);
                Leftovers(Number);
                if (Number == Sessions)
                    break;
                if (Host)
                {
                    yield return new WaitForSecondsRealtime(6f);
                    Plugin.Logger?.LogWarning($"{Tag} Session {Number + 1}: opening online again in {SceneManager.GetActiveScene().name}");
                    PauseMenuPatches.StartHosting();
                }
                else
                {
                    yield return new WaitForSecondsRealtime(10f);
                    Plugin.Logger?.LogWarning($"{Tag} Session {Number + 1}: joining the host again");
                    COTLMP.Network.SteamBridge.QueueFakeRejoin(1);
                }
            }
            Plugin.Logger?.LogWarning($"{Tag} Sessions done");
        }

        private static void Leftovers(int Number)
        {
            Dictionary<string, string> Now = Snapshot();
            List<string> Left = Now.Where(Pair => !Baseline.TryGetValue(Pair.Key, out string Was) || Was != Pair.Value)
                                   .Select(Pair => $"{Pair.Key} = {Pair.Value}").OrderBy(Line => Line, StringComparer.Ordinal).ToList();
            Plugin.Logger?.LogWarning($"{Tag} After session {Number}: {Left.Count} things of the mod still set that weren't before the first");
            foreach (string Line in Left)
                Plugin.Logger?.LogWarning($"{Tag}   left: {Line}");
        }

        /*
         * Every static field of the mod's game, network and UI types that holds something: a true flag, a state that isn't
         * the first, text, a collection with items, an object. Numbers are left out (counters and clocks).
         */
        private static Dictionary<string, string> Snapshot()
        {
            Dictionary<string, string> Found = new();
            foreach (Type Item in typeof(TestHarness).Assembly.GetTypes())
            {
                string Space = Item.Namespace ?? "";
                if (!(Space.StartsWith("COTLMP.Game") || Space.StartsWith("COTLMP.Network") || Space.StartsWith("COTLMP.Ui")) ||
                    Item.IsGenericTypeDefinition || Item.Name.Contains("<"))
                    continue;
                foreach (FieldInfo Field in Item.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (Field.IsLiteral || Field.Name.Contains("<") || typeof(Delegate).IsAssignableFrom(Field.FieldType))
                        continue;
                    string Shown;
                    try
                    {
                        Shown = Describe(Field.GetValue(null));
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                    if (Shown != null)
                        Found[$"{Item.FullName.Replace("COTLMP.", "")}.{Field.Name}"] = Shown;
                }
            }
            return Found;
        }

        private static string Describe(object Value)
        {
            switch (Value)
            {
                case null:
                    return null;
                case bool Flag:
                    return Flag ? "true" : null;
                case string Text:
                    return Text.Length == 0 ? null : (Text.Length > 60 ? Text.Substring(0, 60) + "…" : Text);
                case Enum State:
                    return Convert.ToInt64(State) == 0 ? null : State.ToString();
                case UnityEngine.Object Thing:
                    return Thing == null ? null : $"{Thing.GetType().Name} {Thing.name}";
                case ICollection Items:
                    return Items.Count == 0 ? null : $"{Items.Count} items";
                case IConvertible:
                    return null;
                case Vector2 or Vector3 or Vector2Int or Color or Quaternion or Rect:
                    return null;
                default:
                    if (Value.GetType().IsValueType)
                        return null;
                    return Value.GetType().Name;
            }
        }
    }
}

/* EOF */
