// Kage 95 - Puan bildirimi (toast) ve liderlik yardımcıları. Bağımsız vanilla JS, React gerektirmez.
// Kullanım: Kage95Liderlik.yanittan(yanitGovdesi);  // yanıtta puanKazanildi > 0 ise "+N puan" gösterir
//           Kage95Liderlik.bildir(10);
// Sunucu sözleşmesi: puan veren uçların yanıtlarında "puanKazanildi": int (0 ya da yoksa bildirim gösterilmez).
(function () {
    'use strict';
    if (window.Kage95Liderlik) return;

    var STIL = [
        '.k95-toast{position:fixed;z-index:200;left:max(16px,env(safe-area-inset-left,0px));bottom:calc(var(--alt-gezinme-yuksekligi,0px) + 16px + env(safe-area-inset-bottom,0px));',
        'max-width:calc(100vw - 32px);pointer-events:none}',
        '.k95-toast-ic{display:inline-flex;align-items:center;gap:8px;padding:10px 16px;background:var(--bg-kart,#141111);color:var(--text-ana,#ede7e2);',
        'border:1px solid var(--border-belirgin,#3a3030);border-left:3px solid var(--vurgu-parlak,#e0343c);border-radius:2px;font-size:.9rem;font-weight:500;line-height:1.3;',
        'opacity:0;transform:translateY(8px);transition:opacity .18s ease,transform .18s ease;visibility:hidden}',
        '.k95-toast-ic.goster{opacity:1;transform:none;visibility:visible}',
        '.k95-toast-ic strong{font-weight:600;color:var(--vurgu-parlak,#e0343c)}',
        '@media (max-width:768px){.k95-toast-ic{font-size:.9375rem}}',
        '@media (prefers-reduced-motion:reduce){.k95-toast-ic{transition:none;transform:none}}'
    ].join('');

    var kap = null, ic = null, strong = null, span = null, zaman = null;

    function kur() {
        if (kap || !document.body) return;
        if (!document.getElementById('kage95-liderlik-stili')) {
            var s = document.createElement('style');
            s.id = 'kage95-liderlik-stili';
            s.textContent = STIL;
            document.head.appendChild(s);
        }
        // Canlı bölge içerik gelmeden önce sayfada bulunmalı ki ekran okuyucular yeni metni duyursun.
        kap = document.createElement('div');
        kap.className = 'k95-toast';
        kap.setAttribute('role', 'status');
        kap.setAttribute('aria-live', 'polite');
        kap.setAttribute('aria-atomic', 'true');
        ic = document.createElement('span');
        ic.className = 'k95-toast-ic';
        strong = document.createElement('strong');
        span = document.createElement('span');
        ic.appendChild(strong);
        ic.appendChild(span);
        kap.appendChild(ic);
        document.body.appendChild(kap);
    }

    function bildir(n) {
        n = Number(n);
        if (!isFinite(n) || n <= 0) return;
        kur();
        if (!kap) return;
        strong.textContent = '+' + Math.round(n);
        span.textContent = 'puan';
        // Görünür yap (bir önceki bildirim hâlâ açıksa sayaç yenilenir).
        ic.classList.remove('goster');
        void ic.offsetWidth;
        ic.classList.add('goster');
        clearTimeout(zaman);
        zaman = setTimeout(function () { ic.classList.remove('goster'); }, 3000);
    }

    function yanittan(veri) {
        if (veri && typeof veri === 'object') bildir(veri.puanKazanildi);
    }

    // Sunucu kuralları gelmezse gösterilecek yedek metin.
    var YEDEK_KURALLAR = [
        { eylem: 'bolum', ad: 'Bölümü sonuna kadar okumak', puan: 10, kural: 'Günde en fazla 10 bölüm' },
        { eylem: 'yorum', ad: 'Yorum yazmak', puan: 5, kural: 'En az 20 karakter, günde en fazla 5 yorum' },
        { eylem: 'puan', ad: 'Seri ya da bölüme puan vermek', puan: 2, kural: '' },
        { eylem: 'kutuphane', ad: 'Kütüphaneye seri eklemek', puan: 3, kural: '' },
        { eylem: 'giris', ad: 'Günlük giriş', puan: 2, kural: '7 gün üst üste girersen +10' },
        { eylem: 'avatar', ad: 'Profil resmi eklemek', puan: 10, kural: 'Bir kez' }
    ];
    var YEDEK_NOT = 'Günlük toplam üst sınır 250 puandır.';

    window.Kage95Liderlik = { bildir: bildir, yanittan: yanittan, yedekKurallar: YEDEK_KURALLAR, yedekNot: YEDEK_NOT };

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', kur);
    else kur();
})();
