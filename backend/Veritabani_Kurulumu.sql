/* =========================================================
   Kage 95 - Manga Okuma Platformu
   Kapsamlı İlişkisel Veritabanı Kurulum Scripti (SQL Server / MSSQL)
   Faz 1: Veri Modeli ve Veritabanı Altyapısı

   NOT (yeniden çalıştırma güvenliği): Bu script idempotent'tir. Zaten kurulu
   bir veritabanında tekrar çalıştırılabilir; eski kayıtlar SİLİNMEZ, yeni
   tablo/kolon/indeksler ve yer tutucu örnek veriler IF NOT EXISTS ile eklenir.
   Örnek veri yer tutucudur ("Örnek Manga 1..6"); telifli seri adı ve harici
   kapak kullanılmaz. Eski kurulumdan kalan örnek kayıtlar (Wind Breaker vb.)
   elle silinmediği sürece veritabanında kalır.
   ========================================================= */

-- 1) Veritabanını oluştur (yoksa)
IF DB_ID(N'Kage95DB') IS NULL
BEGIN
    CREATE DATABASE Kage95DB;
END
GO

USE Kage95DB;
GO

-- 2) Kullanicilar Tablosu
IF OBJECT_ID(N'dbo.Kullanicilar', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Kullanicilar
    (
        Id             INT IDENTITY(1,1) NOT NULL,
        KullaniciAdi   NVARCHAR(50)      NOT NULL,
        Email          NVARCHAR(100)     NOT NULL,
        SifreHash      NVARCHAR(256)     NOT NULL,
        Rol            NVARCHAR(20)      NOT NULL CONSTRAINT DF_Kullanicilar_Rol DEFAULT ('Kullanici'),
        ProfilResmiURL NVARCHAR(500)     NULL,
        GoogleSubject  NVARCHAR(200)     NULL,
        DiscordUserId  NVARCHAR(100)     NULL,
        KayitTarihi    DATETIME2         NOT NULL CONSTRAINT DF_Kullanicilar_KayitTarihi DEFAULT (SYSDATETIME()),

        CONSTRAINT PK_Kullanicilar PRIMARY KEY (Id),
        CONSTRAINT UQ_Kullanicilar_KullaniciAdi UNIQUE (KullaniciAdi),
        CONSTRAINT UQ_Kullanicilar_Email UNIQUE (Email)
    );
END
ELSE
BEGIN
    -- Eski şemadan güncellemeler
    IF COL_LENGTH(N'dbo.Kullanicilar', N'SifreHash') IS NULL AND COL_LENGTH(N'dbo.Kullanicilar', N'Sifre') IS NOT NULL
        EXEC sp_rename 'dbo.Kullanicilar.Sifre', 'SifreHash', 'COLUMN';

    IF COL_LENGTH(N'dbo.Kullanicilar', N'Rol') IS NULL
        ALTER TABLE dbo.Kullanicilar ADD Rol NVARCHAR(20) NOT NULL CONSTRAINT DF_Kullanicilar_Rol DEFAULT ('Kullanici');

    IF COL_LENGTH(N'dbo.Kullanicilar', N'ProfilResmiURL') IS NULL
        ALTER TABLE dbo.Kullanicilar ADD ProfilResmiURL NVARCHAR(500) NULL;
END
GO

IF COL_LENGTH(N'dbo.Kullanicilar', N'GoogleSubject') IS NULL
    ALTER TABLE dbo.Kullanicilar ADD GoogleSubject NVARCHAR(200) NULL;
IF COL_LENGTH(N'dbo.Kullanicilar', N'DiscordUserId') IS NULL
    ALTER TABLE dbo.Kullanicilar ADD DiscordUserId NVARCHAR(100) NULL;
-- E-posta doğrulama; kolon eklenirken mevcut hesaplar doğrulanmış sayılır
IF COL_LENGTH(N'dbo.Kullanicilar', N'EmailDogrulandi') IS NULL
BEGIN
    ALTER TABLE dbo.Kullanicilar ADD EmailDogrulandi BIT NOT NULL CONSTRAINT DF_Kullanicilar_EmailDogrulandi DEFAULT (0);
    EXEC(N'UPDATE dbo.Kullanicilar SET EmailDogrulandi = 1');
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Kullanicilar_GoogleSubject' AND object_id = OBJECT_ID(N'dbo.Kullanicilar'))
    CREATE UNIQUE INDEX IX_Kullanicilar_GoogleSubject ON dbo.Kullanicilar(GoogleSubject) WHERE GoogleSubject IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Kullanicilar_DiscordUserId' AND object_id = OBJECT_ID(N'dbo.Kullanicilar'))
    CREATE UNIQUE INDEX IX_Kullanicilar_DiscordUserId ON dbo.Kullanicilar(DiscordUserId) WHERE DiscordUserId IS NOT NULL;
GO

-- 2b) Yetki sistemi: Yetkiler (virgülle ayrılmış izin anahtarları) ve Aktif (hesap açık/kapalı)
IF COL_LENGTH(N'dbo.Kullanicilar', N'Yetkiler') IS NULL
    ALTER TABLE dbo.Kullanicilar ADD Yetkiler NVARCHAR(1000) NULL;
GO
IF COL_LENGTH(N'dbo.Kullanicilar', N'Aktif') IS NULL
    ALTER TABLE dbo.Kullanicilar ADD Aktif BIT NOT NULL CONSTRAINT DF_Kullanicilar_Aktif DEFAULT (1);
GO

