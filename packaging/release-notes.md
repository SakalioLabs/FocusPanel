# FocusPanel 0.9.20

- 停止隐藏 `Shell_TrayWnd` 和强写 Windows 工作区，移除 Explorer 与 FocusPanel 的持续争用和任务栏闪烁。
- 改用 Windows 官方原生任务栏自动隐藏状态，保留快捷设置、通知、输入法和托盘宿主。
- 恢复验证失败时保留会话供 watchdog 重试，不再提前丢失恢复信息。
- 修复桌面收纳拖拽后自动滚动 timer 未停止、页面持续向上滚动的问题。
