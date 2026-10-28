import { test, expect } from '@playwright/test';
import { sahteApiKur, sayfaHatalari } from '../sahte/api.mjs';

// [ad, yol, sayfanın yüklendiğini gösteren öğe]
const sayfalar = [
    ['ana sayfa', '/', (p) => p.getByText('Test Ninja').first()],
    ['manga detay', '/manga/test-ninja', (p) => p.getByText('Başlangıç').first()],
    ['okuyucu', '/read.html?chapter=101', (p) => p.locator('img[src^="data:image/svg+xml"]').first()],
    ['giriş', '/login.html', (p) => p.locator('#emailInput')],
];

test.describe('Mobil görünüm @sahte', () => {
    test.skip(({ isMobile }) => !isMobile, 'Yalnızca mobil projede çalışır');

    for (const [ad, yol, hazir] of sayfalar) {
        test(`${ad}: yatay taşma yok`, async ({ page }) => {
            const hatalar = sayfaHatalari(page);
            await sahteApiKur(page);
            await page.goto(yol);
            await expect(hazir(page)).toBeVisible();

            const tasma = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
            expect(tasma, 'sayfa ekran genişliğinden taşıyor').toBeLessThanOrEqual(1);
            expect(hatalar).toEqual([]);
        });
    }
});
