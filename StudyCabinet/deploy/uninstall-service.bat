@echo off
chcp 65001 >nul
rem Удаление службы (данные в App_Data остаются). Запускать от имени администратора.
sc stop StudyCabinet
sc delete StudyCabinet
pause
