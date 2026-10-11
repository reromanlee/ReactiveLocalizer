using reromanlee.ReactiveLocalizer.Authoring;
using reromanlee.ReactiveLocalizer.Authoring.Exchange;
using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// An import being prepared: the files read, the catalog they go into, which catalog language each of their
    /// languages is, and the options. It plans as often as a preview needs, and applies as one step Undo can take back.
    /// </summary>
    internal sealed class ImportSession
    {
        /// <summary>Starts an import of <paramref name="files"/> into the catalog they most likely belong to.</summary>
        public ImportSession(IReadOnlyList<ImportedFile> files)
        {
            Files = files ?? Array.Empty<ImportedFile>();
            SetCatalog(LocalizationExchange.FindCatalog(Files));
        }

        public IReadOnlyList<ImportedFile> Files { get; }

        /// <summary>The catalog imported into; null when the project has no usable one.</summary>
        public IndexedCatalog Catalog { get; private set; }

        public ImportOptions Options { get; } = new();

        /// <summary>Why the import can't be applied at all, or null.</summary>
        public string Problem
        {
            get
            {
                if (Catalog?.Info == null)
                {
                    return "The project has no usable catalog to import into.";
                }
                return Catalog.IsWritable ? null : $"The catalog '{Catalog.Name}' is in a read-only package, so its files can't change.";
            }
        }

        /// <summary>Imports into <paramref name="catalog"/>, deciding again which of its languages the files' languages are.</summary>
        public void SetCatalog(IndexedCatalog catalog)
        {
            Catalog = catalog;
            if (catalog?.Info == null)
            {
                return;
            }
            for (int i = 0; i < Files.Count; i++)
            {
                ImportPlanner.ResolveLanguages(catalog.Info, Files[i]);
            }
        }

        /// <summary>Sets which catalog language every language the files label <paramref name="label"/> is; null skips them.</summary>
        public void SetLanguage(string label, string languageName)
        {
            for (int f = 0; f < Files.Count; f++)
            {
                for (int l = 0; l < Files[f].Languages.Count; l++)
                {
                    if (string.Equals(Files[f].Languages[l].Label, label, StringComparison.OrdinalIgnoreCase))
                    {
                        Files[f].Languages[l].LanguageName = languageName;
                    }
                }
            }
        }

        /// <summary>Plans the import on the files as they are now, changing nothing on disk. Null without a usable catalog.</summary>
        public ImportPlan Plan() => Plan(out _);

        /// <summary>
        /// Plans the import afresh, on the files as they are now, and writes what it changes as one Undo step. Returns
        /// what was done; null, writing nothing, while <see cref="Problem"/> says why it can't.
        /// </summary>
        public ImportPlan Apply()
        {
            if (Problem != null)
            {
                return null;
            }
            ImportPlan plan = Plan(out List<TableEdit> edits);
            if (plan.HasChanges)
            {
                TableEdit.Save(LocalizationExchange.UndoName, edits.ToArray());
            }
            return plan;
        }

        private ImportPlan Plan(out List<TableEdit> edits)
        {
            List<TableEdit> opened = new();
            edits = opened;
            IndexedCatalog catalog = Catalog;
            if (catalog?.Info == null)
            {
                return null;
            }
            return ImportPlanner.Plan(catalog.Info, Files, tableName =>
            {
                if (!catalog.TryGetTable(new TableKey(tableName), out _))
                {
                    return null;
                }
                TableEdit edit = new(catalog, tableName);
                opened.Add(edit);
                return edit.Files;
            }, Options);
        }
    }
}
