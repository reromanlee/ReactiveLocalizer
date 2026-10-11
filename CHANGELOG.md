# ReactiveLocalizer (Unreleased)

### What's new

1. Localization for Unity 2022.3 and newer without package dependencies: catalogs of project-defined languages, invented and fan-made ones included, each with its source language and fallback chains.
2. A table window, `Window/ReactiveLocalizer/Tables`, showing every table in every language in one grid. It has status tints, filters, search, a detail pane with multi-line editors and a live plural preview, and Undo for every edit.
3. ICU MessageFormat messages with plurals, ordinals, selects, number styles and nesting, compiled at import and formatted without allocating.
4. An Inspector field for entries that searches keys and texts, lists recent picks first, and creates a missing entry on the spot. It previews the text in the preview language and fixes renamed or moved references in one click, in both UI Toolkit and IMGUI Inspectors.
5. Generated keys per catalog, such as `LocalizationKeys.Shop.Purchase`, with typed methods for messages, such as `LocalizationKeys.Shop.CoinBalance(coins: 5)`, and `[Obsolete]` forwarders for renamed entries. Edits made in the editor update them when Unity loses focus, so working in the editor never triggers a recompile.
6. Text files as the source of truth: one `.lang` file per table and language, with comments for translators, aliases for renamed entries, and fingerprints that flag outdated translations.
7. Validation, `Tools/ReactiveLocalizer/Validate`, and a build gate that fails player builds on broken messages, orphans, missing required translations, and references to keys that don't exist in built scenes, prefabs and Resources.
8. Exchange with translators: an XLSX workbook, CSV files or XLIFF 1.2 and 2.0 files, exported with a choice of tables, languages, and all entries or only missing or outdated ones. Keys are written `Shop.Purchase`, so renaming a file or sheet changes nothing.
9. Imports that change translations only, never keys: a preview lists every new, updated, outdated, confirmed and rejected text before anything is written, each compiled like the files, and the whole import is one step Undo can take back. The source text in the file becomes the fingerprint, so a translation of an older text arrives outdated.
10. An LLM handoff: missing or outdated entries copied as a text bundle with rules for the model, its plural forms included, and the model's reply imported from the clipboard, with no API keys.
11. Bindings that update text on every language switch without allocating, and messages that are formatted again only when their arguments change.
12. Live reload: saving a table file updates bound text at once, in Play Mode and Edit Mode.
13. A Scene view overlay that picks the preview language. Components preview their text live in Edit Mode through `EditModePreview`, and scenes and prefabs always save the text they were authored with.
14. Rename, move and delete entries across every language at once. Moved entries keep working through `@formerly Table.Entry`, at runtime and in generated code.
15. Tables loaded on demand: `@loading OnDemand` tables stay in memory only while bindings or `HoldTable` handles use them, and follow every language switch while they do.
16. Streaming delivery: `@delivery Streaming` tables ship in StreamingAssets and load without blocking the main thread, through UnityWebRequest on Android and WebGL.
17. Fan translations and mods: `FolderTableSource` compiles `.lang` files from a folder at runtime, `RegisterLanguage` adds new languages, and sources asked first patch shipped languages entry by entry.
18. Plural rules and number formats for every CLDR 48 locale, built in without `CultureInfo`, with digits and separators each language can override.
19. Fallback languages loaded only for tables a translation leaves gaps in, so a complete translation costs no extra memory.
20. `ReactiveText` for view models and data binding, and `BindCharacters` for text that updates without allocating, such as TextMeshPro's.
21. Import checks that report problems at their line and column, check every translation against its source text and the plural forms of its language, and suggest the key an orphan most likely meant.
22. *Show source change* finds the source text an outdated translation was made from in git, and marks the words that changed.
23. One canonical layout for table files, written by every tool, with keys in natural order, such as `Line9` before `Line10`.
24. Creating a catalog asks for its source language and culture, and checks the culture against CLDR.
25. Lock-free lookups from any thread, with language switches applied all at once on the main thread, and rapid switches sharing what they load.
26. Missing keys shown as `[Table.Key]` in every build and reported once, and failures that never throw into game code.
27. Unity integration: `UnityHost`, tables packed into builds, the editor reading table files directly, and `EntryReference` and `CatalogReference` Inspector fields.
28. Quick Start and TextMeshPro samples, the TextMeshPro component previewing its text in Edit Mode.

### Known issues

1. Pseudo-localization, character sets and the remaining samples, Google Sheets sync among them, aren't available yet.
2. Undo keeps whole copies of the table files each edit changes, so many edits of one very large table take a lot of memory.
3. Spreadsheet applications take CSV texts starting with = for formulas, so exchange XLSX with spreadsheets; formula errors in an imported CSV are rejected.
4. XLIFF 1.2 can't hold control characters, so entries with one are left out of it and reported; XLIFF 2.0 and XLSX keep them.
