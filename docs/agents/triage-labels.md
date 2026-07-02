# Triage Labels

The skills speak in terms of five canonical triage roles. This file maps those roles to the actual label strings used in this repo's issue tracker.

| Label in mattpocock/skills | Label in our tracker | Meaning                                  |
| -------------------------- | -------------------- | ---------------------------------------- |
| `needs-triage`             | `needs-triage`       | Maintainer needs to evaluate this issue  |
| `needs-info`               | `needs-info`         | Waiting on reporter for more information |
| `ready-for-agent`          | `ready-for-agent`    | Fully specified, ready for an AFK agent  |
| `ready-for-human`          | `ready-for-human`    | Requires human implementation            |
| `wontfix`                  | `wontfix`            | Will not be actioned                     |

When a skill mentions a role (e.g. "apply the AFK-ready triage label"), use the corresponding label string from this table.

## Supplementary labels

Beyond the triage roles, every issue should carry one **type**, one **phase**, and one or more **area** labels where applicable.

### Type

| Label           | Meaning                                    |
| --------------- | ------------------------------------------ |
| `bug`           | Something isn't working                    |
| `enhancement`   | New feature or improvement                 |
| `documentation` | Docs, ADRs, CONTEXT.md, handoffs           |
| `refactor`      | Restructuring with no behavior change      |

### Phase

Mirrors the build phases in `docs/vertical-slice-plan.md`. Tag each issue with the phase it belongs to.

| Label                 | Scope                                                         |
| --------------------- | ------------------------------------------------------------- |
| `phase-0-foundations` | Unity/C#/FishNet learning; networked capsule moves and shoots |
| `phase-1-core-feel`   | Camera rig, movement, 360° aim, projectile combat, graybox    |
| `phase-2-layers`      | Layer system, door transitions, graybox Matins map skeleton   |
| `phase-3-choir`       | The five Choir units + the Escalation director                |
| `phase-4-economy`     | Loot families, inventory, Stash/Workbench/Recycler/Trader     |
| `phase-5-progression` | Skill tree, story Project, Trials, death rules                |
| `phase-6-art`         | Replace grayboxes: art, VFX, audio, UI polish                 |
| `phase-7-playtest`    | Friends playtests, weekly builds, tuning                      |

### Area

Cross-cutting concerns — useful when, say, a netcode bug surfaces during the art phase.

| Label           | Scope                                                  |
| --------------- | ------------------------------------------------------ |
| `area:netcode`  | FishNet, replication, relay, client/server authority   |
| `area:combat`   | Movement, aim, weapons, damage, health/shield          |
| `area:world`    | Matins map, Layers, Waygates, Exfil sites, Hatches     |
| `area:choir-ai` | Enemy behaviors and the Escalation director            |
| `area:economy`  | Loot, inventory, Stash, crafting, Trader, Lumens       |
| `area:meta`     | Skill tree, Projects, Trials, the Fold screens         |
| `area:ux`       | UI, HUD, audio, VFX, game feel                         |

Edit the tables to match whatever vocabulary you actually use.
