# 今天列高亮与悬浮提示验证（2026-11-03）

## 实现

MainWindow按WeekCalendar计算的完整日期匹配本地今天：表头#FFE7BE、正文#FFF2D8。只修改已有日期单元格的Background，保留课程块、双节跨度、事件与表格身份。每分钟检查日期，激活及重新显示时补查；日期未变直接返回，关闭停止日期计时器。重新生成课表时重新登记日期单元格；显示周不含今天时不高亮，不自动切换浏览周。

DialogWindow以WindowSurface Grid包住原DockPanel和普通消息层。消息底部居中、圆角10 DIP、轻阴影，文字固定14 DIP且完全不透明，背景Brush.Opacity=.65，即透明度35%。固定页脚、确认区尺寸变化时调整浮层底部留白，避免覆盖操作按钮。长文本换行并按可用高度滚动，不参与正文占位。StudentWorkspaceWindow的XAML内容重新接入同一根容器。

消息队列及约5秒FIFO规则未改。删除、保存/退出等交互确认仍在原DockPanel底部固定占位，等待用户选择，不套用浮层透明背景或定时关闭。

## 针对性检查

先新增检查并运行原代码，确认3项失败：没有今天表头高亮，消息显示缩小课表，未使用半透明叠加层。实现后构建0警告0错误。

```powershell
dotnet build tests/ClassSchedule.UiChecks -m:1 --no-restore --nologo
dotnet tests/ClassSchedule.UiChecks/bin/Debug/net10.0-windows/ClassSchedule.UiChecks.dll --today-toast
dotnet tests/ClassSchedule.UiChecks/bin/Debug/net10.0-windows/ClassSchedule.UiChecks.dll --layout-stability
dotnet tests/ClassSchedule.UiChecks/bin/Debug/net10.0-windows/ClassSchedule.UiChecks.dll --confirmations
dotnet tests/ClassSchedule.UiChecks/bin/Debug/net10.0-windows/ClassSchedule.UiChecks.dll --reminder-popup-layout
dotnet tests/ClassSchedule.UiChecks/bin/Debug/net10.0-windows/ClassSchedule.UiChecks.dll --tray-startup
```

分别27、15、16、13、36项，共107项通过。

- 跨日移动高亮、昨日复原、完整日期避免跨月误匹配、两个周日归属、浏览周不跳转、恢复显示补查、8/10节与双节跨度。
- 日期更新不重建单元格、不改变课程色；普通消息不改变课表或各窗口正文高度。
- 浮层居中、文字14 DIP/不透明、背景透明度35%；用真实队列计时验证第一条到下一条及最终隐藏。
- 确认区仍固定，按钮右对齐、默认否、可命中和操作；最小窗口下长提示有滚动并避开确认按钮。
- 总体设置、课程管理、时段、提醒窗口共用悬浮层；原有课表圆角、提醒窗口字号/布局、托盘恢复及退出确认直接关联路径保持。

测试使用临时学生目录，跨日通过窗口内的本地日期提供函数模拟，定时和激活调用同一刷新方法。没有等待真实午夜、修改系统日期或执行全面功能回归；没有覆盖所有DPI组合的人工视觉验收。日期刷新最大常规检查间隔1分钟，UI忙或休眠恢复后通过下一次检查/显示补查。

通过Windows PowerShell 5.1执行Publish.ps1 -Offline，已更新dist/ClassSchedule.exe；3个真实data/设置文件路径及SHA256保持。仍不集成.NET；离线发布未执行在线NuGet漏洞检查。git diff --check通过，本轮未提交GitHub。
