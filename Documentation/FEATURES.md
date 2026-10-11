# Features

Everything ReactiveLocalizer does, most important first. How to use each part is in [USAGE.md](USAGE.md), and the exact rules of the files in [FORMAT.md](FORMAT.md).

### One model at every scale

A prototype and a production game with hundreds of thousands of entries use the same files, the same runtime and the same tools, so growing never needs a migration.

1. **A five-minute start:** add a table, write `Play = Play`, and read it from code or pick it in the Inspector. Embedded tables load within the first call, so text can be read on the first frame without awaiting anything.
2. **Room to grow:** tables split by feature, load on demand, stream from StreamingAssets or come from Addressables, and a 50,000-line dialogue table only ever creates strings for the lines actually shown.
3. **No dependencies:** `package.json` declares none. Integration code lives in samples that compile only when their package is installed, so importing one never breaks compilation.
4. **Every platform:** a pure C# core with no engine references runs the same in the Editor, Mono and IL2CPP builds, WebGL included, and builds with plain .NET.

### Text files as the source of truth

Every table is one `.lang` file per language, in a line-based `Key = Value` format that diffs, merges and reviews like code, and that people and AI agents can edit with any text editor.

1. **Translation files hold only translated entries,** so adding a key changes only the source-language file, and translators' deliveries never touch the lines developers edit.
2. **Comments above an entry** are the context translators see in the table window and in every export.
3. **Fingerprints** mark which source text each translation was made from, so a changed source text flags its translations as outdated in every language.
4. **One canonical layout,** written by every tool: keys in natural order, so `Line9` comes before `Line10`, invisible characters as `\u` escapes that show up in reviews, and the file's own line endings.
5. **Hand edits are supported, not forbidden.** Merge conflict resolution, AI agents and quick fixes are validated like any other edit.
6. **The format is a public contract,** specified in [FORMAT.md](FORMAT.md), so outside tools can read and write it.

### Keys the compiler checks

The editor writes one plain C# file per catalog, so code names entries through members that exist or fail to compile.

```csharp
LocalizationKeys.Shop.Purchase                  // a plain entry: a field
LocalizationKeys.Shop.CoinBalance(coins: 5)     // an entry with arguments: a method returning EntryMessage
LocalizationKeys.Shop.BuyButton                 // a renamed entry: an [Obsolete] forwarder to Purchase
LocalizationLanguages.Russian                   // a language
```

1. **Argument types come from the text:** plural and number arguments take any numeric type without boxing, select arguments take strings, and parameters are sorted by name, so rewording a text never reorders them.
2. **Changing an argument breaks its call sites at compile time,** never at runtime.
3. **Editing text never recompiles.** The file changes only when a key, alias, argument, table or language changes, and edits made in the editor's own tools wait until Unity loses focus, so working in the editor never triggers a recompile.
4. **A typo never breaks the build:** while a text's message has errors, the file keeps its current members.
5. **Plain files, not a source generator,** so the code is visible to reviews, IDEs and agents. Tables referenced from data rather than code opt out with `@generateCode false` and are read by name without allocating.

### ICU messages

Messages use ICU MessageFormat, the syntax translation tools understand, compiled into instructions at import and formatted without allocating.

1. **Plurals, ordinals, selects, numbers and nesting:** `{coins, plural, one {# coin} other {# coins}}`, `{place, selectordinal, ...}`, `{gender, select, ...}`, `{ratio, number, percent}`, with `=N` forms and `offset:`.
2. **Plural rules and number formats for every CLDR 48 locale,** generated into the core and used without `CultureInfo`, so a message formats identically on every device. Numbers group as their language groups them, Indian grouping and minimum grouping digits included.
3. **Digits and separators any language can override,** such as Arabic with Latin digits, or an invented language with its own symbols.
4. **Dates, times and currency** go through formatters the project registers per type, which write into a span without allocating.
5. **Checked at import:** syntax errors with their line and column, translations using other arguments than their source, plural forms a language lacks or never uses, and likely mistakes such as `=#` inside a plural form.
6. **Text from a fallback language formats with that language's rules,** since the words around the number are in that language.

### Bindings that follow every switch

