@echo off
setlocal enabledelayedexpansion

:: Pfade definieren
set "FIVEM_PATH=%LocalAppData%\FiveM\FiveM.app"
set "MOD_SOURCE=..\fivem-data"
set "VERSION_FILE=..\version.md"

:: Version auslesen
if exist "%VERSION_FILE%" (
    set /p MOD_VERSION=<"%VERSION_FILE%"
) else (
    set "MOD_VERSION=Unbekannt"
)

title Nicki's World Mod Installer - v%MOD_VERSION%

echo ==================================================
echo      Nicki's World Grafik Mod Installer
echo      Version: %MOD_VERSION%
echo ==================================================
echo.

:: Prüfen ob FiveM installiert ist
if not exist "%FIVEM_PATH%" (
    echo [FEHLER] FiveM wurde nicht gefunden unter:
    echo %FIVEM_PATH%
    echo Bitte stelle sicher, dass FiveM korrekt installiert ist.
    pause
    exit /b
)

echo [INFO] Installiere Grafik-Mod Dateien...
xcopy "%MOD_SOURCE%\*" "%FIVEM_PATH%\" /s /e /y /i

echo.
echo [ERFOLG] Die Mod wurde erfolgreich installiert!
pause