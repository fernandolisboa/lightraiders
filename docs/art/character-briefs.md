# Character Design Briefs

> Art-direction briefs and image-generation prompts for every character in the vertical slice. Sourced from `CONTEXT.md` and `docs/vertical-slice-plan.md`.

## Decisions

- **Art style: Synty-style low-poly (2026-11-20).** Characters must match the Synty POLYGON environments planned for Phase 6 (`docs/vertical-slice-plan.md`). Every prompt below uses the low-poly `Render style:` sentence. The earlier stylised-realism and photoreal variants are kept under *Alternative render styles* for reference only.
- **Production pipeline (Phase 6, not before).** Humans (Raider, Trader): Synty POLYGON characters + Mixamo / Animation Base Locomotion. Choir (no pack covers them): Step 1 portrait → Step 2 three-view sheet → image-to-3D (e.g. Meshy, low-poly mode, A-pose, auto-rig; Tripo as fallback for creatures) → cleanup in Blender → glow done as URP emissive material + bloom, not baked into textures. Target roughly 5k–15k triangles per enemy at the angled top-down camera (ADR 0001).
- **Licensing.** Only use AI-3D outputs under a licence that allows private commercial use (e.g. a paid Meshy plan; the free tier is CC BY 4.0). If the game ships on Steam, AI-generated content must be disclosed.

## How to use

For each character, run **Step 1 · Portrait** first to lock the face, colours and glow, then run **Step 2 · Body & 3 views** on that same character to get the front / ¾ / back turnaround. Both prompts are self-contained. The closing "Do not include" line keeps literal angel / halo / wing / cross iconography out: Light Raiders' religious resonance is *structural, never stated*.

## World rules

- **The two sides.** Raiders & their kin are shadow made practical — fully human, matte, muted, un-glowing salvaged gear. The Choir are light made flesh — luminous, radiant, transfigured humans.
- **The Light.** A warm radiant gold-white energy — the world's treasure and the Choir's substance. It glows from inside Choir bodies, out through cracks, seams and eyes.
- **Family rule.** Every Choir rank is the same species at a different stage of the same transformation. Each still owns a distinct silhouette + glow tint for instant top-down ID.
- **The throughline.** The Choir were human once and took too much Light. Lesser ranks still look half-human; greater ranks are almost pure light. Implied, never stated.

## Protagonist

### The Raider

*Player · In slice*

One who leaves the shadowed places to steal light from the lit world and return alive. This is the player — 1–3 of them per raid, co-op.

- **Read from above:** Human scavenger-soldier; bulky loot backpack is the top-down signature. One bright swappable squad-accent colour.
- **Palette:** Shadow & salvage — matte, cool, muted (`#2b3038`, `#4a5560`, `#8a8073`, `#5bd6c4`)

**Step 1 · Portrait**

```text
Character portrait for the stylised top-down sci-fi extraction game "Light Raiders". A head-and-shoulders bust portrait of a RAIDER on a seamless neutral-grey studio background with soft even lighting. Render style: a stylised low-poly 3D game asset in the Synty / KayKit style — chunky simplified geometry, clean flat-shaded colours, minimal texture detail, game-ready look. World rule: Raiders live in shadow and are fully human and un-glowing — matte, muted, salvaged gear, never emitting light.

A weathered, capable human face, partly hidden behind a half-face respirator or a scarf pulled up over the mouth and nose; a hood or a scuffed helmet carrying one saturated accent-colour stripe for squad identification; harness straps and a worn jacket collar of dark fabric and scuffed grey metal across the shoulders. Grounded, human, practical, faintly grim.

Do not include any glow or luminous materials, clean military uniforms, or superhero styling.
```

**Step 2 · Body & 3 views**

```text
Full-body character turnaround for the stylised top-down sci-fi extraction game "Light Raiders", based on the established portrait of this RAIDER — keep the exact same face, gear, colours and accent stripe. Show the full body in three views on one sheet — front, 3/4 side, and back — feet-to-head with nothing cropped, seamless neutral-grey background, soft even lighting. Render style: a stylised low-poly 3D game asset in the Synty / KayKit style — chunky simplified geometry, clean flat-shaded colours, minimal texture detail, game-ready look. The design must read from a high, angled top-down game camera: strong overhead silhouette, key detail on head, shoulders and back.

A practical human scavenger-soldier in layered survival gear — patched hardshell jacket or coat, harness webbing, kneepads, wrapped or armoured boots. The signature read from above is a large salvage BACKPACK loaded with lashed-on gear, straps and buckles; this is a looter and carry capacity is the fantasy. The accent colour repeats on a shoulder band. Holds a compact projectile firearm. Grounded, weathered stance.

Do not include any glowing or luminous materials, clean military uniforms, capes, or anything that looks made of light.
```

