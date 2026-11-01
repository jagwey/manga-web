import { test, expect } from '@playwright/test';
import { sahteApiKur } from '../sahte/api.mjs';

async function panelAc(page, yzYaniti) {
  await sahteApiKur(page);
  await page.route('https://text.pollinations.ai/**', yzYaniti);
  await page.addInitScript(() => { window.KAGE95_HIKAYE_AI = true; });
  await page.goto('/');
  await page.addScriptTag({ url: '/js/hikaye-ai.js' });
  const panel = page.getByRole('region', { name: 'Hikâyeyle manga bul' });
  await expect(panel).toBeVisible();
  await panel.getByLabel('Manganın hikâyesi').fill('Sahte test mangası, bir ninja maceraya atılır');
  await panel.getByRole('button', { name: 'Manga bul' }).click();
  return panel;
}

test('Hikâye araması: YZ sonucu gösterilir @sahte @ai', async ({ page }) => {
  const panel = await panelAc(page, (r) => r.fulfill({
    json: { choices: [{ message: { content: JSON.stringify({ sonuclar: [{ id: 1, neden: 'Ninja macerası anlatılıyor.' }] }) } }] },
  }));
  await expect(panel.getByRole('status')).toContainText('1 öneri bulundu');
  await expect(panel.getByText('Test Ninja')).toBeVisible();
  await expect(panel.getByText('Ninja macerası anlatılıyor.')).toBeVisible();
});

test('Hikâye araması: YZ çökerse kelime eşleşmesine düşer @sahte @ai', async ({ page }) => {
  const panel = await panelAc(page, (r) => r.fulfill({ status: 429, body: 'limit' }));
  await expect(panel.getByRole('status')).toContainText('öneri bulundu');
  await expect(panel.getByText('Test Ninja')).toBeVisible();
  await expect(panel.getByText('kelime eşleşmeleri')).toBeVisible();
});