-- 3) Kategoriler Tablosu
IF OBJECT_ID(N'dbo.Kategoriler', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Kategoriler
    (
        Id            INT IDENTITY(1,1) NOT NULL,
        KategoriAdi   NVARCHAR(50)      NOT NULL,
        Aciklama      NVARCHAR(250)     NULL,

        CONSTRAINT PK_Kategoriler PRIMARY KEY (Id),
        CONSTRAINT UQ_Kategoriler_KategoriAdi UNIQUE (KategoriAdi)
    );
END
ELSE
BEGIN
    IF COL_LENGTH(N'dbo.Kategoriler', N'Aciklama') IS NULL
        ALTER TABLE dbo.Kategoriler ADD Aciklama NVARCHAR(250) NULL;
END
GO

-- 4) Mangalar Tablosu
IF OBJECT_ID(N'dbo.Mangalar', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Mangalar
    (
        Id            INT IDENTITY(1,1) NOT NULL,
        MangaAdi      NVARCHAR(200)     NOT NULL,
        KapakResmiURL NVARCHAR(500)     NULL,
        Aciklama      NVARCHAR(MAX)     NULL,
        Yazar         NVARCHAR(100)     NULL,
        Cizer         NVARCHAR(100)     NULL,
        Durum         NVARCHAR(30)      NOT NULL CONSTRAINT DF_Mangalar_Durum DEFAULT ('DevamEdiyor'),
        Puan          DECIMAL(3,1)      NOT NULL CONSTRAINT DF_Mangalar_Puan DEFAULT (9.0),
        OkunmaSayisi  NVARCHAR(20)      NOT NULL CONSTRAINT DF_Mangalar_OkunmaSayisi DEFAULT ('0'),
        EklenmeTarihi DATETIME2         NOT NULL CONSTRAINT DF_Mangalar_EklenmeTarihi DEFAULT (SYSDATETIME()),
        GuncellenmeTarihi DATETIME2     NOT NULL CONSTRAINT DF_Mangalar_GuncellenmeTarihi DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_Mangalar PRIMARY KEY (Id),
        CONSTRAINT UQ_Mangalar_MangaAdi UNIQUE (MangaAdi)
    );
END
ELSE
BEGIN
    IF COL_LENGTH(N'dbo.Mangalar', N'Yazar') IS NULL
        ALTER TABLE dbo.Mangalar ADD Yazar NVARCHAR(100) NULL;

    IF COL_LENGTH(N'dbo.Mangalar', N'Cizer') IS NULL
        ALTER TABLE dbo.Mangalar ADD Cizer NVARCHAR(100) NULL;

    IF COL_LENGTH(N'dbo.Mangalar', N'Durum') IS NULL
        ALTER TABLE dbo.Mangalar ADD Durum NVARCHAR(30) NOT NULL CONSTRAINT DF_Mangalar_Durum DEFAULT ('DevamEdiyor');

    IF COL_LENGTH(N'dbo.Mangalar', N'Puan') IS NULL
        ALTER TABLE dbo.Mangalar ADD Puan DECIMAL(3,1) NOT NULL CONSTRAINT DF_Mangalar_Puan DEFAULT (9.0);

    IF COL_LENGTH(N'dbo.Mangalar', N'OkunmaSayisi') IS NULL
        ALTER TABLE dbo.Mangalar ADD OkunmaSayisi NVARCHAR(20) NOT NULL CONSTRAINT DF_Mangalar_OkunmaSayisi DEFAULT ('0');

    IF COL_LENGTH(N'dbo.Mangalar', N'EklenmeTarihi') IS NULL
        ALTER TABLE dbo.Mangalar ADD EklenmeTarihi DATETIME2 NOT NULL CONSTRAINT DF_Mangalar_EklenmeTarihi DEFAULT (SYSDATETIME());
END
GO

-- Mangalar.GuncellenmeTarihi (eski kurulumlar için; yoksa ekle)
IF COL_LENGTH(N'dbo.Mangalar', N'GuncellenmeTarihi') IS NULL
    ALTER TABLE dbo.Mangalar ADD GuncellenmeTarihi DATETIME2 NOT NULL CONSTRAINT DF_Mangalar_GuncellenmeTarihi DEFAULT (SYSUTCDATETIME());
GO

-- 5) MangaKategorileri (Çoka Çok İlişki)
IF OBJECT_ID(N'dbo.MangaKategorileri', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MangaKategorileri
    (
        MangaId    INT NOT NULL,
        KategoriId INT NOT NULL,

        CONSTRAINT PK_MangaKategorileri PRIMARY KEY (MangaId, KategoriId),
        CONSTRAINT FK_MangaKategorileri_Mangalar FOREIGN KEY (MangaId) REFERENCES dbo.Mangalar(Id) ON DELETE CASCADE,
        CONSTRAINT FK_MangaKategorileri_Kategoriler FOREIGN KEY (KategoriId) REFERENCES dbo.Kategoriler(Id) ON DELETE CASCADE
    );
END
GO

-- 6) Bolumler Tablosu
IF OBJECT_ID(N'dbo.Bolumler', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Bolumler
    (
        Id          INT IDENTITY(1,1) NOT NULL,
        MangaId     INT               NOT NULL,
        BolumNo     DECIMAL(6,2)      NOT NULL,
        BolumAdi    NVARCHAR(200)     NULL,
        YayinTarihi DATETIME2         NOT NULL CONSTRAINT DF_Bolumler_YayinTarihi DEFAULT (SYSDATETIME()),

        CONSTRAINT PK_Bolumler PRIMARY KEY (Id),
        CONSTRAINT UQ_Bolumler_Manga_BolumNo UNIQUE (MangaId, BolumNo),
        CONSTRAINT FK_Bolumler_Mangalar FOREIGN KEY (MangaId) REFERENCES dbo.Mangalar(Id) ON DELETE CASCADE
    );
END
GO

-- 7) Sayfalar Tablosu
IF OBJECT_ID(N'dbo.Sayfalar', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Sayfalar
    (
        Id       INT IDENTITY(1,1) NOT NULL,
        BolumId  INT               NOT NULL,
        SayfaNo  INT               NOT NULL,
        ResimURL NVARCHAR(1000)    NOT NULL,

        CONSTRAINT PK_Sayfalar PRIMARY KEY (Id),
        CONSTRAINT UQ_Sayfalar_Bolum_SayfaNo UNIQUE (BolumId, SayfaNo),
        CONSTRAINT FK_Sayfalar_Bolumler FOREIGN KEY (BolumId) REFERENCES dbo.Bolumler(Id) ON DELETE CASCADE
    );
END
GO

-- 8) Yorumlar Tablosu
IF OBJECT_ID(N'dbo.Yorumlar', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Yorumlar
    (
        Id          INT IDENTITY(1,1) NOT NULL,
        BolumId     INT               NOT NULL,
        KullaniciId INT               NOT NULL,
        Metin       NVARCHAR(1000)    NOT NULL,
        BegeniSayisi INT              NOT NULL CONSTRAINT DF_Yorumlar_BegeniSayisi DEFAULT (0),
        Tarih       DATETIME2         NOT NULL CONSTRAINT DF_Yorumlar_Tarih DEFAULT (SYSDATETIME()),

        CONSTRAINT PK_Yorumlar PRIMARY KEY (Id),
        CONSTRAINT FK_Yorumlar_Bolumler FOREIGN KEY (BolumId) REFERENCES dbo.Bolumler(Id) ON DELETE CASCADE,
        CONSTRAINT FK_Yorumlar_Kullanicilar FOREIGN KEY (KullaniciId) REFERENCES dbo.Kullanicilar(Id)
    );
END
GO

-- 9) Favoriler / Okuma Listesi Tablosu
IF OBJECT_ID(N'dbo.Favoriler', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Favoriler
    (
        Id            INT IDENTITY(1,1) NOT NULL,
        KullaniciId   INT               NOT NULL,
        MangaId       INT               NOT NULL,
        EklenmeTarihi DATETIME2         NOT NULL CONSTRAINT DF_Favoriler_EklenmeTarihi DEFAULT (SYSDATETIME()),

        CONSTRAINT PK_Favoriler PRIMARY KEY (Id),
        CONSTRAINT UQ_Favoriler_Kullanici_Manga UNIQUE (KullaniciId, MangaId),
        CONSTRAINT FK_Favoriler_Kullanicilar FOREIGN KEY (KullaniciId) REFERENCES dbo.Kullanicilar(Id) ON DELETE CASCADE,
        CONSTRAINT FK_Favoriler_Mangalar FOREIGN KEY (MangaId) REFERENCES dbo.Mangalar(Id) ON DELETE CASCADE
    );
END
GO

-- 10) OkumaGecmisi Tablosu (Kullanıcının Kaldığı Yer)
IF OBJECT_ID(N'dbo.OkumaGecmisi', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OkumaGecmisi
    (
        Id               INT IDENTITY(1,1) NOT NULL,
        KullaniciId      INT               NOT NULL,
        MangaId          INT               NOT NULL,
        SonOkunanBolumId INT               NOT NULL,
        SonSayfaNo       INT               NOT NULL CONSTRAINT DF_OkumaGecmisi_SonSayfaNo DEFAULT (1),
        GuncellenmeTarihi DATETIME2        NOT NULL CONSTRAINT DF_OkumaGecmisi_GuncellenmeTarihi DEFAULT (SYSDATETIME()),

        CONSTRAINT PK_OkumaGecmisi PRIMARY KEY (Id),
        CONSTRAINT UQ_OkumaGecmisi_Kullanici_Manga UNIQUE (KullaniciId, MangaId),
        CONSTRAINT FK_OkumaGecmisi_Kullanicilar FOREIGN KEY (KullaniciId) REFERENCES dbo.Kullanicilar(Id) ON DELETE CASCADE,
        CONSTRAINT FK_OkumaGecmisi_Mangalar FOREIGN KEY (MangaId) REFERENCES dbo.Mangalar(Id),
        CONSTRAINT FK_OkumaGecmisi_Bolumler FOREIGN KEY (SonOkunanBolumId) REFERENCES dbo.Bolumler(Id)
    );
END
ELSE
BEGIN
    -- Eski yazım hatası: GuncellemeTarihi -> GuncellenmeTarihi (EF modeli bunu bekler)
    IF COL_LENGTH(N'dbo.OkumaGecmisi', N'GuncellenmeTarihi') IS NULL AND COL_LENGTH(N'dbo.OkumaGecmisi', N'GuncellemeTarihi') IS NOT NULL
        EXEC sp_rename 'dbo.OkumaGecmisi.GuncellemeTarihi', 'GuncellenmeTarihi', 'COLUMN';
END
GO

-- =========================================================
-- Performans İndeksleri
-- =========================================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Bolumler_MangaId_BolumNo')
    CREATE NONCLUSTERED INDEX IX_Bolumler_MangaId_BolumNo ON dbo.Bolumler(MangaId, BolumNo);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Sayfalar_BolumId_SayfaNo')
    CREATE NONCLUSTERED INDEX IX_Sayfalar_BolumId_SayfaNo ON dbo.Sayfalar(BolumId, SayfaNo);
GO

-- Bolumler (MangaId, BolumNo) UNIQUE: aynı iki kolonda zaten benzersiz indeks/kısıt yoksa ve
-- mevcut veride çakışma yoksa oluşturulur. Çakışma varsa script durmaz, uyarı basar.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes i
    WHERE i.object_id = OBJECT_ID(N'dbo.Bolumler') AND i.is_unique = 1 AND i.is_primary_key = 0
      AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0) = 2
      AND (SELECT COUNT(*) FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
           WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0 AND c.name IN (N'MangaId', N'BolumNo')) = 2)
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.Bolumler GROUP BY MangaId, BolumNo HAVING COUNT(*) > 1)
        PRINT N'UYARI: Bolumler tablosunda ayni (MangaId, BolumNo) icin tekrar eden kayitlar var; UNIQUE indeks olusturulmadi. Once tekrarlari temizleyin.';
    ELSE
        CREATE UNIQUE NONCLUSTERED INDEX UX_Bolumler_MangaId_BolumNo ON dbo.Bolumler(MangaId, BolumNo);
