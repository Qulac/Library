-- ============================================================
-- Инициализация БД "Домашняя библиотека" (идемпотентный скрипт).
-- Безопасен для повторного запуска: существующие объекты и данные
-- не удаляются, создаётся только то, чего ещё нет.
--
-- Таблица Books + хранимые процедуры insert/update/delete/select/search
-- + триггеры валидации полей книги.
-- Оглавление книги хранится в XML-поле Toc:
--   <toc><![CDATA[ ...HTML-разметка оглавления... ]]></toc>
-- ============================================================

-- Обязательно для XML-индексов и процедур с XML-методами
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF DB_ID(N'HomeLibrary') IS NULL
    CREATE DATABASE HomeLibrary;
GO

USE HomeLibrary;
GO

-- ------------------------------------------------------------
-- Таблица (создаётся только если её ещё нет)
-- ------------------------------------------------------------
IF OBJECT_ID(N'dbo.Books', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Books
    (
        BookId       INT IDENTITY(1,1)  NOT NULL CONSTRAINT PK_Books PRIMARY KEY,
        Title        NVARCHAR(300)      NOT NULL,          -- Название
        Author       NVARCHAR(200)      NOT NULL,          -- Автор
        PublishYear  INT                NULL,              -- Год издания
        Publisher    NVARCHAR(200)      NULL,              -- Издательство
        Isbn         NVARCHAR(20)       NULL,              -- ISBN
        Category     NVARCHAR(100)      NULL,              -- Жанр/категория
        Description  NVARCHAR(MAX)      NULL,              -- Аннотация
        Toc          XML                NULL,              -- Оглавление (XML)
        CreatedAt    DATETIME2(0)       NOT NULL CONSTRAINT DF_Books_CreatedAt DEFAULT SYSDATETIME(),
        ModifiedAt   DATETIME2(0)       NOT NULL CONSTRAINT DF_Books_ModifiedAt DEFAULT SYSDATETIME()
    );
END
GO

-- ------------------------------------------------------------
-- Индексы (создаются только если их ещё нет)
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Books_Title' AND object_id = OBJECT_ID(N'dbo.Books'))
    CREATE INDEX IX_Books_Title ON dbo.Books (Title);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Books_Author' AND object_id = OBJECT_ID(N'dbo.Books'))
    CREATE INDEX IX_Books_Author ON dbo.Books (Author);
GO

-- Индекс по XML-полю для поиска по оглавлению
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Books_Toc' AND object_id = OBJECT_ID(N'dbo.Books'))
    CREATE PRIMARY XML INDEX IX_Books_Toc ON dbo.Books (Toc);
GO

-- ------------------------------------------------------------
-- Добавление книги. Новый Id возвращается через OUTPUT-параметр.
-- ------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.usp_Book_Insert
    @Title       NVARCHAR(300),
    @Author      NVARCHAR(200),
    @PublishYear INT            = NULL,
    @Publisher   NVARCHAR(200)  = NULL,
    @Isbn        NVARCHAR(20)   = NULL,
    @Category    NVARCHAR(100)  = NULL,
    @Description NVARCHAR(MAX)  = NULL,
    @Toc         XML            = NULL,
    @BookId      INT            OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Books (Title, Author, PublishYear, Publisher, Isbn, Category, Description, Toc)
    VALUES (@Title, @Author, @PublishYear, @Publisher, @Isbn, @Category, @Description, @Toc);
    SET @BookId = SCOPE_IDENTITY();
END
GO

-- ------------------------------------------------------------
-- Изменение книги.
-- ------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.usp_Book_Update
    @BookId      INT,
    @Title       NVARCHAR(300),
    @Author      NVARCHAR(200),
    @PublishYear INT            = NULL,
    @Publisher   NVARCHAR(200)  = NULL,
    @Isbn        NVARCHAR(20)   = NULL,
    @Category    NVARCHAR(100)  = NULL,
    @Description NVARCHAR(MAX)  = NULL,
    @Toc         XML            = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Books
    SET Title       = @Title,
        Author      = @Author,
        PublishYear = @PublishYear,
        Publisher   = @Publisher,
        Isbn        = @Isbn,
        Category    = @Category,
        Description = @Description,
        Toc         = @Toc,
        ModifiedAt  = SYSDATETIME()
    WHERE BookId = @BookId;
END
GO

-- ------------------------------------------------------------
-- Удаление книги.
-- ------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.usp_Book_Delete
    @BookId INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.Books WHERE BookId = @BookId;
END
GO

-- ------------------------------------------------------------
-- Выборка одной книги по Id.
-- ------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.usp_Book_GetById
    @BookId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT BookId, Title, Author, PublishYear, Publisher, Isbn, Category, Description, Toc
    FROM dbo.Books
    WHERE BookId = @BookId;
END
GO

-- ------------------------------------------------------------
-- Выборка всех книг (для списка).
-- ------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.usp_Book_GetAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT BookId, Title, Author, PublishYear, Publisher, Isbn, Category, Description, Toc
    FROM dbo.Books
    ORDER BY Title;
END
GO

-- ------------------------------------------------------------
-- Поиск по названию, автору и оглавлению (XML).
-- @SearchTerm — подстрока; ищем в Title, Author и в текстовых узлах Toc.
-- Поиск НЕ зависит от регистра:
--   * LIKE — LOWER() с обеих сторон (независимо от коллации БД);
--   * XQuery — стандартная функция lower-case() (contains() в XQuery
--     всегда чувствителен к регистру,LOWER()/коллация на него не влияют).
-- ------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.usp_Book_Search
    @SearchTerm NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT BookId, Title, Author, PublishYear, Publisher, Isbn, Category, Description, Toc
    FROM dbo.Books
    WHERE LOWER(Title)  LIKE N'%' + LOWER(@SearchTerm) + N'%'
       OR LOWER(Author) LIKE N'%' + LOWER(@SearchTerm) + N'%'
       -- поиск по текстовым узлам XML-оглавления без учёта регистра
       OR Toc.exist('//*[contains(lower-case(.), lower-case(sql:variable("@SearchTerm")))]') = 1
    ORDER BY Title;
END
GO

-- ------------------------------------------------------------
-- Триггеры валидации полей книги.
-- Срабатывают при INSERT/UPDATE (в том числе через хранимые
-- процедуры usp_Book_Insert / usp_Book_Update). При нарушении
-- ограничений транзакция откатывается, операция прерывается,
-- клиент получает ошибку (в приложении — SqlException).
-- ------------------------------------------------------------
CREATE OR ALTER TRIGGER dbo.TR_Books_Validate_Fields
ON dbo.Books
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    -- Название и автор: обязательны, не могут состоять из одних пробелов
    IF EXISTS (
        SELECT 1 FROM inserted
        WHERE Title  IS NULL OR LTRIM(RTRIM(Title))  = N''
           OR Author IS NULL OR LTRIM(RTRIM(Author)) = N''
    )
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50001, N'Название и автор книги обязательны и не могут быть пустыми.', 1;
    END;

    -- Год издания: обязателен для указания (поле не может быть NULL)
    IF EXISTS (
        SELECT 1 FROM inserted
        WHERE PublishYear IS NULL
    )
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50002, N'Год издания обязателен для указания.', 1;
    END;

    -- Год издания: разумный диапазон (в т.ч. отсекает 0, введённый "как есть")
    IF EXISTS (
        SELECT 1 FROM inserted
        WHERE PublishYear < 1000 OR PublishYear > 2100
    )
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50005, N'Год издания должен быть в диапазоне 1000–2100.', 1;
    END;

    -- ISBN: только цифры, латинская X/x, дефисы и пробелы (если указан)
    IF EXISTS (
        SELECT 1 FROM inserted
        WHERE Isbn IS NOT NULL AND Isbn LIKE '%[^0-9Xx -]%'
    )
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50003, N'ISBN может содержать только цифры, латинскую X, дефисы и пробелы.', 1;
    END;
END;
GO

-- ------------------------------------------------------------
-- Валидация XML-оглавления: корневой элемент должен называться "toc"
-- (корректность самого XML гарантируется типом данных XML).
-- ------------------------------------------------------------
CREATE OR ALTER TRIGGER dbo.TR_Books_Validate_Toc
ON dbo.Books
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1 FROM inserted
        WHERE Toc IS NOT NULL
          AND Toc.value('local-name(/*[1])', 'nvarchar(50)') <> N'toc'
    )
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50004, N'Оглавление (Toc) должно быть XML с корневым элементом "toc".', 1;
    END;
