// Sağ alttaki "Yardım" rehberi: konulara bölünmüş hazır cevaplı, anahtar kelime + benzerlik eşleştirmeli küçük bir sohbet botu.
// Gerçek bir yapay zeka değildir; harici API yok, her şey tarayıcıda çalışır. Cevaplar aşağıdaki SORULAR listesinden gelir.
// Yeni soru eklemek için SORULAR dizisine kayıt ekleyin (konu: KONULAR içindeki id). Yeni konu için KONULAR'a kayıt ekleyin.
// Node ile test: require('./js/yardim.js').bul('şifremi unuttum') (tarayıcı dışında arayüz kurulmaz, yalnızca eşleştirme dışa açılır).
(function () {
    'use strict';
    var tarayici = typeof window !== 'undefined' && typeof document !== 'undefined';
    if (tarayici) {
        if (window.__kage95Yardim) return;
        window.__kage95Yardim = true;
    }

    // İletişim sayfası (#/iletisim) yalnızca index.html'de var; read.html'den de çalışsın diye tam yol.
    var ILETISIM = { ad: 'İletişim sayfası', url: 'index.html#/iletisim' };
    var HESABIM = { ad: 'Hesabım', url: 'index.html#/profil' };

    // Konu düğmeleri bu sırayla görünür. yonetici: true olan konu ve soruları yalnızca yöneticilere görünür ve eşleşir.
    var KONULAR = [
        { id: 'yonetim', ad: 'Yönetim paneli', yonetici: true },
        { id: 'benim', ad: 'Benim hesabım' },
        { id: 'hesap', ad: 'Hesap ve giriş' },
        { id: 'okuma', ad: 'Okuma' },
        { id: 'ayar', ad: 'Okuma ayarları' },
        { id: 'favori', ad: 'Favoriler ve kütüphane' },
        { id: 'kesfet', ad: 'Arama ve keşfet' },
        { id: 'puan', ad: 'Puan, seviye ve liderlik' },
        { id: 'yorum', ad: 'Yorumlar ve sohbet' },
        { id: 'bildirim', ad: 'Bildirimler' },
        { id: 'gorunum', ad: 'Tema ve görünüm' },
        { id: 'mobil', ad: 'Mobil ve uygulama' },
        { id: 'sorun', ad: 'Sorun giderme' },
        { id: 'iletisim', ad: 'İletişim' }
    ];

    // anahtar: kullanıcı yazısında geçen kökler/ifadeler (Türkçe karakter ve büyük/küçük harf duyarsız).
    // Kökler ek almış kelimeleri de yakalar ("favori" -> "favorilerim"); 5+ harfli köklerde 1 harflik yazım hatası tolere edilir.
    // Cevaplar sitedeki gerçek özelliklere dayanır (index.html, read.html, login.html, js/*.js); emin olunmayan özellik yazılmaz.
    var SORULAR = [
        // --- Benim hesabım (kişisel) ---
        // niyet: cevap giriş yapmış kullanıcının verisinden (ya da bu cihazdaki okuma kaydından) üretilir; veri yoksa "cevap" gösterilir.
        // takip: "peki o nasıl" gibi devam sorularında açılacak sorunun başlığı.
        { konu: 'benim', soru: 'Hesabımda kimim?', niyet: 'ad', takip: 'Seviyem ve puanım kaç?',
          anahtar: ['adim ne', 'kullanici adim', 'ben kimim', 'hesabimda kimim', 'hesabim','hesap bilgilerim', 'kimle giris','beni taniyor', 'adimi biliyor'],
          cevap: 'Seni tanıyabilmem için giriş yapman gerekiyor. Giriş yapınca adını, seviyeni, favorilerini ve okuma geçmişini söyleyebilirim.',
          baglanti: { ad: 'Giriş yap', url: 'login.html' } },
        { konu: 'benim', soru: 'Seviyem ve puanım kaç?', niyet: 'seviye', takip: 'Nasıl seviye atlarım?',
          anahtar: ['seviyem', 'puanim', 'rutbem', 'kac puan', 'levelim', 'puan durumum', 'kacinci seviye', 'seviyem kac', 'puanim kac', 'kac xp'],
          cevap: 'Seviyeni ve puanını görebilmem için giriş yapman gerekiyor. Giriş yapınca profil sayfanda da puan geçmişini görebilirsin.',
          baglanti: { ad: 'Giriş yap', url: 'login.html' } },
        { konu: 'benim', soru: 'Nasıl seviye atlarım?', niyet: 'atla', takip: 'Puan nasıl kazanılır?',
          anahtar: ['seviye atla', 'level atla', 'seviye yuksel', 'rutbe atla', 'sonraki seviye', 'level up', 'seviye kasma', 'seviyemi artir', 'seviye artir'],
          cevap: 'Seviye puanla yükselir: bölümü sonuna kadar okumak, yorum yapmak, puan vermek, kütüphaneye seri eklemek ve her gün giriş yapmak puan kazandırır. Giriş yaparsan sonraki seviyeye kaç puan kaldığını söyleyebilirim.',
          baglanti: { ad: 'Giriş yap', url: 'login.html' } },
        { konu: 'benim', soru: 'Favorilerimde neler var?', niyet: 'favori', takip: 'Favorilere nasıl eklerim?',
          anahtar: ['favorilerimde', 'favorilerim neler', 'favorilerimi goster', 'kac favori', 'listemde ne', 'kutuphanemde ne', 'takip ettiklerim'],
          cevap: 'Favorilerini görebilmem için giriş yapman gerekiyor. Giriş yapınca kütüphanendeki serileri sayabilirim.',
          baglanti: { ad: 'Giriş yap', url: 'login.html' } },
        { konu: 'benim', soru: 'En son ne okuyordum?', niyet: 'devam', takip: 'Kaç bölüm okudum?',
          anahtar: ['en son ne', 'son okudugum', 'en son okudugum', 'ne okuyordum', 'son okuduklarim', 'okumaya devam'],
          cevap: 'Bu cihazda kayıtlı bir okuma bulamadım. Bir bölüm açtığında kaldığın yeri hatırlarım; ana sayfada "Okumaya devam et" şeridi çıkar.' },
        { konu: 'benim', soru: 'Kaç bölüm okudum?', niyet: 'sayi', takip: 'En son ne okuyordum?',
          anahtar: ['kac bolum okudum', 'kac bolum', 'okudugum bolum', 'ne kadar okudum', 'okuma gecmisim', 'gecmisim', 'okuduklarim'],
          cevap: 'Bu cihazda henüz açılmış bir bölüm kaydı yok. Okuduğun bölümler seri sayfasında "Okundu" diye işaretlenir.' },
        // --- Hesap ve giriş ---
        { konu: 'hesap', soru: 'Nasıl kayıt olurum?', anahtar: ['kayit', 'uye ol', 'uyelik', 'hesap ac', 'hesap olustur', 'yeni hesap'],
          cevap: 'Sağ üstteki "Kayıt ol" düğmesine bas; kullanıcı adı, e-posta ve şifre (en az 8 karakter) yeterli. Hesap açmak ücretsiz.',
          baglanti: { ad: 'Kayıt ol', url: 'login.html?kayit=1' } },
        { konu: 'hesap', soru: 'Giriş yapamıyorum', anahtar: ['giris', 'giremiyorum', 'giris yapamiyorum', 'oturum ac', 'hesaba gir', 'login'],
          cevap: 'Kullanıcı adını ya da e-postanı ve şifreni kontrol et; kullanıcı adında büyük/küçük harf fark etmez, şifrede eder. Çok sayıda hatalı denemeden sonra bir süre beklemen gerekebilir.',
          baglanti: { ad: 'Giriş sayfası', url: 'login.html' } },
        { konu: 'hesap', soru: 'Şifremi unuttum', anahtar: ['sifremi unuttum', 'sifre unut', 'parola unut', 'unuttum', 'sifre sifirla', 'sifremi sifirla', 'sifre yenile'],
          cevap: 'Şifre sıfırlama henüz yok. Hesabına erişemiyorsan İletişim sayfasındaki e-posta ya da Instagram adresinden site sahibine yaz; kullanıcı adını belirtmeyi unutma.',
          baglanti: ILETISIM },
        { konu: 'hesap', soru: 'Şifremi nasıl değiştiririm?', anahtar: ['sifre degistir', 'sifremi degistir', 'sifre guncelle', 'parola degistir', 'yeni sifre'],
          cevap: 'Profiline bas, "Hesabım"ı aç ve "Şifre değiştir" bölümünü kullan. Yeni şifre en az 8 karakter olmalı; değiştirince diğer cihazlardaki oturumların kapanır.',
          baglanti: HESABIM },
        { konu: 'hesap', soru: 'Kullanıcı adımı ya da e-postamı nasıl değiştiririm?', anahtar: ['kullanici adi', 'adimi degistir', 'isim degistir', 'e posta degistir', 'email degistir', 'mail degistir', 'eposta'],
          cevap: 'Profiline bas, "Hesabım"ı aç. Orada kullanıcı adını ve e-postanı değiştirebilirsin (şifreni girmen gerekir).',
          baglanti: HESABIM },
        { konu: 'hesap', soru: 'Profil resmi nasıl eklerim?', anahtar: ['profil resmi', 'avatar', 'fotograf', 'profil foto', 'resim yukle', 'pp'],
          cevap: 'Profiline bas, "Hesabım"ı aç ve "Profil resmi" bölümünden JPG, PNG ya da WebP (en fazla 2 MB) yükle. İlk profil resmi +10 puan kazandırır.',
          baglanti: HESABIM },
        { konu: 'hesap', soru: 'Başkalarının profilini görebilir miyim?', anahtar: ['baskasinin profil', 'kullanici profil', 'profiline bak', 'herkese acik profil', 'uye profil'],
          cevap: 'Evet. Yorumlarda, sohbette ve liderlik tablosunda bir kullanıcı adına basınca o kişinin herkese açık profili açılır; orada kütüphanesini de görebilirsin.' },
        { konu: 'hesap', soru: 'Nasıl çıkış yaparım?', anahtar: ['cikis', 'oturumu kapat', 'hesaptan cik', 'logout'],
          cevap: 'Sağ üstte profiline (adına) bas ve açılan menüden "Çıkış yap"ı seç.' },
        { konu: 'hesap', soru: 'Hesabım banlandı', anahtar: ['banland', 'ban', 'hesabim banland', 'hesabim kapali', 'hesap kapatildi', 'engellendi', 'yasaklandi'],
          cevap: 'Hesabın bir yönetici tarafından kapatılmış olabilir. Giriş yaparken görünen mesajda nedeni ve varsa bitiş zamanı yazar. İtiraz için site sahibiyle iletişime geç.',
          baglanti: ILETISIM },

        // --- Okuma ---
        { konu: 'okuma', soru: 'Bir bölümü nasıl okurum?', anahtar: ['bolum oku', 'nasil okunur', 'okumaya basla', 'ilk bolum', 'manga oku', 'okuyamiyorum'],
          cevap: 'Serinin sayfasını aç; "İlk bölümü oku"ya ya da bölüm listesinden istediğin bölüme bas. Okuma ekranı açılır.' },
        { konu: 'okuma', soru: 'Sayfayı nasıl çeviririm?', anahtar: ['sayfa cevir', 'sayfayi cevir', 'sonraki sayfa', 'onceki sayfa', 'sayfa gec', 'ileri', 'geri'],
          cevap: 'Yatay modda görselin sağ yarısına basınca sonraki, sol yarısına basınca önceki sayfaya geçersin. Alttaki "Önceki/Sonraki" düğmeleri ve klavyede ← → (ya da A/D) tuşları da çalışır.' },
        { konu: 'okuma', soru: 'Webtoon (dikey) modu nasıl çalışır?', anahtar: ['webtoon', 'dikey', 'asagi kaydir', 'kaydirarak oku', 'uzun serit'],
          cevap: 'Webtoon modunda sayfalar alt alta dizilir, aşağı kaydırarak okursun. Bir sayfa görseline tek dokunursan içerik ekranın yaklaşık yarısı kadar aşağı kayar. Modu "Okuma Ayarları > Okuma yönü"nden "Dikey (webtoon)" seçerek açarsın.' },
        { konu: 'okuma', soru: 'Çift sayfa modu nedir?', anahtar: ['cift sayfa', 'iki sayfa', 'yan yana'],
          cevap: '"Yatay (çift sayfa)" modunda iki sayfa yan yana gösterilir ve her geçişte iki sayfa ilerlersin; geniş ekranlarda basılı cilt gibi okumak için uygundur. "Okuma Ayarları > Okuma yönü"nden seçilir.' },
        { konu: 'okuma', soru: 'Kaldığım yerden devam edebilir miyim?', niyet: 'devam', anahtar: ['devam', 'kaldigim', 'hatirla', 'ilerleme', 'nerede kaldim', 'en son'],
          cevap: 'Evet. Okuduğun sayfa tarayıcında hatırlanır; giriş yaptıysan ilerlemen hesabına da kaydedilir. Ana sayfada "Okumaya devam et" şeridi, serinin sayfasında "Bölüm X\'ten devam et" düğmesi çıkar; okuduğun bölümler "Okundu" diye işaretlenir.' },
        { konu: 'okuma', soru: 'Sonraki bölüme nasıl geçerim?', anahtar: ['sonraki bolum', 'onceki bolum', 'bolum gec', 'bolum degistir', 'diger bolum', 'bolum sec'],
          cevap: 'Bölümün sonundaki "Sonraki bölüm" düğmesine bas. Üst çubuktaki oklarla önceki/sonraki bölüme geçebilir ya da bölüm seçici listesinden istediğin bölümü açabilirsin.' },
        { konu: 'okuma', soru: 'Tam ekran nasıl yapılır?', anahtar: ['tam ekran', 'tamekran', 'fullscreen', 'buyuk ekran'],
          cevap: 'Okuma ekranının üst çubuğundaki "Tam ekran" düğmesine bas; çıkmak için tekrar bas ya da Esc tuşunu kullan. Bazı tarayıcılar (özellikle iPhone Safari) tam ekranı desteklemez. Telefonda sayfaya dokununca üst çubuk gizlenip görünür.' },
        { konu: 'okuma', soru: 'Klavye kısayolları neler?', anahtar: ['klavye', 'kisayol', 'tus', 'ok tusu'],
          cevap: 'Okuma ekranında: ← veya A önceki sayfa, → veya D sonraki sayfa (yatay modlar), O otomatik kaydırmayı başlatır/durdurur (webtoon), ? kısayol listesini açar, Esc pencereyi ya da otomatik kaydırmayı kapatır. Listeyi bölümün altındaki "Klavye kısayolları" düğmesi de açar.' },
        { konu: 'okuma', soru: 'Bir bölümü ya da seriyi nasıl paylaşırım?', anahtar: ['paylas', 'link', 'baglanti kopyala', 'adres kopyala', 'linki kopyala'],
          cevap: 'Serinin sayfasında ve okuma ekranında "Bağlantıyı kopyala" düğmesi var; bastığında adres panoya kopyalanır, istediğin yere yapıştırabilirsin.' },

        // --- Okuma ayarları ---
        { konu: 'ayar', soru: 'Okuma Ayarları penceresi nerede?', anahtar: ['okuma ayar', 'ayarlar', 'ayar', 'okuyucu ayar', 'secenekler'],
          cevap: 'Okuma ekranının üst çubuğundaki "Okuma Ayarları" düğmesine bas. Pencerede okuma yönü, tema, sayfa geçiş efekti, sayfa genişliği, parlaklık ve (webtoon modunda) otomatik kaydırma var. Ayarlar tarayıcında saklanır.' },
        { konu: 'ayar', soru: 'Okuma yönünü nasıl değiştiririm?', anahtar: ['okuma yonu', 'yon', 'yatay', 'mod degistir', 'okuma modu', 'mod', 'klasik'],
          cevap: '"Okuma Ayarları > Okuma yönü"nden üç seçenekten birini seç: Yatay (tek sayfa), Yatay (çift sayfa) ya da Dikey (webtoon).' },
        { konu: 'ayar', soru: 'Sayfa geçiş efektini nasıl değiştiririm?', anahtar: ['gecis efekt', 'sayfa gecis', 'efekt', 'animasyon', 'solma', 'kayma'],
          cevap: '"Okuma Ayarları > Sayfa geçiş efekti"nden Yok, Solma ya da Kayma seç. Bu ayar yalnızca yatay (tek/çift sayfa) modlarda görünür.' },
        { konu: 'ayar', soru: 'Sayfa genişliğini nasıl ayarlarım?', anahtar: ['genislik', 'sayfa boyut', 'buyut', 'kucult', 'dar', 'genis', 'zoom'],
          cevap: '"Okuma Ayarları > Sayfa genişliği"nden Dar, Normal, Geniş ya da Tam seç. Görseller ekrana göre bu genişlikte gösterilir.' },
        { konu: 'ayar', soru: 'Parlaklığı nasıl düşürürüm?', anahtar: ['parlaklik', 'karart', 'goz yor', 'gece okuma', 'isik'],
          cevap: '"Okuma Ayarları > Parlaklık" kaydırıcısıyla sayfaları %40 ile %100 arasında karartabilirsin; gece okurken göz yormaz.' },
        { konu: 'ayar', soru: 'Otomatik kaydırma nasıl açılır?', anahtar: ['otomatik kaydir', 'oto kaydir', 'kendi kendine', 'otomatik', 'kaydirma hizi'],
          cevap: 'Yalnızca webtoon modunda: "Okuma Ayarları > Otomatik kaydırma"dan "Başlat"a bas, hızı 1x-4x arasında seç. Klavyede O tuşu başlatır/durdurur, Esc durdurur.' },
        { konu: 'ayar', soru: 'Ayarları varsayılana nasıl döndürürüm?', anahtar: ['varsayilan', 'sifirla', 'eski haline', 'ayarlari geri', 'reset'],
          cevap: '"Okuma Ayarları" penceresinin altındaki "Varsayılanlara dön" düğmesine bas: genişlik Normal, parlaklık %100, geçiş Solma ve kaydırma hızı 2x olur. Okuma yönü ve tema değişmez.' },

        // --- Favoriler ve kütüphane ---
        { konu: 'favori', soru: 'Favorilere nasıl eklerim?', anahtar: ['favori', 'favoriye ekle', 'favorilere ekle', 'kutuphaneye ekle', 'kaydet', 'listeme ekle', 'takip et'],
          cevap: 'Giriş yaptıktan sonra serinin sayfasındaki "Favorilere ekle" düğmesine bas; düğme "Favorilerde" olur. Seri kütüphanene eklenir, ilk eklemede +3 puan kazanırsın.' },
        { konu: 'favori', soru: 'Favorilerden nasıl çıkarırım?', anahtar: ['favoriden cikar', 'favorilerden cikar', 'favori cikar', 'favori kaldir', 'favori sil', 'kutuphaneden cikar', 'kaldir'],
          cevap: 'Serinin sayfasındaki "Favorilerde" düğmesine tekrar bas; seri kütüphanenden çıkarılır.' },
        { konu: 'favori', soru: 'Kütüphanem nerede?', anahtar: ['kutuphane', 'kutuphanem', 'favorilerim', 'favori nerde', 'favori nerede', 'favoriler nerede', 'eklediklerim'],
          cevap: 'Üst menüdeki ya da telefonda alt menüdeki "Kütüphane"ye bas; profil menüsünde "Kütüphanem" olarak da var. Favorilere eklediğin bütün seriler orada listelenir.',
          baglanti: { ad: 'Kütüphaneye git', url: 'index.html#/kutuphane' } },
        { konu: 'favori', soru: 'Okuma durumu (Okuyorum / Okunacak / Bitirdim) nedir?', anahtar: ['okuma durumu', 'okuyorum', 'okunacak', 'bitirdim', 'durum sec'],
          cevap: 'Favorilere eklediğin bir serinin sayfasında "Okuma durumu" seçicisi çıkar: Okuyorum, Okunacak ya da Bitirdim. Kütüphanende bu durumlara göre sekmeler var. Durum bu tarayıcıda saklanır.' },
        { konu: 'favori', soru: 'Yeni bölüm çıkınca haber alabilir miyim?', anahtar: ['haber al', 'yeni bolum bildirim', 'bolum gelince', 'takip'],
          cevap: 'Evet. Bir seriyi favorilerine eklersen o seriye yeni bölüm yayınlandığında üst çubuktaki zile bildirim gelir.' },

        // --- Arama ve keşfet ---
        { konu: 'kesfet', soru: 'Nasıl arama yaparım?', anahtar: ['ara', 'arama', 'bul', 'seri bul', 'manga bul', 'isim'],
          cevap: 'Ana sayfadaki "Manga ara…" kutusuna serinin adını yaz; yazarken öneriler çıkar. Telefonda üstteki büyüteç simgesine bas. Sonuç yoksa "Açıklamalarda da ara" ile aramayı genişletebilirsin.' },
        { konu: 'kesfet', soru: 'Türe ya da duruma göre nasıl süzerim?', anahtar: ['tur', 'kategori', 'filtre', 'suz', 'tamamlanan', 'devam eden', 'siralama', 'sirala'],
          cevap: 'Ana sayfada tür sekmelerine basarak birden fazla tür seçebilirsin. Durum listesinden Devam ediyor / Tamamlandı, sıralamadan En yeni / En yüksek puan / A-Z seç. "Filtreleri temizle" hepsini sıfırlar.' },
        { konu: 'kesfet', soru: 'Ne okuyacağımı bilmiyorum', anahtar: ['rastgele', 'ne okusam', 'oneri', 'tavsiye', 'bilmiyorum', 'sasirt'],
          cevap: 'Ana sayfadaki "Rastgele seri" düğmesine bas; sana rastgele bir seri açılır. Sıralamayı "En yüksek puan" yaparak en beğenilenlere de bakabilirsin.' },
        { konu: 'kesfet', soru: 'Yeni bölümler nerede?', anahtar: ['yeni bolum', 'guncel', 'ne zaman', 'yeni seri', 'yeni', 'son bolum'],
          cevap: 'Her serinin sayfasında bölüm listesi var; en yeni bölümü görmek için sıralamayı "Yeniden eskiye" yap. Yeni eklenen seriler ana sayfada "Yeni" etiketiyle görünür. Favorilerindeki serilerin yeni bölümleri bildirim olarak da gelir.' },

        // --- Puan, seviye ve liderlik ---
        { konu: 'puan', soru: 'Puan nasıl kazanılır?', anahtar: ['puan kazan', 'puan nasil', 'puan topla', 'xp kazan', 'xp'],
          cevap: 'Giriş yapınca eylemlerin puan kazandırır: bir bölümü sonuna kadar okumak +10 (günde 10 bölüme kadar), en az 20 karakterlik yorum +5 (günde 5), seri ya da bölüme puan vermek +2, kütüphaneye seri eklemek +3, günlük giriş +2 (7 gün üst üste girersen +10), profil resmi eklemek +10. Günlük toplam üst sınır 250 puan. Puanın Hesabım sayfasındaki "Puanım" bölümünde görünür.',
          baglanti: HESABIM },
        { konu: 'puan', soru: 'Seviye ve rütbe nedir?', anahtar: ['seviye', 'level', 'rutbe', 'lv'],
          cevap: 'Toplam puanın seviyeni belirler: Seviye 2 için 100, Seviye 3 için 300, Seviye 5 için 1000, Seviye 10 için 4500 puan gerekir. Rütbeler: Çırak (1-2), Genç Gölge (3-5), Gölge (6-11), Usta (12-24), Efsane (25+). İlerlemen Hesabım > "Seviye ve ödüller"de görünür.',
          baglanti: HESABIM },
        { konu: 'puan', soru: 'Seviye ödülleri neler?', anahtar: ['odul', 'seviye odul', 'ad rengi', 'unvan', 'cerceve', 'parlak ad', 'sticker paketi', 'yazi tipi'],
          cevap: 'Seviye 3 ad rengi, 5 sohbet yazı tipi, 8 "Sade" sticker paketi, 10 parlak ad, 15 "Gölge" sticker paketi, 20 animasyonlu parlama ve çerçeve, 30 özel unvan açar. Açtıklarını Hesabım > "Seviye ve ödüller"den seçip önizleyebilirsin.',
          baglanti: HESABIM },
        { konu: 'puan', soru: 'Liderlik tablosu nedir?', anahtar: ['liderlik', 'siralama tablo', 'lider tablo', 'en iyi okur', 'kim birinci', 'birinci'],
          cevap: 'Liderlik tablosu, siteyi en çok kullanan okurları puanlarına göre sıralar. Bu hafta, bu ay ve tüm zamanlar olmak üzere üç dönemi görebilirsin. Sıralamaya girmek için giriş yapıp bölüm okuman, yorum yazman ya da seri puanlaman yeterli.',
          baglanti: { ad: 'Liderlik tablosuna git', url: 'index.html#/liderlik' } },
        { konu: 'puan', soru: 'Seriyi ya da bölümü nasıl puanlarım?', anahtar: ['puanla', 'yildiz', 'oy ver', 'oyla', 'degerlendir', 'puan ver'],
          cevap: 'Giriş yaptıktan sonra serinin sayfasında ("Bu seriyi puanla") ya da bölümün yorumlar kısmında ("Bu bölümü puanla") 1-5 arası yıldıza bas. Puanını istediğin zaman değiştirebilir ya da "Kaldır" ile geri alabilirsin.' },

        // --- Yorumlar ve sohbet ---
        { konu: 'yorum', soru: 'Yorum nasıl yaparım?', anahtar: ['yorum', 'yorum yap', 'yorum yaz', 'dusunce'],
          cevap: 'Yorumlar hem serinin sayfasında hem her bölümün altında. Yazmak için giriş yapman gerekir; serinin sayfasında en fazla 500, bölüm altında en fazla 1000 karakter yazabilirsin. Yorumları "En yeni" ya da beğeniye göre sıralayabilirsin.' },
        { konu: 'yorum', soru: 'Yorumumu nasıl düzenler ya da silerim?', anahtar: ['yorum duzenle', 'yorumu duzenle', 'yorum sil', 'yorumumu sil', 'duzenle'],
          cevap: 'Kendi yorumunun altında "Düzenle" ve "Sil" düğmeleri çıkar. Düzenlenen yorumda "(düzenlendi)" yazar; silmeden önce onay sorulur.' },
        { konu: 'yorum', soru: 'Yanıtlama, beğenme ve şikayet nasıl olur?', anahtar: ['yanitla', 'yanit', 'cevap yaz', 'begen', 'sikayet', 'bildir', 'rapor'],
          cevap: 'Giriş yaptıysan bir yorumun altında "Yanıtla", "Beğen" ve (başkasının yorumunda) "Şikayet et" düğmeleri var. Şikayetini kısa bir nedenle gönderirsin, yöneticiler inceler. Biri yorumuna yanıt verirse bildirim alırsın.' },
        { konu: 'yorum', soru: 'Spoiler nasıl gizlenir? Yazı biçimleri neler?', anahtar: ['spoiler', 'gizli', 'kalin', 'egik', 'renkli yazi', 'bicim', 'format'],
          cevap: 'Yorum yazarken "Spoiler içerir" kutusunu işaretlersen yorum bulanık görünür, okuyan basınca açılır. Biçimler: **kalın**, *eğik*, ||gizli metin|| ve [renk=kirmizi]...[/renk] (kirmizi, mavi, turuncu, mor, gri, sari). Araç çubuğundaki düğmeler bunları senin yerine ekler.' },
        { konu: 'yorum', soru: 'Sohbet nasıl kullanılır?', anahtar: ['sohbet', 'chat', 'canli', 'mesaj', 'mesaj yaz', 'konus'],
          cevap: 'Menüdeki "Sohbet"ten herkesle canlı yazışabilirsin. Mesajları giriş yapmadan da okuyabilirsin; yazmak için giriş gerekir. Bir mesaj en fazla 300 karakter olabilir; 2 saniyede bir, dakikada en fazla 15 mesaj gönderebilirsin.',
          baglanti: { ad: 'Sohbete git', url: 'index.html#/sohbet' } },
        { konu: 'yorum', soru: 'Sohbette sticker ve etiketleme nasıl olur?', anahtar: ['sticker', 'cikartma', 'etiketle', 'etiket', 'bahset', 'mention'],
          cevap: 'Sticker paketleri seviyeyle açılır: Seviye 8 "Sade", Seviye 15 "Gölge"; kilitli paketler gri görünür. Birini etiketlemek için @kullaniciAdi yaz; etiketlenen kişiye bildirim gider.' },

        // --- Bildirimler ---
        { konu: 'bildirim', soru: 'Bildirimler nerede?', anahtar: ['bildirim', 'zil', 'uyari geldi', 'haber'],
          cevap: 'Giriş yaptıysan üst çubuktaki zil simgesine bas; okunmamış bildirim sayısı zilin üstünde görünür. Bir bildirime basınca ilgili sayfa açılır.' },
        { konu: 'bildirim', soru: 'Hangi durumlarda bildirim alırım?', anahtar: ['ne zaman bildirim', 'hangi bildirim', 'bildirim gelmiyor', 'bildirim turleri'],
          cevap: 'Favorilerindeki bir seriye yeni bölüm yayınlandığında, biri yorumuna yanıt verdiğinde ve sohbette @ ile etiketlendiğinde bildirim alırsın.' },
        { konu: 'bildirim', soru: 'Bildirimleri nasıl temizlerim?', anahtar: ['bildirim temizle', 'bildirim sil', 'okundu isaretle', 'okundu yap'],
          cevap: 'Zil menüsünün üstündeki "Okundu işaretle" hepsini okundu yapar, "Temizle" listeyi boşaltır.' },

        // --- Tema ve görünüm ---
        { konu: 'gorunum', soru: 'Temayı nasıl değiştiririm?', anahtar: ['tema', 'gorunum', 'renk', 'karanlik', 'aydinlik', 'arka plan', 'koyu', 'acik tema'],
          cevap: 'Sağ üstteki menüden (üç çizgi ya da profilin) "Görünüm ve temalar"ı aç. Temalar: Kage (koyu kırmızı), Sistem (cihaz temasını izler), Kâğıt (açık krem), Mono (siyah beyaz) ve Gece (lacivert). Okuma ekranında "Okuma Ayarları"ndan da değiştirebilirsin.' },
        { konu: 'gorunum', soru: 'Cihazımın karanlık/aydınlık moduna uysun', anahtar: ['sistem tema', 'cihaz tema', 'otomatik tema', 'telefon temasi'],
          cevap: '"Görünüm ve temalar"dan "Sistem" temasını seç; site cihazının karanlık ya da aydınlık ayarını izler.' },
        { konu: 'gorunum', soru: 'Gece okumak için en iyi ayar ne?', anahtar: ['gece', 'goz', 'karanlikta oku', 'yatarken'],
          cevap: 'Gece ya da Kage temasını seç, okurken "Okuma Ayarları > Parlaklık"ı düşür. Mono teması yüksek kontrast ister misin diye de denenebilir.' },

        // --- Mobil ve uygulama ---
        { konu: 'mobil', soru: 'Siteyi uygulama olarak nasıl yüklerim?', anahtar: ['uygulama', 'yukle', 'indir', 'ana ekran', 'pwa', 'kisayol olustur', 'apk', 'app'],
          cevap: 'Kage 95 ayrı bir mağaza uygulaması değil ama telefonuna uygulama gibi eklenebilir. Android (Chrome): sağ üstteki ⋮ menüden "Uygulamayı yükle" ya da "Ana ekrana ekle". iPhone (Safari): Paylaş düğmesi > "Ana Ekrana Ekle". Bilgisayarda (Chrome/Edge): adres çubuğundaki yükle simgesi.' },
        { konu: 'mobil', soru: 'İnternetsiz çalışır mı?', anahtar: ['internetsiz', 'cevrimdisi', 'offline', 'internet olmadan', 'indirip oku'],
          cevap: 'Hayır, seriler ve bölümler internet gerektirir. Sitenin kabuğu önbellekte tutulduğu için tekrar açılışlar daha hızlıdır ama bölüm indirip çevrimdışı okuma yok.' },
        { konu: 'mobil', soru: 'Telefonda menüler nerede?', anahtar: ['telefon', 'mobil', 'alt menu', 'menu nerede', 'menu'],
          cevap: 'Telefonda ekranın altında gezinme çubuğu var: Kütüphane, Liderlik, Sohbet ve profil buradan açılır. Diğer seçenekler sağ üstteki üç çizgi menüsünde; arama için büyüteç simgesine bas.' },

        // --- Sorun giderme ---
        { konu: 'sorun', soru: 'Sayfa açılmıyor / hata var', anahtar: ['hata', 'acilmiyor', 'yuklenmiyor', 'bozuk', 'calismiyor', 'sorun', 'donuyor', 'beyaz ekran'],
          cevap: 'Sayfayı yenilemeyi dene. "Sunucu çalışmıyor olabilir" yazıyorsa biraz bekleyip tekrar dene. Sorun sürerse ne yaptığını ve hangi sayfada olduğunu yazarak site sahibine haber ver.',
          baglanti: ILETISIM },
        { konu: 'sorun', soru: 'Görseller yüklenmiyor', anahtar: ['gorsel', 'resim yuklenmiyor', 'sayfa gelmiyor', 'resim acilmiyor', 'bos sayfa'],
          cevap: 'İnternet bağlantını kontrol edip sayfayı yenile. Bir sayfa açılamazsa yerine yer tutucu görsel gösterilir; sürekli oluyorsa hangi seri ve bölüm olduğunu site sahibine bildir.',
          baglanti: ILETISIM },
        { konu: 'sorun', soru: 'Puanım gelmedi', anahtar: ['puan gelmedi', 'puan eklenmedi', 'puan artmiyor', 'puan yok'],
          cevap: 'Puan yalnızca giriş yapmışken verilir. Bölüm puanı için bölümü son sayfasına kadar okumalısın; yorum puanı için yorum en az 20 karakter olmalı. Günlük sınırlara (10 bölüm, 5 yorum, toplam 250 puan) ulaştıysan ertesi gün devam eder.' },
        { konu: 'sorun', soru: 'Oturumum kendiliğinden kapandı', anahtar: ['oturum kapandi', 'atildim', 'cikis yapti', 'tekrar giris', 'oturum dustu'],
          cevap: 'Başka bir cihazdan şifre değiştirildiyse ya da oturum süresi dolduysa yeniden giriş yapman gerekir. Hesabın kapatıldıysa giriş ekranında nedeni yazar.',
          baglanti: { ad: 'Giriş sayfası', url: 'login.html' } },
        { konu: 'sorun', soru: 'Site güncellenmemiş gibi görünüyor', anahtar: ['guncellenmedi', 'eski surum', 'degisiklik gorunmuyor', 'onbellek', 'cache'],
          cevap: 'Sayfayı yenile. Siteyi uygulama olarak yüklediysen uygulamayı tamamen kapatıp yeniden aç; yeni sürüm bir sonraki açılışta yüklenir.' },

        // --- İletişim ---
        { konu: 'iletisim', soru: 'Site sahibine nasıl ulaşırım?', anahtar: ['iletisim', 'ulas', 'site sahibi', 'yonetici', 'admin', 'instagram', 'e posta', 'mail', 'destek'],
          cevap: 'İletişim sayfasında site sahibinin (Jagwey) Instagram ve e-posta adresi var. Menüdeki "İletişim" bağlantısından da açılır.',
          baglanti: ILETISIM },
        { konu: 'iletisim', soru: 'Hata bildirmek ya da öneri vermek istiyorum', anahtar: ['oneri ver', 'hata bildir', 'istek', 'seri istegi', 'geri bildirim', 'fikir'],
          cevap: 'Memnuniyetle! İletişim sayfasındaki e-posta ya da Instagram adresinden yaz. Hata için hangi sayfada ne olduğunu, seri isteği için serinin adını belirt.',
          baglanti: ILETISIM },
        { konu: 'iletisim', soru: 'Bu yardım kutusu kim?', anahtar: ['sen kimsin', 'bot', 'yapay zeka', 'gercek kisi', 'robot', 'ai', 'yardim kutusu', 'asistan', 'kage asistan'],
          cevap: 'Ben Kage Asistan: kural tabanlı bir yardımcıyım; gerçek bir kişi ya da yapay zeka değilim. Sorunu yazınca en uygun hazır cevabı bulurum; giriş yaptıysan seviyeni, favorilerini ve okuma geçmişini de söyleyebilirim. Cevabım yetmezse İletişim sayfasından site sahibine ulaşabilirsin.',
          baglanti: ILETISIM },

        // --- Yönetim paneli (yalnızca yöneticiler) ---
        // y_ ile başlayan niyetler canlı yönetim verisinden cevaplanır; yetki yoksa ya da veri gelmezse "cevap" gösterilir.
        // Asıl yetki kontrolü sunucudadır; buradaki kontrol yalnızca gereksiz istek ve yanıltıcı düğme olmasın diyedir.
        { konu: 'yonetim', soru: 'Asistanla neler yapabilirim?', anahtar: ['komutlar', 'neler yapabilirsin', 'ne yapabilirim', 'yonetici komut', 'admin komut', 'yardim komut'],
          cevap: 'Yönetici olarak bana şunları yazabilirsin: "özet", "istatistik", "kullanıcı ara ahmet", "banla ahmet 7 gün spam", "ban kaldır ahmet", "oturumlarını kapat ahmet", "son yorumlar", "son kayıtlar", "ayarlar", "bakımı aç/kapat", "kayıtları aç/kapat", "yorumları aç/kapat", "sohbeti aç/kapat", "duyuruyu aç/kapat", "duyuru yaz <metin>". Değişiklik yapan her komut önce onay ister.',
          baglanti: { ad: 'Yönetim paneli', url: 'admin.html' } },
        { konu: 'yonetim', soru: 'Site özeti', niyet: 'y_ozet', takip: 'Son 7 günde neler oldu?',
          anahtar: ['ozet', 'site ozeti', 'site durumu', 'kac manga', 'kac seri var', 'kac kullanici', 'kac uye', 'toplam seri', 'toplam bolum', 'genel durum'],
          cevap: 'Özet şu an alınamadı. Yönetim panelindeki "Özet" bölümüne bakabilirsin.',
          baglanti: { ad: 'Özet', url: 'admin.html#ozet' } },
        { konu: 'yonetim', soru: 'Son 7 günde neler oldu?', niyet: 'y_istatistik', takip: 'Son yorumlar',
          anahtar: ['istatistik', 'son 7 gun', 'son bir hafta', 'bu hafta', 'haftalik', 'yeni uye', 'kac kisi kayit', 'son gunler', 'aktivite'],
          cevap: 'İstatistik şu an alınamadı. Yönetim panelinin "Özet" bölümünde son 14 günün grafiği var.',
          baglanti: { ad: 'Özet', url: 'admin.html#ozet' } },
        { konu: 'yonetim', soru: 'Son yorumlar', niyet: 'y_yorumlar',
          anahtar: ['son yorumlar', 'yorumlari goster', 'yeni yorumlar', 'yorum listesi', 'yorumlara bak', 'yorum denetle'],
          cevap: 'Yorumları görmek için "yorum.sil" yetkisi gerekiyor ya da liste şu an alınamadı.',
          baglanti: { ad: 'Yorumlar', url: 'admin.html#yorumlar' } },
        { konu: 'yonetim', soru: 'Son yönetim kayıtları', niyet: 'y_kayitlar',
          anahtar: ['son kayitlar', 'denetim kaydi', 'kim ne yapti', 'islem gecmisi', 'yonetim kaydi', 'log', 'loglar'],
          cevap: 'Kayıtları görmek için "kayit.gor" yetkisi gerekiyor ya da liste şu an alınamadı.',
          baglanti: { ad: 'Kayıtlar', url: 'admin.html#kayitlar' } },
        { konu: 'yonetim', soru: 'Site ayarları ne durumda?', niyet: 'y_ayarlar',
          anahtar: ['ayarlar', 'site ayarlari', 'ayarlar ne durumda', 'bakim modu acik mi', 'kayit acik mi', 'yorumlar acik mi', 'sohbet acik mi'],
          cevap: 'Ayarları görmek için "ayar.yonet" yetkisi gerekiyor ya da ayarlar şu an alınamadı.',
          baglanti: { ad: 'Ayarlar', url: 'admin.html#ayarlar' } },
        { konu: 'yonetim', soru: 'Kullanıcı nasıl banlanır?', anahtar: ['banla', 'ban at', 'kullanici banla', 'engelle', 'yasakla', 'ban kaldir', 'uzaklastir'],
          cevap: 'Bana "banla KULLANICIADI 7 gün NEDEN" yaz (süre isteğe bağlı, yazmazsan süresiz). Onaylayınca ban uygulanır ve kişinin oturumları kapanır. Kaldırmak için "ban kaldır KULLANICIADI". Panelde: Kullanıcılar, kişiyi seç, "Banla". Bunun için "kullanici.engelle" yetkisi gerekir.',
          baglanti: { ad: 'Kullanıcılar', url: 'admin.html#kullanicilar' } },
        { konu: 'yonetim', soru: 'Yeni bölüm nasıl eklenir?', anahtar: ['bolum ekle', 'bolum yukle', 'yeni bolum', 'sayfa yukle', 'sayfa ekle', 'chapter ekle', 'resim yukle'],
          cevap: 'Seriler, seriyi seç, Bölümler, "Yeni bölüm". Bölüm numarasını (12 ya da 12.5) ve istersen adını yaz. Sayfaları "Dosyadan yükle" sekmesinde sürükleyip bırak (dosya adına göre sıralanır, Yukarı/Aşağı ile düzeltilir) ya da "URL ile ekle" sekmesine her satıra bir adres yaz. Aynı numaralı bölüm varsa sayfalar o bölümün sonuna eklenir.',
          baglanti: { ad: 'Seriler', url: 'admin.html#seriler' } },
        { konu: 'yonetim', soru: 'Yeni seri nasıl eklenir?', anahtar: ['seri ekle', 'manga ekle', 'yeni seri', 'yeni manga', 'seri olustur', 'kapak yukle'],
          cevap: 'Seriler bölümünde "Yeni seri" düğmesine bas. Seri adı zorunlu; yazar, çizer, durum, açıklama ve kategoriler isteğe bağlı. "Yayında" kapalıysa seri taslak kalır, ziyaretçiler görmez. "Öne çıkan" işaretlersen ana sayfada öne çıkar.',
          baglanti: { ad: 'Seriler', url: 'admin.html#seriler' } },
        { konu: 'yonetim', soru: 'Bakım modu ve site ayarları', anahtar: ['bakim modu', 'bakima al', 'siteyi kapat', 'kayitlari kapat', 'yorumlari kapat', 'sohbeti kapat', 'bakim mesaji'],
          cevap: 'Bana "bakımı aç", "kayıtları kapat", "yorumları kapat", "sohbeti kapat" gibi yazabilirsin; onaylayınca uygulanır. Panelde Ayarlar bölümünde: yeni kayıtlara izin ver, yorumlara izin ver, sohbete izin ver, bakım modu (ve bakım mesajı), duyuru (ve duyuru metni). "ayar.yonet" yetkisi gerekir.',
          baglanti: { ad: 'Ayarlar', url: 'admin.html#ayarlar' } },
        { konu: 'yonetim', soru: 'Duyuru ve öne çıkanlar nerede?', anahtar: ['duyuru', 'duyuru ekle', 'kayan yazi', 'one cikan', 'slider', 'vitrin', 'ana sayfa kapak'],
          cevap: 'Duyurular: ana sayfadaki kayan duyuru şeridi. Öne çıkanlar: ana sayfanın üstündeki büyük kapak kaydırıcısı. İkisi de panelde ayrı bölümlerdir ve "duyuru.yonet" yetkisi ister. Bana "duyuru yaz <metin>" dersen ayarlardaki duyuru metnini değiştirip açarım.',
          baglanti: { ad: 'Duyurular', url: 'admin.html#duyurular' } },
        { konu: 'yonetim', soru: 'Yorum ve şikayetler nasıl yönetilir?', anahtar: ['yorum sil', 'sikayet', 'sikayetler', 'raporlanan', 'kotu yorum', 'kufur'],
          cevap: 'Bana "son yorumlar" yaz; listede her yorumun altında "Sil" düğmesi çıkar, onaylayınca silinir. Panelde Yorumlar ve Şikayetler bölümleri var ("yorum.sil" yetkisi). Sohbet mesajları için Sohbet bölümü ("sohbet.yonet").',
          baglanti: { ad: 'Yorumlar', url: 'admin.html#yorumlar' } },
        { konu: 'yonetim', soru: 'Yönetici ve yetki nasıl verilir?', anahtar: ['yonetici ekle', 'admin ekle', 'yetki ver', 'yetkiler', 'moderator', 'yetki kaldir'],
          cevap: 'Yalnızca site sahibi (Owner) yapabilir: panelde Yöneticiler bölümünde kişiyi seçip yetkilerini tek tek işaretlersin (seri, bölüm, kategori, yorum, sohbet, kullanıcı, duyuru, kayıt, ayar). Her yönetim işlemi Kayıtlar bölümünde tutulur.',
          baglanti: { ad: 'Yöneticiler', url: 'admin.html#yoneticiler' } },
        { konu: 'yonetim', soru: 'Kullanıcı silme ve şifre sıfırlama', anahtar: ['kullanici sil', 'hesap sil', 'sifre sifirla', 'sifresini degistir', 'puanlarini sil', 'rol degistir'],
          cevap: 'Geri alınamayan bu işlemleri güvenlik için asistandan yapmıyorum. Panelde Kullanıcılar bölümünde kişiyi seç: şifre belirleme, puanları silme ve hesabı silme orada.',
          baglanti: { ad: 'Kullanıcılar', url: 'admin.html#kullanicilar' } }
    ];

    var KARSILAMA = 'Merhaba! Bir konu seç ya da sorunu kendi cümlelerinle yaz.';
    var BULUNAMADI = 'Bunu tam anlayamadım, kusura bakma. Aşağıdaki konulardan biri işine yarayabilir ya da sorunu başka kelimelerle yazabilirsin. Yine bulamazsan İletişim sayfasından site sahibine yazabilirsin.';
    var GIRIS_NOTU = 'Giriş yaparsan seviyeni, favorilerini ve okuma geçmişini de sorabilirsin.';
    // "peki o nasıl", "ya o?", "başka" gibi önceki konuya bağlı devam soruları (normalize edilmiş metinde).
    var TAKIP = /^(peki|ya|ee+|e peki|o zaman|baska|daha fazla|devami|sonra|ayrica)( |$)|(^| )(o nasil|nasil yani|peki o|ya o|onu nasil|bunu nasil|baska ne)( |$)/;
    var ONERI_METNI = 'Bunu mu demek istediniz?';

    // Eşleştirme eşikleri: GUCLU ve üstü doğrudan cevaplanır; ZAYIF-GUCLU arası ya da başa baş sonuçlar öneri olarak sunulur.
    var ESIK_GUCLU = 4;
    var ESIK_ZAYIF = 2;

    // Anlam taşımayan sık kelimeler (normalize edilmiş hâlleriyle); kelime kelime eşleştirmede yok sayılır.
    var DURAK = ['nasil', 'nedir', 'neden', 'niye', 'nerede', 'nerde', 'nereden', 'nereye', 'ne', 'mi', 'mu', 'mudur', 'midir',
        'var', 'bir', 'ben', 'benim', 'bana', 'beni', 'sen', 'siz', 'icin', 'ile', 've', 'veya', 'ya', 'da', 'de', 'ki', 'bu', 'su',
        'o', 'acaba', 'lutfen', 'istiyorum', 'olur', 'olurum', 'yapabilirim', 'yaparim', 'yapilir', 'edilir', 'nasl', 'merhaba',
        'selam', 'abi', 'hocam', 'yapmak', 'etmek', 'olarak', 'gibi', 'en', 'cok', 'sey', 'diye'];

    function norm(t) {
        return String(t).toLocaleLowerCase('tr-TR')
            .replace(/[çğıöşüâîû]/g, function (c) { return { 'ç': 'c', 'ğ': 'g', 'ı': 'i', 'ö': 'o', 'ş': 's', 'ü': 'u', 'â': 'a', 'î': 'i', 'û': 'u' }[c]; })
            .replace(/[^a-z0-9\s]/g, ' ')
            .replace(/\s+/g, ' ').trim();
    }

    function kelimeler(n) {
        return n ? n.split(' ').filter(function (w) { return w.length >= 2 && DURAK.indexOf(w) === -1; }) : [];
    }

    // İki kelime arasındaki düzenleme uzaklığı (Levenshtein); yazım hatası toleransı için.
    function mesafe(a, b) {
        if (a === b) return 0;
        if (!a.length) return b.length;
        if (!b.length) return a.length;
        var onceki = [], i, j;
        for (j = 0; j <= b.length; j++) onceki[j] = j;
        for (i = 1; i <= a.length; i++) {
            var simdiki = [i];
            for (j = 1; j <= b.length; j++) {
                var bedel = a.charAt(i - 1) === b.charAt(j - 1) ? 0 : 1;
                simdiki[j] = Math.min(onceki[j] + 1, simdiki[j - 1] + 1, onceki[j - 1] + bedel);
            }
            onceki = simdiki;
        }
        return onceki[b.length];
    }

    // Bir anahtar kelimenin kullanıcı kelimelerinden biriyle ne kadar uyduğu (0-1).
    function kelimeUyumu(kw, kelimeListesi) {
        var en = 0;
        for (var i = 0; i < kelimeListesi.length && en < 1; i++) {
            var t = kelimeListesi[i];
            var u = 0;
            if (t === kw) u = 1;
            else if (kw.length >= 3 && t.indexOf(kw) === 0) u = 1;           // ek almış: "sifremi" -> "sifre"
            else if (t.length >= 4 && kw.indexOf(t) === 0) u = 0.6;          // yarım yazılmış: "kutup" -> "kutuphane"
            else if (kw.length >= 5) {
                var d = Math.min(mesafe(t, kw), mesafe(t.slice(0, kw.length), kw));
                if (d <= 1) u = 0.75;                                         // tek harf hatası: "webton", "sifrmi"
                else if (kw.length >= 8 && d <= 2) u = 0.6;
            }
            if (u > en) en = u;
        }
        return en;
    }

    function ifadePuani(nk, n, tumKelime) {
        if (!nk) return 0;
        var uzunluk = nk.replace(/ /g, '').length;
        // Tam ifade bir kelime başında geçiyorsa tam puan ("ara" -> "arama" olur, "kara" olmaz);
        // kelimenin tamamıysa ("bildirim" içinde "bildir" değil, "bildirim") biraz daha fazla.
        var yer = (' ' + n + ' ').indexOf(' ' + nk);
        if (yer !== -1) return (' ' + n + ' ').charAt(yer + nk.length + 1) === ' ' ? uzunluk * 1.2 : uzunluk;
        // İfadenin bütün kelimeleri (durak kelimeler dahil: "favori nerde"de "nerde" de) aranır.
        var hepsi = nk.split(' ').filter(function (w) { return w.length >= 2; });
        var kw = kelimeler(nk);
        if (!kw.length) return 0;
        var kalite = 1;
        for (var i = 0; i < hepsi.length; i++) {
            kalite = Math.min(kalite, kelimeUyumu(hepsi[i], tumKelime));
            if (kalite < 0.6) break;
        }
        // Bütün kelimeler bulunamadıysa ve ifadenin ilk (asıl) kelimesi geçiyorsa öneri için küçük bir puan.
        if (kalite < 0.6) return kelimeUyumu(kw[0], tumKelime) >= 0.75 ? 0.3 * kw[0].length : 0;
        // Puan yalnızca anlamlı kelimelerin uzunluğundan gelir; durak kelimeler şişirmesin.
        return kalite * kw.join('').length * 0.9;
    }

    // Anahtar ve soru metinlerini bir kez normalize edip saklar.
    SORULAR.forEach(function (s) {
        s._anahtar = s.anahtar.map(norm);
        s._soruKelime = kelimeler(norm(s.soru)).filter(function (w) { return w.length >= 4; });
    });

    function puanla(s, n, kelimeListesi) {
        // Aynı kelimeyi yakalayan birden çok anahtar puanı şişirmesin: en iyisi tam, sonrakiler azalarak eklenir.
        var puanlar = s._anahtar.map(function (nk) { return ifadePuani(nk, n, n.split(' ')); })
            .filter(function (p) { return p > 0; })
            .sort(function (a, b) { return b - a; });
        var puan = 0, carpan = 1;
        puanlar.forEach(function (p) { puan += p * carpan; carpan *= 0.5; });
        // Soru başlığındaki kelimeler küçük bir ek puan verir (anahtar listesinde unutulan kelimeler için).
        s._soruKelime.forEach(function (w) {
            var u = kelimeUyumu(w, kelimeListesi);
            if (u >= 0.75) puan += 0.25 * w.length * u;
        });
        return puan;
    }

    // Yönetim konusu yalnızca yöneticilere açıktır.
    function konuAcik(id, yonetici) {
        var k = konuBul(id);
        return !!k && (!k.yonetici || !!yonetici);
    }

    // Sonuç: { tur: 'cevap' | 'oneri' | 'yok', kayit, oneriler }. yonetici: yönetim sorularını da ara.
    function bul(metin, yonetici) {
        var n = norm(metin);
        var kelimeListesi = kelimeler(n);
        if (!n) return { tur: 'yok', kayit: null, oneriler: [] };
        var sirali = SORULAR.filter(function (s) { return konuAcik(s.konu, yonetici); })
            .map(function (s) { return { s: s, puan: puanla(s, n, kelimeListesi) }; })
            .filter(function (x) { return x.puan > 0; })
            .sort(function (a, b) { return b.puan - a.puan; });
        var bir = sirali[0], iki = sirali[1];
        if (!bir || bir.puan < ESIK_ZAYIF) {
            // Soru bulunamadı ama bir konu adı yazılmışsa ("hesap", "okuma") o konunun soruları gösterilir.
            var konu = konuEslestir(kelimeListesi, yonetici);
            if (konu) return { tur: 'konu', kayit: null, oneriler: [], konu: konu };
            return { tur: 'yok', kayit: null, oneriler: [] };
        }
        var acik = !iki || iki.puan <= bir.puan * 0.7 || bir.puan - iki.puan >= 5;
        if (bir.puan >= ESIK_GUCLU && acik) return { tur: 'cevap', kayit: bir.s, oneriler: [] };
        // Net cevap yoksa ve bir konu adı yazılmışsa ("hesap", "okuma") o konunun soruları daha yararlıdır.
        var konu2 = konuEslestir(kelimeListesi, yonetici);
        if (konu2 && bir.puan < ESIK_GUCLU) return { tur: 'konu', kayit: null, oneriler: [], konu: konu2 };
        var alt = Math.max(ESIK_ZAYIF, bir.puan * 0.4);
        var oneriler = sirali.filter(function (x) { return x.puan >= alt; }).slice(0, 3).map(function (x) { return x.s; });
        return { tur: 'oneri', kayit: null, oneriler: oneriler };
    }

    // Kullanıcı kelimeleri bir konunun adıyla (4+ harfli kelimeleri) uyuşuyor mu? En iyi uyan konu ya da null.
    function konuEslestir(kelimeListesi, yonetici) {
        var enIyi = null, enPuan = 0;
        KONULAR.forEach(function (k) {
            if (k.yonetici && !yonetici) return;
            var puan = 0;
            kelimeler(norm(k.ad)).forEach(function (w) {
                if (w.length < 4) return;
                var u = kelimeUyumu(w, kelimeListesi);
                if (u >= 0.75) puan += u * w.length;
            });
            if (puan > enPuan) { enPuan = puan; enIyi = k; }
        });
        return enIyi;
    }

    function konuBul(id) {
        for (var i = 0; i < KONULAR.length; i++) if (KONULAR[i].id === id) return KONULAR[i];
        return null;
    }

    // Manga arama / tavsiye / açma niyetlerini tespit eder
    function mangaNiyeti(metin, sonucTur) {
        var n = norm(metin);
        var oneriKelimeleri = ['manga oner', 'seri oner', 'tavsiye', 'ne okusam', 'ne okuyayim', 'rastgele', 'guzel manga', 'populer manga', 'bana manga'];
        var oneriMi = oneriKelimeleri.some(function (k) { return n.indexOf(k) !== -1; });

        var kategoriler = ['aksiyon', 'macera', 'romantizm', 'fantastik', 'komedi', 'dram', 'korku', 'gizem', 'seinen', 'shounen', 'webtoon', 'bilim kurgu'];
        var bulunanKategori = null;
        for (var i = 0; i < kategoriler.length; i++) {
            if (n.indexOf(kategoriler[i]) !== -1) {
                bulunanKategori = kategoriler[i];
                break;
            }
        }
        if (bulunanKategori && (oneriMi || n.indexOf('oner') !== -1 || n.indexOf('tavsiye') !== -1 || n.indexOf('var mi') !== -1)) {
            return { tur: 'kategori', kategori: bulunanKategori };
        }
        if (oneriMi) {
            return { tur: 'rastgele' };
        }

        var eylemVar = /\b(oku|ac|git|sayfasi|sayfasina git|var mi|nerde|nerede|serisi|mangasi|bolumu|acabilir misin|goster)\b/.test(n);
        var temiz = n
            .replace(/\b(oku|ac|git|sayfasi|sayfasina git|var mi|nerde|nerede|serisi|mangasi|bolumu|acabilir misin|lutfen|bana|beni|acarmisin|okumak|istiyorum|goster)\b/g, '')
            .replace(/\s+/g, ' ')
            .trim();

        if (temiz.length >= 2 && (eylemVar || sonucTur === 'yok')) {
            return { tur: 'ara', terim: temiz };
        }

        return null;
    }

    // Yönetici komutları (ham metinden; kullanıcı adındaki harfler korunur). Komut değilse null.
    // { tur: 'ban', ad, gun, neden } | { tur: 'banKaldir' | 'oturum' | 'ara', ad } | { tur: 'ayar', alan, deger }
    // | { tur: 'duyuruYaz', metin } | { tur: 'yorumSil', id }
    var AYAR_ALANLARI = { bakim: 'bakimModu', kayit: 'kayitAcik', uyelik: 'kayitAcik', yorum: 'yorumAcik', sohbet: 'sohbetAcik', duyuru: 'duyuruAcik' };

    function yonetimKomutu(metin) {
        var ham = String(metin || '').trim();
        var m = ham.match(/^(?:banla|ban at|engelle|yasakla)\s+@?(\S+)\s*([\s\S]*)$/i);
        if (m) {
            var kalan = m[2].trim(), gun = null;
            var s = kalan.match(/^(\d{1,3})\s*(g[üu]n|g|saat|sa)(?=[\s.]|$)\.?\s*([\s\S]*)$/i);
            if (s) {
                gun = /^s/i.test(s[2]) ? Number(s[1]) / 24 : Number(s[1]);
                kalan = s[3].trim();
            } else if (/^(kal[ıi]c[ıi]|s[üu]resiz)(?=\s|$)/i.test(kalan)) {
                kalan = kalan.replace(/^\S+\s*/, '');
            }
            return { tur: 'ban', ad: m[1], gun: gun > 0 ? gun : null, neden: kalan.replace(/^(neden|sebep)\s*[:\-]?\s*/i, '').slice(0, 500) };
        }
        m = ham.match(/^(?:ban[ıi]?\s+kald[ıi]r|engeli?\s+kald[ıi]r|ban\s+a[çc])\s+@?(\S+)\s*$/i);
        if (m) return { tur: 'banKaldir', ad: m[1] };
        m = ham.match(/^(?:oturumlar[ıi]n[ıi]|oturumlar[ıi]|oturum)\s+kapat\s+@?(\S+)\s*$/i);
        if (m) return { tur: 'oturum', ad: m[1] };
        m = ham.match(/^(?:kullan[ıi]c[ıi]|[üu]ye)\s+(?:ara|bul|g[öo]ster)\s+@?(\S+)\s*$/i);
        if (m) return { tur: 'ara', ad: m[1] };
        m = ham.match(/^duyuru\s+(?:yaz|yap|ekle|ayarla|g[üu]ncelle)\s*[:\-]?\s+([\s\S]{2,})$/i);
        if (m) return { tur: 'duyuruYaz', metin: m[1].trim().slice(0, 500) };
        m = ham.match(/^yorum(?:u)?\s+sil\s+#?(\d+)\s*$/i);
        if (m) return { tur: 'yorumSil', id: Number(m[1]) };
        var n = norm(ham);
        m = n.match(/^(bakim|kayit|uyelik|yorum|sohbet|duyuru)[a-z]*(?: modu[a-z]*)?\s+(ac|kapat)[a-z]*$/);
        if (m) return { tur: 'ayar', alan: AYAR_ALANLARI[m[1]], deger: m[2] === 'ac' };
        return null;
    }

    // Tarayıcı dışında (node testi) yalnızca eşleştirme dışa açılır.
    if (!tarayici) {
        if (typeof module !== 'undefined' && module.exports) {
            module.exports = { bul: bul, norm: norm, mesafe: mesafe, SORULAR: SORULAR, KONULAR: KONULAR, TAKIP: TAKIP, mangaNiyeti: mangaNiyeti, yonetimKomutu: yonetimKomutu };
        }
        return;
    }

    // Düzen: başlık / mesajlar (esner, kendi içinde kayar) / yatay kayan konu-soru şeridi / yazma kutusu.
    // Masaüstünde sağ altta ~380x560 pencere; telefonda görünür alanı (visualViewport) kaplayan tam ekran sayfa.
    var css = [
        '.k95y-btn{position:fixed;right:16px;bottom:16px;z-index:90;display:inline-flex;align-items:center;gap:8px;min-height:44px;padding:0 18px;border-radius:22px;',
        'background:var(--bg-ana,#0e0c0c);color:var(--text-ana,#ede7e2);border:1px solid var(--border-belirgin,#3a3030);font-family:inherit;font-size:.9rem;font-weight:500;line-height:1;cursor:pointer;transition:border-color .15s ease,color .15s ease}',
        '@media (hover:hover){.k95y-btn:hover{border-color:var(--vurgu-parlak,#e0343c);color:var(--vurgu-parlak,#e0343c)}}',
        '.k95y-btn[aria-expanded="true"]{border-color:var(--vurgu-parlak,#e0343c);color:var(--vurgu-parlak,#e0343c)}',
        '.k95y-btn svg{width:18px;height:18px;flex:none}',
        // Sabit buton alt bilgideki bağlantıların üstüne binmesin
        '@media (min-width:769px){:root .site-footer{padding-bottom:calc(var(--bosluk-4,32px) + 56px)}.site-footer .footer-ic nav{padding-right:140px}}',
        // Panel her zaman flex; kapalıyken görünmez + hafif aşağıda. visibility gecikmeli kapanır ki çıkış animasyonu görünsün.
        '.k95y-panel{position:fixed;right:16px;bottom:72px;z-index:95;display:flex;flex-direction:column;box-sizing:border-box;width:380px;max-width:calc(100vw - 32px);',
        'height:560px;height:min(560px,calc(100vh - 96px));height:min(560px,calc(var(--k95y-vh,100dvh) - 96px));',
        'background:var(--bg-ana,#0e0c0c);color:var(--text-ana,#ede7e2);border:1px solid var(--border-renk,#2a2323);border-radius:2px;font-family:inherit;overflow:hidden;',
        'box-shadow:0 16px 40px rgba(0,0,0,.35);opacity:0;visibility:hidden;pointer-events:none;transform:translateY(12px) scale(.98);transform-origin:100% 100%;',
        'transition:opacity .18s ease,transform .18s ease,visibility 0s linear .18s}',
        '.k95y-panel.acik{opacity:1;visibility:visible;pointer-events:auto;transform:none;transition:opacity .22s ease,transform .26s cubic-bezier(.2,.8,.2,1),visibility 0s}',
        '.k95y-ust{display:flex;flex:none;align-items:center;justify-content:space-between;gap:8px;padding:6px 4px 6px 16px;border-bottom:1px solid var(--border-renk,#2a2323)}',
        '.k95y-baslik{display:flex;flex-direction:column;min-width:0;line-height:1.25}',
        '.k95y-ust strong{font-weight:500;font-size:.95rem}',
        '.k95y-not{font-size:.75rem;color:var(--text-ikincil,#a89f99);white-space:nowrap;overflow:hidden;text-overflow:ellipsis}',
        '.k95y-kapat{flex:none;background:none;border:0;color:var(--text-ikincil,#a89f99);font:inherit;font-size:.85rem;cursor:pointer;min-height:44px;min-width:44px;padding:0 12px}',
        '@media (hover:hover){.k95y-kapat:hover,.k95y-form button:hover{color:var(--vurgu-parlak,#e0343c)}.k95y-oneri button:hover{border-color:var(--vurgu-parlak,#e0343c)}}',
        '.k95y-liste{flex:1 1 auto;min-height:0;overflow-y:auto;overscroll-behavior:contain;-webkit-overflow-scrolling:touch;padding:14px 14px 10px;display:flex;flex-direction:column;gap:10px}',
        '.k95y-m{max-width:88%;font-size:.9rem;line-height:1.5;overflow-wrap:anywhere;padding:8px 12px;border-radius:2px}',
        '.k95y-m.bot{align-self:flex-start;color:var(--text-ana,#ede7e2);background:var(--bg-kart,#171313);border:1px solid var(--border-renk,#2a2323)}',
        '.k95y-m.ben{align-self:flex-end;border:1px solid var(--border-belirgin,#3a3030)}',
        '.k95y-m a{display:inline-block;margin-top:6px;color:var(--vurgu-parlak,#e0343c);text-decoration:underline;text-underline-offset:3px}',
        // Konu/soru şeridi: tek satır, yatay kayar; sağ kenar solarak daha fazlası olduğunu gösterir.
        '.k95y-oneri{flex:none;display:flex;flex-wrap:nowrap;align-items:center;gap:8px;padding:8px 12px;overflow-x:auto;overflow-y:hidden;overscroll-behavior-x:contain;-webkit-overflow-scrolling:touch;',
        'scroll-snap-type:x proximity;scroll-padding:0 12px;scrollbar-width:none;border-top:1px solid var(--border-renk,#2a2323)}',
        '.k95y-oneri::-webkit-scrollbar{display:none}',
        '.k95y-oneri.tasma:not(.son){-webkit-mask-image:linear-gradient(90deg,#000 calc(100% - 36px),transparent);mask-image:linear-gradient(90deg,#000 calc(100% - 36px),transparent)}',
        '.k95y-oneri button{flex:none;scroll-snap-align:start;white-space:nowrap;min-height:44px;padding:0 14px;background:none;border:1px solid var(--border-renk,#2a2323);border-radius:2px;color:var(--text-ana,#ede7e2);font:inherit;font-size:.85rem;line-height:1.2;cursor:pointer;transition:border-color .15s ease,background-color .15s ease}',
        '.k95y-oneri button.k95y-geri{color:var(--text-ikincil,#a89f99);padding:0 12px}',
        '.k95y-oneri button:focus-visible,.k95y-kapat:focus-visible,.k95y-form button:focus-visible,.k95y-btn:focus-visible{outline:2px solid var(--vurgu-parlak,#e0343c);outline-offset:-2px}',
        '.k95y-oneri button[aria-disabled="true"]{opacity:.45;cursor:default}',
        '.k95y-form{display:flex;flex:none;gap:8px;align-items:center;padding:8px 12px;border-top:1px solid var(--border-renk,#2a2323)}',
        // iOS odaklanınca yakınlaştırmasın diye yazı boyutu en az 16px.
        '.k95y-form input{flex:1;min-width:0;min-height:44px;box-sizing:border-box;padding:0 12px;background:var(--bg-kart,#171313);border:1px solid var(--border-renk,#2a2323);border-radius:2px;color:var(--text-ana,#ede7e2);font:inherit;font-size:16px}',
        '.k95y-form input:focus{outline:none;border-color:var(--vurgu-parlak,#e0343c)}',
        '.k95y-form button{flex:none;min-height:44px;padding:0 14px;background:none;border:1px solid var(--border-belirgin,#3a3030);border-radius:2px;color:var(--text-ana,#ede7e2);font:inherit;font-size:.9rem;font-weight:500;cursor:pointer}',
        '.k95y-form button:disabled{opacity:.5;cursor:default}',
        '.k95y-sr{position:absolute;width:1px;height:1px;margin:-1px;padding:0;overflow:hidden;clip:rect(0 0 0 0);white-space:nowrap;border:0}',
        '.k95y-m{animation:k95yGel .22s ease-out}.k95y-m.eski{animation:none}',
        '@keyframes k95yGel{from{opacity:0;transform:translateY(6px)}to{opacity:1;transform:none}}',
        '.k95y-yaziyor{display:inline-flex;gap:4px;align-items:center;height:1.2em}',
        '.k95y-yaziyor i{width:5px;height:5px;border-radius:50%;background:var(--text-ikincil,#a89f99);animation:k95yNokta 1s infinite ease-in-out}',
        '.k95y-yaziyor i:nth-child(2){animation-delay:.15s}.k95y-yaziyor i:nth-child(3){animation-delay:.3s}',
        '@keyframes k95yNokta{0%,60%,100%{opacity:.25}30%{opacity:1}}',
        '.k95y-imlec::after{content:"";display:inline-block;width:1px;height:1em;margin-left:2px;vertical-align:text-bottom;background:currentColor;animation:k95yImlec .8s steps(1) infinite}',
        '@keyframes k95yImlec{50%{opacity:0}}',
        '.k95y-btn{right:max(16px,env(safe-area-inset-right,0px))}.k95y-panel{right:max(16px,env(safe-area-inset-right,0px))}',
        // Alçak ama geniş ekranlar (yatay telefon, küçük pencere): panel ekrana sığsın, düğme panelin altında kalmasın.
        '@media (min-width:769px) and (max-height:620px){.k95y-panel{bottom:8px;height:calc(100vh - 16px);height:calc(var(--k95y-vh,100dvh) - 16px)}.k95y-btn[aria-expanded="true"]{visibility:hidden}}',
        // Telefon: görünür alanın (klavye açıkken klavyenin üstü) tamamını kaplayan sayfa. Konum ve yükseklik JS'ten gelir.
        '@media (max-width:768px){',
        '.k95y-btn{bottom:calc(var(--alt-gezinme-yuksekligi,0px) + 16px + env(safe-area-inset-bottom,0px));width:44px;padding:0;justify-content:center;border-radius:50%}',
        '.k95y-btn-yazi{display:none}',
        '.k95y-btn[aria-expanded="true"]{visibility:hidden}',
        '.k95y-panel{z-index:1000;left:0;right:0;top:var(--k95y-top,0px);bottom:auto;width:100%;max-width:none;height:100vh;height:100dvh;height:var(--k95y-vh,100dvh);',
        'border:0;border-radius:0;box-shadow:none;transform:translateY(24px);transform-origin:50% 100%}',
        '.k95y-ust{padding-top:max(6px,env(safe-area-inset-top,0px));padding-left:max(16px,env(safe-area-inset-left,0px));padding-right:max(4px,env(safe-area-inset-right,0px))}',
        '.k95y-oneri{padding-left:max(12px,env(safe-area-inset-left,0px));padding-right:max(12px,env(safe-area-inset-right,0px))}',
        '.k95y-form{padding-left:max(12px,env(safe-area-inset-left,0px));padding-right:max(12px,env(safe-area-inset-right,0px));padding-bottom:max(8px,env(safe-area-inset-bottom,0px))}',
        '.k95y-panel.kb .k95y-form{padding-bottom:8px}',
        '.k95y-panel.dar .k95y-oneri{display:none}',
        'html.k95y-kilit,html.k95y-kilit body{overflow:hidden}',
        '}',
        '@media (prefers-reduced-motion:reduce){.k95y-panel,.k95y-panel.acik,.k95y-btn,.k95y-oneri button{transition:none}.k95y-panel{transform:none}.k95y-m,.k95y-yaziyor i,.k95y-imlec::after{animation:none}}'
    ].join('');

    function el(ad, sinif, metin) {
        var e = document.createElement(ad);
        if (sinif) e.className = sinif;
        if (metin != null) e.textContent = metin;
        return e;
    }

    function kur() {
        var stil = document.createElement('style');
        stil.textContent = css;
        document.head.appendChild(stil);

        var btn = el('button', 'k95y-btn');
        btn.type = 'button';
        btn.setAttribute('aria-haspopup', 'dialog');
        btn.setAttribute('aria-expanded', 'false');
        btn.setAttribute('aria-controls', 'k95yPanel');
        var ikonSvg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        ikonSvg.setAttribute('viewBox', '0 0 20 20');
        ikonSvg.setAttribute('fill', 'none');
        ikonSvg.setAttribute('stroke', 'currentColor');
        ikonSvg.setAttribute('stroke-width', '1.5');
        ikonSvg.setAttribute('stroke-linejoin', 'round');
        ikonSvg.setAttribute('aria-hidden', 'true');
        var ikonYol = document.createElementNS('http://www.w3.org/2000/svg', 'path');
        ikonYol.setAttribute('d', 'M3 4.5h14v9H8.5L5 16.5v-3H3z');
        ikonSvg.appendChild(ikonYol);
        btn.appendChild(ikonSvg);
        btn.appendChild(el('span', 'k95y-btn-yazi', 'Yardım'));
        btn.setAttribute('aria-label', 'Yardım');

        var panel = el('div', 'k95y-panel');
        panel.id = 'k95yPanel';
        panel.setAttribute('role', 'dialog');
        panel.setAttribute('aria-label', 'Kage Asistan');
        panel.setAttribute('aria-hidden', 'true');
        panel.inert = true; // kapalıyken içindeki düğmeler Tab sırasına girmesin

        var ust = el('div', 'k95y-ust');
        var baslik = el('div', 'k95y-baslik');
        baslik.appendChild(el('strong', '', 'Kage Asistan'));
        baslik.appendChild(el('span', 'k95y-not', 'Kural tabanlı asistan; gerçek kişi ya da yapay zeka değil'));
        ust.appendChild(baslik);
        var kapat = el('button', 'k95y-kapat', 'Kapat');
        kapat.type = 'button';
        ust.appendChild(kapat);

        // Harf harf yazılan mesajlar ekran okuyucuda parça parça okunmasın diye liste sessiz;
        // botun cevabı tek seferde ayrı, görünmez bir aria-live bölgesine yazılır.
        var liste = el('div', 'k95y-liste');
        liste.setAttribute('aria-live', 'off');
        var duyuru = el('div', 'k95y-sr');
        duyuru.setAttribute('aria-live', 'polite');
        duyuru.setAttribute('aria-atomic', 'true');
        var oneri = el('div', 'k95y-oneri');
        oneri.setAttribute('role', 'group');

        var form = el('form', 'k95y-form');
        var giris = el('input');
        giris.type = 'text';
        giris.placeholder = 'Sorunu yaz...';
        giris.maxLength = 200;
        giris.autocomplete = 'off';
        giris.setAttribute('aria-label', 'Sorunu yaz');
        giris.setAttribute('enterkeyhint', 'send');
        var gonder = el('button', '', 'Gönder');
        gonder.type = 'submit';
        form.appendChild(giris);
        form.appendChild(gonder);

        panel.appendChild(ust);
        panel.appendChild(liste);
        panel.appendChild(oneri);
        panel.appendChild(form);
        panel.appendChild(duyuru);
        document.body.appendChild(panel);
        document.body.appendChild(btn);

        var azalt = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        var meşgul = false;

        // En yeni mesaja kaydırır; yeni mesajda yumuşak, harf harf yazarken anında (titremesin diye).
        function asagi(yumusak) {
            if (yumusak && !azalt && liste.scrollTo) liste.scrollTo({ top: liste.scrollHeight, behavior: 'smooth' });
            else liste.scrollTop = liste.scrollHeight;
        }

        function baglantiEkle(m, baglanti) {
            m.appendChild(document.createElement('br'));
            var a = el('a', '', baglanti.ad);
            a.href = baglanti.url;
            m.appendChild(a);
        }

        // --- Hafıza: son 10 mesaj ve son konu sekme oturumu boyunca (sessionStorage) saklanır ---
        var GECMIS_ANAHTARI = 'kage95_yardim_gecmis';
        var gecmis = { m: [], son: null, selam: null };
        var sonKayit = null; // son cevaplanan soru; "peki o nasıl" gibi devam soruları buna göre yorumlanır

        function hafizaKaydet() {
            gecmis.son = sonKayit ? sonKayit.soru : null;
            try { window.sessionStorage.setItem(GECMIS_ANAHTARI, JSON.stringify(gecmis)); } catch (e) { /* gizli sekme vb. */ }
        }
        function hafizayaEkle(tur, metin, baglanti) {
            var kayit = { t: tur, x: metin };
            if (baglanti) kayit.l = { ad: baglanti.ad, url: baglanti.url };
            gecmis.m.push(kayit);
            if (gecmis.m.length > 10) gecmis.m = gecmis.m.slice(-10);
            hafizaKaydet();
        }
        function soruBul(baslik) {
            for (var i = 0; i < SORULAR.length; i++) if (SORULAR[i].soru === baslik) return SORULAR[i];
            return null;
        }

        // kaydet === false: hafızaya yazma (geri yüklenen ya da botYaz'ın kendisi kaydettiği mesajlar).
        function mesaj(tur, metin, baglanti, kaydet, eski) {
            var m = el('div', 'k95y-m ' + tur + (eski ? ' eski' : ''));
            m.appendChild(document.createTextNode(metin));
            if (baglanti) baglantiEkle(m, baglanti);
            liste.appendChild(m);
            asagi(!eski);
            if (kaydet !== false) hafizayaEkle(tur, metin, baglanti);
        }

        function duyur(metin) {
            duyuru.textContent = '';
            setTimeout(function () { duyuru.textContent = metin; }, 60);
        }

        // Meşgulken düğmeler "disabled" yerine aria-disabled olur: odak düğmeden düşmesin (klavye kullanıcıları için).
        function mesgulYap(d) {
            meşgul = d;
            Array.prototype.forEach.call(oneri.querySelectorAll('button'), function (b) {
                if (d) b.setAttribute('aria-disabled', 'true'); else b.removeAttribute('aria-disabled');
            });
            gonder.disabled = d;
        }

        // Botun cevabını önce "yazıyor" noktalarıyla, sonra harf harf yazarak gösterir.
        function botYaz(metin, baglanti) {
            duyur(metin + (baglanti ? ' Bağlantı: ' + baglanti.ad + '.' : ''));
            hafizayaEkle('bot', metin, baglanti);
            if (azalt) { mesaj('bot', metin, baglanti, false); return; }
            mesgulYap(true);
            var m = el('div', 'k95y-m bot');
            var nokta = el('span', 'k95y-yaziyor');
            nokta.setAttribute('aria-hidden', 'true');
            nokta.appendChild(document.createElement('i'));
            nokta.appendChild(document.createElement('i'));
            nokta.appendChild(document.createElement('i'));
            m.appendChild(nokta);
            liste.appendChild(m);
            asagi(true);

            setTimeout(function () {
                m.textContent = '';
                var yazi = document.createTextNode('');
                m.appendChild(yazi);
                m.classList.add('k95y-imlec');
                var i = 0;
                var adim = Math.max(1, Math.ceil(metin.length / 120)); // uzun cevaplar da en fazla ~2 sn sürsün
                var z = setInterval(function () {
                    i = Math.min(metin.length, i + adim);
                    yazi.nodeValue = metin.slice(0, i);
                    asagi(false);
                    if (i >= metin.length) {
                        clearInterval(z);
                        m.classList.remove('k95y-imlec');
                        if (baglanti) {
                            baglantiEkle(m, baglanti);
                            asagi(true);
                        }
                        mesgulYap(false);
                    }
                }, 18);
            }, 500);
        }

        // --- Konu / soru şeridi ---
        var sonKonu = null; // "← Konular"a dönünce odak bu konunun düğmesine geri gelir

        // Şeridin sağında gizli düğme kaldıysa kenar solar; sona gelince solma kalkar.
        function seritKenar() {
            var tasma = oneri.scrollWidth > oneri.clientWidth + 2;
            oneri.classList.toggle('tasma', tasma);
            oneri.classList.toggle('son', !tasma || oneri.scrollLeft + oneri.clientWidth >= oneri.scrollWidth - 2);
        }
        oneri.addEventListener('scroll', seritKenar, { passive: true });
        window.addEventListener('resize', seritKenar);

        // Dikey tekerlekle de yatay kaysın (masaüstü fare kullanıcıları için).
        oneri.addEventListener('wheel', function (e) {
            if (Math.abs(e.deltaY) > Math.abs(e.deltaX) && oneri.scrollWidth > oneri.clientWidth) {
                oneri.scrollLeft += e.deltaY;
                e.preventDefault();
            }
        }, { passive: false });

        function dugme(metin, sinif, tikla) {
            var b = el('button', sinif || '', metin);
            b.type = 'button';
            if (meşgul) b.setAttribute('aria-disabled', 'true');
            b.addEventListener('click', function () {
                if (meşgul) return;
                tikla(b);
            });
            oneri.appendChild(b);
            return b;
        }

        function oneriTemizle(etiket) {
            oneri.textContent = '';
            oneri.scrollLeft = 0;
            oneri.setAttribute('aria-label', etiket);
        }

        function konulariGoster(odakla) {
            oneriTemizle('Konular');
            var odak = null;
            KONULAR.forEach(function (k) {
                if (k.yonetici && !yonetici) return;
                var b = dugme(k.ad, 'k95y-konu', function () { sorulariGoster(k.id, true); });
                if (sonKonu === k.id) odak = b;
            });
            if (odak) odak.scrollIntoView({ block: 'nearest', inline: 'nearest' });
            seritKenar();
            if (odakla) (odak || oneri.querySelector('button')).focus({ preventScroll: true });
        }

        function sorulariGoster(konuId, odakla) {
            var k = konuBul(konuId);
            if (!k || !konuAcik(konuId, yonetici)) { konulariGoster(odakla); return; }
            sonKonu = konuId;
            oneriTemizle(k.ad + ' soruları');
            dugme('← Konular', 'k95y-geri', function () { konulariGoster(true); });
            var ilk = null;
            SORULAR.forEach(function (s) {
                if (s.konu !== konuId) return;
                var b = dugme(s.soru, 'k95y-soru', function () { soruSor(s); });
                if (!ilk) ilk = b;
            });
            seritKenar();
            if (odakla && ilk) ilk.focus({ preventScroll: true });
        }

        // "Bunu mu demek istediniz?" önerileri ve konulara dönüş.
        function onerileriGoster(oneriler) {
            oneriTemizle(ONERI_METNI);
            dugme('← Konular', 'k95y-geri', function () { konulariGoster(true); });
            oneriler.forEach(function (s) { dugme(s.soru, 'k95y-soru', function () { soruSor(s); }); });
            seritKenar();
        }

        // Hazır soru önce "Sorunu yaz" kutusuna harf harf yazılır, sonra gönderilip sohbete düşer.
        function soruSor(s) {
            function gonderSoru() {
                giris.value = '';
                mesaj('ben', s.soru);
                kayitCevapla(s);
            }
            if (azalt) { gonderSoru(); return; }
            mesgulYap(true);
            giris.value = '';
            var k = 0;
            var t = setInterval(function () {
                k++;
                giris.value = s.soru.slice(0, k);
                if (k >= s.soru.length) {
                    clearInterval(t);
                    setTimeout(gonderSoru, 250);
                }
            }, 30);
        }

        // --- Kişisel veriler: giriş bilgisi index.html'deki gibi okunur, yalnızca var olan uçlar çağrılır ---
        // Token ve e-posta hiçbir zaman sohbete yazılmaz; yalnızca kullanıcı adı ve herkese açık istatistikler gösterilir.
        var TOKEN_ANAHTARLARI = ['kage95_token', 'token', 'jwt'];
        var API = String(window.KAGE95_API_URL || '').replace(/\/+$/, '');
        var onbellek = {}; // panel her açıldığında sıfırlanır; bir açılışta her uç en fazla bir kez çağrılır

        function tokenAl() {
            var depolar = [];
            try { depolar.push(window.localStorage); } catch (e) { /* erişim yok */ }
            try { depolar.push(window.sessionStorage); } catch (e) { /* erişim yok */ }
            for (var d = 0; d < depolar.length; d++) {
                for (var i = 0; i < TOKEN_ANAHTARLARI.length; i++) {
                    try {
                        var t = depolar[d] && depolar[d].getItem(TOKEN_ANAHTARLARI[i]);
                        if (t) return t;
                    } catch (e) { /* erişim yok */ }
                }
            }
            return null;
        }

        // Sonuç: JSON ya da null (giriş yok, 401, ağ hatası, 6 sn zaman aşımı).
        function getir(yol) {
            if (onbellek[yol]) return onbellek[yol];
            var token = tokenAl();
            if (!token || !API || !window.fetch) return Promise.resolve(null);
            var kontrol = window.AbortController ? new AbortController() : null;
            var sure = kontrol ? setTimeout(function () { kontrol.abort(); }, 6000) : 0;
            onbellek[yol] = fetch(API + yol, {
                headers: { 'Authorization': 'Bearer ' + token, 'Accept': 'application/json' },
                signal: kontrol ? kontrol.signal : undefined
            }).then(function (r) { return r.ok ? r.json() : null; })
              .catch(function () { return null; })
              .then(function (v) { clearTimeout(sure); return v; });
            return onbellek[yol];
        }

        // Genel (giriş gerektirmeyen) API istekleri (manga arama, öneri vb.)
        function getirGenel(yol) {
            if (onbellek[yol]) return onbellek[yol];
            if (!API || !window.fetch) return Promise.resolve(null);
            var kontrol = window.AbortController ? new AbortController() : null;
            var sure = kontrol ? setTimeout(function () { kontrol.abort(); }, 6000) : 0;
            var headers = { 'Accept': 'application/json' };
            var token = tokenAl();
            if (token) headers['Authorization'] = 'Bearer ' + token;
            onbellek[yol] = fetch(API + yol, {
                headers: headers,
                signal: kontrol ? kontrol.signal : undefined
            }).then(function (r) { return r.ok ? r.json() : null; })
              .catch(function () { return null; })
              .then(function (v) { clearTimeout(sure); return v; });
            return onbellek[yol];
        }

        function yerelOku(anahtar) {
            try { return JSON.parse(window.localStorage.getItem(anahtar) || 'null'); } catch (e) { return null; }
        }
        function sayi(n) {
            n = Number(n) || 0;
            try { return n.toLocaleString('tr-TR'); } catch (e) { return String(n); }
        }
        function tarih(t) {
            var d = new Date(t);
            if (isNaN(d.getTime())) return '';
            try { return d.toLocaleDateString('tr-TR', { day: 'numeric', month: 'long', year: 'numeric' }); } catch (e) { return d.toDateString(); }
        }

        // Niyete göre kişisel cevap; veri yoksa null döner ve sorunun hazır cevabı gösterilir. Hiçbir zaman reddetmez.
        function kisiselCevap(niyet) {
            var profil = { ad: 'Profilime git', url: HESABIM };
            var is;
            if (niyet === 'ad') {
                is = getir('/auth/me').then(function (me) {
                    if (!me || !me.kullaniciAdi) return null;
                    var t = tarih(me.kayitTarihi);
                    return { metin: 'Sen ' + me.kullaniciAdi + ' olarak giriş yaptın.' + (t ? ' ' + t + ' tarihinden beri Kage 95 üyesisin.' : '') +
                        ' Profil resmini, şifreni ve puan geçmişini profil sayfandan yönetebilirsin.', baglanti: profil };
                });
            } else if (niyet === 'seviye' || niyet === 'atla') {
                is = getir('/hesap/puan').then(function (p) {
                    if (!p || p.seviye == null) return null;
                    var metin;
                    if (niyet === 'seviye') {
                        metin = 'Seviye ' + p.seviye + (p.rutbe ? ', rütben "' + p.rutbe + '"' : '') + '. Toplam ' + sayi(p.toplam) + ' puanın var; ' +
                            'sonraki seviyeye ' + sayi(p.sonrakiSeviyeIcinKalan) + ' puan kaldı.' +
                            (p.sonrakiRutbe ? ' "' + p.sonrakiRutbe + '" rütbesine ' + sayi(p.kalan) + ' puan var.' : '') +
                            (p.bugun ? ' Bugün ' + sayi(p.bugun.kazanilan) + '/' + sayi(p.bugun.tavan) + ' puan kazandın.' : '');
                    } else {
                        var kurallar = (p.kurallar || []).slice().sort(function (a, b) { return (b.puan || 0) - (a.puan || 0); }).slice(0, 3)
                            .map(function (k) { return k.ad + ' (+' + k.puan + ')'; });
                        metin = 'Seviye ' + (p.seviye + 1) + ' için ' + sayi(p.sonrakiSeviyeIcinKalan) + ' puan daha gerekiyor.' +
                            (kurallar.length ? ' En çok puan getirenler: ' + kurallar.join(', ') + '.' : '') +
                            (p.bugun ? ' Günde en fazla ' + sayi(p.bugun.tavan) + ' puan kazanılır; bugün ' + sayi(p.bugun.kazanilan) + ' puan topladın.' : '');
                    }
                    return { metin: metin, baglanti: profil };
                });
            } else if (niyet === 'favori') {
                is = getir('/user/library').then(function (liste) {
                    if (!Array.isArray(liste)) return null;
                    var kutuphane = { ad: 'Kütüphaneme git', url: 'index.html#/kutuphane' };
                    if (!liste.length) return { metin: 'Favorilerinde henüz seri yok. Bir serinin sayfasındaki kalp düğmesiyle kütüphanene ekleyebilirsin.', baglanti: kutuphane };
                    var adlar = liste.slice(0, 5).map(function (m) { return m.mangaAdi; });
                    return { metin: 'Favorilerinde ' + liste.length + ' seri var: ' + adlar.join(', ') +
                        (liste.length > 5 ? ' ve ' + (liste.length - 5) + ' seri daha.' : '.'), baglanti: kutuphane };
                });
            } else if (niyet === 'devam') {
                // Okuma kaydı bu cihazda tutulur (read.html yazar); giriş gerektirmez.
                var s = yerelOku('kage95_son_okunan');
                if (s && s.bolumId) {
                    var no = s.bolumNo != null ? 'Bölüm ' + s.bolumNo : 'son bölüm';
                    var sayfa = s.sayfa && s.toplam ? ' (sayfa ' + s.sayfa + '/' + s.toplam + ')' : '';
                    return Promise.resolve({ metin: 'En son ' + (s.mangaAdi || 'bir seri') + ', ' + no + sayfa + ' okuyordun. Kaldığın yerden devam edebilirsin.',
                        baglanti: { ad: no.charAt(0).toUpperCase() + no.slice(1) + ' – devam et', url: 'read.html?chapter=' + encodeURIComponent(s.bolumId) } });
                }
                is = Promise.resolve(null);
            } else if (niyet === 'sayi') {
                var o = yerelOku('kage95_okunan');
                var seri = 0, bolum = 0;
                if (o && typeof o === 'object') Object.keys(o).forEach(function (k) {
                    if (Array.isArray(o[k]) && o[k].length) { seri++; bolum += o[k].length; }
                });
                is = Promise.resolve(bolum ? { metin: 'Bu cihazda ' + seri + ' seriden toplam ' + bolum + ' bölüm açmışsın. (Sayım bu tarayıcıdaki kayda göre yapılır.)' } : null);
            } else {
                is = Promise.resolve(null);
            }
            return is.catch(function () { return null; });
        }

        // Bir soruyu cevaplar: kişisel niyet varsa önce veriyi dener, yoksa hazır cevabı yazar.
        function kayitCevapla(kayit) {
            sonKayit = kayit;
            hafizaKaydet();
            if (!kayit.niyet) { botYaz(kayit.cevap, kayit.baglanti); return; }
            mesgulYap(true);
            (/^y_/.test(kayit.niyet) ? yonetimCevap(kayit.niyet) : kisiselCevap(kayit.niyet)).then(function (c) {
                mesgulYap(false);
                if (c) {
                    botYaz(c.metin, c.baglanti);
                    if (c.sonra) c.sonra(); // yönetim cevaplarının işlem düğmeleri (konu şeridinin yerine)
                } else botYaz(kayit.cevap, kayit.baglanti);
            });
        }

        // --- Yönetici modu: yalnızca /admin/ben yetki verirse açılır. Her işlem sunucuda da yetki kontrolünden geçer;
        // değiştiren her işlem önce onay ister. E-posta ve token hiçbir zaman yazılmaz. ---
        var yonetici = null; // { kullaniciAdi, rol, yetkiler } ya da null
        var bekleyen = null; // onay bekleyen işlem: { calistir: () => Promise<{ metin, baglanti }> }
        var KOMUT_YETKI = { ban: 'kullanici.engelle', banKaldir: 'kullanici.engelle', oturum: 'kullanici.engelle', ara: 'kullanici.gor', ayar: 'ayar.yonet', duyuruYaz: 'ayar.yonet', yorumSil: 'yorum.sil' };
        var AYAR_ADLARI = { bakimModu: 'Bakım modu', kayitAcik: 'Yeni üye kaydı', yorumAcik: 'Yorumlar', sohbetAcik: 'Sohbet', duyuruAcik: 'Duyuru şeridi' };
        var AYAR_KOMUT = { bakimModu: 'bakımı', kayitAcik: 'kayıtları', yorumAcik: 'yorumları', sohbetAcik: 'sohbeti', duyuruAcik: 'duyuruyu' };

        function yoneticiAl() {
            var u = yerelOku('kage95_user');
            // Normal üyede /admin/ben hiç çağrılmaz (gereksiz 403 olmasın).
            if (!tokenAl() || (u && u.rol !== 'Admin' && u.rol !== 'Owner')) return Promise.resolve(null);
            return getir('/admin/ben').then(function (b) {
                return b && (b.rol === 'Owner' || (b.rol === 'Admin' && b.yetkiler && b.yetkiler.length)) ? b : null;
            });
        }
        function can(k) { return !!yonetici && (yonetici.rol === 'Owner' || (yonetici.yetkiler || []).indexOf(k) !== -1); }

        // Değiştiren istekler (önbelleğe alınmaz). Sonuç: { ok, veri, mesaj } (mesaj sunucunun "message" alanından).
        function istekGonder(yontem, yol, govde) {
            var token = tokenAl();
            if (!token || !API || !window.fetch) return Promise.resolve({ ok: false, mesaj: 'Oturum bulunamadı, yeniden giriş yap.' });
            var basliklar = { 'Authorization': 'Bearer ' + token, 'Accept': 'application/json' };
            if (govde) basliklar['Content-Type'] = 'application/json';
            return fetch(API + yol, { method: yontem, headers: basliklar, body: govde ? JSON.stringify(govde) : undefined })
                .then(function (r) {
                    return r.json().catch(function () { return null; }).then(function (v) {
                        var mesaj = v && typeof v.message === 'string' ? v.message
                            : r.status === 403 ? 'Bu işlem için yetkin yok.' : r.status === 401 ? 'Oturumun bitmiş, yeniden giriş yap.' : 'İşlem başarısız (' + r.status + ').';
                        return { ok: r.ok, veri: v, mesaj: mesaj };
                    });
                })
                .catch(function () { return { ok: false, mesaj: 'Sunucuya ulaşılamadı.' }; })
                .then(function (s) { onbellek = {}; return s; }); // sonraki okumalar güncel olsun
        }

        function kisalt(t, n) { t = String(t || '').replace(/\s+/g, ' ').trim(); return t.length > n ? t.slice(0, n - 1) + '…' : t; }
        function tarihSaat(t) {
            var d = new Date(t);
            if (isNaN(d.getTime())) return '';
            try { return d.toLocaleString('tr-TR', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' }); } catch (e) { return d.toISOString(); }
        }
        function sureMetni(gun) { return gun < 1 ? Math.max(1, Math.round(gun * 24)) + ' saat' : gun + ' gün'; }
        function girisDoldur(metin) { giris.value = metin; giris.focus({ preventScroll: true }); }
        function yonetimSeridi() { sorulariGoster('yonetim', false); }
        function geriYonetim() { dugme('← Yönetim', 'k95y-geri', function () { sorulariGoster('yonetim', true); }); }

        function onayIste(soru, calistir) {
            bekleyen = { calistir: calistir };
            botYaz(soru + ' Onaylıyor musun?');
            oneriTemizle('Onay');
            dugme('Onayla', 'k95y-soru', function () { mesaj('ben', 'Onayla'); onayla(); });
            dugme('Vazgeç', 'k95y-geri', function () { mesaj('ben', 'Vazgeç'); vazgec(); });
            seritKenar();
        }
        function onayla() {
            var b = bekleyen;
            bekleyen = null;
            if (!b) return;
            mesgulYap(true);
            b.calistir().then(function (c) {
                mesgulYap(false);
                botYaz(c.metin, c.baglanti);
                yonetimSeridi();
            });
        }
        function vazgec() {
            bekleyen = null;
            botYaz('Tamam, işlem iptal edildi.');
            yonetimSeridi();
        }

        // Sonuç: { tam, liste } (tam: büyük/küçük harf duyarsız birebir eşleşen) ya da null.
        function kullaniciBul(ad) {
            return getir('/admin/kullanicilar?search=' + encodeURIComponent(ad) + '&pageSize=10').then(function (v) {
                var liste = Array.isArray(v) ? v : v && Array.isArray(v.items) ? v.items : null;
                if (!liste) return null;
                var a = ad.toLocaleLowerCase('tr-TR'), tam = null;
                liste.forEach(function (u) { if (!tam && String(u.kullaniciAdi).toLocaleLowerCase('tr-TR') === a) tam = u; });
                return { tam: tam, liste: liste };
            });
        }
        function kullaniciSatir(u) {
            var rol = u.rol === 'Owner' ? 'site sahibi' : u.rol === 'Admin' ? 'yönetici' : 'üye';
            var durum = u.aktif ? 'aktif' : 'banlı (' + (u.banBitis ? tarih(u.banBitis) + ' tarihine kadar' : 'süresiz') + (u.banNedeni ? ', neden: ' + kisalt(u.banNedeni, 60) : '') + ')';
            return '• ' + u.kullaniciAdi + ' – ' + rol + ', ' + durum + ', ' + sayi(u.yorumSayisi) + ' yorum' +
                (u.kayitTarihi ? ', kayıt ' + tarih(u.kayitTarihi) : '') + (u.sonGirisTarihi ? ', son giriş ' + tarih(u.sonGirisTarihi) : '');
        }
        function komutMetni(k, ad) {
            if (k.tur === 'ban') return 'banla ' + ad + ' ' + (k.gun ? sureMetni(k.gun) + ' ' : '') + k.neden;
            if (k.tur === 'banKaldir') return 'ban kaldır ' + ad;
            return 'oturumlarını kapat ' + ad;
        }

        function araCevap(ad, liste) {
            if (!liste.length) { botYaz('"' + ad + '" ile eşleşen kullanıcı yok.'); return; }
            botYaz(liste.length + ' kullanıcı buldum:\n' + liste.slice(0, 5).map(kullaniciSatir).join('\n') + (liste.length > 5 ? '\n…ve ' + (liste.length - 5) + ' kullanıcı daha.' : ''),
                { ad: 'Kullanıcılar', url: 'admin.html#kullanicilar' });
            if (!can('kullanici.engelle')) return;
            oneriTemizle('Kullanıcı işlemleri');
            geriYonetim();
            liste.slice(0, 4).forEach(function (u) {
                if (u.aktif) dugme('Banla ' + u.kullaniciAdi, 'k95y-soru', function () { girisDoldur('banla ' + u.kullaniciAdi + ' 7 gün '); });
                else dugme('Banı kaldır ' + u.kullaniciAdi, 'k95y-soru', function () { mesaj('ben', 'ban kaldır ' + u.kullaniciAdi); komutCalistir({ tur: 'banKaldir', ad: u.kullaniciAdi }); });
            });
            seritKenar();
        }

        function kullaniciKomutu(k) {
            if (k.tur === 'ban' && !k.neden) {
                botYaz('Ban nedeni zorunlu. Örnek: "banla ' + k.ad + ' 7 gün spam". Süre yazmazsan ban süresiz olur.');
                girisDoldur('banla ' + k.ad + ' 7 gün ');
                return;
            }
            mesgulYap(true);
            kullaniciBul(k.ad).then(function (s) {
                mesgulYap(false);
                if (!s) { botYaz('Kullanıcı listesi şu an alınamadı.', { ad: 'Kullanıcılar', url: 'admin.html#kullanicilar' }); return; }
                if (k.tur === 'ara') { araCevap(k.ad, s.liste); return; }
                var u = s.tam;
                if (!u) {
                    if (!s.liste.length) { botYaz('"' + k.ad + '" adında kullanıcı bulamadım.'); return; }
                    botYaz('"' + k.ad + '" adında birebir eşleşen kullanıcı yok. Aşağıdakilerden birini seçersen komutu hazırlarım.');
                    oneriTemizle('Benzer kullanıcılar');
                    geriYonetim();
                    s.liste.slice(0, 6).forEach(function (x) {
                        dugme(x.kullaniciAdi, 'k95y-soru', function () { girisDoldur(komutMetni(k, x.kullaniciAdi)); });
                    });
                    seritKenar();
                    return;
                }
                var adres = '/admin/kullanicilar/' + encodeURIComponent(u.id);
                if (k.tur === 'ban') {
                    var bitis = k.gun ? new Date(Date.now() + k.gun * 86400000) : null;
                    onayIste(u.kullaniciAdi + (bitis ? ' ' + sureMetni(k.gun) + ' (' + tarihSaat(bitis) + ' tarihine kadar)' : ' süresiz') + ' banlanacak. Neden: ' + k.neden + '.', function () {
                        return istekGonder('POST', adres + '/ban', { neden: k.neden, bitis: bitis ? bitis.toISOString() : null }).then(function (r) {
                            return { metin: r.ok ? u.kullaniciAdi + ' banlandı.' + (r.veri && r.veri.banBitis ? ' Ban ' + tarihSaat(r.veri.banBitis) + ' tarihinde biter.' : ' Ban süresiz.') : 'Ban uygulanamadı: ' + r.mesaj,
                                baglanti: { ad: 'Kullanıcılar', url: 'admin.html#kullanicilar' } };
                        });
                    });
                } else if (k.tur === 'banKaldir') {
                    if (u.aktif) { botYaz(u.kullaniciAdi + ' zaten banlı değil.'); return; }
                    onayIste(u.kullaniciAdi + ' kullanıcısının banı kaldırılacak.', function () {
                        return istekGonder('POST', adres + '/ban-kaldir').then(function (r) {
                            return { metin: r.ok ? u.kullaniciAdi + ' kullanıcısının banı kaldırıldı.' : 'Ban kaldırılamadı: ' + r.mesaj };
                        });
                    });
                } else {
                    onayIste(u.kullaniciAdi + ' kullanıcısının bütün oturumları kapatılacak; yeniden giriş yapması gerekecek.', function () {
                        return istekGonder('POST', adres + '/oturumlari-kapat').then(function (r) {
                            return { metin: r.ok ? u.kullaniciAdi + ' kullanıcısının bütün oturumları kapatıldı.' : 'Oturumlar kapatılamadı: ' + r.mesaj };
                        });
                    });
                }
            });
        }

        function ayarKomutu(k) {
            mesgulYap(true);
            getir('/admin/ayarlar').then(function (a) {
                mesgulYap(false);
                if (!a) { botYaz('Ayarlar şu an alınamadı.', { ad: 'Ayarlar', url: 'admin.html#ayarlar' }); return; }
                var ad = AYAR_ADLARI[k.alan];
                if (!!a[k.alan] === k.deger) { botYaz(ad + ' zaten ' + (k.deger ? 'açık.' : 'kapalı.')); return; }
                if (k.alan === 'duyuruAcik' && k.deger && !String(a.duyuruMetni || '').trim()) {
                    botYaz('Duyuru metni boş. Önce metni yaz: "duyuru yaz Yeni bölümler geldi!"');
                    girisDoldur('duyuru yaz ');
                    return;
                }
                var not = k.alan === 'bakimModu' && k.deger ? ' Ziyaretçiler siteyi kullanamaz, bakım mesajını görür.' : '';
                onayIste(ad + ' ' + (k.deger ? 'açılacak.' : 'kapatılacak.') + not, function () {
                    var govde = {};
                    govde[k.alan] = k.deger;
                    return istekGonder('PUT', '/admin/ayarlar', govde).then(function (r) {
                        return { metin: r.ok ? ad + ' ' + (k.deger ? 'açıldı.' : 'kapatıldı.') : 'Ayar kaydedilemedi: ' + r.mesaj, baglanti: { ad: 'Ayarlar', url: 'admin.html#ayarlar' } };
                    });
                });
            });
        }

        function yorumSor(y) {
            onayIste('#' + y.id + ' numaralı yorum silinecek: ' + y.kullaniciAdi + ', ' + y.mangaAdi + (y.bolumNo != null ? ' bölüm ' + y.bolumNo : '') +
                ': "' + kisalt(y.metin, 80) + '". Bu geri alınamaz.', function () {
                return istekGonder('DELETE', '/admin/yorumlar/' + encodeURIComponent(y.id) + '?tur=' + y.tur).then(function (r) {
                    return { metin: r.ok ? 'Yorum silindi.' : 'Yorum silinemedi: ' + r.mesaj, baglanti: { ad: 'Yorumlar', url: 'admin.html#yorumlar' } };
                });
            });
        }

        // "yorum sil 42": bölüm ve seri yorumları ayrı numaralanır; ikisinde de varsa seçtirilir.
        function yorumSilKomutu(id) {
            mesgulYap(true);
            Promise.all([getir('/admin/yorumlar?tur=bolum&pageSize=100'), getir('/admin/yorumlar?tur=manga&pageSize=100')]).then(function (v) {
                mesgulYap(false);
                var bulunan = [].concat(v[0] || [], v[1] || []).filter(function (y) { return y.id === id; });
                if (!bulunan.length) { botYaz('#' + id + ' numaralı yorumu son yorumlar arasında bulamadım. Eski yorumları panelden silebilirsin.', { ad: 'Yorumlar', url: 'admin.html#yorumlar' }); return; }
                if (bulunan.length === 1) { yorumSor(bulunan[0]); return; }
                botYaz('#' + id + ' numarasıyla hem bölüm hem seri yorumu var. Hangisi?');
                oneriTemizle('Yorum seç');
                geriYonetim();
                bulunan.forEach(function (y) { dugme((y.tur === 'manga' ? 'Seri yorumu: ' : 'Bölüm yorumu: ') + kisalt(y.metin, 30), 'k95y-soru', function () { yorumSor(y); }); });
                seritKenar();
            });
        }

        function komutCalistir(k) {
            var yetki = KOMUT_YETKI[k.tur];
            if (!can(yetki)) { botYaz('Bu işlem için "' + yetki + '" yetkin yok. Yetkiyi site sahibi "Yöneticiler" bölümünden verebilir.'); return; }
            if (k.tur === 'ayar') { ayarKomutu(k); return; }
            if (k.tur === 'yorumSil') { yorumSilKomutu(k.id); return; }
            if (k.tur === 'duyuruYaz') {
                onayIste('Duyuru metni "' + k.metin + '" olacak ve duyuru şeridi açılacak.', function () {
                    return istekGonder('PUT', '/admin/ayarlar', { duyuruMetni: k.metin, duyuruAcik: true }).then(function (r) {
                        return { metin: r.ok ? 'Duyuru yayında.' : 'Duyuru kaydedilemedi: ' + r.mesaj, baglanti: { ad: 'Ayarlar', url: 'admin.html#ayarlar' } };
                    });
                });
                return;
            }
            kullaniciKomutu(k);
        }

        // Yönetim sorularının canlı cevapları; veri yoksa (yetki/ağ) null döner ve hazır cevap gösterilir.
        function yonetimCevap(niyet) {
            if (!yonetici) return Promise.resolve(null);
            var is;
            if (niyet === 'y_ozet') {
                is = getir('/admin/ozet').then(function (o) {
                    if (!o) return null;
                    var son = (o.sonMangalar || []).slice(0, 3).map(function (m) { return m.mangaAdi; });
                    return { metin: 'Sitede ' + sayi(o.mangaSayisi) + ' seri, ' + sayi(o.bolumSayisi) + ' bölüm, ' + sayi(o.sayfaSayisi) + ' sayfa, ' +
                        sayi(o.kullaniciSayisi) + ' kullanıcı ve ' + sayi((o.yorumSayisi || 0) + (o.mangaYorumSayisi || 0)) + ' yorum var.' +
                        (son.length ? ' Son eklenen seriler: ' + son.join(', ') + '.' : ''), baglanti: { ad: 'Özet', url: 'admin.html#ozet' } };
                });
            } else if (niyet === 'y_istatistik') {
                is = getir('/admin/istatistik?gun=7').then(function (s) {
                    if (!s || !s.toplam) return null;
                    var t = s.toplam, bugun = (s.gunler || [])[(s.gunler || []).length - 1];
                    return { metin: 'Son 7 günde ' + sayi(t.yeniKullanici) + ' yeni üye, ' + sayi(t.yeniYorum) + ' yorum ve ' + sayi(t.yeniBolum) + ' yeni bölüm.' +
                        (bugun ? ' Bugün: ' + sayi(bugun.yeniKullanici) + ' üye, ' + sayi(bugun.yeniYorum) + ' yorum, ' + sayi(bugun.yeniBolum) + ' bölüm.' : ''),
                        baglanti: { ad: 'Özet', url: 'admin.html#ozet' } };
                });
            } else if (niyet === 'y_yorumlar') {
                if (!can('yorum.sil')) return Promise.resolve(null);
                is = Promise.all([getir('/admin/yorumlar?tur=bolum&pageSize=5'), getir('/admin/yorumlar?tur=manga&pageSize=5')]).then(function (v) {
                    if (!v[0] && !v[1]) return null;
                    var liste = [].concat(v[0] || [], v[1] || []).sort(function (a, b) { return new Date(b.tarih) - new Date(a.tarih); }).slice(0, 5);
                    if (!liste.length) return { metin: 'Henüz yorum yok.' };
                    return {
                        metin: 'Son yorumlar:\n' + liste.map(function (y) {
                            return '#' + y.id + ' ' + y.kullaniciAdi + ' → ' + y.mangaAdi + (y.bolumNo != null ? ' B' + y.bolumNo : '') + ': "' + kisalt(y.metin, 70) + '"';
                        }).join('\n') + '\nSilmek için aşağıdaki düğmeye bas ya da "yorum sil <numara>" yaz.',
                        baglanti: { ad: 'Yorumlar', url: 'admin.html#yorumlar' },
                        sonra: function () {
                            oneriTemizle('Yorum işlemleri');
                            geriYonetim();
                            liste.forEach(function (y) { dugme('Sil #' + y.id + ' (' + y.kullaniciAdi + ')', 'k95y-soru', function () { mesaj('ben', 'yorum sil ' + y.id); yorumSor(y); }); });
                            seritKenar();
                        }
                    };
                });
            } else if (niyet === 'y_kayitlar') {
                if (!can('kayit.gor')) return Promise.resolve(null);
                is = getir('/admin/kayitlar?pageSize=5').then(function (v) {
                    if (!Array.isArray(v)) return null;
                    if (!v.length) return { metin: 'Henüz yönetim kaydı yok.' };
                    return { metin: 'Son yönetim işlemleri:\n' + v.map(function (d) {
                        return '• ' + tarihSaat(d.tarih) + ' – ' + d.yapanAdi + ': ' + d.eylem + (d.hedef ? ' (' + kisalt(d.hedef, 40) + ')' : '');
                    }).join('\n'), baglanti: { ad: 'Kayıtlar', url: 'admin.html#kayitlar' } };
                });
            } else if (niyet === 'y_ayarlar') {
                if (!can('ayar.yonet')) return Promise.resolve(null);
                is = getir('/admin/ayarlar').then(function (a) {
                    if (!a) return null;
                    var alanlar = Object.keys(AYAR_ADLARI);
                    return {
                        metin: alanlar.map(function (k) { return AYAR_ADLARI[k] + ': ' + (a[k] ? 'açık' : 'kapalı'); }).join('\n') +
                            (a.duyuruMetni ? '\nDuyuru metni: "' + kisalt(a.duyuruMetni, 120) + '"' : '') + '\nDeğiştirmek için aşağıdan seç ya da "bakımı aç" gibi yaz.',
                        baglanti: { ad: 'Ayarlar', url: 'admin.html#ayarlar' },
                        sonra: function () {
                            oneriTemizle('Ayar işlemleri');
                            geriYonetim();
                            alanlar.forEach(function (k) {
                                var metin = AYAR_KOMUT[k] + ' ' + (a[k] ? 'kapat' : 'aç');
                                dugme(metin.charAt(0).toLocaleUpperCase('tr-TR') + metin.slice(1), 'k95y-soru', function () {
                                    mesaj('ben', metin);
                                    komutCalistir({ tur: 'ayar', alan: k, deger: !a[k] });
                                });
                            });
                            seritKenar();
                        }
                    };
                });
            } else {
                is = Promise.resolve(null);
            }
            return is.catch(function () { return null; });
        }

        // Anlaşılmayan soruda 3 konu önerisi + tüm konular.
        function konuOner() {
            sonKonu = null;
            oneriTemizle('Önerilen konular');
            dugme('Tüm konular', 'k95y-geri', function () { konulariGoster(true); });
            (tokenAl() ? ['benim', 'okuma', 'hesap'] : ['hesap', 'okuma', 'sorun']).forEach(function (id) {
                var k = konuBul(id);
                if (k) dugme(k.ad, 'k95y-konu', function () { sorulariGoster(k.id, true); });
            });
            seritKenar();
        }

        // --- Manga arama: "Solo Leveling aç", "ara berserk" ya da yalnızca seri adı. Herkese açık öneri ucu kullanılır (giriş gerekmez). ---
        var ARAMA_SONEK = /^(.{2,80}?)\s+(okumak istiyorum|sayfas[ıi]na git|sayfas[ıi]|a[çc]|oku|git|bul|ara)[.!?\s]*$/i;
        var ARAMA_ONEK = /^(manga ara|seri ara|ara|bul|a[çc]|oku)\s+(.{2,80}?)[.!?\s]*$/i;
        // Seri adı değil, soru ya da genel kelime olan terimler ("nasıl manga ara", "seri bul").
        var GENEL_ARAMA = /\b(nasil|nereden|nerede|neden|niye|nerden)\b|^(manga|seri|bolum|mangalar|seriler|bolumler|bir seri|bir manga)$/;

        function aramaTerimi(metin) {
            var m = metin.match(ARAMA_ONEK);
            if (m) return { terim: m[2], acik: true };
            m = metin.match(ARAMA_SONEK);
            if (m) return { terim: m[1], acik: true };
            return { terim: metin, acik: false };
        }

        // Sonuç: [{ id, mangaAdi, bolumSayisi }] (en fazla 8) ya da null (ağ hatası, 6 sn zaman aşımı).
        function mangaAra(terim) {
            if (!API || !window.fetch) return Promise.resolve(null);
            var kontrol = window.AbortController ? new AbortController() : null;
            var sure = kontrol ? setTimeout(function () { kontrol.abort(); }, 6000) : 0;
            var basliklar = { 'Accept': 'application/json' };
            var token = tokenAl();
            if (token) basliklar['Authorization'] = 'Bearer ' + token; // yetkiliyse gizli seriler de çıksın
            return fetch(API + '/mangas/oneri?q=' + encodeURIComponent(terim), { headers: basliklar, signal: kontrol ? kontrol.signal : undefined })
                .then(function (r) { return r.ok ? r.json() : null; })
                .catch(function () { return null; })
                .then(function (v) { clearTimeout(sure); return v && Array.isArray(v.sonuclar) ? v.sonuclar : null; });
        }

        function mangaUrl(m) { return 'index.html#/manga/' + encodeURIComponent(m.id); }

        // Bulursa cevabı yazar ve true döner; bulamazsa hiçbir şey yazmaz, false döner.
        function mangaCevapla(terim) {
            mesgulYap(true);
            return mangaAra(terim.trim()).then(function (liste) {
                mesgulYap(false);
                if (!liste || !liste.length) return false;
                var ilk = liste[0];
                var bolum = ilk.bolumSayisi ? ' (' + ilk.bolumSayisi + ' bölüm)' : '';
                if (liste.length === 1) {
                    botYaz('"' + ilk.mangaAdi + '" serisini buldum' + bolum + '. Aşağıdaki bağlantıdan açabilirsin.', { ad: ilk.mangaAdi + ' – aç', url: mangaUrl(ilk) });
                } else {
                    botYaz(liste.length + ' seri buldum. En yakını "' + ilk.mangaAdi + '"' + bolum + '; diğerleri aşağıda.', { ad: ilk.mangaAdi + ' – aç', url: mangaUrl(ilk) });
                }
                oneriTemizle('Bulunan seriler');
                dugme('Tüm konular', 'k95y-geri', function () { konulariGoster(true); });
                liste.forEach(function (m) {
                    dugme(m.mangaAdi, 'k95y-soru', function () { ac(false); window.location.href = mangaUrl(m); });
                });
                seritKenar();
                return true;
            });
        }

        function cevapla(metin) {
            // Onay bekleyen işlem: yazıyla da onaylanabilir; başka bir mesaj işlemi iptal eder.
            if (bekleyen) {
                var cevap = norm(metin);
                if (/^(evet|onayla|onayliyorum|tamam|ok|olur)$/.test(cevap)) { onayla(); return; }
                if (/^(hayir|vazgec|iptal|dur)$/.test(cevap)) { vazgec(); return; }
                bekleyen = null;
            }
            var komut = yonetimKomutu(metin);
            if (komut) {
                if (yonetici) { komutCalistir(komut); return; }
                // Ayar/arama kalıpları normal sorulara da benzeyebilir ("yorumları kapat"), onlar SSS'ye düşer.
                if (komut.tur !== 'ayar' && komut.tur !== 'ara') { botYaz('Bu komut yalnızca yetkili yöneticiler içindir.'); konuOner(); return; }
            }
            // "Solo Leveling ara" gibi açık aramada önce seri aranır; "manga ara" SSS cevabı ancak seri bulunamazsa gelir.
            var acikArama = aramaTerimi(metin);
            if (!komut && acikArama.acik && !GENEL_ARAMA.test(norm(acikArama.terim))) {
                mangaCevapla(acikArama.terim).then(function (bulundu) {
                    if (bulundu) return;
                    // SSS'de net cevap ya da konu varsa o daha faydalı ("karanlık modu aç").
                    var sss = bul(metin, yonetici).tur;
                    if (sss === 'cevap' || sss === 'konu') { sssCevapla(metin, komut, true); return; }
                    botYaz('"' + kisalt(acikArama.terim, 40) + '" adında bir seri bulamadım. Adını farklı yazmayı deneyebilir ya da Keşfet sayfasına bakabilirsin.', { ad: 'Keşfet', url: 'index.html#/' });
                    konuOner();
                });
                return;
            }
            sssCevapla(metin, komut, false);
        }

        function sssCevapla(metin, komut, arandi) {
            var sonuc = bul(metin, yonetici);
            // "peki o nasıl" gibi devam soruları: net bir cevap yoksa önceki sorunun devamı açılır.
            if (sonuc.tur !== 'cevap' && sonKayit && TAKIP.test(norm(metin))) {
                var devami = sonKayit.takip ? soruBul(sonKayit.takip) : null;
                if (devami) {
                    kayitCevapla(devami);
                    sorulariGoster(devami.konu, false);
                } else {
                    var k = konuBul(sonKayit.konu);
                    botYaz('"' + (k ? k.ad : 'Bu') + '" konusunda başka sorular aşağıda; birini seçebilir ya da merak ettiğini daha ayrıntılı yazabilirsin.');
                    sorulariGoster(sonKayit.konu, false);
                }
                return;
            }
            if (sonuc.tur === 'cevap') {
                kayitCevapla(sonuc.kayit);
                sorulariGoster(sonuc.kayit.konu, false); // aynı konudaki diğer sorular da görünsün
                return;
            }
            // SSS'de karşılığı olmayan ayar komutu ("bakımı aç") yöneticiye özeldir.
            if (komut && komut.tur === 'ayar') { botYaz('Bu komut yalnızca yetkili yöneticiler içindir.'); konuOner(); return; }

            // FAQ doğrudan eşleşmediğinde manga arama veya tavsiye niyetini değerlendir
            var mNiyet = mangaNiyeti(metin, sonuc.tur);
            if (!mNiyet && aramaTerimi(metin).acik) mNiyet = { tur: 'ara', terim: aramaTerimi(metin).terim };
            if (mNiyet && mNiyet.tur === 'ara' && arandi) mNiyet = null; // aynı terim az önce arandı
            if (mNiyet) {
                mesgulYap(true);
                if (mNiyet.tur === 'kategori') {
                    getirGenel('/mangas?category=' + encodeURIComponent(mNiyet.kategori) + '&pageSize=4').then(function (liste) {
                        mesgulYap(false);
                        if (Array.isArray(liste) && liste.length > 0) {
                            var secilen = liste[Math.floor(Math.random() * liste.length)];
                            var aciklama = secilen.aciklama ? ' — ' + secilen.aciklama.slice(0, 90) + '...' : '';
                            botYaz('Senin için "' + mNiyet.kategori + '" türünde bir seri önerisi: ' + secilen.mangaAdi + aciklama, {
                                ad: secilen.mangaAdi + ' – Sayfasına git',
                                url: 'index.html#/manga/' + secilen.id
                            });
                            if (liste.length > 1) {
                                oneriTemizle(mNiyet.kategori + ' serileri');
                                dugme('← Konular', 'k95y-geri', function () { konulariGoster(true); });
                                liste.forEach(function (m) {
                                    dugme(m.mangaAdi, 'k95y-soru', function () { ac(false); window.location.href = mangaUrl(m); });
                                });
                                seritKenar();
                            }
                        } else {
                            botYaz('"' + mNiyet.kategori + '" kategorisinde yayında seri bulunamadı. Başka bir tür deneyebilir ya da "Rastgele manga" diyebilirsin.');
                            konuOner();
                        }
                    });
                    return;
                } else if (mNiyet.tur === 'rastgele') {
                    getirGenel('/mangas/rastgele').then(function (veri) {
                        mesgulYap(false);
                        if (veri && veri.id) {
                            botYaz('Sana rastgele bir seri önerisi: ' + veri.mangaAdi + '. Hemen okumaya başlayabilirsin!', {
                                ad: veri.mangaAdi + ' sayfasına git',
                                url: 'index.html#/manga/' + veri.id
                            });
                        } else {
                            botYaz('Şu an rastgele seri önerilemedi. Keşfet sayfasındaki güncel serilere göz atabilirsin.', {
                                ad: 'Keşfet',
                                url: 'index.html#/'
                            });
                        }
                    });
                    return;
                } else if (mNiyet.tur === 'ara') {
                    mesgulYap(false);
                    // "X aç" kalıbında ham terim (Türkçe harfler korunur), yoksa niyetten temizlenen terim aranır.
                    var arama = aramaTerimi(metin);
                    mangaCevapla(arama.acik ? arama.terim : mNiyet.terim).then(function (bulundu) {
                        if (!bulundu) devamEtNormalFaq();
                    });
                    return;
                }
            }

            devamEtNormalFaq();

            function devamEtNormalFaq() {
                if (sonuc.tur === 'oneri') {
                    botYaz(ONERI_METNI + ' Aşağıdan birini seç ya da sorunu farklı yaz.');
                    onerileriGoster(sonuc.oneriler);
                } else if (sonuc.tur === 'konu') {
                    botYaz('"' + sonuc.konu.ad + '" konusundaki sorular aşağıda; birini seç ya da sorunu daha ayrıntılı yaz.');
                    sorulariGoster(sonuc.konu.id, false);
                } else {
                    botYaz(BULUNAMADI, ILETISIM);
                    konuOner();
                }
            }
        }

        // Selamlama: oturumda ilk açılışta (ya da giriş durumu değişince) bir kez; giriş yaptıysa adıyla.
        function selamla() {
            var kimlik = yonetici ? 'yonetici' : tokenAl() ? 'uye' : 'misafir';
            if (gecmis.selam === kimlik) return;
            gecmis.selam = kimlik;
            hafizaKaydet();
            if (kimlik === 'misafir') { botYaz(KARSILAMA + ' ' + GIRIS_NOTU, { ad: 'Giriş yap', url: 'login.html' }); return; }
            if (kimlik === 'yonetici') {
                botYaz('Merhaba ' + yonetici.kullaniciAdi + '! Yönetici modundasın. "Site özeti", "son yorumlar", "kullanıcı ara ahmet", "banla ahmet 7 gün spam", "bakımı aç" ya da "duyuru yaz …" gibi komutlar verebilirsin; değiştiren her işlem önce onayını ister.',
                    { ad: 'Yönetim paneli', url: 'admin.html' });
                sorulariGoster('yonetim', false);
                return;
            }
            mesgulYap(true);
            getir('/auth/me').then(function (me) {
                mesgulYap(false);
                if (me && me.kullaniciAdi) {
                    botYaz('Merhaba ' + me.kullaniciAdi + '! Hesabın, seviyen, favorilerin ya da okuma geçmişin hakkında sorabilir veya aşağıdan bir konu seçebilirsin.');
                } else {
                    gecmis.selam = 'misafir';
                    hafizaKaydet();
                    botYaz(KARSILAMA + ' Hesabına şu an ulaşamadım; kişisel cevaplar için yeniden giriş yapman gerekebilir.', { ad: 'Giriş yap', url: 'login.html' });
                }
            });
        }

        // Önceki mesajları (aynı sekme oturumu) animasyonsuz geri yükle.
        try {
            var kayitli = JSON.parse(window.sessionStorage.getItem(GECMIS_ANAHTARI) || 'null');
            if (kayitli && Array.isArray(kayitli.m)) {
                gecmis.m = kayitli.m.slice(-10).filter(function (x) { return x && (x.t === 'bot' || x.t === 'ben') && typeof x.x === 'string'; });
                gecmis.selam = kayitli.selam === 'uye' || kayitli.selam === 'misafir' || kayitli.selam === 'yonetici' ? kayitli.selam : null;
                sonKayit = kayitli.son ? soruBul(kayitli.son) : null;
            }
        } catch (e) { /* bozuk kayıt: boş başla */ }
        gecmis.m.forEach(function (x) {
            // Yalnızca site içi göreli bağlantılar geri yüklenir (javascript: vb. değil).
            var l = x.l && typeof x.l.url === 'string' && /^[\w.\-\/?#=&%]+$/.test(x.l.url) ? x.l : null;
            mesaj(x.t, x.x, l, false, true);
        });
        konulariGoster(false);

        form.addEventListener('submit', function (e) {
            e.preventDefault();
            var q = giris.value.trim();
            if (!q || meşgul) return;
            mesaj('ben', q);
            giris.value = '';
            cevapla(q);
        });

        // Telefonda panel görünür alanı (visualViewport) birebir kaplar: klavye açılınca küçülür ve klavyenin üstünde kalır,
        // iOS sayfayı kaydırınca (offsetTop) panel de onunla gelir. Masaüstünde yalnızca yükseklik sınırı için kullanılır.
        var vvBekleyen = 0;
        function vvGuncelle() {
            var vv = window.visualViewport, root = document.documentElement;
            var yukseklik = vv ? vv.height : window.innerHeight;
            var ust = vv ? Math.max(0, vv.offsetTop) : 0;
            var kb = vv ? Math.max(0, Math.round(window.innerHeight - vv.height - vv.offsetTop)) : 0;
            root.style.setProperty('--k95y-vh', Math.round(yukseklik) + 'px');
            root.style.setProperty('--k95y-top', Math.round(ust) + 'px');
            panel.classList.toggle('kb', kb > 80);
            panel.classList.toggle('dar', yukseklik < 340); // çok alçak alanda konu şeridi gizlenir, yazma kutusu kalır
            if (panel.classList.contains('acik')) asagi(false);
            seritKenar();
        }
        function vvPlanla() {
            if (vvBekleyen) return;
            vvBekleyen = requestAnimationFrame(function () { vvBekleyen = 0; vvGuncelle(); });
        }
        if (window.visualViewport) {
            window.visualViewport.addEventListener('resize', vvPlanla);
            window.visualViewport.addEventListener('scroll', vvPlanla);
        }
        window.addEventListener('resize', vvPlanla);
        window.addEventListener('orientationchange', function () { setTimeout(vvGuncelle, 300); });
        giris.addEventListener('focus', function () { setTimeout(vvGuncelle, 300); });

        var telefon = window.matchMedia ? window.matchMedia('(max-width: 768px)') : null;

        function ac(durum) {
            panel.classList.toggle('acik', durum);
            panel.setAttribute('aria-hidden', durum ? 'false' : 'true');
            panel.inert = !durum;
            btn.setAttribute('aria-expanded', durum ? 'true' : 'false');
            // Telefonda panel açıkken arkadaki sayfa kaymasın.
            document.documentElement.classList.toggle('k95y-kilit', durum && !!(telefon && telefon.matches));
            if (durum) {
                onbellek = {}; // her açılışta güncel veri (bir açılış içinde önbellekten)
                vvGuncelle();
                asagi(false);
                // Yönetici durumu her açılışta yenilenir (giriş/çıkış ya da yetki değişmiş olabilir).
                yoneticiAl().then(function (y) {
                    var degisti = !!y !== !!yonetici;
                    yonetici = y;
                    if (!y) bekleyen = null;
                    if (degisti && !meşgul) konulariGoster(false);
                    if (!meşgul) selamla();
                });
                if (!(window.matchMedia && window.matchMedia('(pointer:coarse)').matches)) giris.focus({ preventScroll: true });
                else kapat.focus({ preventScroll: true });
            } else {
                btn.focus({ preventScroll: true });
            }
        }
        btn.addEventListener('click', function () { ac(!panel.classList.contains('acik')); });
        kapat.addEventListener('click', function () { ac(false); });
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && panel.classList.contains('acik')) ac(false);
        });

        function rotaGuncelle() {
            var h = window.location.hash || '';
            var sohbetMi = /^#\/sohbet\b/.test(h);
            if (sohbetMi) {
                btn.style.display = 'none';
                if (panel.classList.contains('acik')) ac(false);
            } else {
                btn.style.display = '';
            }
        }
        window.addEventListener('hashchange', rotaGuncelle);
        rotaGuncelle();
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', kur);
    else kur();
})();
