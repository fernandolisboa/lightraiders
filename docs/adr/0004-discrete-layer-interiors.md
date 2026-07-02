# Interiors are discrete Layers, not seamless spaces

Top-down games normally make interiors seamless: roofs fade, and a line-of-sight system governs what you can see through windows and doorways. Light Raiders instead makes every interior a discrete Layer — a separate space connected to the rest of the map only by its door(s), with no sight or fire across the boundary. We chose this because it is the game's original design identity (outdoors = open warzone, indoors = close-quarters knife-edge), it is cheaper to build (no line-of-sight/occlusion engine), and Layer boundaries give multiplayer a natural interest-management seam.

The panic-room exploit is closed by rule, not tech: players can always follow players through doors; only AI respects the small-interior restriction (AI enters Large interiors only).

## Considered Options

- **Seamless + roof cutaway with LOS** (ZERO Sievert model) — richer mid-fight transitions (window shots, doorway peeks). Rejected: needs a vision system, and makes the AI-can't-enter-small-buildings rule feel arbitrary.
- **Hybrid** (small buildings discrete, landmark structures seamless) — best variety, but two systems to build and maintain. Rejected for now; landmark seamless set-pieces can be revisited post-slice as an additive change.
