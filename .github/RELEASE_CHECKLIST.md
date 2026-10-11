# Release checklist

Continuous integration checks what a machine without a display or a device can: the pure assemblies under .NET, every test on the oldest and newest supported Unity, and player builds for WebGL, Android, iOS, macOS and Linux. The rest is checked by hand before each release, most important first.

### Before merging the release pull request

1. Every check of the pull request passes: the fast lane, the tests on both Unity versions, and every player build.
2. The Quick Start sample runs in Windows, Android and WebGL builds: every language switch shows at once, the saved language comes back after a restart, Pirate's missing hint falls back to English, and a development build lists Pseudo while a release build doesn't.
3. The timings in the test results artifacts are no worse than the previous release's, and when the runtime changed, the IL2CPP numbers in `Documentation/PERFORMANCE.md` are measured again on the same machine.
4. In the Editor, on both Unity versions, the table window adds, edits, renames, moves and deletes entries, and Undo takes each back; the Inspector field searches and creates entries; the Preview Language overlay previews every language in Edit Mode while scenes and prefabs save their authored text; and Validate and the build gate report a broken file, an orphan and a missing key.
5. An exchange round trip works: XLSX through Excel or LibreOffice, CSV, XLIFF 2.0 through a translation tool, and an LLM handoff through a chat, each imported through the preview.
6. Every sample imports and compiles with its package on both Unity versions, and runs: TextMeshPro with fonts per language and the coverage check, UI Toolkit with the UXML binding on Unity 6, uGUI Text, Addressables loading a custom table in a build, VContainer starting the localizer, R3, Mods with the example mod in a build, the Editor Tool window, the Unity Localization migration, and the Google Sheets sync pushing to and pulling from a test spreadsheet.
7. A Profiler capture of an Android development build switching languages shows no allocations in steady frames and no new spikes.
8. *Write Agent Guide* writes a section that matches the release, into a project with and without `AGENTS.md`.
9. `package.json` has the new version, `CHANGELOG.md` has its section under the version instead of *Unreleased*, and the README and `Documentation/` describe the release; a release that changes the file format updates `Documentation/FORMAT.md`, and a major release adds a section to `Documentation/MIGRATION.md`.

### After merging

1. Tag the squashed commit on `main` with the version alone, such as `1.0.0`.
2. Export the signed tarball of the package from the Package Manager.
3. Draft the GitHub release from the tag, named like `v1.0.0 (signed)`, with the changelog section as its notes and the tarball attached.
