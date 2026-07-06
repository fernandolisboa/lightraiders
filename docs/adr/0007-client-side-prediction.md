# Client-side prediction for Raider movement (CSP go/no-go)

> **STATUS: BUILT — decision pending the feel-test.** The CSP config is implemented on
> `feat/issue-20-prediction` and the full PlayMode suite is green (38/38). What remains is
> Fernando's: feel the with/without side-by-side under simulated latency, then record the
> adopt/defer call below. Do not treat the Decision section as settled until this line says so.

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

_TBD after the feel-test._ Record here: adopt or defer, and the one-line reason grounded in
what the latency side-by-side actually felt like.

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
- **Felt:** _(the honest subjective read at each latency — 0ms / ~100ms / ~200ms, with vs.
  without prediction; rubber-banding, jitter, aim responsiveness)_
- **Decided:** _(adopt → merged with suite green; or defer → preserved on
  `feat/issue-20-prediction` + this ADR as the resurrection point)_

## Reopening condition

The PvP milestone reopens this by definition (uncheatable-host territory, lag compensation
comes online). If deferred, resurrect from the `feat/issue-20-prediction` branch and this ADR.
