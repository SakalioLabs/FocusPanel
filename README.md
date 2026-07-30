# FocusPanel

FocusPanel 是面向 Windows 11 的右侧玻璃任务栏与桌面效率工作区。它保留桌面收纳、任务、番茄钟、OKR、AI 和 SQLite 数据，同时提供应用启动、运行窗口管理、系统状态与日期时间入口。

![FocusPanel 0.9.49 总览](docs/images/readme-overview.svg)

> 上图及下方模块图为 0.9.49 界面结构示意，用于说明信息层级和交互关系。实际毛玻璃、背景取样和亮暗色效果由 Windows 11 DWM、透明效果开关及当前壁纸共同决定。

## 新壳层

- 主屏右缘显示一条 `3px` 白色运行指示条；它完全点击穿透。最后 `12` 个物理像素由无窗口监测器检测，停留约 `100ms` 唤出 `76px` 紧凑应用坞。
- 多显示器可在设置中选择“最右侧屏幕”或“Windows 主屏”。默认最右侧可避开横向相邻屏幕的接缝；切换后 Panel、12px 热区和 3px 指示条使用同一物理边界立即整体迁移，选择会写入现有 Shell 偏好并在下次启动恢复。
- 右缘监测使用独立后台 `PeriodicTimer` 保持约 `30ms` 物理坐标采样，不再让 WPF Dispatcher 承担高频鼠标与全屏窗口检查；只有热区可用状态变化或达到呼出条件时才回到界面线程，因此工作区加载、布局和展开动画繁忙时仍能准确响应。停止或显示器变化后的旧采样会按代际丢弃，不会迟到误展开。
- 点击搜索、桌面收纳、任务、番茄钟、OKR、AI 等入口后，工作区从右向左展开到约 `720px`。
- 离开约 `300ms` 自动收起；只有搜索框、密码框和下拉选择等输入控件持有焦点时保持展开，普通按钮或应用图标焦点不会锁住 Panel，`Esc` 可关闭。
- 工作区标题栏可点击图钉临时“固定展开”，查看任务、日历、OKR 或对照资料时即使鼠标离开也不会自动收起。固定态使用柔和强调色；再次点击、手动收回、`Esc`、托盘隐藏或退出都会解除，不写入数据库或下次启动设置。
- 应用右键菜单、多窗口列表、下拉选择和桌面收纳的视图/新建/修复弹层打开时会锁住 Panel；即使 ComboBox Popup 使用独立窗口，展开期间也不会被误判为离开，弹层关闭且鼠标离开后才恢复自动收起。
- 桌面文件卡片只有移动距离超过 Windows 系统拖拽阈值后才开始拖动；靠近主内容区上下边缘时平滑滚动，移回中部、离开、取消、释放或完成放置后立即停止。
- 桌面拖入、分区收纳和拖出恢复统一经过受观察的异步交互边界：文件属性操作完成前持续持有 Panel，异常会转为可恢复提示，任何成功、失败或提示异常路径都会释放拖拽与自动收起锁。
- 从 Explorer 发起的外部拖拽与 Panel 自己发起的拖出使用独立会话语义；外部拖拽取消、离开或落下后立即复位，内部拖拽经过子控件时不会被重复 `DragEnter` 误判为外部操作。
- 独占或无边框全屏应用前台时默认停用鼠标热区。
- 全局主动唤出优先使用 `Ctrl+Alt+Space`；若被其他程序占用，自动回退 `Ctrl+Shift+Space`。设置页显示本次会话实际注册成功的组合，两者都不可用时仍可使用右缘热区。
- 在设置中明确开启“九槽位全局快速键”且任务栏接管成功后，`Ctrl+Alt+1…9` 可从任意应用直接启动或切换统一应用栏前九个槽位，追加 `Shift` 则启动新实例；此选项默认关闭并写入 Shell 偏好，避免国际键盘的 AltGr 输入被意外占用。顺序始终按 Panel 当前从上到下的固定项与稳定运行项计算，图标工具提示和读屏名称会说明实际槽位。每个组合独立通过公开 `RegisterHotKey` 注册，冲突项留给原程序，设置页显示本次会话真实可用范围；关闭选项、恢复原任务栏、接管异常、更新或退出时立即幂等注销。FocusPanel 不用键盘钩子强抢 Windows 保留的 `Win+数字` 组合，依据见 [RegisterHotKey 文档](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)。
- 主动唤出后焦点落到搜索入口，可使用 Tab、Shift+Tab 或方向键循环浏览紧凑栏，Enter/Space 执行；应用按钮向读屏提供应用名称和窗口摘要，Shift+F10 或菜单键打开右键菜单。
- 搜索、Focus 中心、状态中心、月历、设置和电源打开后都会把焦点送入首个有效内容；按 `Esc` 关闭时再返回原紧凑栏入口。快速切换或自动收起期间的迟到焦点请求会检查窗口生命周期，不会跳到已隐藏控件。

![主动唤出快捷键回退与真实状态](docs/images/summon-hotkey-fallback.svg)

![九槽位快速应用快捷键与安全生命周期](docs/images/taskbar-slot-hotkeys.svg)

