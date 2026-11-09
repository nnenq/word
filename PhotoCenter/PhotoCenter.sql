/* =====================================================================
   Экзаменационный билет № 14. Информационная система «Фотоцентр».
   СУБД: Microsoft SQL Server 2014 (SQL Server Management Studio 2014).

   КАК ЗАПУСТИТЬ:
   1) Открыть SSMS, подключиться к серверу (например .\SQLEXPRESS или (local)).
   2) Файл -> Открыть -> Файл... -> выбрать этот скрипт (или вставить текст в «Создать запрос»).
   3) Нажать F5 («Выполнить»).
   Скрипт сам удалит старую базу PhotoCenter (если она есть) и создаст всё заново.
   ===================================================================== */

USE master;
GO
IF DB_ID(N'PhotoCenter') IS NOT NULL
BEGIN
    ALTER DATABASE PhotoCenter SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE PhotoCenter;
END
GO
CREATE DATABASE PhotoCenter;
GO
USE PhotoCenter;
GO
SET DATEFORMAT ymd;   -- даты в скрипте записаны как год-месяц-день
GO

/* =====================================================================
   1. ТАБЛИЦЫ
   ===================================================================== */

-- Клиенты организации
CREATE TABLE Clients (
    ClientID    INT IDENTITY(1,1) CONSTRAINT PK_Clients PRIMARY KEY,
    LastName    NVARCHAR(50)  NOT NULL,
    FirstName   NVARCHAR(50)  NOT NULL,
    MiddleName  NVARCHAR(50)  NULL,
    Phone       NVARCHAR(20)  NOT NULL CONSTRAINT UQ_Clients_Phone UNIQUE,
    Email       NVARCHAR(100) NULL,
    RegDate     DATE          NOT NULL CONSTRAINT DF_Clients_RegDate DEFAULT (GETDATE())
);

-- Сотрудники
CREATE TABLE Employees (
    EmployeeID  INT IDENTITY(1,1) CONSTRAINT PK_Employees PRIMARY KEY,
    LastName    NVARCHAR(50)  NOT NULL,
    FirstName   NVARCHAR(50)  NOT NULL,
    MiddleName  NVARCHAR(50)  NULL,
    Position    NVARCHAR(50)  NOT NULL,          -- должность: фотограф, оператор печати, администратор
    Phone       NVARCHAR(20)  NOT NULL,
    HireDate    DATE          NOT NULL CONSTRAINT DF_Employees_HireDate DEFAULT (GETDATE()),
    Salary      DECIMAL(10,2) NOT NULL CONSTRAINT CK_Employees_Salary CHECK (Salary >= 0)
);

-- Оказываемые услуги (прайс-лист)
CREATE TABLE Services (
    ServiceID   INT IDENTITY(1,1) CONSTRAINT PK_Services PRIMARY KEY,
    Name        NVARCHAR(100) NOT NULL CONSTRAINT UQ_Services_Name UNIQUE,
    Description NVARCHAR(200) NULL,
    Price       DECIMAL(10,2) NOT NULL CONSTRAINT CK_Services_Price CHECK (Price > 0),
    Unit        NVARCHAR(20)  NOT NULL CONSTRAINT DF_Services_Unit DEFAULT (N'шт.')
);

-- Заказы и их выполнение
CREATE TABLE Orders (
    OrderID       INT IDENTITY(1,1) CONSTRAINT PK_Orders PRIMARY KEY,
    ClientID      INT          NOT NULL CONSTRAINT FK_Orders_Clients   REFERENCES Clients(ClientID),
    EmployeeID    INT          NOT NULL CONSTRAINT FK_Orders_Employees REFERENCES Employees(EmployeeID),
    OrderDate     DATETIME     NOT NULL CONSTRAINT DF_Orders_OrderDate DEFAULT (GETDATE()),
    DueDate       DATE         NOT NULL,                       -- срок выполнения
    Status        NVARCHAR(20) NOT NULL CONSTRAINT DF_Orders_Status DEFAULT (N'Принят')
                  CONSTRAINT CK_Orders_Status CHECK (Status IN (N'Принят', N'В работе', N'Выполнен', N'Выдан', N'Отменён')),
    CompletedDate DATETIME     NULL,                           -- когда выполнен
    Comment       NVARCHAR(200) NULL
);

