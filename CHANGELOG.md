# Changelog

## v0.9.28 - 2026-07-28

- 修复右键菜单和 `Popup` 位于主窗口边界之外时，Panel 把鼠标误判为离开并在操作过程中自动收起的问题。
- 应用菜单、多窗口列表、开始按钮系统管理菜单和桌面文件菜单通过明确的 `Opened/Closed` 生命周期锁定壳层。
- 桌面收纳的视图选项、新建收纳盒和修复工具弹层接入同一交互锁；视图卸载时会释放尚未结束的锁，避免残留状态。
- 自动收起状态机同时识别 WPF 鼠标捕获；拖拽、按压或 `StaysOpen=False` 弹层交互结束前不会隐藏 Panel。
- 新增自动收起策略与 XAML 契约测试，覆盖临时交互阻止收起、菜单生命周期和三个桌面收纳弹层。

## v0.9.27 - 2026-07-28

- 统一应用栏改为按 Windows 应用身份增量同步；前台窗口、标题或运行状态变化时，只移动、插入、删除或替换实际变化的图标，不再清空整条集合。
- 未变化的应用图标和 WPF 容器保持原实例，应用栏滚动位置不再因窗口快照刷新而重置，降低前台切换时的视觉闪烁。
- 将每秒时钟刷新与系统状态、任务摘要刷新解耦：时间仍每秒更新，音量/网络/电池改为 2 秒刷新，SQLite 待办统计改为 30 秒刷新。
- 打开状态中心或日历时仍立即刷新对应数据，在降低空闲查询和 UI 线程负担的同时保持入口信息及时。
- 新增应用栏增量同步回归测试，覆盖无变化零通知、活动状态局部替换和排序使用 Move 而非 Reset。

## v0.9.26 - 2026-07-28

- 更新主源统一为 GitHub Releases，移除局域网 HTTP、UNC 共享目录、本机目录的来源选择及相关发布脚本。
- 其他设备首次安装后不再需要配置更新路径；启动后自动检查一次，运行期间每 6 小时低频检查一次。
- 自动检查不阻塞启动、不自动重启；发现新版本时更新设置页状态并显示 FocusPanel 托盘提示。
- 手动“一键检查并安装更新”继续展示版本说明、下载差分包或完整包、备份数据库、恢复任务栏并安全重启。
- 清理局域网更新边界和界面契约，更新 README、测试及发布说明。

## v0.9.25 - 2026-07-28

- 自动更新新增可持久化来源配置：保留 GitHub Releases 默认源，同时支持局域网 HTTP/HTTPS、Windows UNC 共享目录和本机绝对目录。
- 其他设备只需在设置页保存一次局域网来源，之后即可继续使用“一键检查并安装更新”；检查前会校验并保存当前来源。
- 新增 `publish-lan-update.ps1` 局域网发布脚本，先复制完整包、差分包等载荷，最后更新 Velopack 清单，避免客户端读到未复制完成的新版本。
- 设置页的软件更新卡片显示实际生效来源，并支持低高度滚动。
- 新增更新来源规范化、无效地址拒绝、UNC 路径和设置界面契约测试；自动测试不下载或安装真实更新。
- README 新增局域网多设备更新章节与流程图，发布说明和默认打包版本提升至 0.9.25。

## v0.9.24 - 2026-07-28

- README 改为图文说明，新增总览、六入口任务栏、Focus 中心、状态中心、任务栏安全恢复、桌面收纳和一键更新共七张结构图。
- 将 76px 紧凑栏收敛为开始、搜索、任务视图、Focus 中心、状态中心和时间六个固定入口；固定/运行应用继续使用中部唯一列表。
- Focus 中心统一承载五个业务模块、最近使用模块和设置更新；状态中心集中网络、音量、电池、输入法、通知、显示桌面与电源操作，并支持低高度滚动。
- 删除第三方托盘溢出入口、UI Automation 探测和选择器，不读取 Explorer 私有数据，也不为此临时显示原生任务栏。
- 任务栏停止事件改为类型化原因；失效后只安全恢复一次，并由用户在状态中心或设置页点击“重新接管任务栏”主动启用。
- 更新任务栏、系统状态和紧凑栏契约测试；自动测试继续使用替代边界，不操作真实任务栏。

