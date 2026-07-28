# FocusPanel

FocusPanel 是面向 Windows 11 的右侧玻璃任务栏与桌面效率工作区。它保留桌面收纳、任务、番茄钟、OKR、AI 和 SQLite 数据，同时提供应用启动、运行窗口管理、系统状态与日期时间入口。

![FocusPanel 0.9.49 总览](docs/images/readme-overview.svg)

> 上图及下方模块图为 0.9.49 界面结构示意，用于说明信息层级和交互关系。实际毛玻璃、背景取样和亮暗色效果由 Windows 11 DWM、透明效果开关及当前壁纸共同决定。

## 新壳层

- 主屏右缘显示一条 `3px` 白色运行指示条；它完全点击穿透。最后 `12` 个物理像素由无窗口监测器检测，停留约 `100ms` 唤出 `76px` 紧凑应用坞。
- 点击搜索、桌面收纳、任务、番茄钟、OKR、AI 等入口后，工作区从右向左展开到约 `720px`。
- 离开约 `300ms` 自动收起；只有搜索框、密码框和下拉选择等输入控件持有焦点时保持展开，普通按钮或应用图标焦点不会锁住 Panel，`Esc` 可关闭。
- 应用右键菜单、多窗口列表和桌面收纳的视图/新建/修复弹层打开时会锁住 Panel；菜单关闭并且鼠标离开后才恢复自动收起。
- 桌面文件卡片只有移动距离超过 Windows 系统拖拽阈值后才开始拖动；靠近主内容区上下边缘时平滑滚动，移回中部、离开、取消、释放或完成放置后立即停止。
- 独占或无边框全屏应用前台时默认停用鼠标热区。
- 全局主动唤出：`Ctrl+Alt+Space`。
- 主动唤出后焦点落到搜索入口，可使用 Tab、Shift+Tab 或方向键循环浏览紧凑栏，Enter/Space 执行；应用按钮向读屏提供应用名称和窗口摘要，Shift+F10 或菜单键打开右键菜单。
- 键盘导航使用统一的 2px Fluent 圆角焦点环，轮廓只在键盘操作时出现，不给鼠标点击增加常驻边框；高对比度模式跟随 Windows 系统高亮色。
- 固定应用与运行应用按 Windows AppUserModelID 或可执行路径合并为单一任务栏图标；固定项保持用户顺序，未固定运行项保持本次运行中的稳定顺序。
- 搜索结果和统一任务栏共用同一个应用图标组件；Shell 无法读取图标时显示带应用名称首字符的 Fluent 圆角占位，不再留下无法识别的空白按钮。中文、英文、数字和特殊字符名称均有稳定降级。
- 搜索结果的列表、启动按钮和标题全部显式使用动态 `FocusTextBrush`；键盘选中使用现有 `FocusSurfaceSoftBrush`，不再落回 WPF 的系统蓝色选择背景，因此深色、浅色及系统强调色变化下都保持可读。
- 开始菜单快捷方式、`shell:AppsFolder` 和应用身份解析在可取消的 STA 后台线程构建；Panel 壳层不再等待完整目录扫描才响应鼠标与键盘。
- 搜索和固定项会先显示名称与首字符占位，再由单一后台队列按需加载真实图标；Shell 图标提供器响应缓慢时不会卡住搜索输入。索引期间显示“正在载入应用目录”，完成但无匹配项时显示明确空状态。
- 打开搜索后会立即聚焦并全选搜索框；无需离开键盘即可用上下方向键选择结果、回车启动，`Esc` 关闭后焦点返回紧凑栏搜索入口。应用目录在后台补全时会按稳定身份保留当前选择，不会把光标跳回第一项。
- 窗口前台状态改变时按应用身份增量更新图标，只替换真正变化的项目，不再清空并重建整条应用栏，因此滚动位置和未变化图标保持稳定。
- 窗口跟踪覆盖 `CREATE / DESTROY / SHOW / HIDE / NAMECHANGE / FOREGROUND` 完整生命周期；新应用窗口创建后及时进入统一应用栏，最后窗口销毁后及时移除，不再依赖下一次偶然的前台或标题事件纠正陈旧图标。
- WinEvent 只接收 `OBJID_WINDOW` 窗口本体并跳过 FocusPanel 自身进程；按钮、菜单和 Panel 显隐不会触发无意义的完整窗口重扫，短时间重复通知继续合并为一次刷新。
- Panel 隐藏后暂停完整窗口枚举、时钟、系统状态和任务摘要刷新；右缘热区、全屏抑制、安全恢复和 GitHub 更新检查继续运行。再次唤出时先刷新窗口快照和当前时间，状态中心与日历在打开时即时刷新。
- 未运行的固定项点击启动；单窗口应用点击激活/最小化；多窗口应用左键展开一层文字窗口列表，点击标题即可直接切换，不再进入二级子菜单。右键菜单继续提供启动新实例、固定、逐窗口关闭和关闭全部窗口。
- 应用图标支持 Windows 任务栏常用的新实例手势：`Shift+左键` 或鼠标中键直接启动新实例；没有可靠启动目标的受保护窗口不会显示或执行该动作。工具提示和读屏帮助会同步说明当前可用操作。
- 多窗口列表精确标记当前前台窗口；同一应用内部切换窗口也会增量更新标记。标题超过 340px 时视觉省略，读屏名称仍保留完整标题并说明“当前窗口”。
- 开始按钮右键、应用管理、多窗口列表、关机子菜单和托盘菜单统一使用单层 Fluent 圆角菜单；静态 XAML 项与运行时创建项自动继承同一主题，深色模式不再落回系统浅色背景、蓝色高亮或直角模板。
- 菜单悬停、键盘焦点、当前窗口勾选、禁用状态和子菜单箭头全部使用动态主题令牌；子菜单保持文字省略与完整辅助功能名称，不增加黑色投影边框。
- 全部工具提示使用单层 Fluent 圆角表面，背景、文字和描边跟随动态深浅色主题；不再复用 WPF 默认矩形模板、硬编码深色底或系统黑色阴影。应用状态与新实例手势等多行提示也保持同一信息层级。
- 应用图标左侧使用任务栏式圆角状态条：后台运行显示 `4×12px` 短条，当前活动扩展为 `4×24px` 长条并使用单一柔和背景，固定但未运行的应用不显示状态条。状态层完全点击穿透，不会吞掉图标左缘操作。
- 工具提示和读屏名称明确区分“已固定 · 未运行”“正在运行 · 1 个窗口”“正在使用 · 2 个窗口”；辅助操作提示会按启动、单窗口切换/最小化和多窗口列表自动变化。
- 应用启动会区分普通可执行文件、快捷方式、Shell 路径和 `shell:AppsFolder` 返回的 AUMID；商店应用不再把 AUMID 错当文件名。应用已卸载、固定路径移动或 Shell 拒绝启动时不会让 Panel 闪退，而是在状态中心说明原因并引导重新固定。
- 窗口切换、最小化和关闭会检查 Win32 的真实结果；Windows 拒绝前台切换、窗口已失效或关闭消息未能入队时，状态中心会显示对应窗口和原因，不再表现为点击后毫无反应。
- 固定、取消固定和拖动排序会确认 SQLite 提交结果；数据库短暂锁定或写入失败不会冲击 UI 线程，也不会把未保存的顺序伪装成成功。
- 运行项可通过右键固定；拖动未固定运行项会自动创建固定项并保存排序，取消固定后只要窗口仍在就继续显示。
- 紧凑栏固定为开始、搜索、任务视图、Focus 中心、状态中心和时间六个入口，中部只显示统一的固定/运行应用列表。
- 中部应用列表超出可视高度时显示轻量悬浮上下导航；到达顶部或底部后相应箭头自动消失，点击按一个应用图标步长移动，鼠标滚轮仍可直接滚动。
- Focus 中心统一承载桌面收纳、任务、番茄钟、OKR、AI、最近使用模块和设置更新；状态中心集中音量、静音、网络、电池、通知、输入法、显示桌面和电源操作。
- 状态中心的快捷设置、通知、输入法、显示桌面、锁定、睡眠与电源操作均返回明确结果；成功后关闭 FocusPanel 弹层以免遮挡 Windows 界面，系统拒绝或启动失败时自动回到状态中心显示可操作的替代方式，不再静默失败或让异常冲击 UI 线程。
- 音量和静音使用一次性 Core Audio 快照区分“真实 0%”与“没有默认输出设备”；端点切换或写入失败时滑块回到最后确认值并显示原因。无输出设备时控件自动停用，设备恢复后由状态刷新重新启用；紧凑栏滚轮只有在音量写入成功后才会取消静音。
- 紧凑栏状态入口和状态中心静音按钮会根据当前音量显示 Segoe Fluent 音量、静音或设备不可用图标；工具提示和读屏名称同步显示百分比。Panel 从隐藏状态重新唤出时立即刷新一次，不需要先打开状态中心，也不会在隐藏期间常驻轮询。
- 电池状态通过单次快照同步读取是否存在、百分比和充电状态；状态中心按 10% 档位显示 Segoe Fluent Battery/BatteryCharging 图标和“充电中”文本。紧凑栏状态入口的一个提示整合网络、音量与电池，不增加额外按钮或破坏六入口布局。
- 网络状态通过单次快照生成可用性、连接类型、接口名称和详情；状态中心按无线、有线、其他连接显示 WiFi、Ethernet 或 Globe 图标，离线时显示 Error。接口切换或枚举失败不会再把不同采样时刻的在线/离线文案拼在一起，也不读取 Explorer 私有托盘数据。
- 输入法状态通过一次前台键盘布局读取生成语言和输入法简称；状态中心入口显示“输入法 · 中 / 拼”“输入法 · EN”等，工具提示提供完整状态。点击继续使用 Win+Space，不读取 Explorer 私有托盘结构，也不擅自修改输入法设置。
- 时间入口提供周一开头的 6 周月历，可切换月份、回到今天或直接选择日期；完成过番茄钟的日期显示专注圆点，底部汇总所选日期的专注次数和分钟数。
- 后台发现 GitHub 新版本后，紧凑栏 Focus 中心入口会显示更新状态点，Focus 中心顶部显示目标版本卡片；点击即可进入设置页一键安装，不再只依赖托盘气泡。
- 开始按钮左键打开 Windows 开始菜单，右键提供 Win+X 风格系统管理菜单，包括安装的应用、电源选项、事件查看器、系统、设备管理器、网络连接、磁盘管理、计算机管理、终端、管理员终端、任务管理器、设置和文件资源管理器。
- 第三方托盘溢出内容不再提供入口：FocusPanel 不读取 Explorer 私有 UI 数据，也不会为打开托盘而临时显示原生任务栏。

