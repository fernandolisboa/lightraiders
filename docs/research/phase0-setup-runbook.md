# Phase 0 Setup Runbook

Synthesized 2026-07-02 from a 7-topic research sweep with adversarial verification of decision-critical claims (29 claims checked, 2 corrected — corrections are already folded in below). Companion to the Phase 0 section of `docs/vertical-slice-plan.md`.

## 1. Environment: Windows-native (decided 2026-07-02)

The repo and Unity project live on the Windows filesystem (`C:\dev\lightraiders`), with Claude Code running natively on Windows (PowerShell). The WSL2 setup is retired for this project.

Why (each point independently sufficient):

- Unity **does not support** opening projects from network shares — its asset database needs memory-mapped files, and `\\wsl$` is a network redirector. Projects on WSL's ext4 are unsupported, not just slow (https://issuetracker.unity3d.com/issues/cannot-create-a-new-unity-project-in-a-shared-network-folder-with-r-slash-w-access).
- A WSL-side agent working on `/mnt/c` pays a ~10–25× 9P tax (`git status` ~10 s vs ~400 ms; https://markentier.tech/posts/2020/10/faster-git-under-wsl2/). Anthropic's own troubleshooting docs name "run Claude Code natively on Windows" as the fix (https://code.claude.com/docs/en/troubleshooting).
- Claude Code has been native on Windows since v1.0.51 (July 2025) and native install is the officially recommended method; it needs Git for Windows for the Bash tool. Known limitation: no bash sandboxing on native Windows (https://code.claude.com/docs/en/setup).
- Native Windows Claude Code gets correct `C:\` paths in Unity-generated `.sln`/`.csproj` and plain-localhost access to Unity MCP bridges (which WSL needs netsh port-forwarding for) (https://github.com/CoplayDev/unity-mcp/wiki/3.-Common-Setup-Problems).
- Empirically: WSL2 crashed repeatedly during this project's kickoff session, killing background work and wiping `/tmp`.

Windows-side one-time setup, in PowerShell:

```powershell
winget install -e --id Git.Git          # includes Git LFS and Git Bash
winget install -e --id GitHub.cli
winget install -e --id Unity.UnityHub   # fallback: UnityHubSetup-x64.exe from unity.com/download on hash mismatch
irm https://claude.ai/install.ps1 | iex # Claude Code native
gh auth login                            # HTTPS + credential manager is simplest
gh repo clone fernandolisboa/lightraiders C:\dev\lightraiders
cd C:\dev\lightraiders; git lfs install
# optional, speeds Unity + git noticeably (admin shell):
Add-MpPreference -ExclusionPath 'C:\dev'
```

Set git identity if fresh: `git config --global user.name/user.email`.

## 2. Unity 6.3 LTS install

- **Version: latest 6000.3.x LTS patch** — 6000.3.19f1 as of 2026-07-02 (Unity release API). NOT 6000.0 (its LTS support ends ~Oct 2026; 6.3 runs to Dec 2027), not the 6000.4/6000.5 tech streams. Stay current on 6000.3 patches — 6000.3.0f1 itself shipped with a known URP compile regression fixed in later patches.
- Modules: keep the preselected **Visual Studio Community** only if not using Rider (see §6); add **Windows Build Support (IL2CPP)** and **Documentation**. Skip everything else — Dedicated Server modules are a two-click add later. ~12 GB installed + ~7 GB temp downloads; budget 25–30 GB free (project Library caches grow).
- Licensing: zero action. Runtime Fee is cancelled; Unity Personal is free with full features and no splash mandate up to $200k trailing-12-month revenue/funding (https://unity.com/products/pricing-updates).

## 3. Repo layout and git hygiene

- **Unity project goes in a subfolder** (e.g. `game/`), keeping `docs/`, `README.md`, `CONTEXT.md` at root — the standard pattern when a repo is more than the Unity project.
- The canonical `Unity.gitignore` (github/gitignore — now includes Unity 6's `.utmp/`) goes **inside the Unity subfolder**, since it's written to sit at the Unity project root.
- **LFS before any binary lands** (retrofitting requires history rewrite): the repo-root `.gitattributes` (committed 2026-07-02, based on the webbertakken gist) LFS-tracks images/models/audio/video/`.unitypackage` and the known-binary `*LightingData/*NavMesh/*OcclusionCullingData/*Terrain.asset` files, routes Unity YAML to `unityyamlmerge`, and normalizes line endings. GitHub free tier: 10 GiB LFS storage + 10 GiB/month bandwidth, $0 budget = blocked not billed; 100 MiB hard limit for non-LFS files.
- After creating the project, **verify** (don't expect to change — both are Unity 6 defaults): Edit > Project Settings > Editor > Asset Serialization = **Force Text** (enable "Reduce version control noise"); Project Settings > Version Control = **Visible Meta Files**.
- Later, when branches touch scenes: configure UnityYAMLMerge as git mergetool (`C:\Program Files\Unity\Hub\Editor\<ver>\Editor\Data\Tools\UnityYAMLMerge.exe`). Low urgency solo.

## 4. FishNet

- Current stable **4.7.2R** (Apr 2026, actively maintained ~monthly); Unity 6 fully supported. Install the **free tier from the Unity Asset Store** (the officially recommended method).
- **Never run pre-4.6.19 FishNet on Unity 6** (old builds had Unity 6-specific multithreading/API bugs). **On every FishNet update: delete `Assets/FishNet` entirely, then re-import** — never import over a stale copy.
- Free tier is the full netcode (server-authoritative, prediction, SyncTypes, scene management, no CCU caps). **Pro (~$50, often $25) is deferred to the PvP milestone** — its lag compensation (collider rollback) matters for PvP hit registration; nothing in the co-op slice is paywalled.
- Learning path: official GitBook "Getting Started" series (https://fish-networking.gitbook.io/docs/tutorials/getting-started), then the in-package `Assets/FishNet/Demos` scenes — SceneManager and Prediction demos first. Join the FirstGearGames Discord immediately (~9.5k members; it is the mandatory support channel — GitHub issues require prior Discord troubleshooting). **Avoid 2022–2023 YouTube tutorials** (FishNet 3.x era; v4 API drift).

## 5. Second-client testing: MPPM

- Use Unity's **Multiplayer Play Mode** (netcode-agnostic; FishNet's own tutorial recommends it for Unity 6+ and detects MPPM clones in core). ParrelSync is the fallback only: unmaintained since June 2024, never officially Unity 6-verified, "stable but inactive" per FishNet docs.
- On Unity 6.3 LTS, MPPM 3.0+ functionality is largely built into the engine. Setup: Package Manager > Unity Registry > "Multiplayer Play Mode", then Window > Multiplayer > Multiplayer Play Mode, enable 2–3 virtual players (first activation builds clones under `Library/VP`, takes minutes). Main editor + 3 virtual players = 4, covering the 1–3 co-op requirement.
- Workflow rules: all edits in the main editor (virtual players are read-only); branch startup logic with MPPM player tags or FishNet's `Configuration.IsMultiplayerClone()`.
- **CORRECTED CLAIM — known unfixed limitation**: editing a networked prefab while virtual players run causes "Prefab Id not found" errors. FishNet issue #916 was closed 2026-02 as a **Unity limitation, without a fix** (the "fixed in 4.6.10" claim circulating is wrong — it was reopened and never resolved). Mitigation: keep **Domain Reload enabled** (the repro requires it disabled) and **restart virtual players after editing any networked prefab**.
- Timebox one evening to validate host + 2 clients moving a synced cube; fall back to ParrelSync (git URL: `https://github.com/VeriorPies/ParrelSync.git?path=/ParrelSync`) only if MPPM misbehaves on this machine.

## 6. IDE: Rider, with a licensing plan

- **JetBrains Rider** under the free non-commercial license: best Unity integration of the three, full-featured, and gracefully re-syncs files edited externally by an agent (silent reload on window activation). Telemetry is mandatory on the free license.
- **The catch (verified)**: JetBrains' FAQ counts commercial intent "now or in the future" — a firm plan to sell premium Early Access breaks eligibility. Decision point: when Light Raiders is genuinely headed to paid EA, either buy Rider (~$165–175/yr) or switch to **VS Code + Microsoft's Unity extension** (stable v1.2.2, real attach-debugging; C# Dev Kit is free for individuals *even when selling the game*).
- Visual Studio is not deprecated for Unity but is mid-transition (SDK-style projects, .slnx churn, Hub still bundles VS 2022) and handles external edits worst. Skip.
- Agent-edit workflow: leave Unity's Auto Refresh on; alt-tab into Unity after agent edits (focus triggers reimport/recompile), Ctrl+R if stale. Never hand-write `.meta` files; when deleting/renaming a `.cs`, move its `.meta` in the same change.

## 7. Learning path (~4 weeks, doing over watching)

| Week | Do | Notes |
|---|---|---|
| 1 | **Unity Essentials** pathway on Unity 6.3 (learn.unity.com) | Officially updated for 6.3; ~1 focused week. Code Monkey's free C# course as reference, basics units only |
| 2 | **Create with Code, through Unit 2** (inside Junior Programmer) | Unit 2 is literally top-down movement + projectiles in Unity 6 — the exact Phase 0 skill. Skip later missions |
| 3 | **Build own graybox top-down shooter** from scratch: WASD + mouse aim + instantiated projectiles + dumb targets | References when stuck: Code Monkey's Kitchen Chaos controller; Sebastian Lague's Create a Game series (concepts only — Unity 5 era, don't follow line-by-line) |
| 4 | **FishNet**: GitBook Getting Started + Beyond the Basics + package demos, then network the Week-3 shooter (spawn players, sync transforms, server-side projectile spawn) | Validates the Phase 0 exit criteria with MPPM as the second client |

Traps: Code Monkey's free multiplayer course teaches Netcode for GameObjects, not FishNet (concepts transfer, code doesn't); any FishNet video older than v4 won't compile as shown — the GitBook docs are the canonical path in 2026.

## Phase 0 exit criteria (from the slice plan)

A capsule moves and shoots a projectile in a networked session with a second client connected.
