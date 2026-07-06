# #20 build plan — FishNet CSP prediction-enabled RaiderMovement

Prep artifact written 2026-07-06 for the #20 build session (post-#21). Grounded in the
in-repo FishNet 4.7.2 CharacterController prediction demo and the current
server-authoritative movement. **Nothing here is committed to `main`** — the adopt/defer
call is made after the feel-test (ADR-0007). This is a branch (`feat/issue-20-prediction`)
work-plan, not a closed decision.

## The shape flip

Current (server-authoritative, ADR-0005): `RaiderMovement : NetworkBehaviour` subscribes
`TimeManager.OnTick`. Owner reads `IRaiderIntentProvider.GetIntent()` → `ServerRpcSendIntent`
(unreliable) each tick → server applies facing (LookRotation from `AimPoint`), moves the
`CharacterController`, then `RaiderWeapon.TryFireOnServer`. **NetworkTransform replicates the
resulting position + rotation to everyone.**

Target (CSP): `RaiderMovement : TickNetworkBehaviour` (or NetworkBehaviour with
`SetTickCallbacks`), with:

- `struct ReplicateData : IReplicateData` — the owner's per-tick input. Carries `Move`
  (Vector2) and, pending decision #1, `AimPoint` and `FirePressed`. Needs `GetTick`/`SetTick`/
  `Dispose`. This *replaces* `RaiderIntent` on the wire.