## v0.9.24 - 2026-07-28

- 将 76px 紧凑栏收敛为开始、搜索、任务视图、Focus 中心、状态中心和时间六个固定入口；固定/运行应用继续使用中部唯一列表。
- Focus 中心统一承载五个业务模块、最近使用模块和设置更新；状态中心集中网络、音量、电池、输入法、通知、显示桌面与电源操作，并支持低高度滚动。
- 删除第三方托盘溢出入口、UI Automation 探测和选择器，不读取 Explorer 私有数据，也不为此临时显示原生任务栏。
- 任务栏停止事件改为类型化原因；失效后只安全恢复一次，并由用户在状态中心或设置页点击“重新接管任务栏”主动启用。
- 更新任务栏、系统状态和紧凑栏契约测试；自动测试继续使用替代边界，不操作真实任务栏。

## v0.9.23 - 2026-07-28 04:34 (UTC+8)

- 开始按钮改为左键打开 Windows 开始菜单、右键打开 Win+X 风格系统管理菜单，补齐安装的应用、电源选项、事件查看器、系统、设备管理器、网络连接、磁盘管理、计算机管理、终端、管理员终端、任务管理器、设置、文件资源管理器、搜索、运行、关机与桌面入口。
- 紧凑栏重新按应用、FocusPanel 工作区、Windows 核心操作和系统状态分层，并恢复独立任务视图入口，避免系统管理能力与业务模块混杂。
- 任务栏替代模式先通过 `ABM_SETSTATE + ABS_AUTOHIDE` 释放工作区，再一次性隐藏主屏 `Shell_TrayWnd`；不结束 Explorer、不处理副屏任务栏。
- 守护器改为只读验证，不再定时重写任务栏可见性或工作区。Windows 主动恢复任务栏或 Explorer 宿主变化时，替代模式安全停止并恢复原设置，避免重新出现占用区域反复跳动和闪烁。
- 保留独立 watchdog、紧急恢复快捷键和幂等恢复；自动测试继续使用替代 Win32 边界，不操作开发机真实任务栏。

## v0.9.22 - 2026-07-28 04:18 (UTC+8)

- 将固定应用与运行应用合并为单一 `TaskbarApps` 集合，同一应用不再在侧边任务栏出现两个图标。
- 新增应用身份解析边界：窗口显式 `System.AppUserModel.ID` 优先，其次读取进程 AUMID，最后使用规范化可执行路径；快捷方式优先读取自身 AppID，再通过 `IShellLink` 解析目标路径。
- 固定项继续使用数据库顺序；未固定运行项按首次出现顺序稳定排列，不再因前台状态变化跳动。无法读取身份的受保护进程按 PID 隔离，不按显示名称误合并。
- 统一点击和右键语义：未运行时启动、单窗口激活/最小化、多窗口显示文字列表，并支持启动新实例、固定/取消固定、逐个关闭或关闭全部窗口。
- 拖动未固定运行项时，根据 AUMID 或可执行路径创建固定项并持久化排序；取消固定后，运行窗口关闭前图标继续保留。
- 保持现有 `PinnedApps` 表不变，不迁移或删除用户数据。

## v0.9.21 - 2026-07-28 01:42 (UTC+8)

- 补齐侧边任务栏的 Windows 原生功能菜单，可直接唤起开始菜单、Windows 搜索、任务视图、小组件和运行对话框，不再用普通设置页替代这些操作。
- 多窗口应用不再默认只操作第一个窗口：左键直接显示文字窗口列表，支持逐个切换、正常发送 `WM_CLOSE` 关闭以及关闭全部窗口。
- 紧凑栏音量图标新增滚轮调节和右键静音；调高音量时会自动解除静音。
- 将 Windows 快捷键映射提取为可测试边界，自动测试覆盖所有新增快捷入口；继续保留 Explorer 托盘宿主，不复制第三方托盘图标的私有结构。

## v0.9.20 - 2026-07-27 20:09 (UTC+8)

