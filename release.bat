@echo off
rem Lokalny instalator (Setup.exe) do folderu "releases". Na GitHubie robi to samo workflow Release po tagu v*.
rem Uzycie: release.bat 1.0.0
cd /d "%~dp0"
set VERSION=%1
if "%VERSION%"=="" set VERSION=1.0.0
echo RELEASE START %date% %time% > release-log.txt
dotnet tool update -g vpk >> release-log.txt 2>&1
dotnet publish src\ArcSperanzaIntel.App -c Release -r win-x64 --self-contained true -p:Version=%VERSION% -o publish -nologo >> release-log.txt 2>&1
vpk pack -u ArcSperanzaIntel -v %VERSION% -p publish -e ArcSperanzaIntel.exe --packTitle "ARC Speranza Intel" --packAuthors "Filip Bujak" -i src\ArcSperanzaIntel.App\Assets\app.ico -o releases >> release-log.txt 2>&1
echo EXIT %ERRORLEVEL% >> release-log.txt
