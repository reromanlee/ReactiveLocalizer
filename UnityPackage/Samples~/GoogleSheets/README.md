# Google Sheets sync

Two-way sync of a catalog with a private Google spreadsheet: one sheet per table, with the same columns as a CSV export. Translators edit the language columns, and the editor pulls their work back through the import preview.

### Set up the spreadsheet

1. Create a spreadsheet in Google Sheets and keep it private. Share it only with the people who translate.
2. Open *Extensions > Apps Script*, replace the contents of `Code.gs` with `AppsScript.gs.txt` from this sample, and save.
3. Open *Project Settings > Script Properties* and add the property `TOKEN`. Its value is a secret of at least 16 random characters, such as one a password manager makes.
4. Choose *Deploy > New deployment*, with the type *Web app*, *Execute as: Me* and *Who has access: Anyone*. Deploy it, allow the access it asks for, and copy the web app URL, which ends in `/exec`.

### Sync from Unity

1. Open *Tools > ReactiveLocalizer > Samples > Google Sheets Sync*, and paste the URL and the token. Leave *Catalog* empty for the default catalog.
2. *Push Tables* writes every table of the catalog into its own sheet, replacing what that sheet held.
3. *Pull Translations* reads every sheet named after a table and opens the import preview, which lists every change before anything is written.

Pull before you push, since a push replaces each table's sheet, and edits made since the last pull would be lost.

### Security

1. *Who has access: Anyone* lets anyone with the URL call the script, but it answers only requests carrying the token. The spreadsheet is never published to the web.
2. The editor saves the URL and token in `UserSettings/`, which version control leaves out. Keep them out of commits, chats and screenshots.
3. To revoke access, change the `TOKEN` property; the old token stops working at once.

### What a pull changes

A pull is an import like any other. It changes translations only, never keys. Rows with unknown keys are rejected as orphans, empty cells never erase a translation, and changes to the source column are ignored unless *Import source changes* is ticked in the preview.