`Bind` calls a callback with an entry's text right away and again every time it changes: a language switch, an on-demand table arriving, a table file saved in the editor, or new arguments.

1. **Nothing allocates in steady state.** The binding handle is a struct, the callback a static lambda that receives its target, and `BindCharacters` hands over characters without creating strings, which TextMeshPro copies straight into its buffer.
2. **Messages format again only when their arguments change,** so setting the same arguments every frame costs nothing.
3. **A language switch is all or nothing:** the new language's tables load first, then every lookup and binding switches in the same frame, so no frame ever shows two languages. Rapid switches collapse into the latest one.
4. **Inactive UI costs nothing.** Components bind while enabled only, and text enabled later arrives in the current language.
5. **A failing callback is reported and skipped,** and the other bindings still update. Bindings whose target was destroyed are released and reported.
6. **`ReactiveText`** wraps a binding as an observable value with a `Changed` event and `INotifyPropertyChanged`, for view models, data binding and reactive libraries.

### The table window

**Window > ReactiveLocalizer > Tables** shows every table of every catalog in one grid, a column per language, built on UI Toolkit's list view, which renders only the rows in view, however long the table.

1. **Status tints** for missing, outdated, broken, too long and orphaned texts, filters for what is missing, outdated or broken, and badges with the counts on every table in the sidebar.
2. **Search** across keys, text in every language and comments.
3. **A detail pane** with multi-line editors for every language, the translator context, the maximum length, and a live preview of a plural message with sample values, such as Russian with 1, 2, 5 and 21.
4. **Add, rename, move and delete** entries across every language file at once, each one step that Undo takes back.
5. **Edits save when committed,** with no unsaved state, and bound text updates live.
6. **Show source change** finds the source text an outdated translation was made from in git and marks the words that changed; **Mark as current** confirms a translation that still fits.

### Inspector fields

`EntryReference` is the field type for components that show an entry. It stores the catalog, table and key as names, so references survive reordering, merges and code changes.

1. **A search popup** over keys and texts, with recent picks first, rendering only the visible results.
2. **Entries created on the spot:** when nothing matches, the popup creates the entry, suggesting a table named after the scene or prefab and a key named after the GameObject's role, such as `Title` for `TitleLabel`. Creating and assigning it is one Undo step, and it writes only the source file, so nothing recompiles.
3. **A preview of the text** in the preview language below the field.
4. **Broken references show red,** and a reference to a renamed entry offers to update itself.
5. **UI Toolkit and IMGUI Inspectors alike,** and `[EntryCatalog(typeof(ToolKeys))]` limits a field to one catalog.

### Edit Mode preview

The **Preview Language** overlay of the Scene view switches the language every bound text in the open scenes shows, live, in Edit Mode.

1. **Scenes and prefabs always save the text they were authored with,** never the preview, and text typed over the preview counts as the new authored text.
2. **Entry fields and the table window** preview in the same language.
3. **Components opt in** through `EditModePreview`, as the samples' components do, and the TextMeshPro sample switches fonts per language in the preview too.
4. **Saving a table file updates bound text at once,** in Edit Mode and Play Mode.

### Validation and the build gate

Every file is checked as it imports, and **Tools > ReactiveLocalizer > Validate** checks the whole project. Problems are reported with their file, line and column, and the table window shows the same problems the build gate does.

1. **Errors fail player builds:** malformed files, merge conflict markers, duplicate keys, orphans, broken messages, arguments that differ between languages, entries missing in required languages, and references to keys that don't exist from the scenes, prefabs and Resources in the build.
2. **Warnings don't:** outdated and unverified translations, entries missing in other languages, texts over their maximum length, unused aliases, and the optional PascalCase naming rule.
3. **Orphans suggest the key they most likely meant,** such as `Purchase` for `Purchse`.
4. **Translations re-validate automatically** whenever their source file changes.

### Loading and delivery