![六入口紧凑任务栏](docs/images/six-entry-taskbar.svg)

![应用图标加载与稳定降级](docs/images/app-icon-fallback.svg)

![非阻塞应用目录与图标队列](docs/images/app-catalog-background.svg)

![统一应用栏可靠启动链路](docs/images/app-launch-safety.svg)

![任务栏操作结果与失败反馈](docs/images/taskbar-action-feedback.svg)

![运行应用窗口生命周期跟踪](docs/images/window-lifecycle-tracking.svg)

![多窗口应用一层直接列表](docs/images/multi-window-direct-list.svg)

![统一应用栏运行与活动状态](docs/images/taskbar-app-state-feedback.svg)

![应用搜索完整键盘路径](docs/images/app-search-keyboard-flow.svg)

![统一 Fluent 任务栏菜单](docs/images/fluent-context-menu-system.svg)

![统一应用栏鼠标操作](docs/images/taskbar-app-mouse-actions.svg)

![统一 Fluent 工具提示](docs/images/fluent-tooltip-system.svg)

### 两个中心

Focus 中心只放 FocusPanel 的业务模块；状态中心只放设备状态、Windows 公开入口与任务栏恢复信息。两个中心与搜索、日历、设置、电源弹层互斥，按 `Esc` 可关闭。

