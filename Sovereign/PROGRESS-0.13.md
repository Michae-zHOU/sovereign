# 继续制作接续点：0.13道光

**当前状态：0.13 Windows运行版已完成本轮验收。分发ZIP和哈希以`outputs/release-manifest-0.13.json`及实际文件为准。** 本文件用于后续工作接续，不代表已安排自动运行。开始下一次工作前核对最新记录，避免重复生成已确认模型或把旧结果当作本轮验证。

## 本轮边界

只调整道光的皮肤底色、须根与胡须，以及帽子、领口和披肩的织物形体。保留0.12已有的脸部结构、11骨骼与眼部动作，不新增游戏系统。其余19个历史身份的20个模型沿用0.11。

范围仍为12国、20个身份、21个模型、60地区、128城、6商品及1836—1846战役；经济和政治沿用0.10，存档版本6。完整世界城市、全领袖重制及商业游戏成品美术质量均未完成。玩家可读的本轮说明见[RELEASE-0.13.md](RELEASE-0.13.md)。

## 已落盘内容与依赖

| 文件 | 本轮职责与当前记录 |
| --- | --- |
| [build_leaders_13.py](Assets/leaders/source/build_leaders_13.py) | 中年皮肤底色、较弱法线、较轻须根染色、弯曲渐细胡须；只生成道光。正式GLB与blend已重建、导入。 |
| [qing_tailoring_13.py](Assets/leaders/source/qing_tailoring_13.py) | 帽带织纹、低反光帽顶与贴面红丝、软领口和披肩中心垂坠；组合模型已在Godot审图并保留。 |
| [build_leaders_12.py](Assets/leaders/source/build_leaders_12.py)、[qing_tailoring_12.py](Assets/leaders/source/qing_tailoring_12.py) | 0.12基线，提供面部结构、眼骨、动画和服饰基础；仍是0.13依赖，不能删除或单独运行后误覆盖本轮道光。 |
| [build_leaders_11.py](Assets/leaders/source/build_leaders_11.py)、[build_leaders.py](Assets/leaders/source/build_leaders.py) | 其余人物和共用骨架、网格、材质及导出工具。全量重建时最后再生成0.13道光。 |

正式制作日志为`work/leader13-final-authoring.log`。当前效果见原生[面容](../Sovereign-Leaders-0.13/daoguang-face.png)与[全身](../Sovereign-Leaders-0.13/daoguang-full.png)截图。

已完成的检查与边界：

- Debug与ExportRelease构建均为0警告、0错误；正式道光资源已导入。`work/verify13.ps1`完成导出前流程。
- [模型资源验证](QA/leader-model-validation-0.13.json)为21/21、失败0。[原生人物组件检查](QA/leader-runtime-validation-0.13.txt)仅覆盖道光1/1，不能写成21个模型的原生回归。
- Windows导出已成功，`work/export13-sdk-selection.json`记录`verified: true`，使用.NET8，运行时目录186个文件。
- `work/check_release13.ps1`完成并打印`SOVEREIGN_RELEASE_013_VERIFIED`。最终exe的`--smoke`、`--content-smoke`、`--province-diplomacy-smoke`均退出0；道光面容／全身、英国领袖、幼年女王和外交会谈五张完整界面截图已生成。stderr仍有已知根证书存储诊断，没有写成无日志。
- [实机视频](../Sovereign-0.13-道光动态.mp4)为180帧、30fps、6秒，第90帧已确认切到近景。[三种视图短程采样](QA/leader-performance-0.13.txt)已完成，具体数值与本机限制见[TEST-REPORT.md](TEST-REPORT.md)。
- 组合审图可保留：帽带较窄且呈哑光织纹，红丝贴帽面，肤色较柔和。胡须仍略刷状，披领和衣服仍偏硬。0.12记录只作为旧基线。

最终结果已经写入[TEST-REPORT.md](TEST-REPORT.md)。保留旧发行包、0.12可重建配方及本轮证据；后续如无新问题，不为分发重复制作正式模型。

## 下一轮优先切入：修形体

下一轮优先选择**披领锥形与衣服硬筒形体**这一项，先让轮廓和体积发生可检查的改善；不以再次换纹理或增加版本号作为完成标准。人物站姿自然动作是另一个明确方向，应在形体改动确认后单独推进，避免两项同时改动而难以判断效果。

1. 以0.13正式模型为基线，在固定Godot灯光、相机、同一待机帧下保存正面、侧面及三分之四视角；当前披领像锥形壳、袍身像硬筒，须从多个方向记录。
2. 在独立候选中调整披领落肩、前后下垂、轮廓转折与衣身截面，让衣物围绕肩胸腰部形成厚度和布料堆叠；保留原有袖口、手部、纹样UV与骨骼对应，检查领口、腋下和袖子避让。具体参数由实际网格决定，不能只用法线假装改变轮廓。
3. 在相同取景下比较：披领侧影是否减弱均匀锥形，衣身是否减少等宽硬筒感，转角和致意动作是否出现新的穿插。确认一项形体问题得到改善后，才讨论纹理微调及正式导出。
4. 后续若转向自然动作，重点处理左右肩臂完全对称、重心固定及站姿僵硬，制作幅度小且有重心依据的待机变化；现有骨架没有腿骨，不能声称已实现步态或真实迈步。形体与动作都应在真实游戏中比较，不能以脚本完成标记代替审图。

## 重建入口

候选制作命令示例，在`outputs/Sovereign`目录执行；需要Blender4.5.3及现有CC0源资产：

```powershell
blender --background --python Assets/leaders/source/build_leaders_13.py -- --out-dir ../../work/leader13-candidate --render
```

`--out-dir`将GLB、blend及可选检查图写入指定目录；省略它会更新项目道光资产。详细工具与原生检查入口见[BUILD.md](BUILD.md)和[QA说明](QA/README.zh-CN.md)。本机工具路径与中间日志属于工作区内容，不是发行依赖。

## 保留的问题

- **形体与站姿：**披领锥形、衣服硬筒和人物站姿僵硬仍明显；这是下一轮需真正修复的主要问题，不能用更细织纹掩盖。
- **皮肤：**底色柔和后，色彩层次仍待提高；需要在额头、眼下、鼻唇沟和侧颊保留体积，不能靠深色噪点代替年龄。
- **须髭：**上唇细毛仍略刷状，正常缩放下的暗片、领口避让及动作衔接继续观察，不把密度调整等同于真实毛发质量。
- **织物：**披肩侧肩区域沿用旧避让形体，帽带、领边和织纹仍可能重复或缺少厚度；已有审图不证明所有角度已无穿插。
- **历史与质量：**道光精确肤色、须髭细节与个人眼神习惯缺乏可直接复原的证据。沿用[历史与美术依据](Assets/leaders/source/DAOGUANG-ART-DIRECTION-0.12.md)的不确定性说明，不声称写实、完美或全领袖完成。

后续状态以实际文件、日志和审图结论为准；分发记录由主线程维护。
