# 托盘、启动与退出确认验证（2026-11-02）

## 实现与原因

- `StartupSettings`参考MySchedule的具体设置服务，处理程序级JSON、外部修改保护、原子替换及当前用户Run注册；首次缺失默认启用，取消后保留，失败记录错误并尝试回滚。Debug注入空注册回调，测试使用临时文件及可控回调，不更改用户真实启动项。发布版注册带引号的ClassSchedule.exe路径及`--startup`。
- `TrayService`参考MySchedule的NotifyIcon、WPF圆角菜单及隐藏/恢复流程，使用bee资源。主窗口关闭或最小化隐藏，手动启动显示，自动启动透明不激活、完成Loaded后隐藏。延迟隐藏发现用户已恢复窗口时不再隐藏；提醒服务在后台隐藏后初始化，避免到期提醒随主窗口被隐藏。恢复不永久置顶，跳过已关闭的OwnedWindow，保留当前进程内的编辑链。
- `SingleInstance`仍以原有互斥与窗口消息激活原进程，激活回调复用托盘Restore，二次实例不创建托盘或写启动设置。
- `ExitCoordinator`从内到外处理编辑窗口。`DialogWindow`底部固定是/否确认、默认否、右对齐，未选择时阻止叉号/Esc绕过；普通页脚按钮暂停但编辑内容可修正。具体保存仍在原ViewModel或时段接受回调中执行；保存失败不继续。时段先进入父课程草稿，父课程再确认持久保存；日期变更的额外确认保留。全部处理后先Dispose托盘，再显式Shutdown，OnExit释放提醒和互斥。
- 主菜单首项开机启动使用MenuItem的原生可勾选状态，模板在右侧绘制勾选框。移除160 DIP最小宽，实测120 DIP。WPF菜单自带Separator模板预留左图标栏，原测量左右36/6 DIP；显式应用公共Separator样式后两条线均12/12 DIP。
- 学生schema2不变，不保存或恢复跨进程草稿；未引入日历组件、DI、消息总线或通用设置框架。新增WinForms仅使用共享Windows Desktop运行时中的NotifyIcon，不集成.NET。

## 验证命令及结果

```powershell
dotnet build tests/ClassSchedule.UiChecks -m:1 --no-restore --nologo
dotnet tests/ClassSchedule.UiChecks/bin/Debug/net10.0-windows/ClassSchedule.UiChecks.dll --tray-startup --tray-smoke
dotnet tests/ClassSchedule.UiChecks/bin/Debug/net10.0-windows/ClassSchedule.UiChecks.dll --button-feedback
dotnet tests/ClassSchedule.UiChecks/bin/Debug/net10.0-windows/ClassSchedule.UiChecks.dll --confirmations
dotnet tests/ClassSchedule.UiChecks/bin/Debug/net10.0-windows/ClassSchedule.UiChecks.dll --course-reminders
dotnet tests/ClassSchedule.UiChecks/bin/Debug/net10.0-windows/ClassSchedule.UiChecks.dll --layout-stability
./Publish.ps1 -Offline
./tests/RuntimeChecks.ps1
git diff --check
```

构建0警告0错误。定向界面检查依次36、10、16、33、15项，共110项通过。

新增36项覆盖默认/关闭/重载、注册失败回滚、回滚也失败时明确报错、损坏文件与外部修改保护、菜单顺序和自适应宽度、分隔线对称、托盘菜单、叉号/最小化隐藏、自动启动不激活与手动唤醒竞态、真实第二测试进程恢复隐藏实例、隐藏主窗口时新提醒可见、过期提醒不复活、真实ShowDialog嵌套链恢复、从内到外确认、默认否且靠右、叉号/Esc不绕过、保存失败可修正重试、否放弃、学生新增和总体设置日期确认、托盘退出回调。

`--tray-smoke`短暂创建真实NotifyIcon；学生、偏好文件均位于临时目录。其余测试保留共享按钮、底部确认、提醒计时与主课表布局的直接关联行为。

离线框架依赖发布成功，`dist/data`中2个文件的路径及SHA256发布前后一致。RuntimeChecks通过隔离子进程验证缺少.NET的原生提示及下载链接，未改本机运行时或启动项。离线发布未执行在线NuGet漏洞检查。

## 全面代码审查

采用[项目code review skill](../skills/project-code-review/SKILL.md)，本人检查整体差异、视图/状态/文档，同时两名独立只读审查员分别覆盖生命周期与启动/退出，以及Core/存储/提醒/ViewModels/普通视图。未发现未解决的正确性必需修复；重点检查项包括失败回滚、数据指纹、所有者链、模态确认、事件释放、提醒生命周期和MVVM职责。审查建议的冷启动竞态、隐藏提醒与过期窗口、回滚失败验证均已补充。已有公开但无调用的方法未为减少行数擅自删除，无额外架构重构，未删除资源或用户文件。

本批提交同时包含此前待提交日志中的4 DIP编辑框Padding及按钮按下/键盘焦点修订。Git提交说明根据日志与实际差异整理，不提交发布输出、真实数据或程序级设置。

## 边界

遵循用户约定，没有运行所有无参数测试或全面人工功能回归。未重启Windows验证真实登录自动启动，未覆盖所有系统托盘策略、DPI组合和真实鼠标外部点击；启动注册的失败场景采用隔离回调，未对真实HKCU Run写入测试值。托盘外侧点击收起的实现沿用MySchedule的前台Popup机制，自动检查实际打开并关闭WPF菜单。完整发布EXE未被启动以避免修改真实启动项；其布局/生命周期通过同一生产类的WPF测试与子进程单实例检查验证。
