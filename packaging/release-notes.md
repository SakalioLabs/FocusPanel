# FocusPanel 0.11.43

- 搜索“快捷设置 / Win+A / 键盘 / Win+Space”现在直接进入 Panel 的网络或输入法详情，不再发送系统快捷键。
- 状态按钮右键新增网络、应用音量和输入法三个 Panel 原生入口；Windows 通知中心继续明确标注为系统表面。
- 删除 QuickSettings、InputSwitcher 和 TaskView 的状态服务、ViewModel 与虚拟键残留链。
- 完整收纳界面运行时实测 720px 工作区为 6 列、664px 可用宽度，防止图标模式再次退化为一行一个。

升级前仍会恢复原生任务栏；现有 SQLite 数据、桌面文件路径、自定义分区和用户设置不会被删除。
