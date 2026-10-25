# Kage 95

Mobil uyumlu, minimal kırmızı-siyah tasarımlı Türkçe manga okuma ve arşiv platformu.

## Özellikler

- **Okuyucu ve Katalog:** Klasik (sayfa sayfa) ve Webtoon (dikey kaydırma) okuma modları, gece okuma parlaklık ayarı, klavye kısayolları (`←`/`→`, `A`/`D`, `?`), kaldığın bölümden devam etme, dönemlik "En çok okunanlar" (hafta / ay / tüm zamanlar) ve öne çıkanlar vitrini.
- **Kütüphane ve İlerleme:** Serileri kütüphaneye kaydetme, okuma durumu sekmeleri (`Tümü`, `Okuyorum`, `Okunacak`, `Bitirdim`), okunan bölümleri işaretleme ve okuma geçmişi.
- **Yorum Sistemi (v2):** Seri ve bölüm yorumları, yanıtlama, düzenleme/silme, beğeni, şikayet bildirme, `||gizli||` spoiler bulanıklaştırma ve biçimlendirme araç çubuğu (`**kalın**`, `*eğik*`, renk etiketleri).
- **Canlı Sohbet:** SignalR tabanlı gerçek zamanlı sohbet şeridi ve tam sayfa sohbet ekranı (`#/sohbet`), özel SVG çıkartma (sticker) paketleri, çevrimiçi sayacı ve hız sınırı.
- **Puan, Seviye ve Liderlik:** Bölüm okuma, yorum yazma, puan verme ve günlük girişlerle puan kazanma; seviye ve rütbe sistemi (`Çırak` -> `Efsane`); seviyeye göre açılan ad renkleri, yazı tipleri, parıltı efektleri, rozetler ve özel unvanlar; haftalık/aylık/tüm zamanlar liderlik tablosu (`#/liderlik`).
- **Tema Sistemi:** 4 hazır tema (`Kage`, `Kağıt`, `Mono`, `Gece`) + cihaz tercihini izleyen `Sistem` modu.
- **Yönetim Paneli (`admin.html`):** Rol ve granüler izin sistemi (`Owner`, `Admin`, `User`), seri/bölüm/kategori yönetimi, sürükle-bırak ve sıra numaralı sayfa yöneticisi, toplu görsel yükleme, öne çıkanlar ve duyuru yönetimi, yorum şikayetleri, sohbet moderasyonu (mesaj silme / kullanıcı susturma), denetim kayıtları ve site ayarları.

## Teknoloji Yığını

| Katman | Teknoloji |
| --- | --- |
| **Backend** | ASP.NET Core (.NET 10), Entity Framework Core, SignalR, JWT + BCrypt, Swagger/OpenAPI |
| **Veritabanı** | Microsoft SQL Server (`.\SQLEXPRESS`, `Kage95DB`) |
| **Ön Yüz** | Tek dosyalı React 18 (CDN + Babel), Vanilla JS modülleri (`js/`), özel CSS değişkenleri (`css/`) |

## Proje Yapısı

```text
├── backend/                  # ASP.NET Core Web API (.NET 10)
│   ├── Controllers/          # REST API uçları (Manga, Bölüm, Yorum, Sohbet, Puan, Admin vb.)
│   ├── Data/                 # AppDbContext (EF Core)
│   ├── Hubs/                 # SignalR SohbetHub (/hub/sohbet)
│   ├── Models/               # Varlıklar (Entities) ve DTO'lar
│   ├── Security/             # [Yetki] izin filtresi, hesap durumu kontrolleri
│   └── Services/             # Puan, ödül, yorum, denetim, dosya yükleme servisleri
├── css/                      # Ortak vurgu stilleri (vurgu.css)
├── js/                       # Ortak ön yüz modülleri (config, tema, yorum, sohbet, liderlik, puan, adstili, yardim)
├── index.html                # Ana sayfa, seri detay, kütüphane, profil, liderlik, sohbet
├── read.html                 # Manga okuyucu (klasik + webtoon)
├── login.html                # Giriş ve kayıt sayfası
└── admin.html                # Yönetim paneli
```

## Kurulum ve Çalıştırma

### Gereksinimler

- [.NET 10 SDK](https://dotnet.microsoft.com/)
- SQL Server veya SQL Server Express (`.\SQLEXPRESS`)
- Statik dosyaları sunmak için VS Code Live Server eklentisi veya Python (`python -m http.server`)

### 1. Veritabanını Hazırlama

SQL Server Management Studio (SSMS) veya `sqlcmd` ile `backend/Veritabani_Kurulumu.sql` betiğini çalıştırarak `Kage95DB` veritabanını ve tabloları oluşturun:

```powershell
sqlcmd -S .\SQLEXPRESS -E -i backend/Veritabani_Kurulumu.sql
```

### 2. Backend Yapılandırması

`backend/appsettings.Local.json` adında bir dosya oluşturun (bu dosya `.gitignore` içindedir) ve en az 32 karakterlik bir JWT gizli anahtarı tanımlayın:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.\\SQLEXPRESS;Database=Kage95DB;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "JwtSettings": {
    "Secret": "buraya-en-az-32-karakterlik-uzun-ve-rastgele-bir-gizli-anahtar-yazin"
  }
}
```

> Alternatif olarak `JwtSettings__Secret` ortam değişkenini de tanımlayabilirsiniz.

### 3. Backend API'yi Başlatma

```powershell
cd backend
dotnet run
```

API varsayılan olarak `http://localhost:5035` adresinde çalışır. Geliştirme ortamında Swagger arayüzüne `http://localhost:5035/swagger` adresinden ulaşabilirsiniz.

### 4. Ön Yüzü Başlatma

Proje kök dizininde herhangi bir statik dosya sunucusu çalıştırın:

```powershell
python -m http.server 5500
```

Ardından tarayıcıda `http://localhost:5500` adresini açın.

> Ön yüz varsayılan olarak `http://localhost:5035/api` adresine bağlanır (`js/config.js`). Farklı bir API adresi kullanmak için tarayıcı konsolunda `localStorage.setItem('kage95_api', 'http://adres:port/api')` tanımlayabilirsiniz.

## Yetkilendirme ve Site Sahibi (Owner)

- `Owner` rolü yalnızca veritabanı üzerinden atanır; API aracılığıyla verilemez veya alınamaz.
- `Veritabani_Kurulumu.sql` içindeki varsayılan kurala göre `Jagwey` kullanıcı adlı hesap oluşturulduktan sonra betik çalıştırıldığında otomatik olarak `Owner` rolüne yükseltilir; veya doğrudan SQL ile atanabilir:
  ```sql
  UPDATE dbo.Kullanicilar SET Rol = N'Owner' WHERE KullaniciAdi = N'KullaniciAdiniz';
  ```
- Site sahibi `admin.html` üzerinden diğer kullanıcılara `Admin` rolü ve granüler izinler (`manga.yaz`, `bolum.duzenle`, `yorum.sil`, `sohbet.yonet` vb.) tanımlayabilir.