- 根据 0.9.19 实机现场确认：1920×1080 主屏的 Windows 工作区持续回到 1920×1032，旧守护又定期写回 1080，根因是直接隐藏 `Shell_TrayWnd`、强写 `SPI_SETWORKAREA` 与 Explorer 自身工作区管理长期冲突。
- 任务栏兼容模式改用 Windows 官方 `ABM_SETSTATE + ABS_AUTOHIDE`；不再隐藏原生任务栏窗口、不再写工作区，最大化区域由 Explorer 自己维护，从机制上移除 1032/1080 闪烁循环。
- 原生 `Shell_TrayWnd` 保持存活，快捷设置、通知中心、输入法和托盘溢出继续拥有系统宿主；鼠标移到原任务栏边缘仍可临时唤出，多显示器遵循 Windows 的统一自动隐藏设置。
- 新恢复会话标记为原生自动隐藏模式，退出只恢复原 AppBar 状态；旧版会话仍兼容原工作区恢复。恢复结果验证失败时不再删除会话，watchdog 保留重试机会。
- 修复桌面收纳页拖拽靠近顶部后持续自动上滚：根控件补绑 `DragLeave`/`Unloaded`，所有 Drop 和拖拽结束路径统一停止自动滚动 timer。
- 更新任务栏状态机、失败恢复和界面契约测试；自动测试继续使用替代 Win32 边界，不改变测试机任务栏。

## v0.9.19 - 2026-07-27 18:43 (UTC+8)

- 根据本机 Windows 事件日志定位并修复 0.9.18 闪退：任务栏守护定时器等待跨进程状态锁超时后，未捕获的 `TimeoutException` 会从线程池终止整个进程。
- 任务栏守护检测增加单次执行门闩，前一次检测未完成时直接跳过后续 tick，不再并发争抢工作区和任务栏状态锁。
- 锁超时现在只跳过一次检测，不再触发进程崩溃或任务栏恢复/重新隐藏循环；其他守护故障会安全恢复 Windows 任务栏，并把替代模式关闭状态写回设置。
- 恢复守护进程增加有限重试；应用退出、崩溃恢复和紧急快捷键遇到短暂锁竞争时保留会话文件并重试，不因第二个异常丢失恢复机会。
- 新增锁超时隔离和并发守护回归测试；自动测试通过替代 Win32 边界执行，不隐藏测试机任务栏。

## v0.9.18 - 2026-07-27 18:02 (UTC+8)

- 主壳改为经过返回值验证的 Windows 11 DWM Desktop Acrylic：只有 `DWMSBT_TRANSIENTWINDOW` 与客户区扩展都成功时才启用轻薄 tint，失败、高对比度、关闭透明效果或远程桌面时自动回退为实色。
- 移除根页面重复半透明底板和桌面收纳页重阴影，统一使用系统强调色、10px 卡片和 8px 控件圆角；工具栏改为 Segoe Fluent Icons，并移除会全局隐藏桌面图标的入口。
- 自动整理改为逐项容错的无移动批处理：单个项目失败不再中断整批，公共桌面项目单独请求管理员授权，完成后强制刷新并报告成功与剩余数量。
- 视图选项新增“新增桌面项目自动按类型收纳”开关；自动流程与手动按钮共用同一分类策略、属性收纳事务和并发锁。
- 新增类型映射、逐项失败、授权继续、单层表面与原生 Acrylic 契约测试；自动测试不会操作真实桌面项目。

## v0.9.17 - 2026-07-27 16:46 (UTC+8)

- 紧凑侧栏新增 Windows 11 原生托盘溢出入口；通过可访问性调用 Explorer 的“显示隐藏的图标”按钮，第三方图标继续由原应用和 Explorer 托管，保留左键、右键菜单及状态更新。
- 常驻系统区拆分为输入语言/输入法、网络、音量、时钟日期、通知、显示桌面和设置，不再用一个含糊按钮代替整套任务栏能力。
- 固定应用、运行应用和 FocusPanel 工作区合并到中部可滚动区，底部系统按钮不会再因高 DPI 或窗口高度不足而顶掉设置入口。
- 新增托盘可访问节点选择测试；自动测试仍不隐藏开发机任务栏，也不使用跨进程 Explorer 内存读取。

## v0.9.16 - 2026-07-27 16:30 (UTC+8)

