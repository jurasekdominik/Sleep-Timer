$ErrorActionPreference = 'Stop'
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    dotnet build validation/WindowsChecks.csproj -c Release --nologo -warnaserror
    if ($LASTEXITCODE -ne 0) { throw 'Windows validation build failed' }
    & ./validation/bin/Release/net48/WindowsChecks.exe
    if ($LASTEXITCODE -ne 0) { throw 'Windows checks failed' }
} finally { Pop-Location }
