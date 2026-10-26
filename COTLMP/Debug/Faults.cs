/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Counts what went wrong in the mod, so a caught error doesn't go unseen
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Debug
{
    /// <summary>
    /// What went wrong, counted.
    /// </summary>
    /// <remarks>
    /// The mod catches errors so that one bad frame doesn't end a session, and logs them. Here they are
    /// counted by kind: every warning and error the mod logs, the exceptions the game's log shows with
    /// the mod's code in their stack (from its patches, which nothing of ours catches), and the quiet
    /// fallbacks that note themselves (Note). The test harness report has the counts, and the log gets
    /// the most frequent kinds when a session ends and when the game quits.
    /// </remarks>
    internal static class Faults
    {
        internal static int Warnings, Errors, Unhandled, Noted;

        private static readonly Dictionary<string, int> Kinds = new();
        private static readonly object Lock = new();
        private static readonly Regex Numbers = new(@"\d+", RegexOptions.Compiled);

        [ThreadStatic]
        private static bool Inside;

        public static void Initialize()
        {
            BepInEx.Logging.Logger.Listeners.Add(new Listener());
            Application.logMessageReceivedThreaded += OnUnityLog;
            COTLMP.Network.Network.OnDisconnect += () => Summary("session");
            Application.quitting += () => Summary("game");
        }

        /// <summary>
        /// The mod's old listener goes with it (an update in the running game)
        /// </summary>
        internal static void Release()
        {
            foreach (ILogListener Item in BepInEx.Logging.Logger.Listeners.Where(Item => Item is Listener).ToList())
                BepInEx.Logging.Logger.Listeners.Remove(Item);
            Application.logMessageReceivedThreaded -= OnUnityLog;
        }

        /// <summary>
        /// A quiet fallback that may hide a real error: counted, the first few of each place logged
        /// </summary>
        internal static void Note(string Where, Exception Error)
        {
            int Seen;
            lock (Lock)
            {
                ++Noted;
                Kinds.TryGetValue("note " + Where, out Seen);
                Kinds["note " + Where] = ++Seen;
            }
            if (Seen <= 3)
                Plugin.Logger?.LogInfo($"[COOP] {Where} fell back: {Error?.GetType().Name} {Error?.Message}");
        }

        private static void Count(string Kind)
        {
            lock (Lock)
            {
                Kinds.TryGetValue(Kind, out int Seen);
                Kinds[Kind] = Seen + 1;
            }
        }

        /* A kind: the start of the line, numbers out, so one error with other IDs is one kind */
        private static string KindOf(string Line)
        {
            Line = Numbers.Replace(Line ?? "", "#");
            int End = Line.IndexOfAny(new[] { '\n', '\r' });
            if (End >= 0)
                Line = Line.Substring(0, End);
            return Line.Length > 90 ? Line.Substring(0, 90) : Line;
        }

        private static void OnUnityLog(string Condition, string Stack, LogType Type)
        {
            if (Type != LogType.Exception || Stack == null || Stack.IndexOf("COTLMP", StringComparison.Ordinal) < 0 || Inside)
                return;
            Inside = true;
            try
            {
                int Seen;
                string Kind = "unhandled " + KindOf(Condition);
                lock (Lock)
                {
                    ++Unhandled;
                    Kinds.TryGetValue(Kind, out Seen);
                    Kinds[Kind] = ++Seen;
                }
                /* The game's log has it, ours too (once a kind), with where in the mod it came from */
                if (Seen == 1)
                {
                    string Where = Stack.Split('\n').FirstOrDefault(Line => Line.Contains("COTLMP"))?.Trim();
                    Plugin.Logger?.LogError($"[COOP] Unhandled in the mod: {Condition} at {Where}");
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                Inside = false;
            }
        }

        private static void Summary(string Of)
        {
            List<KeyValuePair<string, int>> Top;
            lock (Lock)
                Top = Kinds.OrderByDescending(Pair => Pair.Value).Take(6).ToList();
            if (Top.Count == 0)
                return;
            Plugin.Logger?.LogInfo($"[COOP] Faults so far ({Of} end): {Warnings} warnings, {Errors} errors, {Unhandled} unhandled, {Noted} fallbacks; most: " +
                                   string.Join("; ", Top.Select(Pair => $"{Pair.Value}x {Pair.Key}")));
        }

        private sealed class Listener : ILogListener
        {
            public void LogEvent(object Sender, LogEventArgs Event)
            {
                if (Event.Source != Plugin.Logger || Inside)
                    return;
                string Line = Event.Data?.ToString() ?? "";
                /* The test harness's own reports and frames are warnings by choice */
                if (Line.StartsWith("[QA", StringComparison.Ordinal))
                    return;
                if ((Event.Level & (LogLevel.Error | LogLevel.Fatal)) != 0)
                    System.Threading.Interlocked.Increment(ref Errors);
                else if ((Event.Level & LogLevel.Warning) != 0)
                    System.Threading.Interlocked.Increment(ref Warnings);
                else
                    return;
                Count(KindOf(Line));
            }

            public void Dispose()
            {
            }
        }
    }
}

/* EOF */
