import { test, expect } from '@playwright/test';
import { sahteApiKur, sayfaHatalari } from '../sahte/api.mjs';

test.describe('Okuyucu @sahte', () => {
    test('bölüm sayfalarını gösterir', async ({ page }) => {
        const hatalar = sayfaHatalari(page);
        const { istekler } = await sahteApiKur(page);

        await page.goto('/read.html?chapter=101');
        await expect(page.locator('img[src^="data:image/svg+xml"]').first()).toBeVisible();
        expect(istekler).toContain('GET /api/chapters/101/pages');
        expect(hatalar).toEqual([]);
    });

    test('API hatasında hata durumunu gösterir, çökmez', async ({ page }) => {
        const hatalar = sayfaHatalari(page);
        await sahteApiKur(page, [['GET', /^\/api\/chapters\/\d+\/pages$/, () => ({ status: 500, contentType: 'application/json', body: '{}' })]]);

        await page.goto('/read.html?chapter=101');
        await expect(page.getByRole('alert').getByText('Bölüm açılamadı')).toBeVisible();
        await expect(page.locator('img[src^="data:image/svg+xml"]')).toHaveCount(0);
        expect(hatalar).toEqual([]);
    });
});
