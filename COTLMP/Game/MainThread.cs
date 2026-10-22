/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Runs work from network threads on the Unity main thread
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using System;
using System.Collections;
using System.Collections.Concurrent;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Game
{
    /// <summary>
    /// Queues actions from any thread and runs them on the Unity main thread.
    /// </summary>
    /// <remarks>
    /// Server events (ServerStopped) fire on the server task, and Unity objects
    /// must not be touched from there.
    /// </remarks>
    internal static class MainThread
    {
        private static readonly ConcurrentQueue<Action> Pending = new();

        /// <summary>
        /// Starts the coroutine that drains the queue.
        /// </summary>
        public static void Initialize()
        {
            Plugin.MonoInstance.StartCoroutine(Drain());
        }

        /// <summary>
        /// Runs an action on the main thread during the next frame.
        /// </summary>
        /// <param name = "Work">The action to run.</param>
        public static void Post(Action Work)
        {
            Pending.Enqueue(Work);
        }

        private static IEnumerator Drain()
        {
            while (true)
            {
                while (Pending.TryDequeue(out Action Work))
                {
                    try
                    {
                        Work();
                    }
                    catch (Exception e)
                    {
                        Plugin.Logger?.LogError($"Main thread work failed: {e}");
                    }
                }
                yield return null;
            }
        }
    }
}

/* EOF */
