# 可动三维领袖 · 0.13

本版包含 **20 个历史人物身份、21 个全身骨骼 GLB**，用于 12 国、1836—1846 年的现有战役。模型具有人体网格、皮肤材质、服装、骨骼与面部形变，可以环绕观察；界面内的小肖像也由三维场景渲染。道光旧二维概念肖像是独立的可选插画，不是这些模型的替代网格。

**0.13仅继续修改道光，其余19个身份的20个模型沿用0.11。** 本轮替换中年皮肤底色、减轻须根黑斑，重做弯曲并离开脸部表面的须髭，调整帽带、帽穗、领口和披肩织物。正式道光GLB与blend已重建并导入，Windows运行版已导出，三项exe烟雾和五张完整界面截图检查已完成；进度见[PROGRESS.md](../../PROGRESS.md)。

0.12的清瘦颊部、颧骨、眼周、较薄嘴唇、法线连接修复及独立眼骨继续保留。袖口、手部和靴子沿用0.12服饰基础，0.13没有再次重做这些部位。共用肖像灯光也继承0.12，本轮没有新增全领袖灯光或动作改造。

人物是依据历史资料制作的**暂定美术重建**，不是本人扫描，也不代表已经通过历史肖像相似度验收。共用 CC0 人体拓扑后再调整头部参数、年龄、毛发和服装，不能等同于逐人手工雕刻或高精度数字人。

## 覆盖与年龄

| 国家 | 历史人物身份 |
| --- | --- |
| 大清 | 道光帝 |
| 英国 | 威廉四世、维多利亚 |
| 普鲁士 | 腓特烈·威廉三世、腓特烈·威廉四世 |
| 日本 | 德川家齐、德川家庆 |
| 法国 | 路易-菲利普一世 |
| 奥地利 | 费迪南一世 |
| 俄罗斯 | 尼古拉一世 |
| 美国 | 安德鲁·杰克逊、马丁·范布伦、威廉·亨利·哈里森、约翰·泰勒、詹姆斯·诺克斯·波尔克 |
| 奥斯曼 | 马哈茂德二世、阿卜杜勒-迈吉德一世 |
| 西班牙 | 伊莎贝拉二世 |
| 葡萄牙 | 玛丽亚二世 |
| 比利时 | 利奥波德一世 |

伊莎贝拉使用 `isabella_ii.glb`（5 岁造型）与 `isabella_ii_adolescent.glb`（13 岁造型）。游戏年龄达到 **11 岁**时选择后者，所以 20 个身份对应 21 个模型。切换阈值是当前美术阶段划分，不是“11 岁已经长成 13 岁”的历史断言；没有逐年衰老或连续变形。她的多段摄政记录仍共用同一身份。

任期与身份来自 [HistoricalLeaders.cs](../../Simulation/HistoricalLeaders.cs)。[历史造型依据与制作清单](source/HISTORICAL-ART-DIRECTION-0.11.md) 列出各人物馆藏参考、证据缺口与后续验收目标；其中要求的更多年龄阶段和精细雕刻不应被理解为本版全部完成。[道光0.12造型说明](source/DAOGUANG-ART-DIRECTION-0.12.md) 区分馆藏与文献支持的清瘦方向、艺术参数及尚不能证实的53岁精确容貌。

## 骨骼、动画与查看方式

共用基础骨架包含 **9 个骨骼**：`Root`、`Pelvis`、`Chest`、`Neck`、`Head`、`LeftUpperArm`、`LeftForearm`、`RightUpperArm`、`RightForearm`。**道光沿用0.12新增的`LeftEye`、`RightEye`，共11骨骼；其余20个模型仍为9骨骼。** 这是站姿人物和会谈动作的骨架；没有腿部行走骨骼或逐指动作控制。

