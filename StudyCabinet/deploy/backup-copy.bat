@echo off
chcp 65001 >nul
rem Копирование резервных копий на другой носитель (правило 3-2-1).
rem Копии создаются приложением автоматически каждый день в 21:00 (App_Data\backups).
rem Укажите свою папку на другом диске или флешке:
set TARGET=D:\StudyCabinet-Backups
if not exist "%TARGET%" mkdir "%TARGET%"
robocopy "%~dp0App_Data\backups" "%TARGET%" backup-*.zip /XO /NFL /NDL /NJH
echo Копии скопированы в %TARGET%
