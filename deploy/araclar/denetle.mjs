// Sayfalardaki <script type="text/babel"> bloklarını ve js/*.js dosyalarını tanımsız değişkenlere karşı denetler.
// Örn. bir değişkeni yanlış kapsamda kullanmak tarayıcıda ReferenceError verip tüm sayfayı siyah bırakır.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';
import babel from '@babel/core';
import { Linter } from 'eslint';

const require = createRequire(import.meta.url);
const presetReact = require.resolve('@babel/preset-react');
const kok = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');

const linter = new Linter();
const ayar = {
    env: { browser: true, es2022: true },
    parserOptions: { ecmaVersion: 2022, sourceType: 'script' },
    globals: { React: 'readonly', ReactDOM: 'readonly', module: 'readonly' },
    rules: { 'no-undef': 'error' },
};

let hataSayisi = 0;
const denetle = (ad, kod) => {
    for (const m of linter.verify(kod, ayar)) {
        hataSayisi++;
        console.error(`${ad}:${m.line}:${m.column} ${m.message}`);
    }
};

for (const ad of ['index.html', 'admin.html', 'read.html', 'login.html']) {
    const html = fs.readFileSync(path.join(kok, ad), 'utf8');
    const m = html.match(/<script type="text\/babel">([\s\S]*?)<\/script>/);
    if (!m) continue;
    const onceki = html.slice(0, m.index).split('\n').length - 1;
    const kod = babel.transformSync(m[1], {
        presets: [[presetReact, { runtime: 'classic' }]],
        retainLines: true, configFile: false, babelrc: false,
    }).code;
    denetle(ad, '\n'.repeat(onceki) + kod);
}
for (const ad of fs.readdirSync(path.join(kok, 'js')).filter(f => f.endsWith('.js'))) {
    denetle('js/' + ad, fs.readFileSync(path.join(kok, 'js', ad), 'utf8'));
}

if (hataSayisi) { console.error(`${hataSayisi} sorun bulundu.`); process.exit(1); }
console.log('Denetim temiz.');
