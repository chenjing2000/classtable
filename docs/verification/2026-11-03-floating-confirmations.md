# 交互确认卡片悬浮（2026-11-03）

## 实现

按后续确认方案，DialogWindow的交互确认从正文DockPanel移入WindowSurface叠加层，底部居中，正文/表格不因确认显示而变小。确认使用与主窗口相同的WindowBackground资源（#F5F8FD），完全不透明；主窗口本身保持原色。卡片圆角、轻阴影，正文和按钮显式14 DIP，原按钮顺序、右对齐、默认否或继续编辑及选择回调保持。

确认卡片位于固定页脚上方，普通临时提示在确认卡片上方，至少8 DIP间隔。两者并存时为普通消息预留可见空间，再按实际高度分配确认区域；不依赖第一次测量已有高度，避免长确认占满窗口而使新错误不可见。确认仅文字区域滚动，操作按钮留在卡片内固定可见；普通提示保留原35%透明背景、5秒FIFO与滚动。

窗口层优先接收悬浮卡片内的滚轮，避免总体设置的整页滚轮处理把卡片滚动重定向到底层页面。确认不加入临时消息队列，不自动消失、不自动选择。删除、取消、保存失败及退出阻止规则保持。

## 针对性验证

```powershell
dotnet build tests/ClassSchedule.UiChecks -m:1 --no-restore --nologo
dotnet tests/ClassSchedule.UiChecks/bin/Debug/net10.0-windows/ClassSchedule.UiChecks.dll --floating-confirmations
dotnet tests/ClassSchedule.UiChecks/bin/Debug/net10.0-windows/ClassSchedule.UiChecks.dll --confirmations
dotnet tests/ClassSchedule.UiChecks/bin/Debug/net10.0-windows/ClassSchedule.UiChecks.dll --tray-startup
dotnet tests/ClassSchedule.UiChecks/bin/Debug/net10.0-windows/ClassSchedule.UiChecks.dll --today-toast
```

新增检查先确认原版3项失败：确认仍占位、列表缩小、背景不是主窗口颜色；移入浮层后通过。随后复现设置页卡片滚轮被整页拦截的2项失败，修正后通过。

构建0警告0错误。新增18项、原确认16项、托盘退出36项、日期/提示27项，共97项通过。

- 确认层不挤压列表/正文；背景与主窗口共享且不透明；字体14 DIP，默认焦点及按钮右对齐、命中正确。
- 普通提示在上、确认在下，留有间隔；固定页脚和两类卡片按钮均可操作。
- 等待临时消息超时，确认仍保留且未触发任何选择。
- 最小窗口下长确认和长错误同时可见，文字各自滚动，确认按钮保持可见；显式否只关闭确认，不清除普通提示。
- 三选项保存/放弃/继续编辑顺序及安全焦点保持；设置页滚轮只滚卡片内容，页面偏移不变。
- 删除是/否、退出逐层保存、叉号/Esc不绕过退出选择、保存失败、日期二次确认与原有临时消息FIFO均保持。

测试使用临时学生文件，不改真实启动项；未运行全套测试或全面人工回归，没有穷举所有DPI或屏幕尺寸。前一份日期/普通提示记录中“确认固定”对应当时方案，现以本次确认悬浮规则为准。

已使用 Windows PowerShell 5.1 执行 Publish.ps1 -Offline，成功更新 dist/ClassSchedule.exe；发布前后真实学生 data 与设置共3个文件的 SHA-256 一致。离线发布未运行在线 NuGet 漏洞审计。本轮未提交 GitHub。