END
GO

-- Sayfalar (BolumId, SayfaNo) UNIQUE: aynı kontrol.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes i
    WHERE i.object_id = OBJECT_ID(N'dbo.Sayfalar') AND i.is_unique = 1 AND i.is_primary_key = 0
      AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0) = 2
      AND (SELECT COUNT(*) FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
           WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0 AND c.name IN (N'BolumId', N'SayfaNo')) = 2)
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.Sayfalar GROUP BY BolumId, SayfaNo HAVING COUNT(*) > 1)
        PRINT N'UYARI: Sayfalar tablosunda ayni (BolumId, SayfaNo) icin tekrar eden kayitlar var; UNIQUE indeks olusturulmadi. Once tekrarlari temizleyin.';
    ELSE
        CREATE UNIQUE NONCLUSTERED INDEX UX_Sayfalar_BolumId_SayfaNo ON dbo.Sayfalar(BolumId, SayfaNo);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Yorumlar_BolumId')
    CREATE NONCLUSTERED INDEX IX_Yorumlar_BolumId ON dbo.Yorumlar(BolumId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MangaKategorileri_KategoriId')
    CREATE NONCLUSTERED INDEX IX_MangaKategorileri_KategoriId ON dbo.MangaKategorileri(KategoriId);
GO

-- =========================================================
-- TOHUMLAMA (SEED DATA)
-- =========================================================

-- Kullanıcılar (Şifre: 123456 - SHA256 Hash: 8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92)
IF NOT EXISTS (SELECT 1 FROM dbo.Kullanicilar WHERE KullaniciAdi = N'admin')
    INSERT INTO dbo.Kullanicilar (KullaniciAdi, Email, SifreHash, Rol)
    VALUES (N'admin', N'admin@kage95.com', N'8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92', N'Admin');

IF NOT EXISTS (SELECT 1 FROM dbo.Kullanicilar WHERE KullaniciAdi = N'Ninja95')
    INSERT INTO dbo.Kullanicilar (KullaniciAdi, Email, SifreHash, Rol)
    VALUES (N'Ninja95', N'ninja@kage95.com', N'8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92', N'Kullanici');

IF NOT EXISTS (SELECT 1 FROM dbo.Kullanicilar WHERE KullaniciAdi = N'SamuraiX')
    INSERT INTO dbo.Kullanicilar (KullaniciAdi, Email, SifreHash, Rol)
    VALUES (N'SamuraiX', N'samurai@kage95.com', N'8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92', N'Kullanici');
GO

-- Site sahibi: kullanıcı adı 'Jagwey' (büyük/küçük harf duyarsız) varsa Owner yapılır, yoksa dokunulmaz.
-- Owner rolü yalnızca veritabanından atanır; API ile verilemez/alınamaz.
UPDATE dbo.Kullanicilar SET Rol = N'Owner' WHERE LOWER(KullaniciAdi) = N'jagwey' AND Rol <> N'Owner';
GO

-- Kategoriler
IF NOT EXISTS (SELECT 1 FROM dbo.Kategoriler WHERE KategoriAdi = N'Aksiyon')
    INSERT INTO dbo.Kategoriler (KategoriAdi, Aciklama) VALUES (N'Aksiyon', N'Dövüş, macera ve yüksek tempolu sahneler');
IF NOT EXISTS (SELECT 1 FROM dbo.Kategoriler WHERE KategoriAdi = N'Shounen')
    INSERT INTO dbo.Kategoriler (KategoriAdi, Aciklama) VALUES (N'Shounen', N'Genç erkek hedef kitleye yönelik gelişim ve mücadele hikayeleri');
IF NOT EXISTS (SELECT 1 FROM dbo.Kategoriler WHERE KategoriAdi = N'Macera')
    INSERT INTO dbo.Kategoriler (KategoriAdi, Aciklama) VALUES (N'Macera', N'Bilinmeyene yolculuk ve keşif');
IF NOT EXISTS (SELECT 1 FROM dbo.Kategoriler WHERE KategoriAdi = N'Fantastik')
    INSERT INTO dbo.Kategoriler (KategoriAdi, Aciklama) VALUES (N'Fantastik', N'Büyü, efsaneler ve doğaüstü dünyalar');
IF NOT EXISTS (SELECT 1 FROM dbo.Kategoriler WHERE KategoriAdi = N'Dram')
    INSERT INTO dbo.Kategoriler (KategoriAdi, Aciklama) VALUES (N'Dram', N'Duygusal derinlik ve karakter odaklı çatışmalar');
IF NOT EXISTS (SELECT 1 FROM dbo.Kategoriler WHERE KategoriAdi = N'Gizem')
    INSERT INTO dbo.Kategoriler (KategoriAdi, Aciklama) VALUES (N'Gizem', N'Sır perdeleri ve çözüm bekleyen vakalar');
IF NOT EXISTS (SELECT 1 FROM dbo.Kategoriler WHERE KategoriAdi = N'Romantizm')
    INSERT INTO dbo.Kategoriler (KategoriAdi, Aciklama) VALUES (N'Romantizm', N'Aşk ve ilişki hikayeleri');
GO

-- Mangalar (YER TUTUCU içerik: Örnek Manga 1..6)
-- Kapaklar placehold.co üzerinden üretilir; gerçek/telifli içerik kullanılmaz.
DECLARE @i INT = 1;
WHILE @i <= 6
BEGIN
    DECLARE @Ad NVARCHAR(200) = N'Örnek Manga ' + CAST(@i AS NVARCHAR(10));
    IF NOT EXISTS (SELECT 1 FROM dbo.Mangalar WHERE MangaAdi = @Ad)
        INSERT INTO dbo.Mangalar (MangaAdi, KapakResmiURL, Aciklama, Yazar, Cizer, Durum, Puan, OkunmaSayisi)
        VALUES (@Ad,
                N'https://placehold.co/500x750/171313/e0343c?text=Ornek+' + CAST(@i AS NVARCHAR(10)),
                N'Yer tutucu içerik',
                N'Yer Tutucu Yazar', N'Yer Tutucu Çizer',
                CASE WHEN @i = 3 THEN N'Tamamlandi' ELSE N'DevamEdiyor' END,
                CAST(8.0 + (@i % 3) * 0.5 AS DECIMAL(3,1)),
                CAST(@i * 10 AS NVARCHAR(10)) + N'K');
    SET @i += 1;
END
GO

-- Manga Kategorileri Eşleştirmesi (her yer tutucu manga için 2 kategori; eksikse ekle)
INSERT INTO dbo.MangaKategorileri (MangaId, KategoriId)
SELECT m.Id, k.Id
FROM dbo.Mangalar m
JOIN dbo.Kategoriler k ON k.KategoriAdi IN (
    CASE RIGHT(m.MangaAdi, 1) WHEN N'1' THEN N'Aksiyon' WHEN N'2' THEN N'Fantastik' WHEN N'3' THEN N'Macera'
                              WHEN N'4' THEN N'Dram'    WHEN N'5' THEN N'Gizem'      ELSE N'Romantizm' END,
    N'Shounen')
WHERE m.MangaAdi LIKE N'Örnek Manga [1-6]'
  AND NOT EXISTS (SELECT 1 FROM dbo.MangaKategorileri mk WHERE mk.MangaId = m.Id AND mk.KategoriId = k.Id);
GO

-- Bölümler: Örnek Manga 1..6 için 2-3 bölüm (1 ve 4 numaralı mangalar 3 bölüm, diğerleri 2)
INSERT INTO dbo.Bolumler (MangaId, BolumNo, BolumAdi)
SELECT m.Id, n.No, N'Bölüm ' + CAST(n.No AS NVARCHAR(10)) + N': Yer tutucu'
FROM dbo.Mangalar m
CROSS JOIN (VALUES (1), (2), (3)) AS n(No)
WHERE m.MangaAdi LIKE N'Örnek Manga [1-6]'
  AND (n.No <= 2 OR RIGHT(m.MangaAdi, 1) IN (N'1', N'4'))
  AND NOT EXISTS (SELECT 1 FROM dbo.Bolumler b WHERE b.MangaId = m.Id AND b.BolumNo = n.No);
GO

-- Sayfalar: her yer tutucu bölüme 4 sayfa
INSERT INTO dbo.Sayfalar (BolumId, SayfaNo, ResimURL)
SELECT b.Id, p.No, N'https://placehold.co/900x1400/171313/e0343c?text=Sayfa+' + CAST(p.No AS NVARCHAR(10))
FROM dbo.Bolumler b
JOIN dbo.Mangalar m ON m.Id = b.MangaId
CROSS JOIN (VALUES (1), (2), (3), (4)) AS p(No)
WHERE m.MangaAdi LIKE N'Örnek Manga [1-6]'
  AND NOT EXISTS (SELECT 1 FROM dbo.Sayfalar sf WHERE sf.BolumId = b.Id AND sf.SayfaNo = p.No);
GO

-- Yorumlar (yer tutucu): Örnek Manga 1, Bölüm 1
DECLARE @B1Id INT = (SELECT b.Id FROM dbo.Bolumler b JOIN dbo.Mangalar m ON b.MangaId = m.Id WHERE m.MangaAdi = N'Örnek Manga 1' AND b.BolumNo = 1);
DECLARE @NinjaId INT = (SELECT Id FROM dbo.Kullanicilar WHERE KullaniciAdi = N'Ninja95');
DECLARE @SamuraiId INT = (SELECT Id FROM dbo.Kullanicilar WHERE KullaniciAdi = N'SamuraiX');

IF @B1Id IS NOT NULL AND @NinjaId IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.Yorumlar WHERE BolumId = @B1Id AND KullaniciId = @NinjaId AND Metin = N'Yer tutucu yorum 1')
    INSERT INTO dbo.Yorumlar (BolumId, KullaniciId, Metin, BegeniSayisi, Tarih)
    VALUES (@B1Id, @NinjaId, N'Yer tutucu yorum 1', 5, DATEADD(MINUTE, -10, SYSDATETIME()));