- `struct ReconcileData : IReconcileData` — authoritative state: `Position`,
  `_verticalVelocity`, and (pending decision #1) facing. Needs `GetTick`/`SetTick`/`Dispose`.
- `[Replicate] PerformReplicate(ReplicateData, state, channel)` — runs the movement sim.
  Executes on the owner (predicted) and server (authoritative), and re-runs on the owner
  during reconcile replay. Move logic (clamp, gravity, `_controller.Move`) lives here.
- `[Reconcile] PerformReconcile(ReconcileData, channel)` — the CC `enabled = false` →
  write `transform.position` → `enabled = true` dance (demo lines 396–429). **This is why we
  drop NetworkTransform for position: reconcile now owns the transform.**
- `CreateReconcile()` override — builds `ReconcileData` from current state and calls
  `PerformReconcile`. Demo builds it in `OnPostTick`; we have no platform trigger, so
  building in `OnTick` after the sim is likely fine (revisit if colliders need post-sim trace).

## Two decisions to make during the build

**Decision #1 — is facing predicted or server-authoritative?**
- **Predict it** (AimPoint in ReplicateData, LookRotation in replicate, rotation in
  ReconcileData): owner gets instant aim response — the whole point of CSP. Costs a rotation
  field in reconcile and more replay surface. ADR-0001 keeps facing decoupled from motion, so
  this is clean to predict independently.
- **Keep it server-auth** (AimPoint stays a separate ServerRpc or rides ReplicateData but is
  applied server-side only; NetworkTransform keeps *rotation* sync on, position off): smaller
  rewrite, but owner aim still has RTT latency — undercuts the feel-test.
- *Lean*: predict facing too, since aim responsiveness is a core-feel lever and the demo's
  pattern extends to it cheaply. Confirm by feel.

**Decision #2 — fire stays server-only (ADR-0005), NOT predicted.**
- Projectiles are server-owned, server-spawned, no client authority (see `RaiderWeapon`
  comments). Do **not** predict-spawn projectiles. `FirePressed` rides `ReplicateData` (it is
  owner input), but is consumed **server-side only** — read it in the server branch of the
  replicate (or a small server-side hook after the authoritative tick) and call
  `TryFireOnServer`. Keep the muzzle origin computed from the server-authoritative transform,
  exactly as today. This preserves ADR-0005 with zero change to `RaiderWeapon`/`Projectile`.

## NetworkTransform / prefab config (RaiderPrefabGenerator)

- **Drop position sync** on the Raider's NetworkTransform (reconcile drives position).
- Rotation sync: keep ON if facing stays server-auth (decision #1b); turn OFF if facing is
  predicted (decision #1a, reconcile carries it).
- Confirm a `PredictionManager` is present/configured on the NetworkManager (interpolation +
  reconcile settings). The demo relies on it; check `NetworkManagerGenerator` (or wherever the
  managers are authored) and add `PredictionManager` if missing.
- **This touches generators → CLI regen with the editor CLOSED, then commit the regenerated
  prefabs/scene/`DefaultPrefabObjects.asset` + `.meta`** (docs/testing.md; handoff landmine).

## Intent seam & providers

- `IRaiderIntentProvider` / `ScriptedRaiderIntentProvider` / `RaiderInputIntentProvider`
  survive as the owner-side input source. `BuildMoveData()` (owner-only) reads the provider and
  packs a `ReplicateData`. The `SetIntentProvider` test seam is preservable — keep it so the
  PlayMode suite can inject scripted input.
- `RaiderIntent` (struct) is effectively superseded by `ReplicateData`. Decide whether to keep
  `RaiderIntent` as the provider's return type and adapt into `ReplicateData`, or have providers
  return `ReplicateData` directly. *Lean*: keep `RaiderIntent` as the provider contract (minimal
  churn to providers + tests) and adapt in `BuildMoveData`.

## Latency simulator (prerequisite for a feelable side-by-side)

FishNet's framework-level simulator, transport-agnostic, lives on `TransportManager`:

```csharp
var sim = networkManager.TransportManager.LatencySimulator;
sim.SetLatency(100);      // ms added per packet; DOUBLED when acting as host
sim.SetEnabled(true);     // flip live to feel with/without
// optional: sim.SetPacketLoss(0.02); sim.SetOutOfOrder(0.02);
```

- Serialized on `TransportManager` (NetworkManager inspector) too, but a serialized value hits
  all MPPM instances at once and can't be toggled live. **Recommended: a tiny debug
  `LatencyToggle` MonoBehaviour** on the main-editor NetworkManager that flips
  `SetEnabled`/`SetLatency` on a keypress, so the with/without difference is felt back-to-back
  in one session. Note: host latency is doubled, and added latency is floored at one tick.
- This component is also usable in **#21** to feel the *current* server-auth model under
  latency — the baseline Fernando judges CSP against.
- Suggested feel values: 0ms (baseline), ~80–120ms RTT (typical broadband), ~200ms (stress).

## Test suite rework (`RaiderMovementTests` + neighbors)

- Server still simulates the authoritative tick via replicate, so **server-side position
  assertions largely hold** (`OwnerIntent_MovesRaiderOnServer`, `Raider_IsBlockedByObstacle`,
  `NonOwnerIntent_DoesNotMoveAnotherRaider`). The injection path changes: scripted input must
  reach `BuildMoveData` on the owner.
- `OwnerMovement_IsObservedByOtherClient` — observer now sees a predicted/reconciled transform
  instead of a NetworkTransform-interpolated one; the >0.5m planar-movement assertion should
  still pass but may need a longer settle window.
- Add a **reconcile/prediction-specific test**: under simulated latency, the owner's *local*
  predicted position leads the server position, and converges after reconcile (no permanent
  desync / no rubber-banding beyond threshold).
- Neighboring suites that lean on movement/facing: `RaiderAimTests` (facing — impacted by
  decision #1), `RaiderFireTests` / `RaiderCombatTests` (fire origin from server transform —
  should be stable if fire stays server-only), `RaiderCameraRigTests` (camera follows the
  owner transform — verify it tracks the predicted transform smoothly).
- Keep the whole PlayMode suite green in the adopted config. **#27's flaky aim test is the only
  allowed red** — confirm it's the sole failure before calling a run green.

## Sequencing for the build session

1. Feel the **baseline** first (#21 + latency toggle on the current model) — the reference point.
2. `ReplicateData`/`ReconcileData` structs + replicate/reconcile skeleton (facing decision #1).
3. Prefab/generator config (drop NT position; PredictionManager) → CLI regen (editor closed).
4. Fire seam (server-only consume of `FirePressed`) — verify `RaiderWeapon` untouched.
5. Latency `LatencyToggle` component wired for live flipping.
6. Rework the test suite; get the full PlayMode suite green (only #27 red).
7. MPPM three-window feel-test with/without latency → fill ADR-0007 → adopt or defer.

## Exit

Suite green in the adopted config (only #27 red), ADR-0007 recorded (evaluated / felt /
decided / reopening = the PvP milestone by definition). If deferred after feeling it, preserve
this branch + ADR as the resurrection point.
