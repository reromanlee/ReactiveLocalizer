# ReactiveLocalizer (Unreleased)

### What's new

1. Localization for Unity 2022.3 and newer without package dependencies: catalogs of project-defined languages, invented and fan-made ones included, each with its source language and fallback chains.
2. A table window, `Window/ReactiveLocalizer/Tables`, showing every table in every language in one grid. It has status tints, filters, search, a detail pane with multi-line editors and a live plural preview, and Undo for every edit.
3. ICU MessageFormat messages with plurals, ordinals, selects, number styles and nesting, compiled at import and formatted without allocating.
4. An Inspector field for entries that searches keys and texts, lists recent picks first, and creates a missing entry on the spot. It previews the text in the preview language and fixes renamed or moved references in one click, in both UI Toolkit and IMGUI Inspectors.
5. Generated keys per catalog, such as `LocalizationKeys.Shop.Purchase`, with typed methods for messages, such as `LocalizationKeys.Shop.CoinBalance(coins: 5)`, and `[Obsolete]` forwarders for renamed entries. Edits made in the editor update them when Unity loses focus, so working in the editor never triggers a recompile.
6. Text files as the source of truth: one `.lang` file per table and language, with comments for translators, aliases for renamed entries, and fingerprints that flag outdated translations.
7. Validation, `Tools/ReactiveLocalizer/Validate`, and a build gate that fails player builds on broken messages, orphans, missing required translations, and references to keys that don't exist in built scenes, prefabs and Resources.
8. Bindings that update text on every language switch without allocating, and messages that are formatted again only when their arguments change.
9. Live reload: saving a table file updates bound text at once, in Play Mode and Edit Mode.
10. A Scene view overlay that picks the preview language. Components preview their text live in Edit Mode through `EditModePreview`, and scenes and prefabs always save the text they were authored with.
11. Rename, move and delete entries across every language at once. Moved entries keep working through `@formerly Table.Entry`, at runtime and in generated code.
12. Tables loaded on demand: `@loading OnDemand` tables stay in memory only while bindings or `HoldTable` handles use them, and follow every language switch while they do.
13. Streaming delivery: `@delivery Streaming` tables ship in StreamingAssets and load without blocking the main thread, through UnityWebRequest on Android and WebGL.
14. Fan translations and mods: `FolderTableSource` compiles `.lang` files from a folder at runtime, `RegisterLanguage` adds new languages, and sources asked first patch shipped languages entry by entry.
15. Plural rules and number formats for every CLDR 48 locale, built in without `CultureInfo`, with digits and separators each language can override.
16. Fallback languages loaded only for tables a translation leaves gaps in, so a complete translation costs no extra memory.
17. `ReactiveText` for view models and data binding, and `BindCharacters` for text that updates without allocating, such as TextMeshPro's.
18. Import checks that report problems at their line and column, check every translation against its source text and the plural forms of its language, and suggest the key an orphan most likely meant.
19. *Show source change* finds the source text an outdated translation was made from in git, and marks the words that changed.
20. One canonical layout for table files, written by every tool, with keys in natural order, such as `Line9` before `Line10`.
21. Creating a catalog asks for its source language and culture, and checks the culture against CLDR.
22. Lock-free lookups from any thread, with language switches applied all at once on the main thread, and rapid switches sharing what they load.
23. Missing keys shown as `[Table.Key]` in every build and reported once, and failures that never throw into game code.
24. Unity integration: `UnityHost`, tables packed into builds, the editor reading table files directly, and `EntryReference` and `CatalogReference` Inspector fields.
25. Quick Start and TextMeshPro samples, the TextMeshPro component previewing its text in Edit Mode.

### Known issues

1. CSV, XLSX and XLIFF exchange, pseudo-localization and the remaining samples aren't available yet.
2. Undo keeps whole copies of the table files each edit changes, so many edits of one very large table take a lot of memory.