1. **Only the current language is loaded.** Fallback languages load only for tables with gaps, so a complete translation costs no extra memory.
2. **`@loading OnDemand` tables** stay in memory only while bindings or `HoldTable` handles use them, follow every language switch while they do, and unload two updates after the last user lets go, so a panel toggled within a frame keeps its table.
3. **`@delivery Embedded`** packs a table inside the build, readable on the first frame; **`Streaming`** packs it into StreamingAssets, read without blocking the main thread, through UnityWebRequest on Android and WebGL; **`Custom`** leaves it to a source of the project, such as the Addressables sample's.
4. **Sources stack:** a mod folder asked first patches a shipped language entry by entry, the game's own tables fill in the rest, and a language a source doesn't have falls through to the next.
5. **Failures never throw into game code.** A table that fails to load falls back to its fallback language, and the task reports what failed.

### Languages

1. **Languages are records the project defines,** with a name, a display name and an optional culture, so invented and fan-made languages work as well as real ones.
2. **Any language can be the source,** such as `@source Russian` for a Russian-first project.
3. **Fallback chains** per language, always ending at the source language.
4. **Languages added at runtime** with `RegisterLanguage`, such as a fan translation from a mods folder, which a language picker lists like any other.
5. **Required languages** fail builds while any entry is missing in them.
6. **Several catalogs per project:** packages and editor tools ship their own, so their tables and languages never mix with the game's, and a catalog in an Editor folder never reaches a build.

### Exchange with translators

**Tools > ReactiveLocalizer > Export** and **Import** exchange translations in the formats translators use, all holding the same thing: the key, the context, the maximum length, the source text, and each language's text and state.

1. **XLSX:** one workbook, a sheet per table, read and written without dependencies, with the header row frozen, long texts wrapped, outdated texts tinted, and text cells that never turn into numbers or formulas.
2. **CSV:** a file per table, saved so Excel reads non-Latin text correctly, with the separator and encoding detected when importing.
3. **XLIFF 1.2 and 2.0:** a file per language, with states for missing, outdated and translated texts, read from any translation tool's output.
4. **An LLM handoff:** missing or outdated entries copied to the clipboard as a text bundle with rules for the model, its plural forms included, and the model's whole reply imported from the clipboard, with no API keys.
5. **Imports change translations only,** never keys. A preview lists every new, updated, outdated, confirmed and rejected text before anything is written, every text is compiled like the files are, and the whole import is one step Undo takes back.
6. **The source text in the file becomes the fingerprint,** so a translation of an older text arrives already marked outdated.
7. **Exports pick** the tables, the languages, and all entries, only missing ones, only outdated ones, or both.

### Pseudo-localization

A `Pseudo` language made from the source text, accented, a third longer and bracketed, so hardcoded text, truncation and overflow show up before any translation exists: `Buy now` shows as `[Bûý ñöŵ one]`.

1. **Arguments, rich text tags and escapes stay intact,** and every text is checked like a real translation.
2. **Accents come from Latin-1 and Latin Extended-A,** which nearly every font has.
3. **The editor's preview always offers it,** Play Mode and development builds list it as one more language, and release builds never ship it.

### Character sets

**Tools > ReactiveLocalizer > Character Sets** lists every character a language can show, for the font atlases of TextMeshPro's Font Asset Creator.

1. **Everything that can appear:** the language's own text, the fallback text its missing entries show, every plural and select form, and its digits and number symbols, without rich text tags or control characters.
2. **Several languages at once** give the set a shared font needs, and printable ASCII or extra characters can be added for text that arguments bring at runtime.
3. **Copied or saved** in code point order. `CharacterSets.Collect` is the editor API behind it.

### Renames and moves

1. **Renaming an entry** updates every language file of its table and leaves `@formerly OldName`, so scenes, data and code that use the old name keep working.
2. **Moving an entry** to another table leaves `@formerly OldTable.OldName`, so it resolves at runtime, in saved references and in generated code.
3. **Validation lists what still uses each alias,** so it can be removed once nothing does.

### Safe by construction

1. **A missing key shows as `[Shop.Purchase]`** in every build and is reported once per key, as an error, so a test that touches one fails. `MissingKeyCount` lets a smoke test assert there are none.
2. **Lookups are lock-free from any thread.** Changes requested from any thread apply in order on the main thread, where bindings are notified, and nothing needs threads of its own, so WebGL works the same.
3. **Damaged data fails with a reason,** never a crash: compiled tables are checked on load, and a file with errors costs only its broken lines.
4. **Idle localization costs one flag check per frame.**