Focus 中心顶部提供“今日概览”：以只读方式汇总未完成任务、今日专注、进行中 OKR 和已收纳桌面项目，并显示可以立即推进的任务与目标。概览不会改变业务数据，打开或手动刷新时才读取最新本地快照。

![Focus 中心](docs/images/focus-center.svg)

![状态中心](docs/images/status-center.svg)

![今日概览与快速行动](docs/images/dashboard-today.svg)

### 月历与专注回顾

时间弹层只承载日期、专注历史和两个高频动作，不再重复堆放通知、桌面等状态中心入口。月历固定生成 42 个日期，避免不同月份打开时高度跳动；今天使用细描边，选中日期使用单一强调表面，相邻月份降低透明度。专注圆点与摘要直接读取现有 `PomodoroSessions`，不会创建新业务表，也不会修改历史记录。

![月历与每日专注回顾](docs/images/calendar-focus-history.svg)

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
- 桌面文件变化后按收纳盒身份和文件完整路径差量同步：未变化的收纳盒、卡片和展开状态保留原对象，文件移动只更新对应集合，列表不会因 `Clear()` 重建而自动跳回顶部。
- 左右列排序使用 `Move` 更新而非清空重加；同路径文件刷新保留选中状态。数据库瞬时读取失败时保留最后一次有效界面，不把暂时错误显示成“所有收纳盒消失”。
- 桌面根目录监视器会把 500ms 内重复的创建、写入、删除和重命名通知按路径合并，只读取真正变化的项目；旧仓库变化或监视器缓冲区异常时才安全回退全量扫描。
- 外部重命名、文件大小和类型变化继续复用原卡片并更新派生文本；已收纳文件外部丢失时保留“需要恢复”入口。退出应用会释放全部桌面监视器和计时器，不留下重复刷新源。
- 网格和列表模式使用与外层统一滚动条协作的视口虚拟化面板，只创建当前可见行和前后缓冲行的文件卡片；完整集合、滚动范围、双列收纳盒和业务排序不变。
- 文件卡片容器在滚动时回收复用，图标缩放、窗口宽度、收纳盒折叠和集合变化会重新计算实现区间。1000 项真实 WPF 冒烟中，首屏、中段、集合变化和返回顶部仅存在 `9/12/12/9` 个视觉容器。
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