## The Choir · Enemies (built for the slice)

### Chorister

*Choir · Swarm · In slice*

The Choir's smallest unit: a fast, fragile ground swarmer that attacks in melee packs. The most degraded — barely anything human left.

- **Read from above:** Small, low, skittering, hunched — reads weak and numerous.
- **Glow:** Dim, flickering, unstable cold-white (`#cfe8ff`, `#7fa8c9`, `#3a4658`)

**Step 1 · Portrait**

```text
Character portrait for the stylised top-down sci-fi extraction game "Light Raiders". A head-and-shoulders bust portrait of a CHORISTER on a seamless neutral-grey studio background with soft even lighting. Render style: a stylised low-poly 3D game asset in the Synty / KayKit style — chunky simplified geometry, clean flat-shaded colours, minimal texture detail, game-ready look. World rule: the Choir were human once and consumed too much Light — a warm radiant gold-white energy that now burns inside them and glows out through cracks, seams and eyes; every Choir unit is the same species at a different stage of that transformation.

This is the lowest, most burnt-out member of the Choir: a gaunt, ashen head with cracked grey skin and sunken features, and where a face should be only a DIM, FLICKERING, unstable cold-white light leaking weakly through the cracks and eye-sockets — a light almost gone out. Barely-there tatters of a hood or robe at the shoulders. Frail, hollow, pitiful.

Do not make it an insect or bug, a cartoon ghost, cute, or armoured — it is a hollowed-out former person running on its last light.
```

**Step 2 · Body & 3 views**

```text
Full-body character turnaround for the stylised top-down sci-fi extraction game "Light Raiders", based on the established portrait of this CHORISTER — keep the exact same head, cracked skin and dim cold-white glow. Show the full body in three views on one sheet — front, 3/4 side, and back — feet-to-head with nothing cropped, seamless neutral-grey background, soft even lighting. Render style: a stylised low-poly 3D game asset in the Synty / KayKit style — chunky simplified geometry, clean flat-shaded colours, minimal texture detail, game-ready look. The design must read from a high, angled top-down game camera: strong overhead silhouette.

A small, gaunt, hunched humanoid husk, low to the ground and built to scramble like it moves in a swarm — thin, overlong limbs, curled-forward posture, head sensing ahead. Its ashen, cracked body leaks only dim, flickering cold-white light from the cracks. Frail and fast-looking, clearly the weakest thing on the field; only barely-there robe tatters remain.

Do not make it an insect or bug, a cartoon ghost, cute, or heavily armoured — a hollowed-out former person on its last light.
```

### Power

*Choir · Line soldier · In slice*

The Choir's rank-and-file ground soldier: patrols the Surface and casts ranged psychic bolts. The most recognisably human of the combat units — the baseline.

- **Read from above:** Upright humanoid soldier, mid-size; glowing hands telegraph the cast.
- **Glow:** Steady warm gold — the true Light (`#ffd77a`, `#f0a94b`, `#e8ecf4`)

**Step 1 · Portrait**

```text
Character portrait for the stylised top-down sci-fi extraction game "Light Raiders". A head-and-shoulders bust portrait of a POWER on a seamless neutral-grey studio background with soft even lighting. Render style: a stylised low-poly 3D game asset in the Synty / KayKit style — chunky simplified geometry, clean flat-shaded colours, minimal texture detail, game-ready look. World rule: the Choir were human once and consumed too much Light — a warm radiant gold-white energy that now burns inside them and glows out through cracks, seams and eyes; every Choir unit is the same species at a different stage of that transformation.

This is the Choir's standard line soldier and the most recognisably human of them: a helmed head with a glowing WARM-GOLD visor-slit instead of a face, set on smooth pale ceremonial plates that look grown-into rather than worn; the seams at the neck and shoulders glow steady warm gold from within. Poised, disciplined, composed.

Do not make it a literal winged angel with feathered wings or a halo, a mechanical metal robot, or a crystalline alien — a human remade in light.
```

**Step 2 · Body & 3 views**

