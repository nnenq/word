@echo off
chcp 65001 >nul
rem Установка как службы Windows: приложение запускается вместе с компьютером и перезапускается при сбое.
rem Запускать от имени администратора из папки C:\StudyCabinet
set DIR=%~dp0
sc create StudyCabinet binPath= "\"%DIR%StudyCabinet.exe\" --urls http://0.0.0.0:5000" start= auto DisplayName= "Электронный учебный кабинет"
sc description StudyCabinet "Электронный учебный кабинет преподавателя и студента"
sc failure StudyCabinet reset= 86400 actions= restart/60000/restart/60000/restart/60000
sc start StudyCabinet
echo Служба установлена и запущена. Проверка: check.bat
pause