-- Состав заказа: какие услуги и сколько
CREATE TABLE OrderItems (
    OrderItemID INT IDENTITY(1,1) CONSTRAINT PK_OrderItems PRIMARY KEY,
    OrderID     INT           NOT NULL CONSTRAINT FK_OrderItems_Orders   REFERENCES Orders(OrderID) ON DELETE CASCADE,
    ServiceID   INT           NOT NULL CONSTRAINT FK_OrderItems_Services REFERENCES Services(ServiceID),
    Quantity    INT           NOT NULL CONSTRAINT CK_OrderItems_Quantity CHECK (Quantity > 0),
    Price       DECIMAL(10,2) NOT NULL CONSTRAINT CK_OrderItems_Price CHECK (Price > 0)   -- цена на момент заказа
);
GO

-- Индексы для быстрого поиска истории заказов
CREATE INDEX IX_Orders_ClientID   ON Orders(ClientID);
CREATE INDEX IX_Orders_EmployeeID ON Orders(EmployeeID);
CREATE INDEX IX_OrderItems_OrderID ON OrderItems(OrderID);
GO

/* =====================================================================
   2. ТЕСТОВЫЕ ДАННЫЕ
   ===================================================================== */

INSERT INTO Clients (LastName, FirstName, MiddleName, Phone, Email, RegDate) VALUES
(N'Иванов',   N'Сергей',   N'Петрович',   N'+373 777 10001', N'ivanov@mail.ru',   '2025-01-15'),
(N'Петрова',  N'Анна',     N'Викторовна', N'+373 777 10002', N'petrova@mail.ru',  '2025-02-03'),
(N'Сидоренко',N'Олег',     N'Иванович',   N'+373 777 10003', NULL,                '2025-03-21'),
(N'Кузнецова',N'Мария',    N'Андреевна',  N'+373 777 10004', N'kuz@gmail.com',    '2025-04-10'),
(N'Морозов',  N'Дмитрий',  NULL,          N'+373 777 10005', NULL,                '2025-05-05'),
(N'Белова',   N'Екатерина',N'Сергеевна',  N'+373 777 10006', N'belova@yandex.ru', '2025-06-18');

INSERT INTO Employees (LastName, FirstName, MiddleName, Position, Phone, HireDate, Salary) VALUES
(N'Смирнов',  N'Алексей', N'Игоревич',   N'Фотограф',          N'+373 778 20001', '2023-09-01', 8000),
(N'Ковальчук',N'Ирина',   N'Павловна',   N'Оператор печати',   N'+373 778 20002', '2024-02-15', 6500),
(N'Гончар',   N'Виктор',  N'Николаевич', N'Фотограф',          N'+373 778 20003', '2024-06-01', 7800),
(N'Лисица',   N'Ольга',   N'Дмитриевна', N'Администратор',     N'+373 778 20004', '2022-11-10', 7000);

INSERT INTO Services (Name, Description, Price, Unit) VALUES
(N'Фото на документы',      N'4 фото 3x4 или 35x45 мм',             60,  N'комплект'),
(N'Печать фото 10x15',      N'Глянцевая или матовая бумага',          3,  N'шт.'),
(N'Печать фото 20x30',      N'Глянцевая или матовая бумага',         15,  N'шт.'),
(N'Портретная фотосессия',  N'1 час в студии, 10 обработанных фото', 500, N'час'),
(N'Выездная фотосъёмка',    N'Свадьбы, выпускные, праздники',       800, N'час'),
(N'Ретушь фотографии',      N'Цветокоррекция и ретушь лица',         40,  N'шт.'),
(N'Реставрация старого фото',N'Восстановление повреждённого снимка', 120, N'шт.'),
(N'Фотокнига',              N'20 разворотов, твёрдая обложка',      450, N'шт.');

INSERT INTO Orders (ClientID, EmployeeID, OrderDate, DueDate, Status, CompletedDate, Comment) VALUES
(1, 1, '2026-10-01T10:15:00', '2026-10-01', N'Выдан',    '2026-10-01T10:40:00', NULL),
(1, 2, '2026-10-20T14:00:00', '2026-10-22', N'Выдан',    '2026-10-21T16:00:00', N'Матовая бумага'),
(2, 3, '2026-10-25T11:00:00', '2026-10-30', N'Выполнен', '2026-10-29T18:00:00', N'Студия, белый фон'),
(3, 2, '2026-11-02T09:30:00', '2026-11-05', N'Выполнен', '2026-11-04T12:00:00', NULL),
(4, 1, '2026-11-03T16:20:00', '2026-11-10', N'В работе', NULL,               N'Свадьба 08.11'),
(5, 4, '2026-11-05T12:00:00', '2026-11-05', N'Выдан',    '2026-11-05T12:20:00', NULL),
(2, 2, '2026-11-06T15:45:00', '2026-11-12', N'В работе', NULL,               N'Фотокнига к юбилею'),
(6, 3, '2026-11-07T10:00:00', '2026-11-14', N'Принят',   NULL,               N'Старое фото 1960-х'),
(1, 3, '2026-11-08T13:30:00', '2026-11-15', N'Принят',   NULL,               NULL),
(3, 1, '2026-11-08T17:00:00', '2026-11-09', N'Отменён',  NULL,               N'Клиент отказался');