IF @B1Id IS NOT NULL AND @SamuraiId IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.Yorumlar WHERE BolumId = @B1Id AND KullaniciId = @SamuraiId AND Metin = N'Yer tutucu yorum 2')
    INSERT INTO dbo.Yorumlar (BolumId, KullaniciId, Metin, BegeniSayisi, Tarih)
    VALUES (@B1Id, @SamuraiId, N'Yer tutucu yorum 2', 2, DATEADD(HOUR, -1, SYSDATETIME()));
GO

-- =========================================================
-- Faz 3: Gelişmiş yönetim paneli (ban, oturum sürümü, denetim kaydı, site ayarları, yayın durumu)
-- Idempotent: tekrar çalıştırmak güvenlidir.
-- =========================================================
USE Kage95DB;
GO

IF COL_LENGTH(N'dbo.Kullanicilar', N'SonGirisTarihi') IS NULL
    ALTER TABLE dbo.Kullanicilar ADD SonGirisTarihi DATETIME2 NULL;
GO
IF COL_LENGTH(N'dbo.Kullanicilar', N'BanNedeni') IS NULL
    ALTER TABLE dbo.Kullanicilar ADD BanNedeni NVARCHAR(500) NULL;
GO
IF COL_LENGTH(N'dbo.Kullanicilar', N'BanTarihi') IS NULL
    ALTER TABLE dbo.Kullanicilar ADD BanTarihi DATETIME2 NULL;
