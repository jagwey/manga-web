// Kage 95 - Seviye ödülü kullanıcı adı stili (renk, parlama, animasyon, rozet çerçevesi, unvan).
// Sunucudaki adStili nesnesini (adRengiHex, parlak, animasyon, rozet, unvan, mesajFontu) görünüme çevirir.
// Kullanım (React): <span className={'yorum-yazar ' + Kage95AdStili.sinif(y.adStili)} style={Kage95AdStili.stil(y.adStili)}>
//                    {Kage95AdStili.unvan(y.adStili) && <span className="as-unvan">{Kage95AdStili.unvan(y.adStili)}</span>}
(function () {
    'use strict';
    if (window.Kage95AdStili) return;

    var HEX = /^#[0-9a-fA-F]{6}$/;
    var FONTLAR = {
        serif: "Georgia, 'Times New Roman', Times, serif",
        mono: "'Courier New', Consolas, Menlo, monospace",
        elyazisi: "'Segoe Print', 'Bradley Hand', 'Comic Sans MS', cursive"
    };

    function sinif(st) {
        if (!st) return '';
        var s = [];
        if (st.parlak) s.push('as-parlak');
        if (st.parlak && st.animasyon) s.push('as-anim');
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
        return st && st.mesajFontu && FONTLAR[st.mesajFontu] ? FONTLAR[st.mesajFontu] : undefined;
    }

    window.Kage95AdStili = { sinif: sinif, stil: stil, unvan: unvan, font: font, fontlar: FONTLAR };
})();
