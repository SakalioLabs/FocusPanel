# FocusPanel

FocusPanel 是面向 Windows 11 的右侧玻璃任务栏与桌面效率工作区。它保留桌面收纳、任务、番茄钟、OKR、AI 和 SQLite 数据，同时提供应用启动、运行窗口管理、系统状态与日期时间入口。

![Windows 11 侧边任务栏](artifacts/shell-redesign-onboarding.png)

## 新壳层

- 主屏右缘显示一条 `3px` 白色运行指示条；它完全点击穿透。最后 `12` 个物理像素由无窗口监测器检测，停留约 `100ms` 唤出 `76px` 紧凑应用坞。
- 点击搜索、桌面收纳、任务、番茄钟、OKR、AI 等入口后，工作区从右向左展开到约 `720px`。
- 离开约 `300ms` 自动收起；搜索框或编辑控件持有焦点时保持展开，`Esc` 可关闭。
- 独占或无边框全屏应用前台时默认停用鼠标热区。
- 全局主动唤出：`Ctrl+Alt+Space`。
- 固定应用与运行应用集中显示；运行应用支持激活、最小化、多窗口列表和正常关闭。
- 日期入口打开月历与今日任务，系统区提供音量、静音、网络、电池、通知、输入法、显示桌面和电源操作。

## 任务栏替代模式与安全恢复

任务栏替代模式只隐藏主显示器的 `Shell_TrayWnd`，不会结束 Explorer，也不会处理副屏的 `Shell_SecondaryTrayWnd`。主屏工作区只在当前会话临时释放，退出时恢复原值。

首次启用前会显示安全说明。只有在侧边壳层、热区以及独立恢复守护进程都就绪后，FocusPanel 才会隐藏原任务栏；紧急快捷键注册失败时不会进入替代模式。

- 紧急恢复：`Ctrl+Alt+Shift+F10`
- 正常退出、未处理异常、数据库恢复重启：均先恢复任务栏和工作区
- 父进程异常退出：`--taskbar-watchdog` 守护模式负责恢复
- Explorer 重启或显示设置改变：重新识别主任务栏并按当前模式处理
- 恢复会话：`%LOCALAPPDATA%\FocusPanel\taskbar-session.json`

遇到异常时，先按紧急恢复快捷键。仍未恢复可重新启动 FocusPanel；启动阶段会检查并恢复遗留会话。程序永远不会通过结束 Explorer 来实现任务栏替代。

## 桌面收纳与效率模块

- 新收纳文件始终保留在原桌面路径，不改名、不移动；FocusPanel 保存原始文件属性并追加 `Hidden + System`，取消收纳时精确恢复原属性。
- 从 Windows 桌面把文件或文件夹拖入收纳分区，会立即执行同一套隐藏事务；其他目录的项目不会被擅自移动。
- 普通“显示隐藏项目”开启时，已收纳图标仍会隐藏；如果同时开启“显示受保护的系统文件”，设置页会提示 Windows 无法保证图标不可见。
- 不再注入或持续修改 Explorer 的桌面列表；Explorer 刷新、重启和系统重启后按文件属性保持状态。
- 属性改变后会通知 Shell 更新项目并重新枚举桌面目录，避免图标只变成半透明却仍停留在桌面。
- 旧版 `.FocusPanel` 仓库继续兼容，升级时不自动移动旧文件。
- 不创建全屏桌面覆盖窗口；Windows 原生桌面保持可点击，文件收纳操作集中在侧边栏工作区完成。
- 保留任务的项目/子任务、列表/看板和自定义字段语义。
- 保留番茄钟会话、飞书 OKR 双向同步、AI 入口、数据库备份与恢复。
- 新增固定应用持久化；`PinnedApps` 表由现有 `EnsureSchema()` 机制创建，不改动原业务表内容。

## 兼容性与视觉

