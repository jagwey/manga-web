import { test, expect } from '@playwright/test';
import { sahteApiKur } from '../sahte/api.mjs';
test('Premium fiyatı, kapalı ödeme ve sayfa sorusu @sahte', async ({ page }) => {
  await sahteApiKur(page);
  await page.addInitScript(() => localStorage.setItem('kage95_token', 'test-token'));
  await page.route('**/api/premium/durum', r => r.fulfill({ json: { premium: true } }));
  let sent;
  await page.route('**/api/premium/bolum/11/sor', r => {
    sent = r.request().postDataJSON();
    return r.fulfill({ json: { sayfa: 2, yanit: 'Bu sayfada karakter şaşırmış görünüyor.' } });
  });
  await page.goto('/premium.html?chapter=11');
  await expect(page.getByRole('button', { name: 'Premium yakında' })).toBeDisabled();
  await expect(page.locator('.fiyat')).toContainText('9,99 TL');
  await expect(page.locator('#uyelik')).toContainText('üyeliğin aktif');
  await page.getByLabel('Sayfa numarası').fill('2');
  await page.getByLabel('Bu sayfa hakkında sorun').fill('Karakter burada neden şaşırıyor?');
  await page.getByRole('button', { name: 'Sayfayı AI’ya sor' }).click();
  await expect(page.locator('#yanit')).toContainText('karakter şaşırmış');
  expect(sent.sayfa).toBe(2);
  await page.screenshot({ path: test.info().outputPath('premium.png'), fullPage: true });
});
test('Premium olmayan üyeye erişim mesajı @sahte', async ({ page }) => {
  await sahteApiKur(page);
  await page.addInitScript(() => localStorage.setItem('kage95_token', 'test-token'));
  await page.route('**/api/premium/durum', r => r.fulfill({ json: { premium: false } }));
  await page.route('**/api/premium/bolum/11/indir', r => r.fulfill({ status: 403, json: { message: 'Aktif premium üyelik gerekir.' } }));
  await page.goto('/premium.html?chapter=11');
  await page.getByRole('button', { name: 'Bölümü ZIP indir' }).click();
  await expect(page.locator('#yanit')).toContainText('premium üyelik gerekir');
});
