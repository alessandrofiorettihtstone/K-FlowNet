/*
    K-Flow schema reference for ASP.NET Core migration.
    Prefer restoring KFlow.bak when SQL Server permissions allow it; this script is a non-destructive reference scaffold.
*/

IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users
    (
        [Guid] uniqueidentifier NOT NULL,
        IDUser int IDENTITY(1,1) NOT NULL,
        Username varchar(50) NOT NULL,
        [Password] varchar(50) NOT NULL,
        [Name] varchar(50) NOT NULL,
        Surname varchar(50) NOT NULL,
        [Role] varchar(50) NOT NULL,
        Department varchar(50) NOT NULL,
        [Enabled] bit NOT NULL,
        CreateDate date NOT NULL,
        LastModify datetime NOT NULL,
        Sector int NOT NULL,
        CONSTRAINT PK_Users PRIMARY KEY ([Guid])
    );
END
GO

IF OBJECT_ID(N'dbo.Partners', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Partners
    (
        [Guid] uniqueidentifier NOT NULL,
        PartnerType varchar(1) NOT NULL,
        ID varchar(50) NOT NULL,
        [Name] varchar(50) NOT NULL,
        Sector int NOT NULL,
        CONSTRAINT PK_Partners PRIMARY KEY ([Guid])
    );
END
GO

IF OBJECT_ID(N'dbo.Products', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Products
    (
        [Guid] uniqueidentifier NOT NULL,
        ID varchar(50) NOT NULL,
        [Name] varchar(150) NOT NULL,
        UMBase varchar(50) NOT NULL,
        CONSTRAINT PK_Products PRIMARY KEY ([Guid])
    );
END
GO

IF OBJECT_ID(N'dbo.Status', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Status
    (
        ID smallint NOT NULL,
        [Guid] uniqueidentifier NOT NULL,
        [Description] varchar(50) NOT NULL,
        Color varchar(50) NOT NULL,
        [Enabled] bit NOT NULL,
        CONSTRAINT PK_Status PRIMARY KEY (ID)
    );
END
GO

IF OBJECT_ID(N'dbo.Locations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Locations
    (
        LocationID int NOT NULL,
        LocationGuid uniqueidentifier NOT NULL,
        PartnerType varchar(1) NOT NULL,
        PartnerID varchar(50) NOT NULL,
        LocationCode varchar(5) NOT NULL,
        LocationName varchar(50) NOT NULL,
        CONSTRAINT PK_Locations PRIMARY KEY (LocationID)
    );
END
GO

IF OBJECT_ID(N'dbo.Combination', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Combination
    (
        ID int IDENTITY(1,1) NOT NULL,
        [Guid] uniqueidentifier NOT NULL,
        PartnerType varchar(1) NOT NULL,
        IDPartner varchar(50) NOT NULL,
        LocationID int NOT NULL,
        IDProduct varchar(50) NOT NULL,
        IDCross varchar(50) NOT NULL,
        PDR int NOT NULL,
        MOQ int NOT NULL,
        CONSTRAINT PK_Combination PRIMARY KEY (ID)
    );
END
GO

IF OBJECT_ID(N'dbo.Cards', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Cards
    (
        [Guid] uniqueidentifier NOT NULL,
        IDCard varchar(50) NOT NULL,
        IDCombination int NOT NULL,
        Quantity int NOT NULL,
        Location varchar(50) NULL,
        [Status] smallint NOT NULL,
        CONSTRAINT PK_Cards PRIMARY KEY (IDCard)
    );
END
GO

IF OBJECT_ID(N'dbo.Sectors', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Sectors
    (
        SectorID int NOT NULL,
        SectorGuid uniqueidentifier NOT NULL,
        SectorName varchar(50) NOT NULL,
        CONSTRAINT PK_Sectors PRIMARY KEY (SectorID)
    );
END
GO

IF OBJECT_ID(N'dbo.tblLog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblLog
    (
        id int IDENTITY(1,1) NOT NULL,
        ODP varchar(15) NOT NULL,
        IDMix varchar(3) NOT NULL,
        IDRows varchar(3) NOT NULL,
        IDUser varchar(3) NOT NULL,
        [Text] varchar(max) NOT NULL,
        PostingDate datetime NOT NULL,
        CONSTRAINT PK_tblLog PRIMARY KEY (id)
    );
END
GO

IF OBJECT_ID(N'dbo.ZPL', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ZPL
    (
        id int IDENTITY(1,1) NOT NULL,
        ZPL varchar(max) NULL,
        CONSTRAINT PK_ZPL PRIMARY KEY (id)
    );
END
GO

IF OBJECT_ID(N'dbo.Status', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.Status WHERE ID = 100)
        INSERT INTO dbo.Status (ID, [Guid], [Description], Color, [Enabled]) VALUES (100, NEWID(), 'Vuoto', '#dc3545', 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.Status WHERE ID = 200)
        INSERT INTO dbo.Status (ID, [Guid], [Description], Color, [Enabled]) VALUES (200, NEWID(), 'In Attesa', 'Gray', 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.Status WHERE ID = 300)
        INSERT INTO dbo.Status (ID, [Guid], [Description], Color, [Enabled]) VALUES (300, NEWID(), 'In Ordine', 'SteelBlue', 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.Status WHERE ID = 400)
        INSERT INTO dbo.Status (ID, [Guid], [Description], Color, [Enabled]) VALUES (400, NEWID(), 'Riordinato', '#ffc107', 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.Status WHERE ID = 500)
        INSERT INTO dbo.Status (ID, [Guid], [Description], Color, [Enabled]) VALUES (500, NEWID(), 'Prodotto', '-', 0);
    IF NOT EXISTS (SELECT 1 FROM dbo.Status WHERE ID = 600)
        INSERT INTO dbo.Status (ID, [Guid], [Description], Color, [Enabled]) VALUES (600, NEWID(), 'Spedito', 'DarkOrange', 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.Status WHERE ID = 700)
        INSERT INTO dbo.Status (ID, [Guid], [Description], Color, [Enabled]) VALUES (700, NEWID(), 'Disponibile', '#198754', 1);
END
GO

CREATE OR ALTER VIEW dbo.vwKanbanBoard
AS
SELECT
    dbo.Cards.[Guid],
    dbo.Cards.IDCard,
    dbo.Cards.[Status],
    dbo.Combination.ID AS IDCombinazione,
    dbo.Combination.IDPartner,
    dbo.Partners.[Name] AS PartnerName,
    dbo.Combination.IDProduct,
    dbo.Products.[Name] AS ProductName,
    dbo.Cards.Quantity,
    dbo.Products.UMBase,
    dbo.Status.[Description] AS StatusDescription,
    dbo.Status.Color,
    dbo.Locations.LocationCode,
    dbo.Locations.LocationName,
    dbo.Combination.IDCross
FROM dbo.Cards
INNER JOIN dbo.Combination ON dbo.Cards.IDCombination = dbo.Combination.ID
INNER JOIN dbo.Partners ON dbo.Combination.PartnerType = dbo.Partners.PartnerType
    AND dbo.Combination.IDPartner = dbo.Partners.ID
INNER JOIN dbo.Products ON dbo.Combination.IDProduct = dbo.Products.ID
INNER JOIN dbo.Status ON dbo.Cards.[Status] = dbo.Status.ID
LEFT OUTER JOIN dbo.Locations ON dbo.Combination.LocationID = dbo.Locations.LocationID;
GO
