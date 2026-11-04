@echo off
chcp 65001 >nul
rem Сборка в конфигурации Release и публикация в папку C:\StudyCabinet.
rem Запускать из папки deploy. Если приложение работает как служба — сначала остановите её: sc stop StudyCabinet
set TARGET=C:\StudyCabinet
echo Публикация в %TARGET% ...
dotnet publish "%~dp0..\StudyCabinet\StudyCabinet.csproj" -c Release -o "%TARGET%" || goto :error
for %%f in (start.bat check.bat backup-copy.bat backup-sqlserver.bat install-service.bat uninstall-service.bat firewall.bat) do copy /y "%~dp0%%f" "%TARGET%\" >nul
echo.
echo Готово. Приложение опубликовано в %TARGET%
echo Данные (база, файлы лекций, копии) хранятся в %TARGET%\App_Data и при обновлении не затираются.
pause
exit /b 0
:error
echo Ошибка публикации. Проверьте, что установлен .NET 8 SDK (dotnet --version).
pause
exit /b 1
