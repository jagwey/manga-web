import { test, expect } from '@playwright/test';
import { sahteApiKur } from '../sahte/api.mjs';

test('Kage AI backend sonucu ve favori tercihi @sahte', async ({ page }) => {
  await sahteApiKur(page);
  await page.route('**/api/hikaye-arama/durum', r => r.fulfill({ json: { etkin: true } }));
  let request;
  const external = [];
  page.on('request', r => { if (/pollinations|groq|openrouter|generativelanguage/.test(r.url())) external.push(r.url()); });
  await page.route('**/api/hikaye-arama', async r => {
    request = r.request().postDataJSON();
    await r.fulfill({ json: { mod: 'kage-ai', sonuclar: [{ id: 7, mangaAdi: 'Katalogdaki Gizem', aciklama: '<script>bad()</script>' }] } });
  });
  await page.goto('/kesfet');
  await page.getByLabel('Manganın hikâyesi').fill('Geçmişini hatırlamayan gizemli kahraman');
  await page.getByLabel('Favorilerimi de kullan (giriş gerekir)').check();
  await page.getByRole('button', { name: 'Manga bul', exact: true }).click();
  await expect(page.getByRole('link', { name: /Katalogdaki Gizem/ })).toBeVisible();
  expect(request.favorilerimiKullan).toBe(true);
  expect(external).toEqual([]);
});

test('Kage AI kota ve boş sonuç @sahte', async ({ page }) => {
  await sahteApiKur(page);
  await page.route('**/api/hikaye-arama/durum', r => r.fulfill({ json: { etkin: true } }));
  let calls = 0;
  await page.route('**/api/hikaye-arama', r => r.fulfill(++calls === 1
    ? { json: { mod: 'kage-ai', sonuclar: [] } }
    : { status: 429, json: { message: 'Bugünkü AI arama hakkın doldu.' } }));
  await page.goto('/kesfet');
  await page.getByLabel('Manganın hikâyesi').fill('Geçmişini hatırlamayan gizemli kahraman');
  const button = page.getByRole('button', { name: 'Manga bul', exact: true });
  await button.click();
  await expect(page.locator('.hy-durum')).toContainText('uyan bir seri bulamadık');
  await button.click();
  await expect(page.locator('.hy-durum')).toContainText('hakkın doldu');
});