![工作区固定展开与自动收起生命周期](docs/images/workspace-pin-lifecycle.svg)
- 键盘导航使用统一的 2px Fluent 圆角焦点环，轮廓只在键盘操作时出现，不给鼠标点击增加常驻边框；高对比度模式跟随 Windows 系统高亮色。
- 固定应用与运行应用按 Windows AppUserModelID 或可执行路径合并为单一任务栏图标；固定项保持用户顺序，未固定运行项保持本次运行中的稳定顺序。
- 应用图标右键菜单会按需读取 Windows 公开 Jump List 的“最近项目”，最多直接铺开 8 项，不再进入多级子菜单；点击文件时优先复用该应用的可靠启动目标，打包应用则交给 Windows 文件关联安全打开。查询只在菜单打开期间运行于可取消的 STA 后台线程，关闭菜单、更新或退出会丢弃迟到结果；没有明确 AUMID 的应用不按名称猜测，也不读取 Explorer 私有数据。接口依据见 [IApplicationDocumentLists::GetList](https://learn.microsoft.com/zh-cn/windows/win32/api/shobjidl_core/nf-shobjidl_core-iapplicationdocumentlists-getlist)。
- 运行应用图标停留约 `420ms` 会打开无激活 DWM 实时窗口预览：画面由 Windows 桌面合成器持续提供，不截屏、不轮询，也不会抢走当前输入焦点；点击画面直接切换，底部标题栏可关闭窗口并标记当前窗口。目标显示器按物理高度和 DPI 自动容纳 1–4 张预览，其余窗口继续通过左键完整文字列表访问。DWM 关闭、远程桌面、受保护窗口或原生注册失败时自动退回现有文字窗口列表。
- 多窗口应用无需打开列表即可在图标上滚轮切换：向下进入下一个窗口，向上返回上一个窗口，首尾自动环绕。连续滚动会记住刚刚选中的窗口，不等待 WinEvent 快照回写，也用 90ms 节流抑制高分辨率触控板抖动；单窗口应用不会吞掉应用栏滚动。悬停窗口列表中可用中键或 `Delete` 直接关闭目标窗口，仍只发送正常 `WM_CLOSE`。
- 搜索结果和统一任务栏共用同一个应用图标组件；Shell 无法读取图标时显示带应用名称首字符的 Fluent 圆角占位，不再留下无法识别的空白按钮。中文、英文、数字和特殊字符名称均有稳定降级。
- 应用搜索按“完整名称 → 可执行文件名 → 名称前缀 → 缩写 → 多词前缀 → 包含”分级匹配；`vsc` 可命中 Visual Studio Code，`studio co` 可按词查找，标点、大小写和重音符号会被统一规范化。固定状态只在同一匹配等级内作为次级排序，不会再把固定但弱相关的结果压到精确结果前面；不做易误启动的无限模糊纠错。
- 搜索现在把应用、已打开窗口和 Windows 系统命令放在同一条结果列表中：输入文档、网页或会话的窗口标题即可直接切换；输入“任务管理器”“设备管理器”“硬盘分区”“admin terminal”或 `taskmgr`、`devmgmt` 等命令名即可直接执行管理工具；输入“运行”“快捷设置”“通知中心”“切换输入法”“任务视图”“小组件”“显示桌面”或 `win r`、`win a`、`win tab` 等快捷键别名可直接执行 Shell 动作。新增“上一首 / 播放暂停 / 下一首”以及 `previous track / play pause / next track` 媒体命令，共计 25 个系统入口；锁定、电源和关闭桌面等高影响动作不会进入无确认搜索。精确应用名仍优先启动应用，空查询保持原有固定应用顺序，不混入窗口或系统命令；窗口快照更新时按稳定键保留键盘选中项。该入口不扩展为网页或磁盘全文搜索。
- 搜索框也可直接安全计算：支持括号、`+ - * / %`、小数、负数以及 `× / ÷ / （ ）`，结果固定显示在首位，点击或按 Enter 即复制。解析器不执行脚本或动态代码，最多接受 128 字符和 16 层括号；纯数字、路径、应用名、除零、指数语法和错误表达式不会伪装成结果。剪贴板被占用时会异步重试三次，持续失败才进入状态中心说明，不冻结搜索输入。
- 统一搜索可以直接控制默认输出音量：输入 `音量 35` / `volume 35` 精确设置百分比，输入 `音量 +10`、`音量降低 5` 或 `volume up 10` 相对调整，输入“静音 / 取消静音”直接设定目标状态。结果固定排在普通应用前，点击或按 Enter 后进入既有串行 Core Audio 控制器，无需先展开状态中心；正音量会同时取消静音，结果按 `0–100%` 夹取，设备切换或写入失败仍由状态中心给出可恢复说明。相对调整只在当前默认设备音量已确认时执行，不会在启动读取完成前把未知音量误当成 0%；解析只接受完整、明确的命令，`音量`、`音量 101`、小数、文件名和尾随文本不会被误执行。
- 状态中心新增内置显示器亮度滑块，不再为了调光先打开 Win+A；统一搜索同步支持 `亮度 35`、`亮度 +10`、`亮度降低 5`、`brightness 60` 和 `brightness down 10`。连续拖动只保留最新目标值并在后台串行写入，旧结果不能覆盖新的滑块位置；读取与写入使用 Windows 公开的 `WmiMonitorBrightness` / `WmiMonitorBrightnessMethods`，只控制系统实际公开的内置显示设备。外接显示器、远程桌面或驱动未公开亮度能力时，控件会禁用并说明原因，仍可点击“快捷设置”，不会调用 DDC 私有协议或假装成功。

![状态中心与统一搜索直接控制内置屏亮度](docs/images/direct-brightness-control.svg)
- 状态中心内置应用级音量混音器：直接列出默认输出设备上尚未过期的音频会话，显示正在播放状态，并为每个应用提供独立音量与静音，不必再从任务栏打开快捷设置、进入声音混合器。活动会话优先、名称与会话实例标识稳定，最多显示 12 项并由状态中心整体滚动；系统声音使用明确标签，受保护进程无法读取产品名时降级为进程名，不按窗口标题猜测身份。滑块连续变化按会话合并最后值，不同应用严格串行；写入期间暂停旧快照覆盖，当前会话结束或设备切换时回滚到最后确认值并刷新列表。实现仅使用 Windows 公开的 `IAudioSessionManager2`、`IAudioSessionEnumerator`、`IAudioSessionControl2` 和 `ISimpleAudioVolume`，不读取播放器私有数据，也不影响独占模式音频流。

![状态中心应用级音量混音器](docs/images/application-volume-mixer.svg)

- 状态中心新增 Wi‑Fi 与蓝牙直接开关：打开状态中心即可按当前真实 Radio 状态开启或关闭，不再先展开 Win+A。第一次主动切换时才请求 Windows 无线控制权限，并在本次会话复用结果；系统仅接受请求还不算成功，Panel 会重新读取最终硬件状态后再更新按钮。飞行模式、硬件开关、驱动或组织策略禁用、权限拒绝和设备移除都会显示具体原因，同时保留“快捷设置”作为公开系统入口。实现使用 Windows 公开 [`Radio.GetRadiosAsync`](https://learn.microsoft.com/en-us/uwp/api/windows.devices.radios.radio.getradiosasync)、[`Radio.RequestAccessAsync`](https://learn.microsoft.com/en-us/uwp/api/windows.devices.radios.radio.requestaccessasync) 与 [`Radio.SetStateAsync`](https://learn.microsoft.com/en-us/uwp/api/windows.devices.radios.radio.setstateasync)，不读取 Explorer 托盘私有数据。

![状态中心直接切换 Wi-Fi 与蓝牙](docs/images/direct-radio-controls.svg)

- 状态中心可直接查找附近 Wi‑Fi 并连接 Windows 已保存的网络：用户点击“查找网络”后才调用公开 Native Wi‑Fi API，等待扫描完成通知并按“当前连接优先、信号强度、名称”稳定排列，最多显示 10 项，避免网络较多时淹没其他状态控制。点击已保存网络后调用 `WlanConnect`，但只有重新读取到真实 Connected 标记才显示成功；网络离开范围、配置删除、Radio 关闭、WLAN AutoConfig 停止或连接超时都会保留 Panel 并说明原因。未保存网络不会读取或保存密码，而是明确转入 Windows 快捷设置完成首次连接。
- Windows 11 24H2 会把附近 Wi‑Fi 列表作为精确位置能力管理；首次扫描可能出现一次系统授权，拒绝时 Panel 显示“打开位置权限”，直达“设置 > 隐私和安全性 > 位置”。依据见微软的 [Wi‑Fi 访问和位置行为变更](https://learn.microsoft.com/en-us/windows/win32/nativewifi/wi-fi-access-location-changes)、[`WlanScan`](https://learn.microsoft.com/en-us/windows/win32/api/wlanapi/nf-wlanapi-wlanscan)、[`WlanGetAvailableNetworkList`](https://learn.microsoft.com/en-us/windows/win32/api/wlanapi/nf-wlanapi-wlangetavailablenetworklist) 与 [`WlanConnect`](https://learn.microsoft.com/en-us/windows/win32/api/wlanapi/nf-wlanapi-wlanconnect)。FocusPanel 不导出 Wi‑Fi 配置、不读取明文密钥，也不注入 Explorer 网络面板。

![状态中心附近 Wi-Fi 与已保存网络直连](docs/images/wifi-network-chooser.svg)

- 媒体播放也不再依赖原生快捷设置：状态中心提供上一首、播放/暂停、下一首三个 44px 直接按钮，成功后保持状态中心打开，便于连续切歌；紧凑栏状态按钮中键可从任意应用一击播放或暂停，滚轮调音量和右键静音语义保持不变。统一搜索同步支持“上一曲”“播放暂停”“下一首”及英文 `previous track / play pause / next track`。执行使用 Windows SDK 公开的 `VK_MEDIA_*` 虚拟键和现有批量 `SendInput` 按下/释放链，不枚举、注入或读取播放器私有数据；系统阻止模拟输入时进入状态中心明确提示。

![状态中心、紧凑栏与统一搜索的媒体控制](docs/images/media-transport-controls.svg)
- 统一搜索也能一步开始专注：输入 `专注 25`、`开始番茄钟 45 分钟`、`focus 30 min` 或 `pomodoro 60`，按 Enter 后直接复用现有番茄钟状态机设置 1–180 分钟并开始，无需先打开 Focus 中心、番茄钟页面和时长按钮。计时浮窗初次显示不抢走当前激活窗口；运行中或已经暂停的会话绝不会被新命令重置，而会打开番茄钟工作区让用户继续处理。解析要求完整命令与显式时长，裸“专注”、越界值、小数、文件名和尾随文本不会启动计时。
- 统一搜索还能一步收集任务：输入 `任务 买牛奶`、`待办：回复邮件`、`todo book dentist` 或 `task: prepare release`，按 Enter 后直接保存为 Inbox 下的待处理任务，成功时在 Panel 左侧显示可点击 Toast，无需进入任务页。主壳与任务页共享同一个 `TaskService` 串行写入闸门；任务页若正显示 Inbox，新任务会按持久化后的 Id 唯一增量插入，其他范围不会被打扰。标题限制 120 个字符且拒绝换行、控制字符、空内容和尾随伪命令；英文 `task manager` 明确保留给系统任务管理器，只有带冒号的 `task:` 才表示收集。
- 已有待办也进入同一搜索入口：输入任务标题或所属项目名称会匹配未完成任务，按 Enter 直接切到对应项目并打开任务详情；右侧勾选按钮则在搜索结果内直接完成，不必打开 Focus 中心或任务工作区。索引只在搜索打开时通过共享 `TaskService` 后台刷新，输入期间只过滤内存快照，不会每按一个键就访问 SQLite；空查询不会展示任务列表。任务被其他位置删除、读取失败或保存失败时会保留最后有效快照并给出明确反馈，退出会等待已接收的搜索与完成操作。

![统一搜索已有待办与一键完成](docs/images/task-search-direct-actions.svg)
- 搜索结果和任务列表统一继承全局 Fluent `ListBox/ListBoxItem`：启动按钮与标题显式使用动态 `FocusTextBrush`，选中项使用 `FocusAccentSoftBrush`、强调描边和主题文字，不再落回 WPF 的系统浅蓝选择背景，因此深色、浅色及系统强调色变化下都保持可读。
- 任务标题、完成状态和自定义字段采用 180ms 合并保存；根任务、子任务、增删改和全局字段通过同一个后台数据库闸门严格串行，每次操作创建并释放自己的短生命周期 `AppDbContext`，读取使用无跟踪快照。页面切换会先排空旧范围修改，退出会等待已入队保存完成，避免快速输入触发 EF 并发异常、跨操作跟踪污染或丢失最后一次修改。
- 任务 Markdown 图片选择完成后，目标目录创建、唯一文件名生成和文件复制全部在工作线程执行；网络盘、云盘占位图片和大文件不会冻结任务详情。任务在复制期间被关闭或切换时，迟到结果不会写入新任务；点击 Markdown 图片也复用后台 Shell 打开边界，失效关联不会造成 Panel 闪退。
- 开始菜单快捷方式、`shell:AppsFolder` 和应用身份解析在可取消的 STA 后台线程构建；Panel 壳层不再等待完整目录扫描才响应鼠标与键盘。
- 搜索和固定项会先显示名称与首字符占位，再由单一后台队列按需加载真实图标；Shell 图标提供器响应缓慢时不会卡住搜索输入。索引期间显示“正在载入应用目录”，完成但无匹配项时显示明确空状态。
- 打开搜索后会立即聚焦并全选搜索框；无需离开键盘即可用上下方向键选择结果、回车启动，`Esc` 关闭后焦点返回紧凑栏搜索入口。应用目录在后台补全时会按稳定身份保留当前选择，不会把光标跳回第一项。
- 窗口前台状态改变时按应用身份增量更新图标，只替换真正变化的项目，不再清空并重建整条应用栏，因此滚动位置和未变化图标保持稳定。
- 同一运行应用的活动状态、窗口标题和窗口数量改为在原有任务栏项目上原位同步；WPF 不再因前台切换执行集合 `Replace`、销毁按钮并重新创建视觉树，当前应用状态条和工具提示可以平滑更新。
- 应用数量超过紧凑栏高度时，切换到可视区外的应用会自动以最小距离滚动到完整可见位置，并为上下悬浮导航各保留 30px 安全区；已经可见的活动图标不移动。触发依据是稳定应用身份，同一应用内部切换窗口、标题变化或用户手动浏览时不会反复抢回滚动位置。
- 应用栏溢出后，拖动固定项或未固定运行项到可视区上下边缘会渐进自动滚动：越靠近边缘速度越快，离开感应区立即停止，45ms 节流避免高刷新率鼠标导致跳跃。整个拖拽会话持有 Panel 临时交互锁，拖到视口外、取消或放下时不会被自动收起；运行项跨视口放下后仍按原语义自动固定并保存顺序。
- 拖到固定图标上半区时在其顶部显示 3px 强调色插入线并插到之前，拖到下半区则在线条下方插到之后；向上、向下移动都会按移除源项后的真实索引计算，不会错一位。未固定运行区不伪造任意插入位置，只有固定区与运行区的真实边界显示提示；在其他运行项上放下仍按既有语义追加到固定区末尾。
- 固定应用不再只能拖拽排序：右键菜单提供“上移固定应用 / 下移固定应用”，键盘聚焦图标后可直接使用 `Alt+↑ / Alt+↓`。首项和末项会按真实边界禁用不可用方向；菜单、快捷键和拖拽请求都进入应用目录的同一异步写入闸门，在最新持久顺序上计算，不会因快速连续操作使用旧索引覆盖新结果。
- 窗口跟踪覆盖 `CREATE / DESTROY / SHOW / HIDE / NAMECHANGE / FOREGROUND` 完整生命周期；新应用窗口创建后及时进入统一应用栏，最后窗口销毁后及时移除，不再依赖下一次偶然的前台或标题事件纠正陈旧图标。
- 顶层窗口枚举、AUMID/进程身份解析和图标提取通过单消费者后台快照执行，不再占用 WPF 界面线程；窗口事件在捕获期间继续到达时只保留一次尾随刷新，并用修订号拒绝旧结果、隐藏后的迟到结果和退出后的回调。
- WinEvent 只接收 `OBJID_WINDOW` 窗口本体并跳过 FocusPanel 自身进程；按钮、菜单和 Panel 显隐不会触发无意义的完整窗口重扫，短时间重复通知继续合并为一次刷新。
- 窗口枚举采用最后有效快照提交：`EnumWindows` 整体失败时保留当前应用栏，不把系统瞬时错误显示成“所有运行应用消失”；单个受保护窗口的进程、身份或图标读取失败只降级该项目，不中断其他窗口。
- `SnapshotChanged` 按订阅者隔离发布，一个界面监听器异常不会阻断其他状态消费者，也不会反向终止窗口枚举。退出时先标记跟踪器失效、停止计时器并解除 WinEvent；已经在途的原生回调会在 Dispatcher 关闭前静默丢弃。
- Panel 隐藏后暂停完整窗口枚举、时钟、系统状态和任务摘要刷新；右缘热区、全屏抑制、安全恢复和 GitHub 更新检查继续运行。再次唤出时先刷新窗口快照和当前时间，状态中心与日历在打开时即时刷新。
- 音频、网络、输入法和电池状态通过单消费者后台快照读取；刷新期间的重复请求合并为一次尾随刷新，只有最终快照回到 WPF Dispatcher 后才更新界面。设备读取失败不会阻塞悬停呼出，退出后的迟到结果会被丢弃。
- 待办数量和日历 42 天专注摘要也在工作线程使用独立 `AppDbContext` 查询；快速翻月时旧月份结果不会覆盖当前网格，SQLite 临时繁忙时保留最后有效摘要而不是闪成 0 项。
- 固定应用在后台读取后保存为线程安全内存快照；前台切换、窗口标题变化及窗口创建/关闭只组合缓存与运行窗口，不再在 WinEvent 高频路径上反复打开 SQLite。固定、取消固定和拖动排序也通过专用后台闸门串行提交，成功后才原子替换缓存；拖放提交期间保持 Panel 展开，退出会等待在途写入完成。
- 主壳启动时在工作线程用一个无跟踪快照读取首次引导、任务栏替代、主题和全屏热区设置，不再阻塞 `MainViewModel` 构造或为四个键重复打开 SQLite。窗口在快照就绪前保持隐藏，热区、托盘唤出和全局快捷键不会提前展开；就绪后才创建热区并决定显示引导或申请一次任务栏接管。运行中写入继续按设置键合并并由单消费者后台队列串行落盘。
- 任务栏控制器构造只校验恢复会话路径，不再在普通启动期间同步创建 `%LOCALAPPDATA%\FocusPanel` 目录；只有用户真正启用替代模式时，才在隐藏原任务栏之前准备目录并写入恢复快照。任一步失败都会中止接管，避免出现“任务栏已隐藏但没有可恢复会话”。
- 安全退出采用两阶段关闭：先恢复原生任务栏、停止热区和新的界面操作，再异步排空音频控制、开机启动设置、任务、桌面布局、番茄钟、固定应用与 Shell 设置；完成后才真正销毁窗口。慢磁盘或最后一次拖拽保存不会冻结 WPF Dispatcher，重复退出也只等待同一事务。
- 正常启动会先在当前线程幂等恢复遗留任务栏会话与原生桌面图标，再把 SQLite 在线启动备份、完整性检查、手工建表、异常库归档和备份恢复作为一个不可拆分事务交给后台协调器。WPF Dispatcher 不再被大数据库或慢磁盘占住；事务通过后才创建主壳，恢复提示与安全停止提示仍回到界面线程显示。
- 数据库后台事务开始前立即显示现有的 `3px` 右缘指示条，并以低亮度呼吸表示“FocusPanel 正在启动”；系统启用减少动态效果或高对比度时使用稳定亮度。它保持点击穿透、无激活和不抢焦点，也不会提前创建热区；主壳就绪后直接接管同一个窗口并切换为稳定运行指示，避免关闭、重建或边缘闪烁。数据检查失败时先关闭指示条再显示安全提示。
- Windows 开机启动项的初始注册表读取、启用、禁用和失败复读全部通过串行后台协调器执行，主壳构造与设置开关不再同步访问注册表。快速连续切换会按请求顺序写入并以最后选择为准；启动期迟到读取不能覆盖用户操作，写入失败则回滚到后台复读的真实状态，退出前排空已接收的修改。
- 番茄钟历史统计在工作线程加载；倒计时归零时立即更新界面、播放提醒并把完整会话交给后台单消费者串行保存。开始新一轮、暂停或重置后，上一轮迟到的保存结果不会覆盖当前提示，退出前会排空已经进入队列的会话。
- OKR 首次打开时由工作线程一次读取本地目标、飞书配置、同步间隔和最后同步时间；SQLite 较大或暂时繁忙不会阻塞工作区展开。手动飞书同步、配置读写和 AI 预留接口也不再同步等待 WPF Dispatcher，旧加载快照不会覆盖刚完成的本地编辑。
- OKR 的新增、编辑、删除目标与关键结果，以及 AI 创建草稿，统一进入同一个后台持久化闸门；界面不再直接创建数据库上下文或执行 schema 检查。读取快照与写入严格互斥，保存成功后才提交生成 ID 和界面集合变化；等待飞书删除同步的目标不会在后台刷新时重新出现。
- AI 页面打开时异步读取加密凭据状态与模型配置，不再在构造 WPF 工作区时同步打开 SQLite；发送前解密、保存模型和清除凭据也通过同一个后台闸门严格串行。配置仍使用当前 Windows 用户的 DPAPI 加密，数据库键名与既有数据完全兼容。
- 未运行的固定项点击启动；单窗口应用点击激活/最小化；多窗口应用左键展开一层文字窗口列表，点击标题即可直接切换，不再进入二级子菜单。右键菜单继续提供启动新实例、固定、逐窗口关闭和关闭全部窗口。
- 应用图标支持 Windows 任务栏常用的新实例手势：`Shift+左键` 或鼠标中键直接启动新实例；没有可靠启动目标的受保护窗口不会显示或执行该动作。工具提示和读屏帮助会同步说明当前可用操作。
- 可把 Explorer、桌面或文件管理器中的文件和文件夹直接拖到统一应用栏图标上，用该应用打开；目标图标只显示一层 Fluent 强调反馈，且不会把外部文件误判成固定项排序或桌面收纳。普通可执行文件与快捷方式在后台一次接收最多 32 个路径，尾部反斜杠和空格会按 Windows 命令行规则正确转义；打包应用使用公开的 [`IApplicationActivationManager::ActivateForFile`](https://learn.microsoft.com/zh-cn/windows/win32/api/shobjidl_core/nf-shobjidl_core-iapplicationactivationmanager-activateforfile) 文件激活合约。不存在、不可访问或超出上限的项目会给出部分成功/失败 Toast，退出前会排空已进入边界的打开请求。
- 多窗口列表精确标记当前前台窗口；同一应用内部切换窗口也会增量更新标记。标题超过 340px 时视觉省略，读屏名称仍保留完整标题并说明“当前窗口”。
- 开始按钮右键、应用管理、多窗口列表、关机子菜单和托盘菜单统一使用单层 Fluent 圆角菜单；静态 XAML 项显式引用主题，运行时创建项会在创建、挂载和打开三个阶段重新确认应用级 Style。独立 Popup 使用不透明的专用深浅主题表面，并覆盖 WPF 系统菜单的后备背景、文字与选中色；即使 Popup HWND 创建期间短暂解析到系统模板，也不会再出现透明浅底、白字或直角亮蓝选中条。
- 菜单悬停、键盘焦点、当前窗口勾选、禁用状态和子菜单箭头全部使用动态主题令牌；子菜单保持文字省略与完整辅助功能名称，不增加黑色投影边框。
- 全部工具提示使用单层 Fluent 圆角表面，背景、文字和描边跟随动态深浅色主题；不再复用 WPF 默认矩形模板、硬编码深色底或系统黑色阴影。应用状态与新实例手势等多行提示也保持同一信息层级。
- 主题、AI 模型、任务字段、OKR 同步间隔等下拉选择统一使用非编辑式 Fluent ComboBox：封闭状态、箭头、键盘焦点、选中标记和展开 Popup 共用动态主题令牌，设置页不再漏回系统默认浅色直角控件。
- 设置选项、AI 数据授权、任务完成和多选字段统一使用 Fluent CheckBox：20px 圆角标记位于至少 44px 点击区内，悬停、按下、选中、不确定和禁用状态均使用动态主题，不再混入系统方框或蓝色勾选样式。
- 主壳、Focus/状态中心、桌面收纳、任务、OKR、AI 和所有弹层统一使用 10px Fluent 滚动轨道：滑块为单层圆角表面，普通状态柔和、悬停时显示强调色，纵向和横向仍保留轨道分页、拖动与滚轮语义。
- 状态中心音量改用 44px 交互高度的 Fluent Slider：已选轨道、未选轨道和圆形滑块使用动态主题；电池、更新、番茄钟与 OKR 统一为圆角 Fluent ProgressBar，并提供明确的确定进度和脉冲加载状态。
- 搜索、任务、OKR、桌面重命名、AI 输入和密钥字段统一使用 Fluent 输入系统：文本、光标、选择色、只读、禁用和键盘焦点全部跟随主题；焦点只增强同一个圆角表面，不再额外嵌套系统矩形焦点框。
- 任务视图分段选择、桌面收纳工具栏开关和文件选中态统一使用跟随 Windows 的柔和强调色：选中项以低饱和背景、动态描边或底部标记表达，不再混用整块高亮蓝和硬编码 `#007AFF`。
- 右缘运行指示、桌面重命名遮罩和 Explorer 系统限制警告均使用动态状态令牌；浅色桌面上的边缘提示、警告文字对比度及遮罩层级不再依赖页面硬编码颜色。`Views/` 中的 XAML 禁止直接写十六进制颜色。
- 主壳、六个业务工作区、日历、任务详情和浮动番茄钟统一使用语义字体层级：页面 28px、章节 18px、卡片 15px、正文 13px、说明 12px、元信息 11px；指标与计时使用 Segoe UI Variable Display，图标和紧凑栏时间保留专用尺寸。
- 应用搜索整行启动、普通操作和退出/关机/删除/清除凭据等危险操作共用 Fluent 单层按钮模板；危险按钮使用动态柔和背景与危险前景，深色、浅色和高对比度均保持可读，页面不会再回退到 WPF 原生矩形按钮。
- 删除任务、清除凭据、自动整理、数据库恢复、电源和软件更新等运行期提示统一使用 FocusPanel Fluent 模态对话框：复用 DWM 原生圆角与毛玻璃、动态主题、语义图标和统一按钮。危险确认默认聚焦安全的“否”，长更新说明可滚动；只有应用资源尚未建立的灾难恢复阶段才使用系统提示兜底。
- 模态对话框显示期间会持有 Panel 的临时交互锁，背后的工作区不会因窗口失焦自动缩回；确认、取消、关闭和异常路径都会幂等释放。应用搜索整行按钮与“最近使用”入口也提供随内容变化的读屏名称。
- 任务设置中的图片保存目录使用 Windows Shell 原生现代文件夹选择器，不再弹出老式 WinForms 树形窗口；选择器定位当前目录、归属 FocusPanel 窗口并保持 Panel 展开，取消不修改设置，保存失败会回滚原路径。
- Markdown 插入图片同样通过可替换的 Windows 现代文件选择边界：选图窗口归属当前 FocusPanel 窗口，期间保持 Panel 展开；中文过滤器只显示支持的图片，取消不会改写正文，错误进入统一 Fluent 提示。
- 应用图标左侧使用任务栏式圆角状态条：后台运行显示 `4×12px` 短条，当前活动扩展为 `4×24px` 长条并使用单一柔和背景，固定但未运行的应用不显示状态条。状态层完全点击穿透，不会吞掉图标左缘操作。
- 工具提示和读屏名称明确区分“已固定 · 未运行”“正在运行 · 1 个窗口”“正在使用 · 2 个窗口”；辅助操作提示会按启动、单窗口切换/最小化和多窗口列表自动变化。
- 应用启动会区分普通可执行文件、快捷方式、Shell 路径和 `shell:AppsFolder` 返回的 AUMID；商店应用不再把 AUMID 错当文件名。应用已卸载、固定路径移动或 Shell 拒绝启动时不会让 Panel 闪退，而是在状态中心说明原因并引导重新固定。
- 搜索结果、固定应用和“启动新实例”的 Windows Shell 启动全部在工作线程执行；网络快捷方式或 Shell 宿主响应缓慢时不会冻结 Panel。连续点击允许并发启动，等待中的旧结果不能覆盖最后一次点击的成功/失败反馈；传入工作线程的是不含 WPF 图标的纯启动快照。
- 窗口切换、最小化和关闭会检查 Win32 的真实结果；Windows 拒绝前台切换、窗口已失效或关闭消息未能入队时，状态中心会显示对应窗口和原因，不再表现为点击后毫无反应。
- 固定、取消固定和拖动排序会确认 SQLite 提交结果；数据库短暂锁定或写入失败不会冲击 UI 线程，也不会把未保存的顺序伪装成成功。
- 运行项可通过右键固定；拖动未固定运行项会自动创建固定项并保存排序，取消固定后只要窗口仍在就继续显示。
- 紧凑栏从上到下固定为开始、搜索、统一固定/运行应用列表、任务视图、Focus 中心、状态中心和时间；六个系统入口顺序稳定，中部只承担可滚动应用区。
- 任务视图入口不再只是 Win+Tab 转发：滚轮向上/下可直接切换上一个/下一个虚拟桌面，右键菜单可切换、新建或关闭当前桌面，菜单同步显示 `Win+Ctrl+←/→/D/F4`。滚轮使用 160ms 防抖，不会因高分辨率触控板一次动作跨过多个桌面；实现只复用微软公布的[多桌面快捷键](https://support.microsoft.com/en-us/windows/keyboard-shortcuts-in-windows-dcc61a57-8ff0-cffe-9796-cb9706c75eec)，不调用未公开 Virtual Desktop COM 接口。
- 多屏定位以 Windows 主屏的物理边界和主屏 DPI 为唯一基准；紧凑栏、展开动画、12px 热区和 3px 指示条不会混用窗口当前屏的 WPF DIP。主屏存在负坐标、位于副屏右侧或采用不同缩放时，Panel 仍完整向主屏内部展开。
- 中部应用列表超出可视高度时显示轻量悬浮上下导航；到达顶部或底部后相应箭头自动消失，点击按一个应用图标步长移动，鼠标滚轮仍可直接滚动。
- Focus 中心统一承载桌面收纳、任务、番茄钟、OKR、AI、最近使用模块和设置更新；状态中心集中音量、静音、网络、电池、通知、输入法、显示桌面和电源操作。
- 状态中心的快捷设置、通知、输入法、显示桌面、锁定、睡眠与电源操作均返回明确结果；成功后关闭 FocusPanel 弹层以免遮挡 Windows 界面，系统拒绝或启动失败时自动回到状态中心显示可操作的替代方式，不再静默失败或让异常冲击 UI 线程。
- 开始、搜索、任务视图、小组件、运行、Win+X 管理工具、电源设置、显示桌面、锁定、睡眠以及确认后的重启/关机统一通过后台系统动作协调器执行；Explorer 或 Shell 宿主繁忙不会冻结紧凑栏。多个入口连续触发时允许独立执行，但只有最后一次请求能更新状态中心，旧失败不会覆盖新成功；浏览器下载页与数据库恢复交接也使用同一异常边界。
- 音量和静音使用一次性 Core Audio 快照区分“真实 0%”与“没有默认输出设备”；端点切换或写入失败时滑块回到最后确认值并显示原因。无输出设备时控件自动停用，设备恢复后由状态刷新重新启用；紧凑栏滚轮只有在音量写入成功后才会取消静音。
- 音量 Slider 的高频变化、静音点击和紧凑栏滚轮改由单消费者后台控制器执行；等待中的音量请求只保留最终值，音量与静音严格串行。每次工作线程操作独立初始化 Core Audio COM、创建并释放端点枚举器，旧修订结果不会覆盖新操作；写入期间暂停旧状态快照回填，最新请求失败才回退到最后真实成功值并提示。
- 紧凑栏状态入口和状态中心静音按钮会根据当前音量显示 Segoe Fluent 音量、静音或设备不可用图标；工具提示和读屏名称同步显示百分比。Panel 从隐藏状态重新唤出时立即刷新一次，不需要先打开状态中心，也不会在隐藏期间常驻轮询。
- 电池状态通过单次快照同步读取是否存在、百分比和充电状态；状态中心按 10% 档位显示 Segoe Fluent Battery/BatteryCharging 图标和“充电中”文本。紧凑栏状态入口的一个提示整合网络、音量与电池，不增加额外按钮或破坏六入口布局。
- 网络状态通过单次快照生成可用性、连接类型、接口名称和详情；状态中心按无线、有线、其他连接显示 WiFi、Ethernet 或 Globe 图标，离线时显示 Error。接口切换或枚举失败不会再把不同采样时刻的在线/离线文案拼在一起，也不读取 Explorer 私有托盘数据。
- 输入法状态通过一次前台键盘布局读取生成语言和输入法简称；状态中心入口显示“输入法 · 中 / 拼”“输入法 · EN”等，工具提示提供完整状态。点击继续使用 Win+Space，不读取 Explorer 私有托盘结构，也不擅自修改输入法设置。
- 时间入口提供周一开头的 6 周月历，可切换月份、回到今天或直接选择日期；方向键按日/周移动，`PageUp` / `PageDown` 跨月，`Ctrl+Home` 回到今天，键盘焦点始终跟随所选日期。右键或 `Shift+F10` 可直接进入 Windows 日期时间与通知设置。完成过番茄钟的日期显示专注圆点，底部汇总所选日期的专注次数和分钟数。
- 后台发现 GitHub 新版本后，紧凑栏 Focus 中心入口会显示更新状态点，Focus 中心顶部显示目标版本卡片；点击即可进入设置页一键安装，不再只依赖托盘气泡。
- Velopack 安装定位和更新管理器在共享工作线程准备，主窗口构造与 XAML 首帧不再等待安装目录扫描；首次自动检查和设置页手动检查都会等待同一个初始化结果，安装版不会因为准备尚未结束而漏掉开机后的更新。
- 更新包下载完成后，SQLite 完整性检查、在线备份和历史备份清理也在专用工作线程执行；设置页会保持忙碌与 Panel 交互锁，备份完成后才恢复原任务栏、桌面图标并启动 Velopack。准备失败会恢复右缘监测并显示原因，不会让界面停在“正在安全重启”或允许重复提交。
- 开始按钮左键打开 Windows 开始菜单，右键提供 Win+X 风格系统管理菜单，包括安装的应用、电源选项、事件查看器、系统、设备管理器、网络连接、磁盘管理、计算机管理、终端、管理员终端、任务管理器、设置和文件资源管理器。
- 第三方托盘溢出内容不再提供入口：FocusPanel 不读取 Explorer 私有 UI 数据，也不会为打开托盘而临时显示原生任务栏。

![六入口紧凑任务栏](docs/images/six-entry-taskbar.svg)

![任务视图与虚拟桌面快捷控制](docs/images/virtual-desktop-task-view.svg)

![右缘热区后台采样与低频 UI 提交](docs/images/edge-hot-zone-background-sampling.svg)

![系统入口后台执行与反馈隔离](docs/images/system-action-background-coordinator.svg)

![紧凑栏顺序与深色菜单](docs/images/compact-dock-dark-menu.svg)

![应用图标加载与稳定降级](docs/images/app-icon-fallback.svg)

![非阻塞应用目录与图标队列](docs/images/app-catalog-background.svg)

![统一应用栏可靠启动链路](docs/images/app-launch-safety.svg)

![统一应用栏并发后台启动协调](docs/images/app-launch-background-coordinator.svg)

![完整任务栏接管与悬停窗口切换](docs/images/taskbar-exclusive-hover-switcher.svg)

![滚轮循环窗口与悬停快速关闭](docs/images/taskbar-wheel-window-actions.svg)

![DWM 实时窗口缩略预览与安全降级](docs/images/taskbar-dwm-live-preview.svg)

![应用右键最近项目与原生 Jump List](docs/images/taskbar-jump-list-recents.svg)

![拖文件到应用图标直接打开](docs/images/taskbar-file-drop-launch.svg)

![任务栏操作结果与失败反馈](docs/images/taskbar-action-feedback.svg)

![运行应用窗口生命周期跟踪](docs/images/window-lifecycle-tracking.svg)

![运行窗口最后有效快照与异常隔离](docs/images/window-snapshot-resilience.svg)

![运行窗口合并式后台快照](docs/images/window-tracker-background-refresh.svg)

![系统状态合并式后台刷新](docs/images/system-status-background-refresh.svg)

![状态中心音频后台控制链路](docs/images/audio-control-background-pipeline.svg)

![日历摘要后台查询与月份隔离](docs/images/calendar-summary-background-refresh.svg)

![统一应用栏固定项内存快照](docs/images/pinned-app-memory-snapshot.svg)

![统一应用栏固定项后台提交](docs/images/pinned-app-background-write.svg)

![Panel 设置非阻塞持久化](docs/images/shell-preference-background-write.svg)

![主壳偏好后台加载与接管安全门](docs/images/shell-preference-startup-gate.svg)

![任务栏恢复会话按需创建](docs/images/taskbar-session-lazy-creation.svg)

![更新管理器后台准备与首轮检查](docs/images/update-manager-background-initialization.svg)

![更新安装后台备份与安全交接](docs/images/update-install-background-handoff.svg)

![两阶段异步安全退出](docs/images/async-shutdown-drain.svg)

![数据库启动事务后台协调](docs/images/database-startup-background.svg)

![启动指示条与主壳无缝交接](docs/images/startup-indicator-handoff.svg)

![开机启动注册表后台串行协调](docs/images/auto-startup-background-coordinator.svg)

![番茄钟非阻塞统计与安全落盘](docs/images/pomodoro-persistence-lifecycle.svg)

![OKR 后台快照与异步同步边界](docs/images/okr-background-workspace.svg)

![OKR CRUD 后台串行持久化](docs/images/okr-crud-background-persistence.svg)

![AI 配置后台读取与串行保存](docs/images/ai-settings-background-persistence.svg)

![多窗口应用一层直接列表](docs/images/multi-window-direct-list.svg)

![统一应用栏多窗口角标与文字预览](docs/images/taskbar-window-group-preview.svg)

![统一应用栏运行与活动状态](docs/images/taskbar-app-state-feedback.svg)

![溢出列表中的活动应用自动露出](docs/images/taskbar-active-app-reveal.svg)

![溢出应用栏拖拽自动滚动](docs/images/taskbar-drag-auto-scroll.svg)

![任务栏前后插入位置反馈](docs/images/taskbar-drop-insertion-cue.svg)

![固定应用菜单与键盘排序](docs/images/taskbar-keyboard-reorder.svg)

![统一应用栏原位状态同步](docs/images/taskbar-in-place-sync.svg)

![应用搜索完整键盘路径](docs/images/app-search-keyboard-flow.svg)

![应用搜索分级匹配与稳定排序](docs/images/app-search-ranked-matching.svg)

![应用与窗口统一搜索切换](docs/images/unified-app-window-search.svg)

![统一搜索直达 Windows 系统命令](docs/images/system-command-search.svg)

![统一搜索直达 Windows Shell 快捷动作](docs/images/shell-quick-command-search.svg)

![统一搜索安全内联计算与复制](docs/images/inline-calculator-search.svg)

![统一搜索一键音量命令](docs/images/audio-command-search.svg)

![统一搜索一步开始专注](docs/images/focus-command-search.svg)

![统一搜索一步收集任务](docs/images/task-capture-search.svg)

![统一 Fluent 任务栏菜单](docs/images/fluent-context-menu-system.svg)

![运行时菜单深色主题解析](docs/images/runtime-menu-theme.svg)

![独立 Popup 与 DWM 壳层主题隔离](docs/images/popup-theme-isolation.svg)

![任务编辑安全落盘生命周期](docs/images/task-save-lifecycle.svg)

![任务工作区后台短上下文持久化](docs/images/task-background-persistence.svg)

![任务图片后台导入与安全打开](docs/images/task-image-background-pipeline.svg)

![统一应用栏鼠标操作](docs/images/taskbar-app-mouse-actions.svg)

![统一 Fluent 工具提示](docs/images/fluent-tooltip-system.svg)

![统一 Fluent 下拉选择](docs/images/fluent-combobox-system.svg)

![统一 Fluent 勾选控件](docs/images/fluent-checkbox-system.svg)

![统一 Fluent 滚动系统](docs/images/fluent-scrollbar-system.svg)

![统一 Fluent 滑块与进度](docs/images/fluent-slider-progress-system.svg)

![统一 Fluent 输入状态](docs/images/fluent-input-system.svg)

![统一 Fluent 选择状态](docs/images/fluent-selection-system.svg)

![动态状态色令牌](docs/images/fluent-state-tokens.svg)

![统一 Fluent 列表状态](docs/images/fluent-list-selection-system.svg)

![统一 Fluent 字体层级](docs/images/fluent-typography-system.svg)

![统一 Fluent 操作按钮](docs/images/fluent-action-buttons.svg)

![统一 Fluent 模态对话框](docs/images/fluent-dialog-system.svg)

![Windows Shell 现代文件夹选择器](docs/images/modern-folder-picker.svg)

![Windows 现代图片选择器](docs/images/modern-image-picker.svg)

### 无激活原生通知

更新可用和番茄钟完成不再依赖 Explorer 托盘气泡，而由 FocusPanel 在 Panel 左侧显示单层 Fluent Toast。通知复用 Windows 11 DWM 原生圆角与 Desktop Acrylic；显示时不激活窗口、不打断当前输入，鼠标悬停会暂停自动关闭。更新通知可直接进入设置页，专注完成通知可直接回到番茄钟；托盘图标只保留恢复原生任务栏与安全退出能力。

![FocusPanel 无激活原生毛玻璃通知](docs/images/native-focus-toast.svg)

### 两个中心

Focus 中心只放 FocusPanel 的业务模块；状态中心只放设备状态、Windows 公开入口与任务栏恢复信息。两个中心与搜索、日历、设置、电源弹层互斥，按 `Esc` 可关闭。

Focus 中心顶部提供“今日概览”：以只读方式汇总未完成任务、今日专注、进行中 OKR 和已收纳桌面项目，并显示可以立即推进的任务与目标。概览不会改变业务数据，打开或手动刷新时才读取最新本地快照。

![Focus 中心](docs/images/focus-center.svg)

![状态中心](docs/images/status-center.svg)

![今日概览与快速行动](docs/images/dashboard-today.svg)

### 月历与专注回顾

时间弹层只承载日期、专注历史和两个高频动作，不再重复堆放通知、桌面等状态中心入口。月历固定生成 42 个日期，避免不同月份打开时高度跳动；今天使用细描边，选中日期使用单一强调表面，相邻月份降低透明度。专注圆点与摘要直接读取现有 `PomodoroSessions`，不会创建新业务表，也不会修改历史记录。

![月历与每日专注回顾](docs/images/calendar-focus-history.svg)

![月历键盘导航与焦点跟随](docs/images/calendar-keyboard-navigation.svg)

![弹层焦点往返与时间设置](docs/images/transient-panel-focus-return.svg)

## 侧边任务栏完整替代与安全恢复

完整替代模式会先保存主屏原工作区、AppBar 状态、任务栏可见性和原始 DWM app-cloak 位，再通过公开 [`ABM_SETSTATE`](https://learn.microsoft.com/en-us/windows/win32/shell/abm-setstate) 清除 `ABS_AUTOHIDE`、一次性把主屏工作区释放到完整显示器边界。随后通过公开 [`SetWindowRgn`](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowrgn) 为主屏 `Shell_TrayWnd` 设置空窗口区域，并以公开 [`DWMWA_CLOAK`](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute) 增加独立的 DWM 合成隐藏层后隐藏宿主。即使 Explorer 的边缘手势重新设置逻辑可见位，任务栏仍没有可绘制、可命中或可呈现的表面。FocusPanel 不结束 Explorer、不修改副屏任务栏，也不读取 Explorer 私有结构。

守护器只读取 AppBar、工作区、宿主句柄、空区域和 DWM cloak 状态，不会周期性执行 `SetWindowRgn`、`DwmSetWindowAttribute`、`ShowWindow`、`ABM_SETSTATE` 或 `SPI_SETWORKAREA`，因此不会与 Explorer 在“隐藏/显示”或“占用/释放工作区”之间来回争抢。仅宿主变为逻辑可见而两层抑制仍有效时继续稳定接管；任一呈现层丢失都属于确定的原生表面恢复，会立即安全恢复并提示。Explorer 宿主或显示布局等其余瞬时变化仍要求连续两次、约 4 秒确认，中间恢复正常会清除待确认状态。

接管成功时会记录当前主屏 `Shell_TrayWnd` 句柄。Explorer 重启并创建新宿主后，即使新窗口碰巧处于隐藏状态，也会准确报告“Explorer 宿主变化”，而不是误报成普通可见性变化。每次恢复和重新接管都会推进会话代际，已经在后台读取中的旧守护结果会自动作废，不会污染新会话或弹出迟到警告。状态中心和设置页会显示停止原因；确认环境正常后，由用户点击“重新接管任务栏”手动启用。

首次启用前会显示安全说明。只有在侧边壳层、热区以及独立恢复守护进程都就绪后，FocusPanel 才会隐藏原任务栏；紧急快捷键注册失败时不会改变任务栏设置。

“随 Windows 启动”只在用户明确勾选后写入当前用户 Run 键。FocusPanel 会创建缺失的注册表键、正确引用带空格的程序路径，并在权限或路径失败时显示原因、回滚复选框；不会静默显示成已启用。

- 紧急恢复：`Ctrl+Alt+Shift+F10`
- 正常退出、未处理异常、数据库恢复重启：均恢复原任务栏可见性与 AppBar 设置
- 父进程异常退出：`--taskbar-watchdog` 守护模式负责恢复
- Explorer 重启或任务栏状态改变：停止本次替代并恢复原设置，避免可见性循环
- 恢复会话：`%LOCALAPPDATA%\FocusPanel\taskbar-session.json`

遇到异常时，先按紧急恢复快捷键。仍未恢复可重新启动 FocusPanel；启动阶段会检查并恢复遗留会话。程序永远不会结束 Explorer，也不会持续覆盖 Windows 工作区。完整替代后，Win+A、Win+N、Win+Space 等公开系统快捷入口继续可用；Explorer 的第三方托盘溢出内容属于私有壳层，FocusPanel 不读取其进程内存，也不能保证在原任务栏隐藏时完整复制。

![任务栏安全状态机](docs/images/taskbar-safety-flow.svg)

![任务栏守护连续异常确认](docs/images/taskbar-guard-confirmation.svg)

![原生任务栏空区域接管与恢复](docs/images/taskbar-empty-region-takeover.svg)

![原生任务栏双层呈现抑制](docs/images/taskbar-dual-surface-suppression.svg)

![混合 DPI 双屏物理坐标定位](docs/images/multi-monitor-physical-placement.svg)

![开机启动写入与回滚](docs/images/startup-safety.svg)

## 桌面收纳与效率模块

- 新收纳文件始终保留在原桌面路径，不改名、不移动；FocusPanel 保存原始文件属性并追加 `Hidden + System`，取消收纳时精确恢复原属性。
- 从 Windows 桌面把文件或文件夹拖入收纳分区，会立即执行同一套隐藏事务；其他目录的项目不会被擅自移动。
- Explorer 外部拖入会先在工作线程生成一次路径预检快照：规范化并按大小写不敏感身份稳定去重，再检查存在性并区分用户桌面、公共桌面和越界路径。拖放回调不再逐项同步访问磁盘；单个失效或无权限路径不会终止整批，缺失、越界、重复和授权取消会分别计数反馈。
- 普通“显示隐藏项目”开启时，已收纳图标仍会隐藏；如果同时开启“显示受保护的系统文件”，设置页会提示 Windows 无法保证图标不可见。
- 不再注入或持续修改 Explorer 的桌面列表；Explorer 刷新、重启和系统重启后按文件属性保持状态。
- 属性改变后会通知 Shell 更新项目并重新枚举桌面目录，避免图标只变成半透明却仍停留在桌面。
- 收纳时的文件存在检查、完整原属性读取、`Hidden + System` 写入及 Explorer 属性通知统一经过后台 I/O 边界，不再在 WPF 调用上下文同步访问文件系统；公共桌面的应用与失败回滚使用同一提权路径，避免属性已经改变但普通权限无法恢复。数据库仍先记录“收纳中”，成功后标记稳定，失败时恢复原属性或保留“需要恢复”记录。
- 旧版 `.FocusPanel` 仓库继续兼容，升级时不自动移动旧文件。
- 不创建全屏桌面覆盖窗口；Windows 原生桌面保持可点击，文件收纳操作集中在侧边栏工作区完成。
- 桌面收纳工具栏、收纳盒、视图选项、新建、修复和重命名已全部使用共享 Fluent 控件；页面不再保留 Material 兼容控件或矩形/圆角双重外框。
- 桌面文件变化后按收纳盒身份和文件完整路径差量同步：未变化的收纳盒、卡片和展开状态保留原对象，文件移动只更新对应集合，列表不会因 `Clear()` 重建而自动跳回顶部。
- 桌面监控、拖入拖出、视图切换和手动刷新触发的分区重组不再在 WPF UI 线程读取全部 SQLite 分区与文件偏好；重复请求合并为后台快照，返回后继续使用差量同步器，因此大量记录下滚动、拖拽和 Panel 自动收起保持响应。
- 图标缩放、列表/网格、个性化/时间线和自动整理开关使用 180ms 合并式后台保存；快速连续调整只写入最后状态，数据库读写严格串行，退出前排空当前设置，保存失败会保留本次会话选择并显示提示。
- 新建、重命名、删除、拖拽排序、跨列移动和普通文件分类也统一进入同一个后台仓库闸门；操作成功后才刷新布局，失败时原界面和数据库保持原状并显示 Fluent 错误提示，退出前会等待已经进入仓库的写操作完成。
- 双击文件卡片和“打开桌面文件夹”不再从 WPF 命令同步调用 Windows Shell；每次打开请求独立进入工作线程，慢磁盘、离线路径或失效文件关联不会冻结收纳区。连续打开时只有最后一次请求可以显示失败提示，旧失败不会盖住用户后续的成功操作。
- 分区拖拽排序持有既有临时交互锁直到 SQLite 提交完成；这段时间 Panel 不会自动收起，重复拖拽不能并发改写顺序。跨列移动会同时连续重排来源列和目标列，不留下重复或跳跃序号。
- 左右列排序使用 `Move` 更新而非清空重加；同路径文件刷新保留选中状态。数据库瞬时读取失败时保留最后一次有效界面，不把暂时错误显示成“所有收纳盒消失”。
- 桌面根目录监视器会把 500ms 内重复的创建、写入、删除和重命名通知按路径合并，只读取真正变化的项目；旧仓库变化或监视器缓冲区异常时才安全回退全量扫描。
- “新增桌面项目自动按类型收纳”只处理开关开启后由监视器确认的新路径；普通内容变化、属性刷新、既有项目改名、启动扫描和错误恢复不会把原有桌面项目批量抓走。复制临时文件改名到最终名称时保留新增身份，取消收纳与救援工具创建的项目则明确跳过，避免恢复后立即被重新收纳。
- 自动收纳不会在后台弹出管理员授权窗口；公共桌面项目保持可见并显示“需手动授权”，成功、部分失败和设置保存异常也会在选项下方给出非打断式状态。
- 自动收纳和手动一键整理都会登记为可等待操作；应用退出时先停止接收新批次并等待已开始的文件属性事务收尾，再释放监视器。进度渲染和文件列表订阅者各自捕获异常，单个卡片、迟到 Dispatcher 消息或观察者失败不会再触发整个 Panel 的全局闪退。
- 外部重命名、文件大小和类型变化继续复用原卡片并更新派生文本；已收纳文件外部丢失时保留“需要恢复”入口。退出应用会释放全部桌面监视器和计时器，不留下重复刷新源。
- 网格和列表模式使用与外层统一滚动条协作的视口虚拟化面板，只创建当前可见行和前后缓冲行的文件卡片；完整集合、滚动范围、双列收纳盒和业务排序不变。
- 文件卡片容器在滚动时回收复用，图标缩放、窗口宽度、收纳盒折叠和集合变化会重新计算实现区间。1000 项真实 WPF 冒烟中，首屏、中段、集合变化和返回顶部仅存在 `9/12/12/9` 个视觉容器。
- 网格与列表中的文件右键菜单按可视卡片隔离实例，并在每次打开时绑定当前右键目标；即使容器经过虚拟化回收，直接右键未选中的文件也会先同步选择，再执行移动、收纳或恢复，不会误操作上一次选中的项目。菜单显式使用同一套深浅色 Fluent 主题。
- 视图选项、新建收纳盒、收纳盒操作和修复工具使用单一活动弹层：打开新 Popup 会关闭旧 Popup，焦点进入首个可操作控件；在独立弹层内按 `Esc` 会关闭弹层并回到原触发按钮。文件右键菜单、模块切换和页面卸载也会清理活动浮层，不会让自动收起计数残留。
- 文件监控、手动刷新和自动整理后的刷新共享同一个串行闸门；收纳与取消收纳另用文件属性操作闸门。多次拖放、自动整理和拖出恢复不会并发改写同一项目，退出后的排队操作和迟到 Dispatcher 回调也不会继续触碰已释放界面。
- 主壳构造不再同步读取桌面收纳 JSON 设置或创建文件监控器；默认工作区先显示轻量 Fluent 加载状态，设置读取、旧目录检查和监控器准备在工作线程完成。准备期间切换到其他模块时，迟到结果只缓存不抢回页面；初始化失败会显示可读状态，退出后完成的实例会立即释放。
- 设置页用于提示收纳限制的“显示受保护的系统文件”状态也通过合并式后台刷新读取；启动和每次打开设置都不会同步访问 Explorer 注册表，关闭主壳后的迟到结果不会再修改界面。
- 任务模块使用统一 Fluent 表面，保留项目/子任务、列表和自定义字段语义；看板会按状态生成真实列，任务可前后移动、进入详情、进入子任务或删除。
- 任务详情使用 Windows 11 原生 DWM 毛玻璃窗口；关闭模块或切换数据上下文时同步解除订阅并关闭详情，避免重复窗口和失效对象残留。
- 番茄钟提供 25/45/60 分钟预设、准确剩余进度、暂停/继续、完成声音和无激活毛玻璃 Toast；悬浮计时器使用与主壳一致的 Windows 原生毛玻璃，不创建全屏覆盖层。
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

![桌面收纳后台布局快照](docs/images/organizer-background-layout.svg)

![桌面收纳分区写入与拖拽锁](docs/images/organizer-partition-mutations.svg)

![桌面文件监视路径级增量刷新](docs/images/organizer-path-refresh.svg)

![只收纳开启后新增的桌面项目](docs/images/organizer-new-items-only.svg)

![大量桌面文件视口虚拟化](docs/images/organizer-viewport-virtualization.svg)

![桌面右键菜单目标安全链路](docs/images/organizer-context-menu-target.svg)

![桌面收纳异步生命周期](docs/images/organizer-async-lifecycle.svg)

![桌面收纳后台准备与导航隔离](docs/images/organizer-background-preparation.svg)

![桌面收纳文件属性后台事务](docs/images/organizer-visibility-background-io.svg)

![Explorer 拖入后台路径预检](docs/images/organizer-drop-preflight.svg)

![桌面文件与文件夹后台打开](docs/images/organizer-shell-open-background.svg)

![桌面拖拽交互锁与异常边界](docs/images/organizer-drag-lifecycle.svg)

![桌面收纳弹层互斥与键盘路径](docs/images/organizer-popup-keyboard.svg)

自动收纳的文件属性事务、监视器通知和布局刷新现在是同一条可观察异步链：服务先释放文件刷新闸门，再执行新增项目收纳；单个文件失败会留在桌面并进入结果摘要，末尾扫描或 SQLite 竞态也只会显示可恢复错误，不会再把异常抛给 WPF 全局退出策略。

![自动收纳异常隔离与防闪退](docs/images/organizer-auto-crash-boundary.svg)

![跨盘安装校验与自动收纳退出闭环](docs/images/install-organizer-safety-closure.svg)

文件较多时，工具栏下方会临时显示实时进度、当前项目和已处理数量；整理按钮同步禁用，防止重复点击排队。手动整理、自动新增收纳和公共桌面授权阶段各自使用独立进度修订，上一阶段迟到的 UI 消息不会覆盖最终摘要。

![桌面整理实时进度与重复触发抑制](docs/images/organizer-progress-feedback.svg)

![任务列表、真实看板与毛玻璃详情](docs/images/task-workspace.svg)

![本地优先 OKR 与飞书同步](docs/images/okr-local-sync.svg)

![番茄钟与原生毛玻璃悬浮计时器](docs/images/pomodoro-focus.svg)

![隐私优先的 AI 助手](docs/images/ai-assistant-workspace.svg)

## 兼容性与视觉

- 推荐 Windows 11 22H2（Build 22621）或更高版本。
- 使用 DWM Desktop Acrylic、圆角和自定义 Fluent 资源，不再依赖 MaterialDesignInXamlToolkit。
- 跟随系统浅色/深色主题；高对比度、关闭透明效果、远程桌面或 DWM 不可用时降级为不透明主题。
- 启用 Per-Monitor V2 DPI，支持多显示器与不同缩放比例。
- 不镜像第三方托盘图标、不读取 Explorer 私有数据，也不重做完整开始菜单或通知中心；窗口预览由公开 DWM 缩略图接口提供。

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

未处理异常会先恢复原生任务栏和桌面图标，再写入 `%LOCALAPPDATA%\FocusPanel\Logs\crash.log` 并安全退出。异常处理不会删除或覆盖业务数据库，也不会把 DLL 当作程序启动。启动时只有在原数据库已成功归档保留、且没有有效备份可用的情况下才会创建新库；归档失败或恢复库仍不兼容时停止启动并保留所有文件，避免用“空库能打开”掩盖数据损失。

普通界面设置位于 `%APPDATA%\FocusPanel\settings.json`，与 Velopack 的只读安装目录和版本目录分离。0.9.43 首次启动会在新文件不存在时读取旧版安装目录中的 `settings.json`，原子复制到新位置，并保留旧文件作为回退；用户自定义图片目录不会被重写，只有旧版默认的安装目录 `Images` 会迁移到 `%APPDATA%\FocusPanel\Images`。设置文件损坏时应用使用安全默认值，保存失败会留下可诊断错误，不会先删除已有配置。

![设置迁移与原子保存](docs/images/settings-migration-safety.svg)

![数据库安全备份与恢复](docs/images/database-restore-safety.svg)

![崩溃与数据库安全恢复](docs/images/crash-recovery-safety.svg)

## 安装包与一键更新

应用目标框架仍为 `.NET 7`。生成 Velopack 安装包时额外需要 `.NET 8 SDK`，它只用于运行打包工具：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\package-release.ps1 `
  -Version 0.10.66 `
  -Dotnet8Path dotnet `
  -PublishDotnetPath dotnet
```

正常迭代不要使用 `-CleanPackages`，这样上一个完整包会保留并生成差分包。只有首次建立本地包目录时才使用 `-CleanPackages`；若同一版本尚未发布、但在最终复核后需要重新构建，使用 `-ReplaceCurrentVersion` 精确替换该版本和共享清单，同时保留上一版本作为差分基线。两个清理开关不能同时使用。

安装包输出到 `artifacts/release/packages/`，其中包括：

- `FocusPanel-win-Setup.exe`：个人设备唯一推荐入口。双击后必须先出现“选择 FocusPanel 安装位置”窗口，可直接输入或浏览到 D/E 盘任意绝对目录；如果没有看到这个窗口，说明运行的不是当前发布包，请删除旧下载后从 Latest Release 重新下载。向导同时设置 MSI 的 `VELOPACK_INSTALLDIR` 与 `INSTALLFOLDER`，安装完成后直接检查所选根目录下的 `current\FocusPanel.exe`，不再依赖 MSI 可能使用 GUID 的卸载注册项；程序若实际落到其他盘会明确报出所选目录和检测目录，绝不把返回代码 0 当成成功。有至少 512MB 可用空间的非系统固定盘时优先推荐其中剩余空间最大的一块；否则才回退当前用户目录。旧版识别会同时枚举 Velopack 名称项和 MSI GUID 项；若旧版位于另一目录，向导会先确认、等待旧卸载注册和程序文件真正释放，再安装到新位置。任务、收纳记录和设置保留在用户 AppData。
- `FocusPanel-win.msi`：标准 Windows Installer，负责当前用户/整机范围与企业部署；任意路径的无人值守部署应同时传入 `VELOPACK_INSTALLDIR` 与 `INSTALLFOLDER`。
- `FocusPanel-0.10.66-full.nupkg`：完整更新包。
- `releases.win.json` 和 `RELEASES`：Velopack 更新清单。
- 后续版本生成的 delta 包：用于减少更新下载量。

![一键安装与自定义目录安装](docs/images/custom-install-location.svg)

![Windows Installer 强制自定义目录](docs/images/msi-install-location-flow.svg)

![跨盘安装校验与自动收纳退出闭环](docs/images/install-organizer-safety-closure.svg)

![目录安装器与自动收纳发布验收](docs/images/setup-organizer-release-verification.svg)

![跨盘落盘与自动整理单次提交](docs/images/install-organizer-single-commit.svg)

安装版和 Velopack 便携版统一使用项目的公开 [GitHub Releases](https://github.com/SakalioLabs/FocusPanel/releases)，无需在每台设备配置更新地址或访问令牌。客户端直接读取 GitHub Latest Release 的静态 `releases.win.json` 和包资产，不调用匿名 Releases API，因此不会因共享 IP 的 API 次数耗尽而收到 403。程序启动后会自动检查一次，之后每 6 小时最多检查一次；发现新版本时更新设置和托盘都会提示，但不会强制重启。

正式发布流程会把当前版本显式设为 GitHub Latest，并回读验证 `releases.win.json`、`RELEASES`、完整更新包、带路径向导的 Setup 和 MSI。Setup 在打包时以特殊探针参数无安装执行，必须返回目录向导专用标识；上传后还会将 GitHub 公开资产的 SHA-256 与本地文件比较，不能再由同名默认安装器悄悄顶替。中文发布说明使用带签名的 Unicode 中间文件，并在打包后与更新清单逐字核对；任何代码页转换或内容损坏都会直接中止发布。验证通过后，另一台设备只要安装过一次，以后即可在设置页直接完成检查、下载、安装和重启。个人设备使用 `Setup.exe` 选择目录，企业部署使用 MSI。设置页同时保留“打开官方下载页”按钮；网络策略、代理或临时服务异常时可以直接下载安装器覆盖升级，业务数据库和 `%APPDATA%` 设置不会被安装包删除。

![GitHub 静态清单一键更新与手动兜底](docs/images/github-static-update-flow.svg)

用户点击“一键检查并安装更新”后，FocusPanel 会显示更新说明、下载完整包或差分包、备份数据库、恢复原任务栏设置，然后重启安装。其他设备首次运行 `Setup.exe` 时即可选择目录；后续版本均沿用同一条更新链，不会因自定义目录而回到默认位置。

![一键更新流程](docs/images/one-click-update.svg)

源码直接运行的开发版不会原地覆盖自身，设置页会提示先安装 `Setup.exe`。

## 多显示器右缘定位

0.10.21 起，Panel、12px 物理热区和 3px 运行指示条默认共同选择虚拟桌面中最外侧的右屏，而不是盲目使用主屏右缘。这样当主屏在左、副屏在右时，呼出位置位于整套显示器的最右边，不再卡在两屏接缝；副屏在左时则仍落在主屏外侧。0.10.27 进一步在移动窗口前直接读取目标显示器的有效 DPI，不再用窗口原来所在屏幕的 DPI 做第一次定位；收到 `WM_DPICHANGED` 后会按同一目标重新锚定。0.10.31 除了“自动：最右侧屏幕 / Windows 主屏”，还会列出每台已连接显示器的主屏标记、分辨率与虚拟桌面坐标，可以精确固定到上下排列、负坐标或三屏中的任意一台。目标设备断开时临时回退主屏但保留选择；目标切换、设备重连、分辨率、主屏或缩放改变时，Panel、热区和指示条都会一起重算。

![双屏外侧右缘选择](docs/images/multi-monitor-edge-target.svg)

![最右侧屏幕与主屏选择](docs/images/display-target-selection.svg)

![设备级显示器选择与断开回退](docs/images/display-device-selection.svg)

将生成的包上传为 GitHub Release 草稿：

```powershell
$env:GITHUB_TOKEN = "仅放在当前终端，不要写入仓库"
.\scripts\publish-github-release.ps1 `
  -Version 0.9.64 `
  -Dotnet8Path dotnet
```

确认后添加 `-Publish` 可正式发布。推送 `v*` 标签或手动运行“构建并发布 Windows 安装包”工作流，也会自动构建、测试、生成差分包并创建 Release。

当前仓库没有提供代码签名证书，因此本地生成的安装包会显示“未知发布者”。正式分发时应通过 `-SignParams` 传入 `signtool.exe` 参数，或在发布工作流中接入 Azure Trusted Signing；不要把证书密码写入仓库。
