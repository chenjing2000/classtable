# 课程表文档

范围为Windows WPF与简单MVVM、学生单学期、每日8/10节、每人统一单节或双节。当前操作与构建入口见[项目说明](../README.md)；待提交内容见[待提交日志](../CHANGELOG_PENDING.md)。历史验证记录反映各自当时版本，当前规则以requirements与用户最新决定为准。

## 阅读顺序

1. [产品与交互需求](requirements/requirements.md)：已确认的功能范围与界面规则。
2. [数据格式](requirements/data-model.md)：JSON字段、完整示例和保存约束。
3. [验收清单](requirements/acceptance.md)：实现后应逐项验证的行为。
4. [程序设计方案](design/program-design.md)：职责划分、状态流程、存储及验证设计；末节列出实现采用的默认选择。
5. [UI参考说明](ui/README.md)：四张概念图及其局限。
6. [项目代码审查 skill](skills/project-code-review/SKILL.md)：实现与审查时的质量准则。

## 按主题查阅验证记录

| 主题 | 记录 |
|---|---|
| 代码审查与学生扫描错误整改 | [本批审查](verification/2026-11-02-review-cleanup.md) |
| 跨电脑构建与Windows PowerShell 5.1 | [构建方式对齐](verification/2026-11-02-build-parity.md) |
| 提醒样式、OK/Later及事件级追加次数 | [提醒弹窗](verification/2026-11-02-reminder-popup.md) |
| 托盘、自动启动及退出保存确认 | [托盘启动](verification/2026-11-02-tray-startup.md) |
| 主课表稳定性与课程编辑布局 | [布局稳定](verification/2026-11-02-layout-stability.md) |
| 鼠标按下与键盘焦点 | [按钮反馈](verification/2026-11-02-button-feedback.md) |
| 五行时段表及更早实现 | [紧凑时段表](verification/2026-11-01-compact-slots.md)、[历史验证目录](verification/) |

计划与演进记录保留在design，实际验证范围及未验证项见各记录；提交工作约定见[项目说明](../README.md#待提交日志约定)。

## 规范优先级

用户在对话中的最新明确决定优先；requirements目录维护已确认规则，design目录维护实现方案及待审阅建议。图片仅提供视觉参考，不作为示例数据或行为规范。尚未批准的设计建议不能覆盖已确认需求。

## 目录职责

| 目录 | 内容 |
|---|---|
| requirements/ | 产品需求、数据契约、验收标准 |
| design/ | 程序架构与流程设计、实施记录 |
| verification/ | 测试、审查和原生UI验证记录 |
| ui/ | 原始概念参考图及说明 |
| skills/ | 项目开发与审查规范 |

原交接包内容已迁入上述目录，重复ZIP已核验后删除。后续直接维护这里的文档，不另保留一套旧交接规范。