INSERT INTO OrderItems (OrderID, ServiceID, Quantity, Price) VALUES
(1, 1, 1, 60),
(2, 2, 40, 3), (2, 3, 5, 15),
(3, 4, 2, 500), (3, 6, 5, 40),
(4, 2, 100, 3),
(5, 5, 4, 800),
(6, 1, 2, 60),
(7, 8, 1, 450), (7, 6, 10, 40),
(8, 7, 3, 120),
(9, 4, 1, 500), (9, 3, 10, 15),
(10, 1, 1, 60);
GO

/* =====================================================================
   3. ПРЕДСТАВЛЕНИЯ (VIEW) — ПРОСМОТР СПИСКА КЛИЕНТОВ И ЗАКАЗОВ
   ===================================================================== */

-- Список клиентов с количеством заказов и суммой
CREATE VIEW vClients AS
SELECT  c.ClientID                                        AS [Код],
        c.LastName + N' ' + c.FirstName + ISNULL(N' ' + c.MiddleName, N'') AS [Клиент],
        c.Phone                                           AS [Телефон],
        c.Email                                           AS [Email],
        c.RegDate                                         AS [Дата регистрации],
        COUNT(DISTINCT o.OrderID)                         AS [Заказов],
        ISNULL(SUM(CASE WHEN o.Status <> N'Отменён' THEN oi.Quantity * oi.Price END), 0) AS [Сумма заказов]
FROM Clients c
LEFT JOIN Orders o      ON o.ClientID = c.ClientID
LEFT JOIN OrderItems oi ON oi.OrderID = o.OrderID
GROUP BY c.ClientID, c.LastName, c.FirstName, c.MiddleName, c.Phone, c.Email, c.RegDate;
GO

-- Список заказов: клиент, сотрудник, статус, сумма
CREATE VIEW vOrders AS
SELECT  o.OrderID                                   AS [Номер заказа],
        o.OrderDate                                 AS [Дата заказа],
        c.LastName + N' ' + c.FirstName             AS [Клиент],
        c.Phone                                     AS [Телефон клиента],
        e.LastName + N' ' + e.FirstName             AS [Сотрудник],
        e.Position                                  AS [Должность],
        o.DueDate                                   AS [Срок],
        o.Status                                    AS [Статус],
        o.CompletedDate                             AS [Выполнен],
        (SELECT SUM(oi.Quantity * oi.Price) FROM OrderItems oi WHERE oi.OrderID = o.OrderID) AS [Сумма],
        o.Comment                                   AS [Комментарий]
FROM Orders o
JOIN Clients c   ON c.ClientID = o.ClientID
JOIN Employees e ON e.EmployeeID = o.EmployeeID;
GO

-- Состав заказов (какие услуги в каждом заказе)
CREATE VIEW vOrderDetails AS
SELECT  oi.OrderID              AS [Номер заказа],
        s.Name                  AS [Услуга],
        oi.Quantity             AS [Количество],
        s.Unit                  AS [Ед.],
        oi.Price                AS [Цена],
        oi.Quantity * oi.Price  AS [Стоимость]
FROM OrderItems oi
JOIN Services s ON s.ServiceID = oi.ServiceID;
GO

/* =====================================================================
   4. ХРАНИМЫЕ ПРОЦЕДУРЫ — ДОБАВЛЕНИЕ И РЕДАКТИРОВАНИЕ ДАННЫХ
   ===================================================================== */

-- ---------- Клиенты ----------
CREATE PROCEDURE sp_AddClient
    @LastName NVARCHAR(50), @FirstName NVARCHAR(50), @MiddleName NVARCHAR(50) = NULL,
    @Phone NVARCHAR(20), @Email NVARCHAR(100) = NULL