```text
Full-body character turnaround for the stylised top-down sci-fi extraction game "Light Raiders", based on the established portrait of this POWER — keep the exact same helmed head, pale plates and warm-gold glow. Show the full body in three views on one sheet — front, 3/4 side, and back — feet-to-head with nothing cropped, seamless neutral-grey background, soft even lighting. Render style: a stylised low-poly 3D game asset in the Synty / KayKit style — chunky simplified geometry, clean flat-shaded colours, minimal texture detail, game-ready look. The design must read from a high, angled top-down game camera: strong overhead silhouette, key detail on head and shoulders.

An upright humanoid soldier on a clearly human frame — two arms, two legs — encased in fused pale plates over a body of hardened light, joints and seams glowing warm gold. Its hands are the tell: they radiate brighter than the rest, charged and ready to hurl a bolt of light. Mid-sized, disciplined patrol posture — the baseline enemy the player calibrates against.

Do not make it a literal winged angel with feathered wings or a halo, a mechanical metal robot, or a crystalline alien — a human remade in light.
```

### Herald

*Choir · Support caster · In slice*

A fragile flying unit that avoids combat and sings to summon reinforcements, feeding Escalation. The priority target — must be conspicuous. (Validated first pass — see reference render.)

- **Read from above:** Tall, thin, hovering, brightly lit; cyan throat/chest resonator is the "song" tell.
- **Glow:** Cyan-white resonator + warm-gold body cracks (`#8fe6ff`, `#eafcff`, `#ffd77a`)

**Step 1 · Portrait**

```text
Character portrait for the stylised top-down sci-fi extraction game "Light Raiders". A head-and-shoulders bust portrait of a HERALD on a seamless neutral-grey studio background with soft even lighting. Render style: a stylised low-poly 3D game asset in the Synty / KayKit style — chunky simplified geometry, clean flat-shaded colours, minimal texture detail, game-ready look. World rule: the Choir were human once and consumed too much Light — a warm radiant gold-white energy that now burns inside them and glows out through cracks, seams and eyes; every Choir unit is the same species at a different stage of that transformation.

A slender, elongated, serene head with glowing gold eyes and smooth pale skin threaded with fine warm-gold glowing cracks. At the throat and upper chest sits a bright CYAN-WHITE resonator — an open, luminous cavity or shard of light — that must be the single brightest, most eye-catching thing in the image. Delicate wisps and ribbons of light rise from the shoulders. Otherworldly, mournful, transfigured.

Do not include feathered wings, a literal halo, or a trumpet/horn; keep the face graceful and transfigured, never sickly, emaciated or gory.
```

**Step 2 · Body & 3 views**

```text
Full-body character turnaround for the stylised top-down sci-fi extraction game "Light Raiders", based on the established portrait of this HERALD — keep the exact same face, gold eyes, cyan-white resonator and warm-gold body cracks. Show the full body in three views on one sheet — front, 3/4 side, and back — feet-to-head with nothing cropped, seamless neutral-grey background, soft even lighting. Render style: a stylised low-poly 3D game asset in the Synty / KayKit style — chunky simplified geometry, clean flat-shaded colours, minimal texture detail, game-ready look. The design must read from a high, angled top-down game camera: strong overhead silhouette, and keep the cyan resonator visible from a high angle.

A tall, slender, elongated humanoid HOVERING just off the ground, weightless, trailing long ribbon-like veils of light; delicate and unarmoured, clearly built to flee rather than fight. The cyan-white throat/chest resonator stays the brightest element (the "kill this first" beacon); fine warm-gold cracks thread the body to tie it to the Choir. Elegant, mournful.

Do not make it bulky, armoured or menacing; no feathered wings or literal horn; keep the body transfigured and graceful, never sickly, emaciated or gory. Let the light ribbons trail down and outward rather than framing the head like a halo.
```

### Dominion

*Choir · Armoured heavy · In slice*

A slow, heavily armored ground unit with a devastating beam attack. Its armor plates have weak points — the design must show them.

- **Read from above:** Broadest, heaviest footprint; a few unmistakable white-hot weak spots.
- **Glow:** Deep amber plates, white-hot weak points (`#c9781f`, `#ff9d3c`, `#fff3d0`)

**Step 1 · Portrait**

