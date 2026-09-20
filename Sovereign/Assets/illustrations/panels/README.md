# 0.14 面板插画与图集

本目录的6张PNG由本项目使用内置`image_gen.imagegen`生成，作为原创界面插画。完整提示词与生成方式保存在[generation-prompts-0.14.json](generation-prompts-0.14.json)。下表尺寸和SHA-256来自实际文件；提示词中的目标分辨率不等同于最终输出尺寸。

| 文件 | 实际尺寸（像素） | SHA-256 |
| --- | --- | --- |
| [politics.png](politics.png) | 2172 × 724 | `f6ae6e44166cf23c871ca269be8196a0bf4eff460b04211cfde66ab56a9b5c96` |
| [population.png](population.png) | 2172 × 724 | `5e6d328a44507e4c634b01baa30833ae47a5604df693b599d28ed19278d84093` |
| [diplomacy.png](diplomacy.png) | 2172 × 724 | `fc2ff10d843d03c52f5914daf50fe676949c5dbd2dfdd2a616d0ddcade26bff7` |
| [building-scenes.png](building-scenes.png) | 1448 × 1086 | `b90b4adcffd4533c407c6b55f590257e1f8a3e6b1aa2ed0264df91a3d794797b` |
| [population-portraits.png](population-portraits.png) | 1774 × 887 | `0fc4153626987d48af91d16d7c5b97c9c4328182dcbc1da8eb389ebc377430b1` |
| [political-groups.png](political-groups.png) | 1774 × 887 | `d9485cc9d8ab5c1609d5d07e2bc2011b21d2c540a4440abee0c9b1edaaaca077` |

## 用途与图集顺序

`politics.png`、`population.png`、`diplomacy.png`分别用于政治、人口和外交面板横幅，主题为朝议、居民生活与外交会谈。

`building-scenes.png`按**2列、4行**排列，从左到右、从上到下读取：

| 行 | 左列 | 右列 |
| --- | --- | --- |
| 1 | `farm`：农场 | `lumber`：伐木场 |
| 2 | `coal_mine`：煤矿 | `iron_mine`：铁矿 |
| 3 | `toolworks`：工具工坊 | `textile`：纺织工坊 |
| 4 | `construction_sector`：营建部门 | `railway`：铁路 |

`population-portraits.png`按**4列、2行**排列，用于七个人口阶层及一个备用学者形象：

| 行 | 第1列 | 第2列 | 第3列 | 第4列 |
| --- | --- | --- | --- | --- |
| 1 | 农民 | 劳工 | 技工 | 店主 |
| 2 | 资本家 | 贵族 | 失业者 | 学者（备用） |

`political-groups.png`按**4列、2行**排列，以物件组合示意八个利益集团：

| 行 | 第1列 | 第2列 | 第3列 | 第4列 |
| --- | --- | --- | --- | --- |
| 1 | 地主 | 工业家 | 乡村民众 | 知识分子 |
| 2 | 宗教势力 | 军队 | 小市民 | 工会 |

## 表现范围

这些图像是清代风格的题材示意，不是历史照片，也不是对真实人物、建筑、服饰或1836年具体场景的精确还原。人口与利益集团图集表现游戏中的类别，不对应某位真实人物；备用学者也不表示新增了第八个人口阶层。

这些二维插画用于面板展示，**没有替代骨骼三维领袖、人物动画或三维城市场景**。目前非大清（非QNG）国家也共用这套示意资产，尚待按国家与文化补充本地化图像；不能将清代风格图像理解为其他国家的历史外观。