AS
BEGIN
    INSERT INTO Clients (LastName, FirstName, MiddleName, Phone, Email)
    VALUES (@LastName, @FirstName, @MiddleName, @Phone, @Email);
    SELECT SCOPE_IDENTITY() AS [Код нового клиента];
END
GO

CREATE PROCEDURE sp_UpdateClient
    @ClientID INT, @LastName NVARCHAR(50), @FirstName NVARCHAR(50), @MiddleName NVARCHAR(50) = NULL,
    @Phone NVARCHAR(20), @Email NVARCHAR(100) = NULL
AS
BEGIN
    UPDATE Clients
    SET LastName = @LastName, FirstName = @FirstName, MiddleName = @MiddleName, Phone = @Phone, Email = @Email
    WHERE ClientID = @ClientID;
    IF @@ROWCOUNT = 0 RAISERROR(N'Клиент с таким кодом не найден', 16, 1);
END
GO

-- ---------- Сотрудники ----------
CREATE PROCEDURE sp_AddEmployee
    @LastName NVARCHAR(50), @FirstName NVARCHAR(50), @MiddleName NVARCHAR(50) = NULL,
    @Position NVARCHAR(50), @Phone NVARCHAR(20), @Salary DECIMAL(10,2)
AS
BEGIN
    INSERT INTO Employees (LastName, FirstName, MiddleName, Position, Phone, Salary)
    VALUES (@LastName, @FirstName, @MiddleName, @Position, @Phone, @Salary);
    SELECT SCOPE_IDENTITY() AS [Код нового сотрудника];
END
GO

CREATE PROCEDURE sp_UpdateEmployee
    @EmployeeID INT, @LastName NVARCHAR(50), @FirstName NVARCHAR(50), @MiddleName NVARCHAR(50) = NULL,
    @Position NVARCHAR(50), @Phone NVARCHAR(20), @Salary DECIMAL(10,2)
AS
BEGIN
    UPDATE Employees
    SET LastName = @LastName, FirstName = @FirstName, MiddleName = @MiddleName,
        Position = @Position, Phone = @Phone, Salary = @Salary
    WHERE EmployeeID = @EmployeeID;
    IF @@ROWCOUNT = 0 RAISERROR(N'Сотрудник с таким кодом не найден', 16, 1);
END
GO

-- ---------- Услуги ----------
CREATE PROCEDURE sp_AddService
    @Name NVARCHAR(100), @Description NVARCHAR(200) = NULL, @Price DECIMAL(10,2), @Unit NVARCHAR(20) = N'шт.'
AS
BEGIN
    INSERT INTO Services (Name, Description, Price, Unit) VALUES (@Name, @Description, @Price, @Unit);
    SELECT SCOPE_IDENTITY() AS [Код новой услуги];
END
GO

CREATE PROCEDURE sp_UpdateService
    @ServiceID INT, @Name NVARCHAR(100), @Description NVARCHAR(200) = NULL, @Price DECIMAL(10,2), @Unit NVARCHAR(20) = N'шт.'
AS
BEGIN
    UPDATE Services SET Name = @Name, Description = @Description, Price = @Price, Unit = @Unit
    WHERE ServiceID = @ServiceID;
    IF @@ROWCOUNT = 0 RAISERROR(N'Услуга с таким кодом не найдена', 16, 1);
END
GO

-- ---------- Заказы ----------
-- Создать заказ (без услуг). Возвращает номер заказа.
CREATE PROCEDURE sp_AddOrder
    @ClientID INT, @EmployeeID INT, @DueDate DATE, @Comment NVARCHAR(200) = NULL
AS
BEGIN
    INSERT INTO Orders (ClientID, EmployeeID, DueDate, Comment)
    VALUES (@ClientID, @EmployeeID, @DueDate, @Comment);
    SELECT SCOPE_IDENTITY() AS [Номер нового заказа];
END
GO

-- Добавить услугу в заказ (цена берётся из прайс-листа)
CREATE PROCEDURE sp_AddOrderItem
    @OrderID INT, @ServiceID INT, @Quantity INT
AS
BEGIN
    INSERT INTO OrderItems (OrderID, ServiceID, Quantity, Price)
    SELECT @OrderID, ServiceID, @Quantity, Price FROM Services WHERE ServiceID = @ServiceID;
    IF @@ROWCOUNT = 0 RAISERROR(N'Услуга с таким кодом не найдена', 16, 1);
