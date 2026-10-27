// index.html, admin.html, read.html ve login.html içindeki <script type="text/babel"> bloklarını derleyip küçültür,
// React'in production sürümünü sabit adresten yükler. Çıktı: <depo>/dist (yayinlamada bu yüklenir).
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';
import babel from '@babel/core';
import { transform } from 'esbuild';

const require = createRequire(import.meta.url);
const presetReact = require.resolve('@babel/preset-react');

const kok = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const dist = path.join(kok, 'dist');
fs.mkdirSync(path.join(dist, 'js'), { recursive: true });

const REACT = 'https://cdnjs.cloudflare.com/ajax/libs/react/18.3.1/umd/react.production.min.js';
const REACT_DOM = 'https://cdnjs.cloudflare.com/ajax/libs/react-dom/18.3.1/umd/react-dom.production.min.js';

for (const ad of ['index.html', 'admin.html', 'read.html', 'login.html']) {
    const ad0 = ad.replace('.html', '');
    let html = fs.readFileSync(path.join(kok, ad), 'utf8');
    html = html.replace(/<script src="https:\/\/unpkg\.com\/react@18\/umd\/react\.development\.js"[^>]*><\/script>/, `<script src="${REACT}" crossorigin="anonymous"></script>`);
    html = html.replace(/<script src="https:\/\/unpkg\.com\/react-dom@18\/umd\/react-dom\.development\.js"[^>]*><\/script>/, `<script src="${REACT_DOM}" crossorigin="anonymous"></script>`);
    html = html.replace(/[ \t]*<script src="https:\/\/unpkg\.com\/@babel\/standalone\/babel\.min\.js"><\/script>\r?\n?/, '');
    // Yerel css/js bağlantılarına içerik özeti eklenir; CDN eski dosyayı sunmaz
    html = html.replace(/(src|href)="((?:css|js)\/[\w.-]+\.(?:css|js))(?:\?[^"]*)?"/g, (tam, nit, yol) => {
        const dosya = path.join(kok, yol);
        if (!fs.existsSync(dosya)) return tam;
        return `${nit}="${yol}?v=${crypto.createHash('sha1').update(fs.readFileSync(dosya)).digest('hex').slice(0, 8)}"`;
    });

    const m = html.match(/<script type="text\/babel">([\s\S]*?)<\/script>/);
    if (!m) throw new Error(ad + ': text/babel bulunamadı');
    const js = babel.transformSync(m[1], {
        presets: [[presetReact, { runtime: 'classic' }]],
        comments: false, configFile: false, babelrc: false,
    }).code;
    const kucuk = (await transform(js, { minify: true, target: 'es2019' })).code;
    const hash = crypto.createHash('sha1').update(kucuk).digest('hex').slice(0, 8);
    fs.writeFileSync(path.join(dist, 'js', `uygulama-${ad0}.js`), kucuk);
    html = html.replace(m[0], () => `<script src="js/uygulama-${ad0}.js?v=${hash}"></script>`);
    fs.writeFileSync(path.join(dist, ad), html);
    console.log(ad, 'kaynak', m[1].length, '->', kucuk.length, 'bayt');
}