```text
Character portrait for the stylised top-down sci-fi extraction game "Light Raiders". A head-and-shoulders bust portrait of a DOMINION on a seamless neutral-grey studio background with soft even lighting. Render style: a stylised low-poly 3D game asset in the Synty / KayKit style — chunky simplified geometry, clean flat-shaded colours, minimal texture detail, game-ready look. World rule: the Choir were human once and consumed too much Light — a warm radiant gold-white energy that now burns inside them and glows out through cracks and seams; every Choir unit is the same species at a different stage of that transformation.

A broad, hunched helm-head of dark, hardened-light plates over massive plated shoulders, with a FEW unmistakable white-hot glowing gaps (not an even web of cracks) where a deep-amber molten glow shows through the seams. Crucially this is caged radiant LIGHT trapped inside a cracked shell, not lava or rock. Immense, ponderous, immovable.

Do not make it a volcanic magma or obsidian rock golem; concentrate the glow into a few clear, bright weak points rather than cracks all over.
```

**Step 2 · Body & 3 views**

```text
Full-body character turnaround for the stylised top-down sci-fi extraction game "Light Raiders", based on the established portrait of this DOMINION — keep the exact same plated head, dark hardened-light plates and deep-amber weak-point glow. Show the full body in three views on one sheet — front, 3/4 side, and back — feet-to-head with nothing cropped, seamless neutral-grey background, soft even lighting. Render style: a stylised low-poly 3D game asset in the Synty / KayKit style — chunky simplified geometry, clean flat-shaded colours, minimal texture detail, game-ready look. The design must read from a high, angled top-down game camera: the largest, broadest overhead silhouette, with weak points visible from above.

A broad, towering, heavily built humanoid hunched under thick layered plates of hardened light. Concentrate the deep-amber glow into a FEW white-hot WEAK POINTS — chest core, shoulder seams, and a spine of light down the back — far brighter than the dull plated shell. Its chest or one arm houses a heavy beam-emitter that charges with building light. Ponderous, immovable — the tank of the roster.

Do not make it look agile, and do not make it a volcanic magma or obsidian rock golem — the glow is trapped warm gold-amber light bleeding from a cracked shell, with unmistakable weak points.
```

### Throne

*Choir · Elite · In slice*

A rare floating elite that manifests only at high Escalation and actively hunts players. The reason raids end — it should inspire awe and dread.

- **Read from above:** The single brightest, largest, most elevated silhouette; reads as "run".
- **Glow:** Blinding white-gold, prismatic edges — the purest Light (`#ffffff`, `#ffe9a8`, `#ffd77a`)

**Step 1 · Portrait**

```text
Character portrait for the stylised top-down sci-fi extraction game "Light Raiders". A head-and-shoulders bust portrait of a THRONE on a seamless neutral-grey studio background with soft even lighting. Render style: a stylised low-poly 3D game asset in the Synty / KayKit style — chunky simplified geometry, clean flat-shaded colours, minimal texture detail, game-ready look. World rule: the Choir were human once and consumed too much Light — the greater the rank, the less human and more pure-light it becomes.

At the centre, only the ghost of a serene human face remains, wreathed in blinding white-gold radiance with faint prismatic edges — the purest, most overwhelming Light in the world. Concentric rings and drifting shards of light frame the head like architecture rather than a halo. Regal, serene, merciless.

Do not make it a cluttered mechanical mech; avoid literal halos, feathered wings, or crosses.
```

**Step 2 · Body & 3 views**

```text
Full-body character turnaround for the stylised top-down sci-fi extraction game "Light Raiders", based on the established portrait of this THRONE — keep the exact same ghost-of-a-face core and blinding white-gold radiance. Show the full body in three views on one sheet — front, 3/4 side, and back — feet-to-head with nothing cropped, seamless neutral-grey background, soft even lighting. Render style: a stylised low-poly 3D game asset in the Synty / KayKit style — chunky simplified geometry, clean flat-shaded colours, minimal texture detail, game-ready look. The design must read from a high, angled top-down game camera as the single brightest, largest, most elevated silhouette on the map.

A large, awe-inspiring figure HOVERING well off the ground, wreathed in blinding white-gold radiance; only the ghost of a human form at its centre, the rest become architecture of light — concentric rings, drifting plated fragments and shards orbiting a luminous core, robes of pure glow. When it appears the player should immediately read "run". Favour restraint and grandeur over busy detail.

Do not make it a cluttered mechanical mech, ground-bound, small or humble; avoid literal halos, feathered wings, or crosses.
```

## Support · Non-combatant

### The Trader

*NPC · The Fold · Menu-only in slice*

