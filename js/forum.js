// Kage 95 - Forum (#/forum). Konular seriye ve bölüme bağlanabilir; spoiler, okuru o bölüme gelene kadar bulanık kalır.
// Kullanım (React): <Kage95Forum.Sayfa yol={location.hash} api={API} tokenAl={tokenAl} kullanici={kullanici} />
//                   <Kage95Forum.SeriKutusu mangaId={7} mangaAdi="..." api={API} />
// Yollar: #/forum, #/forum/k/{kategori}, #/forum/seri/{mangaId}, #/forum/konu/{id}, #/forum/yeni[?seri=ID]
// Gerekir: React, js/adstili.js, js/yorum.js (metin işaretleri)
(function () {
    'use strict';
    if (window.Kage95Forum) return;
    var React = window.React;
    var h = React.createElement;
    var useState = React.useState, useEffect = React.useEffect, useRef = React.useRef;

    var MAKS_ICERIK = 2000;

    function htmlCoz(s) {
        return new DOMParser().parseFromString(s || '', 'text/html').body.textContent || '';
    }

    function metinOgeleri(s, anahtar) {
        var Y = window.Kage95Yorum;
        var duz = htmlCoz(s);
        return Y && Y.ayristir ? Y.ayristir(duz, 0, anahtar || 'f') : duz;
    }

    function tarih(t) {
        if (!t) return null;
        var d = new Date(/[zZ]|[+-]\d\d:?\d\d$/.test(t) ? t : t + 'Z');
        return isNaN(d) ? null : d;
    }

    function once(t) {
        var d = tarih(t);
        if (!d) return '';
        var sn = Math.max(0, (Date.now() - d.getTime()) / 1000);
        if (sn < 60) return 'az önce';
        if (sn < 3600) return Math.floor(sn / 60) + ' dk önce';
        if (sn < 86400) return Math.floor(sn / 3600) + ' sa önce';
        if (sn < 86400 * 30) return Math.floor(sn / 86400) + ' gün önce';
        return d.toLocaleDateString('tr-TR', { day: 'numeric', month: 'short', year: 'numeric' });
    }

    function tamTarih(t) {
        var d = tarih(t);
        return d ? d.toLocaleString('tr-TR', { dateStyle: 'medium', timeStyle: 'short' }) : '';
    }

    function bolumYaz(n) {
        if (n == null) return '';
        var s = String(n);
        return s.indexOf('.') >= 0 ? s.replace(/\.?0+$/, '') : s;
    }

    function istek(yol, yontem, token, govde) {
        var b = {};
        if (govde !== undefined) b['Content-Type'] = 'application/json';
        if (token) b.Authorization = 'Bearer ' + token;
        return fetch(yol, { method: yontem || 'GET', headers: b, body: govde !== undefined ? JSON.stringify(govde) : undefined })
            .then(function (r) {
                return r.text().then(function (x) {
                    var v = null;
                    try { v = x ? JSON.parse(x) : null; } catch (e) { v = null; }
                    if (r.status === 401 && token && (yontem || 'GET') === 'GET') return istek(yol, 'GET', null);
                    return { ok: r.ok, durum: r.status, veri: v };
                });
            });
    }

    function hataMetni(c, varsayilan) {
        var m = c && c.veri && (c.veri.message || c.veri.mesaj);
        if (m) return m;
        if (c && c.durum === 429) return 'Çok hızlı gidiyorsun, biraz bekle.';
        if (c && c.durum === 401) return 'Oturumun düşmüş, tekrar giriş yap.';
        return varsayilan;
    }

    function yolCoz(yol) {
        var y = (yol || '').replace(/^#/, '');
        var m;
        if ((m = y.match(/^\/forum\/konu\/(\d+)/))) return { gorunum: 'konu', id: +m[1] };
        if ((m = y.match(/^\/forum\/yeni(?:\?seri=(\d+))?/))) return { gorunum: 'yeni', seri: m[1] ? +m[1] : null };
        if ((m = y.match(/^\/forum\/seri\/(\d+)/))) return { gorunum: 'liste', seri: +m[1] };
        if ((m = y.match(/^\/forum\/k\/([a-z]+)/))) return { gorunum: 'liste', kategori: m[1] };
        return { gorunum: 'liste' };
    }

    // ---- Küçük parçalar ----

    function rutbeSinif(r) {
        return 'fr-rutbe fr-rutbe-' + String(r || '').toLowerCase()
            .replace(/ç/g, 'c').replace(/ğ/g, 'g').replace(/ı/g, 'i').replace(/ö/g, 'o').replace(/ş/g, 's').replace(/ü/g, 'u')
            .replace(/[^a-z]+/g, '-');
    }

    function Yazar(p) {
        var y = p.yazar || {};
        var S = window.Kage95AdStili;
        var stil = y.adStili;
        var harf = (y.ad || '?').charAt(0).toUpperCase();
        return h('div', { className: 'fr-yazar' + (p.kucuk ? ' kucuk' : '') },
            y.resim
                ? h('img', { className: 'fr-avatar', src: y.resim, alt: '', loading: 'lazy', decoding: 'async' })
                : h('span', { className: 'fr-avatar fr-avatar-harf', 'aria-hidden': 'true' }, harf),
            h('div', { className: 'fr-yazar-bilgi' },
                h('a', {
                    href: '#/kullanici/' + encodeURIComponent(y.ad || ''),
                    className: 'fr-ad ' + (S ? S.sinif(stil) : ''), style: S ? S.stil(stil) : undefined
                }, y.ad || 'Silinmiş', S && S.unvan(stil) ? h('span', { className: 'as-unvan' }, S.unvan(stil)) : null),
                y.rutbe ? h('span', { className: rutbeSinif(y.rutbe), title: 'Seviye ' + y.seviye }, y.rutbe, h('b', null, ' ' + y.seviye)) : null,
                p.zaman ? h('time', { className: 'fr-zaman', dateTime: p.zaman, title: tamTarih(p.zaman) }, once(p.zaman)) : null
            )
        );
    }

    // Spoiler örtüsü: kullanıcı o bölümü okuduysa ya da "göster" derse açılır.
    function Ortu(p) {
        var a = useState(false), acik = a[0], setAcik = a[1];
        if (!p.spoiler || p.okudu || acik) return h(React.Fragment, null, p.children);
        var etiket = p.mangaAdi
            ? (p.mangaAdi + (p.bolumNo != null ? ' · Bölüm ' + bolumYaz(p.bolumNo) : '') + ' spoiler içeriyor')
            : 'Spoiler içeriyor';
        return h('div', { className: 'fr-ortu' },
            h('div', { className: 'fr-ortu-ic', 'aria-hidden': 'true' }, p.children),
            h('button', {
                type: 'button', className: 'fr-ortu-dugme',
                onClick: function (e) { e.preventDefault(); e.stopPropagation(); setAcik(true); }
            }, h('span', { className: 'fr-ortu-kanji', 'aria-hidden': 'true' }, '秘'), h('span', null, etiket), h('small', null, 'Göstermek için dokun')));
    }

    function SeriEtiketi(p) {
        var k = p.konu;
        if (!k.mangaId) return null;
        var slug = k.mangaSlug || (window.slugOlustur ? window.slugOlustur(k.mangaAdi) : null) || k.mangaId;
        return h('a', { className: 'fr-seri', href: '#/manga/' + encodeURIComponent(slug), onClick: function (e) { e.stopPropagation(); } },
            k.mangaKapak ? h('img', { src: k.mangaKapak, alt: '', loading: 'lazy' }) : null,
            h('span', null, k.mangaAdi || 'Seri'),
            k.bolumNo != null ? h('b', null, 'Bl. ' + bolumYaz(k.bolumNo)) : null);
    }

    // ---- Konu kartı ----

    function KonuKarti(p) {
        var k = p.konu;
        var kat = p.kategoriler[k.kategori] || {};
        var git = function () { location.hash = '#/forum/konu/' + k.id; };
        return h('article', {
            className: 'fr-kart' + (k.sabit ? ' sabit' : '') + (p.dar ? ' dar' : ''),
            onClick: function (e) { if (!e.target.closest('a,button')) git(); }
        },
            h('div', { className: 'fr-kart-ust' },
                h(Yazar, { yazar: k.yazar, zaman: k.tarih, kucuk: true }),
                k.sabit ? h('span', { className: 'fr-isaret' }, '釘 Sabit') : null,
                k.kilitli ? h('span', { className: 'fr-isaret' }, '鎖 Kilitli') : null),
            h('h3', { className: 'fr-kart-baslik' }, h('a', { href: '#/forum/konu/' + k.id }, htmlCoz(k.baslik))),
            p.dar ? null : h(Ortu, { spoiler: k.spoiler, okudu: k.okudu, mangaAdi: k.mangaAdi, bolumNo: k.bolumNo },
                h('p', { className: 'fr-ozet' }, metinOgeleri(k.ozet, 'o' + k.id))),
            h('div', { className: 'fr-kart-alt' },
                kat.ad ? h('a', { className: 'fr-kat-cip', href: '#/forum/k/' + k.kategori }, h('span', { 'aria-hidden': 'true' }, kat.kanji), kat.ad) : null,
                h(SeriEtiketi, { konu: k }),
                h('span', { className: 'fr-sayac', title: 'Yanıt' }, '話 ' + k.yanitSayisi),
                h('span', { className: 'fr-sayac' + (k.benBegendim ? ' aktif' : ''), title: 'Beğeni' }, '♥ ' + k.begeniSayisi),
                k.yanitSayisi > 0 ? h('span', { className: 'fr-son' }, 'Son yanıt ' + once(k.sonYanitTarihi)) : null)
        );
    }

    // ---- Kategori listesi (yan panel) ----

    function YanPanel(p) {
        var girisli = !!p.kullanici;
        var toplam = p.liste.reduce(function (t, k) { return t + (k.sayi || 0); }, 0);
        return h('aside', { className: 'fr-yan' },
            girisli
                ? h('a', { className: 'fr-ac-dugme', href: '#/forum/yeni' + (p.seri ? '?seri=' + p.seri : '') }, h('span', { 'aria-hidden': 'true' }, '筆'), 'Konu aç')
                : h('a', { className: 'fr-ac-dugme ikincil', href: 'login.html' }, h('span', { 'aria-hidden': 'true' }, '鍵'), 'Konu açmak için giriş yap'),
            h('nav', { className: 'fr-katlar', 'aria-label': 'Forum kategorileri' },
                h('a', { href: '#/forum', className: 'fr-kat' + (!p.secili && !p.seri ? ' aktif' : '') },
                    h('span', { className: 'fr-kanji', 'aria-hidden': 'true' }, '全'), h('span', { className: 'fr-kat-ad' }, 'Tüm başlıklar'), h('b', null, toplam)),
                p.liste.map(function (k) {
                    return h('a', { key: k.anahtar, href: '#/forum/k/' + k.anahtar, className: 'fr-kat' + (p.secili === k.anahtar ? ' aktif' : ''), title: k.aciklama },
                        h('span', { className: 'fr-kanji', 'aria-hidden': 'true' }, k.kanji), h('span', { className: 'fr-kat-ad' }, k.ad), h('b', null, k.sayi || 0));
                })),
            h('div', { className: 'fr-kural' },
                h('strong', null, 'Gölge kuralı'),
                h('p', null, 'Konunu seriye ve bölüme bağla. O bölümü okumamış olan spoiler görmez, okuyan direkt görür.'))
        );
    }

    // ---- Liste görünümü ----

    function Liste(p) {
        var a = useState('son'), sirala = a[0], setSirala = a[1];
        var b = useState(1), sayfa = b[0], setSayfa = b[1];
        var c = useState(null), veri = c[0], setVeri = c[1];
        var d = useState('yukleniyor'), durum = d[0], setDurum = d[1];
        var e = useState(''), ara = e[0], setAra = e[1];
        var f = useState(''), araUygula = f[0], setAraUygula = f[1];

        var nesilRef = useRef(0);
        useEffect(function () { setSayfa(1); }, [p.kategori, p.seri, sirala, araUygula]);
        useEffect(function () {
            var nesil = ++nesilRef.current;
            setDurum('yukleniyor');
            var q = '?sirala=' + sirala + '&page=' + sayfa;
            if (p.kategori) q += '&kategori=' + encodeURIComponent(p.kategori);
            if (p.seri) q += '&mangaId=' + p.seri;
            if (araUygula) q += '&ara=' + encodeURIComponent(araUygula);
            istek(p.api + '/forum/konular' + q, 'GET', p.tokenAl())
                .then(function (c) {
                    if (nesil !== nesilRef.current) return;
                    if (!c.ok) { setDurum('hata'); return; }
                    setVeri(c.veri); setDurum('hazir');
                })
                .catch(function () {
                    if (nesil !== nesilRef.current) return;
                    setDurum('hata');
                });
        }, [p.kategori, p.seri, sirala, sayfa, araUygula]);

        var kat = p.kategori ? p.katHarita[p.kategori] : null;
        var baslik = kat ? kat.ad : (p.seri ? 'Seri konuları' : 'Tüm başlıklar');
        var konular = veri ? veri.konular || [] : [];
        var sabitler = veri ? veri.sabitler || [] : [];
        var toplam = veri ? veri.toplam || 0 : 0;
        var sayfaSayisi = Math.max(1, Math.ceil(toplam / 20));
        var SIRALAR = [['son', 'Son hareket'], ['yeni', 'Yeni'], ['populer', 'Popüler']];

        return h('section', { className: 'fr-ana' },
            h('header', { className: 'fr-ana-ust' },
                h('div', null,
                    h('h2', null, kat ? h('span', { className: 'fr-ana-kanji', 'aria-hidden': 'true' }, kat.kanji) : null, baslik),
                    h('p', null, kat ? kat.aciklama : (p.seri ? 'Bu seriye bağlı tartışmalar' : 'Gölgelerin konuştuğu yer'))),
                h('form', { className: 'fr-ara', role: 'search', onSubmit: function (ev) { ev.preventDefault(); setAraUygula(ara.trim()); } },
                    h('input', { type: 'search', placeholder: 'Başlıklarda ara', value: ara, maxLength: 50, 'aria-label': 'Forumda ara',
                        onChange: function (ev) { setAra(ev.target.value); if (!ev.target.value) setAraUygula(''); } }))),
            h('div', { className: 'fr-sirala', role: 'tablist', 'aria-label': 'Sıralama' },
                SIRALAR.map(function (s) {
                    return h('button', { key: s[0], type: 'button', role: 'tab', 'aria-selected': sirala === s[0], className: sirala === s[0] ? 'aktif' : '',
                        onClick: function () { setSirala(s[0]); } }, s[1]);
                })),
            sabitler.length ? h('div', { className: 'fr-sabitler' },
                h('div', { className: 'fr-sabit-baslik' }, h('span', { 'aria-hidden': 'true' }, '釘'), 'Sabit konular'),
                sabitler.map(function (k) { return h(KonuKarti, { key: k.id, konu: k, kategoriler: p.katHarita, dar: true }); })) : null,
            durum === 'yukleniyor' && !veri ? h('div', { className: 'fr-bos' }, 'Yükleniyor…') : null,
            durum === 'hata' ? h('div', { className: 'fr-bos' }, 'Forum şu an yüklenemedi. Biraz sonra tekrar dene.') : null,
            durum === 'hazir' && konular.length === 0 ? h('div', { className: 'fr-bos' },
                h('span', { className: 'fr-bos-kanji', 'aria-hidden': 'true' }, '無'),
                araUygula ? 'Aramana uyan başlık yok.' : 'Burada henüz konu yok. İlk gölge sen ol.') : null,
            h('div', { className: 'fr-liste' + (durum === 'yukleniyor' ? ' yukleniyor' : '') },
                konular.map(function (k) { return h(KonuKarti, { key: k.id, konu: k, kategoriler: p.katHarita }); })),
            sayfaSayisi > 1 ? h('nav', { className: 'fr-sayfalar', 'aria-label': 'Sayfalar' },
                h('button', { type: 'button', disabled: sayfa <= 1, onClick: function () { setSayfa(sayfa - 1); window.scrollTo(0, 0); } }, 'Önceki'),
                h('span', null, sayfa + ' / ' + sayfaSayisi),
                h('button', { type: 'button', disabled: sayfa >= sayfaSayisi, onClick: function () { setSayfa(sayfa + 1); window.scrollTo(0, 0); } }, 'Sonraki')) : null
        );
    }

    // ---- Yazı alanı (yanıt / konu) ----

    function Yazim(p) {
        var hamUzunluk = (p.deger || '').length;
        return h('div', { className: 'fr-yazim' },
            h('textarea', {
                value: p.deger, maxLength: MAKS_ICERIK, rows: p.satir || 5, placeholder: p.placeholder, 'aria-label': p.etiket || 'Metin',
                onChange: function (e) { p.degis(e.target.value); }
            }),
            h('div', { className: 'fr-yazim-alt' },
                h('span', { className: 'fr-ipucu' }, '**kalın**  *eğik*  ||gizli||'),
                h('span', { className: 'fr-sayi' + (hamUzunluk > MAKS_ICERIK - 100 ? ' uyari' : '') }, hamUzunluk + '/' + MAKS_ICERIK)));
    }

    // ---- Konu görünümü ----

    function Konu(p) {
        var a = useState(null), veri = a[0], setVeri = a[1];
        var b = useState('yukleniyor'), durum = b[0], setDurum = b[1];
        var c = useState(1), sayfa = c[0], setSayfa = c[1];
        var d = useState(''), yanit = d[0], setYanit = d[1];
        var e = useState(false), yanitSpoiler = e[0], setYanitSpoiler = e[1];
        var f = useState(false), bekle = f[0], setBekle = f[1];
        var g = useState(''), hata = g[0], setHata = g[1];
        var i = useState(0), yenile = i[0], setYenile = i[1];
        var girisli = !!p.kullanici;

        var nesilRef = useRef(0);
        useEffect(function () {
            var nesil = ++nesilRef.current;
            setDurum(function (d) { return d === 'hazir' ? d : 'yukleniyor'; });
            istek(p.api + '/forum/konular/' + p.id + '?page=' + sayfa, 'GET', p.tokenAl())
                .then(function (c) {
                    if (nesil !== nesilRef.current) return;
                    if (c.durum === 404) { setDurum('yok'); return; }
                    if (!c.ok) { setDurum('hata'); return; }
                    setVeri(c.veri); setDurum('hazir');
                    if (c.veri && c.veri.konu) document.title = htmlCoz(c.veri.konu.baslik) + ' - Forum - Kage 95';
                })
                .catch(function () {
                    if (nesil !== nesilRef.current) return;
                    setDurum('hata');
                });
        }, [p.id, sayfa, yenile]);

        if (durum === 'yukleniyor') return h('div', { className: 'fr-bos' }, 'Yükleniyor…');
        if (durum === 'yok') return h('div', { className: 'fr-bos' }, h('span', { className: 'fr-bos-kanji', 'aria-hidden': 'true' }, '無'), 'Konu bulunamadı ya da silindi. ', h('a', { href: '#/forum' }, 'Foruma dön'));
        if (durum === 'hata' || !veri) return h('div', { className: 'fr-bos' }, 'Konu yüklenemedi.');

        var k = veri.konu;
        var kat = p.katHarita[k.kategori] || {};
        var yetkili = !!veri.yetkili;
        var sayfaSayisi = Math.max(1, Math.ceil((veri.toplam || 0) / (veri.sayfaBoyutu || 30)));

        var begen = function () {
            if (!girisli) { location.href = 'login.html'; return; }
            istek(p.api + '/forum/konular/' + k.id + '/begeni', 'POST', p.tokenAl()).then(function (c) {
                if (c.ok && c.veri) setVeri(Object.assign({}, veri, { konu: Object.assign({}, k, { benBegendim: c.veri.begendim, begeniSayisi: c.veri.begeniSayisi }) }));
            });
        };
        var degistir = function (alan) {
            istek(p.api + '/forum/konular/' + k.id + '/' + alan, 'PUT', p.tokenAl()).then(function (c) {
                if (c.ok) setYenile(yenile + 1);
            });
        };
        var konuSil = function () {
            if (!window.confirm('Konu silinsin mi?')) return;
            istek(p.api + '/forum/konular/' + k.id, 'DELETE', p.tokenAl()).then(function (c) {
                if (c.ok) location.hash = '#/forum'; else window.alert(hataMetni(c, 'Silinemedi.'));
            });
        };
        var yanitSil = function (id) {
            if (!window.confirm('Yanıt silinsin mi?')) return;
            istek(p.api + '/forum/yanitlar/' + id, 'DELETE', p.tokenAl()).then(function (c) {
                if (c.ok) setYenile(yenile + 1); else window.alert(hataMetni(c, 'Silinemedi.'));
            });
        };
        var gonder = function (ev) {
            ev.preventDefault();
            if (bekle || !yanit.trim()) return;
            setBekle(true); setHata('');
            istek(p.api + '/forum/konular/' + k.id + '/yanitlar', 'POST', p.tokenAl(), { icerik: yanit, spoiler: yanitSpoiler })
                .then(function (c) {
                    setBekle(false);
                    if (!c.ok) { setHata(hataMetni(c, 'Yanıt gönderilemedi.')); return; }
                    setYanit(''); setYanitSpoiler(false);
                    var son = Math.max(1, Math.ceil(((veri.toplam || 0) + 1) / (veri.sayfaBoyutu || 30)));
                    if (son !== sayfa) setSayfa(son); else setYenile(yenile + 1);
                })
                .catch(function () { setBekle(false); setHata('Bağlantı hatası.'); });
        };

        return h('section', { className: 'fr-ana fr-konu' },
            h('nav', { className: 'fr-iz', 'aria-label': 'Konum' },
                h('a', { className: 'fr-geri-dugme', href: '#/forum' }, '← Foruma dön'),
                h('span', { className: 'fr-iz-ayrac', 'aria-hidden': 'true' }, '·'),
                h('a', { href: '#/forum/k/' + k.kategori }, kat.ad || k.kategori)),
            h('article', { className: 'fr-gonderi ana' },
                h('h1', { className: 'fr-konu-baslik' }, htmlCoz(k.baslik)),
                h('div', { className: 'fr-konu-meta' },
                    h(Yazar, { yazar: k.yazar, zaman: k.tarih }),
                    h(SeriEtiketi, { konu: k }),
                    k.kilitli ? h('span', { className: 'fr-isaret' }, '鎖 Kilitli') : null),
                h(Ortu, { spoiler: k.spoiler, okudu: k.okudu, mangaAdi: k.mangaAdi, bolumNo: k.bolumNo },
                    h('div', { className: 'fr-govde' }, metinOgeleri(k.icerik, 'k'))),
                h('div', { className: 'fr-eylemler' },
                    h('button', { type: 'button', className: 'fr-begen' + (k.benBegendim ? ' aktif' : ''), 'aria-pressed': !!k.benBegendim, onClick: begen }, '♥ ', k.begeniSayisi),
                    k.benim || yetkili ? h('button', { type: 'button', onClick: konuSil }, 'Sil') : null,
                    yetkili ? h('button', { type: 'button', onClick: function () { degistir('sabit'); } }, k.sabit ? 'Sabitten çıkar' : 'Sabitle') : null,
                    yetkili ? h('button', { type: 'button', onClick: function () { degistir('kilit'); } }, k.kilitli ? 'Kilidi aç' : 'Kilitle') : null)),
            h('h2', { className: 'fr-yanit-baslik' }, veri.toplam ? veri.toplam + ' yanıt' : 'Henüz yanıt yok'),
            h('ol', { className: 'fr-yanitlar' },
                (veri.yanitlar || []).map(function (y, n) {
                    return h('li', { key: y.id, className: 'fr-gonderi' + (y.yazar && y.yazar.id === k.yazar.id ? ' sahip' : '') },
                        h('div', { className: 'fr-gonderi-ust' },
                            h(Yazar, { yazar: y.yazar, zaman: y.tarih, kucuk: true }),
                            y.yazar && y.yazar.id === k.yazar.id ? h('span', { className: 'fr-isaret' }, 'Konu sahibi') : null,
                            h('span', { className: 'fr-no' }, '#' + ((sayfa - 1) * (veri.sayfaBoyutu || 30) + n + 1))),
                        h(Ortu, { spoiler: y.spoiler, okudu: k.okudu, mangaAdi: k.mangaAdi, bolumNo: k.bolumNo },
                            h('div', { className: 'fr-govde' }, metinOgeleri(y.icerik, 'y' + y.id))),
                        y.benim || yetkili ? h('div', { className: 'fr-eylemler' }, h('button', { type: 'button', onClick: function () { yanitSil(y.id); } }, 'Sil')) : null);
                })),
            sayfaSayisi > 1 ? h('nav', { className: 'fr-sayfalar', 'aria-label': 'Yanıt sayfaları' },
                h('button', { type: 'button', disabled: sayfa <= 1, onClick: function () { setSayfa(sayfa - 1); } }, 'Önceki'),
                h('span', null, sayfa + ' / ' + sayfaSayisi),
                h('button', { type: 'button', disabled: sayfa >= sayfaSayisi, onClick: function () { setSayfa(sayfa + 1); } }, 'Sonraki')) : null,
            k.kilitli && !yetkili ? h('div', { className: 'fr-bos' }, '鎖 Bu konu yanıtlara kapalı.')
                : !girisli ? h('div', { className: 'fr-bos' }, h('a', { href: 'login.html' }, 'Yanıt yazmak için giriş yap'))
                : h('form', { className: 'fr-form', onSubmit: gonder },
                    h(Yazim, { deger: yanit, degis: setYanit, placeholder: 'Yanıtını yaz…', etiket: 'Yanıt', satir: 4 }),
                    h('div', { className: 'fr-form-alt' },
                        h('label', { className: 'fr-onay' }, h('input', { type: 'checkbox', checked: yanitSpoiler, onChange: function (e) { setYanitSpoiler(e.target.checked); } }), 'Spoiler içeriyor'),
                        hata ? h('span', { className: 'fr-hata', role: 'alert' }, hata) : null,
                        h('button', { type: 'submit', className: 'fr-gonder', disabled: bekle || !yanit.trim() }, bekle ? 'Gönderiliyor…' : 'Yanıtla')))
        );
    }

    // ---- Yeni konu ----

    function SeriSecici(p) {
        var a = useState(''), q = a[0], setQ = a[1];
        var b = useState([]), sonuc = b[0], setSonuc = b[1];
        useEffect(function () {
            if (p.secili || q.trim().length < 2) { setSonuc([]); return; }
            var ctrl = new AbortController();
            var t = setTimeout(function () {
                fetch(p.api + '/mangas/oneri?q=' + encodeURIComponent(q.trim()), { signal: ctrl.signal })
                    .then(function (r) { return r.ok ? r.json() : null; })
                    .then(function (v) { setSonuc(v && v.sonuclar ? v.sonuclar.slice(0, 6) : []); })
                    .catch(function () {});
            }, 250);
            return function () { clearTimeout(t); ctrl.abort(); };
        }, [q, p.secili]);

        if (p.secili) return h('div', { className: 'fr-secili-seri' },
            p.secili.kapakResmiURL ? h('img', { src: p.secili.kapakResmiURL, alt: '' }) : null,
            h('span', null, p.secili.mangaAdi),
            h('button', { type: 'button', onClick: function () { p.sec(null); setQ(''); } }, 'Kaldır'));
        return h('div', { className: 'fr-seri-ara' },
            h('input', { type: 'search', value: q, placeholder: 'Seri ara (isteğe bağlı)', 'aria-label': 'Bağlanacak seri', onChange: function (e) { setQ(e.target.value); } }),
            sonuc.length ? h('ul', { className: 'fr-seri-sonuc' }, sonuc.map(function (m) {
                return h('li', { key: m.id }, h('button', { type: 'button', onClick: function () { p.sec(m); } },
                    m.kapakResmiURL ? h('img', { src: m.kapakResmiURL, alt: '' }) : null, h('span', null, m.mangaAdi)));
            })) : null);
    }

    function YeniKonu(p) {
        // Ekip: duyuru kategorisi ve sabitleme seçeneği görünür; asıl yetki kontrolü sunucuda.
        var ku = p.kullanici;
        var ekip = !!ku && (ku.rol === 'Owner' || (ku.rol === 'Admin' && Array.isArray(ku.yetkiler) && ku.yetkiler.indexOf('yorum.sil') >= 0));
        var izinli = p.liste.filter(function (k) { return ekip || !k.sadeceYetkili; });
        var s = useState(false), sabit = s[0], setSabit = s[1];
        var a = useState(p.seri ? 'seri' : 'dojo'), kat = a[0], setKat = a[1];
        var b = useState(''), baslik = b[0], setBaslik = b[1];
        var c = useState(''), icerik = c[0], setIcerik = c[1];
        var d = useState(null), seri = d[0], setSeri = d[1];
        var e = useState(''), bolum = e[0], setBolum = e[1];
        var f = useState(false), spoiler = f[0], setSpoiler = f[1];
        var g = useState(false), bekle = g[0], setBekle = g[1];
        var i = useState(''), hata = i[0], setHata = i[1];

        useEffect(function () {
            if (!p.seri) return;
            fetch(p.api + '/mangas/' + p.seri).then(function (r) { return r.ok ? r.json() : null; })
                .then(function (m) { if (m && m.id) setSeri({ id: m.id, mangaAdi: m.mangaAdi, kapakResmiURL: m.kapakResmiURL }); })
                .catch(function () {});
        }, [p.seri]);

        if (!p.kullanici) return h('div', { className: 'fr-bos' }, h('a', { href: 'login.html' }, 'Konu açmak için giriş yap'));

        var gonder = function (ev) {
            ev.preventDefault();
            if (bekle) return;
            setBekle(true); setHata('');
            var bn = bolum.trim() ? parseFloat(bolum.replace(',', '.')) : null;
            istek(p.api + '/forum/konular', 'POST', p.tokenAl(), {
                kategori: kat, baslik: baslik, icerik: icerik, mangaId: seri ? seri.id : null,
                bolumNo: seri && bn != null && !isNaN(bn) ? bn : null, spoiler: spoiler, sabit: ekip && sabit
            }).then(function (c) {
                setBekle(false);
                if (!c.ok) { setHata(hataMetni(c, 'Konu açılamadı.')); return; }
                location.hash = '#/forum/konu/' + c.veri.id;
            }).catch(function () { setBekle(false); setHata('Bağlantı hatası.'); });
        };

        return h('section', { className: 'fr-ana fr-yeni' },
            h('nav', { className: 'fr-iz', 'aria-label': 'Konum' },
                h('a', { className: 'fr-geri-dugme', href: '#/forum' }, '← Foruma dön'),
                h('span', { className: 'fr-iz-ayrac', 'aria-hidden': 'true' }, '·'),
                h('span', null, 'Yeni konu aç')),
            h('h1', { className: 'fr-konu-baslik' }, h('span', { className: 'fr-ana-kanji', 'aria-hidden': 'true' }, '筆'), 'Yeni konu aç'),
            h('form', { className: 'fr-form', onSubmit: gonder },
                h('fieldset', { className: 'fr-kat-sec' },
                    h('legend', null, 'Kategori'),
                    izinli.map(function (k) {
                        return h('label', { key: k.anahtar, className: kat === k.anahtar ? 'aktif' : '' },
                            h('input', { type: 'radio', name: 'fr-kat', value: k.anahtar, checked: kat === k.anahtar, onChange: function () { setKat(k.anahtar); } }),
                            h('span', { className: 'fr-kanji', 'aria-hidden': 'true' }, k.kanji), k.ad);
                    })),
                h('label', { className: 'fr-alan' }, h('span', null, 'Başlık'),
                    h('input', { type: 'text', value: baslik, maxLength: 120, required: true, minLength: 4, placeholder: 'Kısa ve net bir başlık', onChange: function (e) { setBaslik(e.target.value); } })),
                h('div', { className: 'fr-alan fr-bag' },
                    h('span', null, 'Seri ve bölüm bağla'),
                    h('div', { className: 'fr-bag-satir' },
                        h(SeriSecici, { api: p.api, secili: seri, sec: setSeri }),
                        seri ? h('input', { type: 'text', inputMode: 'decimal', className: 'fr-bolum', value: bolum, placeholder: 'Bölüm no', 'aria-label': 'Bölüm numarası',
                            onChange: function (e) { setBolum(e.target.value.replace(/[^\d.,]/g, '').slice(0, 7)); } }) : null),
                    h('small', null, 'Bölüm girersen, o bölüme gelmemiş okurlar için spoiler örtüsü takılır.')),
                h('label', { className: 'fr-alan' }, h('span', null, 'İçerik'),
                    h(Yazim, { deger: icerik, degis: setIcerik, placeholder: 'Ne düşünüyorsun?', etiket: 'İçerik', satir: 8 })),
                h('div', { className: 'fr-form-alt' },
                    h('label', { className: 'fr-onay' },
                        h('input', { type: 'checkbox', checked: spoiler || kat === 'teori', disabled: kat === 'teori', onChange: function (e) { setSpoiler(e.target.checked); } }),
                        kat === 'teori' ? 'Teoriler her zaman spoiler sayılır' : 'Spoiler içeriyor'),
                    ekip ? h('label', { className: 'fr-onay' },
                        h('input', { type: 'checkbox', checked: sabit, onChange: function (e) { setSabit(e.target.checked); } }),
                        '釘 Konuyu sabitle') : null,
                    hata ? h('span', { className: 'fr-hata', role: 'alert' }, hata) : null,
                    h('button', { type: 'submit', className: 'fr-gonder', disabled: bekle || baslik.trim().length < 4 || !icerik.trim() }, bekle ? 'Açılıyor…' : 'Konuyu aç')))
        );
    }

    // ---- Ana sayfa ----

    var katOnbellek = null;

    function Sayfa(p) {
        var yol = yolCoz(p.yol);
        var a = useState(katOnbellek || []), liste = a[0], setListe = a[1];
        var b = useState(0), surum = b[0], setSurum = b[1];

        useEffect(function () {
            istek(p.api + '/forum/kategoriler', 'GET').then(function (c) {
                if (c.ok && Array.isArray(c.veri)) { katOnbellek = c.veri; setListe(c.veri); }
            }).catch(function () {});
        }, [yol.gorunum === 'liste' ? p.yol : surum]);

        useEffect(function () {
            if (yol.gorunum !== 'konu') document.title = 'Forum - Kage 95';
            window.scrollTo(0, 0);
        }, [p.yol]);

        var harita = {};
        liste.forEach(function (k) { harita[k.anahtar] = k; });
        var ortak = { api: p.api, tokenAl: p.tokenAl, kullanici: p.kullanici, katHarita: harita, liste: liste };

        var icerik = yol.gorunum === 'konu' ? h(Konu, Object.assign({ key: yol.id, id: yol.id }, ortak))
            : yol.gorunum === 'yeni' ? h(YeniKonu, Object.assign({ seri: yol.seri }, ortak))
            : h(Liste, Object.assign({ kategori: yol.kategori, seri: yol.seri }, ortak));

        return h('div', { className: 'ana-icerik fr-sayfa' },
            h('div', { className: 'fr-kafa' },
                h('span', { className: 'fr-kafa-kanji', 'aria-hidden': 'true' }, '影'),
                h('div', null, h('h1', null, 'Gölge Meclisi'), h('p', null, 'Kage 95 forumu. Seriye bağla, bölümü işaretle, spoiler derdi olmadan konuş.'))),
            h('div', { className: 'fr-duzen fr-duzen-' + (yol.gorunum || 'liste') }, h(YanPanel, { liste: liste, secili: yol.kategori, seri: yol.seri, kullanici: p.kullanici }), icerik));
    }

    // ---- Seri sayfası kutusu ----

    function SeriKutusu(p) {
        var a = useState(null), veri = a[0], setVeri = a[1];
        var b = useState('yukleniyor'), durum = b[0], setDurum = b[1];
        useEffect(function () {
            var iptal = false;
            setDurum('yukleniyor');
            istek(p.api + '/forum/konular?mangaId=' + p.mangaId + '&sirala=son', 'GET')
                .then(function (c) {
                    if (iptal) return;
                    if (c.ok) setVeri(c.veri || { konular: [], toplam: 0 });
                    setDurum(c.ok ? 'hazir' : 'hata');
                })
                .catch(function () { if (!iptal) setDurum('hata'); });
            return function () { iptal = true; };
        }, [p.mangaId]);
        var konular = veri ? (veri.konular || []).slice(0, 3) : [];
        var govde = durum === 'yukleniyor'
            ? h('p', null, 'Yükleniyor…')
            : durum === 'hata'
                ? h('p', null, 'Forum konuları yüklenemedi.')
                : konular.length
                    ? h('ul', null, konular.map(function (k) {
                        return h('li', { key: k.id },
                            h('a', { href: '#/forum/konu/' + k.id }, htmlCoz(k.baslik)),
                            h('span', null, (k.bolumNo != null ? 'Bl. ' + bolumYaz(k.bolumNo) + ' · ' : '') + k.yanitSayisi + ' yanıt · ' + once(k.sonYanitTarihi)));
                    }))
                    : h('p', null, 'Bu seri hakkında henüz konu yok.');
        return h('section', { className: 'fr-seri-kutu' },
            h('div', { className: 'fr-seri-kutu-ust' },
                h('h2', null, h('span', { 'aria-hidden': 'true' }, '影'), 'Forumda konuş'),
                h('a', { href: '#/forum/yeni?seri=' + p.mangaId }, 'Konu aç')),
            govde,
            durum === 'hazir' && veri && veri.toplam > 3 ? h('a', { className: 'fr-seri-tumu', href: '#/forum/seri/' + p.mangaId }, 'Tüm ' + veri.toplam + ' konuyu gör') : null);
    }

    window.Kage95Forum = { Sayfa: Sayfa, SeriKutusu: SeriKutusu };
})();