- 修复 0.9.15 守卫定时器每秒无条件调用 `SPI_SETWORKAREA` 和隐藏任务栏，造成 Explorer 工作区在 1032px 与 1080px 之间反复切换、界面持续闪烁的问题；现在只有工作区或任务栏实际偏离目标状态时才写入。
- 任务栏隐藏与恢复增加跨进程命名互斥，避免主进程守卫和独立恢复守护进程同时改写 `Shell_TrayWnd`；恢复顺序改为先恢复工作区与 AppBar 状态、最后显示任务栏，并验证显示结果。
- 新增稳定替代状态回归测试，连续执行守卫不得增加工作区写入或任务栏可见性写入次数；本机现场已使用安全停用标记恢复原工作区和原生任务栏，并临时关闭替代配置。
- 紧凑栏合并网络、音量、电池和输入法入口，减少低分辨率及高 DPI 下的固定按钮占用，为固定应用与运行窗口列表留出高度。
- 设置按钮恢复为独立直达入口，不再被“通知、设置与电源”上下文菜单替代；首次启用页只覆盖工作区，不再遮挡右侧紧凑任务栏。

## v0.9.15 - 2026-07-27 16:10 (UTC+8)

- 修复数据库中安全说明已确认、但 `Shell.ReplacementEnabled=False` 时界面不再解释为何 Windows 任务栏仍显示的问题；替代模式未启用时持续提供明确的安全启用页，仍然必须由用户点击后才隐藏。
- 任务栏接管增加最终状态验证：释放主屏工作区后同时使用 `ShowWindow` 与 `SetWindowPos(SWP_HIDEWINDOW)` 隐藏 `Shell_TrayWnd`，并再次检查窗口可见性；任一步失败都会恢复原工作区和任务栏，并展示具体失败点。
- 重建侧栏系统控制中心：Core Audio 默认设备在每次操作时重新解析，支持面板内音量滑块与静音；网络显示真实活动网卡、连接类型和 IPv4；电池、锁定、睡眠、通知、输入法和显示桌面均可直接操作。
- 网络、通知和输入法快捷键注入失败时不再跳转 `ms-settings:`；界面显示明确错误并提示对应系统快捷键，避免把“打开设置”伪装成任务栏功能。
- 时间入口改为面板内时钟与今日任务摘要，任务、通知中心和显示桌面可直接进入；新增任务栏隐藏失败自动回滚测试和系统控制 XAML 契约测试。

## v0.9.14 - 2026-07-27 16:20 (UTC+8)

- 移除会阻断 Desktop Acrylic 的 GDI `SetWindowRgn` 裁剪链路；主壳现在只由 DWM 同时提供唯一圆角轮廓与 `DWMSBT_TRANSIENTWINDOW` 背景模糊，不再叠加矩形/圆角外壳。
- 文本框模板改为由边框承载内边距，并将垂直内容对齐传递给 `PART_ContentHost`；使用 `MinHeight` 代替固定高度，避免局部较大内边距把文字裁成半截。
- 自动收回改用物理光标与 HWND 边界判断；计时器被鼠标、拖拽或输入焦点阻挡时会继续等待，失去前台后忽略残留输入焦点并收回，消除偶发永久展开。
- 删除伪快捷设置和伪日历浮层；紧凑坞新增真实网络、音量、Windows 通知/日历、输入法、电池与显示桌面入口，状态来自 Core Audio、网络接口和系统电源状态。
- Windows 快捷键注入失败时回退到对应系统设置 URI；显示桌面增加 Shell 回退，避免底部入口点击无效。
- 任务栏替代启用改为事务式：工作区切换失败会立即恢复任务栏及原工作区，不再留下半启用状态；守护进程和紧急恢复快捷键保持不变。

## v0.9.13 - 2026-07-27 15:45 (UTC+8)

- 修复 Windows 将用户桌面与公共桌面合并显示、但拖拽收纳只识别用户桌面的路径判断错误；现在同时扫描并监听两套桌面根目录。
- 收纳入口改为沿用拖入项目的真实完整路径，不再通过文件名拼接用户桌面路径，避免公共桌面快捷方式被误判。
- 公共桌面项目在明确说明“影响本机所有账户”并取得用户确认后，才为该次属性修改请求 UAC；提权助手严格限制为公共桌面根目录的直接子项。
- 拖放结果分别报告非桌面项目、管理员授权取消和属性写入失败，不再用“不是桌面或失败”混合提示。

