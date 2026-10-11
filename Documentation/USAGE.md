# Usage

How to use ReactiveLocalizer, from the first table to sources of your own. What each part does is in [FEATURES.md](FEATURES.md), and the exact rules of the files in [FORMAT.md](FORMAT.md).

### Catalogs

A catalog is a localization unit: its languages, its settings and its tables. Most games need one; packages and editor tools ship their own, so their text never mixes with the game's.

1. **Create one** with **Assets > Create > ReactiveLocalizer > Catalog**. It asks for the name, the source language, its culture and display name, and checks the culture against CLDR. A catalog named `Localization` generates the classes `LocalizationKeys` and `LocalizationLanguages`.
2. **Or let the first table create it:** in a project without a catalog, creating a table first creates the default catalog, `Assets/Localization/Localization.catalog`, asking for its source language.
3. **Add a language** with a section in the catalog file. `Culture` gives it plural rules and number formats; leave it out for an invented language, which then takes them from its `Fallback`.

```
@source English

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

`Required = true` fails builds while an entry is missing in the language. Every field is in [FORMAT.md](FORMAT.md#catalog-files).

### Tables and entries

A table is one `.lang` file per language, named `<Table>.<Language>.lang`, anywhere under its catalog's folder: next to the feature that uses it, or all in one place.

1. **Create one** with **Assets > Create > ReactiveLocalizer > Table** in a folder of the catalog, then rename `NewTable.English.lang`, or with **New Table** in the table window.
2. **Add entries** in the table window, from an Inspector field, or by writing `Key = Text` lines in the source-language file. Comments right above an entry are what translators see.
3. **Name entries after their role,** such as `PlayButton` or `Intro`, never after their text: the text changes, the role doesn't. Names use ASCII letters, digits and underscores, start with a letter, and are unique ignoring case.
4. **Translations** go in the same table window, or in `Shop.Russian.lang` holding only the translated entries. The editor's tools stamp each translation with a fingerprint of its source text, which flags it as outdated once the source changes.

```
# Shop.English.lang

