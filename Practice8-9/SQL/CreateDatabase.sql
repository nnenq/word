/*
    Практическое занятие № 8-9. База данных пользователей.
    Выполнить в SQL Server Management Studio: Файл → Открыть → этот файл → Выполнить (F5).
    Создаёт базу UsersDB, таблицы «СекретныйВопрос» и «Пользователь» и заполняет список секретных вопросов.
*/

IF DB_ID(N'UsersDB') IS NULL
    CREATE DATABASE UsersDB;
GO

USE UsersDB;
GO

-- Секретные вопросы для восстановления пароля
IF OBJECT_ID(N'dbo.СекретныйВопрос', N'U') IS NULL
CREATE TABLE dbo.СекретныйВопрос
(
    КодСекретногоВопроса INT IDENTITY(1, 1) NOT NULL   -- идентификатор: начальное значение 1, шаг 1
        CONSTRAINT PK_СекретныйВопрос PRIMARY KEY,
    СекретныйВопрос      NVARCHAR(100) NOT NULL
        CONSTRAINT UQ_СекретныйВопрос UNIQUE
);
GO

-- Пользователи
IF OBJECT_ID(N'dbo.Пользователь', N'U') IS NULL
CREATE TABLE dbo.Пользователь
(
    КодПользователя        INT IDENTITY(1, 1) NOT NULL  -- идентификатор: начальное значение 1, шаг 1
        CONSTRAINT PK_Пользователь PRIMARY KEY,
    Фамилия                NVARCHAR(50)  NOT NULL,
    Имя                    NVARCHAR(50)  NOT NULL,
    ЭлектроннаяПочта       NVARCHAR(100) NOT NULL       -- используется как логин
        CONSTRAINT UQ_Пользователь_Почта UNIQUE,
    Пароль                 NVARCHAR(100) NOT NULL,      -- хранится хеш пароля SHA-256, а не сам пароль
    КодовоеСлово           NVARCHAR(50)  NOT NULL,
    ОтветНаСекретныйВопрос NVARCHAR(100) NOT NULL,
    КодСекретногоВопроса   INT           NOT NULL
        CONSTRAINT FK_Пользователь_СекретныйВопрос
        REFERENCES dbo.СекретныйВопрос (КодСекретногоВопроса),
    ДатаРегистрации        DATETIME2(0)  NOT NULL
        CONSTRAINT DF_Пользователь_ДатаРегистрации DEFAULT (SYSDATETIME())
);
GO

-- Список секретных вопросов
IF NOT EXISTS (SELECT 1 FROM dbo.СекретныйВопрос)
INSERT INTO dbo.СекретныйВопрос (СекретныйВопрос) VALUES
    (N'Девичья фамилия матери'),
    (N'Кличка вашего первого питомца'),
    (N'Город, в котором вы родились'),
    (N'Название вашей первой школы'),
    (N'Любимая книга детства');
GO

SELECT * FROM dbo.СекретныйВопрос;
GO
