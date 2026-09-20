# 界面资源（0.9）

0.9 按实屏参考重绘面板皮肤，新增 `hud-top.svg`、`nav-rail.svg`、`outliner-heading.svg`、`clock-face.svg` 和 `lens-tray.svg`。青灰布纹、古铜细线、卷轴标题与圆形三态均为原创 SVG；普通面板以九宫格保护边角，布纹区域平铺，圆形控件和表盘使用零切片边距。`region-vignette.svg` 为保留的早期概览示意，当前省份页使用 `../illustrations/region-landscape.png`。具体布局依据见项目根目录 `INTERFACE-REFERENCE-0.9.md`。下述导航 PNG 图集与契约沿用 0.8。

本目录同时包含原创 SVG 界面装饰和通过内置 `image_gen` 工具生成的 `icon-atlas.png`。0.8 图标采用简化的单一主体与清楚轮廓，减少细碎背景和装饰，保证小尺寸辨识度；文字、选中态、按钮背景由 Godot 控件绘制。生成记录见 `../illustrations/generation-prompts-minimal-0.8.json`，没有提取商业游戏的界面图片。

## 0.8 导航图集契约

`icon-atlas.png` 按 **4 列 × 3 行**等分，行内从左向右读取。`Presentation/InterfaceArt.cs` 中的 `UiIcon` 依据图片实际尺寸切分 `AtlasTexture` 并缓存，启用 `FilterClip`。更换分辨率时保留格数、顺序和透明空间，主体不可跨格，不能加入格间边框或图片文字。

| 行 | 第 1 格 | 第 2 格 | 第 3 格 | 第 4 格 |
| --- | --- | --- | --- | --- |
| 1 | 朝政 `overview` | 政令 `politics` | 建设产业 `industry` | 市场 `markets` |
| 2 | 人口 `population` | 科技 `research` | 外交 `diplomacy` | 史事 `chronicle` |
| 3 | 地区地图 `atlas` | 领袖 `leadership` | 营建 `construction` | 地形 `terrain` |

图集含 12 项图标；主要管理导航仍为前十项，不表示新增了十二个管理页面。建筑和商品图集见 `../illustrations/README.md`；国家识别旗与它们的独立来源、年代和许可见 `../flags/README.md`。在实际控件尺寸检查每格内容与边缘，不能只检查放大的整张图集。

## 保留的原创 SVG

These SVG files were drawn from simple vector primitives specifically for the Sovereign project. They contain no extracted game artwork, third-party icon set, embedded font, or raster image. They may be used and modified with the project source.

## Legacy SVG navigation icons

`overview.svg`, `politics.svg`, `industry.svg`, `markets.svg`, `population.svg`, `research.svg`, `diplomacy.svg`, `chronicle.svg`, `atlas.svg`, and `leadership.svg` share a 64 × 64 transparent canvas, a muted brass stroke (`#d0b47b`), 2.5-unit line width, and rounded joins. Display at 28–34 pixels for a compact navigation rail, or at 40–48 pixels for a larger control. Give selected and hovered states a separate background in the UI so the artwork remains readable.

## Qing presentation ornaments

`qing-seal.svg` is a 96 × 96 circular brass ornament containing an original abstract coiled dragon motif. `qing-banner.svg` is a 128 × 80 decorative gold brocade panel with an original related motif. These are fictional interface ornaments inspired by period materials; neither is a reproduction of an official historical flag, imperial seal, or a named ruler's personal emblem. They should not be used as evidence of historical flag design.

SVGs use basic paths, circles, and ellipses for compatibility with Godot's SVG importer. No external resource is needed.

## Cabinet chrome (0.5)

`cabinet-body.svg`, `cabinet-header.svg`, and `cabinet-top.svg` are original nine-patch skins with muted blue-green metal, burgundy panel headings, fine grain, and narrow brass bevels. `medallion.svg`, `medallion-hover.svg`, and `medallion-active.svg` provide the circular navigation and map-lens controls. These assets use only project-authored SVG geometry and gradients.

The layout was studied from Paradox's public Victoria 3 [Graphic Overview](https://www.paradoxinteractive.com/games/victoria-3/news/dev-diary-49-graphic-overview) and [UX Improvements](https://www.paradoxinteractive.com/games/victoria-3/news/dev-diary-74-ux-improvements) screenshots: dense two-row instruments, an independent date/speed bar, compact circular navigation, burgundy drawer headings, and circular map lenses. The game ships no image pixels or extracted assets from those references.

0.6: original SVG skins recolored to brown top instruments and slate-violet panels, with embossed wood action rows and an original architectural header illustration. Based on observation of live UI structure; no original game artwork was extracted. See `INTERFACE-REFERENCE-0.6.md`.
