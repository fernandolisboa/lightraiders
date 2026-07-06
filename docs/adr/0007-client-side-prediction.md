# Client-side prediction for Raider movement (CSP go/no-go)

> **STATUS: DRAFT — decision pending the feel-test.** This ADR is scaffolded ahead of the
> #20 build so the evaluation is recorded as it happens. The adopt/defer call is Fernando's,
> made after feeling the with/without side-by-side under simulated latency. Do not treat the
> Decision section as settled until the STATUS line says so.

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

- **Evaluated:** _(what was built and measured — latency values tried, config: facing
  predicted or not, reconcile cadence)_
- **Felt:** _(the honest subjective read at each latency — 0ms / ~100ms / ~200ms, with vs.
  without prediction; rubber-banding, jitter, aim responsiveness)_
- **Decided:** _(adopt → merged with suite green; or defer → preserved on
  `feat/issue-20-prediction` + this ADR as the resurrection point)_

## Reopening condition

The PvP milestone reopens this by definition (uncheatable-host territory, lag compensation
comes online). If deferred, resurrect from the `feat/issue-20-prediction` branch and this ADR.
