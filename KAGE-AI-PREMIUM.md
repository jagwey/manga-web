# Kage AI ve Premium kurulumu

## Doğrulanan mimari

Bu depo ASP.NET Core 10 + EF Core **SQL Server** kullanıyor; MySQL değil.
Veritabanı sağlayıcısı değiştirilmedi. Gerçek MySQL kullanan başka sürüm varsa o kod incelenmeden bu SQL uygulanmamalı.
Bot `aiRouterServisi.js` üzerinden sağlayıcı yönlendiriyor; `mangaServisi.js` site `/api/mangas` verisini okuyor.
Site iki hikâye arayüzü içeriyordu: EVREN backend bileşeni ve doğrudan Pollinations çağıran script.
Tek arayüze indirildi; tarayıcı yalnızca site backend'ini çağırır.

## Yeni davranış

Backend yayınlanan/silinmemiş katalogdan en fazla 25 aday bulur. Küçük katalogda tüm adaylar,
büyük katalogda sözcük önekleri ve hafıza/gizem gibi terimlerle ön seçim kullanılır.
Bu düşük maliyetli yöntem, büyük katalogda eşanlamlı ama sözcüksel eşleşmeyen eserleri kaçırabilir.
AI yalnızca ID döndürür; ID'ler hem bot API'sinde hem backend'de izinli adaylarla kesiştirilir.
Sonuçlar sunulmadan önce yayın durumu tekrar veritabanından okunur. AI hata verirse kelime eşleşmesi gösterilir.
Favoriler açık tercih ve geçerli JWT gerektirir; yalnızca oturum sahibinin son 5 favorisi kullanılır.
Kullanıcı kimliği veya Discord konuşmaları sağlayıcıya gönderilmez.

Premium sayfası `/premium.html`: **9,99 TL / ay**. Kullanıcı talebiyle ödeme bağlantısı sonraya bırakıldı;
satış butonu kapalı, satın alma API'si 503 döner ve hiçbir üyelik açmaz.
Haklar `PremiumUyelikler` tablosundaki bitiş/iptal durumundan gelir; istemci veya JWT premium iddiası kabul edilmez.
Premium: günde 50 hikâye araması, ayrıca 10 sayfa sorusu; normal üye 10, misafir IP başına 3 hikâye araması.
Kotalar UTC gününde yenilenir; başarısız denemeler ve cache isabetleri de sayılır.
Sayaçlar tek süreç belleğindedir, yeniden başlatmada sıfırlanır. Çoklu sunucuya veya gerçek ücretli satışa
geçmeden kalıcı atomik DB/Redis kotası ve ödeme webhook'u eklenmelidir.

Bölüm araçları okuyucudaki Premium bağlantısından açılır. İndirme bölüm başınadır (ZIP, en fazla 300 sayfa/100 MB).
Yalnızca sitenin yerel `wwwroot/uploads/<32 haneli GUID>.jpg|jpeg|png|webp` görselleri desteklenir;
uzak CDN/eksik dosyalarda 409 gösterilir. Var olan herkese açık sayfa görsellerine DRM eklenmez;
premium, hazır ZIP indirme kolaylığı sağlar.
AI sorusu seçilen **tek sayfanın** gerçek görselini okur (en fazla 1,5 MB, Gemini gerekli).
Tüm bölümü okuduğu iddia edilmez. Görsel yanıtları olasılıksaldır; testler edebî doğruluğu garanti etmez.

## Yayına alma

1. İki depoya yamaları uygulayın, aşağıdaki testleri çalıştırın. Bot API kurulumunu diğer deponun
   `MANGA-AI-API.md` belgesine göre yapın; mevcut Discord sürecini değiştirmeyin.
2. Mevcut SQL Server veritabanını yedekleyin; `backend/Premium.sql` dosyasını o veritabanında çalıştırın.
   Tablo oluşturulmadan yeni üyelik kullanan backend'i başlatmayın. Mevcut veriler silinmez.