The sell-only vendor back at the Fold: buys Valuables for Lumens, sells a small floor of basics. In the slice the Fold is menu screens only — the portrait may be all you need, but Step 2 future-proofs a walkable Fold.

- **Read as:** A fellow shadow-dweller, not Choir. Warm, characterful, appraising.
- **Palette:** Muted shadow tones, one warm lamp-lit accent (`#2b3038`, `#7a6a4f`, `#c98a3c`)

**Step 1 · Portrait**

```text
Character portrait for the stylised top-down sci-fi extraction game "Light Raiders". A head-and-shoulders bust portrait of THE TRADER on a seamless neutral-grey studio background with soft even lighting. Render style: a stylised low-poly 3D game asset in the Synty / KayKit style — chunky simplified geometry, clean flat-shaded colours, minimal texture detail, game-ready look. World rule: the people of the Fold live in shadow and are fully human and un-glowing — matte, muted, salvaged clothing, never emitting light.

A shrewd, weathered, characterful human face — settled rather than field-worn — with an appraiser's loupe over one eye or pushed up on the brow; layered practical clothing and a vest or apron collar full of pockets at the shoulders; muted matte fabric lit by a single warm lamp-glow accent, as if standing in a dim shop. Faintly greedy but not villainous.

Do not make them glow or appear made of light, do not armour them, and avoid generic fantasy-shopkeeper clichés.
```

**Step 2 · Body & 3 views**

```text
Full-body character turnaround for the stylised top-down sci-fi extraction game "Light Raiders", based on the established portrait of THE TRADER — keep the exact same face, clothing, loupe and colours. Show the full body in three views on one sheet — front, 3/4 side, and back — feet-to-head with nothing cropped, seamless neutral-grey background, soft even lighting. Render style: a stylised low-poly 3D game asset in the Synty / KayKit style — chunky simplified geometry, clean flat-shaded colours, minimal texture detail, game-ready look.

A grounded human merchant in layered practical clothes, an apron or vest full of pockets and pouches, with small tools for weighing and valuing at hand; muted matte palette with one warm lamp-glow accent. Shrewd, settled posture.

Do not make them glow or appear made of light, do not armour them, and avoid generic fantasy-shopkeeper clichés — they belong to the same grounded, human world as the Raiders.
```

## Reserved · post-slice (do not build yet)

### Virtue

*Choir · Support / Healer · Reserved*

Reserved support rank — mends and empowers other Choir.


**Step 1 · Portrait**

```text
Character portrait for the stylised top-down sci-fi extraction game "Light Raiders". A head-and-shoulders bust portrait of a VIRTUE on a seamless neutral-grey studio background with soft even lighting. Render style: a stylised low-poly 3D game asset in the Synty / KayKit style — chunky simplified geometry, clean flat-shaded colours, minimal texture detail, game-ready look. World rule: the Choir were human once and consumed too much Light — a warm radiant gold-white energy glowing out through cracks, seams and eyes; every Choir unit is the same species at a different stage of that transformation.

A serene, unarmoured head and face of light with a soft, warm glow and a gentle, gracious expression; warm-gold glowing cracks thread the skin; faint wisps of light reach outward.

Do not make it armoured, menacing, or a literal winged angel; keep it the same glowing-human species as the rest of the Choir.
```

**Step 2 · Body & 3 views**

```text
Full-body character turnaround for the stylised top-down sci-fi extraction game "Light Raiders", based on the established portrait of this VIRTUE — keep the exact same face, soft warm glow and gold cracks. Show the full body in three views on one sheet — front, 3/4 side, and back — feet-to-head with nothing cropped, seamless neutral-grey background, soft even lighting. Render style: a stylised low-poly 3D game asset in the Synty / KayKit style — chunky simplified geometry, clean flat-shaded colours, minimal texture detail, game-ready look. Keep a strong overhead silhouette for a top-down game camera.

A serene, unarmoured humanoid of light with a softer, warmer glow than a line soldier, sending gentle tendrils or beams of light out toward unseen allies; mid-sized, graceful, calm.

Do not make it armoured, menacing, or a literal winged angel; keep it the same glowing-human species as the rest of the Choir.
```

### Principality

*Choir · Zone commander · Reserved*

Reserved commander rank — coordinates a district's Choir.


**Step 1 · Portrait**

