# Электронный учебный кабинет преподавателя и студента

Индивидуальное практическое задание. Кирчу Игорь.
Веб-приложение на **ASP.NET Core 8 (Razor Pages) + Entity Framework Core + Bootstrap 5**.
Преподаватель размещает лекции, контрольные вопросы и тесты, студент изучает лекции **строго по порядку**:
следующая лекция открывается только после выполнения условий предыдущей (ответы на вопросы, тест не ниже порога,
проверка развёрнутого ответа). Ограничение работает **на сервере** — по прямой ссылке закрытую лекцию не открыть (ответ 403).

## Быстрый запуск (Visual Studio)

1. Установите **Visual Studio 2022** с компонентом «ASP.NET и разработка веб-приложений» (.NET 8).
2. Откройте `StudyCabinet.sln` и нажмите **F5**. При первом запуске скачаются пакеты (нужен интернет).
3. Откроется `http://localhost:5090`. База данных `StudyCabinet/App_Data/study.db` (SQLite) создаётся сама с демо-данными.

| Роль | Логин | Пароль |
|---|---|---|
| Преподаватель | `teacher` | `Teacher123` |
| Студент (Кирчу Игорь) | `student` | `Student123` |
| Студенты | `petrova`, `sidorov`, `kuznetsova`, `orlov` | `Student123` |

Чтобы начать с чистыми демо-данными — остановите сайт и удалите папку `StudyCabinet/App_Data`.

## База данных: SQLite или SQL Server

По умолчанию — **SQLite** (один файл, ничего ставить не нужно). Чтобы работать с **SQL Server**, в `StudyCabinet/appsettings.json`:

```json
"Database": {
  "Provider": "SqlServer",
  "SqlServer": "Server=NNENQ\\SQLKI;Database=StudyCabinet;Trusted_Connection=True;TrustServerCertificate=True"
}
```

Имя сервера — то же, что в SQL Server Management Studio (обратная косая черта в JSON пишется дважды).
База `StudyCabinet` со всеми таблицами и демо-данными создаётся при первом запуске.
SQL-скрипт создания базы (таблицы, первичные и внешние ключи, ограничения) — `Database/create_database_sqlserver.sql`.

## Внедрение на компьютер преподавателя

1. Установить **.NET 8 Runtime (ASP.NET Core Runtime, Hosting Bundle)** — на компьютере, где не будет Visual Studio. Для сборки нужен .NET 8 SDK.
2. Выполнить `deploy\publish.bat` — сборка Release и публикация в `C:\StudyCabinet`.
3. Вариант 1 (локально): `C:\StudyCabinet\start.bat`, открыть `http://localhost:5000`.
4. Вариант 2 (локальная сеть): от имени администратора `firewall.bat` (порт 5000 только для частной сети),
   затем `install-service.bat` — приложение работает как служба Windows и запускается вместе с компьютером.
   Студенты открывают `http://<IP-адрес компьютера преподавателя>:5000`. Порт базы данных не открывается.
5. Проверка: `check.bat` (ответ `"status":"ok"`) и вход под `teacher`. **Сразу смените пароли демо-учётных записей.**

Интернет: открывать приложение в интернет без HTTPS, сертификата, резервного копирования и дополнительной защиты нельзя.

## Сопровождение

| Что | Где |
|---|---|
| Доступность сайта и базы | `/health`, `check.bat`, «Управление → Состояние системы» |
| Журнал ошибок | `App_Data/logs/app-ГГГГММДД.log`, последние строки — на странице «Состояние системы» |
| Журнал действий преподавателя | «Управление → Журнал событий» |
| Резервные копии | автоматически каждый день в 21:00 (`Backup:DailyAt`), хранятся 14 последних (`Backup:Keep`), папка `App_Data/backups`; вручную — «Управление → Резервные копии» |
| Копия на другой носитель | `backup-copy.bat` в планировщике заданий (правило 3-2-1) |
| SQL Server | `backup-sqlserver.bat` — `BACKUP DATABASE`, восстановление — `RESTORE DATABASE` |
| Восстановление (SQLite) | «Резервные копии → Восстановить» (перед этим автоматически делается копия текущего состояния) |
| Обновление | сделать копию → `sc stop StudyCabinet` → `publish.bat` → `sc start StudyCabinet` → `check.bat`; данные в `App_Data` не затираются |

## Структура

```
StudyCabinet/
  Program.cs              настройка: база данных, вход, роли, защита разделов, /health, служба Windows
  Models/Entities.cs      сущности: Users, StudentGroups, GroupMemberships, Disciplines, Topics, Lectures,
                          ControlQuestions, Tests, TestQuestions, AnswerOptions, StudentAnswers, TestAttempts,
                          LectureProgress, AuditLog
  Data/AppDb.cs           связи, ключи, ограничения целостности
  Data/Seed.cs            демо-данные
  Services/StudyService   последовательное открытие лекций и условия прохождения (главная логика)
  Services/Grading        автоматическая проверка ответов (5 типов вопросов)
  Services/MaterialStore  хранение файлов лекций вне wwwroot, просмотр DOCX
  Services/BackupService, AutoBackupService, FileLogger, Audit — сопровождение
  Pages/Study             кабинет студента: дисциплины, лекции, тесты, результаты
  Pages/Teacher           панель преподавателя
  Pages/Materials         выдача файла лекции с проверкой прав
deploy/                   скрипты внедрения для Windows
Database/                 SQL-скрипт создания базы для SQL Server
Отчёт/                    отчёт, скриншоты, схемы
```