END
GO

-- Редактировать заказ (сотрудник, срок, комментарий)
CREATE PROCEDURE sp_UpdateOrder
    @OrderID INT, @EmployeeID INT, @DueDate DATE, @Comment NVARCHAR(200) = NULL
AS
BEGIN
    UPDATE Orders SET EmployeeID = @EmployeeID, DueDate = @DueDate, Comment = @Comment
    WHERE OrderID = @OrderID;
    IF @@ROWCOUNT = 0 RAISERROR(N'Заказ с таким номером не найден', 16, 1);
END
GO

-- Изменить статус выполнения заказа. При статусе «Выполнен» дата выполнения ставится автоматически.
CREATE PROCEDURE sp_SetOrderStatus
    @OrderID INT, @Status NVARCHAR(20)
AS
BEGIN
    UPDATE Orders
    SET Status = @Status,
        CompletedDate = CASE WHEN @Status = N'Выполнен' THEN GETDATE() ELSE CompletedDate END
    WHERE OrderID = @OrderID;
    IF @@ROWCOUNT = 0 RAISERROR(N'Заказ с таким номером не найден', 16, 1);
END
GO

/* =====================================================================
   5. ИСТОРИЯ ЗАКАЗОВ КОНКРЕТНОГО КЛИЕНТА ИЛИ СОТРУДНИКА
   ===================================================================== */

CREATE PROCEDURE sp_ClientOrderHistory @ClientID INT
AS
BEGIN
    SELECT o.OrderID AS [Номер заказа], o.OrderDate AS [Дата], s.Name AS [Услуга],
           oi.Quantity AS [Кол-во], oi.Quantity * oi.Price AS [Стоимость],
           o.Status AS [Статус], e.LastName + N' ' + e.FirstName AS [Сотрудник]
    FROM Orders o
    JOIN OrderItems oi ON oi.OrderID = o.OrderID
    JOIN Services s    ON s.ServiceID = oi.ServiceID
    JOIN Employees e   ON e.EmployeeID = o.EmployeeID
    WHERE o.ClientID = @ClientID
    ORDER BY o.OrderDate DESC;
END
GO

CREATE PROCEDURE sp_EmployeeOrderHistory @EmployeeID INT
AS
BEGIN
    SELECT o.OrderID AS [Номер заказа], o.OrderDate AS [Дата],
           c.LastName + N' ' + c.FirstName AS [Клиент], o.Status AS [Статус],
           (SELECT SUM(oi.Quantity * oi.Price) FROM OrderItems oi WHERE oi.OrderID = o.OrderID) AS [Сумма]
    FROM Orders o
    JOIN Clients c ON c.ClientID = o.ClientID
    WHERE o.EmployeeID = @EmployeeID
    ORDER BY o.OrderDate DESC;
END
GO

/* =====================================================================
   6. ПРОВЕРКА — ЭТИ ЗАПРОСЫ ПОКАЗЫВАТЬ ПРЕПОДАВАТЕЛЮ
   (выделить нужную строку мышкой и нажать F5 — выполнится только она)
   ===================================================================== */

-- 1) Просмотр списка клиентов и заказов
SELECT * FROM vClients;
SELECT * FROM vOrders ORDER BY [Дата заказа] DESC;

-- 2) Добавление и редактирование (пример)
-- EXEC sp_AddClient N'Новиков', N'Павел', N'Олегович', N'+373 777 10007', N'novikov@mail.ru';
-- EXEC sp_UpdateClient 7, N'Новиков', N'Павел', N'Олегович', N'+373 777 99999', NULL;
-- EXEC sp_AddEmployee N'Руденко', N'Анна', NULL, N'Фотограф', N'+373 778 20005', 7500;
-- EXEC sp_AddService N'Фото на холсте', N'Печать на холсте 30x40', 350, N'шт.';
-- EXEC sp_AddOrder 7, 1, '2026-11-20', N'Срочно';      -- вернёт номер заказа, например 11
-- EXEC sp_AddOrderItem 11, 2, 20;                      -- в заказ 11 добавить 20 фото 10x15
-- EXEC sp_SetOrderStatus 11, N'Выполнен';

-- 3) История заказов конкретного клиента или сотрудника
EXEC sp_ClientOrderHistory 1;      -- все заказы клиента Иванова
EXEC sp_EmployeeOrderHistory 3;    -- все заказы фотографа Гончара
GO
