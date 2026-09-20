# 游戏与领袖验证

## 0.14界面发行检查

`export-smoke-0.14.txt`、`export-content-smoke-0.14.txt`、`export-construction-smoke-0.14.txt`、`export-mechanics-smoke-0.14.txt`、`export-province-diplomacy-smoke-0.14.txt`为最终Windows EXE的实际stdout。机制烟雾新增外交同名按钮的稳定焦点检查；截图及鼠标操作覆盖见[当前测试报告](../TEST-REPORT.md)。

下文仍是原有3D独立验证工具说明，不意味着每轮都重新执行所有模型检查。

## 3D领袖独立验证

这些检查验证资源、骨骼、形变、构图与对象生命周期，不证明人物面貌符合历史，也不替代人工近景和动画审阅。当前目标为 20 位历史人物、21 个 GLB（伊莎贝拉二世含儿童及少年阶段）。

先用 Godot 4.5.1 .NET 导入主项目的最新资源。运行时检查需要先完成一次主项目 C# 构建；纯 GDScript 资源检查不需要重新构建 C#。

## 资源与动画数据

~~~powershell
& $GodotExe --headless --path $ProjectRoot --script 'res://QA/LeaderModelValidation.gd' -- "--report=$ReportPath"
~~~

要求全部 21 个文件存在并能实例化；检查全身包围盒、脚底位置、九个必需骨骼、蒙皮绑定、idle 动画、实际头颈运动、完整眨眼、可写的 Speech 权重。直接解析 GLB 中的稠密或稀疏 POSITION accessor，确认 Blink 和 Speech 含有效顶点位移，避免把只有形变名称的空数据误判为通过。

0.12额外要求道光的实际面部材质启用法线并连接纹理，以及`LeftEye`、`RightEye`两块眼骨在idle中各自有小于0.06弧度的真实运动。这用于防止复制材质后法线失联，以及只增加眼骨名称却没有视线动画的回退。

任意缺失、失效、过大动作或释放后遗留节点都会打印 LEADER_QA_FAIL 并返回退出码 1。完整成功打印 LEADER_QA_COMPLETE ... failures=0 并返回 0。JSON 报告记录每个模型的节点、三角形、包围盒、加载时间以及加载/释放前后的资源计数。

去掉 --headless 可记录实际渲染进程的显存、纹理、缓冲区与绘制调用计数。无头模式明确记为 null；这些进程计数不能直接当作单个模型的独占显存预算或跨设备基准。

## 原生运行时、复用与截图

~~~powershell
& $GodotExe --path $ProjectRoot 'res://QA/LeaderRuntimeChecks.tscn' -- "--capture-dir=$CaptureDirectory"
~~~

默认验证 21 个年龄阶段，要求使用真正的有骨骼 3D 资源，并检查：

- 切换全身、面容、角度、显示状态及同一阶段年龄时，不重建人物，骨骼实例保持不变。
- 伊莎贝拉二世未满 11 岁使用儿童文件，11 岁起使用 isabella_ii_adolescent.glb。
- 交谈后无骨骼累积漂移，头部运动在规定范围内。
- 选择截图目录时，分别保存每个模型的全身与面部原生渲染 PNG。

可加 --identity=daoguang 只检查一个身份；伊莎贝拉会检查两个阶段。不需要截图时省略 --capture-dir。此场景直接运行 LeaderView，不加载地图、模拟或主界面；面部构图依据解剖网格包围盒，高帽不会挤走面部。

## 受限 Windows 工作区

如果用户目录的 AppData 不可写，可只为当前验证进程指定工作区临时目录；不要修改系统环境：

~~~powershell
$env:APPDATA = Join-Path $WorkspaceRoot 'work/qa11/appdata'
$env:LOCALAPPDATA = Join-Path $WorkspaceRoot 'work/qa11/localappdata'
~~~

随后使用现有本地工具链的 $GodotExe、$ProjectRoot。保存并检查标准错误和退出码。环境导致的证书存储诊断与模型验证结果应分别报告，不能悄悄忽略真实导入、材质或动画错误。
