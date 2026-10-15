$ErrorActionPreference = 'Stop'
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    dotnet build validation/WindowsChecks.csproj -c Release --nologo -warnaserror
    if ($LASTEXITCODE -ne 0) { throw 'Windows validation build failed' }
    $process = Start-Process ./validation/bin/Release/net48/WindowsChecks.exe -NoNewWindow -PassThru
    if (-not $process.WaitForExit(180000)) {
        $process.Kill()
        throw 'Windows checks timed out after 3 minutes; see the last CHECK/PREVIEW marker'
    }
    if ($process.ExitCode -ne 0) { throw "Windows checks failed: $($process.ExitCode)" }
} finally { Pop-Location }
