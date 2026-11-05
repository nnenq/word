@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo Obnovlenie iz GitHub...
git pull origin claude/compassionate-fermat-o75h0o
if errorlevel 1 (
  echo.
  echo Ne udalos obnovit. Skopiruyte tekst vyshe i otpravte Claude.
) else (
  echo.
  echo Gotovo, vse fayly obnovleny.
)
pause
