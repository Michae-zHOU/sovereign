# 可动三维领袖 · 0.7

这批人物是真正的网格、皮肤材质、骨骼和面部形变；不是旋转的照片或面向镜头的平面。默认打开三维人物。道光的旧二维概念肖像仍留在美术源文件中，但不再作为游戏中的默认领袖视图。

0.7只重做道光的全身比例、服装和垂臂绑定；其余四个解剖人物模型沿用0.6，另外十五个人物继续使用较早的风格化模型。

## 当前覆盖

| 人物 | 立体模型 | 可动内容 |
|---|---|---|
| 道光帝 | 正常全身比例、低冠、金龙纹长袍、深蓝披领、马蹄袖、腰带与佩饰、真实双手 | 呼吸、头颈姿态、眨眼、交谈口型、前臂 |
| 威廉四世 | 老年欧洲面部、灰色后梳发、海军风格礼服、真实双手 | 同上 |
| 年轻维多利亚 | 年轻女性面部、中分盘发、简化冠饰与礼服、真实双手 | 同上 |
| 德川家齐 | 较宽的中老年亚洲面部、发髻、深色和服、真实双手 | 同上 |
| 德川家庆 | 较窄的较年轻亚洲面部、发髻、另一套和服、真实双手 | 同上 |

其余15个人物继续使用原有程序化三维模型，已支持点头、摇头、致意和交谈动作反馈；它们没有本批人物的解剖网格和皮肤材质。人物身份与在位时间来自 `Simulation/HistoricalLeaders.cs`。这五个模型也是美术重建：不是本人扫描、不是经考证的精确容貌，服饰尚需专业历史美术校订。模型固定在相应阶段的视觉年龄，尚无逐年面部衰老系统。

## 动画与接入

道光GLB含9个骨骼：Root、Pelvis、Chest、Neck、Head、LeftUpperArm、LeftForearm、RightUpperArm、RightForearm。其余四个GLB仍含6个骨骼：Root、Chest、Neck、Head、LeftForearm、RightForearm。`idle` 是8秒循环，包含胸部呼吸、头颈微动、前臂微动及真实眼睑眨眼。面部另含 `Speech` 形变，由会谈动作驱动。

`Presentation/LeaderView.cs` 的 `TriggerGesture("greeting" | "talk" | "agree" | "refuse")` 在基础动画上驱动头骨与前臂，交谈时驱动口型。动作本身不声称真实历史人物说过游戏生成的台词，也不提供配音或基于录音的唇形同步。右键拖动可环绕，滚轮缩放。镜头读取实际网格尺寸。道光支持全身、常规、面容三档取景，常规为头部至膝上；其他四位仍是半身模型，不显示全身按钮。布景随环绕方向转动，避免相机转入背墙。

## 可编辑源文件与重建

`source/build_leaders.py` 是本项目编写的 Blender 4.5 脚本；`source/*.blend` 包含五位人物的可编辑网格、材质、骨骼、形变和动画；`source/makehuman/` 保存本次用到的 CC0 源网格、形变、纹理、头发及眉毛资产。旧 `source/imperial_brocade.png` 为原型程序纹样。0.7新增 `source/imperial_dragon_brocade_v2.png` 和 `source/imperial_navy_brocade_v1.png`，由内置 imagegen 生成，完整提示词与处理记录位于同目录的 `TEXTURE-GENERATION-0.7.md`、`NAVY-TEXTURE-GENERATION-0.7.md`。它们是三维材质基础颜色贴图，不是二维人物照片。

在包含 Blender 4.5 的环境中运行：

```powershell
blender --background --python Assets/leaders/source/build_leaders.py -- daoguang william_iv victoria tokugawa_ienari tokugawa_ieyoshi
```

脚本按米制导出 GLB，Godot 中 Y 轴向上、朝向 +Z。模型在美术阶段保留许多朝珠、发丝对象；导出前按材质合并，避免把数百个小对象带进游戏。面部的 Catmull-Clark 细分按每个表情分别烘入网格；因此游戏不依赖 Blender 的修改器，也不会丢失眨眼/说话形变。纹理直接嵌入 GLB，无需联网。`source/.gdignore` 将重建源文件排除在游戏资源导入之外。

## 来源和许可

MakeHuman 官方基础人体、宏观形变、表情、眼睛、眉毛、头发和皮肤属于官方 CC0 图形资产，**不包含该软件的 AGPL 程序代码**。本项目的网格读取、衣服拟合、服装制作、骨骼绑定、动画与导出脚本是独立编写的。本批制作没有使用 Victoria 3 或其他商业游戏的人物模型、贴图或动画。

- [MakeHuman 官方许可说明](https://static.makehumancommunity.org/about/license.html)
- [官方仓库的图形资产许可](https://github.com/makehumancommunity/makehuman/blob/master/LICENSE.ASSETS.md)
- [官方系统资产包与逐项 CC0 列表](https://static.makehumancommunity.org/assets/assetpacks/makehuman_system_assets.html)

完整 CC0 法律文本位于 `LICENSE-MAKEHUMAN-CC0.txt`；来源提交、资产包校验值、每个 GLB 的校验值/面数/骨骼数见 `model-manifest.json`。感谢 MakeHuman Team、Data Collection AB、Joel Palmius 和 Jonas Hauquier 提供基础资产。原始资产文件中的作者与许可声明保留在 `source/makehuman/`。

## 验证

`source/validate_animation.gd` 在 Godot 中加载五个 GLB，逐帧推进8秒，检查骨骼运动、眨眼完整行程和说话形变；验证日志见 `ANIMATION-VALIDATION.txt`。另外已在 Godot 实机核对眼白/瞳孔、袖口/手部、默认构图及模型转动。该验证证明资源与动画能够运行，不代表已达到大型商业游戏最终角色制作质量。
