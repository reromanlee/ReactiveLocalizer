# ReactiveLocalizer: Design Specification

Locked on 2026-10-04 after a full design review. It supersedes `ReactiveLocalizer (initial concept).md` and `ReactiveLocalizer (revised concept).md`, which stay as history. Implementation follows this document, and any change to it is a deliberate decision.

## 1. Principles

1. **Headless core.** The package defines, organizes and retrieves localized text. Everything that touches a concrete package (TextMeshPro, uGUI, UI Toolkit glue, Addressables, DI containers, R3) or a project policy (detecting the OS language, saving the player's choice, global access) lives in project code or in `Samples~`.
2. **No dependencies.** `package.json` declares none. Integration code compiles only when its package is present, so importing a sample can never break compilation.
3. **One data model at every scale.** A prototype and a production project with hundreds of thousands of entries use the same files, runtime and tools. Growing never needs a migration.
4. **Foolproof by construction.** Mistakes are made impossible where possible and loud everywhere else. Nothing crashes, nothing blocks, and every problem is reported with its location.
5. **A precise performance contract.** Steady-state lookups and binding updates allocate nothing, and idle localization costs no CPU. Formatted output allocates one string at the UI boundary, or nothing when written into a caller's buffer. Tests enforce all of it.
6. **Text files are the truth.** They are diffable, mergeable, reviewable, and editable by humans and AI agents. Everything else is compiled from them.

## 2. Scope

1. v1 localizes strings with ICU MessageFormat. The key, table and loading model is independent of the value type, so asset tables (sprites, voice-over, video) can be added later without reshaping it.
2. The package is standalone and has no dependency on `com.unity.localization`; it replaces it. LockInGoals is the first migration target.
3. The minimum Unity version is 2022.3, which stays the preferred version for WebGL projects. CI covers the oldest and the newest supported Unity.
4. Out of scope for the core:
   1. **Right-to-left shaping.** UI Toolkit's Advanced Text Generator or RTLTMPro shape the text. The core only carries a `Direction` hint, and samples may post-process text per language.
   2. **Built-in machine translation.** It is replaced by the LLM handoff (section 13).
   3. **Date, time and currency formatting.** These go through formatter functions the project registers (section 10).

## 3. Package structure

| Assembly | Folder | References | Contents |
|---|---|---|---|
| `reromanlee.ReactiveLocalizer` | `UnityPackage/Core/` | None, `noEngineReferences` | Catalogs, languages, identity, compiled tables, the text parser, lookup, fallback, ICU, CLDR data, reactive bindings, loading coordination, the host contract |
| `reromanlee.ReactiveLocalizer.Unity` | `UnityPackage/Unity/` | Core, UnityEngine only | `UnityHost`, built-in table sources, the frame hook, Console diagnostics, destroyed-object checks, `EntryReference` |
| `reromanlee.ReactiveLocalizer.Authoring` | `UnityPackage/Authoring/` | Core, `noEngineReferences`, Editor platform only | The canonical writer, validator, code emitter, CSV, XLSX, XLIFF and LLM exchange, pseudo-localization, character sets |
| `reromanlee.ReactiveLocalizer.Editor` | `UnityPackage/Editor/` | All of the above, UnityEditor | Importers, build steps, the table window, the inspector field, the Scene view overlay, menus |

1. Samples live in `UnityPackage/Samples~/<Sample>/`, each with its own asmdef.
2. Tests live in `UnityPackage/Tests/<Area>/`, named `reromanlee.ReactiveLocalizer.Tests.<Area>`.
3. The pure assemblies (core and authoring) and their tests also build with plain `dotnet` from `DotNet/`, whose projects link the package sources. A Unity-only API can then never slip in unnoticed, and a future .NET (NuGet) release stays possible. `DotNet/CldrGenerator` writes the core's CLDR data (section 10).
4. `UnityProject/` is the host project for tests and player builds, as in Wireframes.

## 4. Languages and catalogs

### 4.1 Languages

A language is a record the project defines. Standard codes are optional metadata, so invented and fan-made languages work as well as real ones.

| Field | Example | Purpose |
|---|---|---|
| `Name` | `English`, `PortugueseBrazil`, `Pirate` | Identity: the generated constant (`LocalizationLanguages.Pirate`), file names, the player's saved choice |
| `DisplayName` | `Português (Brasil)`, `Pirate Speak` | What a language picker shows |
| `Culture` *(optional)* | `pt-BR`, empty for invented languages | Plural rules, number formatting, the language tag in exports |
| `Fallback` *(optional)* | `English` for `Pirate` | Where missing entries come from |
| `Direction` | `LeftToRight`, `RightToLeft` | A hint for whatever renders the text |
| `Required` *(optional)* | `true` | A build fails while any entry is missing in this language |
| `Digits`, `DecimalSeparator`, `GroupSeparator` *(optional)* | `0123456789`, `,`, `\u00A0` | Number symbols that replace the culture's |

1. **The source language** is the one the project writes in, and can be any language (`@source Russian` for a Russian-first project). It defines the key set, is the last fallback for every language, and is the language the localizer starts in until project code picks another.
2. **Changing the source language later** is a tool operation, never a core change:
   1. The tool checks that the new source has every key.
   2. It swaps the roles of the files.
   3. It re-stamps the fingerprints, keeping anything that was already outdated flagged.
3. **Plural rules** come from `Culture`. Without one they come from the fallback language, and without either, every count uses ICU's `other` form.
4. **Number symbols** (decimal and grouping separators, digits) come from `Culture` the same way, and any language can override them in its catalog section with `Digits`, `DecimalSeparator` and `GroupSeparator`. An empty `GroupSeparator` turns grouping off. Arabic with Latin digits is `Culture = ar` and `Digits = 0123456789`.
5. **A culture CLDR doesn't know** is reported as a warning, and the language then takes its plural rules and number symbols from its fallback, as if it had no culture. A known culture without plural data of its own uses CLDR's root rules, as CLDR does.
6. **Runtime languages.** Languages can be registered at runtime, for example a fan translation a player drops into a mods folder. Generated constants are only a convenience for the languages known at compile time.
7. **The OS language** is mapped to a project language by project code (a switch). The Quick Start sample shows the pattern.

### 4.2 Catalogs

A catalog is a self-contained localization unit: its languages, its settings and its tables.

1. A `.catalog` file claims every table in its folder and below, the way an asmdef claims scripts.
2. Tables outside any catalog belong to the project's default catalog, the way scripts outside any asmdef end up in Assembly-CSharp.
3. The default catalog is created on first use as `Assets/Localization/Localization.catalog`. Its generated classes are `LocalizationKeys` and `LocalizationLanguages`. Creating it asks for the source language, defaulting to `English` with culture `en`.
4. The default catalog is the one at `Assets/Localization/Localization.catalog` when that file exists, so importing a sample that brings its own catalog changes nothing. Otherwise it is the only catalog in Assets outside an Editor folder. When neither applies, tables outside every catalog folder are reported instead of guessed.
5. Packages and editor tools ship their own catalog, so their tables and languages never mix with the game's.
6. A catalog inside an `Editor` folder is editor-only and is never packed into player builds.
7. Table names are unique within a catalog.

The catalog file uses the table syntax (section 5.3), with one `[Language]` section per language:

```
@source English
@namespace MyGame

[English]
DisplayName = English
Culture = en

[Russian]
DisplayName = Русский
Culture = ru
Required = true

[Pirate]
DisplayName = Pirate Speak
Fallback = English
```

## 5. Table files

### 5.1 Layout

1. Each table is stored as one file per language, named `<Table>.<Language>.lang` (for example `Shop.English.lang` and `Shop.Russian.lang`).
2. Identity comes from the file name, so folders are free-form. A table can sit next to the feature that owns it, or every table can live in one place.
3. A translation file holds only translated entries. A missing key means untranslated, and an empty value means intentionally empty. Adding a key changes only the source-language file.
4. Every table uses the same layout. There is no per-table choice between one combined file and split files.

### 5.2 Identity

1. An entry is identified by `Table` + `Key` everywhere: in files, generated code, serialized component fields and lookups from project data. Files carry no hidden IDs.
2. Names (catalogs, tables, keys, languages) are ASCII letters, digits and underscores, start with a letter, and are unique regardless of case. Any table can therefore turn on code generation at any time. Numeric data IDs need a prefix, such as `Item10234`.
3. The PascalCase, no-underscore convention from LockInGoals is an optional validator rule, not a package-wide restriction.
4. At runtime every name becomes a 64-bit FNV-1a hash of its UTF-16LE bytes, with ASCII letters lowercased first. Lookups therefore ignore case, which matches the uniqueness rule. Generated keys compute their hashes once, when their class initializes, never per lookup. String lookups hash a `ReadOnlySpan<char>` without allocating. Import fails if two names in one scope produce the same hash. Golden-value tests pin the function.
5. **Renames never break anything.**
   1. The rename tool updates every language file of the table.
   2. It leaves `@formerly OldName` on the entry. Moving an entry to another table leaves `@formerly OldTable.OldName`.
   3. Old references (scenes, data, the generated `[Obsolete]` forwarder in code) keep resolving.
   4. The validator lists what still uses each alias, so the alias can be removed once nothing does.

### 5.3 Format

A strict line-based `Key = Value` format, modeled on `.properties` and INI files.

`Shop.English.lang`:

```
@loading Preload
@delivery Embedded

@formerly CoinsLabel
CoinBalance = You have {coins, plural, one {# coin} other {# coins}}.

Disclaimer = Prices include VAT.\nRefunds within 14 days.

# Button that confirms buying the selected item.
@maximumLength 16
Purchase = Buy
```

`Shop.Russian.lang`:

```
CoinBalance [8b0e47] = У вас {coins, plural, one {# монета} few {# монеты} many {# монет} other {# монеты}}.
Purchase [3fa2c1] = Купить
```

1. **One entry per line.** The value is everything after the first `=`, and needs no quotes.
2. **Whitespace** around `=` and at both ends of the line is trimmed. Intentional spaces at the ends of a value are written as escapes.
3. **Escapes:** `\n`, `\t`, `\\` and `\uXXXX`. An unknown escape is reported and kept literally.
4. **Values are always plain text** and are never interpreted as types.
5. **Characters that are syntax elsewhere are plain text inside a value.** Only the first `=` splits the line. Only lines that *start* with `#` or `@` are comments or attributes. The fingerprint tag can only appear before the `=`.
6. **Comments.** `#` lines are comments. In the source file, comments above an entry are the context translators see in the grid and in exports.
7. **Attributes.** `@` lines are attributes attached to the next entry: `@formerly`, `@maximumLength`.
8. **Table settings** are attributes at the top of the source file, before the first entry: `@loading`, `@delivery`, `@generateCode`.
9. **Fingerprints.** A translation line carries a fingerprint of the source text it was made from: `Key [8b0e47] = ...`. The tag is the first 6 hex digits of the 64-bit hash of the source text.
   1. When the source changes, the fingerprints stop matching and those translations are flagged as outdated.
   2. The tool writes the tags itself. A line added by hand without one counts as unverified.
10. **The canonical writer** always produces the same output:
    1. keys sorted in natural order (`Line9` before `Line10`), with their comments and attributes moving with them;
    2. UTF-8 without BOM;
    3. newlines escaped;
    4. invisible characters (non-breaking spaces, zero-width and bidi marks) written as `\u` escapes so they show up in reviews;
    5. the file's existing line endings kept, and LF for new files.
11. **ICU syntax** inside values follows ICU's own rules. A literal `{` is written `'{'`, and an ordinary apostrophe like in `Don't` needs nothing. Entries created from text a project already shows, such as the *Localize* action does, are quoted so they show exactly that text.
12. **Editing outside the tool** (merge conflict resolution, AI agents, hand fixes) is supported and validated, not forbidden. The editor is the primary path but not the only one.

### 5.4 Foolproofing

1. **Keys exist only in the source language.** A key that appears in a translation file but not in the source is an *orphan*.
   1. The importer reports it with file and line and leaves it out of the build.
   2. The editor offers a fix. When an orphan looks like a misspelling of a missing key (`Purchse` next to a missing `Purchase`), the fix suggests renaming it.
2. **Translation files re-validate automatically** whenever their source file changes.
3. **Entries are matched by key, never by position.**
4. **The importer reports problems with file, line and column:** duplicate keys, leftover merge-conflict markers, malformed lines and unknown languages.
5. **A missing translation is a visible state:** highlighted in the grid, counted by the validator, and falling back at runtime.
6. **Operations that touch several files** (add, rename, move, delete) update every language file in one step, with Undo.

## 6. Compiled tables and memory

1. Each table-and-language file compiles into one binary blob:
   1. a versioned header (format version, catalog, table, language), so a format change or a mismatched file is detected, never misread;
   2. the source keys hash: a hash of the source-language file's keys, carried by the source table and by every translation that has all of them, and zero otherwise;
   3. sorted 64-bit key hashes, aliases included, searched by binary search;
   4. start positions and lengths into one UTF-16 character buffer that holds every value;
   5. pre-parsed ICU instructions, only for entries with arguments.
2. A translation takes over the source text's aliases, so a renamed entry resolves in every language without its fallback.
3. A loaded table is a handful of arrays, whatever its entry count.
4. **Strings are created on first use.**
   1. The first request for an entry creates its `string` and caches it.
   2. Every later read returns the same instance without allocating, including from several threads at once.
   3. A 50,000-line dialogue table only ever creates strings for the lines actually shown.
5. **Callers that need zero allocations** read values as `ReadOnlyMemory<char>`, and bind them with `BindCharacters`. The TextMeshPro sample passes them to `SetCharArray` through `MemoryMarshal.TryGetArray` without copying.
6. **UTF-16, not UTF-8.** UTF-16 is .NET's native string encoding, so creating a string is one copy and nothing needs decoding. UTF-8 would only save memory for Latin-alphabet text, and costs more for Chinese, Japanese and Korean.

## 7. The localizer runtime

### 7.1 Host

1. There is one implementation, the core `Localizer`. What changes per platform is its host, which provides:
   1. the table sources, in priority order;
   2. the once-per-frame hook that applies queued changes;
   3. the diagnostics sink;
   4. the check that tells whether a target object was destroyed.
2. The core defines `ILocalizerHost`, and the Unity layer implements it as `UnityHost`:

   ```csharp
   Localizer localizer = new Localizer(LocalizationKeys.CatalogKey, new UnityHost());
   await localizer.InitializeAsync();
   ```

3. With embedded tables, `InitializeAsync` finishes within the call. A prototype can read text on its first frame without awaiting anything.
4. Extra sources stack in priority order. `new UnityHost(new FolderTableSource(LocalizationKeys.CatalogKey, path))` asks a mods folder first, so a fan translation can also patch languages the game already ships: its entries come first, and the game's fill in the rest (section 8).
5. A .NET server would pass a host with a `FolderTableSource`, and a test would pass one that holds tables in memory.
6. Bridges are named by their role (`UnityHost`, `EmbeddedTableSource`, `EntryReference`), never as another "Localizer".

### 7.2 Threading

1. **Reads** work from any thread without locks. The localizer publishes its state (the current language and the loaded tables) as one object that never changes after it is built, and replaces it by swapping a single reference. A read takes that object once, so it can never see a half-switched language.
2. **Changes** can be requested from any thread and are applied in order on the host thread: Unity's main thread, through the frame hook. That covers switching the language, loading or unloading tables, and registering a language at runtime with `RegisterLanguage`.
3. **Bindings are notified** on that same thread, so UI code never has to switch threads itself.
4. **Nothing requires extra threads.** On WebGL everything runs on the main thread, and loading uses the platform's own async loading.
5. **Idle cost.** While idle, the frame hook costs one flag check per frame.

### 7.3 Lookup and fallback

1. A lookup resolves the entry in the current language. If it's missing there, the lookup walks the fallback chain and ends at the source language.
2. Synchronous reads never block. A read from an `OnDemand` table that isn't loaded returns empty text, with one warning per table.
3. Async operations (startup, language switches, explicit loads) return `Task`. They are rare, several systems can await the same switch, and `Task` works on 2022.3 and in plain .NET. UniTask and `Awaitable` users convert with one call.
4. Failures never throw into game code. A task's result reports what failed.

### 7.4 What shows up when text can't be shown

| Situation | Player sees | Developer gets |
|---|---|---|
| Entry missing in the current language | Text from the fallback chain, ending at the source | Counted by the validator |
| Key doesn't exist (deleted, stale reference, typo in data) | `[Shop.Purchase]`, in every build | One error per unique key |
| Table still loading | The binding keeps its previous text; empty only on the very first load | Nothing |
| An `OnDemand` table read while nothing holds it | Empty text | One warning per table |
| Table failed to load | Fallback-language text, loaded as a recovery | An error with the reason; the task reports the failure |
| Broken ICU in a translation | Fallback-language text | An import error with file, line and column |
| Broken ICU in the source language | The text as written | An import error with file, line and column |
| A message missing an argument, or given one of the wrong kind | `{coins}` for a missing one; a wrong one as it is, choosing the `other` form | One error per unique problem |
| No formatter registered for a type such as `{deadline, date}` | The value as it is, dates as `yyyy-MM-dd HH:mm:ss` | One error per argument |
| An entry with arguments read as plain text | `{coins}` in place of each argument | One warning per entry |

1. **One error per unique missing key per session.** Errors are used rather than warnings, so Unity Test Framework fails any test that touches a missing key.
2. **Missing-key counters** can be read from code, so a pre-release smoke test can assert "zero missing keys".
3. **The build gate** fails builds whose scenes, prefabs or assets reference a key that doesn't exist (section 12.2).

## 8. Loading and delivery

1. **Only the current language is loaded.** Fallback languages are loaded only for tables with gaps in the current language. A fully translated language costs no extra memory.
   1. A table is loaded one language of its fallback chain at a time, and stops at the first table that has every key of the source language: one whose source keys hash (section 6) matches the hash the catalog carries for the table.
   2. A translation compiled for other source keys, such as remote content from an older release or a mod compiled at runtime, never matches, so its fallbacks always load and a newer key never shows as missing.
2. **Sources stack.** For each language, every source is asked in priority order, and each table found is searched before the ones after it, until one has every key. A mod's partial patch of Russian therefore comes first, the game's Russian fills in the rest, and English isn't loaded at all.
3. **Loading mode,** set per table with `@loading`:
   1. **`Preload` (default):** loaded together with the language, so synchronous reads always work.
   2. **`OnDemand`:** loaded while something holds it, and unloaded two host updates after nothing does, so a panel toggled within a frame keeps its table. Bindings hold their tables automatically, and code holds one with `localizer.HoldTable(...)`, which returns a disposable `TableHandle` whose `WhenLoaded` task completes once the table can be read.
4. **A binding that needs an unloaded table** starts the load and updates when the data arrives, keeping its previous text meanwhile.
5. **Language switch:**
   1. load the new language for every preloaded table and every held one;
   2. swap everything in one frame;
   3. release what the new language doesn't use.

   Rapid switches collapse: only the latest request loads, loads already started for an earlier one are shared rather than repeated, and every caller's task completes once it lands. A table held while a switch loads joins it. `LanguageChanging` and `LanguageChanged` events serve projects that want a loading screen.
6. **Delivery,** set per table with `@delivery`:
   1. **In the editor,** tables are read straight from the imported assets. There is no build step, and they're always current.
   2. **`Embedded` (default):** a build step packs the table inside the build, readable synchronously on every platform.
   3. **`Streaming`:** a build step packs the table into StreamingAssets, read on demand by `StreamingTableSource`, which keeps huge content out of memory and, on WebGL, out of the initial download. Files are read on a thread pool thread where StreamingAssets is a folder, and through UnityWebRequest where it's a URL (Android, WebGL), which needs the built-in Unity Web Request module. A list of the packed files sits next to the catalog, so a table a language doesn't have is never requested.
   4. **Anything else** (Addressables, a CDN) plugs into the same table-source interface. A source can attach warnings to what it delivers, which reach the host's diagnostics.
7. **Mods and downloaded translations** come from `FolderTableSource`: `<Table>.<Language>.lang` files anywhere under a folder, compiled when they're asked for, with their problems reported by file and line. The language sections of the folder's `.catalog` files define new languages, which `localizer.RegisterLanguage(...)` adds at runtime, before or after initialization.

## 9. Reactive layer

1. **`Bind` is the primitive everything else uses:**

   ```csharp
   TextBinding binding = localizer.Bind(LocalizationKeys.MainMenu.Play, label, static (label, text) => label.text = text);
   binding.Dispose();
   ```

   1. It applies the text immediately, then again whenever the text changes: a language switch, an `OnDemand` table arriving, a table file edited in the editor (live, in Play and Edit Mode, arriving with the editor tooling of step 4), or new arguments via `binding.SetMessage(...)`, which formats only when the arguments differ, so calling it every frame costs nothing.
   2. Binding and updating allocate nothing. The handle is a struct, and the callback is a static lambda that receives its target as state. `Bind` hands the callback a `string`; `BindCharacters` hands it `ReadOnlyMemory<char>`, valid during the call, and formats messages into a buffer it reuses, so even a changing message allocates nothing.
   3. Disposing a stale or copied handle is a safe no-op.
   4. A binding keeps its `OnDemand` table loaded while it's alive.
   5. A callback that throws is reported and skipped, and the remaining bindings still update. That's the same rule as EventAggregator.
   6. Bindings whose Unity target was destroyed without being disposed are released automatically and reported.
2. **`ReactiveText`** wraps a binding with `Value`, a `Changed` event and `INotifyPropertyChanged`, for view models, UI Toolkit data binding and the R3 adapter. Its events are raised only when the text actually differs.
3. **Refreshing inactive text costs nothing.** Component samples bind in `OnEnable` and dispose in `OnDisable`, so a language switch only touches what's active, and anything enabled later reads the current language as it binds.
4. **No time-sliced update mode in v1.** It can be added if profiling ever shows a need.

## 10. Messages

1. **The syntax is classic ICU MessageFormat (MF1).**
   1. MF2 has been a stable Unicode specification since March 2025, but as of May 2026 the major translation platforms didn't support it, and the major JavaScript libraries were still MF1-only.
   2. Messages compile into instructions at import, so MF2 can be added later as a second syntax without touching the runtime.
2. **Supported syntax:**
   1. `{name}`;
   2. `{n, number}` with the `integer` and `percent` styles;
   3. `{n, plural, …}` with `=N`, `offset:` and `#`;
   4. `{n, selectordinal, …}` and `{x, select, …}`;
   5. nesting and apostrophe quoting, which follows ICU: an apostrophe only starts quoted text before a brace, or before `#` inside a plural form.

   Arguments are named, since the names become the parameters of generated code. ICU's numbered arguments (`{0}`) and its deprecated `choice` are rejected with an explanation.
3. **Dates, times and currency** are not built in. `{deadline, date}` calls the formatter the project registers for the type: `localizer.SetFormatter("date", formatter)`.
   1. An `ArgumentFormatter` writes into a span it's given, so formatting allocates nothing, and is called again with more room when it reports the span too small.
   2. It receives the style text (`short` in `{deadline, date, short}`) and the language of the text, whose culture names the conventions to follow.
   3. A missing or throwing formatter never breaks the text; it's reported once.
4. **Language data comes from CLDR (the Unicode Common Locale Data Repository).**
   1. Plural rules (cardinal and ordinal) and number symbols are generated from CLDR into the core. The rules become code, one case per distinct rule set; the symbols are kept only for the locales whose symbols differ from the locale their tag shortens to, because a lookup shortens the tag (`de-CH`, then `de`) until it finds one.
   2. Nothing depends on `CultureInfo`, which has a history of platform-specific failures in IL2CPP builds. Output is identical on every device.
   3. The generator is a repository tool pinned to one CLDR release (48.2.3), and its output is committed. Moving to another release is changing the version, running it, and reviewing the diff.
5. **Numbers** are grouped as the language groups them (including Indian grouping and minimum grouping digits, so Spanish writes 1234 but 12.345), show up to three fraction digits rounded half to even, and use the language's digits. A plural form is chosen from the digits shown, so 1.0004 shows as 1 and takes the `one` form.
6. **Text found in a fallback language** formats with that language's plural rules and symbols, because the words around the number are in that language.
7. **At runtime** an `EntryMessage` struct carries the entry and its arguments, up to four without allocating. Arguments are matched by name, so a translation may use them in any order. `Get` returns the formatted string, `TryFormat` writes into a caller's buffer without allocating, and `Bind` keeps a message current.
8. **Import checks:**
   1. syntax errors, reported with line and column, counting the escapes written before them;
   2. every translation uses the same argument names as the source, each in a way its value supports: a number may also be shown plainly, but text can't choose a plural form. A translation that doesn't match is left out, so it shows in its fallback language, with an error;
   3. plural messages need `other`, and get a warning when they lack a form their language uses (Russian: one, few, many, other; English: one, other), unless exact forms stand in for it, as `=1` does for English `one`, and when they have a form their language never uses;
   4. likely mistakes are warned about: `=#` inside a plural form, which turns a rich-text color into the number, and `#` inside a select within a plural, which ICU shows as is.

## 11. Code generation

1. **The editor writes one plain `.cs` file per catalog,** next to the catalog file, so it compiles into whichever assembly owns that folder.
   1. It is not a Roslyn source generator: in Unity those only read files under `Assets` with a special `.additionalfile` name, and their output is invisible to code review and AI agents.
   2. The namespace is `@namespace` from the catalog. Without it, the owning asmdef's root namespace, then the project's root namespace setting, then none.
2. **It runs automatically.**
   1. The file is regenerated when keys, aliases or argument lists change.
   2. Text edits never touch it, so text edits never cause a recompile.
   3. A result identical to the existing file is never rewritten.
   4. While a source text's message has errors, the file keeps its current members, so a typo in a text never breaks the code that calls it.
3. **Shape:**

   ```csharp
   LocalizationKeys.CatalogKey                     // the catalog, for new Localizer(...)
   LocalizationKeys.Shop.TableKey                  // the table, for TableHandle and exports
   LocalizationKeys.Shop.Purchase                  // plain entry: a field
   LocalizationKeys.Shop.CoinBalance(coins: 5)     // entry with arguments: a method returning EntryMessage
   LocalizationKeys.Shop.BuyButton                 // alias: an [Obsolete] forwarder to Purchase
   LocalizationLanguages.Russian                   // languages, for the project's OS-language switch
   ```

   1. **Argument types come from the source text.** Plural and number arguments take a `MessageNumber`, which every numeric type converts to without boxing. Select arguments take strings. Anything shown as is or through a formatter takes a `MessageValue`.
   2. **Parameters are sorted by name,** so rewording a text never reorders them.
   3. **Argument changes break call sites at compile time.** That covers renaming, removing or adding an argument in the source.
   4. The validator reserves `TableKey` and `CatalogKey` as entry names.
4. **Opting out.** A table opts out with `@generateCode false`, for example a 50,000-line dialogue table referenced from data rather than code. It stays reachable by name (`localizer.Get(dialogueTable, line.Key)`, hashed without allocating).
5. **Read-only packages.** Catalogs inside read-only packages ship their generated file. The tool never writes into a package it can't modify.

## 12. Editor tooling

### 12.1 Importers

1. `ScriptedImporter`s compile `.lang` and `.catalog` files.
2. Translation files declare a dependency on their source file, so they re-validate whenever it changes, and every table file on its catalog, whose cultures decide which plural forms are checked. A translation imported before its source file existed is imported again once it arrives.

### 12.2 Validation and the build gate

1. Validation runs on every import and on demand (`Tools/ReactiveLocalizer/Validate`).
2. **Errors fail the build:**
   1. malformed files and merge-conflict markers;
   2. duplicate keys and hash collisions;
   3. orphans and broken ICU;
   4. argument mismatches between languages;
   5. missing entries in required languages;
   6. references to nonexistent keys from scenes, prefabs or assets in the build.
3. **Warnings don't fail the build:**
   1. outdated or unverified translations;
   2. missing entries in other languages;
   3. entries over their maximum length;
   4. unused aliases;
   5. naming-convention rules, when enabled.

### 12.3 Table window (`Window/ReactiveLocalizer/Tables`)

1. **Built on UI Toolkit's `MultiColumnListView`,** which only renders visible rows, so 100,000-entry tables scroll smoothly.
2. **Sidebar:** catalogs and their tables, with badges for missing, outdated and broken entries.
3. **Grid:** one row per entry, with columns for the key, the translator context, then one per language. Each language can be shown or hidden.
4. **Detail pane** for the selected entry: every language stacked vertically with full multi-line editors. This is where long paragraphs live. It also has:
   1. an ICU preview with editable sample values (for example, Russian with `1`, `2`, `5` and `21`);
   2. the attributes;
   3. the outdated state, with *Show source change* and *Mark as current*.
5. **Status tints** for missing, outdated, broken ICU, over maximum length and orphaned. Filters for *missing in X*, *outdated* and *errors*. Search covers keys, text and comments.
6. **Add, rename, delete and move to another table,** all with Undo through a stand-in object registered with Unity's Undo. Moving leaves a cross-table alias.
7. **Edits save when committed** (Enter or leaving the cell). There's no unsaved state, and bound text updates live.
8. **Generated code is written when the user leaves the window or after a short idle period,** never in the middle of typing, and waits until Play Mode ends.

### 12.4 Inspector field

1. **`EntryReference`** is the serializable field type. It lives in the Unity layer and stores the catalog, table and key as names. The Inspector fills the catalog in when an entry is picked, so a reusable component works with whichever catalog its entry comes from. `[EntryCatalog(typeof(ToolWindowKeys))]` limits a field's dropdown to one catalog.
2. **`CatalogReference`** is the field type for components that work with a whole catalog, such as a language picker. Empty means the default catalog.
3. **The field shows `Shop ▾ Purchase`** with a text preview in the current preview language.
4. **Clicking it opens a search popup.** It searches keys *and* text, shows recent picks first, and only renders visible results.
5. **Creating entries on the spot.** When nothing matches, the popup offers to create the entry.
   1. The table defaults to the one named after the prefab or scene being edited, then the last used one, or the user types a new table name.
   2. The key is suggested from the GameObject's name, with UI suffixes stripped (`TitleLabel` → `Title`). It's never suggested from the English text, because entries are named after their role.
   3. Creating an entry writes only the source file, so there's no recompile.
   4. `EntryAuthoring` is the public editor API behind it, so samples and project tools create entries the same way.
6. **Broken references show red.** When a renamed key's alias resolves, the field offers to update itself.
7. **The field is drawn in both UI Toolkit and IMGUI inspectors.**

### 12.5 Scene view overlay

The overlay switches the preview language, and every bound text in the open scenes updates live in Edit Mode.

### 12.6 Menus

1. `Window/ReactiveLocalizer/Tables`
2. `Tools/ReactiveLocalizer/…` (validate, import, export, character sets, write the agent guide)
3. `Assets/Create/ReactiveLocalizer/…` (catalog, table)

## 13. Exchange formats

1. **XLSX:** one workbook, one sheet per table, with the grid's columns (key, context, maximum length, source, languages). The authoring assembly reads and writes the zip and XML itself, with no dependencies.
2. **CSV:** one file per table, RFC 4180. It's saved as UTF-8 with a BOM so Excel doesn't garble non-Latin text, and import detects a comma, semicolon or tab separator.
3. **XLIFF 1.2 and 2.0:** one file per target language.
4. **LLM handoff:**
   1. The tool exports missing or outdated entries as a text bundle. A rules header (keep ICU syntax, keep `\n`, respect the maximum length) is followed by the entries in `.lang` syntax, with their context and fingerprint tags.
   2. The model's reply is imported from the clipboard.
   3. No API keys and no provider integration are needed.
5. **Google Sheets:** two-way sync, shipped as a sample. It works through an Apps Script the sample provides, with a secret token, so the sheet stays private. The "publish to web" shortcut is never used, because it makes the sheet public.
6. **Import rules, the same for every format:**
   1. Imports change translations only by default. They never create, rename or delete keys. Rows with unknown keys are reported as orphans, and an empty cell never erases an existing translation.
   2. Changes to the source column are ignored unless *import source changes* is ticked.
   3. The source text in the file is what the translator saw, so it becomes the fingerprint. If the source changed since the export, the imported translation arrives already marked outdated.
   4. Everything goes through the same ICU checks as the files.
   5. A preview shows what will change (updated, new, outdated, rejected) before anything is written.
7. **Export options:** which tables, which languages, and all entries or only missing or outdated ones.

## 14. Developer tools

1. **Pseudo-localization:** a virtual language generated on the fly from the source text and never stored in files.
   1. It's accented so hardcoded strings stand out, padded by about 35% so overflow shows up, and bracketed so truncation and glued-together strings are obvious.
   2. ICU syntax, rich text tags and escapes stay intact.
   3. It's available in the editor preview and in development builds, and excluded from release builds.
2. **Character sets:** for each language, the exact set of characters any table can display (literal text, plural forms, number symbols, digits, without rich text tags). They're exported to a file or the clipboard for TextMeshPro's Font Asset Creator.
3. **Font coverage check** (TextMeshPro sample): lists every character a language's assigned font would render as an empty box.

## 15. Samples

Each sample is a thin adapter with its own folder and asmdef. Code that integrates with another package compiles only when that package is installed.

| Sample | Contents |
|---|---|
| Quick Start | The `GlobalLocalizer` access point, which a DI setup can also hand its own instance; saving the chosen language; a template for mapping the OS language; a language picker; a demo scene |
| TextMeshPro | A component with the zero-allocation path, a *Localize* context-menu action that turns the current text into an entry and wires the component, fonts per language, the font coverage check |
| uGUI Text | A component for legacy `Text` |
| UI Toolkit | A data binding declared in UXML (Unity 6), plus a code-only binding for 2022.3 |
| Addressables | `AddressablesTableSource` |
| VContainer | An installer with async initialization |
| R3 | Observables built from `ReactiveText` and bindings |
| Google Sheets | Two-way sync through Apps Script |
| Editor Tool | An `EditorWindow` localized with its own catalog, for tool authors |
| Mods | Fan translations read from a mods folder through the core's `FolderTableSource`, with their languages registered at runtime |
| Unity Localization Migration | Converts locales to languages and string table collections to `.lang` files, converts simple Smart Format placeholders to ICU, and flags complex ones for review |

1. **UniTask needs no sample:** it can already await `Task` directly.
2. **UI samples find the localizer through Quick Start's access point,** so import Quick Start first.
3. **In Edit Mode,** the access point returns the editor's preview instance, so components show the preview language live.

## 16. Naming reference

| Concept | Name |
|---|---|
| Table files | `Shop.English.lang` |
| Catalog files | `Localization.catalog` |
| The localizer | `Localizer`, `ILocalizer` |
| Host | `ILocalizerHost`, `UnityHost` |
| Identity handles | `CatalogKey`, `TableKey`, `EntryKey`, `LanguageKey` |
| Language record | `LanguageInfo` |
| Entry with arguments | `EntryMessage`, `MessageArgument`, `MessageValue`, `MessageNumber` |
| Formatter of a registered type | `ArgumentFormatter` |
| Binding and observable | `Bind`, `BindCharacters`, `TextBinding` (struct), `ReactiveText` (class) |
| On-demand table hold | `HoldTable`, `TableHandle` (struct) |
| Runtime languages | `RegisterLanguage` |
| Table sources | `EmbeddedTableSource`, `StreamingTableSource`, `FolderTableSource`, and in samples `AddressablesTableSource` |
| Inspector fields | `EntryReference`, `CatalogReference`, `[EntryCatalog(typeof(...))]` |
| Editor authoring API | `EntryAuthoring` |
| Generated code | `LocalizationKeys`, `LocalizationLanguages` |
| Sample access point | `GlobalLocalizer.Instance` |
| File attributes | `@source`, `@namespace`, `@formerly`, `@maximumLength`, `@loading`, `@delivery`, `@generateCode` |

1. Names use full words, one suffix per family of types, and lowerCamelCase for file attributes so they never look like keys.
2. They avoid names projects are likely to have already, like `Language`, `Table` or `Catalog`.
3. They avoid Unity Localization's type names (`LocalizedString`, `LocalizedReference`, `Locale`, `StringTable`, `TableReference`), so both packages can coexist while a project migrates.

## 17. Quality

1. **Fast lane:** `dotnet test` on the core and authoring assemblies on every push. It takes seconds and needs no Unity license.
2. **Unity lane,** mirroring Wireframes:
   1. GameCI tests in Edit and Play Mode on the oldest (2022.3) and newest Unity;
   2. compiler warnings from the package fail the run;
   3. player builds for WebGL, Android, iOS, macOS and Linux catch problems that only show up in IL2CPP builds or after code stripping.
3. **What the tests cover:**
   1. reading then writing a file gives byte-identical output, and randomized input (escapes, invisible characters, merge markers) never crashes the parser and always reports its position;
   2. plural tests are generated from CLDR's sample numbers for every plural form of every language;
   3. golden values pin the hash function, since a change would silently break every saved reference;
   4. the generated code and the XLSX, CSV and XLIFF output are compared against reference files kept in the repository, and the fast lane also compiles generated code with Roslyn as C# 9, the language Unity compiles;
   5. language switches stay all-or-nothing under concurrent reads from other threads;
   6. `OnDemand` tables load and unload as their users come and go, translations with every key load no fallback, and stacked sources patch the languages behind them;
   7. bindings whose target was destroyed get released;
   8. Play Mode works with domain reload disabled.
4. **No allocations in steady state:** `Is.Not.AllocatingGCMemory()` checks cached lookups, binding updates, re-setting identical arguments, and formatting into a caller's buffer.
5. **Performance:** Unity Performance Testing measures lookups, formatting, switching 1,000 bindings, and loading a 50,000-entry table.
   1. The numbers are published in `Documentation/PERFORMANCE.md`.
   2. A release checklist compares them with the previous release.
6. **Test style:** tests use only NUnit's `Assert.That` constraint syntax, so the same test sources run under Unity's NUnit and under plain `dotnet test`.

## 18. Documentation

1. `README.md` covers install via git URL and a 5-minute start. `Documentation/` holds `USAGE.md`, `FEATURES.md`, `PERFORMANCE.md`, `MIGRATION.md`, and `FORMAT.md`, the exact grammar of `.lang` and `.catalog` files, which are a public contract.
2. `Tools/ReactiveLocalizer/Write Agent Guide` adds a short, opt-in section to the project's `AGENTS.md` or `CLAUDE.md`, since agents read the project, not the package cache. It covers where catalogs live, how to add an entry, the naming rules, never editing generated files, and how to validate.
3. Generated files carry a header saying what produced them and how to regenerate them.
4. Public APIs carry brief, contract-focused `<summary>` documentation.
5. Migrating from Unity Localization: name the catalog `Localization` and the generated class is `LocalizationKeys`, so call sites like `LocalizationKeys.SettingsPage.Title` stay as they are and only the `using` lines change.

## 19. Build plan

Steps 0 to 2 were branches squashed into `create/working-prototype`, which reached `main` as the working prototype. Every later step is a working branch of its own, squashed into `main`.

| Step | Branch | Delivers |
|---|---|---|
| 0 | `create/working-prototype` | This specification, the package skeleton (four assemblies plus tests), both CI lanes |
| 1 | `feature/vertical-slice` | `.lang` and `.catalog` import, compiled tables, `Localizer` lookups with fallback, `Bind`, the Quick Start and TextMeshPro samples: the prototype workflow end to end |
| 2 | `feature/message-formatting` | ICU parsing and formatting, the CLDR data generator, typed `EntryMessage` code generation |
| 3 | `feature/table-loading` | `OnDemand` handles, `Streaming` delivery, language-switch coordination, the mods source |
| 4 | `feature/editor-tooling` | The table window, inspector search and creation, rename, move and aliases, validation with the build gate, the Scene view overlay |
| 5 | `feature/exchange-formats` | CSV, XLSX, XLIFF, LLM handoff |
| 6 | `feature/developer-tools` | Pseudo-localization, character sets |
| 7 | `feature/integration-samples` | The remaining samples |
| 8 | `generate/documentation` | README, `Documentation/`, release checklist, `CHANGELOG.md` |

v1.0 covers every step. The core is finished after step 3, and from step 4 onward each step only adds tooling and integrations on top.

## Appendix: rejected alternatives

| Alternative | Why it was rejected |
|---|---|
| Enums as entry keys | Unity saves enums as integers, so inserting or reordering generated members silently re-points every scene reference. Every new key would also need a recompile before use. |
| Languages as an enum | DLC, mods and remote translations can't add enum members. |
| ScriptableObject table storage | Huge YAML lists conflict constantly in merges, import slowly, and are opaque to reviews and agents. |
| Permanent random IDs per entry | An opaque ID on every line, IDs to invent for every hand edit, and two branches adding the same key collide when merged. |
| One combined file per table | A translator's delivery would touch every line developers edit, the whole table would reimport for any change, and adding a language would rewrite every file. |
| Updating bindings across several frames | It trades a one-frame hitch for several frames of mixed languages. The cost of a switch is loading and text mesh rebuilds, not lookups. |
| A singleton inside the package | It stays out of the headless core and is provided by the Quick Start sample instead. |
| A log line on every missing read | Stack trace capture and string allocations every frame, floods of crash-reporting events, and real warnings buried under repeats. |
| MessageFormat 2 | Stable since 2025, but not yet supported by translators' tools. It can be added later as a second syntax. |
| `CultureInfo` for formatting | Platform-specific failures in IL2CPP builds would make the same message format differently on different devices. |
| A Roslyn source generator for keys | Unity limits its inputs to specially named files under `Assets`, and its output is invisible to reviews and agents. |
