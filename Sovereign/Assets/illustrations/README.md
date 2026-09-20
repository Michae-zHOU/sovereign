# 建筑与商品图集（0.8）

本目录为建设、城市和市场界面的原创位图素材，由内置 `image_gen` 工具生成。风格以单一建筑或商品主体、清楚轮廓、简化背景为主，优先保证约 25—112 像素下的辨识度；不在图片中写文字或绘制按钮。界面边框、文字和交互由代码负责。生成记录见 `generation-prompts-minimal-0.8.json`。

## 图集契约

所有图集按从左到右、从上到下读取。素材可以更新分辨率，但格数、顺序及各格占画布的等分比例不能变化；主体不可跨格，格间不要添加额外留白或边框。运行时由 `Presentation/ConstructionVisuals.cs` 按实际图片尺寸切分并缓存 `AtlasTexture`，使用 `FilterClip` 防止相邻格采样串色。

| 文件 | 切分 | 格序与模拟 ID |
| --- | --- | --- |
| `buildings.png` | 2 列 × 3 行 | 农场 `farm`、伐木场 `lumber`、煤矿 `coal_mine`、铁矿 `iron_mine`、工具工坊 `toolworks`、纺织厂 `textile` |
| `goods.png` | 3 列 × 2 行 | 谷物 `grain`、木材 `timber`、煤炭 `coal`、铁 `iron`、工具 `tools`、衣物 `clothes` |
| `construction.png` | 单张，不切分 | 营建部门与省份建设预览 |

使用方式为等比居中，商品与图标应保留独立主体周围的透明空间。建筑缩略图只表达建筑类型；不是某座真实城市或特定历史建筑的精确复原，也不是运行时三维场景。

这些插图没有使用从商业游戏提取的图片。旗帜另由 `Assets/flags/` 的来源和许可说明管理；导航图集契约见 `Assets/interface/README.md`。更换素材后需重新导入并在实际小尺寸界面中查看，不能仅以整张大图判断效果。

0.9 新增 `region-landscape.png`：内置 image_gen 生成的原创江南通用风景横幅，原始 PNG 原样保留，尺寸为 1816 × 866；仅用于省份概览氛围示意，不是江苏或具体城镇的历史地貌复原，提示词和校验值见 `generation-prompt-region-0.9.json`，其余 0.8 图集保持不变。
