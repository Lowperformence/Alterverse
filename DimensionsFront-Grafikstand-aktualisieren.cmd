@echo off
setlocal
title DIMENSIONS FRONT - Grafikstand aktualisieren

set "PREVIEW=C:\GitHub\Alterverse-World-Preview"
set "BRANCH=art/worldwide-visual-pass"
set "GIT=git"

if not exist "%PREVIEW%\.git" (
  echo Unity-Preview ist kein Git-Projekt oder wurde nicht gefunden: %PREVIEW%
  goto failed
)

where git >nul 2>&1
if errorlevel 1 (
  set "GIT="
  for /d %%D in ("%LOCALAPPDATA%\GitHubDesktop\app-*") do (
    if exist "%%~fD\resources\app\git\cmd\git.exe" set "GIT=%%~fD\resources\app\git\cmd\git.exe"
  )
)
if "%GIT%"=="" (
  echo Git wurde nicht gefunden. GitHub Desktop muss installiert sein.
  goto failed
)

echo Hole den aktuellen Grafikzweig direkt im Unity-Preview-Projekt ...
"%GIT%" -C "%PREVIEW%" fetch origin %BRANCH%
if errorlevel 1 goto failed

echo Sichere lokale Preview-Aenderungen ...
"%GIT%" -C "%PREVIEW%" stash push -u -m "Preview vor Grafikupdate"
if errorlevel 1 goto failed

echo Aktualisiere nur den Preview-Ordner ...
"%GIT%" -C "%PREVIEW%" switch --detach origin/%BRANCH%
if errorlevel 1 goto failed

echo.
echo Fertig. Aktueller Stand:
"%GIT%" -C "%PREVIEW%" log -1 --format=%%h%%x20%%s
echo Du kannst Unity mit dem Projekt %PREVIEW% wieder starten.
pause
exit /b 0

:failed
echo.
echo Die Aktualisierung wurde gestoppt. Bitte sende einen Screenshot dieses Fensters.
pause
exit /b 1
