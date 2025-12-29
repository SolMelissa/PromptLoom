CHANGE LOG
- 2025-12-29 | Request: Create versioning policy | Added versioning rules and branch conventions.

# Versioning

## Format
- Release (main): MAJOR.MINOR.PATCH.COMMITCOUNT+CHANGEMADE
- Dev (all non-main): MAJOR.MINOR.PATCH.COMMITCOUNT.dev+CHANGEMADE
- Titlebar: MAJOR.MINOR.PATCH #COMMITCOUNT

## Commit Count
- COMMITCOUNT = git rev-list --count HEAD

## CHANGEMADE (derived from branch name)
Branch format:
- MAJOR.MINOR.PATCH-<tag>-<slug>

Allowed tags:
- feat, fix, refactor, chore, test, docs

Derivation:
- CHANGEMADE = <tag>.<slug>
- Example: 1.8.3-feat-ui -> feat.ui

## Branch Rules
- Version-first branch names are required.
- Build must fail if branch name does not match the required format.
- On failure, prompt for user input to supply CHANGEMADE.

## Version Bump Rules
- MAJOR: breaking changes or incompatible data format
- MINOR: new features, additive behavior
- PATCH: bug fixes or internal changes without behavior change

## Centralization
- Versions are defined in Directory.Build.props.
- AssemblyVersion = MAJOR.MINOR.PATCH.0
