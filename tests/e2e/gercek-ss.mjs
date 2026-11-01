import { chromium } from '@playwright/test';
const b = await chromium.launch();
for (const [ad, w, h] of [['m', 1280, 900], ['mob', 390, 844]]) {
  for (const t of ['kage','kagit','mono','gece','pop']) {
    const c = await b.newContext({ viewport: { width: w, height: h }, serviceWorkers: 'block', locale: 'tr-TR' });
    const p = await c.newPage();
    await p.addInitScript((t) => { window.KAGE95_HIKAYE_AI = true; try { localStorage.setItem('kage95_tema', t); } catch {} }, t);
    await p.goto('http://127.0.0.1:5500/index.html');
    await p.addScriptTag({ url: '/js/hikaye-ai.js' });
    const k = p.getByRole('region', { name: 'Hikâyeyle manga bul' });
    await k.waitFor({ timeout: 20000 });
    await k.getByLabel('Manganın hikâyesi').fill('Sert bir savaşçı, vikinglerin intikamı ve savaşın anlamsızlığı');
    await k.getByRole('button', { name: 'Manga bul' }).click();
    const s = await k.getByRole('status').textContent().catch(() => '');
    await k.getByText(/öneri bulundu|bulamadık|yapılamadı/).waitFor({ timeout: 60000 }).catch(() => {});
    console.log(t, ad, await p.evaluate(() => document.documentElement.dataset.tema), '|', (await k.getByRole('status').textContent()).trim(), '|', (await k.locator('.hy-kaynak').textContent()).trim());
    await k.screenshot({ path: `${process.env.TEMP}/g-${t}-${ad}.png` });
    await c.close();
  }
}
await b.close();