- 推荐 Windows 11 22H2（Build 22621）或更高版本。
- 使用 DWM Desktop Acrylic、圆角和自定义 Fluent 资源，不再依赖 MaterialDesignInXamlToolkit。
- 跟随系统浅色/深色主题；高对比度、关闭透明效果、远程桌面或 DWM 不可用时降级为不透明主题。
- 启用 Per-Monitor V2 DPI，支持多显示器与不同缩放比例。
- 第一版不镜像第三方托盘图标、不提供实时窗口缩略图，也不重做完整开始菜单或通知中心。

## 技术栈

| 模块 | 技术 |
| --- | --- |
| 框架 | C# / .NET 7 / WPF |
| 架构 | MVVM / CommunityToolkit.Mvvm |
| UI | 自定义 Fluent ResourceDictionary / DWM |
| 数据库 | SQLite / EF Core 7 |
| 托盘 | Hardcodet.NotifyIcon.Wpf |
| 系统集成 | Win32 / DWM / AppBar / Core Audio / Shell |
| 安装与更新 | Velopack 1.2 / GitHub Releases |

## 项目结构

```text
FocusPanel/
├─ Views/        # 壳层和业务页面
├─ ViewModels/   # MVVM ViewModel
├─ Models/       # EF Core 实体和 DTO
├─ Services/     # 壳层协调、系统集成与原有业务逻辑
├─ Data/         # AppDbContext 和 EnsureSchema 手工迁移
├─ Helpers/      # 桌面、图标及 Win32 互操作
├─ Themes/       # Fluent 设计令牌与控件样式
├─ Controls/     # 项目自有兼容控件
└─ Tests/        # 不触碰真实任务栏的单元测试
```

## 构建、测试与运行

```bash
dotnet build FocusPanel.csproj
dotnet test Tests/FocusPanel.Tests.csproj
dotnet run --project FocusPanel.csproj
```

要求：

- Windows 11
- .NET 7 SDK
- 仓库没有 `.sln` 文件，直接构建 `FocusPanel.csproj`

数据库位于 `%APPDATA%\FocusPanel\focuspanel.db`。启动时会执行 `EnsureSchema()`；数据库损坏时会归档损坏文件并尝试恢复最新滚动备份。`--restore` 参数可从最新备份恢复。

## 安装包与一键更新

应用目标框架仍为 `.NET 7`。生成 Velopack 安装包时额外需要 `.NET 8 SDK`，它只用于运行打包工具：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\package-release.ps1 `
  -Version 0.9.14 `
  -Dotnet8Path dotnet `
  -PublishDotnetPath dotnet `
  -CleanPackages
```

安装包输出到 `artifacts/release/packages/`，其中包括：

- `FocusPanel-win-Setup.exe`：首次安装入口。
- `FocusPanel-0.9.14-full.nupkg`：完整更新包。
- `releases.win.json`、`assets.win.json` 和 `RELEASES`：更新清单。
- 后续版本生成的 delta 包：用于减少更新下载量。

安装版和 Velopack 便携版会在“设置与恢复 → 软件更新”中显示当前版本。点击“一键检查并安装更新”后，FocusPanel 会从项目的 GitHub Releases 检查版本，显示更新说明，下载更新包，备份数据库，恢复 Windows 任务栏，然后重启安装。

源码直接运行的开发版不会原地覆盖自身，设置页会提示先安装 `Setup.exe`。

将生成的包上传为 GitHub Release 草稿：

```powershell
$env:GITHUB_TOKEN = "仅放在当前终端，不要写入仓库"
.\scripts\publish-github-release.ps1 `
  -Version 0.9.14 `
  -Dotnet8Path dotnet
```

确认后添加 `-Publish` 可正式发布。推送 `v*` 标签或手动运行“构建并发布 Windows 安装包”工作流，也会自动构建、测试、生成差分包并创建 Release。

当前仓库没有提供代码签名证书，因此本地生成的安装包会显示“未知发布者”。正式分发时应通过 `-SignParams` 传入 `signtool.exe` 参数，或在发布工作流中接入 Azure Trusted Signing；不要把证书密码写入仓库。
