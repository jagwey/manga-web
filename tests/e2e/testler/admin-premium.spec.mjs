import { test, expect } from '@playwright/test';
import { sahteApiKur } from '../sahte/api.mjs';
test('Yönetici kendi hesabına Premium verebilir @sahte', async ({ page }) => {
  const user = { id: 1, kullaniciAdi: 'sahip', email: 'sahip@example.test', rol: 'Owner', aktif: true, yetkiler: ['*'], yorumSayisi: 0 };
  let active = false, sent = null;
  await sahteApiKur(page, [
    ['GET', /^\/api\/admin\/ben$/, () => ({ status: 200, json: user })],
    ['GET', /^\/api\/admin\/kullanicilar$/, () => ({ status: 200, json: [user], headers: { 'X-Total-Count': '1' } })],
    ['GET', /^\/api\/admin\/kullanicilar\/1$/, () => ({ status: 200, json: user })],
    ['GET', /^\/api\/admin\/kullanicilar\/1\/premium$/, () => ({ status: 200, json: { aktif: active, bitisUtc: '2027-01-01T00:00:00Z' } })],
    ['POST', /^\/api\/admin\/kullanicilar\/1\/premium$/, r => { sent = r.postDataJSON(); active = true; return { status: 200, json: { aktif: true, bitisUtc: '2027-01-01T00:00:00Z' } }; }],
  ]);
  await page.addInitScript(() => localStorage.setItem('kage95_token', 'test-token'));
  await page.goto('/admin.html#kullanicilar');
  await page.getByRole('button', { name: 'sahip ayrıntıları' }).click();
  await page.getByRole('button', { name: 'Premium yönet' }).click();
  await expect(page.getByText('Aktif Premium yok.')).toBeVisible();
  await page.getByLabel('Eklenecek gün').fill('60');
  await page.getByRole('button', { name: 'Premium ver / uzat' }).click();
  await expect(page.getByText(/Premium aktif · Bitiş/)).toBeVisible();
  expect(sent.gun).toBe(60);
});