# How many coins the player has, on the shop's top bar.
CoinBalance = You have {coins, plural, one {# coin} other {# coins}}.

# The button that buys the selected item.
@maximumLength 16
Purchase = Buy
```

#### Messages

Text with arguments uses ICU MessageFormat, with named arguments:

| You want | Write |
|---|---|
| A value as it is | `Hello, {name}!` |
| A count with plural forms | `{coins, plural, =0 {no coins} one {# coin} other {# coins}}` |
| An ordinal | `{place, selectordinal, one {#st} two {#nd} few {#rd} other {#th}}` |
| A choice by text | `{gender, select, female {her} male {his} other {their}}` |
| A number | `{distance, number}`, `{count, number, integer}`, `{ratio, number, percent}` |
| A date or price | `{deadline, date}`, with a formatter the project registers |
| A literal brace | `'{'` |

Each translation writes the plural forms of its own language: Russian uses `one`, `few`, `many` and `other`. The table window's preview shows a message with sample values in every language, and the importer reports forms a language lacks or never uses.

### Reading text from code

Create one localizer per catalog and keep it for as long as the game runs. The Quick Start sample's `GlobalLocalizer` does that for you, as does a DI container with the VContainer sample.

```csharp
using reromanlee.ReactiveLocalizer;
using reromanlee.ReactiveLocalizer.Unity;

Localizer localizer = new Localizer(LocalizationKeys.CatalogKey, new UnityHost());
// Embedded tables load within the call, so text can be read right away.
localizer.InitializeAsync();

string play = localizer.Get(LocalizationKeys.MainMenu.Play);
string coins = localizer.Get(LocalizationKeys.Shop.CoinBalance(coins: 5));
```

1. **`Get(key)`** returns the same cached string on every call, so reading text allocates nothing. A missing translation falls back along the language's fallbacks to the source language.
2. **`Get(message)`** formats a message in the language its text was found in, allocating the returned string. **`TryFormat(message, buffer, out written)`** writes it into a buffer instead, without allocating.
3. **`Get(tableName, entryName)`** reads an entry named by data, such as dialogue lines a script refers to, hashing the names without allocating.
4. **`TryGet(key, out text)`** is for keys that may legitimately be absent: it reports nothing when they are.
5. **`GetMemory(key)`** returns the characters of a text without creating a string.
6. **A key that exists nowhere** shows as `[Shop.Purchase]` and is reported once. An entry with arguments read with `Get(key)` shows them as `{coins}`; read it as a message.
7. **`InitializeAsync`** completes within the call for embedded tables. Await it when tables come from somewhere slower, such as Addressables.

Lookups work from any thread without locks. Everything that changes the localizer, such as switching languages, applies on the main thread in the order it was asked for, and bindings and events are notified there.

### Binding text

A binding calls its callback with an entry's text right away, and again every time it changes, until it is disposed: a language switch, an on-demand table arriving, a table file saved in the editor, or new arguments. Bind while enabled, so hidden UI costs nothing during a switch.

```csharp
using reromanlee.ReactiveLocalizer;
using reromanlee.ReactiveLocalizer.Samples.QuickStart;
using TMPro;
using UnityEngine;

public sealed class ShopBar : MonoBehaviour
{
    [SerializeField] private TMP_Text _purchase;
    [SerializeField] private TMP_Text _coins;

    private TextBinding _purchaseBinding;
    private TextBinding _coinsBinding;

    public int Coins { get; set; }

    private void OnEnable()
    {
        // Quick Start's access point, or the project's own.
        ILocalizer localizer = GlobalLocalizer.Instance;
        // A static lambda takes its target as a parameter, so binding allocates nothing.
        _purchaseBinding = localizer.Bind(LocalizationKeys.Shop.Purchase, _purchase, static (label, text) => label.text = text);
        _coinsBinding = localizer.Bind(LocalizationKeys.Shop.CoinBalance(coins: Coins), _coins, static (label, text) => label.text = text);
    }

    private void OnDisable()
    {
        _purchaseBinding.Dispose();
        _coinsBinding.Dispose();
    }

    private void Update()
    {
        // Formats again only when the count changed, so calling it every frame costs nothing.
        _coinsBinding.SetMessage(LocalizationKeys.Shop.CoinBalance(coins: Coins));
    }
}
```

1. **`TextBinding` is a struct.** Disposing it twice, or disposing a copy, is safe.
2. **`BindCharacters`** hands the callback `ReadOnlyMemory<char>` instead of a string, valid during the call, and formats messages into a buffer it reuses, so even a changing message allocates nothing. TextMeshPro takes the characters with `SetCharArray`, as the TextMeshPro sample's component does:

   ```csharp
   _binding = localizer.BindCharacters(key, _text, static (text, characters) =>
   {
       if (MemoryMarshal.TryGetArray(characters, out ArraySegment<char> segment))
       {
           text.SetCharArray(segment.Array, segment.Offset, segment.Count);
       }
   });
   ```

3. **A binding keeps its entry's table loaded,** so a table loaded on demand arrives with it; until then, the callback receives an empty string, or keeps its previous text during a switch.
4. **A callback that throws** is reported and skipped, and the other bindings still update. Bindings whose Unity target was destroyed are released and reported.
5. **Register formatters before binding:** text formatted before keeps its text until it is formatted again.

`ReactiveText` wraps a binding for code that wants a value rather than a callback, such as view models, UI Toolkit data binding and reactive libraries:

```csharp
ReactiveText title = new ReactiveText(localizer, LocalizationKeys.MainMenu.Title);
title.Changed += text => Debug.Log(text);
// title.Value is always the current text; dispose it when done.
```

### Switching languages

```csharp
await localizer.SetLanguageAsync(LocalizationLanguages.Russian);
```

1. **A switch is all or nothing:** the new language's tables load, then every lookup and binding switches in the same frame. Bindings keep their previous text while the tables load.
2. **Rapid switches collapse,** so a player scrolling through a language list loads only the last one, and every task completes once it lands.
3. **`localizer.Languages`** lists the languages to offer, with the `DisplayName` a picker shows, and **`CurrentLanguage`** is the one shown.
4. **`LanguageChanging`** is raised when a switch starts loading, for a loading screen, and **`LanguageChanged`** once every text has switched.
5. **Before initialization,** `SetLanguageAsync` chooses the language the localizer starts in; without it, it starts in the source language.
6. **A language the catalog doesn't have** is reported and changes nothing. The tasks never fail.

The package leaves remembering the player's choice and matching the OS language to the project: the Quick Start sample's `GlobalLocalizer` saves every switch and starts in the saved language, or in the OS language when the catalog has it, through a switch in `SystemLanguageMapping` that maps Unity's `SystemLanguage` to the catalog's languages.

### Components and the Inspector

`EntryReference` is the serializable field for an entry picked in the Inspector, and `CatalogReference` for a whole catalog, such as a language picker's.

```csharp
[SerializeField] private EntryReference _title;
[SerializeField, EntryCatalog(typeof(ToolWindowKeys))] private EntryReference _toolTitle;

private void OnEnable()
{
    _binding = localizer.Bind(_title.ToKey(), _label, static (label, text) => label.text = text);
}
```

1. **Click the field** to search keys and texts, with recent picks first. When nothing matches, the popup creates the entry: a search written as `Table.Entry` names it, and any other search becomes its text. The table defaults to the one named after the scene or prefab, and the key to the GameObject's role.
2. **The field shows `Shop.Purchase`** with the text in the preview language below it, red when the key doesn't exist, and an **Update** button when it points to an entry that was renamed.
3. **`[EntryCatalog(typeof(...))]`** limits a field to the catalog of a generated keys class. Otherwise the field saves whichever catalog its entry comes from, so a reusable component works with any catalog.
4. **`ToCatalogKey()`** gives the field's catalog, to pick the localizer, as Quick Start's components do.

#### Ready-made components

The Quick Start sample brings `LocalizedBehaviour`, the base of the samples' text components, which binds while enabled, previews in Edit Mode, and saves the authored text with the scene. The TextMeshPro, uGUI Text and UI Toolkit samples build on it.

1. **Add *ReactiveLocalizer > Localized TextMeshPro*** to a GameObject with a TextMeshPro text and pick its entry, or right-click the text component and choose **Localize**, which turns its current text into a new entry and wires the component in one step.
2. **`LanguageFonts`** gives each language its own TextMeshPro font, such as one with Cyrillic or CJK glyphs, and its Inspector's **Check Font Coverage** lists the characters each language's font, fallbacks included, can't draw.
3. **A component of your own** derives from `LocalizedBehaviour` and binds its own kind of text:

```csharp
[RequireComponent(typeof(Text))]
public sealed class LocalizedLegacyText : LocalizedBehaviour
{
    private Text _text;

    private Text Target => _text != null ? _text : _text = GetComponent<Text>();

    protected override string Text
    {
        get => Target != null ? Target.text : null;
        set
        {
            if (Target != null)
            {
                Target.text = value;
            }
        }
    }

    protected override TextBinding Bind(ILocalizer localizer, in EntryKey key)
    {
        return localizer.Bind(in key, this, static (component, text) => component.Text = text);
    }
}
```

#### Edit Mode preview

The **Preview Language** overlay of the Scene view picks the language bound text shows in Edit Mode, the `Pseudo` language included. Entry fields and the table window preview in the same language.

Code outside the samples opts in through `EditModePreview`: outside Play Mode, `EditModePreview.For(catalog)` hands out a localizer in the preview language. A component that shows preview text calls `EditModePreview.Track(this, restore, show)`, so it puts its authored text back before its scene or prefab saves and shows the preview again right after; `LocalizedBehaviour` already does this.

### The table window

**Window > ReactiveLocalizer > Tables** edits every table of every catalog.

1. **The sidebar** lists catalogs and tables, with counts of problems. **New Table** adds one.
2. **The grid** has a row per entry, with the key, the translator context and a column per language; **Languages** hides columns. Missing, outdated, broken, too long and orphaned texts are tinted, and the filter menu shows only the entries missing in a language, outdated, or with problems. The search field searches keys, texts and comments.
3. **Double-click a cell** to edit it in place; Enter or leaving the cell saves it. Text with line breaks opens in the detail pane, where Enter starts a new line and Ctrl+Enter saves.
4. **The detail pane** of the selected entry has every language's text, the context, the maximum length, and a preview of a message with sample values, such as `1, 2, 5, 21`. An outdated translation has **Show source change**, which finds the source text it was made from in git and marks the words that changed, and **Mark as current**.
5. **New Entry, Rename, Move and Delete** change every language file at once. Renaming and moving leave `@formerly`, so everything that used the old name keeps working. Every change is one step Undo takes back.
6. **Validate** checks the whole project, and **Exchange** exports and imports translations.

Generated code waits until Unity loses focus, or Play Mode ends, so editing tables in the editor never triggers a recompile.

### Validation

Every file is checked as Unity imports it, and problems go to the Console with their file and line. **Tools > ReactiveLocalizer > Validate** checks the whole project, references from scenes, prefabs and Resources included.

1. **Errors fail player builds:** broken files and messages, merge conflict markers, duplicate keys, orphans, arguments that differ between languages, entries missing in required languages, and references to keys that don't exist.
2. **Warnings don't:** outdated and unverified translations, entries missing in other languages, texts over their maximum length, unused aliases and, when turned on, names that aren't PascalCase.
3. **Project Settings > ReactiveLocalizer** turns off failing builds on errors, turns on the PascalCase rule, and chooses whether Play Mode and development builds offer the pseudo language.
4. **A smoke test** can assert that a session never asked for a key that doesn't exist:

```csharp
Assert.That(localizer.MissingKeyCount, Is.Zero);
```

Missing keys are reported as errors, so Unity's Test Framework already fails any test that touches one.

### Loading and delivery

Two settings at the top of a table's source-language file choose when it is in memory and how it ships:

```
@loading OnDemand
@delivery Streaming
```

1. **`@loading Preload`** (the default) loads the table with its language, so reads always work. **`OnDemand`** loads it only while something uses it, for large tables such as dialogue or item descriptions.
2. **`@delivery Embedded`** (the default) packs it inside the build. **`Streaming`** packs it into StreamingAssets, read without blocking the main thread, which keeps huge content out of memory and, on WebGL, out of the initial download. **`Custom`** leaves it out of builds, for a source of your own such as the Addressables sample's.

Bindings hold their tables on their own. Code that reads an on-demand table directly, such as a dialogue system, holds it while it needs it:

```csharp
private TableHandle _dialogue;

private async void OnEnable()
{
    _dialogue = localizer.HoldTable(LocalizationKeys.Dialogue.TableKey);
    if (await _dialogue.WhenLoaded)
    {
        ShowLine(localizer.Get("Dialogue", _lineKey));
    }
}

private void OnDisable()
{
    _dialogue.Dispose();
}
```

1. **A held table follows every language switch** and is unloaded two updates after nothing holds it, so a panel toggled within a frame keeps it.
2. **Holding a preloaded table changes nothing,** so code never needs to know how a table loads.
3. **An on-demand table read while nothing holds it** returns empty text, with one warning per table.
4. **Only what the current language needs is loaded:** a fallback language loads only for tables the current language leaves gaps in.

### Formatters

Dates, times and currencies follow the project's own conventions, so the package leaves them to formatters registered per type name. A formatter writes into the span it is given, so formatting allocates nothing, and returns false when the span is too small, to be called again with more room.

```csharp
// {deadline, date} and {deadline, date, short}, in the conventions of the text's language.
localizer.SetFormatter("date", static (in MessageValue value, ReadOnlySpan<char> style, LanguageInfo language, Span<char> destination, out int written) =>
{
    if (!value.TryGetDateTime(out DateTime date))
    {
        written = 0;
        return true;
    }
    string format = style.SequenceEqual("short".AsSpan()) ? "d" : "D";
    return date.TryFormat(destination, out written, format, CultureInfo.GetCultureInfo(language.Culture));
});
```

A formatter runs on whichever thread formats the message. A missing one shows the value as it is, dates as `yyyy-MM-dd HH:mm:ss`, and one that throws shows `{deadline}`; both are reported once.

### Table sources and mods

A localizer asks its host's table sources for each table, in order. `UnityHost` asks the sources it is given first, then the built-in ones: the imported files in the editor, and the tables packed into the build and into StreamingAssets in players.

**Fan translations and downloaded text** come from `FolderTableSource`, which compiles `.lang` files from a folder when they are asked for. Asked first, it patches the languages the game ships entry by entry, and the game's tables fill in the rest. Languages it defines in a `.catalog` file are registered at runtime:

```csharp
FolderTableSource mods = new FolderTableSource(LocalizationKeys.CatalogKey, modsFolder);
Localizer localizer = new Localizer(LocalizationKeys.CatalogKey, new UnityHost(mods));

List<string> problems = new List<string>();
foreach (LanguageInfo language in mods.ReadLanguages(problems))
{
    localizer.RegisterLanguage(language);
}
localizer.InitializeAsync();
```

**A source of your own** implements `ITableSource`. It returns false at once for what it doesn't have, so the next source is asked, or true and then answers exactly once, from any thread, now or later: `receiver.Receive` with the compiled table, `receiver.Fail` with a reason, or `receiver.Decline` when it learns only later that it doesn't have the table, which passes the request on with nothing reported. A compiled table is the `Data` of the `TableAsset` a `.lang` file imports as, which is what the Addressables sample loads.

```csharp
public sealed class DownloadedTables : ITableSource
{
    private readonly Dictionary<(ulong Table, ulong Language), byte[]> _tables = new();

    public bool TryLoad(in TableRequest request, TableReceiver receiver)
    {
        if (request.IsCatalog || !_tables.TryGetValue((request.Table.Hash, request.Language.Hash), out byte[] compiled))
        {
            return false;
        }
        receiver.Receive(compiled);
        return true;
    }
}
```

### Exchanging translations

**Tools > ReactiveLocalizer > Export**, or **Exchange** in the table window, writes translations for translators, and the import commands read them back.

1. **Pick the format:** an XLSX workbook with a sheet per table, CSV files per table, XLIFF 1.2 or 2.0 files per language, or an LLM handoff.
2. **Pick what goes out:** the tables, the languages, and all entries, only missing ones, only outdated ones, or both.
3. **Import** with **Import File** or **Import Folder**. The preview lists what is new, updated, outdated, confirmed, rejected and unchanged before anything is written, lets you change the catalog and the languages the file matched, and the whole import is one step Undo takes back.
4. **Imports change translations only.** They never create, rename or delete keys, an empty cell never erases a translation, and changes to the source column are ignored unless **Import source changes** is ticked.
5. **Every imported text is compiled like the files are,** so a translation with broken syntax or other arguments is rejected with the reason.
6. **The source text in the file becomes the fingerprint,** so a translation made from an older source text arrives already marked outdated.

**With a language model:** export with the **LLM handoff** format and one language, which copies a text bundle to the clipboard: rules for the model, the language's plural forms, and the entries to translate with their context and maximum length. Paste it into a chat, copy the model's whole reply, and choose **Tools > ReactiveLocalizer > Import LLM Reply**, which goes through the same preview. No API key is involved.

### Pseudo-localization

Pick **Pseudo** in the Scene view's **Preview Language** overlay, or in a language picker in Play Mode, to see every text accented, a third longer and bracketed: `[Bûý ñöŵ one]`. Text that stays plain is hardcoded, a cut-off bracket is truncation, and the extra length shows overflow, before any translation exists.

Play Mode and development builds offer it as one more language of every catalog, unless **Project Settings > ReactiveLocalizer** turns that off; release builds never do. A catalog that defines a language named `Pseudo` keeps its own.

### Character sets

**Tools > ReactiveLocalizer > Character Sets** collects every character a catalog's languages can show, for TextMeshPro font atlases:

1. Pick the catalog and one or more languages; several languages give the set a font they share needs.
2. Add printable ASCII or extra characters for text that arguments bring at runtime, such as player names.
3. Copy the set, or save it as a text file, and use it in TextMeshPro's **Font Asset Creator** with **Custom Characters** or **Characters from File**.

`CharacterSets.Collect(catalog, language)` is the editor API behind it, for build scripts that make font atlases.

### Samples

Import samples from **Window > Package Manager > ReactiveLocalizer > Samples**. The UI samples build on Quick Start, so import it first.

| Sample | How to use it |
|---|---|
| Quick Start | Open its scene and enter Play Mode. `GlobalLocalizer.Instance` is the default catalog's localizer, and `GlobalLocalizer.For(catalog)` any other's; both remember the player's language. Add **ReactiveLocalizer > Language Picker** for a picker. |
| TextMeshPro | Add **Localized TextMeshPro**, or right-click a TextMeshPro text and choose **Localize**. **Assets > Create > ReactiveLocalizer > Language Fonts** gives each language its font. |
| UI Toolkit | On Unity 6, add a `LocalizedTextBinding` to an element's property in UI Builder or UXML. From code, `label.BindText(LocalizationKeys.Menu.Title)`. Without code, add **Localized Document** next to a UIDocument and list element names with their entries. |
| uGUI Text | Add **Localized Text (Legacy)**, or right-click a Text and choose **Localize**. |
| Addressables | Mark tables `@delivery Custom`, run **Tools > ReactiveLocalizer > Samples > Mark Custom Tables Addressable**, and build Addressables content as usual. A table Addressables don't have falls back as usual. |
| VContainer | In a `LifetimeScope`, `new LocalizerInstaller(LocalizationKeys.CatalogKey).Install(builder);`, then inject `ILocalizer`. |
| R3 | `localizer.ObserveText(key)`, `coins.SelectText(localizer, count => LocalizationKeys.Shop.CoinBalance(coins: count))` and `localizer.ObserveLanguage()`. |
| Google Sheets | Follow its README to deploy the Apps Script, then use **Tools > ReactiveLocalizer > Samples > Google Sheets Sync** to push and pull. |
| Mods | **Tools > ReactiveLocalizer > Samples > Install Example Mod** puts a mod in the player's mods folder; enter Play Mode and pick Elvish. |
| Editor Tool | Open **Tools > ReactiveLocalizer > Samples > Localized Tool Window** and read its code, a template for localizing a tool or package. |
| Unity Localization Migration | **Tools > ReactiveLocalizer > Samples > Migrate from Unity Localization**, described in [MIGRATION.md](MIGRATION.md). |

### Coding agents

**Tools > ReactiveLocalizer > Write Agent Guide** adds a section to the project's `AGENTS.md` or `CLAUDE.md`, asking which one, or creates `AGENTS.md` when neither exists. It tells agents where the catalogs and generated keys are, how to add, translate and rename entries, and what never to edit by hand. Run it again after adding catalogs or languages: it replaces its own section and leaves the rest of the file alone.

### Assemblies

Scripts in Assembly-CSharp see the package without any setup. An asmdef of your own references what it uses:

| Assembly | Namespace | What it holds | Reference it from |
|---|---|---|---|
| `reromanlee.ReactiveLocalizer` | `reromanlee.ReactiveLocalizer` | The localizer, keys, messages, bindings and table sources, with no engine references | Game code |
| `reromanlee.ReactiveLocalizer.Unity` | `reromanlee.ReactiveLocalizer.Unity` | `UnityHost`, `EntryReference`, `CatalogReference` and `EditModePreview` | Game code |
| `reromanlee.ReactiveLocalizer.Authoring` | `reromanlee.ReactiveLocalizer.Authoring` | Reading, validating and writing files, exchange formats and pseudo-localization, with no engine references | Editor tools |
| `reromanlee.ReactiveLocalizer.Editor` | `reromanlee.ReactiveLocalizer.Editor` | The importers, windows and editor APIs such as `EntryAuthoring` | Editor tools |

The generated keys compile into the assembly that owns the catalog's folder, so put a catalog where the code that uses it can see it.

### Editor scripting

Project tools use the same editor APIs the package's windows do:

1. **`EntryAuthoring.TryCreateEntry`** creates an entry in a table's source file, as the Inspector field does, with `SuggestKey` and `SuggestTable` for names.
2. **`TranslationExchange.Collect`** gathers a catalog's entries for an export, and **`TranslationExchange.PreviewImport`** opens the import preview for files a tool fetched, as the Google Sheets sample does.
3. **`CharacterSets.Collect`** returns a language's character set.
4. **`MessageText.Quote`** turns any text into a message that shows it as written, and **`MessageText.IsValid`** checks a message, for tools that write entries.
5. **`NameRules.IsValid`** checks a name against the naming rule.
