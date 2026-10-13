@echo off
rem Builds SleepTimer.exe (C#, .NET Framework 4.8 - included in Windows 10/11, nothing to install for the exe).
rem Building needs the .NET SDK: winget install Microsoft.DotNet.SDK.8
setlocal
cd /d "%~dp0"

dotnet build SleepTimer.csproj -c Release -o bin\Release
if errorlevel 1 goto :failed

copy /y bin\Release\SleepTimer.exe SleepTimer.exe >nul
if errorlevel 1 (echo Could not replace SleepTimer.exe - is it still running? & goto :failed)

echo.
echo Build OK: %~dp0SleepTimer.exe
pause
exit /b 0

:failed
echo.
echo BUILD FAILED - see the messages above.
pause
exit /b 1
