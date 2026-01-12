CHANGE LOG
- 2026-03-09 | Request: Tag-only docs | Remove category references from changelog entries.
- 2025-12-30 | Request: Bump major version | Added 2.0.0 entry.

# PromptLoom Changelog
## 2.0.0
- Version reset to 2.0.0 for the Codex CLI and GitHub era.

## 1.8.0.5
- UI: Prompt tab now shows a generation progress indicator + status message while SwarmUI generation is in-flight.
- UI: Prompt tab now shows a preview of the most recently generated image (best-effort download from the SwarmUI response).

## 1.8.0.4
- Fixed build: eliminated CS0579 duplicate assembly attribute errors by disabling SDK auto-generated assembly info and defining it once in Properties/AssemblyInfo.cs.
- Fixed SwarmUI integration: SwarmUI's /API/GenerateText2Image now requires a `model` value; the bridge now attempts to infer the currently loaded model (and a safe default resolution) before sending.

## 1.8.0.2
- Fixed SwarmUi.Client build error (CS0246): added missing `using Microsoft.AspNetCore.Routing;` for `IEndpointRouteBuilder` in the optional webhook helper.

## 1.8.0.1
- SwarmUI integration: Send prompt using SwarmUI's current UI settings (do not override model or resolution).

## 1.8.0.0
- Added SwarmUI integration (SwarmUiBridge): includes SwarmUi.Client source as a project reference.
- UI: Added "Send to SwarmUI" button next to Copy/Randomize, which calls SwarmUI's GenerateText2Image API using the current PromptLoom prompt.

## 1.7.5.7
- Fixed build: restored missing namespace/usings in Services/PromptEngine.cs (Regex + model types).
## 1.7.5.6
- Fixed underlying XAML structure issue: closed the View-pane DockPanel wrapper that was left unclosed, causing cascading MC3000 tag mismatch errors.
## 1.7.5.5
- Fixed XAML tag mismatch in MainWindow.xaml (removed stray </DockPanel> causing MC3000).
- Fixed Subcategories TabControl layout: moved it to Grid.Row=1 so header DockPanel stays in row 0.
## 1.7.5.4
- Fixed build error: removed duplicate Command attributes in MainWindow.xaml Button elements.
## 1.7.5.3
- Fixed build error: removed duplicate XAML attributes (Padding/Margin/etc.) introduced during label-style conversion.
- Added defensive dedup pass on common layout attributes in MainWindow.xaml.
## 1.7.5.2
- Fixed build error: removed duplicate HorizontalAlignment attribute in MainWindow.xaml.
## 1.7.5.1
- Fixed XAML parse error caused by malformed binding quotes (""{Binding ...}).
## 1.7.5
- Fixed build error: removed invalid Command binding on TextBlock in MainWindow.xaml.
  - Restored a label-like Button for rows that need a Command.
## 1.7.4
- Cleanup: reduced allocations in wildcard .txt loading (loop instead of LINQ).
- Cleanup: compiled regexes in prompt normalization.
- Cleanup: removed unused using directives where safe.
## 1.7.2
- Swapped Status and System panel content while keeping labels the same.
  - Status now shows high-level SystemMessages.
  - System now shows diagnostic ErrorEntries.

## 1.7.0.6
- UI: moved status/diagnostics output to the bottom of the Prompt tab.

## 1.7.0.5
- Fix: If multiple .txt files are checked in a subcategory, PromptEngine now uses **all** checked files in order
  (even if the subcategory is in single-file mode), preventing the "only first file shows" confusion.
- Fix: Loader no longer force-enables any selected file during migration/load; checkbox state is respected.

## 1.7.0.4
- Fix: Tabs visibility improvements (App.xaml) for nested TabControls.
- Fix: PromptEngine no longer mutates file checkbox state.
- Add: UI toggle for "Use all enabled files" within the subcategory editor.

## 1.7.0.3
- Advanced View: re-added Select All/None buttons for file lists inside each subcategory (Entries).

