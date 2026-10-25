// Kage 95 - Seviye ödülü kullanıcı adı stili (renk, ad efekti, mühür çerçevesi, unvan, sohbet yazı tipi).
// Sunucudaki adStili nesnesini (adRengiHex, adEfekti, rozet, unvan, mesajFontu) görünüme çevirir.
// Kullanım (React): <span className={'yorum-yazar ' + Kage95AdStili.sinif(y.adStili)} style={Kage95AdStili.stil(y.adStili)}>
//                    {Kage95AdStili.unvan(y.adStili) && <span className="as-unvan">{Kage95AdStili.unvan(y.adStili)}</span>}
(function () {
    'use strict';
    if (window.Kage95AdStili) return;

    var HEX = /^#[0-9a-fA-F]{6}$/;
    // Yığınlar backend/Services/OdulKatalogu.cs ile aynı tutulur.
    var FONTLAR = {
        yuvarlak: "'Nunito', system-ui, sans-serif",
        genis: "'Rubik', system-ui, sans-serif",
        serif: "Georgia, 'Times New Roman', Times, serif",
        kitap: "'Lora', Georgia, serif",
        zarif: "'Playfair Display', Georgia, serif",
        mono: "'Courier New', Consolas, Menlo, monospace",
        terminal: "'JetBrains Mono', Consolas, monospace",
        elyazisi: "'Segoe Print', 'Bradley Hand', 'Comic Sans MS', cursive",
        kalem: "'Patrick Hand', 'Comic Sans MS', cursive",
        defter: "'Kalam', 'Comic Sans MS', cursive",
        kaligrafi: "'Caveat', 'Segoe Print', cursive",
        baskin: "'Bangers', Impact, sans-serif"
    };
    // Google Fonts aileleri: yalnız biri kullanıldığında yüklenir.
    var WEB_FONT = {
        yuvarlak: 'Nunito:wght@400;700', genis: 'Rubik:wght@400;600', kitap: 'Lora:wght@400;600',
        zarif: 'Playfair+Display:wght@400;700', terminal: 'JetBrains+Mono:wght@400;600',
        kalem: 'Patrick+Hand', defter: 'Kalam:wght@400;700', kaligrafi: 'Caveat:wght@400;700', baskin: 'Bangers'
    };
    var EFEKTLER = { murekkep: 1, aura: 1, golge: 1, hiz: 1, ton: 1, nabiz: 1, varak: 1, alev: 1 };
    var yuklenen = {};

    function fontYukle(id) {
        if (!WEB_FONT[id] || yuklenen[id]) return;
        yuklenen[id] = true;
        var l = document.createElement('link');
        l.rel = 'stylesheet';
        l.href = 'https://fonts.googleapis.com/css2?family=' + WEB_FONT[id] + '&display=swap';
        document.head.appendChild(l);
    }

    // Eski sunucu yanıtı: parlak -> aura, parlak + animasyon -> nabiz
    function efekt(st) {
        if (!st) return '';
        if (st.adEfekti && EFEKTLER[st.adEfekti]) return st.adEfekti;
        if (st.parlak) return st.animasyon ? 'nabiz' : 'aura';
        return '';
    }

    function sinif(st) {
        if (!st) return '';
        var s = [];
        var e = efekt(st);
        if (e) s.push('as-ef as-ef-' + e);
        if (st.rozet) s.push('as-rozet');
        return s.join(' ');
    }

    function stil(st) {
        return st && st.adRengiHex && HEX.test(st.adRengiHex) ? { color: st.adRengiHex } : undefined;
    }

    function unvan(st) {
        return st && st.unvan ? String(st.unvan) : '';
    }

    function font(st) {
        var id = st && st.mesajFontu;
        if (!id || !FONTLAR[id]) return undefined;
        fontYukle(id);
        return FONTLAR[id];
    }

    window.Kage95AdStili = { sinif: sinif, stil: stil, unvan: unvan, font: font, fontlar: FONTLAR, fontYukle: fontYukle };
})();
