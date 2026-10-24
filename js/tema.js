// Ortak tema sistemi: tüm sayfalar aynı renk değişkenlerini kullanır, tema yalnızca bu değişkenleri değiştirir.
// Yeni tema eklemek için TEMALAR dizisine bir kayıt eklemek yeterli.
(function () {
    'use strict';
    var ANAHTAR = 'kage95_tema';

    var TEMALAR = [
        { id: 'kage', ad: 'Kage', not: 'Koyu, kan kırmızısı', renk: '#8e0e15', zemin: '#0e0c0c', vars: null },
        { id: 'kagit', ad: 'Kâğıt', not: 'Açık, krem zemin', renk: '#8e0e15', zemin: '#f3ede3', vars: {
            'color-scheme': 'light',
            '--vurgu-rengi': '#8e0e15', '--vurgu-parlak': '#a3121a',
            '--bg-ana': '#f3ede3', '--bg-kart': '#ebe4d8', '--bg-yuzey': '#e2dacb', '--bg-kaplama': 'rgba(243,237,227,.82)',
            '--border-renk': '#d6cdbd', '--border-belirgin': '#b9ad9a',
            '--text-ana': '#1f1a17', '--text-ikincil': '#6a5f56',
            '--bg-okuyucu': '#f3ede3', '--bg-kutu': '#ebe4d8', '--kaydirma': '#b9ad9a' } },
        { id: 'mono', ad: 'Mono', not: 'Siyah beyaz, yüksek kontrast', renk: '#ffffff', zemin: '#000000', vars: {
            'color-scheme': 'dark',
            '--vurgu-rengi': '#4a4a4a', '--vurgu-parlak': '#ffffff',
            '--bg-ana': '#000000', '--bg-kart': '#0d0d0d', '--bg-yuzey': '#161616', '--bg-kaplama': 'rgba(0,0,0,.8)',
            '--border-renk': '#2e2e2e', '--border-belirgin': '#5a5a5a',
            '--text-ana': '#ffffff', '--text-ikincil': '#a8a8a8',
            '--bg-okuyucu': '#000000', '--bg-kutu': '#0d0d0d', '--kaydirma': '#5a5a5a' } },
        { id: 'gece', ad: 'Gece', not: 'Soğuk lacivert-gri', renk: '#5ab0e6', zemin: '#0b0f14', vars: {
            'color-scheme': 'dark',
            '--vurgu-rengi': '#1f5f8b', '--vurgu-parlak': '#5ab0e6',
            '--bg-ana': '#0b0f14', '--bg-kart': '#10161d', '--bg-yuzey': '#151d26', '--bg-kaplama': 'rgba(11,15,20,.78)',
            '--border-renk': '#1f2a36', '--border-belirgin': '#2f3f50',
            '--text-ana': '#e6edf3', '--text-ikincil': '#8b9aab',
            '--bg-okuyucu': '#080b0f', '--bg-kutu': '#10161d', '--kaydirma': '#2f3f50' } },
        { id: 'pop', ad: 'Pop', not: 'Krem zemin, kalın çerçeve', renk: '#ff3b2a', zemin: '#f1ede2', vars: {
            'color-scheme': 'light',
            '--vurgu-rengi': '#ff3b2a', '--vurgu-parlak': '#c81e10',
            '--bg-ana': '#f1ede2', '--bg-kart': '#ffffff', '--bg-yuzey': '#e6e0d0', '--bg-kaplama': 'rgba(241,237,226,.9)',
            '--border-renk': '#0d0d0d', '--border-belirgin': '#0d0d0d',
            '--text-ana': '#0d0d0d', '--text-ikincil': '#4a443a',
            '--bg-okuyucu': '#f1ede2', '--bg-kutu': '#ffffff', '--kaydirma': '#0d0d0d',
            '--font-baslik': "'Anton', 'Bebas Neue', Impact, 'Arial Narrow', sans-serif",
            '--pop-golge': '4px 4px 0 #0d0d0d' } }
    ];

    // Pop temasının yazı tipi ve stil dosyaları yalnızca tema 'pop' iken sayfada bulunur.
    var POP_LINKLER = [
        { id: 'kage95-pop-font', href: 'https://fonts.googleapis.com/css2?family=Anton&family=Archivo:wght@400;500;600;700;800&display=swap' },
        { id: 'kage95-pop-css', href: 'css/tema-pop.css' },
        { id: 'kage95-pop-sayfalar', href: 'css/tema-pop-sayfalar.css' }
    ];

    function popLinkleri(ac) {
        POP_LINKLER.forEach(function (l) {
            var mevcut = document.getElementById(l.id);
            if (!ac) { if (mevcut && mevcut.parentNode) mevcut.parentNode.removeChild(mevcut); return; }
            if (mevcut) return;
            if (document.readyState === 'loading') {
                // Ayrıştırma sırasında eşzamanlı eklenir: sayfa ilk boyamada pop stilleriyle gelir (flash olmaz).
                document.write('<link rel="stylesheet" id="' + l.id + '" href="' + l.href + '">');
            } else {
                var el = document.createElement('link');
                el.rel = 'stylesheet'; el.id = l.id; el.href = l.href;
                document.head.appendChild(el);
            }
        });
    }

    function oku() {
        try { return localStorage.getItem(ANAHTAR) || 'kage'; } catch (e) { return 'kage'; }
    }

    function bul(id) {
        for (var i = 0; i < TEMALAR.length; i++) if (TEMALAR[i].id === id) return TEMALAR[i];
        return TEMALAR[0];
    }

    var stil = document.createElement('style');
    stil.id = 'kage95-tema-stili';
    stil.textContent = TEMALAR.filter(function (t) { return t.vars; }).map(function (t) {
        return ':root[data-tema="' + t.id + '"]{' + Object.keys(t.vars).map(function (k) { return k + ':' + t.vars[k]; }).join(';') + '}';
    }).join('\n');
    (document.head || document.documentElement).appendChild(stil);

    function uygula(id) {
        var t = bul(id);
        document.documentElement.setAttribute('data-tema', t.id);
        popLinkleri(t.id === 'pop');
        var meta = document.querySelector('meta[name="theme-color"]');
        if (meta) meta.setAttribute('content', t.zemin);
        return t.id;
    }

    function sec(id) {
        var t = bul(id);
        try { localStorage.setItem(ANAHTAR, t.id); } catch (e) {}
        uygula(t.id);
        window.dispatchEvent(new CustomEvent('kage95-tema', { detail: t.id }));
    }

    uygula(oku());
    window.addEventListener('storage', function (e) {
        if (e.key !== ANAHTAR) return;
        var id = uygula(oku());
        window.dispatchEvent(new CustomEvent('kage95-tema', { detail: id }));
    });
    window.Kage95Tema = { liste: TEMALAR, al: oku, sec: sec };
})();