## v0.9.12 - 2026-07-27 15:30 (UTC+8)

- 修复主窗口同时叠加 DWM 圆角、原生窗口 Region 圆角和 XAML 外壳圆角，导致边缘出现多层轮廓的问题；现在只由原生窗口 Region 定义唯一外形。
- 移除工作区整页的独立圆角底板、透明应用坞的无意义圆角以及全屏引导页的第二层外框，内容区不再与主壳形成“矩形套圆角”的嵌套边界。
- 移除全局隐式 `Border` 圆角，卡片和控件只在明确使用对应设计令牌时绘制圆角，避免普通布局容器被意外圆角化。

## v0.9.11 - 2026-07-27 15:04 (UTC+8)

- 修复 Desktop Acrylic 只设置 backdrop、未把 DWM frame 扩展进 WPF 客户区的问题；透明模式现在调用 `DwmExtendFrameIntoClientArea(-1)`，让背景模糊覆盖整个壳层。
- 顶层 HWND 增加随窗口动画和 DPI 更新的 24px 原生圆角 region，并继续关闭 DWM border，避免透明矩形角和系统描边形成黑边。
- 玻璃表面只保留轻量 tint，关闭透明效果、高对比度或远程桌面时仍自动回退为不透明表面。
- 新增统一圆角令牌：壳层 24px、卡片 16px、控件 12px；同步整理主壳、桌面收纳、任务、OKR 和任务详情的混用圆角。
- 取消收纳成功后清空原分区归属；拖回桌面的项目会立即从面板集合消失，不再以普通分类项残留。

## v0.9.10 - 2026-07-27 14:55 (UTC+8)

- 修复从面板拖回桌面时 `GetClassName` 使用不可封送泛型参数而抛出运行时异常的问题，改用 Win32 支持的 `StringBuilder` 并增加真实 API 回归测试。
- 壳层半透明遮罩由接近不透明调整为可感知的 Desktop Acrylic，移除 DWM 系统黑色边框，外壳仅保留原生圆角与阴影。
- 精简紧凑应用坞：统一 44px 操作尺寸，降低分割线噪声，将低频 OKR、AI、通知、设置和电源入口归入两个分组菜单。
- 右缘热区唤出紧凑坞后立即启动 900ms 进入宽限；未进入面板则自动隐藏，进入后在离开 350ms 后收回，输入焦点和文件拖拽期间仍保持展开。
- 展开工作区移除重复副标题和常驻退出按钮，降低嵌套卡片与描边密度。

## v0.9.9 - 2026-07-27 14:36 (UTC+8)

- 补齐桌面收纳的反向拖拽：已收纳项目从面板拖到 Windows 原生桌面并释放后，会执行取消收纳。
- 拖出判断只识别 `Progman`、`WorkerW`、`SHELLDLL_DefView` 和桌面 `SysListView32`，避免释放到其他应用或资源管理器窗口时误恢复。
- 复用原有幂等恢复事务，精确还原收纳前属性并通知 Explorer 重新枚举，不复制、不移动实体文件。

## v0.9.8 - 2026-07-27 14:24 (UTC+8)

### 桌面图标重新枚举修复

- 修复只发送 `SHCNE_ATTRIBUTES` 后，Explorer 将已收纳项目渲染为半透明图标但不从桌面视图移除的问题。
- 属性变更后追加受支持的 `SHCNE_UPDATEITEM` 与 `SHCNE_UPDATEDIR`，并使用 `SHCNF_FLUSHNOWAIT` 要求 Shell 及时重新枚举桌面目录。
- 不重启 Explorer、不修改“显示隐藏项目”设置，也不恢复旧版跨进程 ListView 操作。

## v0.9.7 - 2026-07-27 14:16 (UTC+8)

### 桌面拖拽收纳入口修复

- 修复外部 `FileDrop` 仍调用旧 `ImportFiles` 分类逻辑，导致从桌面拖进分区后没有执行隐藏属性事务的问题。
- 桌面根目录文件和文件夹拖入分区后，直接复用 `HideFileFromDesktop`，立即应用 `Hidden + System` 并保留原路径。
- 非桌面根目录项目不再移动到桌面；界面明确报告跳过数量，避免“无移动收纳”语义被旧导入逻辑破坏。
- 拖放事件改为等待收纳完成并阻止继续冒泡，新增桌面根目录、子目录和外部目录判定测试。