### Samples

Samples are thin adapters with their own folders and asmdefs, imported from the Package Manager, each compiling only when its package is installed.

| Sample | What it adds |
|---|---|
| Quick Start | `GlobalLocalizer`, a project-wide access point that remembers the player's language and maps the OS language; a language picker; `LocalizedBehaviour`, the base of the UI components; a demo scene in English, Russian and an invented Pirate language |
| TextMeshPro | `LocalizedTextMeshPro`, which shows text without creating strings; `LanguageFonts`, a font per language; a check of which characters each font can't draw; a *Localize* action in the text's context menu |
| UI Toolkit | `LocalizedTextBinding` for UXML and UI Builder on Unity 6, `BindText` from code and `LocalizedDocument` on every version |
| uGUI Text | `LocalizedLegacyText` for legacy `Text`, with its own *Localize* action |
| Addressables | `AddressablesTableSource` for `@delivery Custom` tables, and a menu that sets up their addresses |
| VContainer | `LocalizerInstaller`, which registers a catalog's localizer and initializes it as the container starts |
| R3 | Observables of text, of messages fed by other observables, and of the current language |
| Google Sheets | Two-way sync with a private spreadsheet through an Apps Script that answers only a secret token |
| Mods | `ModTranslations`, fan translations from the player's mods folder that patch shipped languages and add new ones, with an example mod |
| Editor Tool | An editor window localized with a catalog of its own, as tool and package authors ship it |
| Unity Localization Migration | Converts locales, string tables, comments and plain placeholders, and lists what needs converting by hand |

### Coding agents

**Tools > ReactiveLocalizer > Write Agent Guide** adds a short section to the project's `AGENTS.md` or `CLAUDE.md`, since agents read the project rather than the package cache: where the catalogs and generated keys are, how to add and rename entries, the naming rules, and what never to edit by hand. Writing it again updates the section in place.

### Quality

1. **675 tests** run on Unity 2022.3 and Unity 6, and the 593 of them that cover the pure assemblies also run under plain .NET on every push.
2. **Allocation tests** hold cached lookups, binding updates, unchanged arguments and formatting into a buffer to zero allocations.
3. **Generated tests** check the plural rules against CLDR's sample numbers for every form of every language, and golden values pin the hash that saved references depend on.
4. **Player builds** for WebGL, Android, iOS, macOS and Linux run on every pull request, catching what only IL2CPP and code stripping break.
5. **Performance tests** measure lookups, formatting, language switches and huge tables; the numbers are in [PERFORMANCE.md](PERFORMANCE.md).

### Why it works this way

The choices that shape the package, and what they were chosen over:

| Instead of | ReactiveLocalizer uses | Because |
|---|---|---|
| Enums as keys | Names, hashed once | Unity saves enums as integers, so adding or reordering members silently re-points every saved reference, and every new key would need a recompile |
| An enum of languages | Language records in the catalog | Mods, DLC and downloaded translations can't add enum members |
| ScriptableObject tables | Text files per table and language | Long YAML lists conflict constantly in merges, import slowly, and are opaque to reviews and agents |
| A hidden ID on every entry | The table and key | IDs would have to be invented for every hand edit, and two branches adding the same key would collide when merged |
| One file per table with every language | One file per table and language | A translator's delivery would touch every line developers edit, and adding a language would rewrite every file |
| A Roslyn source generator | A plain generated file | Unity limits a generator's inputs to specially named files, and its output is invisible to reviews and agents |
| `CultureInfo` | CLDR data generated into the core | Platform-specific failures in IL2CPP builds would make the same message format differently on different devices |
| A singleton in the package | The Quick Start sample's access point | Global access is a project's choice, and a DI container hands out its own instance |
| Updating bindings over several frames | Every binding in one frame | Spreading a switch trades a one-frame hitch for several frames of mixed languages; the cost of a switch is loading and rebuilding text meshes, not lookups |
| A log line on every missing read | One error per missing key | Stack traces and strings every frame would flood logs and crash reporting, and bury real problems |
| MessageFormat 2 | MessageFormat 1 | MessageFormat 2 is a stable specification, but translation tools don't support it yet; messages compile at import, so it can be added as a second syntax |
