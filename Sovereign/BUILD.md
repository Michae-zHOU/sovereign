# 构建 Sovereign 0.10

Sovereign 是 Godot 4.5.1 .NET / C# 的 Windows 原生开发原型。0.10 新增订单经济、地区价格、人口阶层、生产方式、建筑经营、私人投资和政治过程；目录范围仍为12国、60地区、128城、6商品及1836—1846。参见 [README.md](README.md)和 [RELEASE-0.10.md](RELEASE-0.10.md)。

## 工具要求

- Godot **4.5.1 .NET** Windows x64；普通非 .NET 版不能加载 C#。使用该版本匹配的 .NET 导出模板。
- .NET **8 SDK**；本工作区使用8.0.425，项目目标为`net8.0`、`Godot.NET.Sdk/4.5.1`。
- 首次 NuGet 恢复需要网络，之后可复用本地缓存。

保留`.csproj`、`.sln`、`project.godot`、`main.tscn`、`export_presets.cfg`、源文件、资源、`.uid`及许可，以及相邻的`Sovereign.Tests`项目。源代码包不需要`.godot`、`bin`、`obj`或旧导出目录。

## 编译、导入与启动

在`Sovereign`源码目录打开PowerShell，填写本机编辑器路径：

```powershell
$ErrorActionPreference = 'Stop'
$ProjectPath = (Get-Location).Path
$GodotExe = 'C:\Tools\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64_console.exe'
dotnet build (Join-Path $ProjectPath 'Sovereign.csproj') --nologo
if ($LASTEXITCODE -ne 0) { throw 'C# build failed.' }
& $GodotExe --headless --path $ProjectPath --editor --import
if ($LASTEXITCODE -ne 0) { throw 'Godot import failed.' }
& $GodotExe --path $ProjectPath
```

资源导入应在C#编译完成后执行。一般启动进入国家选择；附加`-- --preview`直接打开暂停的大清开局。设计视口为1600×1000，画布随窗口缩放。默认Forward+和4×MSAA；附加`--rendering-method gl_compatibility`使用兼容渲染。

## 验证流程

下面是应执行的检查，不是本版本已通过的结果。实际输出写入`TEST-REPORT.md`，不要以编辑器运行成功代替导出验证。

```powershell
$TestsProject = Join-Path (Split-Path $ProjectPath -Parent) 'Sovereign.Tests/Sovereign.Tests.csproj'
dotnet run --project $TestsProject --configuration Release
if ($LASTEXITCODE -ne 0) { throw 'Simulation checks failed.' }
foreach ($Mode in @('--smoke', '--construction-smoke', '--world-smoke', '--province-diplomacy-smoke', '--content-smoke')) {
    & $GodotExe --headless --path $ProjectPath -- $Mode
    if ($LASTEXITCODE -ne 0) { throw "Failed: $Mode" }
}
```

检查退出码、对应`SOVEREIGN_*_PASS`标记、stderr、缺失资源及`ZH_REVIEW`输出。纯模拟项目覆盖哪些用例以其当前源码和实际输出为准；保留其全部`Fixtures`。导出exe应再执行相应检查，不能仅验证编辑器的缓存程序集。

原生画面与交互还需检查：市场订单、地区价格差、七阶层的地区筛选、生产方式技术锁、资格提示、盈利/亏损数字、公私资金来源、铁路排队及完工、政府与私人暂停、法律审议、组阁和机构预算。核对正常与最小支持窗口的截字、溢出、圆形按钮、中文输入、滚动、地图选择和三维人物。无界面测试不能证明美术质量或控件命中。

已有截图入口示例：

```powershell
& $GodotExe --path $ProjectPath -- --capture 'C:\Temp\province-prices.png' --province QNG_jiangsu --province-page prices
& $GodotExe --path $ProjectPath -- --capture 'C:\Temp\province-pops.png' --province QNG_jiangsu --province-page population
& $GodotExe --path $ProjectPath -- --capture 'C:\Temp\population.png' --population
& $GodotExe --path $ProjectPath -- --capture 'C:\Temp\construction.png' --construction
```

确保输出目录已存在。截图模式建立独立演示战役并推进日期，不读取用户存档。参数以`Presentation/Main.cs`当前实现为准。`-- --leader-clip <绝对帧目录>`写出180帧运行时人物画面，可用ffmpeg编码；它不是生成式人物视频。

## Windows导出与打包

```powershell
$BuildPath = Join-Path (Split-Path $ProjectPath -Parent) 'Sovereign-Windows-0.10'
New-Item -ItemType Directory -Force -Path $BuildPath | Out-Null
& $GodotExe --headless --path $ProjectPath --export-release 'Windows Desktop' (Join-Path $BuildPath 'Sovereign.exe')
if ($LASTEXITCODE -ne 0) { throw 'Windows export failed.' }
```

使用版本化目录保留旧发行包。资源包嵌入exe，托管代码和运行时放在`data_Sovereign_windows_x86_64`；必须分发整个目录。保留`.json`、`.geojson`、`.bin`导出包含规则及导入后的纹理、字体。源包排除生成缓存，并包括相邻的测试项目与固定存档样本。

