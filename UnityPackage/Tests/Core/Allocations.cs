using NUnit.Framework;
using System;
#if UNITY_2022_3_OR_NEWER
using UnityEngine.TestTools.Constraints;
using AllocationIs = UnityEngine.TestTools.Constraints.Is;
#endif

namespace reromanlee.ReactiveLocalizer.Tests
{
    /// <summary>
    /// Asserts that code allocates nothing on the managed heap: with Unity's constraint inside Unity, and with the
    /// runtime's allocation counter under plain .NET.
    /// </summary>
    internal static class Allocations
    {
        /// <summary>Runs <paramref name="action"/> once to warm it up, then asserts that running it again allocates nothing.</summary>
        public static void AssertNone(Action action)
        {
            action();
#if UNITY_2022_3_OR_NEWER
            Assert.That(() => action(), AllocationIs.Not.AllocatingGCMemory());
#else
            long before = GC.GetAllocatedBytesForCurrentThread();
            action();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero, "Bytes allocated");
#endif
        }
    }
}
