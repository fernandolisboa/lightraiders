# Client-side prediction for Raider movement (CSP go/no-go)

> **STATUS: ACCEPTED (2026-11-16).** Fernando felt the with/without side-by-side under
> simulated latency and chose to adopt. The CSP config merges to `main` from
> `feat/issue-20-prediction` after code review and a green PlayMode gate.

## Context

ADR-0005 chose FishNet specifically because it ships client-side prediction (CSP) free and
lag compensation cheap, anticipating the certain-PvP milestone. Phase-1 movement (#15–#19)
shipped server-authoritative: owner intent → ServerRpc → server CC sim → NetworkTransform
replication. At ~0ms on one machine (MPPM three-window) this already feels immediate to the
owner, which is *why* the go/no-go needs a real side-by-side under simulated latency (#20):
the benefit of CSP is invisible without RTT.

The decision: don't defer the feature sight-unseen — **build** the CSP config on a branch,
feel it against the baseline Fernando has already accepted (#21), then adopt or defer with
eyes open. Reference: `Assets/FishNet/Demos/Prediction/CharacterController` (Replicate/
Reconcile + the CC enabled-toggle dance). Build plan:
`docs/research/issue-20-prediction-build-plan.md`.

## Considered Options

- **Adopt CSP now** — predicted owner movement (and possibly facing), reconcile-driven
  transform, NetworkTransform dropped for movement. Owner input feels instant regardless of
  RTT. Cost: large, invasive rewrite of movement + its test suite; more moving parts to
  maintain through the rest of Phase 1+.
- **Defer CSP, keep server-authoritative** — the current shape. Simpler, fully tested, already
  feels good at low latency. Cost: owner aim/move carry RTT latency that will bite at the PvP
  milestone — but that milestone reopens this decision by definition, so deferral is not
  permanent.

## Decision

**Adopt client-side prediction for Raider movement and facing now.** Fire stays server-only.

Why now rather than at the PvP milestone:
- The slice is played host + relay over the internet, so every non-host friend has real RTT. That is exactly where the server-authoritative model's input lag shows.
- Phase 2 builds every Layer transition (doors, Waygates, Hatches) as a teleport on top of the movement model. Migrating after those exist would cost more than migrating now.
- The expensive part (the rewrite and its tests) was already built and green, and the PvP milestone would have reopened the question anyway.

## Evaluated / Felt / Decided

- **Evaluated:** Built the full CSP config. RaiderMovement is a `TickNetworkBehaviour` with
  `[Replicate]`/`[Reconcile]`; **position AND facing are predicted** (ADR-0001 decoupling kept);
  reconcile drives the transform (NetworkTransform dropped for the Raider) and `_enablePrediction`
  is set on the NetworkObject so observers get reconcile/state. **Fire stays server-only**
  (ADR-0005) — `FirePressed` rides the replicate but is consumed only on the server branch, no
  client-predicted projectiles. Reconcile is built every PostTick (no throttling). A `LatencyToggle`
  debug component drives `TransportManager.LatencySimulator` (F9 toggle, F10/F11 ±20ms). Automated
  guards pass owner-local prediction + reconcile convergence at 0ms and under 80ms one-way simulated
  latency; full PlayMode suite green (38/38, only the #27 flake as an occasional red — it passed
  this run). _To measure by feel: 0ms baseline, ~100ms, ~200ms, with F9 on/off._
- **Felt:** Fernando ran the MPPM side-by-side with the `LatencyToggle` at 0ms and under
  simulated latency, with and without prediction, and judged it good enough to adopt. Per-latency
  notes were not recorded. The build had no graphical smoothing, so at 30Hz ticks the root
  moved in tick steps and reconcile corrections snapped. Fixed for adoption (#32): Visual and
  AimIndicator now hang off a `Graphics` child with a `NetworkTickSmoother`, and the camera rig and
  world-space bars follow that child.
- **Decided:** Adopt (2026-11-16). Before merge, a code review found and fixed:
  - Input-less replicates (spawn, respawn, input underrun, spectator future ticks) were snapping
    facing to world origin and could fire. These are now gated on `ReplicateState.Created`.
  - `LatencyToggle` was live in release builds. It is now inert outside the editor and development
    builds.
  - The prediction tests could pass without prediction. They now assert that the owner leads the
    server under latency, and that position and facing converge at rest.
  - Stale NetworkTransform-era comments were updated.

## Reopening condition

The PvP milestone reopens this by definition (uncheatable-host territory, lag compensation
comes online). If deferred, resurrect from the `feat/issue-20-prediction` branch and this ADR.
