using reromanlee.ReactiveLocalizer.Unity;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Samples.QuickStart
{
    /// <summary>
    /// The project-wide access point to localizers, for projects that don't use a DI container: one localizer per
    /// catalog, created on first use. It starts in the language the player chose last time, else in the OS language
    /// when the catalog has it, and remembers every switch the player makes.
    /// </summary>
    /// <remarks>
    /// Main thread only. A DI setup hands it the instances it created with <see cref="Use"/>, before anything asks
    /// for one, so components built on this access point use those instead.
    /// </remarks>
    public static class GlobalLocalizer
    {
        private const string SavedLanguagePrefix = "reromanlee.ReactiveLocalizer.Language.";

        private static readonly Dictionary<ulong, ILocalizer> Localizers = new();
        private static readonly List<Localizer> Created = new();

        /// <summary>
        /// The catalog <see cref="Instance"/> and references without a catalog mean: the package's default catalog,
        /// unless set to another before first use.
        /// </summary>
        public static CatalogKey DefaultCatalog { get; set; } = new("Localization");

        /// <summary>The localizer of <see cref="DefaultCatalog"/>.</summary>
        public static ILocalizer Instance => For(DefaultCatalog);

        /// <summary>
        /// Returns the localizer of <paramref name="catalog"/>, creating and initializing it on first use. An empty key
        /// means <see cref="DefaultCatalog"/>. Outside Play Mode it returns the editor's preview localizer, which is null
        /// when the project has no such catalog, or before the editor's preview starts.
        /// </summary>
        public static ILocalizer For(CatalogKey catalog)
        {
            CatalogKey key = catalog.IsEmpty ? DefaultCatalog : catalog;
            // Edit Mode never gets a localizer of its own, which would outlive it.
            if (!Application.isPlaying)
            {
                return EditModePreview.For(key);
            }
            if (Localizers.TryGetValue(key.Hash, out ILocalizer existing))
            {
                return existing;
            }
            Localizer localizer = new(key, new UnityHost());
            Localizers.Add(key.Hash, localizer);
            Created.Add(localizer);
            Task initialization = localizer.InitializeAsync();
            // Embedded tables load within the call; any other source finishes later, on the main thread.
            if (initialization.IsCompleted)
            {
                StartInPreferredLanguage(localizer);
            }
            else
            {
                initialization.ContinueWith(_ => StartInPreferredLanguage(localizer), TaskScheduler.FromCurrentSynchronizationContext());
            }
            return localizer;
        }

        /// <summary>Makes <paramref name="localizer"/> the one handed out for its catalog, instead of creating one here.</summary>
        public static void Use(CatalogKey catalog, ILocalizer localizer)
        {
            Localizers[catalog.Hash] = localizer;
        }

        /// <summary>Switches every localizer handed out so far whose catalog has <paramref name="language"/>.</summary>
        public static void SetLanguage(LanguageKey language)
        {
            foreach (ILocalizer localizer in Localizers.Values)
            {
                if (TryFind(localizer.Languages, language.Name, out LanguageKey found))
                {
                    localizer.SetLanguageAsync(found);
                }
            }
        }

        private static void StartInPreferredLanguage(Localizer localizer)
        {
            if (!localizer.IsInitialized)
            {
                return;
            }
            string savedName = PlayerPrefs.GetString(SavedLanguagePrefix + localizer.CatalogKey.Name, string.Empty);
            if (TryFind(localizer.Languages, savedName, out LanguageKey saved))
            {
                localizer.SetLanguageAsync(saved);
            }
            else if (SystemLanguageMapping.TryMap(localizer.Languages, out LanguageKey system))
            {
                localizer.SetLanguageAsync(system);
            }
            // Subscribed only now, so the starting language itself is never taken for a choice the player made.
            localizer.LanguageChanged += language => PlayerPrefs.SetString(SavedLanguagePrefix + localizer.CatalogKey.Name, language.Name);
        }

        /// <summary>Finds the language of <paramref name="languages"/> named <paramref name="name"/>, ignoring case.</summary>
        internal static bool TryFind(IReadOnlyList<LanguageInfo> languages, string name, out LanguageKey language)
        {
            for (int i = 0; i < languages.Count; i++)
            {
                if (string.Equals(languages[i].Name, name, System.StringComparison.OrdinalIgnoreCase))
                {
                    language = languages[i].Key;
                    return true;
                }
            }
            language = default;
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlayMode()
        {
            // With domain reload disabled, statics survive between Play Mode sessions; each session starts clean.
            for (int i = 0; i < Created.Count; i++)
            {
                Created[i].Dispose();
            }
            Created.Clear();
            Localizers.Clear();
        }
    }
}
