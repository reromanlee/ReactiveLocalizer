using reromanlee.ReactiveLocalizer.Hosting;
using reromanlee.ReactiveLocalizer.Samples.QuickStart;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Samples.Mods
{
    /// <summary>
    /// Reads fan translations a player drops into the mods folder, <c>Mods/&lt;Catalog&gt;/</c> under
    /// <see cref="Application.persistentDataPath"/>, for every localizer <see cref="GlobalLocalizer"/> creates.
    /// </summary>
    /// <remarks>
    /// The folder holds <c>.lang</c> files named like the game's, in any folders below it. A file for a language the
    /// game ships patches the entries it has. A new language is defined by a <c>.catalog</c> file with its section,
    /// such as <c>[Elvish]</c> with <c>Fallback = English</c>, and becomes a language like any other: language pickers
    /// list it, and the player's choice of it is remembered. Problems in a file go to the Console with its path and line.
    /// </remarks>
    public static class ModTranslations
    {
        private static readonly Dictionary<ulong, FolderTableSource> Sources = new();

        /// <summary>The folder mods go in, with one folder per catalog inside it.</summary>
        public static string Root => Path.Combine(Application.persistentDataPath, "Mods");

        /// <summary>
        /// Reads the mods folder again, as after a player added or removed a mod while playing, and registers the
        /// languages that are new. Text already shown changes with the next language switch.
        /// </summary>
        public static void Refresh()
        {
            foreach (FolderTableSource source in Sources.Values)
            {
                source.Refresh();
                ILocalizer localizer = GlobalLocalizer.For(source.Catalog);
                if (localizer is Localizer created)
                {
                    RegisterLanguages(created, source, localizer.Languages);
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            Sources.Clear();
            GlobalLocalizer.AddTableSources(CreateSource);
            GlobalLocalizer.Created += OnCreated;
        }

        private static ITableSource CreateSource(CatalogKey catalog)
        {
            // A folder that doesn't exist yet has no tables, and Refresh finds what a player puts there later.
            string folder = Path.Combine(Root, catalog.Name);
            // Read within each request, so the first frame already shows modded text. A game with huge mods can pass
            // true to read them on a thread pool thread instead, except on WebGL, which has none.
            FolderTableSource source = new(catalog, folder, false);
            Sources[catalog.Hash] = source;
            return source;
        }

        private static void OnCreated(Localizer localizer)
        {
            if (Sources.TryGetValue(localizer.CatalogKey.Hash, out FolderTableSource source))
            {
                RegisterLanguages(localizer, source, null);
            }
        }

        private static void RegisterLanguages(Localizer localizer, FolderTableSource source, IReadOnlyList<LanguageInfo> known)
        {
            List<string> problems = new();
            foreach (LanguageInfo language in source.ReadLanguages(problems))
            {
                if (known == null || !Contains(known, language))
                {
                    localizer.RegisterLanguage(language);
                }
            }
            for (int i = 0; i < problems.Count; i++)
            {
                Debug.LogWarning($"[ReactiveLocalizer] Mod: {problems[i]}");
            }
        }

        private static bool Contains(IReadOnlyList<LanguageInfo> languages, LanguageInfo language)
        {
            for (int i = 0; i < languages.Count; i++)
            {
                if (languages[i].Key == language.Key)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
