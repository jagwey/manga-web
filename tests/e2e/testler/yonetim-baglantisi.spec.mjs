import { test, expect } from '@playwright/test';
import { sahteApiKur } from '../sahte/api.mjs';

const sahip = { id: 1, kullaniciAdi: 'sahip', email: 'sahip@example.test', rol: 'Owner', yetkiler: ['*'], emailDogrulandi: true };

test.describe('Yönetim paneli bağlantısı @sahte', () => {
    // Hata service worker üzerinden oluşuyordu; bu testlerde SW açık olmalı
    test.use({ serviceWorkers: 'allow' });

    test.beforeEach(async ({ page }) => {
        await sahteApiKur(page, [['GET', /^\/api\/auth\/me$/, () => ({ status: 200, contentType: 'application/json', body: JSON.stringify(sahip) })]]);
        await page.addInitScript((k) => {
            localStorage.setItem('kage95_token', 'sahte.jwt.token');
            localStorage.setItem('kage95_user', JSON.stringify(k));
        }, sahip);
        await page.goto('/');
        await page.waitForFunction(() => navigator.serviceWorker.ready.then(() => true));
        await page.reload();
        await page.waitForFunction(() => !!navigator.serviceWorker.controller);
    });

    const paneliAc = async (page) => {
        await page.locator('button.profil-chip').click();
        await page.getByRole('menuitem', { name: 'Yönetim paneli' }).click();
    };

    test('menüden yönetim paneline gider', async ({ page }) => {
        await paneliAc(page);
        await expect(page).toHaveURL(/\/admin\.html$/);
        await expect(page).toHaveTitle(/Yönetim paneli/);
    });

    test('sunucu hata verirse SW ana sayfayı "Sayfa bulunamadı" olarak göstermez', async ({ page, context }) => {
        let ilk = true;
        await context.route('**/admin.html', async (route) => {
            if (ilk) { ilk = false; await route.fulfill({ status: 503, contentType: 'text/html; charset=utf-8', body: '<title>Bakımda</title>503' }); return; }
            await route.continue();
        });

        await paneliAc(page);
        await expect(page).toHaveURL(/\/admin\.html$/);
        await expect(page.getByText('Sayfa bulunamadı')).toHaveCount(0);
        await expect(page).toHaveTitle('Bakımda');

        await page.reload();
        await expect(page).toHaveTitle(/Yönetim paneli/);
    });
});
