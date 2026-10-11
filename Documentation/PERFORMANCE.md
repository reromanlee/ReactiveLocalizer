# Performance

What ReactiveLocalizer costs, measured, and the design behind the numbers. Tips to keep it light are at the end.

### How it stays fast

1. **A lookup is a binary search and a cached string.** Every name is hashed once, generated keys when their class initializes, and a loaded table keeps its key hashes sorted, so finding an entry is a binary search over 64-bit numbers. Its string is created the first time it's read, and every later read returns the same one.
2. **Steady state allocates nothing,** which tests enforce for cached lookups, binding updates, unchanged arguments and formatting into a buffer.
3. **Idle localization costs one flag check per frame.** Bindings never poll: they're called only when their text changes.
4. **A language switch touches only what is active.** Components bind while enabled, so hidden UI costs nothing during a switch and reads the current language when it shows.
5. **Messages format only when their arguments change,** so a binding given the same arguments every frame does nothing.
6. **Only the current language is loaded.** Fallback languages load only for tables the current language leaves gaps in, and on-demand tables only while something uses them.
7. **A loaded table is a handful of arrays,** whatever its size, read from the build with one copy per array.

### CPU time

Measured by the package's performance tests in the Editor, on an Intel Core i7-12700K, as the median of 10 measurements, and by the same operations in an IL2CPP release build for Windows made with Unity 2022.3, on the same machine. The Editor runs scripts on Mono, slower than IL2CPP player builds. Reads and formatting are timed over 10,000 calls and given per call, and allocations are counted by the Editor's tests.

| Operation | IL2CPP build | Unity 6000.5 Editor | Unity 2022.3 Editor | Allocations |
|---|---|---|---|---|
| Read a text by key | 55 ns | 73 ns | 71 ns | None |
| Read a text by table and entry name | 85 ns | 90 ns | 90 ns | None |
| Format a plural message into a buffer | 0.55 µs | 0.79 µs | 0.79 µs | None |
| Format a plural message into a string | 0.66 µs | 0.99 µs | 1.00 µs | The string |
| Switch the language of 1,000 bound texts | 0.46 ms | 0.36 ms | 0.35 ms | The 1,000 new strings and about 75 more |
| Give 1,000 bound messages new arguments | 0.92 ms | 1.23 ms | 1.28 ms | The 1,000 new strings |
| Give 1,000 character-bound messages new arguments | 0.87 ms | 1.12 ms | 1.14 ms | None |
| Load a 50,000-entry table | 3.87 ms | 4.35 ms | 4.51 ms | 29 |
| Compile a 50,000-entry table, as its import does | Editor only | 244 ms | 247 ms | Editor only |

1. **Lookups** read a table of 1,000 entries; by name, the two names are hashed on every call.
2. **The message** is `You have {coins, plural, one {# coin} other {# coins}}.` with a new count on every call.
3. **The switch** goes between English and Russian tables of 1,000 entries, every one of them bound; it loads the new tables, gives every binding its text, and releases the old tables. In the build, the tables come from the build's resources, while the Editor's tests hand them over from memory.
4. **The 50,000-entry table** holds lines of about 65 characters, loaded on demand, and is unloaded between measurements.

### Memory

1. **A loaded table** takes 2 bytes per character of its texts plus 24 bytes per entry: the 50,000 lines above take 7.3 MB.
2. **Strings are created only for the texts actually read,** at about 30 bytes plus 2 bytes per character each: reading every one of those 50,000 lines adds 7.8 MB, while a dialogue that shows a hundred lines adds about 16 KB.
3. **A compiled table** in a build is about as large as it is in memory: 6.9 MB for those lines.
4. **A complete translation costs no extra memory,** since its fallback languages are never loaded.

### Where the numbers come from

The Editor numbers come from the tests in `UnityPackage/Tests/Performance`, which run with every other test on both Unity versions in continuous integration, where the results are part of the test results artifacts. They need the Performance Testing package, `com.unity.test-framework.performance`, and compile only when it's installed. Nothing in them asserts a speed, since that depends on the machine; the release checklist compares them with the previous release's.

### Tips

1. **Bind text that stays on screen,** and read it with `Get` only where it's read once. A binding costs nothing until its text changes.
2. **Use `BindCharacters` for text that changes often,** such as a counter, with TextMeshPro: it formats into a buffer it reuses and creates no strings at all.
3. **Mark large tables `@loading OnDemand`,** such as dialogue, quests and item descriptions, and split them by chapter or area, so a load stays small and memory holds only what's in use.
4. **Use `@delivery Streaming` for large content on WebGL,** where it stays out of the initial download and loads only when asked for.
5. **Keep shipped translations complete,** with `Required = true` on their languages, so no fallback table is ever loaded.
6. **Set the language before initializing** with `SetLanguageAsync`, so the localizer starts in the player's language instead of loading the source language first.
7. **Opt data tables out of code generation** with `@generateCode false`, so a 50,000-line table doesn't become 50,000 fields to compile.
8. **Register formatters before binding,** so no text is formatted twice.
