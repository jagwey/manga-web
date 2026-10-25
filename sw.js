// Kage 95 - basit servis çalışanı: uygulama kabuğu önbelleği.
// /api ve /hub istekleri asla önbelleğe alınmaz, doğrudan ağa gider.
const SURUM = 'kage95-v14';
const KABUK = [
    './', 'index.html', 'read.html', 'login.html', 'admin.html',
    'assets/logo.svg', 'assets/favicon.svg', 'manifest.webmanifest', 'assets/icon-192.png', 'assets/icon-512.png'
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

    // Ağ öncelikli, çevrimdışıysa önbellekten. Yalnızca gezinme (sayfa) isteklerinde index.html'e düşülür.
    e.respondWith(
        fetch(istek).then(function (yanit) {
            if (yanit && yanit.ok) {
                const kopya = yanit.clone();
                caches.open(SURUM).then(function (c) { c.put(istek, kopya); });
            }
            return yanit;
        }).catch(function () {
            return caches.match(istek).then(function (c) {
                if (c) return c;
                if (istek.mode === 'navigate') return caches.match('index.html');
                return null;
            });
        })
    );
});
