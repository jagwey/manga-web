// Kage 95 - Sohbet (bağımsız vanilla JS; SignalR istemcisi window.signalR olarak yüklenmiş olmalı).
// Kullanım:
//   var h = Kage95Sohbet.serit(el, { apiUrl, token: function(){...}, kullanici: {...}|null, sohbetAdresi: '#/sohbet' });
//   var h = Kage95Sohbet.sayfa(el, { apiUrl, token, kullanici });
//   h.yoksay();   // olay dinleyicilerini bırakır, bağlantıyı gerekirse kapatır
// Sunucu sözleşmesi: PROGRESS.md "Backend v2 sözleşmesi" > Sohbet. Mesaj metni her zaman textContent ile basılır.
(function () {
    'use strict';
    if (window.Kage95Sohbet) return;

    var SVGNS = 'http://www.w3.org/2000/svg';
    var MAKS_UZUNLUK = 300;
    var SERIT_ADET = 6;
    var BELLEK_SINIRI = 300;
    var FONTLAR = {
        serif: "Georgia, 'Times New Roman', Times, serif",
        mono: "'Courier New', Consolas, Menlo, monospace",
        elyazisi: "'Segoe Print', 'Bradley Hand', 'Comic Sans MS', cursive"
    };
    // Sunucudaki OdulKatalogu ile aynı: paket kimliği, ad, gereken seviye, sticker (kimlik, ad).
    var PAKETLER = [
        { id: 'sade', ad: 'Sade', seviye: 8, liste: [['ok', 'Ok'], ['kalp', 'Kalp'], ['yildiz', 'Yıldız'], ['tebrik', 'Tebrik'], ['gulumse', 'Gülümse'], ['uzgun', 'Üzgün'],
            ['saskin', 'Şaşkın'], ['kilic', 'Kılıç'], ['kitap', 'Kitap'], ['cay', 'Çay'], ['uyku', 'Uyku'], ['bravo', 'Bravo']] },
        { id: 'golge', ad: 'Gölge', seviye: 15, liste: [['golge', 'Gölge'], ['maske', 'Maske'], ['ay', 'Ay'], ['kaplan', 'Kaplan'], ['shuriken', 'Shuriken'], ['fener', 'Fener'],
            ['sakura', 'Sakura'], ['ejderha', 'Ejderha'], ['daga', 'Hançer'], ['tilsim', 'Tılsım'], ['bulut', 'Bulut'], ['ates', 'Ateş']] }
    ];

    // Sticker çizimleri: 24x24 çizgi ikonları (currentColor), emoji yok.
    var CIZIM = {
        'sade:ok': 'M4 12h15M13 6l6 6-6 6',
        'sade:kalp': 'M12 20s-7-4.4-7-10a4 4 0 0 1 7-2.4A4 4 0 0 1 19 10c0 5.6-7 10-7 10z',
        'sade:yildiz': 'M12 3l2.7 5.7 6.3.8-4.6 4.3 1.2 6.2L12 17l-5.6 3 1.2-6.2L3 9.5l6.3-.8z',
        'sade:tebrik': 'M5 20l4-12 7 7-11 5zM13 5l1 2M17 7l2-1M18 12l2 1',
        'sade:gulumse': 'M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18zM8.5 14.5q3.5 3 7 0M9 9.5v1M15 9.5v1',
        'sade:uzgun': 'M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18zM8.5 16q3.5-3 7 0M9 9.5v1M15 9.5v1',
        'sade:saskin': 'M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18zM12 13.5a1.5 2 0 1 0 0 .01M9 9v1.5M15 9v1.5',
        'sade:kilic': 'M5 19l3-3M7 17l-2-2M8 16L19 5v4l-8 8-3-1z',
        'sade:kitap': 'M5 4h11a3 3 0 0 1 3 3v13H8a3 3 0 0 1-3-3V4zM5 17a3 3 0 0 1 3-3h11',
        'sade:cay': 'M5 10h11v4a5 5 0 0 1-5 5h-1a5 5 0 0 1-5-5v-4zM16 11h2a2 2 0 0 1 0 4h-2M9 3v3M12 3v3',
        'sade:uyku': 'M5 6h6l-6 6h6M13 12h5l-5 5h5',
        'sade:bravo': 'M8 11v9H4v-9zM8 12l4-8a2 2 0 0 1 2 2v4h5a1.5 1.5 0 0 1 1.5 1.8l-1.3 6A2 2 0 0 1 17.3 19H8',
        'golge:golge': 'M12 3c-4 0-6 3-6 7v10l3-2 3 2 3-2 3 2V10c0-4-2-7-6-7zM9.5 10v2M14.5 10v2',
        'golge:maske': 'M4 6c5-2 11-2 16 0v5c0 5-3 8-8 10-5-2-8-5-8-10V6zM8 10l2 1M16 10l-2 1M9 15q3 2 6 0',
        'golge:ay': 'M17 15A7 7 0 0 1 9 4a8 8 0 1 0 8 11z',
        'golge:kaplan': 'M4 5l4 2h8l4-2v8c0 4-3 7-8 7s-8-3-8-7V5zM8 11l2 1M16 11l-2 1M10.5 15h3l-1.5 2z',
        'golge:shuriken': 'M12 2l2.5 7.5L22 12l-7.5 2.5L12 22l-2.5-7.5L2 12l7.5-2.5zM12 10.5a1.5 1.5 0 1 0 0 3 1.5 1.5 0 0 0 0-3z',
        'golge:fener': 'M9 3h6M8 5h8c2 2 2 12 0 14H8C6 17 6 7 8 5zM9 19v2h6v-2M12 8v8M9 12h6',
        'golge:sakura': 'M12 12c-2-2-2-6 0-8 2 2 2 6 0 8zM12 12c2-2 6-2 8 0-2 2-6 2-8 0zM12 12c2 2 2 6 0 8-2-2-2-6 0-8zM12 12c-2 2-6 2-8 0 2-2 6-2 8 0z',
        'golge:ejderha': 'M3 15c3-6 6 3 9-3s6 3 9-3M17 5l3 1-1 3M4 18c3-2 5 1 8-1',
        'golge:daga': 'M12 2l3 4-1 9h-4L9 6zM8 15h8M12 15v6',
        'golge:tilsim': 'M8 3h8v18H8zM10 7h4M10 11h4M10 15h2',
        'golge:bulut': 'M7 18a4 4 0 0 1-.6-8A5.5 5.5 0 0 1 17 9a4.5 4.5 0 0 1 0 9H7z',
        'golge:ates': 'M12 3c1 4 5 5 5 10a5 5 0 0 1-10 0c0-2 1-3 2-4 0 2 1 3 2 3 0-3-1-5 1-9z'
    };

    var STIL = [
        '.ks-cerceve{max-width:100%;color:var(--text-ana,#ede7e2)}',
        '.ks-baslik{display:flex;align-items:center;justify-content:space-between;gap:12px;flex-wrap:wrap}',
        '.ks-cevrimici{display:inline-flex;align-items:center;gap:8px;font-size:.875rem;color:var(--text-ikincil,#a79d96)}',
        '.ks-nokta{width:8px;height:8px;border-radius:50%;background:var(--text-ikincil,#a79d96);flex-shrink:0}',
        '.ks-bagli .ks-nokta{background:var(--vurgu-parlak,#e0343c)}',
        '.ks-liste{list-style:none;margin:0;padding:0;display:flex;flex-direction:column;gap:2px}',
        '.ks-m{display:flex;align-items:baseline;flex-wrap:wrap;gap:2px 8px;padding:6px 8px;border-radius:2px;line-height:1.45;overflow-wrap:anywhere;word-break:break-word}',
        '.ks-m:nth-child(even){background:var(--bg-kart,#141111)}',
        '.ks-saat{font-size:.75rem;color:var(--text-ikincil,#a79d96);flex-shrink:0}',
        '.ks-ad{font-weight:600;text-decoration:none;color:inherit}',
        'a.ks-ad:hover{text-decoration:underline}',
        '.ks-sev{font-size:.6875rem;color:var(--text-ikincil,#a79d96);border:1px solid var(--border-renk,#2a2323);border-radius:2px;padding:0 4px}',
        '.ks-rozet{border:1px solid currentColor;border-radius:2px;padding:0 5px}',
        '.ks-unvan{font-size:.75rem;color:var(--text-ikincil,#a79d96);font-style:italic}',
        '.ks-parlak{text-shadow:0 0 6px currentColor}',
        '.ks-anim{animation:ks-parla 2.4s ease-in-out infinite}',
        '@keyframes ks-parla{0%,100%{text-shadow:0 0 3px currentColor}50%{text-shadow:0 0 11px currentColor}}',
        '.ks-metin{flex-basis:auto;min-width:0}',
        '.ks-st{display:inline-flex;vertical-align:middle;color:var(--vurgu-parlak,#e0343c)}',
        '.ks-st svg{width:40px;height:40px;fill:none;stroke:currentColor;stroke-width:1.5;stroke-linecap:round;stroke-linejoin:round}',
        '.ks-sil{margin-left:auto;background:none;border:0;padding:0 6px;min-height:28px;color:var(--text-ikincil,#a79d96);text-decoration:underline;text-underline-offset:3px;cursor:pointer;font-size:.8125rem}',
        '.ks-bos{padding:16px 8px;color:var(--text-ikincil,#a79d96);font-size:.9rem}',
        '.ks-serit{border:1px solid var(--border-renk,#2a2323);background:var(--bg-kart,#141111);border-radius:2px;padding:12px 14px}',
        '.ks-serit .ks-liste{margin:8px 0}',
        '.ks-serit .ks-m:nth-child(even){background:transparent}',
        '.ks-serit .ks-st svg{width:24px;height:24px}',
        '.ks-git{display:inline-flex;align-items:center;min-height:36px;color:var(--vurgu-parlak,#e0343c);text-decoration:underline;text-underline-offset:3px;font-size:.9rem}',
        '.ks-sayfa .ks-liste{max-height:min(60vh,520px);overflow-y:auto;border:1px solid var(--border-renk,#2a2323);border-radius:2px;padding:6px;margin:12px 0;overscroll-behavior:contain}',
        '.ks-eski{display:block;margin:0 auto 6px;background:none;border:1px solid var(--border-renk,#2a2323);color:var(--text-ikincil,#a79d96);padding:0 14px;min-height:36px;border-radius:2px;cursor:pointer;font-size:.875rem}',
        '.ks-form{display:flex;flex-direction:column;gap:8px}',
        '.ks-satir{display:flex;gap:8px;align-items:stretch}',
        '.ks-giris{flex:1;min-width:0;min-height:44px;padding:10px 12px;font:inherit;font-size:1rem;color:var(--text-ana,#ede7e2);background:var(--bg-kart,#141111);border:1px solid var(--border-belirgin,#3a3030);border-radius:2px}',
        '.ks-giris:focus-visible,.ks-btn:focus-visible,.ks-sil:focus-visible,.ks-eski:focus-visible,.ks-git:focus-visible,.ks-stbtn:focus-visible{outline:1px solid var(--vurgu-parlak,#e0343c);outline-offset:2px}',
        '.ks-btn{min-height:44px;padding:0 18px;font:inherit;font-weight:600;color:#fff;background:var(--vurgu-rengi,#8e0e15);border:1px solid var(--vurgu-rengi,#8e0e15);border-radius:2px;cursor:pointer}',
        '.ks-btn[disabled]{opacity:.5;cursor:not-allowed}',
        '.ks-ikincil{background:transparent;color:var(--text-ana,#ede7e2);border-color:var(--border-belirgin,#3a3030)}',
        '.ks-alt{display:flex;justify-content:space-between;gap:12px;font-size:.8125rem;color:var(--text-ikincil,#a79d96);min-height:18px}',
        '.ks-hata{color:var(--vurgu-parlak,#e0343c)}',
        '.ks-panel{border:1px solid var(--border-renk,#2a2323);background:var(--bg-kart,#141111);border-radius:2px;padding:10px}',
        '.ks-panel h4{margin:8px 0 6px;font-size:.875rem;font-weight:600}',
        '.ks-panel h4:first-child{margin-top:0}',
        '.ks-izgara{display:grid;grid-template-columns:repeat(auto-fill,minmax(52px,1fr));gap:6px}',
        '.ks-stbtn{display:flex;align-items:center;justify-content:center;min-height:52px;background:var(--bg-ana,#0e0c0c);border:1px solid var(--border-renk,#2a2323);border-radius:2px;color:var(--vurgu-parlak,#e0343c);cursor:pointer;padding:0}',
        '.ks-stbtn svg{width:30px;height:30px;fill:none;stroke:currentColor;stroke-width:1.5;stroke-linecap:round;stroke-linejoin:round}',
        '.ks-stbtn[disabled]{opacity:.35;cursor:not-allowed}',
        '.ks-kilit{font-size:.8125rem;color:var(--text-ikincil,#a79d96);margin:0 0 6px}',
        '.ks-not{padding:12px;border:1px solid var(--border-renk,#2a2323);border-radius:2px;color:var(--text-ikincil,#a79d96);font-size:.9rem}',
        '.ks-not a{color:var(--vurgu-parlak,#e0343c)}',
        '@media (hover:hover){.ks-sil:hover,.ks-eski:hover{color:var(--text-ana,#ede7e2)}.ks-stbtn:not([disabled]):hover{border-color:var(--vurgu-parlak,#e0343c)}}',
        '@media (max-width:768px){.ks-satir{flex-wrap:wrap}.ks-btn{flex:1}.ks-sil{min-height:44px}}',
        '@media (prefers-reduced-motion:reduce){.ks-anim{animation:none;text-shadow:0 0 6px currentColor}}',
        '.ks-m{display:flex;align-items:flex-start;flex-wrap:nowrap;gap:10px;padding:6px 4px;background:none!important;animation:ks-gir .25s ease-out}',
        '@keyframes ks-gir{from{opacity:0;transform:translateY(6px)}to{opacity:1;transform:none}}',
        '.ks-av{position:relative;flex:0 0 36px;width:36px;height:36px;border-radius:50%;overflow:hidden;display:flex;align-items:center;justify-content:center;font-weight:700;color:#fff;background:linear-gradient(135deg,var(--vurgu-rengi,#8e0e15),var(--vurgu-parlak,#e0343c));box-shadow:0 0 0 2px var(--border-belirgin,#3a3030)}',
        '.ks-av img{position:absolute;inset:0;width:100%;height:100%;object-fit:cover}',
        '.ks-govde{min-width:0;flex:1}',
        '.ks-ust{display:flex;align-items:center;flex-wrap:wrap;gap:2px 8px;margin-bottom:3px}',
        '.ks-ust .ks-saat{margin-left:auto}',
        '.ks-sev{background:linear-gradient(135deg,var(--vurgu-rengi,#8e0e15),var(--vurgu-parlak,#e0343c));color:#fff;border:0;border-radius:999px;padding:1px 8px;font-weight:700}',
        '.ks-balon{display:inline-block;max-width:100%;padding:8px 12px;border-radius:4px 14px 14px 14px;background:color-mix(in srgb,var(--text-ana,#ede7e2) 9%,var(--bg-kart,#141111));border:1px solid var(--border-renk,#2a2323);overflow-wrap:anywhere;word-break:break-word}',
        '.ks-balon-st{background:none;border:0;padding:0}',
        '.ks-st svg,.ks-serit .ks-st svg{width:56px;height:56px}',
        '.ks-serit{position:relative;overflow:hidden;border-radius:16px;padding:14px 16px;background:linear-gradient(160deg,color-mix(in srgb,var(--vurgu-rengi,#8e0e15) 22%,var(--bg-kart,#141111)),var(--bg-kart,#141111) 55%);box-shadow:0 10px 30px -14px var(--vurgu-rengi,#8e0e15)}',
        '.ks-serit .ks-liste{max-height:320px;overflow-y:auto;overscroll-behavior:contain;margin:10px 0;gap:4px;scrollbar-width:thin}',
        '.ks-kapi{display:flex;align-items:center;justify-content:space-between;gap:12px;flex-wrap:wrap;padding:10px 14px;border-radius:10px;background:color-mix(in srgb,var(--text-ana,#ede7e2) 7%,transparent);font-size:.9rem}',
        '.ks-kapi a{color:var(--vurgu-parlak,#e0343c);font-weight:600;text-underline-offset:3px}',
        '.ks-btn,.ks-giris{border-radius:999px}',
        '.ks-giris{padding-left:16px}',
        '.ks-sayfa .ks-liste{border-radius:12px}',
        '.ks-sabit{display:flex;align-items:center;justify-content:space-between;gap:10px;padding:8px 12px;margin:8px 0 4px;border-left:3px solid var(--vurgu-parlak,#e0343c);background:color-mix(in srgb,var(--vurgu-rengi,#8e0e15) 18%,var(--bg-kart,#141111));border-radius:4px;font-size:.875rem}',
        '.ks-sabit-baslik{font-weight:700;color:var(--vurgu-parlak,#e0343c);margin-right:6px;font-size:.75rem;text-transform:uppercase;letter-spacing:.04em}',
        '.ks-etiket{color:var(--vurgu-parlak,#e0343c);font-weight:600;text-decoration:none}',
        '.ks-etiket:hover{text-decoration:underline}',
        '.ks-bahset{background:none;border:0;padding:0 4px;color:var(--text-ikincil,#a79d96);cursor:pointer;font-size:.75rem}',
        '.ks-bahset:hover{color:var(--vurgu-parlak,#e0343c)}',
        '@media (prefers-reduced-motion:reduce){.ks-m{animation:none}}'
    ].join('\n');

    function stilEkle() {
        if (document.getElementById('kage95-sohbet-stili')) return;
        var s = document.createElement('style');
        s.id = 'kage95-sohbet-stili';
        s.textContent = STIL;
        (document.head || document.documentElement).appendChild(s);
    }

    function el(ad, sinif, metin) {
        var e = document.createElement(ad);
        if (sinif) e.className = sinif;
        if (metin != null) e.textContent = metin;
        return e;
    }

    function stickerSvg(id) {
        var d = CIZIM[id];
        if (!d) return null;
        var svg = document.createElementNS(SVGNS, 'svg');
        svg.setAttribute('viewBox', '0 0 24 24');
        svg.setAttribute('aria-hidden', 'true');
        var p = document.createElementNS(SVGNS, 'path');
        p.setAttribute('d', d);
        svg.appendChild(p);
        return svg;
    }

    function stickerAdi(id) {
        for (var i = 0; i < PAKETLER.length; i++) {
            for (var j = 0; j < PAKETLER[i].liste.length; j++) {
                if (PAKETLER[i].id + ':' + PAKETLER[i].liste[j][0] === id) return PAKETLER[i].liste[j][1];
            }
        }
        return 'Sticker';
    }

    function saatYaz(t) {
        var s = String(t || '');
        if (!/([zZ]|[+-]\d\d:?\d\d)$/.test(s)) s += 'Z';
        var d = new Date(s);
        if (isNaN(d)) return '';
        var bugun = new Date();
        var saat = d.toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' });
        return d.toDateString() === bugun.toDateString() ? saat : d.toLocaleDateString('tr-TR', { day: '2-digit', month: '2-digit' }) + ' ' + saat;
    }

    function hataMetni(e, yedek) {
        var m = e && e.message ? String(e.message) : '';
        var i = m.indexOf('HubException:');
        if (i >= 0) m = m.slice(i + 13).trim();
        // "An unexpected error occurred invoking ..." gibi teknik metinler kullanıcıya gösterilmez.
        if (!m || /invoking|Connection|WebSocket|negotiat|Failed to|Error:/i.test(m)) return yedek;
        return m;
    }

    /* ------------------------- Ortak bağlantı ------------------------- */
    var durum = {
        mesajlar: [],        // eskiden yeniye
        cevrimici: null,
        acik: true,
        sabitMesaj: '',
        bagli: false,
        hazir: false,
        dinleyen: [],        // { degisti: fn }
        baglanti: null,
        kullanim: 0,
        durdurZaman: null,
        apiUrl: '',
        tokenFn: function () { return null; },
        anonim: false,
        baslatiliyor: false
    };

    function haber(tur, veri) {
        durum.dinleyen.slice().forEach(function (d) { try { d(tur, veri); } catch (e) {} });
    }

    function hubAdresi() { return durum.apiUrl.replace(/\/api\/?$/, '') + '/hub/sohbet'; }

    function birlestir(yeni) {
        var var_ = {};
        durum.mesajlar.forEach(function (m) { var_[m.id] = true; });
        var eklenen = false;
        yeni.forEach(function (m) {
            if (!var_[m.id]) { durum.mesajlar.push(m); var_[m.id] = true; eklenen = true; }
        });
        if (eklenen) {
            durum.mesajlar.sort(function (a, b) { return a.id - b.id; });
            if (durum.mesajlar.length > BELLEK_SINIRI) durum.mesajlar = durum.mesajlar.slice(durum.mesajlar.length - BELLEK_SINIRI);
        }
        return eklenen;
    }

    function getir(yol) {
        return fetch(durum.apiUrl + yol).then(function (r) { if (!r.ok) throw new Error('http'); return r.json(); });
    }

    function gecmisiYukle() {
        return getir('/sohbet/mesajlar?limit=50').then(function (liste) {
            if (Array.isArray(liste)) birlestir(liste);
            durum.hazir = true;
            haber('liste');
        }).catch(function () { durum.hazir = true; haber('hata'); });
    }

    function durumuYukle() {
        return getir('/sohbet/durum').then(function (v) {
            if (v) {
                durum.acik = v.acik !== false;
                if (typeof v.cevrimici === 'number') durum.cevrimici = v.cevrimici;
                if (typeof v.sabitMesaj === 'string') durum.sabitMesaj = v.sabitMesaj;
            }
            haber('durum');
        }).catch(function () {});
    }

    function baglantiKur(anonim) {
        var sr = window.signalR;
        if (!sr) return null;
        return new sr.HubConnectionBuilder()
            .withUrl(hubAdresi(), {
                accessTokenFactory: function () { return anonim ? '' : (durum.tokenFn() || ''); },
                withCredentials: false
            })
            .withAutomaticReconnect([0, 2000, 5000, 10000, 20000])
            .configureLogging(sr.LogLevel.None)
            .build();
    }

    function olaylariBagla(b) {
        b.on('MesajGeldi', function (m) {
            if (birlestir([m])) haber('mesaj', m);
        });
        b.on('MesajSilindi', function (v) {
            var n = durum.mesajlar.length;
            durum.mesajlar = durum.mesajlar.filter(function (m) { return m.id !== v.id; });
            if (durum.mesajlar.length !== n) haber('silindi', v.id);
        });
        b.on('Cevrimici', function (v) { durum.cevrimici = v.sayi; haber('durum'); });
        b.on('SabitDegisti', function (v) { durum.sabitMesaj = (v && typeof v.metin === 'string') ? v.metin : ''; haber('durum'); });
        b.onreconnecting(function () { durum.bagli = false; haber('durum'); });
        b.onreconnected(function () { durum.bagli = true; haber('durum'); gecmisiYukle(); durumuYukle(); });
        b.onclose(function () { durum.bagli = false; haber('durum'); });
    }

    function baslat(anonim) {
        var b = baglantiKur(anonim);
        if (!b) return Promise.reject(new Error('signalr'));
        olaylariBagla(b);
        durum.baglanti = b;
        durum.anonim = !!anonim;
        return b.start().then(function () { durum.bagli = true; haber('durum'); });
    }

    function baglan() {
        if (durum.baglanti || durum.baslatiliyor) return;
        durum.baslatiliyor = true;
        var tokenVar = !!durum.tokenFn();
        baslat(!tokenVar).catch(function () {
            // Bozuk ya da süresi dolmuş token bağlantıyı düşürebilir; anonim (salt okur) dene.
            durum.baglanti = null;
            return baslat(true);
        }).catch(function () {
            durum.baglanti = null;
            durum.bagli = false;
            haber('durum');
        }).then(function () { durum.baslatiliyor = false; });
    }

    function yenidenBaglan() {
        var eski = durum.baglanti;
        durum.baglanti = null;
        durum.bagli = false;
        var dur = eski ? eski.stop().catch(function () {}) : Promise.resolve();
        return dur.then(function () {
            durum.baslatiliyor = true;
            return baslat(!durum.tokenFn()).catch(function () { durum.baglanti = null; return baslat(true); });
        }).catch(function () { durum.baglanti = null; }).then(function () { durum.baslatiliyor = false; });
    }

    function kullanimAc(secenek) {
        stilEkle();
        durum.apiUrl = String(secenek.apiUrl || '').replace(/\/$/, '');
        durum.tokenFn = secenek.token || durum.tokenFn;
        clearTimeout(durum.durdurZaman);
        durum.kullanim++;
        if (durum.kullanim === 1 || !durum.hazir) { gecmisiYukle(); durumuYukle(); }
        baglan();
    }

    function kullanimKapat() {
        durum.kullanim = Math.max(0, durum.kullanim - 1);
        if (durum.kullanim === 0) {
            clearTimeout(durum.durdurZaman);
            durum.durdurZaman = setTimeout(function () {
                if (durum.kullanim === 0 && durum.baglanti) {
                    var b = durum.baglanti;
                    durum.baglanti = null;
                    durum.bagli = false;
                    b.stop().catch(function () {});
                }
            }, 3000);
        }
    }

    function dinle(fn) {
        durum.dinleyen.push(fn);
        return function () { durum.dinleyen = durum.dinleyen.filter(function (x) { return x !== fn; }); };
    }

    function gonder(metin, stickerId) {
        var b = durum.baglanti;
        if (!b || !durum.bagli) return Promise.reject(new Error('Bağlantı kurulamadı. Birazdan tekrar dene.'));
        var dene = function () { return durum.baglanti.invoke('GonderAsync', metin, stickerId || null); };
        return dene().catch(function (e) {
            var m = e && e.message ? String(e.message) : '';
            // Anonim bağlanmış ya da token'ı eskimiş bağlantı: token'ı tazeleyip bir kez daha dene.
            if (/authoriz|yetki|401|giriş yapmalısınız/i.test(m) && durum.tokenFn()) {
                return yenidenBaglan().then(function () {
                    if (!durum.baglanti || !durum.bagli) throw new Error('Bağlantı kurulamadı. Birazdan tekrar dene.');
                    return dene();
                });
            }
            throw e;
        });
    }

    /* ------------------------- Mesaj çizimi ------------------------- */
    function metinDoldur(hedef, metin) {
        var s = String(metin || '');
        var re = /@([\p{L}\p{Nd}_.\-]{3,30})/gu;
        var sonIndeks = 0;
        var eslesme;
        while ((eslesme = re.exec(s)) !== null) {
            if (eslesme.index > sonIndeks) {
                hedef.appendChild(document.createTextNode(s.slice(sonIndeks, eslesme.index)));
            }
            var kullaniciAdi = eslesme[1];
            var link = el('a', 'ks-etiket', '@' + kullaniciAdi);
            link.href = '#/kullanici/' + encodeURIComponent(kullaniciAdi);
            hedef.appendChild(link);
            sonIndeks = re.lastIndex;
        }
        if (sonIndeks < s.length) {
            hedef.appendChild(document.createTextNode(s.slice(sonIndeks)));
        }
    }

    function mesajEl(m, secenek) {
        var li = el('li', 'ks-m');
        li.setAttribute('data-id', m.id);
        var st = m.adStili || {};

        var av = el('span', 'ks-av');
        av.setAttribute('aria-hidden', 'true');
        av.textContent = String(m.kullaniciAdi || '?').charAt(0).toUpperCase();
        if (m.profilResmiURL) {
            var img = document.createElement('img');
            img.alt = '';
            img.loading = 'lazy';
            img.src = m.profilResmiURL;
            img.addEventListener('error', function () { if (img.parentNode) img.parentNode.removeChild(img); });
            av.appendChild(img);
        }
        li.appendChild(av);

        var govde = el('div', 'ks-govde');
        var ust = el('div', 'ks-ust');
        var ad = el('a', 'ks-ad', m.kullaniciAdi);
        ad.href = '#/kullanici/' + encodeURIComponent(m.kullaniciAdi || '');
        if (st.adRengiHex) ad.style.color = st.adRengiHex;
        if (st.parlak) ad.classList.add('ks-parlak');
        if (st.parlak && st.animasyon) ad.classList.add('ks-anim');
        if (st.rozet) ad.classList.add('ks-rozet');
        ust.appendChild(ad);
        var sev = el('span', 'ks-sev', 'Lv.' + m.seviye);
        sev.title = m.rutbe || '';
        ust.appendChild(sev);
        if (st.unvan) ust.appendChild(el('span', 'ks-unvan', st.unvan));
        if (secenek.bahset) {
            var bh = el('button', 'ks-bahset', '@');
            bh.type = 'button';
            bh.title = m.kullaniciAdi + ' kullanıcısını etiketle';
            bh.setAttribute('aria-label', m.kullaniciAdi + ' kullanıcısını etiketle');
            bh.addEventListener('click', function () { secenek.bahset(m.kullaniciAdi); });
            ust.appendChild(bh);
        }
        ust.appendChild(el('span', 'ks-saat', saatYaz(m.tarih)));
        if (secenek.sabitle && m.metin) {
            var sb = el('button', 'ks-sil', 'Sabitle');
            sb.type = 'button';
            sb.style.marginLeft = '0';
            sb.setAttribute('aria-label', 'Bu mesajı sabitle');
            sb.addEventListener('click', function () { secenek.sabitle(m, sb); });
            ust.appendChild(sb);
        }
        if (secenek.sil) {
            var b = el('button', 'ks-sil', 'Sil');
            b.type = 'button';
            if (secenek.sabitle && m.metin) b.style.marginLeft = '0';
            b.setAttribute('aria-label', m.kullaniciAdi + ' kullanıcısının mesajını sil');
            b.addEventListener('click', function () { secenek.sil(m, b); });
            ust.appendChild(b);
        }
        govde.appendChild(ust);

        var balon = el('div', 'ks-balon' + (m.stickerId && !m.metin ? ' ks-balon-st' : ''));
        if (m.metin) {
            var t = el('span', 'ks-metin');
            metinDoldur(t, m.metin);
            if (st.mesajFontu && FONTLAR[st.mesajFontu]) t.style.fontFamily = FONTLAR[st.mesajFontu];
            balon.appendChild(t);
        }
        if (m.stickerId) {
            var sv = stickerSvg(m.stickerId);
            if (sv) {
                var k = el('span', 'ks-st');
                k.setAttribute('role', 'img');
                k.setAttribute('aria-label', stickerAdi(m.stickerId));
                k.appendChild(sv);
                balon.appendChild(k);
            }
        }
        govde.appendChild(balon);
        li.appendChild(govde);
        return li;
    }

    function durumYazisi(kap, cevrimiciEl) {
        var n = durum.cevrimici;
        cevrimiciEl.textContent = (n == null ? '' : n + ' kişi çevrimiçi') + (durum.bagli ? '' : (n == null ? 'Bağlanıyor…' : ' · bağlanıyor…'));
        kap.classList.toggle('ks-bagli', !!durum.bagli);
    }

    /* ------------------------- Ana sayfa şeridi ------------------------- */
    function serit(kap, secenek) {
        secenek = secenek || {};
        kullanimAc(secenek);
        var girisli = !!(secenek.kullanici && durum.tokenFn());
        var kok = el('section', 'ks-cerceve ks-serit');
        kok.setAttribute('aria-label', 'Canlı sohbet');
        var ust = el('div', 'ks-baslik');
        var bas = el('h2', null, 'Canlı sohbet');
        bas.style.cssText = 'margin:0;font-size:1.05rem';
        var cev = el('span', 'ks-cevrimici');
        var nokta = el('span', 'ks-nokta');
        nokta.setAttribute('aria-hidden', 'true');
        var cevYazi = el('span');
        cev.appendChild(nokta);
        cev.appendChild(cevYazi);
        ust.appendChild(bas);
        ust.appendChild(cev);
        var sabitKutusu = el('div', 'ks-sabit');
        sabitKutusu.hidden = true;
        var sabitSol = el('div');
        sabitSol.appendChild(el('span', 'ks-sabit-baslik', 'Sabit'));
        var sabitMetinEl = el('span');
        sabitSol.appendChild(sabitMetinEl);
        sabitKutusu.appendChild(sabitSol);
        var liste = el('ul', 'ks-liste');
        // Şerit herkesin gördüğü ana sayfada akıyor; ekran okuyucuyu her mesajda bölmemek için canlı bölge kullanılmaz.
        var kapi = el('div', 'ks-kapi');
        var tam = el('a', null, 'Sohbeti aç');
        tam.setAttribute('href', secenek.sohbetAdresi || '#/sohbet');
        if (girisli) {
            kapi.appendChild(el('span', null, 'Sen de sohbete katıl.'));
        } else {
            var yazi = el('span');
            yazi.appendChild(document.createTextNode('Sohbete katılmak için '));
            var g = el('a', null, 'giriş yapmalısınız');
            g.setAttribute('href', secenek.girisAdresi || 'login.html');
            yazi.appendChild(g);
            yazi.appendChild(document.createTextNode('.'));
            kapi.appendChild(yazi);
        }
        kapi.appendChild(tam);
        kok.appendChild(ust);
        kok.appendChild(sabitKutusu);
        kok.appendChild(liste);
        kok.appendChild(kapi);
        kap.appendChild(kok);

        function ciz() {
            var altta = liste.scrollHeight - liste.scrollTop - liste.clientHeight < 60;
            sabitKutusu.hidden = !durum.sabitMesaj;
            sabitMetinEl.textContent = durum.sabitMesaj || '';
            liste.textContent = '';
            var son = durum.mesajlar.slice(-30);
            if (!son.length) {
                liste.appendChild(el('li', 'ks-bos', durum.hazir ? (durum.acik ? 'Henüz mesaj yok. İlk mesajı sen yaz.' : 'Sohbet şu an kapalı.') : 'Yükleniyor…'));
            } else {
                son.forEach(function (m) { liste.appendChild(mesajEl(m, {})); });
            }
            if (altta) liste.scrollTop = liste.scrollHeight;
            durumYazisi(kok, cevYazi);
        }
        ciz();
        liste.scrollTop = liste.scrollHeight;
        var birak = dinle(function () { ciz(); });
        return {
            yoksay: function () { birak(); kullanimKapat(); if (kok.parentNode) kok.parentNode.removeChild(kok); }
        };
    }

    /* ------------------------- Tam sayfa ------------------------- */
    function sayfa(kap, secenek) {
        secenek = secenek || {};
        kullanimAc(secenek);
        var kullanici = secenek.kullanici || null;
        var girisli = !!(kullanici && durum.tokenFn());
        var yonetici = !!(kullanici && (kullanici.rol === 'Owner' || (kullanici.rol === 'Admin' && Array.isArray(kullanici.yetkiler) && kullanici.yetkiler.indexOf('sohbet.yonet') >= 0)));
        var seviye = 0;
        var iptal = false;

        var kok = el('div', 'ks-cerceve ks-sayfa');
        var ust = el('div', 'ks-baslik');
        var bas = el('h1', null, 'Sohbet');
        bas.style.cssText = 'margin:0;font-size:1.5rem';
        var cev = el('span', 'ks-cevrimici');
        var nokta = el('span', 'ks-nokta');
        nokta.setAttribute('aria-hidden', 'true');
        var cevYazi = el('span');
        cev.appendChild(nokta);
        cev.appendChild(cevYazi);
        ust.appendChild(bas);
        ust.appendChild(cev);

        var uyari = el('div', 'ks-not');
        uyari.style.display = 'none';
        var sabitKutusu = el('div', 'ks-sabit');
        sabitKutusu.hidden = true;
        var sabitSol = el('div');
        sabitSol.appendChild(el('span', 'ks-sabit-baslik', 'Sabit'));
        var sabitMetinEl = el('span');
        sabitSol.appendChild(sabitMetinEl);
        sabitKutusu.appendChild(sabitSol);
        if (yonetici) {
            var sabitKaldirBtn = el('button', 'ks-sil', 'Kaldır');
            sabitKaldirBtn.type = 'button';
            sabitKaldirBtn.style.marginLeft = 'auto';
            sabitKaldirBtn.addEventListener('click', function () {
                var h = { 'Authorization': 'Bearer ' + (durum.tokenFn() || '') };
                fetch(durum.apiUrl + '/admin/sohbet/sabit', { method: 'DELETE', headers: h })
                    .then(function (r) { if (!r.ok) throw new Error(); })
                    .catch(function () { hataYaz('Sabit mesaj kaldırılamadı.'); });
            });
            sabitKutusu.appendChild(sabitKaldirBtn);
        }
        var liste = el('ul', 'ks-liste');
        liste.setAttribute('role', 'log');
        liste.setAttribute('aria-live', 'polite');
        liste.setAttribute('aria-label', 'Sohbet mesajları');
        var eski = el('button', 'ks-eski', 'Daha eski mesajlar');
        eski.type = 'button';

        kok.appendChild(ust);
        kok.appendChild(uyari);
        kok.appendChild(sabitKutusu);
        kok.appendChild(eski);
        kok.appendChild(liste);

        var giris, gonderBtn, stBtn, sayac, hataEl, panel, form;
        if (girisli) {
            form = el('form', 'ks-form');
            form.setAttribute('autocomplete', 'off');
            var satir = el('div', 'ks-satir');
            giris = el('input', 'ks-giris');
            giris.type = 'text';
            giris.maxLength = MAKS_UZUNLUK;
            giris.placeholder = 'Mesajını yaz…';
            giris.setAttribute('aria-label', 'Mesaj');
            giris.enterKeyHint = 'send';
            stBtn = el('button', 'ks-btn ks-ikincil', 'Sticker');
            stBtn.type = 'button';
            stBtn.setAttribute('aria-expanded', 'false');
            gonderBtn = el('button', 'ks-btn', 'Gönder');
            gonderBtn.type = 'submit';
            satir.appendChild(giris);
            satir.appendChild(stBtn);
            satir.appendChild(gonderBtn);
            var alt = el('div', 'ks-alt');
            hataEl = el('span', 'ks-hata');
            hataEl.setAttribute('role', 'alert');
            sayac = el('span', null, '0/' + MAKS_UZUNLUK);
            alt.appendChild(hataEl);
            alt.appendChild(sayac);
            panel = el('div', 'ks-panel');
            panel.hidden = true;
            form.appendChild(satir);
            form.appendChild(alt);
            form.appendChild(panel);
            kok.appendChild(form);
        } else {
            var gn = el('p', 'ks-not');
            gn.appendChild(document.createTextNode('Mesaj yazmak için '));
            var a = el('a', null, 'giriş yap');
            a.setAttribute('href', secenek.girisAdresi || 'login.html');
            gn.appendChild(a);
            gn.appendChild(document.createTextNode('. Mesajları giriş yapmadan da okuyabilirsin.'));
            kok.appendChild(gn);
        }
        kap.appendChild(kok);

        function hataYaz(m) { if (hataEl) hataEl.textContent = m || ''; }

        function yakin() { return liste.scrollHeight - liste.scrollTop - liste.clientHeight < 80; }
        function alta() { liste.scrollTop = liste.scrollHeight; }

        var secenekMesaj = {
            profilAdresi: null,
            bahset: girisli ? function (ad) {
                if (!giris || !ad) return;
                var ek = '@' + ad + ' ';
                if (giris.value.indexOf(ek) < 0) {
                    var yeni = (giris.value ? giris.value.replace(/\s+$/, '') + ' ' : '') + ek;
                    if (yeni.length <= MAKS_UZUNLUK) {
                        giris.value = yeni;
                        sayac.textContent = giris.value.length + '/' + MAKS_UZUNLUK;
                    }
                }
                giris.focus();
            } : null,
            sabitle: yonetici ? function (m, btn) {
                if (!m || !m.metin) return;
                btn.disabled = true;
                var h = { 'Authorization': 'Bearer ' + (durum.tokenFn() || ''), 'Content-Type': 'application/json' };
                fetch(durum.apiUrl + '/admin/sohbet/sabit', { method: 'PUT', headers: h, body: JSON.stringify({ metin: m.kullaniciAdi + ': ' + m.metin }) })
                    .then(function (r) { if (!r.ok) throw new Error(); })
                    .catch(function () { hataYaz('Mesaj sabitlenemedi.'); })
                    .then(function () { btn.disabled = false; });
            } : null,
            sil: yonetici ? function (m, btn) {
                if (!window.confirm('Bu mesaj silinsin mi?')) return;
                btn.disabled = true;
                var h = { 'Authorization': 'Bearer ' + (durum.tokenFn() || '') };
                fetch(durum.apiUrl + '/admin/sohbet/mesajlar/' + m.id, { method: 'DELETE', headers: h })
                    .then(function (r) { if (!r.ok) throw new Error(); })
                    .catch(function () { btn.disabled = false; hataYaz('Mesaj silinemedi.'); });
            } : null
        };

        function tumunuCiz() {
            liste.textContent = '';
            if (!durum.mesajlar.length) {
                liste.appendChild(el('li', 'ks-bos', durum.hazir ? 'Henüz mesaj yok.' : 'Yükleniyor…'));
            } else {
                durum.mesajlar.forEach(function (m) { liste.appendChild(mesajEl(m, secenekMesaj)); });
            }
        }

        function durumCiz() {
            durumYazisi(kok, cevYazi);
            sabitKutusu.hidden = !durum.sabitMesaj;
            sabitMetinEl.textContent = durum.sabitMesaj || '';
            eski.hidden = !durum.mesajlar.length;
            if (!durum.acik) {
                uyari.style.display = '';
                uyari.textContent = 'Sohbet şu an kapalı. Eski mesajları okuyabilirsin.';
            } else { uyari.style.display = 'none'; }
            if (giris) {
                var kapali = !durum.acik;
                giris.disabled = kapali;
                gonderBtn.disabled = kapali;
                stBtn.disabled = kapali;
            }
        }

        tumunuCiz();
        durumCiz();
        alta();

        var birak = dinle(function (tur, veri) {
            if (iptal) return;
            if (tur === 'liste' || tur === 'hata') {
                var altta = yakin();
                tumunuCiz();
                if (altta || tur === 'liste') alta();
            } else if (tur === 'mesaj') {
                var yakinMi = yakin();
                if (liste.querySelector('.ks-bos')) liste.textContent = '';
                liste.appendChild(mesajEl(veri, secenekMesaj));
                if (yakinMi) alta();
            } else if (tur === 'silindi') {
                var li = liste.querySelector('[data-id="' + veri + '"]');
                if (li) li.parentNode.removeChild(li);
            }
            durumCiz();
        });

        eski.addEventListener('click', function () {
            var ilk = durum.mesajlar.length ? durum.mesajlar[0].id : null;
            if (ilk == null) return;
            eski.disabled = true;
            getir('/sohbet/mesajlar?limit=50&onceki=' + ilk).then(function (l) {
                if (iptal) return;
                if (!Array.isArray(l) || !l.length) { eski.textContent = 'Başka mesaj yok'; return; }
                var oncekiYukseklik = liste.scrollHeight;
                birlestir(l);
                tumunuCiz();
                liste.scrollTop = liste.scrollHeight - oncekiYukseklik;
                eski.disabled = false;
            }).catch(function () { if (!iptal) { eski.disabled = false; hataYaz('Eski mesajlar yüklenemedi.'); } });
        });

        if (girisli) {
            // Sticker paneli: seviye /hesap/puan'dan gelir, kilitli paketler gri görünür.
            fetch(durum.apiUrl + '/hesap/puan', { headers: { 'Authorization': 'Bearer ' + (durum.tokenFn() || '') } })
                .then(function (r) { return r.ok ? r.json() : null; })
                .then(function (v) { if (v && typeof v.seviye === 'number') { seviye = v.seviye; } })
                .catch(function () {});

            var panelCiz = function () {
                panel.textContent = '';
                PAKETLER.forEach(function (p) {
                    var acik = seviye >= p.seviye;
                    panel.appendChild(el('h4', null, p.ad + ' paketi'));
                    if (!acik) panel.appendChild(el('p', 'ks-kilit', 'Seviye ' + p.seviye + ' gerekir' + (seviye ? ' (şu an ' + seviye + ')' : '') + '.'));
                    var iz = el('div', 'ks-izgara');
                    p.liste.forEach(function (s) {
                        var id = p.id + ':' + s[0];
                        var b = el('button', 'ks-stbtn');
                        b.type = 'button';
                        b.disabled = !acik;
                        b.setAttribute('aria-label', s[1]);
                        b.title = s[1];
                        b.appendChild(stickerSvg(id));
                        b.addEventListener('click', function () { yolla('', id); });
                        iz.appendChild(b);
                    });
                    panel.appendChild(iz);
                });
            };
            stBtn.addEventListener('click', function () {
                var ac = panel.hidden;
                panel.hidden = !ac;
                stBtn.setAttribute('aria-expanded', ac ? 'true' : 'false');
                if (ac) panelCiz();
            });

            giris.addEventListener('input', function () { sayac.textContent = giris.value.length + '/' + MAKS_UZUNLUK; });

            var yolluyor = false;
            var yolla = function (metin, stickerId) {
                if (yolluyor) return;
                metin = (metin || '').trim();
                if (!metin && !stickerId) return;
                yolluyor = true;
                hataYaz('');
                gonderBtn.disabled = true;
                gonder(metin, stickerId).then(function () {
                    if (iptal) return;
                    if (!stickerId) { giris.value = ''; sayac.textContent = '0/' + MAKS_UZUNLUK; }
                    else { panel.hidden = true; stBtn.setAttribute('aria-expanded', 'false'); }
                    giris.focus();
                }).catch(function (e) {
                    if (!iptal) hataYaz(hataMetni(e, 'Mesaj gönderilemedi. Birazdan tekrar dene.'));
                }).then(function () {
                    yolluyor = false;
                    if (!iptal) gonderBtn.disabled = !durum.acik;
                });
            };
            form.addEventListener('submit', function (e) { e.preventDefault(); yolla(giris.value, null); });
        }

        return {
            yoksay: function () {
                iptal = true;
                birak();
                kullanimKapat();
                if (kok.parentNode) kok.parentNode.removeChild(kok);
            }
        };
    }

    window.Kage95Sohbet = { serit: serit, sayfa: sayfa, stickerPaketleri: PAKETLER };
})();
