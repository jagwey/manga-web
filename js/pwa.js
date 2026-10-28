// Servis çalışanı kaydı (PWA).
// Güncelleme: ilk kurulumda sayfayı yenileme; sonraki sürümde controller değişince bir kez yenile.
// Bekleyen (waiting) çalışan varsa otomatik yenileme yerine "Yeni sürüm hazır" şeridi göster.
(function () {
    if (!('serviceWorker' in navigator)) return;

    var yenileniyor = false;
    var denetleyiciVardi = !!navigator.serviceWorker.controller;

    function yenile() {
        if (yenileniyor) return;
        yenileniyor = true;
        window.location.reload();
    }

    navigator.serviceWorker.addEventListener('controllerchange', function () {
        if (!denetleyiciVardi) {
            denetleyiciVardi = true;
            return;
        }
        yenile();
    });

    function banner(worker) {
        if (!document.body || document.getElementById('kage-guncelleme')) return;
        var kutu = document.createElement('div');
        kutu.id = 'kage-guncelleme';
        kutu.setAttribute('role', 'status');
        kutu.style.cssText = 'position:fixed;left:12px;right:12px;bottom:12px;z-index:80;display:flex;gap:12px;align-items:center;justify-content:space-between;flex-wrap:wrap;max-width:440px;margin:0 auto;padding:12px 14px;background:#171313;color:#ede7e2;border:1px solid #3a3030;border-radius:2px;box-shadow:0 8px 24px rgba(0,0,0,.35);font:500 14px/1.4 system-ui,sans-serif';
        var metin = document.createElement('span');
        metin.textContent = 'Yeni sürüm hazır';
        var btn = document.createElement('button');
        btn.type = 'button';
        btn.textContent = 'Yenile';
        btn.style.cssText = 'background:#8e0e15;color:#fff;border:0;border-radius:2px;min-height:36px;padding:8px 14px;font:inherit;cursor:pointer';
        btn.addEventListener('click', function () {
            btn.disabled = true;
            denetleyiciVardi = true;
            if (worker && worker.postMessage) worker.postMessage({ type: 'SKIP_WAITING' });
            else yenile();
        });
        kutu.appendChild(metin);
        kutu.appendChild(btn);
        document.body.appendChild(kutu);
    }

    function guncellemeBekle(reg) {
        var worker = reg.installing || reg.waiting;
        if (!worker) return;
        var bak = function () {
            if (worker.state === 'installed' && navigator.serviceWorker.controller && reg.waiting === worker) banner(worker);
        };
        if (worker.state === 'installed') bak();
        else worker.addEventListener('statechange', bak);
    }

    window.addEventListener('load', function () {
        navigator.serviceWorker.register('sw.js').then(function (reg) {
            if (reg.waiting && navigator.serviceWorker.controller) banner(reg.waiting);
            reg.addEventListener('updatefound', function () { guncellemeBekle(reg); });
        }).catch(function () {});
    });
})();
