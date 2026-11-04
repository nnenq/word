@echo off
chcp 65001 >nul
rem Проверка доступности: сайт отвечает и база данных подключается
curl -s http://localhost:5000/health
echo.
echo Ответ "status":"ok" — всё работает. Нет ответа — запустите start.bat или службу StudyCabinet.
pause
