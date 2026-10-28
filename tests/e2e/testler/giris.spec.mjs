import { test, expect } from '@playwright/test';
import { sahteApiKur, sayfaHatalari } from '../sahte/api.mjs';

// Sahte kimlik bilgileri; gerçek hesap değildir.
const KULLANICI = 'testci';
const SIFRE = 'sahte-sifre-123';
const gonder = (page) => page.locator('form:has(#sifreInput) button[type="submit"]');

test.describe('Giriş @sahte', () => {
    test('başarılı girişte token kaydedilir', async ({ page }) => {
        const hatalar = sayfaHatalari(page);
        let govde = null;
        await sahteApiKur(page, [['POST', /^\/api\/auth\/login$/, (istek) => {
            govde = istek.postDataJSON();
            return { status: 200, contentType: 'application/json', body: JSON.stringify({ token: 'sahte.jwt.token', id: 7, kullaniciAdi: KULLANICI, email: 'testci@example.test', rol: 'Uye', yetkiler: [], emailDogrulandi: true }) };
        }]]);

        await page.goto('/login.html');
        await page.locator('#emailInput').fill(KULLANICI);
        await page.locator('#sifreInput').fill(SIFRE);
        await gonder(page).click();

        await expect(page.getByText(/Hoş geldiniz/)).toBeVisible();
        expect(govde).toEqual({ kullaniciAdiOrEmail: KULLANICI, sifre: SIFRE });
        expect(await page.evaluate(() => localStorage.getItem('kage95_token'))).toBe('sahte.jwt.token');
        expect(hatalar).toEqual([]);
    });

    test('hatalı girişte sunucu mesajını gösterir', async ({ page }) => {
        await sahteApiKur(page, [['POST', /^\/api\/auth\/login$/, () => ({ status: 401, contentType: 'application/json', body: JSON.stringify({ message: 'Kullanıcı adı veya şifre hatalı.' }) })]]);

        await page.goto('/login.html');
        await page.locator('#emailInput').fill(KULLANICI);
        await page.locator('#sifreInput').fill('yanlis');
        await gonder(page).click();

        await expect(page.getByText('Kullanıcı adı veya şifre hatalı.')).toBeVisible();
        expect(await page.evaluate(() => localStorage.getItem('kage95_token'))).toBeNull();
    });
});
