# Служебные скрипты

| Файл | Назначение |
|---|---|
| `test.js` | Комплексный автотест (Playwright): 12 проверок из задания + дополнительные, скриншоты для отчёта. Запуск: `node test.js <папка проекта StudyCabinet> <папка для результатов>` |
| `answers.py` | Правильные ответы из базы SQLite для автотеста |
| `sqlserver.js`, `answers_sql.py` | Проверка работы на SQL Server |
| `diagrams.html`, `render.js` | Схема БД, алгоритм открытия лекций, схема сети (Mermaid → PNG) |
| `report.js` | Генерация отчёта Word: `node report.js <папка Отчёт> <results.json> <файл.docx>` |