3. Bot ortamına en az 32 karakterli rastgele `KAGE_AI_SHARED_SECRET` ve mevcut sağlayıcı anahtarlarını koyun.
   Yeni ortak anahtar Discord token'ı değildir. Aynı ortak anahtarı siteye aşağıdaki `KageAi__Secret` olarak tanımlayın.
4. Site sunucusuna ortam ayarları ekleyin (örnek alan adını gerçek TLS adresiyle değiştirin):

```text
HikayeArama__Etkin=true
KageAi__Etkin=true
KageAi__Url=https://AI-ALAN-ADINIZ/v1/manga/rank
KageAi__Secret=SUNUCUDA_URETILEN_AYNI_GIZLI_ANAHTAR
KageAi__MisafirGunlukLimit=3
KageAi__UyeGunlukLimit=10
KageAi__PremiumGunlukLimit=50
KageAi__BolumGunlukLimit=10
Evren__Etkin=false
```

5. `dotnet publish backend/backend.csproj -c Release -o publish` ile backend'i hazırlayın.
   Güncel `index.html`, `read.html`, `premium.html` ve `js/` dosyalarını da yayınlayın.
   `yayinla.ps1` premium sayfasını kapsar; bu oturumda yayın komutu çalıştırılmadı.
6. HTTPS üzerinden yetkisiz bot API isteğinin 401, site aramasının sadece katalog kaydı döndürdüğünü
   doğrulayın. Site arama örneği: “Ana karakterin geçmişini hatırlamadığı, gizem ve psikolojik gerilim içeren bir manga arıyorum”.
   Gerçek katalog/sağlayıcı anahtarları bu çalışma ortamında yoktu; gerçek öneri kalitesi ayrıca ölçülmeli.

Ödeme sağlayıcısı seçildiğinde: fiyatı backend'de 999 kuruş/TRY olarak sabitleyin; imzalı webhook'u doğrulayın,
işlem kimliğini benzersiz saklayın, tekrarlı bildirimleri idempotent işleyin; yalnızca doğrulanmış başarılı
ödeme bitiş süresini uzatsın. İade/iptal olaylarını uygulayın. Tarayıcı dönüş sayfası üyelik açmamalı.
Bu ödeme kısmı henüz uygulanmadı; premium'u halka satışa açmayın.

## Test komutları

```powershell
# manga-web kökünde
dotnet build backend/backend.csproj
dotnet run --project tests/ai-backend/ai-backend.csproj
Set-Location tests/e2e
npm ci --ignore-scripts
$env:E2E_PORT='5517'
$env:CI='1'
npx playwright test hikaye-arama.spec.mjs kage-ai.spec.mjs premium.spec.mjs
# bot kökünde
npm install --ignore-scripts --no-package-lock
npm test
```

Backend testleri EF InMemory ve sahte HTTPS kullanır; gerçek SQL/MySQL bağlantısını test etmez.
Derleyici CA3003 dosya yolu uyarıları verdi: dosya yolları yalnızca katalog kaydından alınır ve
GUID dosya adı/uzantı, sabit uploads kökü, origin, yol kaçışı ve reparse point kontrolleriyle sınırlanır.
Yol kaçışı ve dış URL reddi test edildi; uyarılar gizlenmedi.
Bu makinede Node 26.12.1 ve .NET 10.0.403 kullanıldı; bot package.json Node 20.x ister.
Yayındaki Node sürümünde ayrıca test çalıştırın.

Doğrulama sonucu: 24 bot/API testi, 18 backend kontrolü ve 10 masaüstü/mobil tarayıcı testi geçti.

## Geri alma

`KageAi__Etkin=false` ve `HikayeArama__Etkin=false` yapıp siteyi yeniden başlatın; ortak AI sürecini durdurun.
Eski site paketini geri yükleyebilirsiniz. Yeni premium tablosunu tutmak mevcut botu etkilemez.
Discord botunun giriş/komut/olay akışı ve varsayılan AI davranışı korunmuştur.