GO
IF COL_LENGTH(N'dbo.Kullanicilar', N'BanBitis') IS NULL
    ALTER TABLE dbo.Kullanicilar ADD BanBitis DATETIME2 NULL;
GO
IF COL_LENGTH(N'dbo.Kullanicilar', N'BanYapanId') IS NULL
    ALTER TABLE dbo.Kullanicilar ADD BanYapanId INT NULL;
GO
IF COL_LENGTH(N'dbo.Kullanicilar', N'TokenSurumu') IS NULL
    ALTER TABLE dbo.Kullanicilar ADD TokenSurumu INT NOT NULL CONSTRAINT DF_Kullanicilar_TokenSurumu DEFAULT (0);
GO

IF COL_LENGTH(N'dbo.Mangalar', N'Yayinda') IS NULL
    ALTER TABLE dbo.Mangalar ADD Yayinda BIT NOT NULL CONSTRAINT DF_Mangalar_Yayinda DEFAULT (1);
GO
IF COL_LENGTH(N'dbo.Mangalar', N'OneCikan') IS NULL
    ALTER TABLE dbo.Mangalar ADD OneCikan BIT NOT NULL CONSTRAINT DF_Mangalar_OneCikan DEFAULT (0);
GO
IF COL_LENGTH(N'dbo.Mangalar', N'OneCikanResimURL') IS NULL
    ALTER TABLE dbo.Mangalar ADD OneCikanResimURL NVARCHAR(500) NULL;
GO

