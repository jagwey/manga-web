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

