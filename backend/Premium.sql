-- Mevcut depo SQL Server kullanır. Üretim yedeğinden sonra bir kez çalıştırın.
IF OBJECT_ID(N'dbo.PremiumUyelikler', N'U') IS NULL
BEGIN
  CREATE TABLE dbo.PremiumUyelikler (
    KullaniciId INT NOT NULL PRIMARY KEY,
    BitisUtc DATETIME2 NOT NULL,
    IptalEdildi BIT NOT NULL DEFAULT 0,
    CONSTRAINT FK_Premium_Kullanici FOREIGN KEY (KullaniciId) REFERENCES dbo.Kullanicilar(Id)
  );
END;
-- Hakları yalnızca doğrulanmış ödeme webhook'u veya yönetici işlemi güncellemeli.
-- Satın alma dönüş URL'i, istemci isteği veya JWT rolü ödeme kanıtı değildir.
