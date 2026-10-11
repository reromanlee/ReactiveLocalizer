# File format

The exact rules of `.lang` and `.catalog` files. They are a public contract: people, merge tools, AI agents and other programs read and write them, so they stay compatible across versions. Anything these rules don't allow is reported with its file, line and column, and costs only its own line. Most important first.

### Files and names

1. **A table is one file per language,** named `<Table>.<Language>.lang`, such as `Shop.English.lang` and `Shop.Russian.lang`. The name is the identity, so folders are free-form.
2. **A catalog is a `<Catalog>.catalog` file.** It owns every table in its folder and below, unless a catalog in a folder closer to the table owns it, the way an asmdef owns scripts.
3. **Tables in Assets outside every catalog's folder** belong to the default catalog: `Assets/Localization/Localization.catalog` when that file exists, else the only catalog in Assets outside an Editor folder. When neither applies, they are reported instead of guessed.
4. **A catalog inside an `Editor` folder is editor-only:** builds never pack it.
5. **Names** of catalogs, tables, entries, languages and arguments start with an ASCII letter and continue with ASCII letters, digits and underscores, up to 128 characters. They are unique ignoring case, and everything that looks names up ignores case too.
6. **Text is UTF-8.** A byte order mark is skipped when reading and never written. Lines end with CRLF, LF or CR, in any mix; writers keep the line ending a file has, and use LF for a new one.

### Lines

Both kinds of file are read one line at a time, and the first character that isn't a blank decides what a line is. Blanks are spaces and tabs only: every other whitespace character, the no-break space included, is content and is never trimmed.

| A line starting with | Is |
|---|---|
| Nothing but blanks | A blank line |
| `#` | A comment: the text after `#`, without one blank right after it and without trailing blanks |
| `@` | An attribute: a name right after the `@`, then optionally blanks and a value |
| `[` | A language section header, in catalog files only |
| Anything else | An entry in table files, a field in catalog files |

1. **Merge conflict markers are errors:** a line that starts with exactly seven `<`, `=`, `>` or `|` characters, followed by nothing or a blank, is the marker git leaves around a conflict.
2. **Comments right above an entry or a field belong to it.** In a table's source-language file, they are the context translators see in the table window and in every export.
3. **Comments at the top of a file followed by a blank line** are the file's header; comments after the last entry stay at the end.
4. **Unknown attributes and fields are kept** with a warning, so a file written by a newer version loses nothing when an older one rewrites it.

### Table files

```
# Prices and purchases in the shop.

@loading Preload
@delivery Embedded

@formerly CoinsLabel
CoinBalance = You have {coins, plural, one {# coin} other {# coins}}.

Disclaimer = Prices include VAT.\nRefunds within 14 days.

# The button that confirms buying the selected item.
@maximumLength 16
Purchase = Buy
```

An entry is `Key = Value`, with an optional fingerprint between the key and the `=`, and blanks allowed around both:

```
Purchase [3fa2c1] = Купить
```

1. **The key** is the run of letters, digits and underscores the line starts with, and has to follow the naming rule. A key already defined in the file, ignoring case, is an error, and the first definition stays.
2. **The value is everything after the first `=`,** with blanks at both ends trimmed before escapes are resolved. It needs no quotes, and nothing in it is syntax of the file: a `#`, `@`, `=` or `[` inside a value is text.
3. **An empty value is an intentionally empty text,** which is different from a missing entry.
4. **The source-language file defines the keys.** A translation file holds only the entries translated so far: a missing one falls back to the language's fallback, ending at the source language. A key that exists only in a translation is an orphan, reported and left out of builds.
5. **Entries are matched by key, never by position,** so their order in a file doesn't matter.

#### Escapes

| Written | Means |
|---|---|
| `\n` | A line break |
| `\t` | A tab |
| `\\` | A backslash |
| `\uXXXX` | The UTF-16 code unit with that hexadecimal value, such as ` ` for a no-break space |

Any other backslash, a `\u` without four hexadecimal digits, and a backslash ending the value are kept exactly as written, with a warning.

#### Settings

Table settings are attributes at the top of the source-language file, before the first entry and its attributes, each written once. Translation files take the source file's settings. Values ignore case.

| Setting | Values | Default |
|---|---|---|
| `@loading` | `Preload`: loaded with the language. `OnDemand`: loaded while bindings or `HoldTable` handles use it. | `Preload` |
| `@delivery` | `Embedded`: packed inside the build. `Streaming`: packed into StreamingAssets and read when needed. `Custom`: not packed, since a table source of the project delivers it. | `Embedded` |
| `@generateCode` | `true` or `false`: whether the table gets generated keys. | `true` |

#### Entry attributes

Entry attributes are written on the lines right above the entry they belong to, among its comments.

| Attribute | Means |
|---|---|
| `@formerly OldName` | The entry used to be called `OldName`, which still finds it: at runtime, in saved references and as an `[Obsolete]` member of generated code. `@formerly OldTable.OldName` is an entry moved from another table. An entry can have several. |
| `@maximumLength 16` | Translations longer than this many characters are reported. |

#### Fingerprints

A translation line carries a fingerprint of the source text it was translated from: six hexadecimal digits in brackets after the key. When the source text changes, the fingerprints stop matching and the translations are reported as outdated. A translation without one counts as unverified, and brackets that don't hold six hexadecimal digits are ignored with a warning.

