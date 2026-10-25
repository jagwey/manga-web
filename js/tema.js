// Ortak tema sistemi: tüm sayfalar aynı renk değişkenlerini kullanır, tema yalnızca bu değişkenleri değiştirir.
// Yeni tema eklemek için TEMALAR dizisine bir kayıt eklemek yeterli.
(function () {
    'use strict';
    var ANAHTAR = 'kage95_tema';

    var TEMALAR = [
        { id: 'kage', ad: 'Kage', not: 'Koyu, kan kırmızısı', renk: '#8e0e15', zemin: '#0e0c0c', vars: null },
        { id: 'sistem', ad: 'Sistem', not: 'Cihaz temasını izler', renk: '#8e0e15', zemin: '#0e0c0c', vars: null },
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
            '--bg-okuyucu': '#080b0f', '--bg-kutu': '#10161d', '--kaydirma': '#2f3f50' } }
    ];

    function bul(id) {
        for (var i = 0; i < TEMALAR.length; i++) if (TEMALAR[i].id === id) return TEMALAR[i];
        return TEMALAR[0];
    }

    // Kaldırılmış bir tema (ör. eski 'pop') kayıtlıysa varsayılana düşer
    function oku() {
        try { return bul(localStorage.getItem(ANAHTAR) || 'kage').id; } catch (e) { return 'kage'; }
    }

    var stil = document.createElement('style');
    stil.id = 'kage95-tema-stili';
    stil.textContent = TEMALAR.filter(function (t) { return t.vars; }).map(function (t) {
        return ':root[data-tema="' + t.id + '"]{' + Object.keys(t.vars).map(function (k) { return k + ':' + t.vars[k]; }).join(';') + '}';
    }).join('\n');
    (document.head || document.documentElement).appendChild(stil);

    // 'sistem' secenegi: cihazin acik/koyu tercihini izler (koyu: kage, acik: kagit)
    function sistemKoyuMu() {
        return !(window.matchMedia && window.matchMedia('(prefers-color-scheme: light)').matches);
    }
    function cozumle(id) {
        return id === 'sistem' ? (sistemKoyuMu() ? 'kage' : 'kagit') : id;
    }

    function uygula(id) {
        id = cozumle(id);
        var t = bul(id);
        document.documentElement.setAttribute('data-tema', t.id);
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
    // Sistem temasi secili iken cihaz tercihi degisirse otomatik guncelle
    if (window.matchMedia) {
        var sistemMq = window.matchMedia('(prefers-color-scheme: light)');
        var sistemDegisti = function () {
            if (oku() !== 'sistem') return;
            uygula('sistem');
            window.dispatchEvent(new CustomEvent('kage95-tema', { detail: 'sistem' }));
        };
        if (sistemMq.addEventListener) sistemMq.addEventListener('change', sistemDegisti);
        else if (sistemMq.addListener) sistemMq.addListener(sistemDegisti);
    }
    window.Kage95Tema = { liste: TEMALAR, al: oku, sec: sec };
})();
