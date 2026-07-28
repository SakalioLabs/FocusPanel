# FocusPanel 0.9.80

- 任务图片保存目录不再使用老式 WinForms 树形 FolderBrowserDialog，改为 Windows Shell 现代通用文件夹选择器。
- 选择器支持当前目录定位、FocusPanel 窗口 Owner、中文标题和“使用此文件夹”确认按钮。
- 打开选择器期间保持 Panel 展开；取消不会修改设置，Shell 或保存失败会恢复原路径并显示中文错误。
- 新增可替换系统边界及取消、失败、Owner、交互锁和路径决策测试，自动测试不会弹出真实窗口。
