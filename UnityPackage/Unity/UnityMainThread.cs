using System.Threading;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Unity
{
    /// <summary>
    /// Knows which thread is Unity's main thread: the only one allowed to touch Unity objects, and the one every
    /// localizer under <see cref="UnityHost"/> applies its changes on.
    /// </summary>
    internal static class UnityMainThread
    {
        private static int _threadId = -1;

        /// <summary>Whether the calling thread is Unity's main thread.</summary>
        public static bool IsCurrent => Thread.CurrentThread.ManagedThreadId == Volatile.Read(ref _threadId);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void CaptureOnPlay()
        {
            Capture();
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void CaptureOnLoad()
        {
            Capture();
        }
#endif

        private static void Capture()
        {
            Volatile.Write(ref _threadId, Thread.CurrentThread.ManagedThreadId);
        }
    }
}
