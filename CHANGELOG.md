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
11. Pseudo-localization: a `Pseudo` language made from the source text, accented, a third longer and bracketed, so hardcoded text, truncation and overflow show before any translation exists. The editor previews it, Play Mode and development builds list it, and release builds never ship it.
12. Character sets, `Tools/ReactiveLocalizer/Character Sets`: every character a language can show, the fallback text of missing entries and number symbols included, copied or saved for TextMeshPro's Font Asset Creator.
13. Samples for every common integration: TextMeshPro with a font per language and a check of which characters each font can't draw, UI Toolkit with a UXML data binding on Unity 6 and code binding on 2022.3, uGUI Text, Addressables, VContainer, R3, Google Sheets two-way sync, mods, a localized editor tool, and a migration from Unity Localization.
14. Migration from Unity Localization: locales become languages and string tables become .lang files, with keys, comments and plain placeholders kept, and a report of what needs converting by hand.
15. `@delivery Custom` tables that builds don't pack, for a project's own source such as Addressables, and sources that answer asynchronously can decline a request so the next source is asked with nothing reported.
16. Bindings that update text on every language switch without allocating, and messages that are formatted again only when their arguments change.
17. Live reload: saving a table file updates bound text at once, in Play Mode and Edit Mode.
18. A Scene view overlay that picks the preview language. Components preview their text live in Edit Mode through `EditModePreview`, and scenes and prefabs always save the text they were authored with.
19. Rename, move and delete entries across every language at once. Moved entries keep working through `@formerly Table.Entry`, at runtime and in generated code.
20. Tables loaded on demand: `@loading OnDemand` tables stay in memory only while bindings or `HoldTable` handles use them, and follow every language switch while they do.
21. Streaming delivery: `@delivery Streaming` tables ship in StreamingAssets and load without blocking the main thread, through UnityWebRequest on Android and WebGL.
22. Fan translations and mods: `FolderTableSource` compiles `.lang` files from a folder at runtime, `RegisterLanguage` adds new languages, and sources asked first patch shipped languages entry by entry.
23. Plural rules and number formats for every CLDR 48 locale, built in without `CultureInfo`, with digits and separators each language can override.
24. Fallback languages loaded only for tables a translation leaves gaps in, so a complete translation costs no extra memory.
25. `ReactiveText` for view models and data binding, and `BindCharacters` for text that updates without allocating, such as TextMeshPro's.
26. Import checks that report problems at their line and column, check every translation against its source text and the plural forms of its language, and suggest the key an orphan most likely meant.
27. *Show source change* finds the source text an outdated translation was made from in git, and marks the words that changed.
28. One canonical layout for table files, written by every tool, with keys in natural order, such as `Line9` before `Line10`.
29. Creating a catalog asks for its source language and culture, and checks the culture against CLDR.
30. Lock-free lookups from any thread, with language switches applied all at once on the main thread, and rapid switches sharing what they load.
31. Missing keys shown as `[Table.Key]` in every build and reported once, and failures that never throw into game code.
32. Unity integration: `UnityHost`, tables packed into builds, the editor reading table files directly, and `EntryReference` and `CatalogReference` Inspector fields.
33. Public helpers for project tools: `NameRules`, `MessageText`, `TranslationExchange` and `CharacterSets`.

### Known issues

1. The README and the guides in Documentation/ aren't written yet.
2. Tables delivered by custom sources, such as Addressables, have no pseudo version, so pseudo-localization shows their source text.
3. Undo keeps whole copies of the table files each edit changes, so many edits of one very large table take a lot of memory.
4. Spreadsheet applications take CSV texts starting with = for formulas, so exchange XLSX with spreadsheets; formula errors in an imported CSV are rejected.
5. XLIFF 1.2 can't hold control characters, so entries with one are left out of it and reported; XLIFF 2.0 and XLSX keep them.
6. Character sets can't know text that arguments bring at runtime, such as player names; the window adds printable ASCII or extra characters for it.
7. The Google Sheets sample was checked offline only, since a live check needs a Google account; its requests follow Apps Script's documented web app behavior.
8. The migration converts Smart Format's plain placeholders only; its plural, choose and other formatters are kept as written and marked for converting by hand.