## v0.9.6 - 2026-07-27 14:07 (UTC+8)

### 无移动桌面收纳修复

- 新收纳项目不改名、不移动，改为保存完整原始属性后追加 `Hidden + System`；取消收纳时逐位恢复原属性。
- 删除通过跨进程读写 Explorer ListView 并发送 `LVM_DELETEITEM` 隐藏单个图标的非公开实现，改用正式文件属性与 Shell 属性刷新通知。
- `DesktopFilePreference` 新增托管路径、原始属性、稳定文件标识、收纳模式和操作状态，支持改名匹配、中断恢复和缺失项目保留。
- 旧 `.FocusPanel` 仓库继续兼容且不自动迁移；分区调整只更新数据库，不再移动收纳文件。
- 设置页新增“显示受保护的系统文件”检测与限制提示，恢复异常项目在收纳页面标记为“需恢复”。
- 新增属性组合、精确恢复、文件改名标识稳定性和 Explorer 私有操作清理测试。

## v0.9.5 - 2026-07-27 13:47 (UTC+8)

### 普通最大化窗口右缘呼出修复

- 修复隐藏 Windows 原生任务栏后，普通最大化窗口因覆盖整个显示器而被误判为无边框全屏，导致右缘指示条和鼠标热区同时停用的问题。
- 全屏策略新增最大化状态与标准窗口边框判断；保留标题栏或可调整边框的普通最大化窗口不再抑制热区，真正无边框、F11 和独占全屏仍遵循原有设置。
- 新增普通最大化覆盖整屏和无边框覆盖整屏的回归测试，并在真实发布版中验证旧固定路径已切换到当前版本。

## v0.9.4 - 2026-07-27 13:02 (UTC+8)

### 全界面资源回归测试与运行验证

- 修复任务详情窗口引用未定义的 `BooleanToVisibilityConverter`、番茄钟引用未定义的 `SecondaryHueMidBrush`；这两项此前只会在进入对应界面时暴露。
- 新增 XAML 资源契约测试，扫描全部主要页面的 `StaticResource`、`DynamicResource` 以及代码后置 `FindResource`/`TryFindResource`，阻止缺失资源再次进入发布包。
- 新增 `--ui-smoke-test` 独立进程自检模式，真实创建、测量和布局桌面收纳、任务、番茄钟、OKR、AI、任务详情、悬浮番茄钟和右缘指示窗口。
- 冷启动实际程序后验证：隐藏状态仅有 3px 点击穿透指示条可见；物理鼠标在主屏右缘停留后，76px 紧凑坞能够显示。

## v0.9.3 - 2026-07-27 12:34 (UTC+8)

### 桌面收纳主题资源修复

- 修复打开桌面收纳页面或执行分区拖拽时，代码后置仍查找已移除的 `OrganizerCardBrush`、`OrganizerCardBorderBrush` 而触发未处理异常的问题。
- 分区拖拽反馈改用现有 `FocusSurfaceSoftBrush`，恢复状态使用 `FocusSurfaceStrongBrush` 与 `FocusStrokeBrush`，与当前 Fluent 主题字典保持一致。
- 审查全部代码后置 `FindResource` 调用，确认剩余资源键均由当前主题或页面资源提供。

## v0.9.2 - 2026-07-27 12:19 (UTC+8)

### 右缘运行提示与热区可靠性

- 修复 `Progman`、`WorkerW` 等 Windows 桌面壳窗口覆盖整屏时被误判为全屏应用，导致右缘热区在桌面场景始终被禁用的问题。
- 新增主屏右缘 `3px` 白色运行指示条；窗口使用 `WS_EX_TRANSPARENT`、`WS_EX_NOACTIVATE` 和工具窗口样式，完全点击穿透且不抢焦点。
- 指示条在侧边栏展开、托盘隐藏或真正的全屏应用前台时自动隐藏，恢复可用后重新显示。
- 新增单实例互斥保护；重复启动 FocusPanel 时不再创建第二套壳层、热区和任务栏守护链路，而是唤出已有实例。
- 新增 Windows 壳窗口、当前进程、真实全屏应用和保留任务栏的最大化窗口判定测试。

