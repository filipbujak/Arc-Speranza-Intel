@echo off
rem Gotowa aplikacja do folderu "publish" (exe + DLL obok). Wymaga .NET 9 Desktop Runtime.
cd /d "%~dp0"
echo PUBLISH START %date% %time% > publish-log.txt
dotnet publish src\ArcSperanzaIntel.App -c Release -r win-x64 --self-contained false -o publish -nologo >> publish-log.txt 2>&1
echo EXIT %ERRORLEVEL% >> publish-log.txt
