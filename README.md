<!-- CHANGE LOG
- 2026-03-09 | Request: Tag-only docs | Replace category references with tag + library guidance.
- 2026-01-02 | Request: Sync app version | Update README version header to 2.1.2.
-->
# PromptLoom 2.1.2 (Windows 11)

WPF (.NET 8) prompt builder driven by ComfyUI-style wildcard `.txt` files arranged in folders.

## Highlights
- Tags drive prompt building from wildcard files arranged in folders.
- Select any number of tag-matched files to build multi-line prompts.
- Tag search suggests related terms and shows match counts.
- Prompt order follows the selected file ordering.
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
`Library/<TagPath>/*.txt` (one entry per line)