## v0.9.1 - 2026-07-27 12:03 (UTC+8)

### 桌面阻塞与右缘呼出修复

- 永久移除会在开机时覆盖桌面并拦截点击的全屏 `DesktopOverlayWindow`；启动、退出和异常恢复仍会恢复 Explorer 原生桌面图标。
- 桌面收纳继续保留在侧边栏中，文件分区、隐藏、恢复、拖拽及现有 SQLite 数据不变。
- 将 `3px` WPF 悬停窗口替换为无窗口物理坐标监测器，以 `30ms` 周期检测主屏右缘最后 `12px`，停留 `100ms` 后呼出紧凑坞。
- 增加 `32px` 复位区域与触发锁，避免重复抖动；显示设置变化后重新计算主屏边界，全屏抑制和全局快捷键行为保持不变。
- 新增边缘区域、停留时间、快速划过、重复锁定、负坐标屏幕和纵向边界测试。
- 发布版本提升至 `0.9.1`，更新安装包和设置页一键更新所需的发布说明。

## v0.9.0 - 2026-07-27 04:20 (UTC+8)

### 安装包与一键更新

- 接入 Velopack 1.2.0，生成 Windows x64 自包含安装器、完整更新包、更新清单和后续差分包。
- 设置页新增“一键检查并安装更新”，支持 GitHub Releases 版本检查、更新说明、下载进度和自动重启安装。
- 安装更新前自动备份业务数据库、恢复主屏 Windows 任务栏和原生桌面图标，避免更新进程直接退出后留下替代状态。
- 开发运行版不会覆盖自身，会明确提示先安装 `Setup.exe`；安装版与便携版支持原地更新。
- 新增可复现的本地打包、GitHub Release 草稿/发布脚本，以及标签触发和手动触发的 Windows 发布工作流。
- 新增更新服务边界和开发运行版测试；自动测试总数增加至 12 项。

## v0.8.0 - 2026-07-27 01:06 (UTC+8)

### Windows 11 侧边任务栏

- 将仅桌面显示的旧抽屉重构为跨应用可用的右侧玻璃壳层，提供 `76px` 紧凑坞、约 `720px` 工作区和独立无激活热区。
- 新增应用目录搜索、固定与拖动排序、运行窗口跟踪、多窗口列表、激活/最小化/正常关闭操作。
- 新增时钟月历、今日任务、音量与静音、网络/电池状态、通知、输入法、显示桌面和电源入口。
- 使用 `SetWinEventHook` 跟踪窗口变化，并在独占或无边框全屏应用前台时默认停用鼠标热区。
- 新增 `Ctrl+Alt+Space` 全局主动唤出快捷键，以及可持久化的主题和全屏热区选项。
- 桌面收纳、任务、番茄钟、OKR、AI 和数据库恢复全部迁入新工作区，原业务数据库内容与语义保持不变。

### 任务栏安全与恢复

- 新增仅作用于主显示器的 `TaskbarController`，隐藏 `Shell_TrayWnd` 并临时释放主屏工作区，不结束 Explorer、不处理副屏任务栏。
- 新增同一可执行文件的 `--taskbar-watchdog` 守护模式；父进程异常退出时恢复任务栏与原工作区。
- 增加 `Ctrl+Alt+Shift+F10` 紧急恢复快捷键；快捷键注册失败时禁止隐藏任务栏。
- 启动、正常退出、未处理异常和数据库恢复重启均执行幂等恢复；Explorer 重启及显示设置变化后重新识别任务栏。
- 移除构造函数中的无条件开机自启动，改为首次安全引导中的明确选项。

### 视觉、数据与测试

- 移除 MaterialDesignThemes 与 MaterialDesignColors 包，改用自定义 Fluent 设计令牌、Segoe Fluent Icons、DWM 圆角和 Desktop Acrylic。
- 支持系统浅色/深色、高对比度及透明效果不可用时的安全降级，并增加 Per-Monitor V2 DPI manifest。
- 新增 `PinnedApps` 表和唯一索引，继续通过 `EnsureSchema()` 手工维护数据库结构。
- 新增可替换的任务栏、窗口、应用目录和系统状态边界，以及统一管理生命周期的 `ShellCoordinator`。
- 新增独立 xUnit 测试项目，覆盖应用去重、固定项排序、任务栏安全前置条件与幂等状态转换；测试不会调用真实的隐藏任务栏 API。

