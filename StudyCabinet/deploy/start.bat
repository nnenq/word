@echo off
chcp 65001 >nul
rem Запуск учебного кабинета вручную (вариант 1 — локально, вариант 2 — по локальной сети)
cd /d "%~dp0"
set ASPNETCORE_ENVIRONMENT=Production
echo Электронный учебный кабинет
echo   на этом компьютере:  http://localhost:5000
echo   из локальной сети:   http://^<IP-адрес этого компьютера^>:5000   (ipconfig — строка IPv4)
echo Окно не закрывайте, пока идут занятия. Остановить — Ctrl+C.
StudyCabinet.exe --urls http://0.0.0.0:5000
pause
