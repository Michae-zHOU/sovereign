# World geography and original 3D presentation

The world terrain combines public geographic datasets with original Godot geometry and shaders. No proprietary Victoria assets, textures, or code are used.

## Natural Earth

- `ne_50m_land.geojson`: 1:50 million physical land polygons. Source: https://github.com/nvkelso/natural-earth-vector/blob/master/geojson/ne_50m_land.geojson
- `ne_110m_land.geojson`: original prototype's coarse physical coastline, retained as a reference dataset. Source: https://github.com/nvkelso/natural-earth-vector/blob/master/geojson/ne_110m_land.geojson
- `earth_landcover.jpg`: derived from Natural Earth II, 1:50 million land cover with shaded relief, version 3.2.0 (`NE2_50M_SR.tif`, 10800 × 5400). Resampled to 8192 × 4096 for the game.
  - Dataset page: https://www.naturalearthdata.com/downloads/50m-raster-data/50m-natural-earth-2/
  - Source archive: https://naturalearth.s3.amazonaws.com/50m_raster/NE2_50M_SR.zip

All Natural Earth raster and vector datasets are public domain. Primary authors: Tom Patterson, Nathaniel Vaughn Kelso, and Natural Earth contributors. Terms: https://www.naturalearthdata.com/about/terms-of-use/

## NASA / GEBCO elevation

`earth_height.bin` and the elevation channel of `earth_physical.png` derive from the NASA Earth Observatory topography image, using GEBCO data produced by the British Oceanographic Data Centre. Credit: imagery by Jesse Allen, NASA's Earth Observatory; underlying General Bathymetric Chart of the Oceans (GEBCO).

- Original catalog page: https://visibleearth.nasa.gov/images/73934/topography
- Direct source: https://eoimages.gsfc.nasa.gov/images/imagerecords/73000/73934/gebco_08_rev_elev_21600x10800.png
- Original dimensions: 21600 × 10800; source elevations scaled across 0–6400 metres.
- NASA media usage guidelines: https://www.nasa.gov/nasa-brand-center/images-and-media/
- This artwork does not imply NASA or GEBCO endorsement.

The elevation source was resampled to 8192 × 4096 for GPU shading and 4096 × 2048 for CPU geometry, and masked to Natural Earth land polygons. Elevation is vertically exaggerated in the 3D game for legibility; the renderer is not a surveying or elevation-analysis tool.

## Derived asset channels

`earth_physical.png`, 8192 × 4096 RGBA:

- R: resampled GEBCO land elevation.
- G: Natural Earth 1:50 million land coverage.
- B: distance-based near-coast water tint, derived from land coverage.
- A: constant opaque coverage. The alpha channel is not used as geographic data.

`earth_height.bin`: 4096 × 2048 pixels, interleaved unsigned 8-bit elevation and land coverage. Row zero is 90° N; column zero is 180° W. Used to build actual 3D tile meshes and position vegetation/cities without GPU readback. Include `*.bin` in exports.

`terrain.gdshader` and `ocean.gdshader` are original project shaders. City buildings, pitched roofs, railway models, tree clusters, ships, and their animation are original procedural geometry.

All geographic sources were retrieved September 19, 2026.

## Historical and simulation scope

The map has an equirectangular geographic layout, physical land-cover imagery, elevated terrain and enlarged cities for legibility. It deliberately does not present modern political borders as historical 1836 borders. Country markers represent the twelve simulated playable states. The separate 1815 reference atlas supplies geographic territory selection; see `historical/README.md`. Qing province boundaries remain schematic; see `../provinces/README.md`. Physical terrain datasets describe modern surveyed geography and are not a reconstruction of the exact 1836 coastline or land use.

Maritime routes are decorative, period-plausible corridors avoiding modern canal shortcuts. Ships do not encode trade quantities. Trains appear when a country's railway level is positive. City size reflects industrial development, capped for performance.

Controls: W/A/S/D or arrow keys to pan; right-drag to pan; mouse wheel to zoom; click a country marker or use the country selector to focus a playable country.

## 0.8 cartographic presentation

`ne_50m_rivers_lake_centerlines.geojson` adds Natural Earth public-domain river and lake centerlines. Source: <https://github.com/nvkelso/natural-earth-vector/blob/master/geojson/ne_50m_rivers_lake_centerlines.geojson>; retrieved September 19, 2026. SHA-256: `f286e0ce978fde999ca2d7a78c764be08542e19b63cded52b05c12d5173ccc51`. The source has the same public-domain terms cited above. River ribbons are batched into one mesh and follow the existing elevation surface. Width is exaggerated for screen legibility. Modern river centerlines, including the Yellow River, **are not an 1836 river-course reconstruction**.

East Asia and Europe use 192 subdivisions per 20-degree terrain tile. This exposes more of the existing GEBCO elevation samples; it does not add surveyed data or improve the historical accuracy of borders. Near views reveal stronger source-derived hill shading, existing forest clusters and 3D capital buildings. Distant views hide those individual buildings and trees. Political and province views retain readable color fields while revealing terrain shading when zoomed in.

Following the first native 0.8 interface capture, political/province source-relief contrast, dynamic-light weight and normal-map strength were each scaled to 60% of the initial 0.8 shader values so small ridges do not compete with province labels. Terrain mode and geographic height/picking coordinates remain unchanged.

The ocean shader draws paper grain, a faint geographic graticule and engraved coast bands using the existing Natural Earth land-distance channel. These bands are decorative shore engraving, not depth contours. Sea labels are Chinese. Country and province labels use a separate MSDF instance of Source Han Serif; settlement labels use Source Han Sans. Labels maintain deliberate screen sizes and reserve measured text rectangles in country/province/settlement order, including long population or output labels. City points are concentric disks rather than cuboids. All existing political/province/terrain/population/economy modes and geographic pick coordinates are preserved.
