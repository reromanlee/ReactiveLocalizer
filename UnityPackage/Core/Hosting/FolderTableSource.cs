using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Tables;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace reromanlee.ReactiveLocalizer.Hosting
{
    /// <summary>
    /// Delivers tables from text files in a folder, compiled when they are asked for: fan translations a player drops
    /// into a mods folder, or translations downloaded at runtime. Files are named and written like the project's own,
    /// such as <c>Shop.Klingon.lang</c>, anywhere under the folder.
    /// <code>
    /// FolderTableSource mods = new FolderTableSource(LocalizationKeys.CatalogKey, modsPath);
    /// Localizer localizer = new Localizer(LocalizationKeys.CatalogKey, new UnityHost(mods));
    /// foreach (LanguageInfo language in mods.ReadLanguages(null))
    /// {
    ///     localizer.RegisterLanguage(language);
    /// }
    /// </code>
    /// </summary>
    /// <remarks>
    /// Asked before the built-in sources, its tables come first: a file for a language the game ships patches the
    /// entries it has and leaves the rest to the game's own table. New languages come from the language sections of
    /// the folder's <c>.catalog</c> files, registered with <see cref="Localizer.RegisterLanguage"/>. Problems in a file
    /// are reported with its path and line, and never stop the rest of it. The folder is listed the first time it is
    /// needed; <see cref="Refresh"/> lists it again.
    /// </remarks>
    public sealed class FolderTableSource : ITableSource
    {
        private const string TableExtension = ".lang";
        private const string CatalogExtension = ".catalog";

        private readonly object _lockObject = new();
        private readonly bool _isReadingInBackground;
        private Listing _listing;

        /// <param name="catalog">The catalog the folder's tables belong to.</param>
        /// <param name="folder">The folder to read, searched with every folder inside it.</param>
        /// <param name="isReadingInBackground">
        /// Whether files are read and compiled on a thread pool thread rather than within the request, which keeps
        /// large files from costing a frame. Leave it off where there are no threads, as on WebGL.
        /// </param>
        /// <exception cref="ArgumentException"><paramref name="catalog"/> or <paramref name="folder"/> is empty.</exception>
        public FolderTableSource(CatalogKey catalog, string folder, bool isReadingInBackground = false)
        {
            if (catalog.IsEmpty)
            {
                throw new ArgumentException("A folder table source needs the key of its catalog.", nameof(catalog));
            }
            if (string.IsNullOrEmpty(folder))
            {
                throw new ArgumentException("A folder table source needs a folder.", nameof(folder));
            }
            Catalog = catalog;
            Folder = folder;
            _isReadingInBackground = isReadingInBackground;
        }

        /// <summary>The catalog the folder's tables belong to.</summary>
        public CatalogKey Catalog { get; }

        /// <summary>The folder the tables are read from.</summary>
        public string Folder { get; }

        /// <summary>Lists the folder again the next time it is needed, as after a player added or removed a mod.</summary>
        /// <remarks>Tables already loaded stay as they are until the localizer loads them again, such as on a language switch.</remarks>
        public void Refresh()
        {
            lock (_lockObject)
            {
                _listing = null;
            }
        }

        /// <summary>
        /// Reads the languages that the language sections of the folder's <c>.catalog</c> files define, in path
        /// order. Problems are appended to <paramref name="problems"/>, each with its file and line: a section that
        /// repeats a language, a field it can't read, a table file named unlike <c>Table.Language.lang</c>.
        /// </summary>
        public IReadOnlyList<LanguageInfo> ReadLanguages(ICollection<string> problems)
        {
            Listing listing = GetListing();
            for (int i = 0; i < listing.Problems.Count; i++)
            {
                problems?.Add(listing.Problems[i]);
            }
            List<LanguageInfo> languages = new();
            HashSet<ulong> names = new();
            List<DocumentIssue> issues = new();
            for (int i = 0; i < listing.CatalogFiles.Count; i++)
            {
                string path = listing.CatalogFiles[i];
                if (!TryReadText(path, out string text, out string error))
                {
                    problems?.Add($"{path}: {error}");
                    continue;
                }
                issues.Clear();
                CatalogDocument document = CatalogDocument.Parse(text);
                issues.AddRange(document.Issues);
                IReadOnlyList<LanguageInfo> read = CatalogInfo.ReadLanguages(document, issues);
                for (int l = 0; l < read.Count; l++)
                {
                    if (names.Add(read[l].Key.Hash))
                    {
                        languages.Add(read[l]);
                    }
                    else
                    {
                        issues.Add(new DocumentIssue(IssueSeverity.Error, document.Languages[l].Line, 1,
                            $"'{read[l].Name}' is defined by another file of the folder too; the first definition is used."));
                    }
                }
                for (int p = 0; p < issues.Count; p++)
                {
                    problems?.Add(path + issues[p]);
                }
            }
            return languages;
        }

        /// <inheritdoc/>
        public bool TryLoad(in TableRequest request, TableReceiver receiver)
        {
            if (request.IsCatalog || request.Catalog != Catalog || receiver == null)
            {
                return false;
            }
            Listing listing = GetListing();
            if (!listing.Tables.TryGetValue((request.Table.Hash, request.Language.Hash), out string path))
            {
                return false;
            }
            if (listing.Duplicates.TryGetValue((request.Table.Hash, request.Language.Hash), out string duplicate))
            {
                receiver.Warn($"{duplicate} is ignored: {path} already has {request.Table.Name} in {request.Language.Name}.");
            }
            if (!_isReadingInBackground)
            {
                Deliver(path, request, receiver);
                return true;
            }
            TableRequest queued = request;
            ThreadPool.QueueUserWorkItem(_ => Deliver(path, queued, receiver));
            return true;
        }

        private void Deliver(string path, TableRequest request, TableReceiver receiver)
        {
            try
            {
                if (!TryReadText(path, out string text, out string error))
                {
                    receiver.Fail($"{path}: {error}");
                    return;
                }
                TableDocument document = TableDocument.Parse(text);
                List<DocumentIssue> issues = new(document.Issues);
                byte[] data = TableCompiler.Compile(Catalog, request.Table, request.Language, document, issues);
                for (int i = 0; i < issues.Count; i++)
                {
                    receiver.Warn(path + issues[i]);
                }
                receiver.Receive(data);
            }
            catch (Exception exception)
            {
                // Compiling never throws over content; this keeps a bug from leaving the request unanswered.
                receiver.Fail($"{path}: {exception.Message}");
            }
        }

        private Listing GetListing()
        {
            lock (_lockObject)
            {
                return _listing ??= List(Folder);
            }
        }

        /// <summary>Lists every table and catalog file under <paramref name="root"/>, in path order, skipping folders it can't read.</summary>
        private static Listing List(string root)
        {
            Listing listing = new();
            List<string> tableFiles = new();
            Stack<string> folders = new();
            folders.Push(root);
            while (folders.Count > 0)
            {
                string folder = folders.Pop();
                try
                {
                    foreach (string file in Directory.EnumerateFiles(folder))
                    {
                        if (file.EndsWith(TableExtension, StringComparison.OrdinalIgnoreCase))
                        {
                            tableFiles.Add(file);
                        }
                        else if (file.EndsWith(CatalogExtension, StringComparison.OrdinalIgnoreCase))
                        {
                            listing.CatalogFiles.Add(file);
                        }
                    }
                    foreach (string child in Directory.EnumerateDirectories(folder))
                    {
                        folders.Push(child);
                    }
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
                {
                    // A missing folder simply has no tables; one that can't be read is skipped, and the rest still are.
                    if (folder != root || Directory.Exists(root))
                    {
                        listing.Problems.Add($"{folder}: couldn't be read, so its tables are skipped: {exception.Message}");
                    }
                }
            }
            tableFiles.Sort(StringComparer.Ordinal);
            listing.CatalogFiles.Sort(StringComparer.Ordinal);
            for (int i = 0; i < tableFiles.Count; i++)
            {
                string path = tableFiles[i];
                string[] names = Path.GetFileNameWithoutExtension(path).Split('.');
                if (names.Length != 2 || !NameRules.IsValid(names[0]) || !NameRules.IsValid(names[1]))
                {
                    listing.Problems.Add($"{path}: a table file is named <Table>.<Language>.lang, such as Shop.Klingon.lang, so this one is never read.");
                    continue;
                }
                (ulong Table, ulong Language) key = (Hashing.ComputeNameHash(names[0]), Hashing.ComputeNameHash(names[1]));
                if (!listing.Tables.TryAdd(key, path))
                {
                    listing.Duplicates.TryAdd(key, path);
                }
            }
            return listing;
        }

        private static bool TryReadText(string path, out string text, out string error)
        {
            try
            {
                text = File.ReadAllText(path);
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is NotSupportedException)
            {
                text = null;
                error = $"couldn't be read: {exception.Message}";
                return false;
            }
        }

        private sealed class Listing
        {
            public Dictionary<(ulong Table, ulong Language), string> Tables { get; } = new();

            /// <summary>Files that name a table and language another file already has, keeping the first one ignored.</summary>
            public Dictionary<(ulong Table, ulong Language), string> Duplicates { get; } = new();

            public List<string> CatalogFiles { get; } = new();

            public List<string> Problems { get; } = new();
        }
    }
}
