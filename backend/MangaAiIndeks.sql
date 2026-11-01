IF OBJECT_ID(N'dbo.MangaAiIndeks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MangaAiIndeks (
        MangaId int NOT NULL PRIMARY KEY,
        IcerikHash nvarchar(64) NOT NULL,
        Vektor nvarchar(max) NOT NULL,
        CONSTRAINT FK_MangaAiIndeks_Manga FOREIGN KEY (MangaId) REFERENCES dbo.Mangalar(Id) ON DELETE CASCADE
    );
END;
