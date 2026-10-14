@echo off
rem Buduje solucje i zapisuje wynik do build-log.txt (czyta go Claude).
cd /d "%~dp0"
echo BUILD START %date% %time% > build-log.txt
dotnet build ArcSperanzaIntel.sln -c Debug --no-incremental -nologo -clp:NoSummary;ErrorsOnly >> build-log.txt 2>&1
echo EXIT %ERRORLEVEL% >> build-log.txt
