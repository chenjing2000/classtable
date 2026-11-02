# 与MySchedule对齐构建入口（2026-11-02）

## 确认的问题

参考 `C:\Projects\MySchedule` 的OneSchedule.Wpf.csproj、Publish.ps1、build-release.bat及run.bat。原仓库缺少双击发布/启动入口，默认开发构建会生成主程序apphost，发布未经过单文件暂存检查，DPI警告策略也不同。

在仅复制版本控制源码、不复制bin/obj/dist的副本中，首次通过批处理调用Windows PowerShell 5.1，实际复现旧Publish.ps1的解析错误：无BOM的UTF-8中文字符串按本地ANSI编码读取，导致引号/字符串结束符被误识别。本机此前PowerShell 7运行成功不能证明Windows自带PowerShell可运行。没有取得用户另一台电脑的原始错误记录，因此不将此认定为其唯一失败原因。

## 修改

- 根目录增加build-release.bat和run.bat，前者检查dotnet、调用Windows PowerShell发布并保留失败退出码；后者仅启动已发布EXE，缺失时明确提示先构建。脚本使用自身路径，从任意当前目录调用均可；构建批处理可转发-Offline。
- 普通主程序构建UseAppHost=false，与MySchedule一致；发布显式UseAppHost=true、Windows x64、框架依赖单文件、无调试符号、包含需要提取的原生库。不携带.NET运行时。
- 同MySchedule抑制WFO0003：DPI由已有WPF manifest管理，WinForms仅用于托盘。不关闭其他警告或TreatWarningsAsErrors。
- 发布先输出obj/portable-publish，验证仅有ClassSchedule.exe后复制；清理明确列出的过期应用二进制及SDK临时apphost。运行中的ClassSchedule需要先从托盘退出；不强杀进程，不清理data、备份或设置。
- 三个构建入口保持纯ASCII，兼容Windows PowerShell 5.1，无需安装PowerShell 7。现有最终路径dist/ClassSchedule.exe保持，避免改变学生数据和启动偏好位置；没有重排Core/WPF项目结构或绑定本机SDK补丁号。

## 验证

1. 当前工作区 `dotnet build ClassSchedule.sln --nologo -m:1` 通过，0警告0错误；MSBuild查询确认UseAppHost=false、SelfContained=false、net10.0-windows和仅额外抑制WFO0003。
2. 使用Git跟踪文件及本轮新增入口建立源码副本，路径含空格，初始不含bin/obj/dist，仍保留全部图标/manifest/项目引用。`dotnet build ClassSchedule.sln`首次还原和编译通过，0警告0错误，主程序目录没有生成EXE。
3. 从另一工作目录运行副本的run.bat：没有发布EXE时退出码1，提示先运行build-release.bat。
4. 从另一工作目录经cmd.exe运行副本的build-release.bat -Offline，实际使用Windows PowerShell 5.1。修订后退出码0，生成最终单文件EXE，临时发布EXE已移除；测试用学生JSON、.bak及程序级设置的SHA256不变。
5. 入口文件非ASCII字符数为0；没有使用PowerShell 7特有语法或已有本机构建输出来完成源码副本的还原与编译。
6. 当前目录通过Windows PowerShell 5.1重新发布成功；真实data及程序设置共3个文件路径/内容校验不变。RuntimeChecks验证框架依赖EXE缺少.NET时显示原生安装提示及下载链接，通过。发布使用-Offline，未执行在线NuGet漏洞检查；git diff --check通过。

源码副本验证仍复用本机.NET/NuGet安装缓存，不等同于在未安装SDK的另一台物理电脑测试；首次联网还原条件见README。此轮仅修改构建与发布，不运行全面UI回归。
