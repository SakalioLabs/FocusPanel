# FocusPanel

FocusPanel 是面向 Windows 11 的右侧玻璃任务栏与桌面效率工作区。它保留桌面收纳、任务、番茄钟、OKR、AI 和 SQLite 数据，同时提供应用启动、运行窗口管理、系统状态与日期时间入口。

![FocusPanel 0.9.41 总览](docs/images/readme-overview.svg)

> 上图及下方模块图为 0.9.41 界面结构示意，用于说明信息层级和交互关系。实际毛玻璃、背景取样和亮暗色效果由 Windows 11 DWM、透明效果开关及当前壁纸共同决定。

## 新壳层

- 主屏右缘显示一条 `3px` 白色运行指示条；它完全点击穿透。最后 `12` 个物理像素由无窗口监测器检测，停留约 `100ms` 唤出 `76px` 紧凑应用坞。
- 点击搜索、桌面收纳、任务、番茄钟、OKR、AI 等入口后，工作区从右向左展开到约 `720px`。
- 离开约 `300ms` 自动收起；搜索框或编辑控件持有焦点时保持展开，`Esc` 可关闭。
- 应用右键菜单、多窗口列表和桌面收纳的视图/新建/修复弹层打开时会锁住 Panel；菜单关闭并且鼠标离开后才恢复自动收起。
- 桌面文件卡片只有移动距离超过 Windows 系统拖拽阈值后才开始拖动；靠近主内容区上下边缘时平滑滚动，移回中部、离开、取消、释放或完成放置后立即停止。
- 独占或无边框全屏应用前台时默认停用鼠标热区。
- 全局主动唤出：`Ctrl+Alt+Space`。
- 主动唤出后焦点落到搜索入口，可使用 Tab、Shift+Tab 或方向键循环浏览紧凑栏，Enter/Space 执行；应用按钮向读屏提供应用名称和窗口摘要，Shift+F10 或菜单键打开右键菜单。
- 键盘导航使用统一的 2px Fluent 圆角焦点环，轮廓只在键盘操作时出现，不给鼠标点击增加常驻边框；高对比度模式跟随 Windows 系统高亮色。
- 固定应用与运行应用按 Windows AppUserModelID 或可执行路径合并为单一任务栏图标；固定项保持用户顺序，未固定运行项保持本次运行中的稳定顺序。
- 窗口前台状态改变时按应用身份增量更新图标，只替换真正变化的项目，不再清空并重建整条应用栏，因此滚动位置和未变化图标保持稳定。
- Panel 隐藏后暂停完整窗口枚举、时钟、系统状态和任务摘要刷新；右缘热区、全屏抑制、安全恢复和 GitHub 更新检查继续运行。再次唤出时先刷新窗口快照和当前时间，状态中心与日历在打开时即时刷新。
- 未运行的固定项点击启动；单窗口应用点击激活/最小化；多窗口应用点击展开窗口列表，并可逐个切换、正常关闭或关闭全部窗口。
- 运行项可通过右键固定；拖动未固定运行项会自动创建固定项并保存排序，取消固定后只要窗口仍在就继续显示。
- 紧凑栏固定为开始、搜索、任务视图、Focus 中心、状态中心和时间六个入口，中部只显示统一的固定/运行应用列表。
- 中部应用列表超出可视高度时显示轻量悬浮上下导航；到达顶部或底部后相应箭头自动消失，点击按一个应用图标步长移动，鼠标滚轮仍可直接滚动。
- Focus 中心统一承载桌面收纳、任务、番茄钟、OKR、AI、最近使用模块和设置更新；状态中心集中音量、静音、网络、电池、通知、输入法、显示桌面和电源操作。
- 后台发现 GitHub 新版本后，紧凑栏 Focus 中心入口会显示更新状态点，Focus 中心顶部显示目标版本卡片；点击即可进入设置页一键安装，不再只依赖托盘气泡。
- 开始按钮左键打开 Windows 开始菜单，右键提供 Win+X 风格系统管理菜单，包括安装的应用、电源选项、事件查看器、系统、设备管理器、网络连接、磁盘管理、计算机管理、终端、管理员终端、任务管理器、设置和文件资源管理器。
- 第三方托盘溢出内容不再提供入口：FocusPanel 不读取 Explorer 私有 UI 数据，也不会为打开托盘而临时显示原生任务栏。

![六入口紧凑任务栏](docs/images/six-entry-taskbar.svg)

