import { test, expect } from '@playwright/test';
import { sahteApiKur } from '../sahte/api.mjs';
test('Keşfet filtreleri ve gezinme @sahte', async ({ page, isMobile }) => {
  await sahteApiKur(page);
  const queries = [];
  await page.route('**/api/mangas?**', async r => {
    const q = new URL(r.request().url()).searchParams; queries.push(q.toString());
    const data = q.get('search') === 'yokbul' ? [] : [{ id: 8, mangaAdi: 'Bleach', slug: 'bleach' }];
    await r.fulfill({ json: data, headers: { 'Access-Control-Allow-Origin': '*', 'Access-Control-Expose-Headers': 'X-Total-Count', 'X-Total-Count': String(data.length) } });
  });
  await page.goto('/');
  const nav = isMobile ? page.getByRole('navigation', { name: 'Mobil gezinme' }) : page.locator('header');
  await nav.getByRole('link', { name: 'Keşfet' }).click();
  await expect(page.getByRole('heading', { name: 'Keşfet', exact: true })).toBeVisible();
  await expect(page.getByText('Bleach', { exact: true }).first()).toBeVisible();
  await page.getByLabel('Manga adı').fill('yokbul');
  await expect(page.getByText('Eşleşen manga bulunamadı')).toBeVisible();
  expect(queries.some(q => q.includes('search=yokbul'))).toBeTruthy();
  await page.getByRole('button', { name: 'Filtreleri temizle' }).first().click();
  await expect(page.getByText('Bleach', { exact: true }).first()).toBeVisible();
  await page.getByRole('combobox', { name: 'Tür', exact: true }).selectOption({ index: 1 });
  await expect.poll(() => queries.some(q => q.includes('category='))).toBeTruthy();
});


test('Ana sayfadaki kısa kart Keşfet aramasını açar @sahte', async ({ page }) => {
  await sahteApiKur(page);
  await page.route('**/api/hikaye-arama/durum', r => r.fulfill({ json: { etkin: true } }));
  await page.goto('/');
  const mini = page.locator('#hikaye-mini');
  await expect(mini).toBeVisible();
  await expect(page.getByLabel('Manganın hikâyesi')).toBeHidden();
  await mini.getByRole('button', { name: /Hikâyeyle manga bul/ }).click();
  await expect(page.getByLabel('Manganın hikâyesi')).toBeVisible();
  await page.getByLabel('Manganın hikâyesi').fill('Geçmişini hatırlamayan gizemli karakter');
  await page.getByRole('button', { name: 'Hikâye aramasını küçült' }).click();
  await expect(page.getByLabel('Manganın hikâyesi')).toBeHidden();
  await mini.getByRole('button', { name: /Hikâyeyle manga bul/ }).click();
  await expect(page.getByLabel('Manganın hikâyesi')).toHaveValue('Geçmişini hatırlamayan gizemli karakter');
  await page.getByRole('button', { name: 'Hikâye aramasını küçült' }).click();
  await mini.getByRole('link', { name: /Keşfet’e git/ }).click();
  await expect(page.getByRole('heading', { name: 'Hikâyeyle Manga Bul' })).toBeVisible();
  await expect(page.getByLabel('Manganın hikâyesi')).toBeVisible();
});
