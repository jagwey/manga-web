// Sağ alttaki "Yardım" rehberi: hazır cevaplı, anahtar kelime eşleştirmeli sade bir sohbet kutusu.
// Gerçek bir yapay zeka değildir; cevaplar aşağıdaki listeden gelir. Yeni soru eklemek için SORULAR dizisine kayıt ekleyin.
(function () {
    'use strict';
    if (window.__kage95Yardim) return;
    window.__kage95Yardim = true;

    // anahtar: kullanıcı yazısında geçen kökler (Türkçe karakter ve büyük/küçük harf duyarsız).
    var SORULAR = [
        { soru: 'Nasıl kayıt olurum?', anahtar: ['kayit', 'uye ol', 'hesap ac', 'hesap olustur'],
          cevap: 'Sağ üstteki "Kayıt ol" düğmesine bas; kullanıcı adı, e-posta ve şifre yeterli. Hesap açmak ücretsiz.',
          baglanti: { ad: 'Kayıt ol', url: 'login.html?kayit=1' } },
        { soru: 'Giriş yapamıyorum', anahtar: ['giris', 'sifre', 'parola', 'unuttum', 'giremiyorum'],
          cevap: 'Kullanıcı adını ya da e-postanı ve şifreni kontrol et. Şifre sıfırlama henüz yok; hesabına erişemiyorsan site sahibiyle iletişime geç.',
          baglanti: { ad: 'Giriş sayfası', url: 'login.html' } },
        { soru: 'Kütüphaneye nasıl eklerim?', anahtar: ['kutuphane', 'favori', 'kaydet', 'listeme'],
          cevap: 'Giriş yaptıktan sonra serinin sayfasındaki "Kütüphaneye ekle" düğmesini kullan. Eklediklerini alt menüdeki ya da üst menüdeki "Kütüphane"de görürsün.' },
        { soru: 'Okuma modları nedir?', anahtar: ['mod', 'webtoon', 'klasik', 'okuma sekli', 'nasil okunur'],
          cevap: 'Okuma ekranında iki mod var: Klasik (sayfa sayfa) ve Webtoon (aşağı kaydırarak). Webtoon modunda bir sayfaya dokunursan içerik ekranın yaklaşık yarısı kadar aşağı kayar.' },
        { soru: 'Kaldığım yerden devam edebilir miyim?', anahtar: ['devam', 'kaldigim', 'hatirla', 'ilerleme', 'nerede kaldim'],
          cevap: 'Evet. Okuduğun sayfa tarayıcında hatırlanır; giriş yaptıysan ilerlemen hesabına da kaydedilir. Seri sayfasında okuduğun bölümler "Okundu" diye işaretlenir.' },
        { soru: 'Temayı nasıl değiştiririm?', anahtar: ['tema', 'gorunum', 'renk', 'karanlik', 'aydinlik', 'arka plan'],
          cevap: 'Sağ üstteki menüden (üç çizgi ya da profilin) "Görünüm ve temalar"ı aç. Daha fazla tema ileride eklenecek.' },
        { soru: 'Yorum nasıl yaparım?', anahtar: ['yorum', 'mesaj yaz', 'dusunce'],
          cevap: 'Yorumlar hem her bölümün altında hem serinin sayfasında. Yorum yazmak için giriş yapman gerekiyor; yorum en fazla 500 karakter olabilir.' },
        { soru: 'Seriyi ya da bölümü nasıl puanlarım?', anahtar: ['puan', 'yildiz', 'oy ver', 'oyla', 'degerlendir'],
          cevap: 'Giriş yaptıktan sonra serinin sayfasında ya da bölümün yorumlar kısmında 1-5 arası yıldıza bas. Puanını istediğin zaman değiştirebilir ya da "Kaldır" ile geri alabilirsin.' },
        { soru: 'Puan nasıl kazanılır?', anahtar: ['puan kazan', 'puan nasil', 'puan topla', 'rutbe', 'seviye', 'puanim', 'kac puan'],
          cevap: 'Giriş yapınca eylemlerin puan kazandırır: bir bölümü sonuna kadar okumak +10 (günde 10 bölüme kadar), en az 20 karakterlik yorum +5 (günde 5), seri ya da bölüme puan vermek +2, kütüphaneye seri eklemek +3, günlük giriş +2 (7 gün üst üste girersen +10), profil resmi eklemek +10. Günlük toplam üst sınır 250 puan. Puanın ve rütben Hesabım sayfasında görünür.',
          baglanti: { ad: 'Liderlik tablosu', url: 'index.html#/liderlik' } },
        { soru: 'Liderlik tablosu nedir?', anahtar: ['liderlik', 'siralama', 'lider tablo', 'en iyi okur', 'kim birinci', 'birinci'],
          cevap: 'Liderlik tablosu, siteyi en çok kullanan okurları puanlarına göre sıralar. Bu hafta, bu ay ve tüm zamanlar olmak üzere üç dönemi görebilirsin. Sıralamaya girmek için giriş yapıp bölüm okuman, yorum yazman ya da seri puanlaman yeterli.',
          baglanti: { ad: 'Liderlik tablosuna git', url: 'index.html#/liderlik' } },
        { soru: 'Nasıl arama yaparım?', anahtar: ['ara', 'bul', 'seri bul', 'tur', 'kategori', 'filtre'],
          cevap: 'Ana sayfadaki arama kutusuna serinin adını yaz; telefonda üstteki büyüteç simgesini kullan. Türlere göre süzmek için türlerin altındaki sekmelere bas.' },
        { soru: 'Kullanıcı adımı nasıl değiştiririm?', anahtar: ['kullanici adi', 'adimi degistir', 'isim degistir', 'e posta degistir', 'email degistir', 'mail degistir'],
          cevap: 'Profiline bas, "Hesabım"ı aç. Orada kullanıcı adını ve e-postanı değiştirebilirsin (şifreni girmen gerekir).',
          baglanti: { ad: 'Hesabım', url: 'index.html#/profil' } },
        { soru: 'Şifremi nasıl değiştiririm?', anahtar: ['sifre degistir', 'sifremi degistir', 'sifre guncelle', 'parola degistir'],
          cevap: 'Profiline bas, "Hesabım"ı aç ve "Şifre değiştir" bölümünü kullan. Yeni şifre en az 8 karakter olmalı; değiştirince diğer cihazlardaki oturumların kapanır.',
          baglanti: { ad: 'Hesabım', url: 'index.html#/profil' } },
        { soru: 'Profil resmi nasıl eklerim?', anahtar: ['profil resmi', 'avatar', 'fotograf', 'profil foto', 'resim yukle'],
          cevap: 'Profiline bas, "Hesabım"ı aç ve "Profil resmi" bölümünden JPG, PNG ya da WebP (en fazla 2 MB) yükle.',
          baglanti: { ad: 'Hesabım', url: 'index.html#/profil' } },
        { soru: 'Hesabım banlandı', anahtar: ['banland', 'hesabim kapali', 'hesap kapatildi', 'engellendi', 'banlandim'],
          cevap: 'Hesabın bir yönetici tarafından kapatılmış olabilir. Giriş yaparken görünen mesajda nedeni ve varsa bitiş zamanı yazar. İtiraz için site sahibiyle iletişime geç.' },
        { soru: 'Nasıl çıkış yaparım?', anahtar: ['cikis', 'oturumu kapat', 'hesaptan cik'],
          cevap: 'Sağ üstte profiline (adına) bas ve açılan menüden "Çıkış yap"ı seç.' },
        { soru: 'Sayfa açılmıyor / hata var', anahtar: ['hata', 'acilmiyor', 'yuklenmiyor', 'bozuk', 'calismiyor', 'sorun'],
          cevap: 'Sayfayı yenilemeyi dene. Görseller gelmiyorsa internet bağlantını kontrol et. Sorun sürerse site sahibine haber ver.' },
        { soru: 'Yeni bölümler nerede?', anahtar: ['yeni bolum', 'guncel', 'ne zaman', 'yeni seri', 'yeni'],
          cevap: 'Her serinin sayfasında bölüm listesi var; en yeni bölümü görmek için sıralamayı "Yeniden eskiye" yap. Yeni eklenen seriler ana sayfada "Yeni" etiketiyle görünür.' }
    ];

    var BULUNAMADI = 'Bunu tam anlayamadım. Aşağıdaki konulardan birini seçebilir ya da sorunu başka kelimelerle yazabilirsin.';

    function norm(t) {
        return String(t).toLocaleLowerCase('tr-TR')
            .replace(/[çğıöşü]/g, function (c) { return { 'ç': 'c', 'ğ': 'g', 'ı': 'i', 'ö': 'o', 'ş': 's', 'ü': 'u' }[c]; })
            .replace(/[^a-z0-9\s]/g, ' ')
            .replace(/\s+/g, ' ').trim();
    }

    function bul(metin) {
        var n = norm(metin);
        if (!n) return null;
        var en = null, enPuan = 0;
        SORULAR.forEach(function (s) {
            var puan = 0;
            s.anahtar.forEach(function (k) {
                var nk = norm(k);
                // Tek kelimelik kısa kökler ("ara", "tur", "yeni") yalnızca kelime başında eşleşsin.
                var eslesti = nk.length <= 4 ? new RegExp('(^| )' + nk).test(n) : n.indexOf(nk) !== -1;
                if (eslesti) puan += nk.length;
            });
            if (puan > enPuan) { enPuan = puan; en = s; }
        });
        return en;
    }

    var css = [
        '.k95y-btn{position:fixed;right:16px;bottom:16px;z-index:90;display:inline-flex;align-items:center;gap:8px;min-height:44px;padding:0 18px;border-radius:22px;',
        'background:var(--bg-ana,#0e0c0c);color:var(--text-ana,#ede7e2);border:1px solid var(--border-belirgin,#3a3030);font-family:inherit;font-size:.9rem;font-weight:500;line-height:1;cursor:pointer;transition:border-color .15s ease,color .15s ease}',
        '@media (hover:hover){.k95y-btn:hover{border-color:var(--vurgu-parlak,#e0343c);color:var(--vurgu-parlak,#e0343c)}}',
        '.k95y-btn[aria-expanded="true"]{border-color:var(--vurgu-parlak,#e0343c);color:var(--vurgu-parlak,#e0343c)}',
        '.k95y-btn svg{width:18px;height:18px;flex:none}',
        '.k95y-panel{position:fixed;right:16px;bottom:72px;z-index:95;width:340px;max-width:calc(100vw - 32px);max-height:calc(var(--k95y-vh,100vh) - 100px);display:none;flex-direction:column;',
        'background:var(--bg-ana,#0e0c0c);color:var(--text-ana,#ede7e2);border:1px solid var(--border-renk,#2a2323);border-radius:2px;font-family:inherit}',
        '.k95y-panel.acik{display:flex}',
        '.k95y-ust{display:flex;align-items:center;justify-content:space-between;padding:12px 16px;border-bottom:1px solid var(--border-renk,#2a2323)}',
        '.k95y-ust strong{font-weight:500;font-size:.95rem}',
        '.k95y-kapat{background:none;border:0;color:var(--text-ikincil,#a89f99);font:inherit;font-size:.85rem;cursor:pointer;min-height:44px;min-width:44px;padding:0 4px}',
        '@media (hover:hover){.k95y-kapat:hover,.k95y-form button:hover{color:var(--vurgu-parlak,#e0343c)}.k95y-oneri button:hover{border-color:var(--vurgu-parlak,#e0343c)}}',
        '.k95y-liste{flex:0 1 auto;max-height:300px;min-height:0;overflow-y:auto;padding:16px;display:flex;flex-direction:column;gap:12px}',
        '.k95y-liste:empty{display:none}','.k95y-m{max-width:92%;font-size:.9rem;line-height:1.55;overflow-wrap:anywhere}',
        '.k95y-m.bot{align-self:flex-start;color:var(--text-ana,#ede7e2)}',
        '.k95y-m.ben{align-self:flex-end;padding:8px 12px;border:1px solid var(--border-belirgin,#3a3030);border-radius:2px}',
        '.k95y-m a{display:inline-block;margin-top:8px;color:var(--vurgu-parlak,#e0343c);text-decoration:underline;text-underline-offset:3px}',
        '.k95y-oneri{display:flex;flex-wrap:wrap;gap:8px;padding:12px 16px;max-height:132px;overflow-y:auto;flex:0 1 auto;min-height:0}',
        '.k95y-oneri button{min-height:44px;padding:0 12px;background:none;border:1px solid var(--border-renk,#2a2323);border-radius:2px;color:var(--text-ana,#ede7e2);font:inherit;font-size:.875rem;cursor:pointer;transition:border-color .15s ease}',
                '.k95y-form{display:flex;flex:none;border-top:1px solid var(--border-renk,#2a2323);border-bottom:1px solid var(--border-renk,#2a2323)}',
        '.k95y-form input{flex:1;min-width:0;min-height:44px;padding:0 16px;background:transparent;border:0;color:var(--text-ana,#ede7e2);font:inherit;font-size:1rem}',
        '.k95y-form input:focus{outline:none;box-shadow:inset 0 -2px 0 var(--vurgu-parlak,#e0343c)}',
        '.k95y-form button{min-height:44px;padding:0 16px;background:none;border:0;border-left:1px solid var(--border-renk,#2a2323);color:var(--text-ana,#ede7e2);font:inherit;font-size:.9rem;font-weight:500;cursor:pointer}',
                '.k95y-not{padding:8px 16px 12px;font-size:.875rem;color:var(--text-ikincil,#a89f99)}',
        '.k95y-m{animation:k95yGel .22s ease-out}',
        '@keyframes k95yGel{from{opacity:0;transform:translateY(6px)}to{opacity:1;transform:none}}',
        '.k95y-yaziyor{display:inline-flex;gap:4px;align-items:center;height:1.2em}',
        '.k95y-yaziyor i{width:5px;height:5px;border-radius:50%;background:var(--text-ikincil,#a89f99);animation:k95yNokta 1s infinite ease-in-out}',
        '.k95y-yaziyor i:nth-child(2){animation-delay:.15s}.k95y-yaziyor i:nth-child(3){animation-delay:.3s}',
        '@keyframes k95yNokta{0%,60%,100%{opacity:.25}30%{opacity:1}}',
        '.k95y-imlec::after{content:"";display:inline-block;width:1px;height:1em;margin-left:2px;vertical-align:text-bottom;background:currentColor;animation:k95yImlec .8s steps(1) infinite}',
        '@keyframes k95yImlec{50%{opacity:0}}',
        '.k95y-oneri button:disabled{opacity:.45;cursor:default}',
        '@media (prefers-reduced-motion:reduce){.k95y-m,.k95y-yaziyor i,.k95y-imlec::after{animation:none}}',
        '.k95y-btn{right:max(16px,env(safe-area-inset-right,0px))}.k95y-panel{right:max(16px,env(safe-area-inset-right,0px));max-width:calc(100vw - 32px - env(safe-area-inset-left,0px) - env(safe-area-inset-right,0px))}',
        '@media (max-width:768px){.k95y-btn{bottom:calc(var(--alt-gezinme-yuksekligi,0px) + 16px + env(safe-area-inset-bottom,0px) + var(--k95y-kb,0px))}',
        '.k95y-panel{bottom:calc(var(--alt-gezinme-yuksekligi,0px) + 72px + env(safe-area-inset-bottom,0px) + var(--k95y-kb,0px));max-height:calc(var(--k95y-vh,100vh) - var(--alt-gezinme-yuksekligi,0px) - 96px - env(safe-area-inset-bottom,0px))}',
        '.k95y-panel.kb .k95y-oneri,.k95y-panel.kb .k95y-not{display:none}',
        '.k95y-oneri{max-height:96px}.k95y-not{display:none}.k95y-liste:not(:empty){flex:1 1 auto;min-height:120px}',
        '@media (max-height:760px){.k95y-panel.sohbet .k95y-oneri{display:none}}',
        '}',
        '@media (max-width:360px){.k95y-panel{width:auto;left:16px}}',
        '@media (max-height:500px){.k95y-btn{bottom:8px;right:max(12px,env(safe-area-inset-right,0px))}.k95y-panel{bottom:8px;max-height:calc(var(--k95y-vh,100vh) - 16px)}.k95y-oneri,.k95y-not{display:none}}',
        '@media (prefers-reduced-motion:reduce){.k95y-btn,.k95y-oneri button{transition:none}}'
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
        btn.innerHTML = '<svg viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linejoin="round" aria-hidden="true"><path d="M3 4.5h14v9H8.5L5 16.5v-3H3z"/></svg>';
        btn.appendChild(el('span', '', 'Yardım'));

        var panel = el('div', 'k95y-panel');
        panel.id = 'k95yPanel';
        panel.setAttribute('role', 'dialog');
        panel.setAttribute('aria-label', 'Yardım rehberi');

        var ust = el('div', 'k95y-ust');
        ust.appendChild(el('strong', '', 'Yardım'));
        var kapat = el('button', 'k95y-kapat', 'Kapat');
        kapat.type = 'button';
        ust.appendChild(kapat);

        var liste = el('div', 'k95y-liste');
        liste.setAttribute('aria-live', 'polite');
        var oneri = el('div', 'k95y-oneri');

        var form = el('form', 'k95y-form');
        var giris = el('input');
        giris.type = 'text';
        giris.placeholder = 'Sorunu yaz...';
        giris.maxLength = 200;
        giris.setAttribute('aria-label', 'Sorunu yaz');
        var gonder = el('button', '', 'Gönder');
        gonder.type = 'submit';
        form.appendChild(giris);
        form.appendChild(gonder);

        var not = el('div', 'k95y-not', 'Hazır cevaplı rehber; gerçek bir kişi ya da yapay zeka değildir.');

        panel.appendChild(ust);
        panel.appendChild(oneri);
        panel.appendChild(form);
        panel.appendChild(liste);
        panel.appendChild(not);
        document.body.appendChild(panel);
        document.body.appendChild(btn);

        function mesaj(tur, metin, baglanti) {
            var m = el('div', 'k95y-m ' + tur);
            m.appendChild(document.createTextNode(metin));
            if (baglanti) {
                m.appendChild(document.createElement('br'));
                var a = el('a', '', baglanti.ad);
                a.href = baglanti.url;
                m.appendChild(a);
            }
            liste.appendChild(m);
            panel.classList.add('sohbet');
            liste.scrollTop = liste.scrollHeight;
        }

        var azalt = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        var meşgul = false;
        var oneriDugmeleri = [];

        function mesgulYap(d) {
            meşgul = d;
            oneriDugmeleri.forEach(function (b) { b.disabled = d; });
            gonder.disabled = d;
        }

        // Botun cevabını önce "yazıyor" noktalarıyla, sonra harf harf yazarak gösterir.
        function botYaz(metin, baglanti) {
            if (azalt) { mesaj('bot', metin, baglanti); return; }
            mesgulYap(true);
            var m = el('div', 'k95y-m bot');
            var nokta = el('span', 'k95y-yaziyor');
            nokta.innerHTML = '<i></i><i></i><i></i>';
            m.appendChild(nokta);
            liste.appendChild(m);
            liste.scrollTop = liste.scrollHeight;

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
                    liste.scrollTop = liste.scrollHeight;
                    if (i >= metin.length) {
                        clearInterval(z);
                        m.classList.remove('k95y-imlec');
                        if (baglanti) {
                            m.appendChild(document.createElement('br'));
                            var a = el('a', '', baglanti.ad);
                            a.href = baglanti.url;
                            m.appendChild(a);
                            liste.scrollTop = liste.scrollHeight;
                        }
                        mesgulYap(false);
                    }
                }, 18);
            }, 500);
        }

        function cevapla(soru) {
            var s = bul(soru);
            if (s) botYaz(s.cevap, s.baglanti);
            else botYaz(BULUNAMADI);
        }

        SORULAR.slice(0, 9).forEach(function (s) {
            var b = el('button', '', s.soru);
            b.type = 'button';
            b.addEventListener('click', function () {
                if (meşgul) return;
                // Soru önce "Sorunu yaz" kutusuna yazılır, sonra gönderilip sohbete düşer.
                function gonderSoru() {
                    giris.value = '';
                    mesaj('ben', s.soru);
                    botYaz(s.cevap, s.baglanti);
                }
                if (azalt) { gonderSoru(); return; }
                mesgulYap(true);
                giris.value = '';
                giris.focus();
                var k = 0;
                var t = setInterval(function () {
                    k++;
                    giris.value = s.soru.slice(0, k);
                    if (k >= s.soru.length) {
                        clearInterval(t);
                        setTimeout(gonderSoru, 250);
                    }
                }, 30);
            });
            oneri.appendChild(b);
            oneriDugmeleri.push(b);
        });

        form.addEventListener('submit', function (e) {
            e.preventDefault();
            var q = giris.value.trim();
            if (!q || meşgul) return;
            mesaj('ben', q);
            giris.value = '';
            cevapla(q);
        });

        // Klavye açılınca (visualViewport küçülür) kutu klavyenin üstünde ve görünür kalır.
        function vvGuncelle() {
            var vv = window.visualViewport, root = document.documentElement;
            if (!vv) return;
            var kb = Math.max(0, Math.round(window.innerHeight - vv.height - vv.offsetTop));
            var kbAcik = kb > 80;
            root.style.setProperty('--k95y-vh', Math.round(vv.height) + 'px');
            root.style.setProperty('--k95y-kb', (kbAcik ? kb : 0) + 'px');
            panel.classList.toggle('kb', kbAcik);
        }
        if (window.visualViewport) {
            window.visualViewport.addEventListener('resize', vvGuncelle);
            window.visualViewport.addEventListener('scroll', vvGuncelle);
        }
        window.addEventListener('orientationchange', function () { setTimeout(vvGuncelle, 300); });
        giris.addEventListener('focus', function () { setTimeout(vvGuncelle, 300); });

        function ac(durum) {
            panel.classList.toggle('acik', durum);
            btn.setAttribute('aria-expanded', durum ? 'true' : 'false');
            if (durum) {
                vvGuncelle();
                if (!(window.matchMedia && window.matchMedia('(pointer:coarse)').matches)) giris.focus();
            } else {
                btn.focus();
            }
        }
        btn.addEventListener('click', function () { ac(!panel.classList.contains('acik')); });
        kapat.addEventListener('click', function () { ac(false); });
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && panel.classList.contains('acik')) ac(false);
        });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', kur);
    else kur();
})();
