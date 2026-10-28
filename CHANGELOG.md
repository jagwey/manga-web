
## Unreleased

### Gelistirme / CI (2026-10-28)
- GitHub Actions CI genisletildi; Dependabot + gitleaks guvenlik workflow
- Playwright e2e iskeleti (	ests/e2e)
- Cursor skills/rules eklendi
- AnalysisModeSecurity + CA5394 pragma (auth kullanici adi soneki)
# 📋 Değişiklik Günlüğü (Changelog)

KAGE 95 projesindeki tüm önemli değişiklikler, yeni özellikler, güvenlik güncellemeleri ve hata düzeltmeleri bu dosyada belgelenmektedir.

Proje [SemVer (Semantic Versioning)](https://semver.org/lang/tr/) standartlarına uygun olarak sürümlendirilmektedir.

---

## [2.6.0] - 2026-10-28 *(Mevcut Son Sürüm)*

### 🔐 Hesap & Kimlik
- **E-posta Doğrulama:** Kayıttan sonra 3 gün geçerli HMAC imzalı doğrulama bağlantısı gönderilir (`/login.html?eposta-dogrula=`). Giriş engellenmez; doğrulanana kadar sitede uyarı bandı ve "Tekrar gönder" düğmesi (`POST /api/auth/resend-verification`) görünür.
- **Şifre Tekrarı:** Kayıt ve yeni şifre formlarına ikinci şifre alanı eklendi; eşleşmeyen şifre istemcide engellenir.
- **Mail ile Şifre Sıfırlama:** Brevo SMTP üzerinden `noreply@kage95.com` göndericisiyle, 30 dakika geçerli tek kullanımlık bağlantı.
- **Google & Discord ile Giriş:** OAuth sağlayıcıları yapılandırıldığında giriş sayfasında görünür.
- **Açık Kayıt Hataları:** Çakışma durumunda hatanın kullanıcı adında mı e-postada mı olduğu belirtilir (`409`, `alan` alanı ile).

### 🔎 SEO
- Meta description, canonical, Open Graph/Twitter etiketleri, JSON-LD `WebSite`, `robots.txt` ve `sitemap.xml` eklendi.

### 🐛 Düzeltmeler
- Sosyal giriş sonrası 500 hatası: açılış SQL'inde yeni kolon üzerindeki `CREATE INDEX` komutları `EXEC(N'...')` ile ertelendi.

---

## [2.5.0] - 2026-10-26

### 🛡️ Kusursuz SPA Routing & Fallback
- **Doğrudan Link Desteği:** Artık doğrudan girilen temiz URL bağlantıları (`/manga/18`, `/iletisim`, `/kutuphane`, `/forum`, `/sohbet`, `/liderlik`, `/profil`) tarayıcıda doğrudan hedeflenen sayfayı açar.
- **F5 Yenileme Garantisi:** ASP.NET Core tarafına eklenen akıllı `app.MapFallback` mekanizması ile sayfa yenilendiğinde 404 hatası veya sayfa kaybı tamamen engellendi.
- **API İzolasyonu:** `/api/*` ve `/hub/*` uçları fallback kapsamından ayrı tutularak saf JSON/WebSocket bütünlüğü korundu.

### ⚡ Kurşun Geçirmez PWA / Service Worker (v48)
- **Sıfır Çökme Garantisi:** Servis çalışanında gezinme isteklerinde `ERR_INVALID_RESPONSE` oluşmasına yol açan `undefined` yanıt problemi kalıcı olarak çözüldü; her koşulda geçerli bir Response dönecek şekilde zırhlandı.
- **Anında Güncelleme:** `sw.js` için `Cache-Control: no-cache, no-store, must-revalidate` başlıkları eklenerek tarayıcı ve Cloudflare önbellek kilitlenmesi önlendi.

### 🎨 Siber-Gotik Manga Vitrini & Dinamik Şerit
- **Taşmayan Cam Kapak Vitrini:** Manga detay sayfasında dikey/yatay kapakların kesilmesini, taşmasını veya düşük çözünürlüklü görünmesini engelleyen cam panel (`contain` ve `glassmorphism`) tasarımı uygulandı.
- **Akıllı Okuma Şeridi:** Kullanıcının okuma geçmişi yerel hafızadan taranarak kapak üzerine dinamik şerit entegre edildi:
  - Seri okunuyorsa: *"Kaldığın Yerden Devam Et (Bölüm X)"*
  - Seri hiç okunmamışsa: *"İlk Bölümden Başla (Bölüm 1)"*
- **Yumuşak Dokunma Hissi:** Tüm manga kartlarına mobil ve masaüstü uyumlu akıcı dokunma/hover mikromimikleri eklendi.

### 💬 Yenilenen İletişim Portalı
- **Sade ve Güçlü UX:** Gereksiz form kutuları kaldırılarak doğrudan resmi Discord topluluğu (`discord.gg/yxZZ7m2x8a`) ve e-posta kanallarına odaklanan modern siber-gotik kart tasarımı entegre edildi.
- **Navigasyon Senkronizasyonu:** İletişim sayfasında üst ve alt menülerin aktif sekme vurgusu kusursuz hale getirildi.

---

## [2.4.0] - 2026-10-26

### ✨ Akıcı Mikro-Etkileşimler (Smooth Animations)
- **Hamburger Dönüşümü:** 3 çizgili mobil menü butonunun yumuşak ve akıcı 'X' şekline geçiş animasyonu.
- **Kayan Çekmece & Modallar:** Yan navigasyon çekmecesinin (drawer) ve modal pencerelerin `cubic-bezier(0.16, 1, 0.3, 1)` eğrisiyle açılması.
- **Profil Menüsü Süzülmesi:** Kullanıcı profil kartının hafif yaylanarak yumuşak açılış efekti.
- **Okuyucu Sonu Etkileşimleri:** Bölüm bittiğinde zarifçe beliren bölüm sonu kartı, yüzen sayfa balonu ve spoiler metinlerinin şık açılma geçişi.

---

## [2.3.0] - 2026-10-26

### 🔒 Kapsamlı Siber Güvenlik Zırhı
- **DDoS ve Hız Sınırı (Rate Limiting):** IP başına kayıt (10 istek/30dk), giriş (30 istek/5dk) ve genel okuma (120 istek/1dk) hız sınırları devreye alındı.
- **Bellek Doyurma (RAM Exhaustion) Koruması:** Bölüm başına azami 150 sayfa, arama sorgularında 60 karakter, yorumlarda 1000 karakter limiti zorunlu kılındı.
- **WebSocket Fırtınası Koruması:** SignalR sohbetinde IP başına 3 saniyede en fazla 2 mesaj izni verildi ve RAM sızmasını önleyen döngüsel bellek tamponu (50 mesaj) kuruldu.
- **Timing Attack Önleme:** BCrypt şifre kontrolleri sabit zamanlı karşılaştırma mimarisine geçirilerek zamanlama analiziyle kullanıcı keşfi engellendi.
- **SQL Server Dayanıklılığı:** `Max Pool Size=100`, 15 saniye komut zaman aşımı ve bağlantı havuzu tükenmelerini önleyen `AsNoTracking()` mimarisi uygulandı.
- **Cloudflare Edge Koruması:** Sahte `X-Forwarded-For` başlıklarını engellemek amacıyla yalnızca Cloudflare IP bloklarına güvenen `ForwardedHeaders` filtresi entegre edildi.

---

## [2.2.0] - 2026-10-25

### 👥 Birleşik Topluluk Ekosistemi
- **Topluluk Çatısı:** Forum ve Sohbet tek bir sezgisel "Topluluk" başlığında birleştirildi.
- **Masaüstü Dropdown:** Fare üzerine geldiğinde şıkça açılan cam efektli topluluk menüsü.
- **Gelişmiş Bölüm Yönetimi:** 100+ bölümlü mangalar için anında numara/ad araması ve **`☵ Izgara`** / **`☰ Liste`** görünümü değiştirme imkânı.
- **Dağıtım Otomasyonu:** `derle.mjs` aracıyla otomatik Babel minification ve SHA-1 içerik özeti (cache-buster) boru hattı kuruldu.

---

## [2.1.0] - 2026-10-25

### 📱 Mobil & Görsel Optimizasyon
- **Hero Carousel:** Kesintisiz otomatik kayan, tıklandığında doğrudan seriye giden yüksek çözünürlüklü afiş slaytı (Vinland Saga vb.).
- **WebP Sıkıştırma:** Sunucuya yüklenen görsellerin otomatik WebP formatına çevrilmesi ve bant genişliği tasarrufu.
- **Mobil Dokunmatik İyileştirmeler:** iOS 16px font kuralı ile otomatik yakınlaşma engellendi, `safe-area-inset` dolguları eklendi.

---

## [2.0.0] - 2026-10-24

### 🚀 Büyük Canlı Yayın Lansmanı
- **Canlı Domain:** [kage95.com](https://kage95.com) üzerinde Cloudflare SSL ve IIS ASP.NET Core .NET 10 arkasında canlıya geçiş.
- **PWA Desteği:** Masaüstü ve mobil cihazlara tek tıkla uygulama olarak yüklenebilme özelliği.
- **Canlı Sohbet (SignalR):** Gerçek zamanlı WebSocket tabanlı sohbet odası ve çıkartma (sticker) paketi.
- **Oyunlaştırma:** XP kazanımı, dinamik rütbeler (`Çırak` → `Efsane`), unvanlar ve liderlik tablosu.
- **Veritabanı:** Microsoft SQL Server `Kage95DB` üretim veritabanı kurulumu.

---

## [1.0.0] - 2026-10-20

### 🐣 İlk Çekirdek Sürüm
- **Çift Okuma Modu:** Kesintisiz dikey Webtoon modu ve geleneksel Klasik manga çift sayfa modu.
- **REST API:** Manga, Kategori, Bölüm ve Kullanıcı yönetim uçları.
- **Gotik Koyu Tema:** Kırmızı-siyah minimalist KAGE estetiği.
