using System;
using System.Collections.Concurrent;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace reromanlee.ReactiveLocalizer.Unity
{
    /// <summary>
    /// Runs the updates localizers schedule from any thread, on Unity's main thread: from the player loop in Play
    /// Mode and builds, and from the editor's update loop in Edit Mode. While nothing is scheduled, a frame costs one
    /// check of an empty queue.
    /// </summary>
    /// <remarks>
    /// It runs at the start of <see cref="PreLateUpdate"/>, so text changed by an update reaches UI components before
    /// they rebuild for rendering in the same frame.
    /// </remarks>
    internal static class FramePump
    {
        private static readonly ConcurrentQueue<Action> Updates = new();

        /// <summary>Queues <paramref name="update"/> for the next run. Safe from any thread.</summary>
        public static void Schedule(Action update)
        {
            Updates.Enqueue(update);
        }

        /// <summary>Runs every update queued so far. Main thread only.</summary>
        public static void Run()
        {
            while (Updates.TryDequeue(out Action update))
            {
                try
                {
                    update();
                }
                catch (Exception exception)
                {
                    // One failing update must not keep the others from running.
                    Debug.LogException(exception);
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void InstallInPlayerLoop()
        {
            // With domain reload disabled, statics survive between Play Mode sessions; updates of the last one are stale.
            while (Updates.TryDequeue(out _))
            {
                // Each dequeue drops one stale update.
            }
            PlayerLoopSystem root = PlayerLoop.GetCurrentPlayerLoop();
            if (TryInsert(ref root))
            {
                PlayerLoop.SetPlayerLoop(root);
            }
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void InstallInEditor()
        {
            UnityEditor.EditorApplication.update -= Run;
            UnityEditor.EditorApplication.update += Run;
        }
#endif

        private static bool TryInsert(ref PlayerLoopSystem root)
        {
            PlayerLoopSystem[] phases = root.subSystemList;
            if (phases == null)
            {
                return false;
            }
            for (int i = 0; i < phases.Length; i++)
            {
                if (phases[i].type != typeof(PreLateUpdate))
                {
                    continue;
                }
                PlayerLoopSystem[] systems = phases[i].subSystemList ?? Array.Empty<PlayerLoopSystem>();
                for (int s = 0; s < systems.Length; s++)
                {
                    // Already installed: the player loop outlived the last domain, as with domain reload disabled.
                    if (systems[s].type == typeof(FramePumpUpdate))
                    {
                        return false;
                    }
                }
                PlayerLoopSystem[] extended = new PlayerLoopSystem[systems.Length + 1];
                extended[0] = new PlayerLoopSystem { type = typeof(FramePumpUpdate), updateDelegate = Run };
                Array.Copy(systems, 0, extended, 1, systems.Length);
                phases[i].subSystemList = extended;
                return true;
            }
            return false;
        }

        /// <summary>Marks the pump's entry in the player loop.</summary>
        private struct FramePumpUpdate
        {
        }
    }
}
