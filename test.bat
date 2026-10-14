@echo off
rem Uruchamia testy logiki i zapisuje wynik do test-log.txt.
cd /d "%~dp0"
set DOTNET_ROLL_FORWARD=Major
echo TEST START %date% %time% > test-log.txt
dotnet test tests\ArcSperanzaIntel.Tests -nologo >> test-log.txt 2>&1
echo EXIT %ERRORLEVEL% >> test-log.txt