### 两个中心

Focus 中心只放 FocusPanel 的业务模块；状态中心只放设备状态、Windows 公开入口与任务栏恢复信息。两个中心与搜索、日历、设置、电源弹层互斥，按 `Esc` 可关闭。

![Focus 中心](docs/images/focus-center.svg)

![状态中心](docs/images/status-center.svg)

## 侧边任务栏完整替代与安全恢复

完整替代模式先使用微软公开的 `ABM_SETSTATE + ABS_AUTOHIDE` 让 Explorer 释放工作区，再一次性隐藏主屏 `Shell_TrayWnd`。守护器只读取并验证状态，不会周期性执行 `ShowWindow` 或 `SPI_SETWORKAREA`，因此不会与 Explorer 在“占用/释放工作区”之间来回争抢。Windows 若主动恢复任务栏或 Explorer 宿主失效，FocusPanel 会退出替代模式并恢复原设置，而不是反复隐藏造成闪烁。状态中心和设置页会显示停止原因；确认环境正常后，由用户点击“重新接管任务栏”手动启用。

首次启用前会显示安全说明。只有在侧边壳层、热区以及独立恢复守护进程都就绪后，FocusPanel 才会隐藏原任务栏；紧急快捷键注册失败时不会改变任务栏设置。

“随 Windows 启动”只在用户明确勾选后写入当前用户 Run 键。FocusPanel 会创建缺失的注册表键、正确引用带空格的程序路径，并在权限或路径失败时显示原因、回滚复选框；不会静默显示成已启用。

- 紧急恢复：`Ctrl+Alt+Shift+F10`
- 正常退出、未处理异常、数据库恢复重启：均恢复原任务栏可见性与 AppBar 设置
- 父进程异常退出：`--taskbar-watchdog` 守护模式负责恢复
- Explorer 重启或任务栏状态改变：停止本次替代并恢复原设置，避免可见性循环
- 恢复会话：`%LOCALAPPDATA%\FocusPanel\taskbar-session.json`

遇到异常时，先按紧急恢复快捷键。仍未恢复可重新启动 FocusPanel；启动阶段会检查并恢复遗留会话。程序永远不会结束 Explorer，也不会持续覆盖 Windows 工作区。完整替代后，Win+A、Win+N、Win+Space 等公开系统快捷入口继续可用；Explorer 的第三方托盘溢出内容属于私有壳层，FocusPanel 不读取其进程内存，也不能保证在原任务栏隐藏时完整复制。

![任务栏安全状态机](docs/images/taskbar-safety-flow.svg)

![开机启动写入与回滚](docs/images/startup-safety.svg)

## 桌面收纳与效率模块

- 新收纳文件始终保留在原桌面路径，不改名、不移动；FocusPanel 保存原始文件属性并追加 `Hidden + System`，取消收纳时精确恢复原属性。
- 从 Windows 桌面把文件或文件夹拖入收纳分区，会立即执行同一套隐藏事务；其他目录的项目不会被擅自移动。
- 普通“显示隐藏项目”开启时，已收纳图标仍会隐藏；如果同时开启“显示受保护的系统文件”，设置页会提示 Windows 无法保证图标不可见。
- 不再注入或持续修改 Explorer 的桌面列表；Explorer 刷新、重启和系统重启后按文件属性保持状态。
- 属性改变后会通知 Shell 更新项目并重新枚举桌面目录，避免图标只变成半透明却仍停留在桌面。
- 旧版 `.FocusPanel` 仓库继续兼容，升级时不自动移动旧文件。
- 不创建全屏桌面覆盖窗口；Windows 原生桌面保持可点击，文件收纳操作集中在侧边栏工作区完成。
- 桌面收纳工具栏、收纳盒、视图选项、新建、修复和重命名已全部使用共享 Fluent 控件；页面不再保留 Material 兼容控件或矩形/圆角双重外框。
- 任务模块使用统一 Fluent 表面，保留项目/子任务、列表和自定义字段语义；看板会按状态生成真实列，任务可前后移动、进入详情、进入子任务或删除。
- 任务详情使用 Windows 11 原生 DWM 毛玻璃窗口；关闭模块或切换数据上下文时同步解除订阅并关闭详情，避免重复窗口和失效对象残留。
- 番茄钟提供 25/45/60 分钟预设、准确剩余进度、暂停/继续、完成声音和托盘通知；悬浮计时器使用与主壳一致的 Windows 原生毛玻璃，不创建全屏覆盖层。
- OKR 使用本地优先工作流：未配置飞书也能创建、编辑和删除目标与关键结果；配置凭据后可双向同步，修改同步间隔会立即持久化。
- 关键结果支持正向或反向目标，进度限制在 `0–100%`；目标总进度按正权重计算，新增、更新和删除会与数据库中的总进度一次提交。
- AI 助手提供中文 Fluent 对话、快速提示、可取消请求和模型选择，使用 OpenAI Responses API；ChatGPT 订阅不等同于 API Key。
- API Key 通过 Windows DPAPI 加密，仅当前 Windows 用户可解密；界面不回显已保存 Key，也不会写入日志。
- AI 默认无权读取 FocusPanel 数据。用户主动开启授权后，只附带任务标题与状态、OKR 名称与进度和近 7 天专注统计；不读取文件内容、文件路径或凭据。
- AI 助手保持只读，不直接执行删除、关机、外部发送、付费或修改任务/OKR 等操作。
- 保留番茄钟完成会话统计、数据库备份与恢复。
- 新增固定应用持久化；`PinnedApps` 表由现有 `EnsureSchema()` 机制创建，不改动原业务表内容。

