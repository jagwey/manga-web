import { test, expect } from '@playwright/test';
import { sahteApiKur, sayfaHatalari } from '../sahte/api.mjs';

test.describe('Manga detay @sahte', () => {
    test('bilgileri ve bölüm listesini gösterir', async ({ page }) => {
        const hatalar = sayfaHatalari(page);
        const { istekler } = await sahteApiKur(page);

        await page.goto('/manga/test-ninja');
        await expect(page.getByRole('heading', { name: 'Test Ninja' }).first()).toBeVisible();
        await expect(page.getByText('Başlangıç').first()).toBeVisible();
        expect(istekler).toContain('GET /api/mangas/test-ninja');
        expect(hatalar).toEqual([]);
    });

    test('bulunamayan manga sayfayı çökertmez', async ({ page }) => {
        const hatalar = sayfaHatalari(page);
        await sahteApiKur(page, [['GET', /^\/api\/mangas\/yok-boyle$/, () => ({ status: 404, contentType: 'application/json', body: '{"message":"Bulunamadı"}' })]]);

        const yanit = page.waitForResponse(y => y.url().endsWith('/api/mangas/yok-boyle'));
        await page.goto('/manga/yok-boyle');
        await yanit;
        await expect(page.getByRole('heading', { name: 'Test Ninja' })).toHaveCount(0);
        expect(hatalar).toEqual([]);
    });
});