`idle` 为 8 秒循环，包含细小呼吸、头颈和手臂运动。面部提供 `Blink`、`Speech`、`Expression` 形变；眨眼与轻微表情可随待机动画播放，`Speech` 由会谈反馈驱动。`TriggerGesture("greeting" | "talk" | "agree" | "refuse")` 叠加致意、交谈、同意和拒绝动作。没有真人动作采集、历史人物配音或基于录音的唇形同步。

道光的左右眼球围绕各自眼骨运动，微小视线变化并入同一`idle`轨道。它是自然化表现，不是对道光个人眼部动作习惯的历史复原，也不是其余人物都已获得独立眼骨。

[LeaderView.cs](../../Presentation/LeaderView.cs) 提供三档取景：

- **全身**（`full`）：根据整个模型的边界容纳服装与脚部。
- **常规**（`three_quarter`）：人物中近景。
- **面容**（`face`）：根据解剖脸部网格定位，避免高冠或身高差挤占面部画面。

右键拖动环绕、滚轮调节距离。朝政与外交页面的嵌入肖像使用独立渲染视口和相同 GLB；同一 `LeaderView` 重复显示相同身份、年龄阶段和资源版本时复用当前实例。切换人物、跨过年龄阶段或更新资源时才重建，不会预先常驻全部人物。

## 可编辑源文件与重建

0.13道光使用 [source/build_leaders_13.py](source/build_leaders_13.py)，其他人物沿用 [source/build_leaders_11.py](source/build_leaders_11.py)。0.12配方保留在 [source/build_leaders_12.py](source/build_leaders_12.py)，可输出到独立目录重建比较。依赖同目录模块和基础资产：

| 文件 | 职责 |
| --- | --- |
| `build_leaders_13.py` | 仅生成0.13道光：中年皮肤底色、较轻须根过渡、弯曲须髭；复用0.12面部结构及眼骨 |
| `qing_tailoring_13.py` | 本轮帽带织纹、帽顶贴面红丝、软领口与披肩中心垂坠，依赖0.12服饰基础 |
| `build_leaders_12.py` | 可独立重建0.12道光；提供0.13复用的年龄形体、眼骨与微视线方法 |
| `qing_tailoring_12.py` | 0.12披领、袖口和鞋靴基础，仍是0.13依赖 |
| `build_leaders_11.py` | 0.11造型配置与共用形体流程；继续用于其他19个身份、20个模型 |
| `period_wardrobe.py` | 本项目制作的全身军服、文官长礼服、宫廷裙、和服、袖子与鞋靴 |
| `period_hair.py` | 沿用0.11的发型拟合与透明发片制作模块 |
| `build_leaders.py` | 共用网格、材质、骨骼、表情、动画工具及大清服装制作 |
| `source/*.blend` | Blender 可编辑模型、材质、骨骼与形变源文件 |
| `source/makehuman/` | 带作者及许可说明的 CC0 基础网格、形变与系统资产 |

在包含 Blender 4.5、上述模块和完整基础资产的环境中，从项目根目录运行：

```powershell
# 只生成0.13道光候选
blender --background --python Assets/leaders/source/build_leaders_13.py -- --out-dir ../../work/leader13-candidate --render
# 其他人物仍使用0.11入口
blender --background --python Assets/leaders/source/build_leaders_11.py -- william_iv victoria isabella_ii --render
# 重建旧配方作比较，不覆盖项目道光
blender --background --python Assets/leaders/source/build_leaders_12.py -- --out-dir ../../work/leader12-reference --render
```

`--render`输出检查图，不代表图像已通过审核。12与13脚本支持`--out-dir <目录>`，将候选道光GLB、blend和可选检查图放到该目录；省略时才更新项目资源。全量重建顺序为11→13：先运行11脚本的`-- all`，最后运行13脚本覆盖旧道光，不必先执行12脚本。13内部仍依赖11、12、qing12、qing13及共用模块，不能删除这些源文件。13的`--legacy-clothes`仅用于面部隔离比较，最终组合模型不带此参数。

