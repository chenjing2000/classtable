# 不捆绑.NET的发布验证

- Publish.ps1使用--self-contained false、PublishSingleFile=true，项目明确SelfContained=false；移除仅自包含运行库需要的自解压和压缩选项。
- dist仅更新ClassSchedule.exe，不清空data。目标环境需要.NET 10 Windows Desktop Runtime x64。原生apphost在WPF启动前提示安装缺失运行时并提供下载链接；不另外增加启动器。该提示无法由尚未启动的WPF底部信息栏承载。
- 离线发布成功，EXE大小296596字节，生成的runtimeconfig声明Microsoft.NETCore.App与Microsoft.WindowsDesktop.App 10.0.0框架依赖，没有includedFrameworks。
- 对发布EXE子进程设置临时空DOTNET_ROOT_X64、DOTNET_ROOT及DOTNET_MULTILEVEL_LOOKUP=0，禁用该测试子进程GUI错误后捕获到非零退出码2147516547、You must install .NET、Architecture x64、.NET location Not found和官方下载链接。环境仅对子进程生效，未卸载或修改本机.NET。
- 该检查证明缺失运行时的宿主错误信息；没有在无.NET实体电脑上手动验证对话框，也不证明所有运行时版本回滚策略。
- 正常环境中将EXE复制到临时空目录运行，2秒后进程仍正常运行，再终止本次测试进程并清理临时目录。未在该测试中编辑真实学生。
- 完整构建0警告0错误，核心30组检查通过。UI代码未修改，本次没有重跑全部窗口视觉矩阵。
- 发布前后真实dist/data路径和SHA-256一致。离线发布未执行在线NuGet漏洞检查。

参考：https://learn.microsoft.com/en-us/dotnet/core/runtime-discovery/troubleshoot-app-launch
