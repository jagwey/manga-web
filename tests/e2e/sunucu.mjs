// Testler için bağımlılıksız statik sunucu: depo kökünü sunar, uzantısız yolları (/manga/slug) index.html'e yönlendirir.
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const kok = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const port = Number(process.env.E2E_PORT || 5500);
// Yapılandırma ve kaynak klasörleri sunulmaz (deploy/ üretim ayarı içerir)
const YASAK = new Set(['backend', 'deploy', 'tests', 'node_modules', 'publish']);
const turler = {
    '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.mjs': 'text/javascript; charset=utf-8',
    '.css': 'text/css; charset=utf-8', '.json': 'application/json', '.webmanifest': 'application/manifest+json',
    '.svg': 'image/svg+xml', '.png': 'image/png', '.ico': 'image/x-icon', '.jpg': 'image/jpeg', '.webp': 'image/webp',
    '.txt': 'text/plain; charset=utf-8', '.xml': 'application/xml',
};

http.createServer((istek, yanit) => {
    let yol;
    try { yol = decodeURIComponent(new URL(istek.url, 'http://x').pathname); } catch { yanit.writeHead(400).end(); return; }
    if (yol === '/') yol = '/index.html';
    if (!path.extname(yol)) yol = '/index.html';
    const dosya = path.resolve(kok, '.' + yol);
    const ilkKlasor = path.relative(kok, dosya).split(path.sep)[0];
    if (!dosya.startsWith(kok + path.sep) || ilkKlasor.startsWith('.') || YASAK.has(ilkKlasor) || !fs.existsSync(dosya) || !fs.statSync(dosya).isFile()) {
        yanit.writeHead(404).end();
        return;
    }
    yanit.writeHead(200, { 'Content-Type': turler[path.extname(dosya).toLowerCase()] || 'application/octet-stream', 'Cache-Control': 'no-store' });
    fs.createReadStream(dosya).pipe(yanit);
}).listen(port, '127.0.0.1', () => console.log(`E2E sunucusu http://127.0.0.1:${port}`));
