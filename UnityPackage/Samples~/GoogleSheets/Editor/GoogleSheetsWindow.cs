using reromanlee.ReactiveLocalizer.Authoring.Exchange;
using reromanlee.ReactiveLocalizer.Editor;
using reromanlee.ReactiveLocalizer.Unity;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace reromanlee.ReactiveLocalizer.Samples.GoogleSheets.Editor
{
    /// <summary>
    /// <c>Tools/ReactiveLocalizer/Samples/Google Sheets Sync</c>: pushes a catalog's tables into a private Google
    /// spreadsheet, one sheet per table, and pulls translators' edits back through the import preview.
    /// </summary>
    /// <remarks>
    /// Pull, then push: a push replaces the contents of each table's sheet with the project's, so edits made in the
    /// sheet since the last pull would be lost. A pull changes translations only, as every import does.
    /// </remarks>
    internal sealed class GoogleSheetsWindow : EditorWindow
    {
        private Label _status;
        private Button _pull;
        private Button _push;
        private bool _isBusy;

        [MenuItem("Tools/ReactiveLocalizer/Samples/Google Sheets Sync")]
        private static void Open()
        {
            GoogleSheetsWindow window = GetWindow<GoogleSheetsWindow>(true, "Google Sheets Sync");
            window.minSize = new Vector2(420f, 220f);
        }

        private void CreateGUI()
        {
            GoogleSheetsSettings settings = GoogleSheetsSettings.instance;
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 8f;
            TextField url = new("Web app URL") { value = settings.WebAppUrl, tooltip = "The URL of the deployed Apps Script, ending in /exec. Keep it secret." };
            url.RegisterValueChangedCallback(change => settings.WebAppUrl = change.newValue);
            root.Add(url);
            TextField token = new("Token") { value = settings.Token, isPasswordField = true, tooltip = "The script's TOKEN property: at least 16 random characters." };
            token.RegisterValueChangedCallback(change => settings.Token = change.newValue);
            root.Add(token);
            TextField catalog = new("Catalog") { value = settings.CatalogName, tooltip = "The catalog to sync; empty for the default catalog." };
            catalog.RegisterValueChangedCallback(change => settings.CatalogName = change.newValue);
            root.Add(catalog);
            Label note = new("The URL and token are saved in UserSettings, which version control leaves out. Pull before pushing: a push replaces each table's sheet.");
            note.style.whiteSpace = WhiteSpace.Normal;
            note.style.marginTop = 6f;
            note.style.color = new Color(0.6f, 0.6f, 0.6f);
            root.Add(note);

            VisualElement buttons = new();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.justifyContent = Justify.FlexEnd;
            buttons.style.marginTop = 8f;
            _pull = new Button(Pull) { text = "Pull Translations" };
            _push = new Button(Push) { text = "Push Tables..." };
            buttons.Add(_pull);
            buttons.Add(_push);
            root.Add(buttons);
            _status = new Label();
            _status.style.whiteSpace = WhiteSpace.Normal;
            _status.style.marginTop = 6f;
            root.Add(_status);
        }

        private void Pull()
        {
            if (!TryStart(out string url, out string token))
            {
                return;
            }
            Show("Pulling...", false);
            GoogleSheetsClient.Pull(url, token, (sheets, problem) =>
            {
                Stop();
                if (problem != null)
                {
                    Show(problem, true);
                    return;
                }
                ExportBook book = TranslationExchange.Collect(CatalogKey, null);
                List<ImportedFile> files = new();
                for (int i = 0; i < sheets.Length; i++)
                {
                    // Sheets of the spreadsheet's own, such as notes, are no tables of the catalog and stay out.
                    if (sheets[i] != null && book != null && HasTable(book, sheets[i].name))
                    {
                        files.Add(SheetRows.ToImportedFile(sheets[i]));
                    }
                }
                if (files.Count == 0)
                {
                    Show("The spreadsheet has no sheet named after a table of the catalog. Push the tables first.", true);
                    return;
                }
                Show($"Pulled {files.Count} sheet(s); the import preview lists what changes.", false);
                TranslationExchange.PreviewImport(files);
            });
        }

        private void Push()
        {
            if (!TryStart(out string url, out string token))
            {
                return;
            }
            ExportBook book = TranslationExchange.Collect(CatalogKey, null);
            if (book == null || book.Tables.Count == 0)
            {
                Stop();
                Show("The project has no such catalog, or its tables have no entries.", true);
                return;
            }
            if (!EditorUtility.DisplayDialog("Push Tables",
                    $"Replace the sheets of {book.Tables.Count} table(s) with the project's texts? Edits made in those sheets since the last pull are lost.",
                    "Push", "Cancel"))
            {
                Stop();
                return;
            }
            Show("Pushing...", false);
            GoogleSheetsClient.Push(url, token, SheetRows.FromBook(book).ToArray(), problem =>
            {
                Stop();
                Show(problem ?? $"Pushed {book.Tables.Count} table(s), {book.RowCount} entries.", problem != null);
            });
        }

        /// <summary>The catalog synced; empty for the default catalog, which a name that isn't valid never falls back to.</summary>
        private static CatalogKey CatalogKey => new CatalogReference(GoogleSheetsSettings.instance.CatalogName).ToKey();

        private bool TryStart(out string url, out string token)
        {
            GoogleSheetsSettings settings = GoogleSheetsSettings.instance;
            url = settings.WebAppUrl;
            token = settings.Token;
            if (_isBusy)
            {
                return false;
            }
            if (!url.StartsWith("https://script.google.com/", StringComparison.OrdinalIgnoreCase))
            {
                Show("The web app URL starts with https://script.google.com/ and ends in /exec; the sample's README tells where to find it.", true);
                return false;
            }
            if (token.Length < 16)
            {
                Show("The token is at least 16 characters, the same as the script's TOKEN property.", true);
                return false;
            }
            if (settings.CatalogName.Length > 0 && CatalogKey.IsEmpty)
            {
                Show($"'{settings.CatalogName}' isn't a catalog name: ASCII letters, digits and underscores, starting with a letter.", true);
                return false;
            }
            _isBusy = true;
            _pull.SetEnabled(false);
            _push.SetEnabled(false);
            return true;
        }

        private void Stop()
        {
            _isBusy = false;
            _pull?.SetEnabled(true);
            _push?.SetEnabled(true);
        }

        private void Show(string message, bool isProblem)
        {
            if (_status == null)
            {
                return;
            }
            _status.text = message;
            _status.style.color = isProblem ? new Color(0.9f, 0.6f, 0.2f) : new Color(0.6f, 0.6f, 0.6f);
        }

        private static bool HasTable(ExportBook book, string name)
        {
            for (int i = 0; i < book.Tables.Count; i++)
            {
                if (string.Equals(book.Tables[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
