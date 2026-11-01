param([string]$Executable = (Join-Path $PSScriptRoot '../dist/ClassSchedule.exe'))
$ErrorActionPreference = 'Stop'
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$checkDirectory = Join-Path $temporaryRoot ('ClassSchedule-runtime-' + [Guid]::NewGuid().ToString('N'))
$process = $null
try {
    New-Item -ItemType Directory -Path $checkDirectory | Out-Null
    $copy = Join-Path $checkDirectory 'ClassSchedule.exe'
    Copy-Item -LiteralPath $Executable -Destination $copy
    $start = [Diagnostics.ProcessStartInfo]::new($copy)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardError = $true
    $start.RedirectStandardOutput = $true
    # Only this isolated child sees an empty runtime location; installed .NET is untouched.
    $start.Environment['DOTNET_ROOT_X64'] = $checkDirectory
    $start.Environment['DOTNET_ROOT'] = $checkDirectory
    $start.Environment['DOTNET_MULTILEVEL_LOOKUP'] = '0'
    $start.Environment['DOTNET_DISABLE_GUI_ERRORS'] = '1'
    $process = [Diagnostics.Process]::Start($start)
    $errorText = $process.StandardError.ReadToEndAsync()
    $outputText = $process.StandardOutput.ReadToEndAsync()
    if (-not $process.WaitForExit(10000)) { throw 'Missing-runtime check did not exit; verify framework-dependent packaging.' }
    $message = $errorText.GetAwaiter().GetResult()
    $null = $outputText.GetAwaiter().GetResult()
    if ($process.ExitCode -eq 0 -or $message -notmatch 'You must install .NET' -or $message -notmatch 'Architecture: x64' -or $message -notmatch 'https://aka.ms/dotnet') {
        throw "Missing-runtime host message was not found: $message"
    }
    Write-Output 'PASS: framework-dependent x64 host reports missing .NET and provides a download link.'
}
finally {
    if ($null -ne $process) { if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() }; $process.Dispose() }
    $resolved = [IO.Path]::GetFullPath($checkDirectory)
    if (-not $resolved.StartsWith($temporaryRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected cleanup path.' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
