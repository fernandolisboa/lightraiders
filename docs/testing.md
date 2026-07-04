# Testing — CLI invocation and constraints

## Prerequisite: the Unity editor must be CLOSED

Batch-mode runs lock `game/Library`. If the editor (or any MPPM virtual player) is
open, CLI runs fail or hang. Close everything Unity before running the commands
below.

## Generate session assets (Raider prefab + bootstrap arena scene)

On a fresh checkout, generation is a prerequisite for the test run: the PlayMode
tests fail with an actionable message until the Raider prefab exists.

The `New-Item` line matters because `artifacts/` is gitignored and won't exist on
a fresh checkout, and Unity won't reliably create parent directories for
`-logFile`/`-testResults`.

```powershell
New-Item -ItemType Directory -Force "C:\Users\ferna\source\repos\lightraiders\artifacts" | Out-Null
& "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\Users\ferna\source\repos\lightraiders\game" -executeMethod LightRaiders.Editor.ArenaSceneGenerator.GenerateAll -logFile "C:\Users\ferna\source\repos\lightraiders\artifacts\generate.log"
```

**Generated assets are committed.** The generator's outputs — `Raider.prefab`,
`BootstrapArena.unity`, the materials, and the updated
`Assets/DefaultPrefabObjects.asset` — are committed to git after generation:
GUID stability across machines and MPPM clones requires it. Regeneration is
idempotent and GUID-preserving.

## Run PlayMode tests

```powershell
New-Item -ItemType Directory -Force "C:\Users\ferna\source\repos\lightraiders\artifacts" | Out-Null
& "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe" -runTests -batchmode -projectPath "C:\Users\ferna\source\repos\lightraiders\game" -testPlatform PlayMode -testResults "C:\Users\ferna\source\repos\lightraiders\artifacts\playmode-results.xml" -logFile "C:\Users\ferna\source\repos\lightraiders\artifacts\playmode.log"
```

Note: NO `-quit` on the test command — it aborts the test run.

The hardcoded editor path version (`6000.3.19f1`) must match
`game/ProjectSettings/ProjectVersion.txt`; update both commands above when the
editor is upgraded.

## Exit codes

| Code | Meaning | Where to look |
| ---- | ------- | ------------- |
| 0 | All tests passed | — |
| 2 | Test failures | Read the results XML |
| 3 | Other error (compile failure / project locked) | Read the log file |

## Fallback

If a batch-mode PlayMode run fails during graphics initialization, retry the same
command without `-batchmode`.

`artifacts/` is gitignored; logs and result XMLs land there.

## MPPM landmines

- After editing any networked prefab (e.g. `Raider.prefab`), restart the MPPM
  virtual players. Otherwise clones fail with "Prefab Id not found" — an unfixed
  Unity limitation.
- Keep Domain Reload enabled.
