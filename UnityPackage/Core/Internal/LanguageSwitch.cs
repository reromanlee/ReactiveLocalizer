using System.Collections.Generic;
using System.Threading.Tasks;

namespace reromanlee.ReactiveLocalizer.Internal
{
    /// <summary>
    /// A language switch while its tables load. It is applied once nothing is outstanding, unless a newer switch
    /// replaced it first; then the newer one takes over everyone waiting for it.
    /// </summary>
    /// <remarks>Only ever touched on the host thread.</remarks>
    internal sealed class LanguageSwitch
    {
        public LanguageSwitch(LanguageInfo target, IReadOnlyList<LanguageInfo> chain)
        {
            Target = target;
            Chain = chain;
        }

        public LanguageInfo Target { get; }

        public IReadOnlyList<LanguageInfo> Chain { get; }

        /// <summary>The loads of the switch, one per table, by table hash.</summary>
        public Dictionary<ulong, ChainLoad> Loads { get; } = new();

        /// <summary>Tasks to complete once the switch is applied, or once it is given up.</summary>
        public List<TaskCompletionSource<bool>> Waiters { get; } = new();

        /// <summary>Loads not yet done, plus one while loads are still being started.</summary>
        public int Outstanding { get; set; }

        /// <summary>Whether a newer switch replaced this one, so its loads are no longer awaited.</summary>
        public bool IsSuperseded { get; private set; }

        /// <summary>Gives the switch up: its loads stop, and nothing it loaded is applied.</summary>
        public void Supersede()
        {
            IsSuperseded = true;
            foreach (ChainLoad load in Loads.Values)
            {
                load.Cancel();
            }
        }
    }
}
