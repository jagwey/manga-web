// Kage 95 - basit servis çalışanı: uygulama kabuğu önbelleği.
// /api ve /hub istekleri asla önbelleğe alınmaz, doğrudan ağa gider.
const SURUM = 'kage95-v48';
const KABUK = [
    '/', '/index.html', '/js/yol.js', '/read.html', '/login.html', '/admin.html',
    '/logo.svg', '/favicon.svg', '/manifest.webmanifest', '/icon-192.png', '/icon-512.png'
];

self.addEventListener('install', function (e) {
    e.waitUntil(
        caches.open(SURUM)
            .then(function (c) { return Promise.all(KABUK.map(function (u) { return c.add(u).catch(function () {}); })); })
            .then(function () { return self.skipWaiting(); })
    );
});

self.addEventListener('activate', function (e) {
    e.waitUntil(
        caches.keys()
            .then(function (adlar) { return Promise.all(adlar.filter(function (a) { return a !== SURUM; }).map(function (a) { return caches.delete(a); })); })
            .then(function () { return self.clients.claim(); })
    );
});

self.addEventListener('fetch', function (e) {
    const istek = e.request;
    if (istek.method !== 'GET') return;
    const url = new URL(istek.url);
    if (url.origin !== self.location.origin) return;
    if (url.pathname.startsWith('/api') || url.pathname.startsWith('/hub') || url.pathname.indexOf('/api/') !== -1 || url.pathname.indexOf('/hub/') !== -1) return;
    if (url.pathname.indexOf('/uploads/') !== -1 || url.pathname.indexOf('/avatars/') !== -1) return;

    // Sayfa gezinme (HTML / doğrudan link / F5) istekleri
    if (istek.mode === 'navigate') {
        e.respondWith(
            fetch(istek).then(function (yanit) {
                if (yanit && (yanit.ok || yanit.status === 304)) return yanit;
                return caches.match('/index.html').then(function (c) {
                    return c || yanit;
                });
            }).catch(function () {
                return caches.match('/index.html').then(function (c) {
                    if (c) return c;
                    return caches.match('/').then(function (k) {
                        return k || new Response('<!DOCTYPE html><html><head><meta charset="utf-8"><title>Kage 95</title></head><body><script>location.reload();</script></body></html>', {
                            headers: { 'Content-Type': 'text/html; charset=utf-8' }
                        });
                    });
                });
            })
        );
        return;
    }

    // Diğer statik kaynaklar: Ağ öncelikli, başarılıysa önbelleğe al. Asla undefined/null döndürme.
    e.respondWith(
        fetch(istek).then(function (yanit) {
            if (yanit && yanit.ok) {
                const kopya = yanit.clone();
                caches.open(SURUM).then(function (c) { c.put(istek, kopya); });
                return yanit;
            }
            return yanit;
        }).catch(function () {
            return caches.match(istek).then(function (c) {
                return c || new Response('', { status: 404, statusText: 'Not Found' });
            });
        })
    );
});
