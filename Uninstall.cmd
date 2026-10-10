@echo off
setlocal
cd /d "%~dp0"
echo This removes the installed ROTH Unity project folder.
set /p A=Continue? [Y/N] 
if /I not "%A%"=="Y" exit /b 1
cd /d "%TEMP%"
rmdir /s /q "%~dp0" 2>nul
