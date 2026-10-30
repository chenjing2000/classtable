# 学生课程表实施计划

此文件保留首次实施记录；后续变化见[紧凑界面](compact-ui-plan.md)与[学生管理修订](student-management-plan.md)，当前规范位于requirements目录。

> 执行方式：当前会话直接实现，按 executing-plans、test-driven-development 和 verification-before-completion 逐项验证，最后独立审查。规范见 [program-design.md](program-design.md)。

目标：实现可运行的WPF课程表，保护学生数据并落实日期、模式和消息规则。

技术：.NET 10、WPF、System.Text.Json；核心独立类库，测试采用无外部依赖的可执行断言程序，避免网络包依赖。

全局约束：EXE旁data、每学生独立JSON；classesPerDay=8/10、classesUnit=one/two、classPeriods；课程模式固定；地点可空；周日转换保持实际日期；错误警告仅窗口底部约5秒队列；不引入额外通用功能。

## 任务1：核心模型与规则

文件：src/ClassTimetable.Core/{Models,Domain}、tests/ClassTimetable.Tests。

接口：StudentDocument及Clone；WeekCalendar.Date/CurrentWeek/ChangeSunday；ClassTimeGenerator.Generate；WeekSelectionParser.Parse；TimetableValidator.Validate。

- [x] 先编写日期边界、转换日期集合不变、周次解析、课时生成、冲突和模式校验断言，运行确认失败。
- [x] 实现模型、严格校验及无WPF依赖的规则。
- [x] dotnet run --project tests/ClassTimetable.Tests：所有断言通过。

## 任务2：存储与通知状态

文件：src/ClassTimetable.Core/Storage/StudentFileStore.cs、Presentation/NotificationQueue.cs。

接口：Load(name)返回文档+指纹；Scan返回有效学生及错误；Save(name, document, expectedFingerprint)返回新快照；NotificationQueue.Enqueue/Advance/Dispose。

- [x] 先验证非法JSON保留、首次创建不覆盖、外部变更拒绝、学生隔离、备份和FIFO可控时间。
- [x] 实现完整候选校验、临时文件原子替换、规范路径、通知生命周期。
- [x] 重跑全部断言。

## 任务3：应用与编辑流程

文件：src/ClassTimetable/{ViewModels,Views}、App.xaml、主题资源。

接口：WorkspaceViewModel维护当前文档/指纹/周次；编辑VM持有草稿，只有Save成功才发布；视图管理owner、绑定、焦点和模态。

- [x] 建立WPF工程，主表动态显示7列8/10行，支持切周、双击和学生切换。
- [x] 实现新增学生、单页设置、课程和时段编辑、管理课程、窗口底部通知及内联确认。
- [x] 实现取消隔离、模式锁定、窗口关闭释放和失败保留草稿。
- [x] dotnet build：无编译错误；核心全套测试仍通过。

## 任务4：可运行交付与审查

- [x] 运行原生UI，覆盖创建、配置、课程保存重开、周日切换、底部消息。
- [x] 独立审阅职责、数据保护和交互；修复实质问题，重跑相应验证。
- [x] 增加运行/发布说明、保持data的发布脚本、更新文档状态。
- [x] 最终构建和测试通过后报告证据及未测边界，不将构建视为UI全通过。

## Review Focus

外部修改文件不能被旧快照覆盖；周日首末周保持日期；子编辑取消不泄漏；通知等待时不提前过期；保存失败不关闭编辑器。前两项在任务1/2断言，后三项通过VM状态断言与任务4原生UI检查。

## 执行记录

Ruling：无Git仓库，使用当前用户指定目录，不初始化Git、不提交推送；保留此计划作为执行记录。

Ruling：用户要求开始程序，采用设计建议作为第一版默认值；若边界不合适可在后续修订，不扩展范围。


执行完成：18组检查通过；最终单节点解决方案构建零警告零错误；自包含发布版完成原生启动和重载验证。详见verification/2026-10-29.md。