## Unreleased

### Desktop Organizer
- Added a custom desktop overlay so FocusPanel can collect desktop icons into panel partitions while the original files remain in the Desktop folder.
- Added free desktop icon placement with persisted `DesktopX` / `DesktopY` coordinates.
- Added drag support from desktop into the organizer panel and back out to the desktop without triggering Explorer's same-name file prompt.
- Added desktop icon sorting by name, type, date, and size.
- Added blank-desktop double-click to hide or restore the custom desktop icons.
- Expanded native-like desktop context menu actions: open, open with, show in Explorer, cut, copy, paste, rename, delete, refresh, properties, sorting, and desktop folder access.
- Added adjustable desktop icon size from the desktop right-click menu: small, medium, large, and extra large.
- Persisted desktop icon size in app configuration.
- Improved icon rendering quality by loading larger shell icons and using high-quality scaling.

### Desktop Storage Semantics
- Reworked collection behavior away from simply relying on Windows hidden-item visibility as the user-facing model.
- Kept files physically on the desktop while FocusPanel tracks whether they are collected into the panel.
- Updated desktop file scanning to include FocusPanel-managed collected files while still filtering unrelated system-hidden files.
- Added database schema support for desktop icon position and collection state.
- Fixed save failures caused by missing or null desktop file preference fields.

### UI
- Removed the Dashboard navigation entry from the main panel.
- Changed the default startup view to the desktop organizer page.
- Rebuilt the main shell with an Apple-inspired glassmorphism style: translucent surfaces, soft shadows, rounded panels, and lighter navigation.
- Restyled the desktop organizer page with glass cards, polished partition headers, clearer file hover/selection states, and localized Chinese UI copy.
- Improved the organizer toolbar, popups, empty state, rename dialog, and rescue tools presentation.
- Refined the Apple-inspired styling after review to reduce excessive transparency, gradients, and heavy shadows in favor of a cleaner Finder-like panel and organizer layout.
- Reworked the organizer layout after visual QA: removed the nested left action rail, moved organizer actions into a single top toolbar, unified icon sizing/color, and standardized partition card radius, borders, and drag feedback.
- Fixed right-edge drawer chrome: removed right-side rounded corners, removed outer shadow halo, disabled host-window DWM rounding, and made the drawer background fully opaque.

### Desktop-Only Panel Behavior
- Fixed a blank-desktop recovery issue by removing unconditional native desktop icon hiding during overlay load/refresh and synchronizing Explorer icon visibility only after the custom desktop overlay has visible icon data.
- Added startup, tray-hide, app-hide, exit, and unhandled-exception fail-safes that restore Explorer's native desktop icons.
- Hardened desktop ListView discovery by rebuilding the Explorer desktop host before giving up on native icon recovery.
- Fixed desktop-scene detection for minimized or invisible foreground windows and explicitly shows the hidden-startup main window before making the panel visible.
- Fixed Windows Show Desktop recovery by restoring minimized overlay and panel windows when the foreground guard detects the desktop scene.
- Added a minimized-state recovery hook so right-corner Show Desktop can pull FocusPanel back into the desktop scene after Windows minimizes it.
- Fixed a wallpaper-only Show Desktop state by restoring native Explorer icons whenever the custom desktop overlay is minimized.
- Added DWM cloaking detection so native desktop icons are restored if Windows Show Desktop hides the overlay without minimizing it.
- Replaced desktop-scene topmost recovery with non-activating Win32 window restore so Show Desktop can bring FocusPanel back without restoring other application windows.
- Tightened foreground detection and forced non-topmost demotion after desktop recovery so FocusPanel does not linger above newly focused application windows.
- Fixed a blank-desktop fail-safe issue by restoring Explorer's native desktop icons whenever the FocusPanel desktop overlay is hidden.
- Hardened desktop-only window layering so both the desktop overlay and panel stay hidden outside the desktop scene and the panel never uses topmost mode over other applications.
- Improved panel visibility behavior so it can stay available in the desktop scene while avoiding obstruction of normal foreground applications.
- Preserved drag-to-panel behavior while preventing the panel from disappearing during desktop collection workflows.
