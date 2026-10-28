// Backend'e giden tüm istekleri (config.js: 5500 portunda API = :5035) yakalar; gerçek sunucuya istek çıkmaz.
import * as veri from './veri.mjs';

const json = (govde, durum = 200, basliklar = {}) => ({
    status: durum,
    contentType: 'application/json',
    headers: { 'Access-Control-Allow-Origin': '*', 'Access-Control-Expose-Headers': 'X-Total-Count', ...basliklar },
    body: JSON.stringify(govde),
});

const varsayilanlar = [
    ['GET', /^\/api\/ayarlar$/, () => json({ bakimModu: false })],
    ['GET', /^\/api\/auth\/providers$/, () => json({ google: false, discord: false })],
    ['GET', /^\/api\/auth\/me$/, () => json({ message: 'Yetkisiz' }, 401)],
    ['POST', /^\/api\/auth\/login$/, () => json(veri.girisYaniti)],
    ['GET', /^\/api\/categories$/, () => json(veri.kategoriler)],
    ['GET', /^\/api\/mangas\/kategoriler$/, () => json(veri.kategoriler.map(k => k.kategoriAdi))],
    ['GET', /^\/api\/mangas\/(populer|oneri|rastgele)/, () => json(veri.mangalar)],
    ['GET', /^\/api\/mangas$/, () => json(veri.mangalar, 200, { 'X-Total-Count': String(veri.mangalar.length) })],
    ['GET', /^\/api\/mangas\/[^/]+$/, () => json(veri.mangaDetay)],
    ['GET', /^\/api\/chapters\/\d+\/pages$/, () => json(veri.bolumSayfalari)],
    ['GET', /^\/api\/chapters\/\d+\/comments/, () => json([])],
    ['GET', /^\/api\/duyurular$/, () => json([])],
];

/**
 * @param {import('@playwright/test').Page} page
 * @param {Array<[string, RegExp, (istek: import('@playwright/test').Request) => object]>} ezmeler Önce denenir.
 * @returns {{ istekler: string[] }} Yakalanan API istekleri (yöntem + yol).
 */
export async function sahteApiKur(page, ezmeler = []) {
    const istekler = [];
    const kurallar = [...ezmeler, ...varsayilanlar];
    await page.route(/^https?:\/\/(127\.0\.0\.1|localhost):5035\//, async (route) => {
        const istek = route.request();
        const yol = new URL(istek.url()).pathname;
        const yontem = istek.method();
        if (yontem === 'OPTIONS') {
            await route.fulfill({ status: 204, headers: { 'Access-Control-Allow-Origin': '*', 'Access-Control-Allow-Headers': '*', 'Access-Control-Allow-Methods': '*' } });
            return;
        }
        istekler.push(`${yontem} ${yol}`);
        const kural = kurallar.find(([y, desen]) => y === yontem && desen.test(yol));
        if (kural) { await route.fulfill(kural[2](istek)); return; }
        // Bilinmeyen uçlar: GET için boş liste, diğerleri için 404. SignalR hub'ı da burada kapanır.
        await route.fulfill(yontem === 'GET' && yol.startsWith('/api/') ? json([]) : json({ message: 'sahte: tanımsız uç' }, 404));
    });
    return { istekler };
}

/** Yakalanmamış JS hatalarını toplar; test sonunda boş olmalı. */
export function sayfaHatalari(page) {
    const hatalar = [];
    page.on('pageerror', (h) => hatalar.push(h.message));
    return hatalar;
}
