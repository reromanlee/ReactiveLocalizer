### About ReactiveLocalizer

An extremely reliable, production grade, highly efficient to handle any size of translation base, highly polished, cross-platform, reactive localization package for Unity, with clean and pretty API and a very convenient and easy integration for many different usage cases.

### Import/Export spreadsheets

The key advantage of most localization packages is the ability to edit localization via Microsoft Excel, Google Sheets or any other external solution, import the `.xlsx` or `.csv` into their custom Editor window and being able to use those new edits right away.

Another very convenient feature is to be able to edit imported tables in Unity, then export them as `.csv` or `.xlsx` and again open them in external software and view/edit those there.

### Works anywhere

Under any context, whether it is by running it in a single threaded environment with exceptional amount of limitations like WebGL platform, or running it in multiple threads and in async context on some other platform - it has to be flawless under any usage cases possible to ensure that this package is not the cause of any problem when it comes to debugging of exceptions happening in development.

Virtually any platform, from Windows, Mac OS, Linux, Android, iOS, WebGL or anything else that can be built with Unity - has to be fully supported and safe to implement localization with this package.

From a simple MonoBehaviour script in a Unity thread or a non-MonoBehaviour class running outside Unity thread, all should work as expected (except the reasonable cases when we make a Unity Component for localizing something in a scene, which is virtually only accessible within Unity thread).

To take this advantage to the next level, we can introduce Editor Mode support, which might be good for scene sync during edit mode, but it also would be great feature for custom Editor tools to provide them their own packaged localization for a multi-language user base.

### Human factor considered

Not even a single string literal being involved to access tables or their entries: every table is backed by their named value in `TableName` enum; every entry within that table is backed by their `EntryName` enum which is issued individually per each table. Languages added are also having their own enum called `LanguageName`.

### Automatic in-Editor translation

Using service APIs like Google Translate or Yandex Translate (suggest more options) - we can fill in all the missing neighbor entries, considering that we have default language entries filled in. As simple as a click of one button within table viewer after we select translation provider of our preference.

### Extreme efficiency

Zero-allocation awareness of tables and entries. Completely bypassing string allocations via `ReadOnlySpan<char>` or anything that would lets us minimize allocations as much as possible while maintaining access to tables and their entries.

This solution has to consider the scalability of a project, since rapid prototyping is easy to handle, it won't be more than hundred entries and a few tables - we have to consider the ability to handle tens of thousands of entries per table while having hundreds of tables for a high-end triple A project cases. If we manage to find a solution to scale translation base without affecting any of the runtime performance or any significant memory allocations involved - that is going to be the best ever flexible and scalable solution used by virtually everyone who learns about it.

It also has to function flawlessly under a very strict and low end environment such as WebGL which has the worst compatibility and the worst hardware access and therefore, least amount of resources available to work.

Besides zero-allocation or minimal allocations where it is logically impossible to avoid them, we need to ensure zero CPU usage at runtime when we don't expect any changes localization wise. Reactive observers and properties have to be lightweight on CPU and only work when they have to as expected from them.

### Convenient and pretty

What makes a great production ready package is how beautiful it's code is, or how virtually presentable its code is so that it can be shown to others and perceived like a human readable and clean set of instructions, intuitively understood by everyone who briefly reads it.

### AI convenience considered

Take in consideration the accessibility and ease of understanding for agents which are going to work with this package and user written code based on it. Especially nowadays it makes more sense to provide summaries and comments within the code for a very clear understanding and an instant understanding of how to add new values, edit existing ones, and other things like navigate package APIs. 

### Reactive nature

Besides a simple read entry via API that returns you the localized value that you asked for, you can also define a local reactive field in your class, which is, when initialized with its table and entry addresses, automatically synchronizes their target once when started, and every time global language changes or when its table value changes.

```csharp
using reromanlee.ReactiveLocalizer;

public class MyClass
{
    private LocalizerText _playText;
    private LocalizerText _settingsText;
    private LocalizerText _exitText;
}
```

To solve the problem where we have to process thousands or more of those instances syncing up after a language switch, we do the same approach as `R3` solution does: each property is being synced up in order by Localizer instance itself, so there is no simultaneous overhead on resources drawn upon language changes or any other event.

Components that are attached to GameObjects are also reactive by their design, and they are being reactively synced up with current language state.

### Scene components

The most important feature for quick and easy access of desired localization entry is by searching it. Not so many localization tools provide a quick and easy to access search bar in component where you start typing the name and immediately see options which you can click and it automatically is being chosen for this component to sync target fields with.

### Dependency Injection

Since we're going to be dealing with many different architectures, from a production grade high end project with strict rules and conventions to a small prototype that demands rapid prototyping, we have to be flexible.

One way to make such a thing is by providing an API for creating a Singleton and then using it via API and no DI at all. Or creating another way which is to create an instance and then work with it within Dependency scope like VContainer or virtually anything else.

Another way is to avoid anti patterns at all cost, and to provide a middle ground solution by creating a Singleton workaround within Samples folder of this package.

### Custom languages

Languages are collected from tables and compared between each other to make a list of unique languages available.

### Code generation

Code generation is the foundation of how tables are being processed and enums being generated to avoid string literals when accessing tables, languages and entries within the tables with their according language.

### Other highly valuable utilites

1. Based on currently added tables, we can generate a fixed string of characters which can be used to cut off unnecessary font glyphs. One string per each language added.