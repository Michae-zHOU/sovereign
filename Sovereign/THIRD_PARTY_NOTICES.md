# Third-party notices

## Historical reference atlas (added in 0.4)

The 1815 territorial reference dataset is from André Ourednik and the historical-basemaps contributors: [historical-basemaps](https://github.com/aourednik/historical-basemaps), commit `da7a4b735ecef70aebdc9c73e409d8a2500d50f3`. Original GeoJSON and derived atlas data are supplied under GNU GPL v3. The complete license, raw data, conversion script, derived data, provenance and scope notes are included in `Assets/map/historical/`. This is an 1815 reference layer, not a verified 1836 political reconstruction. The reference data license is distinct from the engine, fonts, and original interface artwork.

## Godot Engine 4.5.1

This game uses Godot Engine, copyright Godot Engine contributors, Juan Linietsky and Ariel Manzur, under the MIT license. The engine also incorporates third-party components. The complete license and component notices are supplied as `GODOT-LICENSE.txt` and `GODOT-COPYRIGHT.txt` in the Windows distribution.

Sources: [Godot license](https://github.com/godotengine/godot/blob/4.5.1-stable/LICENSE.txt), [Godot component notices](https://github.com/godotengine/godot/blob/4.5.1-stable/COPYRIGHT.txt).

## .NET runtime

The self-contained Windows build includes Microsoft .NET runtime 8.0.31. Its license and notices are supplied as `DOTNET-LICENSE.txt` and `DOTNET-THIRD-PARTY-NOTICES.txt` in the Windows distribution. The runtime version is recorded in the exported `Sovereign.deps.json`.

Sources: [runtime license](https://github.com/dotnet/runtime/blob/v8.0.31/LICENSE.TXT), [runtime notices](https://github.com/dotnet/runtime/blob/v8.0.31/THIRD-PARTY-NOTICES.TXT).

## Geographic data

Made with Natural Earth. Geographic coastline, physical-color and elevation data are used to generate the physical atlas. They do not establish historical political boundaries. Dataset sources, processing details and acknowledgments are recorded in `Assets/map/README.md` in the source project and `MAP-DATA-NOTICES.md` in the Windows distribution.

## Bundled fonts

Source Sans 3 and Source Serif 4 are bundled under the SIL Open Font License, version 1.1. Complete copyright statements and license terms accompany the source fonts as `Assets/fonts/OFL-SourceSans3.txt` and `Assets/fonts/OFL-SourceSerif4.txt`. Copies are also included with the Windows distribution under those same filenames at its root.

- Source Sans 3: Copyright 2010–2020 Adobe, with Reserved Font Name “Source.”
- Source Serif 4: Copyright 2014–2023 Adobe, with Reserved Font Name “Source.”

Upstream projects: [Source Sans](https://github.com/adobe-fonts/source-sans), [Source Serif](https://github.com/adobe-fonts/source-serif).

The packaged font files are not modified by this project. Their upstream license texts are included unchanged.

### Simplified Chinese interface fonts

Source Han Sans SC Regular (思源黑体) and Source Han Serif SC Regular (思源宋体) are bundled as the official static OpenType/CFF fonts from Adobe. The SC configurations use Simplified Chinese regional glyph forms and retain the full Pan-CJK character repertoire. These are not variable fonts and have not been subsetted or otherwise modified by this project.

- `Assets/fonts/SourceHanSansSC-Regular.otf`: copyright 2014–2025 Adobe, with Reserved Font Name “Source”; SIL Open Font License 1.1. The complete, unchanged upstream license is `Assets/fonts/OFL-SourceHanSans.txt`.
- `Assets/fonts/SourceHanSerifSC-Regular.otf`: copyright 2017–2022 Adobe, with Reserved Font Name “Source”; SIL Open Font License 1.1. The complete, unchanged upstream license is `Assets/fonts/OFL-SourceHanSerif.txt`.

Official upstream binaries: [Source Han Sans SC Regular](https://github.com/adobe-fonts/source-han-sans/blob/release/OTF/SimplifiedChinese/SourceHanSansSC-Regular.otf), [Source Han Serif SC Regular](https://github.com/adobe-fonts/source-han-serif/blob/release/OTF/SimplifiedChinese/SourceHanSerifSC-Regular.otf). Official license sources: [Source Han Sans license](https://github.com/adobe-fonts/source-han-sans/blob/release/LICENSE.txt), [Source Han Serif license](https://github.com/adobe-fonts/source-han-serif/blob/release/LICENSE.txt).

The Windows distribution also includes both Source Han license files at its root. Retain these notices and license files when distributing the Chinese interface build.

## Original implementation and illustrative art

Interface, procedural models and game implementation are original to this project. The industrial-era campaign menu image, `Assets/art/industrial-era.png`, was generated for this project using the built-in image generation tool. No existing game's artwork or reference image was supplied. It illustrates an invented setting and is not a screenshot of the simulation or a historically exact reconstruction. The generation prompt and provenance are recorded in `Assets/art/README.md`.

## License copies

Godot and .NET license copies are also retained in the source project's `Licenses` directory. They were retrieved from the versioned official upstream sources linked above. Do not remove the runtime, engine, font, or geographic notices when redistributing the exported game.

## MakeHuman CC0-derived leader meshes (0.6)

The five new anatomical character meshes derive from MakeHuman CC0 base, morphs, skin, eyes and hair assets, combined with original period-inspired clothing and animation. Details, asset URLs, attribution and scope are in `Assets/leaders/README.zh-CN.md`, `LICENSE-MAKEHUMAN-CC0.txt` and `model-manifest.json`. Original MakeHuman source materials and rebuildable Blender sources are included only in the source distribution under `Assets/leaders/source/`; `.gdignore` excludes these authoring files from runtime imports. No Paradox game model or texture is included.

Qing province polygons are original schematic geometry. Historical CHGIS geometry is not redistributed. See `QING-PROVINCE-SOURCES.md`.