![桌面收纳流程](docs/images/desktop-organizer-flow.svg)

![纯 Fluent 桌面收纳界面](docs/images/desktop-organizer-fluent.svg)

![任务列表、真实看板与毛玻璃详情](docs/images/task-workspace.svg)

![本地优先 OKR 与飞书同步](docs/images/okr-local-sync.svg)

![番茄钟与原生毛玻璃悬浮计时器](docs/images/pomodoro-focus.svg)

![隐私优先的 AI 助手](docs/images/ai-assistant-workspace.svg)

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

数据库位于 `%APPDATA%\FocusPanel\focuspanel.db`。启动备份使用 SQLite 在线备份 API，因此 WAL 中已提交的数据也会进入独立备份文件。恢复时先安全退出当前实例并恢复原生任务栏，再由交接进程等待单实例锁释放；候选备份跨 AppData 与安装目录按时间排序并逐个执行 `PRAGMA quick_check`，最新文件损坏时会回退到更早的有效备份，全部失败则保持当前数据库不动。

![数据库安全备份与恢复](docs/images/database-restore-safety.svg)

## 安装包与一键更新

应用目标框架仍为 `.NET 7`。生成 Velopack 安装包时额外需要 `.NET 8 SDK`，它只用于运行打包工具：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\package-release.ps1 `
  -Version 0.9.41 `
  -Dotnet8Path dotnet `
  -PublishDotnetPath dotnet `
  -CleanPackages
```

安装包输出到 `artifacts/release/packages/`，其中包括：

- `FocusPanel-win-Setup.exe`：首次安装入口。
- `FocusPanel-0.9.41-full.nupkg`：完整更新包。
- `releases.win.json` 和 `RELEASES`：Velopack 更新清单。
- 后续版本生成的 delta 包：用于减少更新下载量。

安装版和 Velopack 便携版统一使用项目的 GitHub Releases，无需在每台设备配置更新地址。程序启动后会自动检查一次，之后每 6 小时最多检查一次；发现新版本时更新设置和托盘都会提示，但不会强制重启。

用户点击“一键检查并安装更新”后，FocusPanel 会显示更新说明、下载完整包或差分包、备份数据库、恢复原任务栏设置，然后重启安装。其他设备只需首次安装一次 `FocusPanel-win-Setup.exe`，后续版本均沿用这条更新链。

![一键更新流程](docs/images/one-click-update.svg)

源码直接运行的开发版不会原地覆盖自身，设置页会提示先安装 `Setup.exe`。

将生成的包上传为 GitHub Release 草稿：

```powershell
$env:GITHUB_TOKEN = "仅放在当前终端，不要写入仓库"
.\scripts\publish-github-release.ps1 `
  -Version 0.9.41 `
  -Dotnet8Path dotnet
```

确认后添加 `-Publish` 可正式发布。推送 `v*` 标签或手动运行“构建并发布 Windows 安装包”工作流，也会自动构建、测试、生成差分包并创建 Release。

当前仓库没有提供代码签名证书，因此本地生成的安装包会显示“未知发布者”。正式分发时应通过 `-SignParams` 传入 `signtool.exe` 参数，或在发布工作流中接入 Azure Trusted Signing；不要把证书密码写入仓库。
