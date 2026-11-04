@echo off
chcp 65001 >nul
rem Только если база данных в SQL Server (Database:Provider = SqlServer).
rem Полная копия базы средствами SQL Server. Имя сервера — как в SQL Server Management Studio.
set SERVER=.\SQLEXPRESS
for /f %%d in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd-HHmm"') do set D=%%d
set FILE=%~dp0App_Data\backups\StudyCabinet_%D%.bak
sqlcmd -S %SERVER% -E -Q "BACKUP DATABASE [StudyCabinet] TO DISK = N'%FILE%' WITH INIT, CHECKSUM"
echo Копия базы: %FILE%
rem Восстановление: sqlcmd -S %SERVER% -E -Q "RESTORE DATABASE [StudyCabinet] FROM DISK = N'путь\к\файлу.bak' WITH REPLACE"
pause
