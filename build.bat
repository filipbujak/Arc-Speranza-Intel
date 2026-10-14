@echo off
rem Czysty build: usuwa stare bin/obj, buduje od zera i zapisuje wynik do build-log.txt.
cd /d "%~dp0"
echo BUILD START %date% %time% > build-log.txt
for %%P in (src\ArcSperanzaIntel.App src\ArcSperanzaIntel.Core tests\ArcSperanzaIntel.Tests) do (
  if exist "%%P\bin" rmdir /s /q "%%P\bin"
  if exist "%%P\obj" rmdir /s /q "%%P\obj"
)
dotnet build ArcSperanzaIntel.sln -c Debug -nologo -clp:NoSummary;ErrorsOnly >> build-log.txt 2>&1
echo EXIT %ERRORLEVEL% >> build-log.txt
