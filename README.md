# 学生课程表

Windows WPF桌面程序：每名学生独立学期配置与课程，支持每日8/10节、统一单/双节、多周次多地点安排。

- 直接运行发布后的 `dist/ClassTimetable.exe`，Windows x64依赖框架单文件，不捆绑.NET；需要.NET 10 Windows Desktop Runtime（x64）。缺少运行时时，原生.NET启动宿主提示安装并提供下载入口。
- “新增学生”只填写姓名，默认“蜡笔小新”；默认单节。学期和课时通过“总体设置”调整。课程数为0时可改模式，有课程时锁定；删除全部课程并保存后可再改。
- 双击空白格新建课程，双击课程块编辑整门课程；“管理课程”可找到其他周的课程。
- 课表滚轮切周，主窗口不显示左右切周按钮。所有错误/警告在当前窗口底部依次显示约5秒。
- Enter保存/确定，Esc取消或进入窗口内放弃修改确认。课时表编辑中的Enter优先提交单元格。
- 主窗口和总体设置默认600×600 DIP；独立对话框与直接父窗口居中。新增学生是480×300的姓名小窗。课时预览只在总体设置显示，刷新直接执行，无确认或成功提示；错误仍在底部显示。
- 设置页滚轮统一滚动整页，日历浮层内则每次切一个整月，仍只可选周一。时间下拉无箭头、最多4项、细滚动条；滚轮不改选值。
- 管理课程为“课程名/周次/时间/地点”四列，同课时段聚拢、名称纵向合并，按周次与时间排序。双击空白新建，双击课程组编辑，下方仅新建/删除。
- 周次输入如1-5, 7, 8；时间使用401（周四第1节）或30102（周三第1/2节），星期仅一位、周日7。课程编辑540×550、时段窗口320×340 DIP；双击时段表空白新增、实际行编辑。
- 全程序滚动条统一11 DIP，下拉文字14 DIP、浅色圆角悬停；参数标签14 DIP、注释12 DIP。时段窗口横排对齐，课程表选中使用半透明浅蓝柔光，管理表按课程组交替底色并保留原课程色。新增学生简化为学生选择右侧的＋。
- 课程颜色为#RRGGBB文本框和色块预览，新课程优先从已有色相最大空档随机选柔和颜色。

数据位于正在运行的EXE旁 `data/姓名.json`，更新保留上一份 `姓名.bak`。请把程序放到可写目录。文件出错或被外部修改时程序拒绝覆盖；备份恢复须先关闭程序并手动备份原文件。不要同时用外部编辑器写入文件。

## 开发和验证

需要.NET 10 SDK和Windows：

```powershell
dotnet build src/ClassTimetable/ClassTimetable.csproj
dotnet run --project tests/ClassTimetable.Tests
dotnet build tests/ClassTimetable.UiChecks -m:1
dotnet tests/ClassTimetable.UiChecks/bin/Debug/net10.0-windows/ClassTimetable.UiChecks.dll
dotnet run --project src/ClassTimetable/ClassTimetable.csproj
./Publish.ps1
./tests/RuntimeChecks.ps1
```

测试为无第三方测试框架依赖的可执行断言程序，失败时返回非零退出码。RuntimeChecks检查已发布EXE的缺失.NET提示，使用临时副本及子进程环境，不修改本机运行时或真实学生数据。断网且已缓存所需运行时包时可用 `./Publish.ps1 -Offline`；该选项明确跳过在线NuGet漏洞检查。

发布脚本不删除已有data。调试、发布各自使用输出目录旁的数据，不自动复制真实学生文件。

设计、数据契约、验收与验证记录见 [docs](docs/README.md)。