![桌面收纳差量刷新与滚动稳定](docs/images/organizer-stable-refresh.svg)

![桌面文件监视路径级增量刷新](docs/images/organizer-path-refresh.svg)

![大量桌面文件视口虚拟化](docs/images/organizer-viewport-virtualization.svg)

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

普通界面设置位于 `%APPDATA%\FocusPanel\settings.json`，与 Velopack 的只读安装目录和版本目录分离。0.9.43 首次启动会在新文件不存在时读取旧版安装目录中的 `settings.json`，原子复制到新位置，并保留旧文件作为回退；用户自定义图片目录不会被重写，只有旧版默认的安装目录 `Images` 会迁移到 `%APPDATA%\FocusPanel\Images`。设置文件损坏时应用使用安全默认值，保存失败会留下可诊断错误，不会先删除已有配置。

![设置迁移与原子保存](docs/images/settings-migration-safety.svg)

![数据库安全备份与恢复](docs/images/database-restore-safety.svg)

## 安装包与一键更新

应用目标框架仍为 `.NET 7`。生成 Velopack 安装包时额外需要 `.NET 8 SDK`，它只用于运行打包工具：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\package-release.ps1 `
  -Version 0.9.64 `
  -Dotnet8Path dotnet `
  -PublishDotnetPath dotnet `
  -CleanPackages
```

安装包输出到 `artifacts/release/packages/`，其中包括：

- `FocusPanel-win-Setup.exe`：首次安装入口。
- `FocusPanel-0.9.64-full.nupkg`：完整更新包。
- `releases.win.json` 和 `RELEASES`：Velopack 更新清单。
- 后续版本生成的 delta 包：用于减少更新下载量。

安装版和 Velopack 便携版统一使用项目的公开 [GitHub Releases](https://github.com/SakalioLabs/FocusPanel/releases)，无需在每台设备配置更新地址或访问令牌。客户端直接读取 GitHub Latest Release 的静态 `releases.win.json` 和包资产，不调用匿名 Releases API，因此不会因共享 IP 的 API 次数耗尽而收到 403。程序启动后会自动检查一次，之后每 6 小时最多检查一次；发现新版本时更新设置和托盘都会提示，但不会强制重启。

正式发布流程会把当前版本显式设为 GitHub Latest，并回读验证 `releases.win.json`、`RELEASES` 和完整更新包。验证失败会中止发布，因此另一台设备只要安装过一次 `Setup.exe`，以后即可在设置页直接完成检查、下载、安装和重启。设置页同时保留“打开官方下载页”按钮；网络策略、代理或临时服务异常时可以直接下载 `FocusPanel-win-Setup.exe` 覆盖升级，业务数据库和 `%APPDATA%` 设置不会被安装包删除。

![GitHub 静态清单一键更新与手动兜底](docs/images/github-static-update-flow.svg)

用户点击“一键检查并安装更新”后，FocusPanel 会显示更新说明、下载完整包或差分包、备份数据库、恢复原任务栏设置，然后重启安装。其他设备只需首次安装一次 `FocusPanel-win-Setup.exe`，后续版本均沿用这条更新链。

![一键更新流程](docs/images/one-click-update.svg)

源码直接运行的开发版不会原地覆盖自身，设置页会提示先安装 `Setup.exe`。

将生成的包上传为 GitHub Release 草稿：

```powershell
$env:GITHUB_TOKEN = "仅放在当前终端，不要写入仓库"
.\scripts\publish-github-release.ps1 `
  -Version 0.9.64 `
  -Dotnet8Path dotnet
```

确认后添加 `-Publish` 可正式发布。推送 `v*` 标签或手动运行“构建并发布 Windows 安装包”工作流，也会自动构建、测试、生成差分包并创建 Release。

当前仓库没有提供代码签名证书，因此本地生成的安装包会显示“未知发布者”。正式分发时应通过 `-SignParams` 传入 `signtool.exe` 参数，或在发布工作流中接入 Azure Trusted Signing；不要把证书密码写入仓库。
