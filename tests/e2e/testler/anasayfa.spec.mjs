import { test, expect } from '@playwright/test';
import { sahteApiKur, sayfaHatalari } from '../sahte/api.mjs';

test.describe('Ana sayfa @sahte', () => {
    test('manga listesini yükler ve detaya gider', async ({ page }) => {
        const hatalar = sayfaHatalari(page);
        const { istekler } = await sahteApiKur(page);

        await page.goto('/');
        await expect(page).toHaveTitle(/Kage/i);
        const kart = page.getByText('Test Ninja').first();
        await expect(kart).toBeVisible();
        expect(istekler.some(i => i.startsWith('GET /api/mangas'))).toBe(true);

        await kart.click();
        await expect(page).toHaveURL(/\/manga\/test-ninja/);
        expect(hatalar).toEqual([]);
    });

    test('bakım modunda bakım ekranını gösterir', async ({ page }) => {
        await sahteApiKur(page, [['GET', /^\/api\/ayarlar$/, () => ({ status: 200, contentType: 'application/json', body: JSON.stringify({ bakimModu: true, bakimMesaji: 'Test bakımı' }) })]]);
        await page.goto('/');
        await expect(page.locator('iframe[title="Bakım"]')).toBeVisible();
    });
});
