param([switch]$Offline)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'src/ClassTimetable/ClassTimetable.csproj'
$destination = Join-Path $PSScriptRoot 'dist'
$publishArguments = @('publish', $project, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'false', '-p:PublishSingleFile=true', '-p:DebugType=None', '-o', $destination, '--nologo')
if ($Offline) { $publishArguments += @('--ignore-failed-sources', '-p:NuGetAudit=false') }
# 不清空输出目录，避免删除EXE旁已有的学生data。
& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) { throw "发布失败，退出码 $LASTEXITCODE。现有data未清理。" }
Write-Output "EXE: $(Join-Path $destination 'ClassTimetable.exe')"
if ($Offline) { Write-Output '使用离线缓存发布，未执行在线NuGet漏洞检查。' }
