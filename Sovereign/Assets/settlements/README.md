# Settlement scenes

This folder is the first content segment of a larger settlement pipeline: **128 curated cities in 12 polities, including 40 Qing settlements**, with one small descriptor per city. It is not a complete list of historical settlements. Population and economic values elsewhere in the scenario remain authored estimates, not census records. The 32-city Qing expansion is documented in `../../QING-SETTLEMENT-SOURCES.md`; `IsPort` includes domestic river/coastal waterfronts and does not imply a treaty port or foreign-trade entitlement in 1836.

`index.json` contains lightweight identity, geography and coverage metadata. The per-city JSON files contain scene instructions. `SettlementScenes.Load(cityId)` opens only the requested descriptor through Godot's `res://` filesystem; it does not read the index, enumerate other descriptors, load meshes, instantiate scenes or retain a global scene cache. The calling city view owns scene creation and release.

The supported descriptor interval begins on 1 January 1836 and ends just before 2 January 1846, including the campaign's terminal day of 1 January 1846. Dates are game-calendar dates without a time zone. Names and coordinates are curated, provisional historical annotations. Coordinates are rounded location points from the scenario catalog. [Natural Earth populated places](https://www.naturalearthdata.com/downloads/10m-cultural-vectors/10m-populated-places/) provides present-day geographic reference; it is neither an 1836 survey nor a historical census. Natural Earth data is public domain.

The included cities use deterministic procedural composites. **No descriptor currently supplies an authored reconstruction of the historical city's streets or buildings.** Regional architecture profiles are shared styling instructions, not evidence of a faithful local reconstruction. Damascus is grouped under Ottoman nominal sovereignty; Egyptian administration during 1832–1840 is not simulated.

Each descriptor has schema version 1 and required fields:

- `Id`, `Name`, `CountryId`, `RegionId`, `Latitude`, `Longitude`: identity and location matching the simulation catalog.
- `ArchitectureProfile`, `IsPort`: scene parameters matching the current scenario.
- `Seed`: a persisted, nonnegative 32-bit seed. Included seeds use UTF-8 FNV-1a, offset 2166136261 and multiplier 16777619 with unsigned 32-bit wrapping, then mask with 0x7fffffff. They do not depend on process hash randomization or system language.
- `ValidFrom`, `ValidToExclusive`: the covered game-calendar interval.
- `AuthoredScenePath`: an empty string for procedural scenes, or an existing `res://Assets/settlements/scenes/` resource ending in `.glb`, `.gltf`, `.tscn` or `.scn`.

The loader rejects unknown settlement IDs, identity mismatches, invalid coordinates, unsupported versions, incomplete fields, unknown fields, malformed JSON, invalid coverage and unsafe or missing authored-scene paths. Descriptor size is limited to 64 KiB. Optional scene-resource existence checks do not instantiate the resource.

`CityView` loads the descriptor only when its selected city changes. A nonempty `AuthoredScenePath` is instantiated as a `PackedScene` with a `Node3D` root; it replaces the procedural district for that city. Authored districts use meters, Y-up, ground near Y=0, and should fit around X=-22 to 22 and Z=-16 to 12. Current inspection-camera targets are housing (-10, 0.8, -5), industry (8, 0.8, -5), civic (-0.3, 1.7, -6.2), waterfront (4, 0.2, 5), and whole city (0, 0, -2). Changing cities releases the old content. No authored district is bundled yet.

Adding future geographic content requires authoritative historical sources, stable settlement IDs, dated names and polity membership, and an index coverage statement. The current loader deliberately accepts only entries in the current simulation catalog. A global gazetteer, streamed simulation data and complete historical settlement coverage remain future work; this folder establishes the per-settlement asset boundary for them.