The fingerprint is the top 24 bits of the 64-bit FNV-1a hash of the source entry's text, after its escapes are resolved, taken over its UTF-16 code units, low byte first. The offset basis is 14695981039346656037 and the prime 1099511628211. Writers write it in lower case, and readers take either case.

### Messages

Values use ICU MessageFormat, the classic syntax (MF1) that translation tools support. Messages are compiled at import, so every problem is reported with its line and column.

| Syntax | Example |
|---|---|
| An argument shown as it is | `Hello, {name}!` |
| A number | `{distance, number}`, `{count, number, integer}`, `{ratio, number, percent}` |
| A plural | `{coins, plural, =0 {no coins} one {# coin} other {# coins}}` |
| A plural with an offset | `{count, plural, offset:1 =0 {Nobody} =1 {Only you} one {You and # other} other {You and # others}}` |
| An ordinal | `{place, selectordinal, one {#st} two {#nd} few {#rd} other {#th}}` |
| A select | `{gender, select, female {her} male {his} other {their}}` |
| A type the project formats | `{deadline, date}`, `{deadline, date, short}`, `{price, currency}` |

1. **Arguments are named,** since names become the parameters of generated code. ICU's numbered arguments, such as `{0}`, and its deprecated `choice` are rejected with an explanation.
2. **Plurals, ordinals and selects need an `other` form.** A plural or ordinal is warned about when it lacks a form its language uses, unless exact forms such as `=1` stand in for it, and when it has a form its language never uses. Russian uses `one`, `few`, `many` and `other`; English `one` and `other`.
3. **`#` inside a plural form** is the number, formatted as the language writes numbers.
4. **Translations use the same arguments as the source text,** each in a way its value supports: a number may also be shown as it is, but text can't choose a plural form. A translation that doesn't match is left out with an error, so it shows in its fallback language.
5. **Types other than `number`, `plural`, `selectordinal` and `select`** call a formatter the project registers, with the style after the second comma. Without one, the value shows as it is and the problem is reported once.

#### Quoting

An apostrophe starts quoted text only right before a brace, or before `#` inside a plural form, and two apostrophes always show one. Everything else is plain text.

| Written | Shows |
|---|---|
| `Don't` | Don't |
| `It''s` | It's |
| `Use '{'braces'}'` | Use {braces} |
| `Write '{name}' literally` | Write {name} literally |

### Catalog files

```
# The game's languages. Pirate is invented: it has no culture, and falls back to English.

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

1. **Attributes come first,** before the first section, each written once:
   1. `@source` names the source language, which has to be one of the sections. It is required.
   2. `@namespace` is the namespace of the generated code. Without it, the code takes the root namespace of the asmdef owning the folder, then the project's root namespace setting, then none.
2. **A section is a language:** its name in brackets, on a line of its own. Blanks inside the brackets are allowed, so `[ English ]` names English. A name already used, ignoring case, is an error, and its section is skipped.
3. **Fields** are `Field = Value` lines inside a section, with the same escapes as table values, each written once.

| Field | Value | Without it |
|---|---|---|
| `DisplayName` | What a language picker shows, such as `Português (Brasil)` | Empty |
| `Culture` | A language tag such as `en` or `pt-BR`, which plural rules, number formatting and exports follow | An invented language: plural rules and number symbols come from the fallback language, and without one every count takes the `other` form |
| `Fallback` | Another language of the catalog, which missing entries come from | Only the source language |
| `Direction` | `LeftToRight` or `RightToLeft`, a hint for whatever renders the text | `LeftToRight` |
| `Required` | `true` or `false`: whether builds fail while an entry is missing in this language | `false` |
| `Digits` | The ten digits from zero to nine, in order, such as `٠١٢٣٤٥٦٧٨٩` | The culture's |
| `DecimalSeparator` | The decimal separator; it can't be empty | The culture's |
| `GroupSeparator` | The digit group separator; empty never groups | The culture's |

1. **A culture CLDR doesn't know** is reported as a warning, and the language then takes its plural rules and number symbols from its fallback, as if it had no culture.
2. **Fallbacks that lead back to their language** are an error, and the fallback is ignored.
3. **Every language falls back to the source language last,** so a missing entry always shows some text.

### Canonical layout

Every tool of the package writes table files the same way, so any edit, by a person, a tool or an agent, gives the same text and the smallest diff:

1. Header comments, then a blank line.
2. Settings, in the order `@loading`, `@delivery`, `@generateCode`, then a blank line.
3. Entries in natural order, ignoring case, so `Line9` comes before `Line10`, each with its comments and attributes above it. A blank line separates the entries from what comes before them, and sets apart every entry that has comments or attributes.
4. Trailing comments, after a blank line.
5. `Key [fingerprint] = Value`, with one space on each side of the `=`, and `Key =` for an empty value.
6. In values, line breaks as `\n`, tabs as `\t`, backslashes as `\\`, spaces at either end as ` `, and characters that are invisible in a review as `\uXXXX`: control characters, unusual spaces such as the no-break space, zero-width characters, bidirectional marks, the soft hyphen and the byte order mark.
7. UTF-8 without a byte order mark, with the file's own line endings.

### What is generated from the files

1. **Compiled tables** live in Unity's import cache and in builds, never in the repository. Their binary format is internal and carries a version, so a table compiled by another version is reported, never misread.
2. **Generated code** is one `<Catalog>Keys.cs` file next to each catalog, holding the `<Catalog>Keys` and `<Catalog>Languages` classes. It changes only when a key, alias, argument, table or language changes, never when only a text does, and it says so in its header. Edits to it are lost.
