@echo off
chcp 65001 >nul
rem Разрешить подключения студентов к порту 5000 только из частной (локальной) сети. Запускать от имени администратора.
netsh advfirewall firewall add rule name="Электронный учебный кабинет (TCP 5000)" dir=in action=allow protocol=TCP localport=5000 profile=private
echo Правило добавлено. Сеть компьютера должна быть отмечена как «Частная».
echo Порт базы данных (SQL Server 1433) НЕ открывайте — студенты работают только через браузер.
pause
