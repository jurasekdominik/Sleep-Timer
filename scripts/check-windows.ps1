$ErrorActionPreference = 'Stop'
dotnet build validation/WindowsChecks.csproj -c Release --nologo -warnaserror
if ($LASTEXITCODE -ne 0) { throw 'Windows checks build failed' }
$checks = Join-Path $PSScriptRoot '../validation/bin/Release/net48/WindowsChecks.exe'
$process = Start-Process $checks -PassThru -NoNewWindow
if (-not $process.WaitForExit(120000)) {
    $process.Kill()
    throw 'Windows UI checks exceeded 120 seconds; see the last CHECKPOINT in the log'
}
if ($process.ExitCode -ne 0) { throw "Windows UI checks failed: $($process.ExitCode)" }
