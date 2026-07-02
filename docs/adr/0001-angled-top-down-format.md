# Angled top-down view, not side-view platformer

Light Raiders is an extraction shooter, and the original vision left the format open between a side-view 2.5D platformer and an angled top-down view. We chose angled top-down (3/4 camera over a 3D world, à la Hades' camera / ZERO Sievert's layout) because it is the proven format for 2D-plane extraction shooters, natively supports the hide-inside-buildings mechanic via roof cutaway + line-of-sight, allows 360° aiming (essential for meaningful gun variety), and is the cheapest format per square meter of map for a solo developer. The "layers behind layers" idea survives as interiors, upper floors, and underground levels rather than side-scroller depth lanes.

## Considered Options

- **Side-view 2.5D platformer** — more distinctive (no side-view extraction shooter exists), and doors-to-back-layers fit it naturally. Rejected: aiming flattens to mostly-horizontal (kills gun variety), PvP readability suffers, and large hand-crafted platforming maps with non-pixelated 3D art are the most expensive level format for a solo dev.
- **Graybox both, pick by feel** — rejected to save ~2 weeks; top-down's advantages were decisive on paper.
