param([switch]$Offline)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'src/ClassSchedule/ClassSchedule.csproj'
$staging = Join-Path $PSScriptRoot 'obj/portable-publish'
$destination = Join-Path $PSScriptRoot 'dist'
if (Get-Process ClassSchedule -ErrorAction SilentlyContinue) { throw 'Please save changes and exit ClassSchedule from its tray menu before publishing.' }
$publishArguments = @('publish', $project, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'false', '-p:UseAppHost=true', '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true', '-p:DebugType=None', '-p:DebugSymbols=false', '-o', $staging, '--nologo')
if ($Offline) { $publishArguments += @('--ignore-failed-sources', '-p:NuGetAudit=false') }
& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) { throw "Publish failed (exit code $LASTEXITCODE). Existing data was not removed." }
$files = @(Get-ChildItem -LiteralPath $staging -File)
if ($files.Count -ne 1 -or $files[0].Name -ne 'ClassSchedule.exe') { throw 'Single-file publish produced unexpected files.' }
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $staging 'ClassSchedule.exe') -Destination (Join-Path $destination 'ClassSchedule.exe') -Force
# Remove only obsolete application binaries and the SDK's temporary apphosts; preserve data and settings.
foreach ($relative in @('dist/ClassSchedule.dll', 'dist/ClassSchedule.deps.json', 'dist/ClassSchedule.runtimeconfig.json',
    'src/ClassSchedule/bin/Release/net10.0-windows/win-x64/ClassSchedule.exe',
    'src/ClassSchedule/obj/Release/net10.0-windows/win-x64/apphost.exe', 'obj/portable-publish/ClassSchedule.exe')) {
    $target = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot $relative))
    if (-not $target.StartsWith($PSScriptRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Build artifact path escaped project.' }
    if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Force }
}
Write-Output "EXE: $(Join-Path $destination 'ClassSchedule.exe')"
if ($Offline) { Write-Output 'Published using offline caches; online NuGet vulnerability audit was skipped.' }
