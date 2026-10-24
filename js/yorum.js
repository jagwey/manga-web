// Kage 95 - Ortak yorum aracı (index.html seri yorumları + read.html bölüm yorumları).
// Kullanım (React): <Kage95Yorum.Yorumlar tur="bolum|manga" id={7} api={API} tokenAl={tokenAl}
//                     sinir={500} placeholder="..." yanittan={fn} />
// İşaretler düz metin olarak ön yüzde ayrıştırılır (React elemanı üretilir, HTML enjekte edilmez):
//   **kalın**, *eğik*, ||gizli||, [renk=kirmizi|mavi|turuncu|mor|gri|sari]..[/renk]
// Gerekir: React, js/adstili.js
(function () {
    'use strict';
    if (window.Kage95Yorum) return;
    var React = window.React;
    var h = React.createElement;
    var useState = React.useState, useEffect = React.useEffect, useRef = React.useRef;

    var SAYFA = 20;
    var RENKLER = ['kirmizi', 'mavi', 'turuncu', 'mor', 'gri', 'sari'];
    var RENK_AD = { kirmizi: 'Kırmızı', mavi: 'Mavi', turuncu: 'Turuncu', mor: 'Mor', gri: 'Gri', sari: 'Sarı' };

    // Sunucu yorumları &<>"' kodlar; metne geri çevir (React zaten kaçışlar).
    function htmlCoz(s) {
        return new DOMParser().parseFromString(s || '', 'text/html').body.textContent || '';
    }

    function tarihYaz(t) {
        if (!t) return '';
        var d = new Date(/[zZ]|[+-]\d\d:?\d\d$/.test(t) ? t : t + 'Z');
        return isNaN(d) ? '' : d.toLocaleString('tr-TR', { dateStyle: 'medium', timeStyle: 'short' });
    }

    // ---- İşaret ayrıştırıcı ----
    var KALIPLAR = [
        { tur: 'gizli', re: /\|\|([\s\S]+?)\|\|/ },
        { tur: 'kalin', re: /\*\*([\s\S]+?)\*\*/ },
        { tur: 'egik', re: /\*([^*\n]+?)\*/ },
        { tur: 'renk', re: /\[renk=(kirmizi|mavi|turuncu|mor|gri|sari)\]([\s\S]+?)\[\/renk\]/ }
    ];

    function Gizli(p) {
        var a = useState(false), acik = a[0], setAcik = a[1];
        return h('span', {
            className: 'yr-gizli' + (acik ? ' acik' : ''),
            role: 'button', tabIndex: 0,
            'aria-label': acik ? 'Gizli metin' : 'Gizli metin, göstermek için etkinleştir',
            onClick: function () { setAcik(!acik); },
            onKeyDown: function (e) { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); setAcik(!acik); } }
        }, p.children);
    }

    function ayristir(s, derin, anahtar) {
        var out = [];
        var kalan = s;
        var n = 0;
        while (kalan) {
            var en = null;
            if (derin < 4) {
                for (var i = 0; i < KALIPLAR.length; i++) {
                    var m = KALIPLAR[i].re.exec(kalan);
                    if (m && (!en || m.index < en.m.index)) en = { tur: KALIPLAR[i].tur, m: m };
                }
            }
            if (!en) { out.push(kalan); break; }
            if (en.m.index > 0) out.push(kalan.slice(0, en.m.index));
            var key = anahtar + '-' + (n++);
            var ic = en.tur === 'renk' ? en.m[2] : en.m[1];
            var cocuk = ayristir(ic, derin + 1, key);
            if (en.tur === 'kalin') out.push(h('strong', { key: key }, cocuk));
            else if (en.tur === 'egik') out.push(h('em', { key: key }, cocuk));
            else if (en.tur === 'gizli') out.push(h(Gizli, { key: key }, cocuk));
            else out.push(h('span', { key: key, className: 'yr-r-' + en.m[1] }, cocuk));
            kalan = kalan.slice(en.m.index + en.m[0].length);
        }
        return out;
    }

    function MetinGoster(p) {
        return h('p', { className: 'yr-metin' }, ayristir(htmlCoz(p.metin), 0, 'm'));
    }

    // ---- API ----
    function mesajAl(veri, durum, varsayilan) {
        var m = veri && (veri.message || veri.mesaj);
        if (m) return m;
        if (durum === 429) return 'Çok hızlı işlem yapıyorsun, biraz bekle.';
        return varsayilan;
    }

    // Dönüş: { ok, durum, veri, toplam }
    function istek(yol, yontem, token, govde) {
        var basliklar = {};
        if (govde !== undefined) basliklar['Content-Type'] = 'application/json';
        if (token) basliklar.Authorization = 'Bearer ' + token;
        return fetch(yol, {
            method: yontem, headers: basliklar,
            body: govde !== undefined ? JSON.stringify(govde) : undefined
        }).then(function (r) {
            var t = r.headers.get('X-Total-Count');
            return r.text().then(function (x) {
                var v = null;
                try { v = x ? JSON.parse(x) : null; } catch (e) { v = null; }
                return { ok: r.ok, durum: r.status, veri: v, toplam: t != null && t !== '' ? parseInt(t, 10) : null };
            });
        });
    }

    // ---- Yazma formu (yeni / yanıt / düzenle) ----
    function Form(p) {
        var a = useState(p.metinIlk || ''), metin = a[0], setMetin = a[1];
        var b = useState(!!p.spoilerIlk), spoiler = b[0], setSpoiler = b[1];
        var c = useState(false), bekle = c[0], setBekle = c[1];
        var d = useState(''), hata = d[0], setHata = d[1];
        var ref = useRef(null);
        var sinir = p.sinir;
        var asti = metin.length > sinir;

        useEffect(function () { if (p.odak && ref.current) ref.current.focus(); }, []);

        function sar(on, son) {
            var ta = ref.current;
            if (!ta) return;
            var x = ta.selectionStart, y = ta.selectionEnd;
            var secili = metin.slice(x, y) || 'metin';
            setMetin(metin.slice(0, x) + on + secili + son + metin.slice(y));
            requestAnimationFrame(function () {
                ta.focus();
                ta.setSelectionRange(x + on.length, x + on.length + secili.length);
            });
        }

        function gonder(e) {
            e.preventDefault();
            var t = metin.trim();
            if (!t || asti || bekle) return;
            setBekle(true); setHata('');
            Promise.resolve(p.onGonder({ metin: t, spoiler: spoiler })).then(function (sonuc) {
                if (sonuc) { setHata(sonuc); setBekle(false); }
                else if (!p.kapaninca) { setMetin(''); setSpoiler(false); setBekle(false); }
            }).catch(function () { setHata('Sunucuya ulaşılamadı.'); setBekle(false); });
        }

        var alanId = 'yr-alan-' + p.anahtar;
        return h('form', { className: 'yr-form' + (p.kompakt ? ' yr-form-kompakt' : ''), onSubmit: gonder },
            h('div', { className: 'yr-arac', role: 'toolbar', 'aria-label': 'Biçimlendirme' },
                h('button', { type: 'button', className: 'yr-arac-btn yr-b', title: 'Kalın', 'aria-label': 'Kalın', onClick: function () { sar('**', '**'); } }, 'K'),
                h('button', { type: 'button', className: 'yr-arac-btn yr-i', title: 'Eğik', 'aria-label': 'Eğik', onClick: function () { sar('*', '*'); } }, 'E'),
                h('button', { type: 'button', className: 'yr-arac-btn', title: 'Gizli metin', 'aria-label': 'Gizli metin', onClick: function () { sar('||', '||'); } }, 'Gizli'),
                h('select', {
                    className: 'yr-renk-sec', 'aria-label': 'Yazı rengi', value: '',
                    onChange: function (e) { var r = e.target.value; if (r) sar('[renk=' + r + ']', '[/renk]'); }
                },
                    h('option', { value: '' }, 'Renk'),
                    RENKLER.map(function (r) { return h('option', { key: r, value: r }, RENK_AD[r]); })
                )
            ),
            h('label', { className: 'yr-sr', htmlFor: alanId }, p.etiket || 'Yorum yaz'),
            h('textarea', {
                id: alanId, ref: ref, className: 'yr-alan', value: metin, rows: p.kompakt ? 3 : 4,
                placeholder: p.placeholder || '', disabled: bekle, 'aria-invalid': asti,
                onChange: function (e) { setMetin(e.target.value); }
            }),
            h('div', { className: 'yr-form-alt' },
                h('label', { className: 'yr-onay' },
                    h('input', { type: 'checkbox', checked: spoiler, disabled: bekle, onChange: function (e) { setSpoiler(e.target.checked); } }),
                    h('span', null, 'Spoiler içerir')),
                h('span', { className: 'yr-sayac' + (asti ? ' asti' : ''), 'aria-live': 'polite' }, metin.length + ' / ' + sinir)
            ),
            hata ? h('p', { className: 'yr-hata', role: 'alert' }, hata) : null,
            h('div', { className: 'yr-form-dugme' },
                p.iptal ? h('button', { type: 'button', className: 'yr-btn', onClick: p.iptal, disabled: bekle }, 'Vazgeç') : null,
                h('button', { type: 'submit', className: 'yr-btn yr-btn-ana', disabled: bekle || !metin.trim() || asti },
                    bekle ? 'Gönderiliyor...' : (p.gonderEtiket || 'Yorum yap'))
            )
        );
    }

    // ---- Tek yorum ----
    function Oge(p) {
        var y = p.y, AS = window.Kage95AdStili;
        var a = useState(false), acik = a[0], setAcik = a[1];
        var b = useState(''), neden = b[0], setNeden = b[1];
        var c = useState(''), eylemHata = c[0], setEylemHata = c[1];
        var d = useState(false), mesgul = d[0], setMesgul = d[1];
        var e = useState(''), bilgi = e[0], setBilgi = e[1];
        var eylem = p.eylem && p.eylem.id === y.id ? p.eylem.tip : null;
        var giris = p.giris;
        var gizli = y.spoiler && !acik;

        function kapat() { p.setEylem(null); setEylemHata(''); setNeden(''); }

        function sikayetGonder(ev) {
            ev.preventDefault();
            var n = neden.trim();
            if (!n || mesgul) return;
            setMesgul(true); setEylemHata('');
            p.islem('POST', '/yorumlar/' + p.tur + '/' + y.id + '/sikayet', { neden: n }).then(function (r) {
                setMesgul(false);
                if (r.ok) { setBilgi('Şikayetin alındı. Teşekkürler.'); kapat(); }
                else setEylemHata(mesajAl(r.veri, r.durum, 'Şikayet gönderilemedi.'));
            }).catch(function () { setMesgul(false); setEylemHata('Sunucuya ulaşılamadı.'); });
        }

        function silOnayla() {
            setMesgul(true); setEylemHata('');
            p.islem('DELETE', p.yorumYolu(y.id)).then(function (r) {
                setMesgul(false);
                if (r.ok) { kapat(); p.yenile(); }
                else setEylemHata(mesajAl(r.veri, r.durum, 'Yorum silinemedi.'));
            }).catch(function () { setMesgul(false); setEylemHata('Sunucuya ulaşılamadı.'); });
        }

        var ust = h('div', { className: 'yr-ust' },
            h('a', { href: 'index.html#/kullanici/' + encodeURIComponent(y.kullaniciAdi || ''), className: 'yr-yazar ' + AS.sinif(y.adStili), style: AS.stil(y.adStili) }, y.kullaniciAdi),
            AS.unvan(y.adStili) ? h('span', { className: 'as-unvan' }, AS.unvan(y.adStili)) : null,
            h('time', { className: 'yr-tarih', dateTime: y.tarih }, tarihYaz(y.tarih)),
            y.duzenlendi ? h('span', { className: 'yr-duzenlendi', title: 'Düzenleme: ' + tarihYaz(y.duzenlendi) }, '(düzenlendi)') : null,
            y.spoiler ? h('span', { className: 'yr-etiket' }, 'Spoiler') : null
        );

        var govde;
        if (eylem === 'duzenle') {
            govde = h(Form, {
                anahtar: 'd' + y.id, kompakt: true, odak: true, metinIlk: htmlCoz(y.metin), spoilerIlk: y.spoiler,
                sinir: p.sinir, etiket: 'Yorumu düzenle', gonderEtiket: 'Kaydet', kapaninca: true, iptal: kapat,
                onGonder: function (v) {
                    return p.islem('PUT', p.yorumYolu(y.id), { metin: v.metin, spoiler: v.spoiler }).then(function (r) {
                        if (r.ok) { kapat(); p.yenile(); return null; }
                        return mesajAl(r.veri, r.durum, 'Yorum güncellenemedi.');
                    });
                }
            });
        } else if (gizli) {
            govde = h('div', { className: 'yr-spoiler' },
                h('div', { className: 'yr-bulanik', 'aria-hidden': 'true' }, h(MetinGoster, { metin: y.metin })),
                h('button', { type: 'button', className: 'yr-spoiler-btn', onClick: function () { setAcik(true); } }, 'Spoiler içerir, göstermek için tıkla')
            );
        } else {
            govde = h(MetinGoster, { metin: y.metin });
        }

        var cubuk = eylem === 'duzenle' ? null : h('div', { className: 'yr-eylemler' },
            h('button', {
                type: 'button', className: 'yr-eylem yr-begeni' + (y.benBegendim ? ' aktif' : ''),
                'aria-pressed': !!y.benBegendim, disabled: !giris || y.benimYorumum || mesgul,
                title: !giris ? 'Beğenmek için giriş yap' : (y.benimYorumum ? 'Kendi yorumunu beğenemezsin' : ''),
                onClick: function () {
                    setMesgul(true); setEylemHata('');
                    p.islem('POST', '/yorumlar/' + p.tur + '/' + y.id + '/begeni').then(function (r) {
                        setMesgul(false);
                        if (r.ok && r.veri) p.guncelle(y.id, { benBegendim: !!r.veri.begendi, begeniSayisi: r.veri.begeniSayisi });
                        else setEylemHata(mesajAl(r.veri, r.durum, 'Beğeni kaydedilemedi.'));
                    }).catch(function () { setMesgul(false); setEylemHata('Sunucuya ulaşılamadı.'); });
                }
            }, (y.benBegendim ? 'Beğendin' : 'Beğen') + ' (' + (y.begeniSayisi || 0) + ')'),
            giris ? h('button', { type: 'button', className: 'yr-eylem', onClick: function () { p.setEylem({ tip: 'yanit', id: y.id }); } }, 'Yanıtla') : null,
            y.benimYorumum ? h('button', { type: 'button', className: 'yr-eylem', onClick: function () { p.setEylem({ tip: 'duzenle', id: y.id }); } }, 'Düzenle') : null,
            y.benimYorumum ? h('button', { type: 'button', className: 'yr-eylem', onClick: function () { p.setEylem({ tip: 'sil', id: y.id }); } }, 'Sil') : null,
            giris && !y.benimYorumum ? h('button', { type: 'button', className: 'yr-eylem', onClick: function () { p.setEylem({ tip: 'sikayet', id: y.id }); } }, 'Şikayet et') : null
        );

        var panel = null;
        if (eylem === 'sil') {
            panel = h('div', { className: 'yr-onay-kutu', role: 'alertdialog', 'aria-label': 'Silme onayı' },
                h('span', null, 'Bu yorum silinsin mi? Bu işlem geri alınamaz.'),
                h('div', { className: 'yr-form-dugme' },
                    h('button', { type: 'button', className: 'yr-btn', onClick: kapat, disabled: mesgul }, 'Vazgeç'),
                    h('button', { type: 'button', className: 'yr-btn yr-btn-tehlike', onClick: silOnayla, disabled: mesgul }, mesgul ? 'Siliniyor...' : 'Evet, sil')
                ));
        } else if (eylem === 'sikayet') {
            panel = h('form', { className: 'yr-sikayet', onSubmit: sikayetGonder },
                h('label', { className: 'yr-sr', htmlFor: 'yr-sk-' + y.id }, 'Şikayet nedeni'),
                h('input', {
                    id: 'yr-sk-' + y.id, type: 'text', className: 'yr-girdi', maxLength: 150, value: neden, autoFocus: true,
                    placeholder: 'Şikayet nedenini yaz (en fazla 150 karakter)', onChange: function (ev) { setNeden(ev.target.value); }
                }),
                h('div', { className: 'yr-form-dugme' },
                    h('button', { type: 'button', className: 'yr-btn', onClick: kapat, disabled: mesgul }, 'Vazgeç'),
                    h('button', { type: 'submit', className: 'yr-btn yr-btn-ana', disabled: mesgul || !neden.trim() }, mesgul ? 'Gönderiliyor...' : 'Şikayet et')
                ));
        } else if (eylem === 'yanit') {
            panel = h(Form, {
                anahtar: 'y' + y.id, kompakt: true, odak: true, sinir: p.sinir, etiket: 'Yanıt yaz',
                placeholder: (y.kullaniciAdi || '') + ' kullanıcısına yanıt yaz', gonderEtiket: 'Yanıtla', kapaninca: true, iptal: kapat,
                onGonder: function (v) {
                    return p.gonder({ metin: v.metin, spoiler: v.spoiler, yanitId: y.id }).then(function (hata) {
                        if (!hata) kapat();
                        return hata;
                    });
                }
            });
        }

        return h('li', { className: 'yr-oge' + (p.derinlik > 0 ? ' yr-yanit' : ''), id: 'yorum-' + y.id },
            h('span', { className: 'yr-avatar', 'aria-hidden': 'true' },
                y.profilResmiURL
                    ? h('img', { src: y.profilResmiURL, alt: '', loading: 'lazy', onError: function (ev) { ev.currentTarget.style.display = 'none'; } })
                    : String(y.kullaniciAdi || '?').charAt(0).toUpperCase()),
            h('div', { className: 'yr-govde' },
                ust, govde, cubuk,
                bilgi ? h('p', { className: 'yr-bilgi', role: 'status' }, bilgi) : null,
                eylemHata ? h('p', { className: 'yr-hata', role: 'alert' }, eylemHata) : null,
                panel,
                p.cocuklar && p.cocuklar.length ? h('ul', { className: 'yr-liste yr-yanitlar' }, p.cocuklar) : null
            )
        );
    }

    // Düz listeden ağaç kurar; yetim yanıtlar kök sayılır.
    function agacKur(liste, sirala) {
        var harita = {}, kokler = [];
        liste.forEach(function (y) { harita[y.id] = { y: y, c: [] }; });
        liste.forEach(function (y) {
            var dugum = harita[y.id];
            if (y.yanitId != null && harita[y.yanitId] && y.yanitId !== y.id) harita[y.yanitId].c.push(dugum);
            else kokler.push(dugum);
        });
        function eskiden(a, b) { return new Date(a.y.tarih) - new Date(b.y.tarih); }
        function dolas(d) { d.c.sort(eskiden); d.c.forEach(dolas); }
        kokler.forEach(dolas);
        return kokler;
    }

    function Yorumlar(p) {
        var tur = p.tur === 'manga' ? 'manga' : 'bolum';
        var api = (p.api || '').replace(/\/$/, '');
        var sinir = p.sinir || 500;
        var tokenAl = p.tokenAl || function () { return null; };
        var giris = !!tokenAl();

        var s1 = useState([]), liste = s1[0], setListe = s1[1];
        var s2 = useState('yukleniyor'), durum = s2[0], setDurum = s2[1];
        var s3 = useState(1), sayfa = s3[0], setSayfa = s3[1];
        var s4 = useState('yeni'), sirala = s4[0], setSirala = s4[1];
        var s5 = useState(null), toplam = s5[0], setToplam = s5[1];
        var s6 = useState(0), dene = s6[0], setDene = s6[1];
        var s7 = useState(null), eylem = s7[0], setEylem = s7[1];
        var s8 = useState(false), sessiz = s8[0], setSessiz = s8[1];
        var kutuRef = useRef(null);

        var listeYolu = tur === 'bolum' ? '/chapters/' + p.id + '/comments' : '/mangas/' + p.id + '/yorumlar';
        function yorumYolu(yid) { return tur === 'bolum' ? '/chapters/comments/' + yid : '/mangas/yorumlar/' + yid; }

        function islem(yontem, yol, govde) { return istek(api + yol, yontem, tokenAl(), govde); }

        useEffect(function () {
            if (p.id == null) { setListe([]); setDurum('bos'); return; }
            var iptal = false;
            if (!sessiz) setDurum('yukleniyor');
            islem('GET', listeYolu + '?sirala=' + sirala + '&page=' + sayfa + '&pageSize=' + SAYFA).then(function (r) {
                if (iptal) return;
                if (!r.ok) { setDurum('hata'); setSessiz(false); return; }
                var v = r.veri;
                var l = Array.isArray(v) ? v : ((v && (v.items || v.yorumlar)) || []);
                setListe(l);
                setToplam(r.toplam != null ? r.toplam : (sayfa === 1 && l.length < SAYFA ? l.length : null));
                setDurum('hazir'); setSessiz(false);
            }).catch(function () { if (!iptal) { setDurum('hata'); setSessiz(false); } });
            return function () { iptal = true; };
        }, [p.id, tur, sirala, sayfa, dene]);

        // Başka bölüme/seriye geçilince başa dön.
        useEffect(function () { setSayfa(1); setEylem(null); }, [p.id, tur]);

        function yenile() { setSessiz(true); setDene(function (n) { return n + 1; }); }

        function guncelle(id, parca) {
            setListe(function (l) { return l.map(function (y) { return y.id === id ? Object.assign({}, y, parca) : y; }); });
        }

        // Yeni yorum / yanıt. Dönüş: hata metni ya da null.
        function gonder(v) {
            var govde = { metin: v.metin, spoiler: !!v.spoiler };
            if (v.yanitId != null) govde.yanitId = v.yanitId;
            if (!tokenAl()) return Promise.resolve('GIRIS');
            return islem('POST', listeYolu, govde).then(function (r) {
                if (r.status === 401 || r.durum === 401) return 'GIRIS';
                if (!r.ok) return mesajAl(r.veri, r.durum, 'Yorum gönderilemedi.');
                if (r.veri && window.Kage95Liderlik && window.Kage95Liderlik.yanittan) window.Kage95Liderlik.yanittan(r.veri);
                if (v.yanitId == null && (sayfa !== 1 || sirala !== 'yeni')) { setSirala('yeni'); setSayfa(1); }
                else yenile();
                return null;
            });
        }

        var kokler = agacKur(liste, sirala);
        function ciz(d, derinlik) {
            var cocuklar = d.c.map(function (k) { return ciz(k, derinlik + 1); });
            return h(Oge, {
                key: d.y.id, y: d.y, derinlik: derinlik, cocuklar: cocuklar, tur: tur, sinir: sinir, giris: giris,
                eylem: eylem, setEylem: setEylem, islem: islem, yorumYolu: yorumYolu, yenile: yenile, guncelle: guncelle, gonder: gonder
            });
        }

        var sayfaSayisi = toplam != null ? Math.max(1, Math.ceil(toplam / SAYFA)) : (liste.length >= SAYFA ? sayfa + 1 : sayfa);
        function sayfaGit(n) {
            setSayfa(n);
            if (kutuRef.current && kutuRef.current.scrollIntoView) kutuRef.current.scrollIntoView({ block: 'start' });
        }

        var ust = h('div', { className: 'yr-baslik' },
            h('h2', null, 'Yorumlar'),
            h('div', { className: 'yr-baslik-sag' },
                durum === 'hazir' && toplam != null ? h('span', { className: 'yr-toplam' }, toplam + ' yorum') : null,
                h('div', { className: 'yr-siralama', role: 'group', 'aria-label': 'Sıralama' },
                    h('button', { type: 'button', className: 'yr-sira-btn', 'aria-pressed': sirala === 'yeni', onClick: function () { setSirala('yeni'); setSayfa(1); } }, 'En yeni'),
                    h('button', { type: 'button', className: 'yr-sira-btn', 'aria-pressed': sirala === 'begeni', onClick: function () { setSirala('begeni'); setSayfa(1); } }, 'En çok beğenilen')
                )
            )
        );

        var yeniForm = giris
            ? h(Form, {
                anahtar: 'yeni-' + tur, sinir: sinir, placeholder: p.placeholder || 'Ne düşünüyorsun? (Spoiler uyarısı yapmayı unutma)',
                onGonder: function (v) { return gonder(v).then(function (hata) { return hata === 'GIRIS' ? 'Oturumun sona ermiş. Tekrar giriş yap.' : hata; }); }
            })
            : h('div', { className: 'yr-giris' },
                h('span', null, 'Yorum yazmak için giriş yapmalısın.'),
                h('a', { className: 'yr-btn', href: 'login.html' }, 'Giriş yap'));

        var sayfalama = durum === 'hazir' && sayfaSayisi > 1
            ? h('nav', { className: 'yr-sayfalama', 'aria-label': 'Yorum sayfaları' },
                h('button', { type: 'button', className: 'yr-btn', disabled: sayfa <= 1, onClick: function () { sayfaGit(sayfa - 1); } }, 'Önceki'),
                h('span', { className: 'yr-sayfa-bilgi', 'aria-current': 'page' }, 'Sayfa ' + sayfa + (toplam != null ? ' / ' + sayfaSayisi : '')),
                h('button', { type: 'button', className: 'yr-btn', disabled: toplam != null ? sayfa >= sayfaSayisi : liste.length < SAYFA, onClick: function () { sayfaGit(sayfa + 1); } }, 'Sonraki'))
            : null;

        return h('section', { className: 'yr-kutu', 'aria-label': 'Yorumlar', ref: kutuRef },
            ust,
            yeniForm,
            durum === 'yukleniyor' ? h('p', { className: 'yr-durum', role: 'status' }, 'Yorumlar yükleniyor...') : null,
            durum === 'bos' ? h('p', { className: 'yr-durum' }, 'Yorumlar bir bölüm açıldığında görünür.') : null,
            durum === 'hata' ? h('div', { className: 'yr-durum', role: 'alert' },
                h('span', null, 'Yorumlar yüklenemedi.'),
                h('button', { type: 'button', className: 'yr-btn', onClick: function () { setDene(dene + 1); } }, 'Tekrar dene')) : null,
            durum === 'hazir' && liste.length === 0 ? h('p', { className: 'yr-durum' }, 'Henüz yorum yok. İlk yorumu sen yaz.') : null,
            durum === 'hazir' && liste.length > 0 ? h('ul', { className: 'yr-liste' }, kokler.map(function (d) { return ciz(d, 0); })) : null,
            sayfalama
        );
    }

    window.Kage95Yorum = { Yorumlar: Yorumlar, ayristir: ayristir, htmlCoz: htmlCoz };
})();
