# CI — PlayMode tests on GitHub Actions

`.github/workflows/playmode-tests.yml` runs the full PlayMode suite on every pull request, on every push to `main`, and on demand from the Actions tab. It uses [GameCI](https://game.ci) `unity-test-runner`, which runs the Unity Editor in a Docker container. The container's editor version comes from `game/ProjectSettings/ProjectVersion.txt` (currently `6000.3.19f1`). Results show up as a check run on the PR, and the result XML is uploaded as an artifact.

Until the `UNITY_LICENSE` secret exists, the job **skips with a notice** instead of failing. Merging the workflow first is therefore harmless.

## One-time setup: the three secrets

Unity no longer supports web-based manual activation for **Personal** licenses. The license file has to come from a machine where Unity Hub has activated one. You need **Unity Hub only**: the license step does not need the Editor.

1. Install Unity Hub and sign in with your Unity account.
2. In Hub, go to **Preferences → Licenses → Add → Get a free personal license**. If a Personal license is already listed, skip this step.
3. Find the license file. On Windows it is `C:\ProgramData\Unity\Unity_lic.ulf`. (macOS: `/Library/Application Support/Unity/Unity_lic.ulf`; Linux: `~/.local/share/unity3d/Unity/Unity_lic.ulf`.)
4. In GitHub, go to **repo → Settings → Secrets and variables → Actions → New repository secret** and add:
   - `UNITY_LICENSE`: the **entire contents** of `Unity_lic.ulf`. Open it in a text editor and paste all of it, XML included.
   - `UNITY_EMAIL`: your Unity account email.
   - `UNITY_PASSWORD`: your Unity account password.
5. Re-run the workflow from the Actions tab (**PlayMode tests → Run workflow**), or push to any PR.

GameCI combines the file with your email and password to activate the editor inside the container. If Unity later rotates or returns the license (for example, if you deactivate it in Hub), repeat steps 2–4.

## Notes and known risks

- **First run is slow.** It pulls a multi-GB editor image and does a cold import of the project. Later runs restore `game/Library` from the Actions cache.
- **Graphics.** The container has no GPU. The suite is graybox and network-only, so the default GameCI setup should be enough. If a run fails during graphics initialization, the local fallback in `docs/testing.md` (dropping `-batchmode`) does not apply here; report it and add the needed `customParameters` to the workflow.
- **Flaky test #27** (`OwnerAim_FacingIsObservedByOtherClient`) can still fail intermittently. Read its failure message before treating a red run as a regression.
- **Generated assets.** CI runs only the committed assets; it does not run the generators. A PR that changes a generator must still commit the regenerated prefabs and scene (see `docs/testing.md`).
- **Forks.** Pull requests from forks don't receive secrets, so the job skips there.
