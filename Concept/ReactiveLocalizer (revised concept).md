# ReactiveLocalizer: Architecture & Implementation Specification

### 1. Package Overview

**ReactiveLocalizer** is an enterprise-grade, highly efficient, cross-platform reactive localization package for Unity. It is designed to handle any scale of translation data—from rapid prototypes to AAA data-heavy projects—while maintaining strict zero-allocation internals, graceful memory management, and developer-friendly APIs.

**Core Directives for AI Implementation:**

* **Performance First:** Internals must prioritize `ReadOnlySpan`, struct-based argument passing, and zero-boxing logic.
* **Interface-Driven:** The core engine must be decoupled from MonoBehaviours and rely on interfaces (e.g., `IReactiveLocalizer`) to support arbitrary Dependency Injection frameworks (VContainer, Zenject).
* **Scalable Memory:** Must support asynchronous on-demand table loading and disposing to fit within strict memory budgets (e.g., WebGL).

### 2. Import/Export & Translation Workflow

* **Formats:** Supports importing/exporting `.xlsx` and `.csv` files via a custom Editor window.
* **External Translation:** The package does not include built-in machine translation APIs (to avoid rate limits and poor context). Instead, the exported `.csv`/`.xlsx` files are designed to be easily fed into external LLMs or passed to localization teams, then re-imported.
* **Editor Syncing:** Imported tables can be previewed and edited directly within Unity, then exported back out. Editor Mode support ensures localization can be used inside custom Editor tools and UI Toolkit extensions.

### 3. Hybrid Access System (Code-Gen + String Keys)

To support both type-safety for UI and infinite scalability for massive datasets (e.g., RPG dialogues), the package uses a hybrid access model:

* **Optional Code Generation:** Developers can flag specific tables (e.g., `UI`, `Settings`) to generate `TableName`, `EntryName`, and `LanguageName` enums. This eliminates magic strings and human error for static UI.
* **String/Hash Key Access:** For massive tables containing tens of thousands of entries, developers can bypass code-gen and access entries via string or hashed integer keys, preventing assembly bloat and compile-time degradation.

### 4. Smart Formatting & Pluralization (ICU Message Format)

The package must support the **ICU Message Format** standard to handle dynamic variables, pluralization, and gender rules directly within the string, avoiding complex C# logic.

* **Pre-Parsing:** To save runtime CPU cycles, ICU strings (e.g., `You have {coinCount, plural, one {one coin} other {# coins}}.`) are pre-parsed into an internal instruction tree during the Unity Editor import phase.
* **Zero-Allocation Variables:** Variables passed into the localizer must avoid boxing. Use generic structs, `ref` parameters, or a custom argument buffer interface (e.g., `new PluralArg("coinCount", 5)`).

### 5. Extreme Efficiency & Zero-Allocation Internals

* **Internal Pipeline:** String allocations must be completely bypassed internally using `ReadOnlySpan` and custom lexers.
* **Output Boundary:** Standard Unity components (Text, TextMeshPro, UI Toolkit) ultimately require a `string` assignment. The engine must ensure that the *only* allocation that occurs is the final string generation at the UI boundary.
* **Idle CPU:** Reactive observers and properties must be completely dormant when localization state is static.

### 6. Memory Management & Addressables Readiness

* **On-Demand Loading:** Tables must be chunked. The package must not load all tables into memory at once.
* **Async Streaming:** The architecture must support asynchronous loading of requested language tables via standard Resource providers or Unity Addressables.
* **Disposal:** Unused tables must be cleanly disposable to free up memory on strict platforms like WebGL and mobile.

### 7. Reactive Nature & Tearing Mitigation

* **Reactive Properties:** Developers can define local reactive fields (e.g., `LocalizerText _playText`). When initialized, they sync automatically and listen for global language changes.
* **Ordered Syncing:** To prevent CPU spiking during a language switch, the `Localizer` instance sequentially updates registered properties.
* **Tearing Handling:** Because sequential updates across thousands of UI elements might cause visual "tearing" (updating over several frames), the API must expose async callbacks/events so the developer can trigger a loading screen before the language switch begins and hide it when the queue finishes.

### 8. Dependency Injection & Rapid Prototyping

* **Core Architecture:** The core logic is instantiated via standard constructors and implements `IReactiveLocalizer`, making it perfectly suited for composition roots and DI frameworks like VContainer.
* **Prototyping Convenience:** To avoid anti-patterns in the core package while remaining accessible to beginners, a pre-built Singleton wrapper (`ReactiveLocalizerManager.cs`) must be included strictly within the `Samples~` folder.

### 9. Scene Components & Developer Experience

* **Searchable Dropdowns:** MonoBehaviour or UI Toolkit components must feature a highly polished custom Inspector with an instant-search dropdown to quickly find and assign target entries.
* **AI & Human Readability:** The internal codebase must feature explicit XML documentation, full-word variable naming, and clear code structure to ensure AI coding agents can easily parse, extend, and navigate the APIs.

### 10. Utilities

* **Glyph Extraction:** The package includes a utility to scan all loaded tables and generate a fixed string of unique characters per language, which can be used to generate optimized, stripped font atlases (e.g., TMPro Font Asset Creator).