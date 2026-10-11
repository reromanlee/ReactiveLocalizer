# Migrating from Unity Localization

ReactiveLocalizer replaces Unity Localization rather than building on it. The two can stay installed side by side while a project moves over, since none of their type names collide. The Unity Localization Migration sample converts the data in one step; the code changes are listed after it. Most important first.

### Converting the tables

1. **Import the sample** from **Window > Package Manager > ReactiveLocalizer > Samples > Unity Localization Migration**. It needs Unity Localization 1.4 or newer, still installed.
2. **Run Tools > ReactiveLocalizer > Samples > Migrate from Unity Localization** and pick a folder in Assets without localization files, such as `Assets/Localization`. It says how many tables, texts and languages it will write, and writes nothing until you confirm.
3. **Read the report in the Console:** the names it changed to follow the naming rule, the placeholders it turned into named arguments, and the texts it left for you to convert.
4. **Search the new files for `MIGRATE`,** the comment above every text to convert by hand, convert them, and delete the comments.

The migration writes `Localization.catalog` and a `.lang` file per table and language, so the generated classes are `LocalizationKeys` and `LocalizationLanguages`. A project that wrapped Unity Localization in its own generated keys class of that name keeps its call sites, such as `LocalizationKeys.SettingsPage.Title`: only the `using` lines change.

| Unity Localization | Becomes |
|---|---|
| A locale | A language named after its language in English, such as `English`, or with its region when two share a language, such as `PortugueseBrazil`; its code becomes the `Culture` |
| The project locale | The source language, or the first locale without Localization Settings |
| A fallback locale | The language's `Fallback` |
| A string table collection | A table, named in PascalCase: `Settings Page` becomes `SettingsPage` |
| A key | An entry, named in PascalCase: `menu/play_button` becomes `MenuPlayButton` |
| A comment on a shared entry | The comment above the entry, which translators see |
| An empty translation | A missing one, which falls back as before |
| `{0}` in any string | `{arg0}`, listed so you can rename it after what it shows |
| `{name}` in a Smart string | `{name}` |
| `{{` and `}}` in a Smart string | Literal braces |
| Any other Smart Format syntax | Kept so it shows as written, and marked `MIGRATE` |

Translations are stamped with the fingerprint of the migrated source text, so they start out current, and a later change to a source text flags them as outdated.

### Converting Smart Format by hand

Smart Format's formatters have ICU counterparts, which translation tools also understand. Each translation writes the plural forms of its own language.

| Smart Format | ICU |
|---|---|
| `{count:plural:One apple\|{} apples}` | `{count, plural, one {One apple} other {# apples}}` |
| `{gender:choose(male\|female):He\|She\|They}` | `{gender, select, male {He} female {She} other {They}}` |
| `{count:cond:=0?No apples\|Some apples}` | `{count, plural, =0 {No apples} other {Some apples}}` |
| `{player.Name}` | `{playerName}`, passed by the code as an argument |
| `{0:N2}` | `{arg0, number}`, which shows up to three fraction digits; for an exact count, format the number in code and pass it as text |
| `{0:P}` | `{arg0, number, percent}` |
| A date with a format, such as `{0:d}` | `{arg0, date}` with a date formatter the project registers |

### Changing the code

| Unity Localization | ReactiveLocalizer |
|---|---|
| `await LocalizationSettings.InitializationOperation` | `await localizer.InitializeAsync()`, which completes within the call for embedded tables |
| `LocalizationSettings.StringDatabase.GetLocalizedString("Shop", "Purchase")` | `localizer.Get(LocalizationKeys.Shop.Purchase)`, or `localizer.Get("Shop", "Purchase")` for names from data |
| `GetLocalizedStringAsync(...)` | `Get`, which is synchronous: preloaded tables are always in memory |
| A `LocalizedString` field | An `EntryReference` field, picked in the Inspector the same way |
| `localizedString.StringChanged += ...` | `localizer.Bind(key, target, static (target, text) => ...)`, disposed in `OnDisable` |
| Smart string arguments | Messages from generated code, such as `LocalizationKeys.Shop.CoinBalance(coins: 5)`, and `binding.SetMessage(...)` to change them |
| A `LocalizeStringEvent` component | The samples' `LocalizedTextMeshPro`, `LocalizedLegacyText` or UI Toolkit bindings |
| `LocalizationSettings.SelectedLocale = locale` | `await localizer.SetLanguageAsync(LocalizationLanguages.Russian)`, or Quick Start's `GlobalLocalizer.SetLanguage` for every catalog |
| `LocalizationSettings.SelectedLocaleChanged` | `localizer.LanguageChanged` |
| `LocalizationSettings.AvailableLocales.Locales` | `localizer.Languages` |
| Tables loaded on demand | `@loading OnDemand` on the table, held by bindings or `HoldTable` |
| String tables delivered by Addressables | Embedded in the build by default; `@delivery Custom` with the Addressables sample keeps delivering them through Addressables |
| A pseudo-locale | The `Pseudo` language every catalog has in the editor, Play Mode and development builds |

The selected locale's persistence and the system locale selector become the Quick Start sample's `GlobalLocalizer`, which saves the player's choice and maps the OS language through `SystemLanguageMapping`.

### What stays with Unity Localization

1. **Asset tables,** such as localized sprites, audio and fonts, aren't part of ReactiveLocalizer 1.0, which localizes strings. Keep Unity Localization for them while the string tables move over; TextMeshPro fonts per language come from the TextMeshPro sample's `LanguageFonts`.
2. **Smart Format's other features,** such as list formatting, templates and custom sources, have no ICU counterpart: build those texts in code from several entries.

Once nothing uses its types, remove the Unity Localization package; the migrated files stand on their own.
