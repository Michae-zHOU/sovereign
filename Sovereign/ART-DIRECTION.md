# Visual production direction

The current build is a playable visual prototype. It establishes real terrain relief, textured physical geography, a navigable 3D world, procedural settlements, moving ships and trains, and a separate city view. These are foundations for the requested production quality; they do not establish visual parity with a finished commercial grand-strategy game.

## Finish one representative region first

Use southern Britain and the English Channel as the first finished region. It combines coastline, agricultural terrain, dense settlement, a major city, industrial buildings, railways and maritime activity. Its neighbouring coast also exposes differences in architecture and political geography. Lock a small set of review cameras: strategic overview, regional view, capital approach and city detail.

Before extending the same treatment across the world, this region must satisfy the acceptance criteria below at all four review distances. A polished screenshot from one fixed camera is insufficient: pan, zoom, change simulation speed, build an industry, switch map modes and reopen a save during review.

| Area | Current foundation | Work required for the representative region | Acceptance criterion |
|---|---|---|---|
| Historical geography | Physical land shapes and capital markers; no historical territorial model in the renderer | Research and author dated country and province boundaries; connect them to simulation ownership; distinguish sovereignty, administration and disputed territory | Borders, labels, selection and the displayed date agree. Boundary uncertainty has a documented editorial decision. Modern borders never silently substitute for historical ones. |
| Terrain and coast | Elevation meshes, physical land-cover imagery and water shaders | Establish a coherent material palette; refine shore masks, beaches, cliffs, rivers, vegetation distribution and mesh transitions | No floating shore fragments, visible tile seams or lighting discontinuities. Relief stays readable without turning every hill into an exaggerated ridge. Water and land meet convincingly through the full zoom range. |
| Regional architecture | Procedural building blocks and pitched roofs | Build a small authored British kit: housing, civic landmarks, warehouses, port structures, factories and rail infrastructure; add an appropriate neighbouring regional kit | Building silhouettes and materials distinguish function and region. Repetition is unobtrusive at normal play distances. Buildings sit on terrain and connect plausibly to roads, quays or rails. |
| Asset efficiency | Generated meshes and batched forest instances | Establish shared texture atlases, material limits, instancing and levels of detail; define collision and shadow rules per asset category | Distant assets simplify without conspicuous popping. Repeated objects share resources. Detail is concentrated where the camera can reveal it. |
| Life and industrial growth | Decorative moving ships and trains; industry changes city size | Author staged construction and expansion; add restrained smoke, port activity and rail movement; tie visible changes to actual simulation state | A player can see what a completed investment changed. Idle, active and expanding districts differ clearly. Motion supports the scene without obscuring selection or implying simulated activity that does not exist. |
| Camera and labels | Perspective navigation, capital focus and billboard labels | Set deliberate strategic, regional and city scales; tune movement, transitions, label hierarchy, occlusion and overlap handling | Selected places remain easy to locate. Labels neither collide nor dominate the landscape. Transitions preserve geographic orientation and all camera controls work consistently. |
| Interface | Playable management panels, map modes and city controls | Establish information hierarchy, spacing and reusable UI components; tune panel density against the visible map; provide clear hover and selected states | Essential state and available actions remain legible at the minimum supported window size. No clipped controls, hidden actions, overlapping labels or ambiguous selected states occur in the review flow. |

## Benchmark and review protocol

Record the exact build, renderer, resolution, graphics settings, hardware and save used for every comparison. Agree on supported hardware and a frame-time target before treating a result as a production pass; do not infer broad performance from one development machine.

Use the same deterministic review sequence for each build:

1. Load the representative-region save and measure startup time, memory and initial frame-time stability.
2. Pan and zoom continuously through the four review distances, including coastlines and the densest settlement.
3. Run the simulation at its highest supported speed while moving the camera and opening management panels.
4. Complete several industry and railway upgrades, inspect the changed districts, then save and reload.
5. Repeat with the most demanding intended combination of visible cities, vegetation, shipping, shadows and interface panels.

Capture frame-time percentiles and visible stalls, GPU and CPU timing where available, memory, draw calls and geometry counts. Review native-resolution screenshots alongside a short motion capture: still images cannot reveal camera discomfort, asset popping, flickering or animation problems. Compare results with the agreed performance target and keep a concise list of visual defects with reproducible camera positions.

## Gate for expanding world content

Expand to another region only when the representative region passes the review flow, its historical boundaries have been checked, its visual kit is reusable, and its performance meets the agreed target. Then select a contrasting region to test the system—for example, coastal Japan—before multiplying settlements and events across the world.

Keep simulation depth and art production connected: every new visual system should either communicate game state, improve geographic understanding or sustain the period setting. Decorative detail earns its cost when it remains coherent, readable and performant during ordinary play.
