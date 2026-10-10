# ReactiveLocalizer (Unreleased)

### What's new

1. Localization for Unity 2022.3 and newer without package dependencies: catalogs of project-defined languages, invented and fan-made ones included, each with its source language and fallback chains.
2. ICU MessageFormat messages with plurals, ordinals, selects, number styles and nesting, compiled at import and formatted without allocating.
3. Generated keys per catalog, such as `LocalizationKeys.Shop.Purchase`, with typed methods for messages, such as `LocalizationKeys.Shop.CoinBalance(coins: 5)`, and `[Obsolete]` forwarders for renamed entries.
4. Text files as the source of truth: one `.lang` file per table and language, with comments for translators, aliases for renamed entries, and fingerprints that flag outdated translations.
5. Bindings that update text on every language switch without allocating, and messages that are formatted again only when their arguments change.
6. Tables loaded on demand: `@loading OnDemand` tables stay in memory only while bindings or `HoldTable` handles use them, and follow every language switch while they do.
7. Streaming delivery: `@delivery Streaming` tables ship in StreamingAssets and load without blocking the main thread, through UnityWebRequest on Android and WebGL.
8. Fan translations and mods: `FolderTableSource` compiles `.lang` files from a folder at runtime, `RegisterLanguage` adds new languages, and sources asked first patch shipped languages entry by entry.
9. Plural rules and number formats for every CLDR 48 locale, built in without `CultureInfo`, with digits and separators each language can override.
10. Fallback languages loaded only for tables a translation leaves gaps in, so a complete translation costs no extra memory.
11. `ReactiveText` for view models and data binding, and `BindCharacters` for text that updates without allocating, such as TextMeshPro's.
12. Import checks that report problems at their line and column, and check every translation against its source text and the plural forms of its language.
13. Lock-free lookups from any thread, with language switches applied all at once on the main thread, and rapid switches sharing what they load.
14. Missing keys shown as `[Table.Key]` in every build and reported once, and failures that never throw into game code.
15. Unity integration: `UnityHost`, tables packed into builds, the editor reading table files directly, and `EntryReference` and `CatalogReference` Inspector fields.
16. Quick Start and TextMeshPro samples.

### Known issues

1. Edited table files reach a running game only after Play Mode restarts; live reload arrives with the editor tooling.
2. The table window, Inspector search, the build gate and the Scene view overlay aren't available yet.
3. CSV, XLSX and XLIFF exchange, pseudo-localization and the remaining samples aren't available yet.
