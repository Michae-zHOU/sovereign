# Sovereign 0.3 — Countries, cities and leadership

Release 0.3 expands the 1836–1846 prototype from three countries to **12 playable countries, 36 scenario regions and 96 selected cities**. The release directory is `Sovereign-Windows-0.3`; the previous 0.2 package is retained separately. See the release test report for the final verified revision, results and limitations.

## Scenario coverage

The playable roster is the United Kingdom, Prussia, Japan, France, the Austrian Empire, the Russian Empire, the United States, the Qing Empire, the Ottoman Empire, Spain, Portugal and Belgium. Its regions group the selected cities for inspection and population accounting. These counts describe the included content, not complete coverage of every country or city in the world.

The coverage target is every historical settlement, without an arbitrary population cutoff. The planned world catalog uses stable IDs, dated records and source provenance, with detailed city scenes generated or loaded only when opened. This release still includes 96 cities; it does not add or imply an exhaustive world settlement catalog.

The campaign still begins on 1 January 1836 and ends on 1 January 1846. Physical geography uses the existing atlas sources; region groupings, country markers and city assignments do not constitute an authoritative historical border atlas.

## Exploration and local economies

Open **Atlas**, choose a country from the dropdown, select **Explore region**, then inspect a city's economy or enter it in 3D. **Housing**, **Industry**, **Civic** and **Waterfront** move the camera within the generated city scene; **City** restores its broad view. Right-drag orbits, the mouse wheel zooms, and the return control goes back to the atlas.

All 96 included cities have population, workforce, employment, productive industries and output. City production feeds shared national goods inventories and contributes to national output. Construction can be targeted to player-owned cities; each city runs its own construction line using national funds and materials. Foreign cities are available for inspection while their governments direct development.

The new **Population** panel shows urban and rural residents, employment and region/city totals. Monthly internal migration transfers people from a region's countryside into its cities; population growth and public health affect totals. Jobs and productive capacity respond to completed local industries. National markets, public finance and aggregate household demand remain simplified shared systems.

City scenes are regional composites with procedural architecture and details. Their layouts and landmarks are not surveyed historical reconstructions. Camera districts have no separate budgets, individual building placement or resident-level pathfinding. Decorative transport is not a visualization of individual trade transactions.

## Dated historical leadership

The **Leadership** panel and Atlas leadership button show the relevant officeholder on the current campaign date, with institutional source links and a 3D figure. The register covers **20 distinct historical identities in 24 dated office-context intervals**. Multiple records can describe one identity when governing context changes, as with Isabella II's regencies.

`Simulation/HistoricalLeaders.cs` supplies the executable date register; `HISTORY-SOURCES.md` records sources and editorial decisions. Succession follows the historical calendar within the supported decade. Japan's governing shogun and imperial sovereign are distinguished; regency and ministerial context are identified where relevant.

The figures are provisional procedural interpretations. Historical identity and office records do not establish a verified facial likeness, wardrobe or portrait reconstruction. Alternate elections, dynasties, rulers and death dates are not simulated.

## Save compatibility

This release writes **version 2 saves** containing regions, cities and local economy records. Valid version 1 saves from the original three-country scenario migrate automatically on load.

- Existing Great Britain, Prussia and Japan campaign state is carried forward, including stock, treasury, policies, research, choices and existing diplomacy.
- Existing national population and industrial capacity are allocated into the new local geography; earlier construction receives a city location.
- Nine additional countries enter at scenario baseline on the saved date. The migration does not simulate their intervening history.
- Saving uses the existing manual slot and preserves its previous file as `campaign.json.bak`. The older 0.2 executable cannot load a version 2 save.

## Rendering, build and remaining scope

Godot 4.5.1 .NET and C# remain the implementation stack. Forward+ with 4× MSAA is the default rendering path; the optional Compatibility launcher uses the same simulation with different graphics capabilities. The illustrated menu background remains separate from the interactive atlas, cities and leader scenes.

The source package excludes generated `.godot`, `bin` and `obj` caches. `BUILD.md` explains restore, build, import, tests and the versioned Windows export destination. Test counts and release pass/fail results belong to the final test report.

Worldwide country and city coverage, full historical boundaries, detailed population cultures/religions/wealth, ownership, alternate political succession, strategic warfare, conquest, multiplayer and the full 1836–1936 century remain unfinished. Version 0.3 expands the working foundation within that larger scope.