道光继续保留0.12在复制脸部材质之前连接细节法线的修复，避免法线只加到旧材质。0.13改用CC0中年亚洲男性皮肤贴图，降低法线强度并减轻须根顶点颜色；颊部、眼周与嘴唇的年龄结构仍由几何承担，不用斑驳底色代替。新增帽带和领口织纹以本项目生成的颜色及法线图嵌入资源，需要核验实际导入。

模型导出为Godot使用的Y轴向上、朝向+Z的GLB。面部细分按形变烘入，服装与毛发在导出前整理，纹理嵌入资源；运行游戏无需Blender或网络。`source/.gdignore`排除制作源文件的游戏资源导入。

## 来源与许可

外部基础人体、宏观形变、表情、眼睛、眉毛、毛发及皮肤来自 **MakeHuman 官方 CC0 图形资产**。不包含该软件的 AGPL 程序代码。服装几何、装饰、绑定、动画和构建脚本由本项目制作；没有使用 Victoria 3 或其他商业游戏的人物模型、贴图或动画。

- [MakeHuman 官方许可说明](https://static.makehumancommunity.org/about/license.html)
- [官方仓库图形资产许可](https://github.com/makehumancommunity/makehuman/blob/master/LICENSE.ASSETS.md)
- [官方系统资产包与 CC0 清单](https://static.makehumancommunity.org/assets/assetpacks/makehuman_system_assets.html)
- [随附 CC0 文本](LICENSE-MAKEHUMAN-CC0.txt)与[模型清单](model-manifest.json)

大清服饰的两张基础颜色纹样沿用项目生成素材，来源与提示词见 [TEXTURE-GENERATION-0.7.md](source/TEXTURE-GENERATION-0.7.md) 和 [NAVY-TEXTURE-GENERATION-0.7.md](source/NAVY-TEXTURE-GENERATION-0.7.md)；这些纹样不是历史画像扫描。馆藏链接仅作研究依据，不表示其数字图像已经获准作为纹理再发布。基础资产作者信息与许可声明保留在源文件中。

## 验证与剩余差距

运行时沿用0.11的透明发片处理：为 `CC0 fitted …` 材质创建实例覆盖，采用 AlphaScissor 与 MSAA AlphaToCoverage，减轻发片重叠产生的黑色三角块；保留贴图透明孔洞和阴影，不改写共享 GLB 材质。发片几何接缝仍需后续模型修整。共用肖像灯光继承0.12，0.13皮肤与服饰的效果应在Godot实际视图中核对。

0.13正式资源检查为[21/21、失败0](../../QA/leader-model-validation-0.13.json)；[Godot原生人物检查](../../QA/leader-runtime-validation-0.13.txt)仅覆盖道光1/1，不能写成全21个模型的原生回归。组合模型已在Godot审图并保留，帽带较窄且呈哑光织纹，红丝贴合帽面，肤色较柔和；胡须仍有细线与刷状感，披领仍显硬。Windows运行版已完成本轮验收：三项exe烟雾、五张界面截图、180帧实机录制及三种视图短程性能采样；结果见[TEST-REPORT.md](../../TEST-REPORT.md)，不等于历史相似度或商业美术质量达标。

构建、资源加载、动画、导出包与运行检查以项目 [TEST-REPORT.md](../../TEST-REPORT.md) 的实际记录为准；每个 GLB 的大小、校验值、骨骼及形变清单见 [model-manifest.json](model-manifest.json)。`faceview`、`framings`、`portraitreuse` 分别关注面部构图、三档取景和重复显示时的模型复用；它们不是美术相似度或性能达标声明。未运行的检查不在本文预填通过结果。

仍需人工改进逐人的头部相似度、发际与毛束、衣物厚度和褶皱、手部细节、儿童比例以及转身时的穿插。历史服装结构、勋章和年龄造型还需专业校订；当前成果不宣称达到 Victoria 3 或其他大型商业游戏的角色制作质量。