导出器不会自动创建全部可读说明，打包时还应复制：

- `README.md`、`开始游戏.md`、`BUILD.md`、`RELEASE-0.10.md`、`MECHANICS-PARITY.md`及最终`TEST-REPORT.md`。
- `HISTORY-SOURCES.md`、`QING-DESIGN.md`、`QING-SETTLEMENT-SOURCES.md`、`CONSTRUCTION-REFERENCE.md`、`INTERFACE-REFERENCE-0.9.md`与`THIRD_PARTY_NOTICES.md`；带版本的旧文档保留为历史证据，当前规则以0.10说明为准。
- `Assets/interface`、`illustrations`、`flags`、`portraits`、`leaders`的来源说明、生成记录和许可；国旗的单独署名与许可条件必须保留。
- `Assets/map/historical`完整来源、原始/派生资料、README和GPL-3.0许可；自然地理说明和其他资源许可。
- `Licenses`以及四类字体的OFL许可、`Sovereign-Compatibility.cmd`。

历史底图采用GPL-3.0，不能改标为Natural Earth公共领域或Godot MIT。兼容启动器使用`start "" "%~dp0Sovereign.exe" --rendering-method gl_compatibility`。

## 模块边界

| 模块 | 职责 |
| --- | --- |
| `EconomicModels.cs`、`OrderEconomy.cs` | 地区/全国订单、价格、人口预算、自给生产、基础设施和每日结算 |
| `ProductionMethods.cs` | 18种配方、岗位与资格、建筑账户和公私等级 |
| `ConstructionSimulation.cs` | 公私共享建造能力、两类资金来源、施工日快照及铁路完成效果 |
| `PoliticalSimulation.cs` | 8集团、25法律、8法组、阶段审议、政府和4类机构 |
| `Persistence.cs`、`EconomicValidation.cs` | 第6版格式、1—5版迁移、一致性及摘要校验 |
| `RegionalSimulation.cs`、`GeographyCatalog.cs` | 场景地理、人口变动和地方/全国汇总 |
| `EconomicMechanicsInterface.cs`、`PoliticalMechanicsInterface.cs` | 可操作的经济和政治界面 |
| `ConstructionInterface.cs`、`ConstructionVisuals.cs`、`ProvinceInterface.cs` | 建造队列、经营明细、方法选择及省份详情 |
| `CabinetInterface.cs`、`ReferenceInterface.cs` | 0.9沿用的HUD、导航、时间、事务与面板外框 |
| `WorldMap.cs`、`ProvinceMap.cs`、`WorldCountryInspector.cs` | 1815参考疆域、示意省份、地图命中和覆盖说明 |
| `CityView.cs`、`SettlementScenes.cs`、`LeaderView.cs` | 按需城市场景、人物、骨骼与表情动画 |

模拟层不依赖Godot，金额和数量主要使用`decimal`，每日模拟与渲染分离。界面读取状态并提交命令，不自行修改经营账户。新经济不从旧`Goods`库存扣工业投入；本地价格不等同全国统一价。建设以当日计划提交订单并结算，私人支出不能混入国库。

命令返回`CommandResult`，必须检查`Success`并呈现原因。事件或终点可能使推进提前停止。加载必须经`SimulationEngine.LoadJson`校验，不能直接反序列化替换运行世界。

## 存档迁移

当前写入第6版。有效第1—5版依序迁移：原三国地理扩展、大清旧八城向40城的人口调整、旧预付项目转建造点、旧大清地理转27区域的规则仍保留。第5→6版初始化生产账户、地区订单、人口群体、法律与机构，已获得的旧改革映射为相应法律和一级机构，不重复收取改革费。

迁移保留载入日旧价格、财政和产出快照，下一日切换新订单结算；不补算新机制在过去日期的结果。旧库存保留兼容用途，不再作为价格和投入来源。新格式保存法律审议进度和政治随机状态，以及建筑账户和私人建设信息。只支持旧格式的exe不能读取第6版；跨版本验证保留原文件副本。

## 美术与原工作区工具

人物及地图沿用先前版本。道光源文件在`Assets/leaders/source`，可用Blender4.5.3执行`build_leaders.py -- daoguang`重建，再导入GLB。详细模型来源与重建要求见`Assets/leaders/README.zh-CN.md`。不要把仅在Blender中存在的着色效果当作已导出的运行时效果。

界面沿用1000单位设计高度、502单位普通抽屉和86单位标题区。比较参考画面时需考虑客户区尺寸和Windows DPI；圆形皮肤与弧形托架不要用不匹配的九宫格边距拉伸。图集顺序契约和原创风景来源见资源README。0.10人口、价格和政治页已改用新模拟，不应恢复0.9的全国一价、三项即时改革文案。

原工作区`work/toolchain/Environment.ps1`仅用于寻找本机SDK与Godot，不是发行依赖。复制源码只需前述标准工具。旧`Export.ps1`可能指向较早目录，使用本文件明确的`Sovereign-Windows-0.10`路径。
