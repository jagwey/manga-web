// Kage 95 - Yıldız puan bileşeni (bağımsız, React gerektirmez).
// Kullanım: var h = Kage95Puan.baslat(kapsayiciEl, { url: API + '/mangas/1/puan', token: function () { return '...'; }, etiket: 'Bu seriyi puanla' });
//           h.yoksay();  // kapsayıcıyı temizler, bekleyen istekleri yok sayar
// Sunucu sözleşmesi: GET/PUT({deger:1..5})/DELETE -> { ortalama: sayı|null, oySayisi: sayı, benimPuanim: sayı|null }
(function () {
    'use strict';

    var STIL = [
        '.kp-puan{max-width:100%;display:flex;flex-wrap:wrap;align-items:center;gap:8px 24px;color:var(--text-ana)}',
        '.kp-ozet{display:inline-flex;align-items:center;gap:8px;font-size:.9rem;color:var(--text-ikincil);min-height:32px}',
        '.kp-ozet svg{width:18px;height:18px;flex-shrink:0;color:var(--vurgu-parlak);fill:currentColor}',
        '.kp-ozet strong{font-size:1.05rem;font-weight:600;color:var(--text-ana)}',
        '.kp-kendi{display:inline-flex;align-items:center;gap:8px;flex-wrap:wrap}',
        '.kp-etiket{font-size:.875rem;color:var(--text-ikincil)}',
        '.kp-yildizlar{display:inline-flex;align-items:center}',
        '.kp-yildiz{background:none;border:0;padding:0;margin:0;cursor:pointer;width:32px;height:32px;display:inline-flex;align-items:center;justify-content:center;color:var(--text-ikincil);border-radius:2px}',
        '.kp-yildiz svg{width:22px;height:22px;fill:none;stroke:currentColor;stroke-width:1.6;stroke-linejoin:round;pointer-events:none}',
        '.kp-yildiz.kp-dolu{color:var(--vurgu-parlak)}',
        '.kp-yildiz.kp-dolu svg{fill:currentColor}',
        '.kp-yildiz:focus-visible,.kp-kaldir:focus-visible,.kp-tekrar:focus-visible{outline:1px solid var(--vurgu-parlak);outline-offset:2px}',
        '.kp-puan[aria-busy="true"] .kp-yildiz{cursor:progress;opacity:.6}',
        '.kp-kaldir,.kp-tekrar{background:none;border:0;padding:0 4px;min-height:32px;font-size:.875rem;color:var(--text-ikincil);text-decoration:underline;text-underline-offset:3px;cursor:pointer}',
        '@media (hover:hover){.kp-kaldir:hover,.kp-tekrar:hover{color:var(--text-ana)}}',
        '.kp-mesaj{flex-basis:100%;font-size:.875rem;color:var(--text-ikincil);min-height:0}',
        '.kp-mesaj:empty{display:none}',
        '.kp-mesaj a{color:var(--vurgu-parlak);text-decoration:underline;text-underline-offset:3px}',
        '@media (max-width:768px),(pointer:coarse){.kp-yildiz{width:44px;height:44px}.kp-yildiz svg{width:26px;height:26px}.kp-kaldir,.kp-tekrar{min-height:44px;padding:0 8px}.kp-ozet{min-height:44px}}',
        '@media (prefers-reduced-motion:reduce){.kp-yildiz{transition:none}}'
    ].join('\n');

    var YILDIZ_YOLU = 'M12 2.8l2.8 5.9 6.4.8-4.7 4.4 1.2 6.4L12 17.2 6.3 20.3l1.2-6.4L2.8 9.5l6.4-.8z';
    var SVGNS = 'http://www.w3.org/2000/svg';

    function stilEkle() {
        if (document.getElementById('kage95-puan-stili')) return;
        var s = document.createElement('style');
        s.id = 'kage95-puan-stili';
        s.textContent = STIL;
        (document.head || document.documentElement).appendChild(s);
    }

    function yildizSvg() {
        var svg = document.createElementNS(SVGNS, 'svg');
        svg.setAttribute('viewBox', '0 0 24 24');
        svg.setAttribute('aria-hidden', 'true');
        var p = document.createElementNS(SVGNS, 'path');
        p.setAttribute('d', YILDIZ_YOLU);
        svg.appendChild(p);
        return svg;
    }

    function el(ad, sinif, metin) {
        var e = document.createElement(ad);
        if (sinif) e.className = sinif;
        if (metin != null) e.textContent = metin;
        return e;
    }

    function ondalik(n) { return Number(n).toFixed(1).replace('.', ','); }

    function baslat(kapsayici, ayar) {
        stilEkle();
        var url = ayar.url;
        var tokenAl = ayar.token || function () { return null; };
        var etiket = ayar.etiket || 'Puanla';
        var girisUrl = ayar.girisUrl || 'login.html';
        var yok = false;
        var durum = { ortalama: null, oySayisi: 0, benimPuanim: null };
        var onizle = 0;
        var yukleniyor = true;
        var yuklemeHata = false;
        var yukleNesil = 0;
        var mesajNode = null;

        kapsayici.textContent = '';
        var kok = el('div', 'kp-puan');
        kok.setAttribute('role', 'group');
        kok.setAttribute('aria-label', etiket);
        kapsayici.appendChild(kok);

        function mesajYaz(metin, girisBaglantisi) {
            if (!mesajNode) return;
            mesajNode.textContent = '';
            if (girisBaglantisi) {
                mesajNode.appendChild(document.createTextNode(metin + ' '));
                var a = el('a', null, 'Giriş yap');
                a.href = girisUrl;
                mesajNode.appendChild(a);
                mesajNode.appendChild(document.createTextNode('.'));
            } else if (metin) {
                mesajNode.textContent = metin;
            }
        }

        function istek(yontem, govde) {
            var basliklar = {};
            var t = tokenAl();
            if (t) basliklar['Authorization'] = 'Bearer ' + t;
            if (govde) basliklar['Content-Type'] = 'application/json';
            return fetch(url, { method: yontem, headers: basliklar, body: govde ? JSON.stringify(govde) : undefined });
        }

        function ozetAl(r) {
            return r.json().then(function (v) {
                return {
                    ortalama: typeof v.ortalama === 'number' ? v.ortalama : null,
                    oySayisi: v.oySayisi || 0,
                    benimPuanim: typeof v.benimPuanim === 'number' ? v.benimPuanim : null,
                    puanKazanildi: typeof v.puanKazanildi === 'number' ? v.puanKazanildi : 0
                };
            });
        }

        function hataMesaji(r) {
            return r.json().then(function (v) { return (v && (v.message || v.mesaj)) || null; }, function () { return null; });
        }

        function yukle() {
            var nesil = ++yukleNesil;
            yukleniyor = true; yuklemeHata = false; ciz();
            istek('GET').then(function (r) {
                if (!r.ok) throw new Error('HTTP');
                return ozetAl(r);
            }).then(function (v) {
                if (yok || nesil !== yukleNesil) return;
                durum = v; yukleniyor = false; yuklemeHata = false; ciz();
            }).catch(function () {
                if (yok || nesil !== yukleNesil) return;
                yukleniyor = false; yuklemeHata = true; ciz();
            });
        }

        function degistir(yontem, deger) {
            if (!tokenAl()) { mesajYaz('Puan vermek için', true); return; }
            kok.setAttribute('aria-busy', 'true');
            mesajYaz('');
            istek(yontem, deger ? { deger: deger } : null).then(function (r) {
                if (r.status === 401) { mesajYaz('Oturumun sona ermiş.', true); return null; }
                if (!r.ok) {
                    return hataMesaji(r).then(function (m) { throw new Error(m || 'Puan kaydedilemedi. Biraz sonra tekrar dene.'); });
                }
                return ozetAl(r);
            }).then(function (v) {
                if (yok) return;
                kok.removeAttribute('aria-busy');
                if (v) {
                    durum = v; ciz(); mesajYaz(yontem === 'DELETE' ? 'Puanın kaldırıldı.' : 'Puanın kaydedildi.');
                    // Sitenin puan sistemi (js/liderlik.js) yüklüyse kazanılan puanı bildir.
                    if (yontem === 'PUT' && v.puanKazanildi > 0 && window.Kage95Liderlik) window.Kage95Liderlik.bildir(v.puanKazanildi);
                }
            }).catch(function (e) {
                if (yok) return;
                kok.removeAttribute('aria-busy');
                mesajYaz(e && e.message && e.message !== 'Failed to fetch' ? e.message : 'Sunucuya ulaşılamadı.');
            });
        }

        function yildizlariBoya(butonlar) {
            var gecerli = onizle || durum.benimPuanim || 0;
            for (var i = 0; i < butonlar.length; i++) {
                butonlar[i].classList.toggle('kp-dolu', i < gecerli);
            }
        }

        function ciz() {
            var eskiMesaj = mesajNode ? mesajNode.textContent : '';
            kok.textContent = '';

            var ozet = el('div', 'kp-ozet');
            if (yukleniyor) {
                ozet.textContent = 'Puan yükleniyor...';
            } else if (yuklemeHata) {
                ozet.appendChild(document.createTextNode('Puan yüklenemedi. '));
                var tk = el('button', 'kp-tekrar', 'Tekrar dene');
                tk.type = 'button';
                tk.addEventListener('click', yukle);
                ozet.appendChild(tk);
            } else if (durum.ortalama == null || !durum.oySayisi) {
                ozet.textContent = 'Henüz puan yok';
            } else {
                ozet.appendChild(yildizSvg());
                ozet.appendChild(el('strong', null, ondalik(durum.ortalama)));
                ozet.appendChild(el('span', null, durum.oySayisi + ' oy'));
                ozet.setAttribute('aria-label', 'Ortalama puan 5 üzerinden ' + ondalik(durum.ortalama) + ', ' + durum.oySayisi + ' oy');
            }
            kok.appendChild(ozet);

            if (!yukleniyor && !yuklemeHata) {
                var kendi = el('div', 'kp-kendi');
                kendi.appendChild(el('span', 'kp-etiket', durum.benimPuanim ? 'Puanın' : etiket));
                var grup = el('div', 'kp-yildizlar');
                grup.setAttribute('role', 'radiogroup');
                grup.setAttribute('aria-label', etiket);
                var butonlar = [];
                var odak = durum.benimPuanim ? durum.benimPuanim - 1 : 0;
                for (var i = 1; i <= 5; i++) {
                    (function (n) {
                        var b = el('button', 'kp-yildiz');
                        b.type = 'button';
                        b.setAttribute('role', 'radio');
                        b.setAttribute('aria-label', n + ' yıldız');
                        b.setAttribute('aria-checked', durum.benimPuanim === n ? 'true' : 'false');
                        b.tabIndex = (n - 1 === odak) ? 0 : -1;
                        b.appendChild(yildizSvg());
                        b.addEventListener('click', function () { if (durum.benimPuanim !== n) degistir('PUT', n); });
                        b.addEventListener('mouseenter', function () { onizle = n; yildizlariBoya(butonlar); });
                        b.addEventListener('focus', function () { onizle = 0; });
                        b.addEventListener('keydown', function (e) {
                            var h = null;
                            if (e.key === 'ArrowRight' || e.key === 'ArrowUp') h = Math.min(4, n);
                            else if (e.key === 'ArrowLeft' || e.key === 'ArrowDown') h = Math.max(0, n - 2);
                            else if (e.key === 'Home') h = 0;
                            else if (e.key === 'End') h = 4;
                            if (h != null) {
                                e.preventDefault();
                                for (var j = 0; j < butonlar.length; j++) butonlar[j].tabIndex = j === h ? 0 : -1;
                                butonlar[h].focus();
                            }
                        });
                        butonlar.push(b);
                        grup.appendChild(b);
                    })(i);
                }
                grup.addEventListener('mouseleave', function () { onizle = 0; yildizlariBoya(butonlar); });
                yildizlariBoya(butonlar);
                kendi.appendChild(grup);
                if (durum.benimPuanim) {
                    var k = el('button', 'kp-kaldir', 'Kaldır');
                    k.type = 'button';
                    k.setAttribute('aria-label', 'Puanımı kaldır');
                    k.addEventListener('click', function () { degistir('DELETE'); });
                    kendi.appendChild(k);
                }
                kok.appendChild(kendi);
            }

            mesajNode = el('p', 'kp-mesaj');
            mesajNode.setAttribute('role', 'status');
            kok.appendChild(mesajNode);
            if (eskiMesaj && !yukleniyor) mesajNode.textContent = eskiMesaj;
        }

        yukle();
        return {
            yoksay: function () { yok = true; kapsayici.textContent = ''; }
        };
    }

    window.Kage95Puan = { baslat: baslat };
})();
