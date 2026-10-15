$ErrorActionPreference = 'Stop'
dotnet build validation/WindowsChecks.csproj -c Release --nologo -warnaserror
if ($LASTEXITCODE -ne 0) { throw 'Windows checks build failed' }
$checks = Join-Path $PSScriptRoot '../validation/bin/Release/net48/WindowsChecks.exe'
$stdout = Join-Path ([IO.Path]::GetTempPath()) 'sleep-timer-checks.stdout.log'
$stderr = Join-Path ([IO.Path]::GetTempPath()) 'sleep-timer-checks.stderr.log'
$process = Start-Process $checks -PassThru -NoNewWindow -RedirectStandardOutput $stdout -RedirectStandardError $stderr
$finished = $process.WaitForExit(120000)
if (-not $finished) { $process.Kill(); $process.WaitForExit() }
Get-Content $stdout
if ((Get-Item $stderr).Length -gt 0) { Get-Content $stderr }
if (-not $finished) { throw 'Windows UI checks exceeded 120 seconds; see the last CHECKPOINT in the log' }
if ($process.ExitCode -ne 0) { throw "Windows UI checks failed: $($process.ExitCode)" }
