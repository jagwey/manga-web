import { test, expect } from '@playwright/test';
import { sahteApiKur } from '../sahte/api.mjs';

test('Hikâye araması sonuç ve hata durumlarını gösterir @sahte', async ({ page }) => {
  await sahteApiKur(page);
  await page.route('**/api/hikaye-arama/durum', r => r.fulfill({ json: { etkin: true } }));
  await page.route('**/api/hikaye-arama', async r => {
    if (r.request().method() !== 'POST') return r.continue();
    await r.fulfill({ status: 503, json: { message: 'Hikâye araması şu anda kullanılamıyor. Normal aramayı kullanabilirsin.' } });
  });
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Hikâyeyle Manga Bul' })).toBeVisible();
  await page.getByLabel('Manganın hikâyesi').fill('Başka bir dünyada yaşayan kahraman');
  await page.getByRole('button', { name: 'Manga bul' }).click();
  await expect(page.getByRole('region', { name: 'Hikâyeyle manga bul' }).getByRole('status')).toContainText('Normal aramayı kullanabilirsin.');
});


