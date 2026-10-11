#if VCONTAINER_UNITASK_INTEGRATION
using Awaitable = Cysharp.Threading.Tasks.UniTask;
#elif UNITY_2023_1_OR_NEWER
using Awaitable = UnityEngine.Awaitable;
#else
using Awaitable = System.Threading.Tasks.Task;
#endif
using reromanlee.ReactiveLocalizer.Samples.QuickStart;
using System.Threading;
using VContainer.Unity;

namespace reromanlee.ReactiveLocalizer.Samples.VContainer
{
    /// <summary>
    /// Initializes the registered localizer as the container starts, then switches it to the player's saved language.
    /// Initialization never fails: a catalog that can't be loaded is reported, and the localizer stays uninitialized.
    /// </summary>
    /// <remarks>
    /// VContainer's async entry points return UniTask when UniTask is installed, Unity's Awaitable on Unity 2023.1 and
    /// newer, and Task before that; this one returns whichever it expects.
    /// </remarks>
    internal sealed class LocalizerStartup : IAsyncStartable
    {
        private readonly Localizer _localizer;

        public LocalizerStartup(Localizer localizer)
        {
            _localizer = localizer;
        }

        public async Awaitable StartAsync(CancellationToken cancellation = default)
        {
            await _localizer.InitializeAsync();
            if (!cancellation.IsCancellationRequested)
            {
                GlobalLocalizer.StartInPreferredLanguage(_localizer);
            }
        }
    }
}