## 1.7.0.2

- Advanced View now uses nested tabs: primary tag groups on top (color-coded), subfolders below (inherits selected group color).
## 1.7.0.1

- Fix warning CS0162 (unreachable code) in PromptEngine.
- Fix prompt generation regression: selecting a file in the UI now updates the owning selection entry,
  so single-file mode and persistence use the correct file.
- Default subcategory behavior is now **UseAllTxtFiles=true** for new configs (matches file-collection mental model).

## 1.7.0

- Design fix: file groups now expose a file-level list (Entries) mirroring the on-disk *.txt files.
- UI: manage individual files with checkbox include and up/down ordering.
- Prompt generation: build from enabled files (or selected file in single-file mode).
- Migration: legacy _subcategory.json (UseAllTxtFiles + SelectedTxtFile) is upgraded into Entries[] automatically.

## 1.6.10

- Fix build error CS0111 (duplicate ErrorReporter.Error method). Kept a single canonical Error(...) method.

## 1.6.9

- Fix build error CS1061: ErrorReporter missing Error(...) method (used by AppDataStore).

## 1.6.8

### Build hardening + Root "/" subcategory
- Added csproj excludes so `bin/**` and `obj/**` are never compiled or packed when building from a source folder/zip.
- Added explicit usings in services that relied on implicit framework imports.
- Implemented the special root subcategory named "/" to capture `.txt` files directly inside a category folder (metadata stored as `_root_subcategory.json`).

## 1.6.6

### Build reliability
- Added explicit framework usings in key files so the project compiles cleanly even if implicit-usings behavior differs across environments.
- Updated version numbers to 1.6.6.

## 1.6.5

### Crash-safe startup logging
- Added a Temp "bootstrap" log file created at the very start of Program.Main, so even failures before WPF startup are captured.
- Hardened ErrorReporter initialization with AppData -> Temp fallback so log creation can't crash the app.
- Startup error dialog now includes the bootstrap log path when available.

## 1.6.3

### Library restore flow
- PromptLoom no longer auto-populates the Library on first run.
- Added **File -> Restore Original Library** to copy the bundled starter Library into AppData.
- A timestamped backup zip of your current Library is created in the Output folder before restore.

## 1.6.2

### Layout + usability
- Removed the legacy **Negative** library folder (it is no longer loaded, and the bundled folder was removed).
- Advanced View is now reliably scrollable for long library or subfolder content.
- Simple View cards now render in a grid that fills **top to bottom**, then **left to right**.

## 1.6.1

### UI polish
- Re-styled icon buttons to be round, smaller, and more readable.
- Added padding between titles and the action buttons.
- Swapped MDL2 glyphs for crisp vector icons.
- Normalized action button sizing using fixed-width button columns.

## 1.6.0

### UI
- System Messages and Diagnostics are now shown in tabs within the Prompt panel (Prompt / System / Diagnostics).
- Removes the tiny inline diagnostics strip and the collapsible expander so logs are always readable.

## 1.5.3.4

### Fixes
- Prefix/suffix values now load correctly from user JSON metadata (camelCase `prefix`/`suffix`) by enabling case-insensitive deserialization and using camelCase naming when writing.
- JSON loader now tolerates trailing commas and comments in metadata files.

## 1.5.3.1

### Fixes
- Prevent silent crash when selecting categories/subcategories by removing recursive TwoWay SelectedItem feedback between the left list and the center tabs.
- Add re-entrancy guards in ViewModel selection sync to avoid StackOverflow based process termination.

## 1.5.3.0

### Fixes
- `MainViewModel` no longer calls `Reload()` in its constructor.
- `MainWindow` now constructs the ViewModel, sets `DataContext`, then calls `vm.Initialize()`.
- `CurrentEntry` and `EntrySummary` updates are treated as output-only signals and do not trigger prompt recompute.
- Event handlers are detached before rebuilding the category graph during Reload.
