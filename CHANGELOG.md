# ReactiveLocalizer (Unreleased)

### What's new

1. Localization for Unity 2022.3 and newer without package dependencies: catalogs of project-defined languages, invented and fan-made ones included, each with its source language and fallback chains.
2. ICU MessageFormat messages with plurals, ordinals, selects, number styles and nesting, compiled at import and formatted without allocating.
3. Generated keys per catalog, such as `LocalizationKeys.Shop.Purchase`, with typed methods for messages, such as `LocalizationKeys.Shop.CoinBalance(coins: 5)`, and `[Obsolete]` forwarders for renamed entries.
4. Text files as the source of truth: one `.lang` file per table and language, with comments for translators, aliases for renamed entries, and fingerprints that flag outdated translations.
5. Bindings that update text on every language switch without allocating, and messages that are formatted again only when their arguments change.
6. Plural rules and number formats for every CLDR 48 locale, built in without `CultureInfo`, with digits and separators each language can override.
7. Import checks that report problems at their line and column, and check every translation against its source text and the plural forms of its language.
8. Lock-free lookups from any thread, with language switches applied all at once on the main thread.
9. Missing keys shown as `[Table.Key]` in every build and reported once, and failures that never throw into game code.
10. Unity integration: `UnityHost`, tables packed into builds, the editor reading table files directly, and `EntryReference` and `CatalogReference` Inspector fields.
11. Quick Start and TextMeshPro samples.

### Known issues

1. Tables set to `@loading OnDemand` or `@delivery Streaming` can't be read until table loading arrives.
2. Edited table files reach a running game only after Play Mode restarts; live reload arrives with the editor tooling.
3. The table window, Inspector search, the build gate and the Scene view overlay aren't available yet.
4. CSV, XLSX and XLIFF exchange, pseudo-localization and the remaining samples aren't available yet.