IF OBJECT_ID(N'dbo.DenetimKayitlari', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DenetimKayitlari
    (
        Id       INT IDENTITY(1,1) NOT NULL,
        YapanId  INT NULL,
        YapanAdi NVARCHAR(50) NOT NULL,
        Eylem    NVARCHAR(100) NOT NULL,
        Hedef    NVARCHAR(200) NOT NULL,
        Detay    NVARCHAR(1000) NULL,
        Tarih    DATETIME2 NOT NULL CONSTRAINT DF_DenetimKayitlari_Tarih DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_DenetimKayitlari PRIMARY KEY (Id)
    );
    CREATE INDEX IX_DenetimKayitlari_Tarih ON dbo.DenetimKayitlari (Tarih DESC);
END
GO

IF OBJECT_ID(N'dbo.SiteAyarlari', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SiteAyarlari
    (
        Anahtar NVARCHAR(100) NOT NULL,
        Deger   NVARCHAR(1000) NOT NULL,
        CONSTRAINT PK_SiteAyarlari PRIMARY KEY (Anahtar)
    );
END
GO

-- Varsayılan ayarlar (yoksa eklenir; var olanlara dokunulmaz)
INSERT INTO dbo.SiteAyarlari (Anahtar, Deger)
SELECT v.Anahtar, v.Deger
FROM (VALUES
    (N'kayitAcik',   N'1'),
    (N'yorumAcik',   N'1'),
    (N'bakimModu',   N'0'),
    (N'bakimMesaji', N'Site kısa süreliğine bakımda. Birazdan tekrar dene.'),
    (N'duyuruAcik',  N'0'),
    (N'duyuruMetni', N'')
) AS v(Anahtar, Deger)
WHERE NOT EXISTS (SELECT 1 FROM dbo.SiteAyarlari a WHERE a.Anahtar = v.Anahtar);
GO

-- =========================================================
-- Faz 4: Seri yorumları, seri puanı, bölüm puanı (1-5 tam sayı)
-- Idempotent: tekrar çalıştırmak güvenlidir; mevcut tablolara dokunmaz.
-- =========================================================
USE Kage95DB;
GO

IF OBJECT_ID(N'dbo.MangaYorumlari', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MangaYorumlari
    (
        Id          INT IDENTITY(1,1) NOT NULL,
        MangaId     INT               NOT NULL,
        KullaniciId INT               NOT NULL,
        Metin       NVARCHAR(1000)    NOT NULL,
        Tarih       DATETIME2         NOT NULL CONSTRAINT DF_MangaYorumlari_Tarih DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_MangaYorumlari PRIMARY KEY (Id),
        CONSTRAINT FK_MangaYorumlari_Mangalar FOREIGN KEY (MangaId) REFERENCES dbo.Mangalar(Id) ON DELETE CASCADE,
        CONSTRAINT FK_MangaYorumlari_Kullanicilar FOREIGN KEY (KullaniciId) REFERENCES dbo.Kullanicilar(Id)
    );
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MangaYorumlari_MangaId_Tarih' AND object_id = OBJECT_ID(N'dbo.MangaYorumlari'))
    CREATE NONCLUSTERED INDEX IX_MangaYorumlari_MangaId_Tarih ON dbo.MangaYorumlari(MangaId, Tarih DESC);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MangaYorumlari_KullaniciId' AND object_id = OBJECT_ID(N'dbo.MangaYorumlari'))
    CREATE NONCLUSTERED INDEX IX_MangaYorumlari_KullaniciId ON dbo.MangaYorumlari(KullaniciId);
GO

IF OBJECT_ID(N'dbo.MangaPuanlari', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MangaPuanlari
    (
        Id          INT IDENTITY(1,1) NOT NULL,
        MangaId     INT               NOT NULL,
        KullaniciId INT               NOT NULL,
        Deger       TINYINT           NOT NULL,
        Tarih       DATETIME2         NOT NULL CONSTRAINT DF_MangaPuanlari_Tarih DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_MangaPuanlari PRIMARY KEY (Id),
        CONSTRAINT CK_MangaPuanlari_Deger CHECK (Deger BETWEEN 1 AND 5),
        CONSTRAINT FK_MangaPuanlari_Mangalar FOREIGN KEY (MangaId) REFERENCES dbo.Mangalar(Id) ON DELETE CASCADE,
        CONSTRAINT FK_MangaPuanlari_Kullanicilar FOREIGN KEY (KullaniciId) REFERENCES dbo.Kullanicilar(Id)
    );
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_MangaPuanlari_MangaId_KullaniciId' AND object_id = OBJECT_ID(N'dbo.MangaPuanlari'))
    CREATE UNIQUE NONCLUSTERED INDEX UX_MangaPuanlari_MangaId_KullaniciId ON dbo.MangaPuanlari(MangaId, KullaniciId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MangaPuanlari_KullaniciId' AND object_id = OBJECT_ID(N'dbo.MangaPuanlari'))
    CREATE NONCLUSTERED INDEX IX_MangaPuanlari_KullaniciId ON dbo.MangaPuanlari(KullaniciId);
GO

IF OBJECT_ID(N'dbo.BolumPuanlari', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BolumPuanlari
    (
        Id          INT IDENTITY(1,1) NOT NULL,
        BolumId     INT               NOT NULL,
        KullaniciId INT               NOT NULL,
        Deger       TINYINT           NOT NULL,
        Tarih       DATETIME2         NOT NULL CONSTRAINT DF_BolumPuanlari_Tarih DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_BolumPuanlari PRIMARY KEY (Id),
        CONSTRAINT CK_BolumPuanlari_Deger CHECK (Deger BETWEEN 1 AND 5),
        CONSTRAINT FK_BolumPuanlari_Bolumler FOREIGN KEY (BolumId) REFERENCES dbo.Bolumler(Id) ON DELETE CASCADE,
        CONSTRAINT FK_BolumPuanlari_Kullanicilar FOREIGN KEY (KullaniciId) REFERENCES dbo.Kullanicilar(Id)
    );
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_BolumPuanlari_BolumId_KullaniciId' AND object_id = OBJECT_ID(N'dbo.BolumPuanlari'))
    CREATE UNIQUE NONCLUSTERED INDEX UX_BolumPuanlari_BolumId_KullaniciId ON dbo.BolumPuanlari(BolumId, KullaniciId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BolumPuanlari_KullaniciId' AND object_id = OBJECT_ID(N'dbo.BolumPuanlari'))
    CREATE NONCLUSTERED INDEX IX_BolumPuanlari_KullaniciId ON dbo.BolumPuanlari(KullaniciId);
GO

/* ---------------------------------------------------------
   Puan defteri (kullanıcı puanı + liderlik): toplam puan = SUM(Puan)
   --------------------------------------------------------- */
IF OBJECT_ID(N'dbo.PuanKayitlari', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PuanKayitlari
    (
        Id          BIGINT IDENTITY(1,1) NOT NULL,
        KullaniciId INT                  NOT NULL,
        Eylem       NVARCHAR(40)         NOT NULL,
        Puan        INT                  NOT NULL,
        Referans    NVARCHAR(60)         NULL,
        Tarih       DATETIME2            NOT NULL CONSTRAINT DF_PuanKayitlari_Tarih DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_PuanKayitlari PRIMARY KEY (Id),
        CONSTRAINT FK_PuanKayitlari_Kullanicilar FOREIGN KEY (KullaniciId) REFERENCES dbo.Kullanicilar(Id)
    );
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PuanKayitlari_KullaniciId_Tarih' AND object_id = OBJECT_ID(N'dbo.PuanKayitlari'))
    CREATE NONCLUSTERED INDEX IX_PuanKayitlari_KullaniciId_Tarih ON dbo.PuanKayitlari(KullaniciId, Tarih);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PuanKayitlari_Eylem' AND object_id = OBJECT_ID(N'dbo.PuanKayitlari'))
    CREATE NONCLUSTERED INDEX IX_PuanKayitlari_Eylem ON dbo.PuanKayitlari(Eylem);
GO
SET QUOTED_IDENTIFIER ON;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_PuanKayitlari_Kullanici_Eylem_Referans' AND object_id = OBJECT_ID(N'dbo.PuanKayitlari'))
    CREATE UNIQUE NONCLUSTERED INDEX UX_PuanKayitlari_Kullanici_Eylem_Referans ON dbo.PuanKayitlari(KullaniciId, Eylem, Referans) WHERE Referans IS NOT NULL;
GO

/* ---------------------------------------------------------
   Yorum v2: duzenleme, spoiler, yanit (tek seviye), begeni, sikayet
   (Yorumlar ve MangaYorumlari tablolarina yalnizca NULL'lanabilir/varsayilanli kolon eklenir; mevcut veri degismez.
    YanitId icin FK yoktur: kendine referansli CASCADE mumkun degil; yanitlar uygulama tarafindan silinir.)
   --------------------------------------------------------- */
IF COL_LENGTH(N'dbo.Yorumlar', N'Duzenlendi') IS NULL ALTER TABLE dbo.Yorumlar ADD Duzenlendi DATETIME2 NULL;
GO
IF COL_LENGTH(N'dbo.Yorumlar', N'Spoiler') IS NULL ALTER TABLE dbo.Yorumlar ADD Spoiler BIT NOT NULL CONSTRAINT DF_Yorumlar_Spoiler DEFAULT (0);
GO
IF COL_LENGTH(N'dbo.Yorumlar', N'YanitId') IS NULL ALTER TABLE dbo.Yorumlar ADD YanitId INT NULL;
GO
IF COL_LENGTH(N'dbo.MangaYorumlari', N'Duzenlendi') IS NULL ALTER TABLE dbo.MangaYorumlari ADD Duzenlendi DATETIME2 NULL;
GO
IF COL_LENGTH(N'dbo.MangaYorumlari', N'Spoiler') IS NULL ALTER TABLE dbo.MangaYorumlari ADD Spoiler BIT NOT NULL CONSTRAINT DF_MangaYorumlari_Spoiler DEFAULT (0);
GO
IF COL_LENGTH(N'dbo.MangaYorumlari', N'YanitId') IS NULL ALTER TABLE dbo.MangaYorumlari ADD YanitId INT NULL;
GO
SET QUOTED_IDENTIFIER ON;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Yorumlar_YanitId' AND object_id = OBJECT_ID(N'dbo.Yorumlar'))
    CREATE NONCLUSTERED INDEX IX_Yorumlar_YanitId ON dbo.Yorumlar(YanitId) WHERE YanitId IS NOT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MangaYorumlari_YanitId' AND object_id = OBJECT_ID(N'dbo.MangaYorumlari'))
    CREATE NONCLUSTERED INDEX IX_MangaYorumlari_YanitId ON dbo.MangaYorumlari(YanitId) WHERE YanitId IS NOT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Yorumlar_BolumId_Tarih' AND object_id = OBJECT_ID(N'dbo.Yorumlar'))
    CREATE NONCLUSTERED INDEX IX_Yorumlar_BolumId_Tarih ON dbo.Yorumlar(BolumId, Tarih DESC);
GO

IF OBJECT_ID(N'dbo.YorumBegenileri', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.YorumBegenileri
    (
        Id          INT IDENTITY(1,1) NOT NULL,
        Tur         VARCHAR(10)       NOT NULL,
        YorumId     INT               NOT NULL,
        KullaniciId INT               NOT NULL,
        Tarih       DATETIME2         NOT NULL CONSTRAINT DF_YorumBegenileri_Tarih DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_YorumBegenileri PRIMARY KEY (Id),
        CONSTRAINT CK_YorumBegenileri_Tur CHECK (Tur IN ('bolum','manga')),
        CONSTRAINT FK_YorumBegenileri_Kullanicilar FOREIGN KEY (KullaniciId) REFERENCES dbo.Kullanicilar(Id) ON DELETE CASCADE
    );
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_YorumBegenileri_Tur_Yorum_Kullanici' AND object_id = OBJECT_ID(N'dbo.YorumBegenileri'))
    CREATE UNIQUE NONCLUSTERED INDEX UX_YorumBegenileri_Tur_Yorum_Kullanici ON dbo.YorumBegenileri(Tur, YorumId, KullaniciId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_YorumBegenileri_KullaniciId' AND object_id = OBJECT_ID(N'dbo.YorumBegenileri'))
    CREATE NONCLUSTERED INDEX IX_YorumBegenileri_KullaniciId ON dbo.YorumBegenileri(KullaniciId);
GO

IF OBJECT_ID(N'dbo.YorumSikayetleri', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.YorumSikayetleri
    (
        Id             INT IDENTITY(1,1) NOT NULL,
        Tur            VARCHAR(10)       NOT NULL,
        YorumId        INT               NOT NULL,
        SikayetEdenId  INT               NOT NULL,
        Neden          NVARCHAR(200)     NOT NULL,
        Tarih          DATETIME2         NOT NULL CONSTRAINT DF_YorumSikayetleri_Tarih DEFAULT (SYSUTCDATETIME()),
        Durum          VARCHAR(10)       NOT NULL CONSTRAINT DF_YorumSikayetleri_Durum DEFAULT ('acik'),
        CONSTRAINT PK_YorumSikayetleri PRIMARY KEY (Id),
        CONSTRAINT CK_YorumSikayetleri_Tur CHECK (Tur IN ('bolum','manga')),
        CONSTRAINT CK_YorumSikayetleri_Durum CHECK (Durum IN ('acik','kapali')),
        CONSTRAINT FK_YorumSikayetleri_Kullanicilar FOREIGN KEY (SikayetEdenId) REFERENCES dbo.Kullanicilar(Id) ON DELETE CASCADE
    );
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_YorumSikayetleri_Tur_Yorum_Eden' AND object_id = OBJECT_ID(N'dbo.YorumSikayetleri'))
    CREATE UNIQUE NONCLUSTERED INDEX UX_YorumSikayetleri_Tur_Yorum_Eden ON dbo.YorumSikayetleri(Tur, YorumId, SikayetEdenId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_YorumSikayetleri_Durum_Tarih' AND object_id = OBJECT_ID(N'dbo.YorumSikayetleri'))
    CREATE NONCLUSTERED INDEX IX_YorumSikayetleri_Durum_Tarih ON dbo.YorumSikayetleri(Durum, Tarih DESC);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_YorumSikayetleri_Eden_Tarih' AND object_id = OBJECT_ID(N'dbo.YorumSikayetleri'))
    CREATE NONCLUSTERED INDEX IX_YorumSikayetleri_Eden_Tarih ON dbo.YorumSikayetleri(SikayetEdenId, Tarih DESC);
GO

/* ---------------------------------------------------------
   Duyurular (coklu, etiketli)
   --------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Duyurular', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Duyurular
    (
        Id          INT IDENTITY(1,1) NOT NULL,
        Baslik      NVARCHAR(150)     NOT NULL,
        Etiket      VARCHAR(10)       NOT NULL CONSTRAINT DF_Duyurular_Etiket DEFAULT ('bilgi'),
        Baglanti    NVARCHAR(300)     NULL,
        Aktif       BIT               NOT NULL CONSTRAINT DF_Duyurular_Aktif DEFAULT (1),
        Sira        INT               NOT NULL CONSTRAINT DF_Duyurular_Sira DEFAULT (0),
        Tarih       DATETIME2         NOT NULL CONSTRAINT DF_Duyurular_Tarih DEFAULT (SYSUTCDATETIME()),
        BitisTarihi DATETIME2         NULL,
        CONSTRAINT PK_Duyurular PRIMARY KEY (Id),
        CONSTRAINT CK_Duyurular_Etiket CHECK (Etiket IN ('bilgi','etkinlik','yeni','uyari'))
    );
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Duyurular_Aktif_Sira' AND object_id = OBJECT_ID(N'dbo.Duyurular'))
    CREATE NONCLUSTERED INDEX IX_Duyurular_Aktif_Sira ON dbo.Duyurular(Aktif, Sira, Tarih DESC);
GO

/* ---------------------------------------------------------
   Canli sohbet (SignalR): mesajlar ve susturmalar
   --------------------------------------------------------- */
IF OBJECT_ID(N'dbo.SohbetMesajlari', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SohbetMesajlari
    (
        Id          INT IDENTITY(1,1) NOT NULL,
        KullaniciId INT               NOT NULL,
        Metin       NVARCHAR(300)     NOT NULL,
        StickerId   VARCHAR(40)       NULL,
        Tarih       DATETIME2         NOT NULL CONSTRAINT DF_SohbetMesajlari_Tarih DEFAULT (SYSUTCDATETIME()),
        Silindi     BIT               NOT NULL CONSTRAINT DF_SohbetMesajlari_Silindi DEFAULT (0),
        CONSTRAINT PK_SohbetMesajlari PRIMARY KEY (Id),
        CONSTRAINT FK_SohbetMesajlari_Kullanicilar FOREIGN KEY (KullaniciId) REFERENCES dbo.Kullanicilar(Id)
    );
END
GO
IF COL_LENGTH(N'dbo.SohbetMesajlari', N'StickerId') IS NULL ALTER TABLE dbo.SohbetMesajlari ADD StickerId VARCHAR(40) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SohbetMesajlari_Tarih' AND object_id = OBJECT_ID(N'dbo.SohbetMesajlari'))
    CREATE NONCLUSTERED INDEX IX_SohbetMesajlari_Tarih ON dbo.SohbetMesajlari(Tarih DESC);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SohbetMesajlari_KullaniciId' AND object_id = OBJECT_ID(N'dbo.SohbetMesajlari'))
    CREATE NONCLUSTERED INDEX IX_SohbetMesajlari_KullaniciId ON dbo.SohbetMesajlari(KullaniciId);
GO

IF OBJECT_ID(N'dbo.SohbetSusturmalari', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SohbetSusturmalari
    (
        KullaniciId INT               NOT NULL,
        Bitis       DATETIME2         NOT NULL,
        Neden       NVARCHAR(200)     NULL,
        Tarih       DATETIME2         NOT NULL CONSTRAINT DF_SohbetSusturmalari_Tarih DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_SohbetSusturmalari PRIMARY KEY (KullaniciId),
        CONSTRAINT FK_SohbetSusturmalari_Kullanicilar FOREIGN KEY (KullaniciId) REFERENCES dbo.Kullanicilar(Id) ON DELETE CASCADE
    );
END
GO

/* ---------------------------------------------------------
   Kullanici tercihleri (seviye odulleri: ad rengi, yazi tipi, parlama, rozet, unvan)
   --------------------------------------------------------- */
IF OBJECT_ID(N'dbo.KullaniciTercihleri', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.KullaniciTercihleri
    (
        KullaniciId INT           NOT NULL,
        AdRengi     NVARCHAR(20)  NULL,
        MesajFontu  NVARCHAR(20)  NULL,
        Parlak      BIT           NOT NULL CONSTRAINT DF_KullaniciTercihleri_Parlak DEFAULT (0),
        Animasyon   BIT           NOT NULL CONSTRAINT DF_KullaniciTercihleri_Animasyon DEFAULT (0),
        Rozet       BIT           NOT NULL CONSTRAINT DF_KullaniciTercihleri_Rozet DEFAULT (0),
        AdEfekti    NVARCHAR(20)  NULL,
        Unvan       NVARCHAR(24)  NULL,
        CONSTRAINT PK_KullaniciTercihleri PRIMARY KEY (KullaniciId),
        CONSTRAINT FK_KullaniciTercihleri_Kullanicilar FOREIGN KEY (KullaniciId) REFERENCES dbo.Kullanicilar(Id) ON DELETE CASCADE
    );
END
GO

/* ---------------------------------------------------------
   Bildirimler (yeni bolum, yorum yaniti, sohbet etiketi)
   --------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Bildirimler', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Bildirimler
    (
        Id          INT IDENTITY(1,1) NOT NULL,
        KullaniciId INT               NOT NULL,
        Tur         VARCHAR(20)       NOT NULL CONSTRAINT DF_Bildirimler_Tur DEFAULT ('bilgi'),
        Baslik      NVARCHAR(250)     NOT NULL,
        Baglanti    NVARCHAR(300)     NULL,
        Okundu      BIT               NOT NULL CONSTRAINT DF_Bildirimler_Okundu DEFAULT (0),
        Tarih       DATETIME2         NOT NULL CONSTRAINT DF_Bildirimler_Tarih DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_Bildirimler PRIMARY KEY (Id)
    );
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Bildirimler_KullaniciId_Okundu_Tarih' AND object_id = OBJECT_ID(N'dbo.Bildirimler'))
    CREATE NONCLUSTERED INDEX IX_Bildirimler_KullaniciId_Okundu_Tarih ON dbo.Bildirimler(KullaniciId, Okundu, Tarih DESC);
GO
