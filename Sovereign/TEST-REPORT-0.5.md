# 万国纪元 · 大清篇 0.5 — 验证记录

验证日期：2026年9月19日。范围为政府建造重构、界面重做和道光二维肖像。模拟规模仍为12国、36个场景地区、128城（大清40城）、1836—1846；不是完整版或美术质量验收。

| 检查 | 结果 |
|---|---|
| C# Debug、ExportRelease | 均成功，0警告、0错误 |
| Godot资产导入、最终Windows导出 | 成功 |
| 独立模拟测试 | 23/23组，含12国完整3653日运行；最终短缺与存档校验修正后，相关9/9组再次通过 |
| 独立建造审查 | 15项断言通过；另验证缺铁时木架继续生产，等待项目数量不会提高真实产能 |
| 真实旧存档 | v1/v2/v3夹具迁移通过，v3来自发布0.4；旧预付工程104日内全部完成，无二次收费或额外等级，存读两分支最终摘要一致 |
| 打包程序基本集成 | SOVEREIGN_SMOKE_PASS date=1836-03-01 build=True |
| 打包程序建造界面 | SOVEREIGN_CONSTRUCTION_UI_PASS enqueue pause resume priority cancel sectors save portrait-switch |
| 打包程序内容场景 | SOVEREIGN_CONTENT_PASS cities=128 leaders=20 districts=640 |
| 打包程序地图界面 | SOVEREIGN_WORLD_UI_PASS records=314 modes=4 drawer=True |

最后四项使用实际导出程序，分别等待进程退出并检查完成标记与错误日志。退出码均0，错误日志为空。中文审查仅报告帮助中的Windows产品名，并不表示全部历史专名校译完成。

建造集成测试通过控件信号完成选址入队、暂停恢复、优先级调整、取消、营建部门查看、存档往返和道光二维/三维原型互切。模拟测试覆盖持续费用、有限材料、岗位、共享能力、完成日余量、终局保护及损坏存档拒绝。

从导出程序截取全国队列和道光肖像；另检查选址、营建部门和朝政布局。修正了肖像图层遮挡操作界面的问题。截图中施工数据来自运行中的模拟。新肖像是二维图片，原三维模型没有因此达到写实质量。

最后通过真实Windows窗口鼠标操作验证：打开建设→商品农场→明确选择苏州→全国队列显示“苏州、商品农场、0/60点”→推进一天后显示“1.4/60点”，日期到1836年1月2日。新版保持暂停，未写入手动存档。该检查与控件信号测试分别记录，不声称逐个手点过全部城市或功能。

## 性能

Windows，RTX 3080 Ti，Godot4.5.1 .NET，Vulkan/Forward+，1600×1000、4×MSAA。各场景预热2秒后收集480个ProcessFrame间隔；采样单独运行，用户其他应用仍可能占用硬件。

| 视图 | 中位帧间隔 | 第95百分位 | 节点快照 |
|---|---:|---:|---:|
| 大清地图，暂停 | 12.18毫秒 | 15.32毫秒 | 3739 |
| 大清地图，12倍速 | 9.14毫秒 | 12.30毫秒 | 3739 |
| 苏州城市，暂停 | 8.34毫秒 | 9.04毫秒 | 3805 |
| 初始500项全国队列，12倍速 | 9.02毫秒 | 12.98毫秒 | 3912 |

长队列测试初始填充500项，允许推进和完工；每页最多8张工程卡，预测按ID查找。已有地图索引、分频标签、场景按需加载、HUD复用保留。

这些是短时采样，不是最低配置、长期帧率或严格的修改前后对照。低端硬件、完整世纪、长期内存增长未验收。发布引擎MemoryStatic返回0，视为数据不可用。

## 复现

独立测试：`dotnet run --project ./Sovereign.Tests/Sovereign.Tests.csproj -c Release`。

Windows发布程序是GUI子系统程序。自动检查需使用`Start-Process -Wait -PassThru`并重定向stdout/stderr，避免把启动命令返回误当作游戏测试完成：

```powershell
$exe = (Resolve-Path ./Sovereign.exe).Path
$run = Start-Process -FilePath $exe -ArgumentList @('--headless','--','--construction-smoke') -Wait -PassThru -RedirectStandardOutput construction.log -RedirectStandardError construction.err
$run.ExitCode
Get-Content construction.log
Get-Content construction.err
```

同样方式运行`--smoke`、`--content-smoke`、`--world-smoke`。性能采样使用`@('--','--profile')`，不会写入手动存档。

原作对照与未实现依赖见[CONSTRUCTION-REFERENCE.md](CONSTRUCTION-REFERENCE.md)，实际算法见[CONSTRUCTION-SYSTEM.md](CONSTRUCTION-SYSTEM.md)。私人投资、州经济与效率、GDP基础产能缩放、科技化项目上限和完整生产方式未完成。政府建造采用独立原型参数，不能称为完整复刻。

