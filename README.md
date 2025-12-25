# PromptLoom 1.5.1 (Windows 11)

WPF (.NET 8) prompt builder driven by ComfyUI-style wildcard `.txt` files arranged in folders.

## Highlights
- Categories are draggable **cards** (order replaces priority sliders).
- Categories start **unchecked/collapsed**. Expands only when enabled.
- **Multiple subcategories per category**: check any number to include them.
- Middle pane edits a subcategory (click its name from the category card).
- Cleaner spacing around prefixes/suffixes.
- Pastel theme: cyan inputs + pastel pink prompt area.

## Run
```powershell
dotnet restore
dotnet run
```

Convenience scripts (from the project folder):
- `run.ps1`
- `run.cmd`

## Folder layout
`Categories/<Category>/<Subcategory>/*.txt` (one entry per line)

Metadata:
- `Categories/<Category>/_category.json`
- `Categories/<Category>/<Subcategory>/_subcategory.json`
