using reromanlee.ReactiveLocalizer.Unity;
using System.Collections.Generic;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Tests
{
    /// <summary>An asset holding entry references, as a project's data would, for the tests of reference scanning.</summary>
    public sealed class EntryHolder : ScriptableObject
    {
        public EntryReference Entry;
        public List<EntryReference> Entries = new();
        public float[] Weights = { 1f, 2f };
    }
}