END;
GO

-- ------------------------------------------------------------
-- Демонстрационные данные (добавляются один раз — только если
-- таблица пуста; при повторном запуске данные не дублируются)
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.Books)
BEGIN
    DECLARE @toc1 XML = N'<toc><![CDATA[<h3>Оглавление</h3><ol><li>Введение</li><li>Типы данных</li><li>Индексы</li><li>Хранимые процедуры</li></ol>]]></toc>';
    DECLARE @toc2 XML = N'<toc><![CDATA[<h3>Оглавление</h3><ol><li>Часть I. Основы</li><li>Часть II. LINQ</li><li>Часть III. async/await</li></ol>]]></toc>';
    DECLARE @id INT;

    EXEC dbo.usp_Book_Insert N'Microsoft SQL Server. Полное руководство', N'Пол Нильсен', 2019,
         N'Диалектика', N'978-5-907203-44-7', N'Базы данных',
         N'Классический труд по администрированию и разработке под SQL Server.', @toc1, @id OUT;

    EXEC dbo.usp_Book_Insert N'C# 8.0. Полный справочник', N'Герберт Шилдт', 2020,
         N'Вильямс', N'978-5-8459-2117-3', N'Программирование',
         N'Исчерпывающий справочник по языку C#.', @toc2, @id OUT;
END
GO