```text
Character portrait for the stylised top-down sci-fi extraction game "Light Raiders". A head-and-shoulders bust portrait of a PRINCIPALITY on a seamless neutral-grey studio background with soft even lighting. Render style: a stylised low-poly 3D game asset in the Synty / KayKit style — chunky simplified geometry, clean flat-shaded colours, minimal texture detail, game-ready look. World rule: the Choir were human once and consumed too much Light — a warm radiant gold-white energy glowing out through cracks, seams and eyes; every Choir unit is the same species at a different stage of that transformation.

A proud, authoritative helmed head, more ornate than a line soldier, richly plated in hardened light with seams glowing steady warm gold from within; a commanding, superior bearing — the figure others form up around.

Do not make it a robot or a literal winged angel; keep the warm-gold caged-light look consistent with the rest of the Choir.
```

**Step 2 · Body & 3 views**

```text
Full-body character turnaround for the stylised top-down sci-fi extraction game "Light Raiders", based on the established portrait of this PRINCIPALITY — keep the exact same ornate head, plating and warm-gold glow. Show the full body in three views on one sheet — front, 3/4 side, and back — feet-to-head with nothing cropped, seamless neutral-grey background, soft even lighting. Render style: a stylised low-poly 3D game asset in the Synty / KayKit style — chunky simplified geometry, clean flat-shaded colours, minimal texture detail, game-ready look. Keep a strong overhead silhouette for a top-down game camera.

A Choir commander — taller, prouder, and more ornately plated in hardened light than a line soldier, glowing warm gold from within; a distinctly larger and more decorated soldier silhouette, still clearly humanoid, with a bearing of authority.

Do not make it a robot or a literal winged angel; keep the warm-gold caged-light look consistent with the rest of the Choir.
```

### Seraph

*Choir · Raid-boss tier · Reserved*

Reserved apex event — an eventual raid-boss-tier manifestation of the Light.


**Step 1 · Portrait**

```text
Character portrait for the stylised top-down sci-fi extraction game "Light Raiders". A head-and-shoulders bust portrait of a SERAPH on a seamless neutral-grey studio background with soft even lighting. Render style: a stylised low-poly 3D game asset in the Synty / KayKit style — chunky simplified geometry, clean flat-shaded colours, minimal texture detail, game-ready look. World rule: the Choir were human once and consumed too much Light — the greater the rank, the less human and more pure-light it becomes; the Seraph is the apex.

Almost no human face survives — only overwhelming architecture of pure, blinding white-gold Light where a head would be; vast, radiant, serene and catastrophic in feeling even in close view.

Do not make it a cluttered mechanical mech; avoid literal halos, feathered wings, or crosses.
```

**Step 2 · Body & 3 views**

```text
Full-body character turnaround for the stylised top-down sci-fi extraction game "Light Raiders", based on the established portrait of this SERAPH — keep the same overwhelming white-gold architecture-of-light look. Show the full body in three views on one sheet — front, 3/4 side, and back — feet-to-head with nothing cropped, seamless neutral-grey background, soft even lighting. Render style: a stylised low-poly 3D game asset in the Synty / KayKit style — chunky simplified geometry, clean flat-shaded colours, minimal texture detail, game-ready look.

The apex of the Choir at raid-boss scale — vast, radiant, world-warping, floating, serene and catastrophic; almost nothing human survives, only overwhelming architecture of pure Light; blinding white-gold at the largest scale in the game. Favour restraint and grandeur over clutter.

Do not make it a cluttered mechanical mech; avoid literal halos, feathered wings, or crosses.
```

## Alternative render styles (reference only)

Swap the `Render style:` sentence in both prompts to explore other looks. Not the chosen direction.

**A · Stylised concept**

```text
Render style: a polished, stylised 3D character concept in clean stylised-realism — semi-real proportions with slightly simplified, crisp surfaces, in the spirit of modern stylised AAA games; not photorealistic and not flat low-poly.
```

**B · Photoreal (richest detail)**

```text
Render style: a photorealistic 3D character / creature-design render with cinematic detail, realistic materials and soft studio lighting.
```

**C · Flat low-poly (chosen, used above)**

```text
Render style: a stylised low-poly 3D game asset in the Synty / KayKit style — chunky simplified geometry, clean flat-shaded colours, minimal texture detail, game-ready look.
```

## Reference renders

Put validated concept renders in `docs/art/characters/` next to this file. A first-pass Herald render was validated earlier in the stylised-realism look but is not in the repo yet; regenerate it in the low-poly style before Phase 6.
