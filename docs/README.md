# 学生课程表文档

第一版范围：Windows WPF + 简单 MVVM，学生单学期、每日8/10节、每人统一单节或双节。当前包含600 DIP紧凑界面、仅姓名新增学生、零课程可改模式及四列分组管理表。运行见[项目说明](../README.md)，最新结果见[代码审查记录](verification/2026-10-30-code-review.md)，历史结果见[运行时发布验证](verification/2026-10-30-framework-dependent.md)、[内容高度验证](verification/2026-10-30-content-fit.md)、[统一UI验证](verification/2026-10-30-unified-ui.md)、[课程分组验证](verification/2026-10-30-grouped-course-table.md)、[学生管理验证](verification/2026-10-29-student-management.md)、[紧凑界面验证](verification/2026-10-29-compact-ui.md)和[初版验证](verification/2026-10-29.md)。

## 阅读顺序

1. [产品与交互需求](requirements/requirements.md)：已确认的功能范围与界面规则。
2. [数据格式](requirements/data-model.md)：JSON字段、完整示例和保存约束。
3. [验收清单](requirements/acceptance.md)：实现后应逐项验证的行为。
4. [程序设计方案](design/program-design.md)：职责划分、状态流程、存储及验证设计；末节列出实现采用的默认选择。
5. [UI参考说明](ui/README.md)：四张概念图及其局限。
6. [项目代码审查 skill](skills/project-code-review/SKILL.md)：实现与审查时的质量准则。

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
