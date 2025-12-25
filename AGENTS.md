\# AGENTS.md



\## Project Overview

This is a .NET WPF application using MVVM.

Stability and clarity are prioritized over cleverness.



\## Architectural Rules

\- MVVM is mandatory

\- No business logic in Views

\- Services must be injected

\- Avoid static state



\## Files to Avoid Editing

\- SwarmUIBridge.cs (modify only if absolutely necessary)

\- Generated files in bin/ and obj/



\## Coding Standards

\- Prefer explicit types over var where clarity matters

\- Public methods must be XML-documented

\- Add top-of-file comments describing bugs fixed and why



\## Versioning

\- Do not bump version numbers unless explicitly requested

\- Tags are used for releases



\## Testing \& Safety

\- Do not remove logging

\- Avoid large refactors unless requested



