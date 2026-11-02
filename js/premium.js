(() => {
  'use strict';
  const api = (window.KAGE95_API_URL || '/api').replace(/\/$/, '');
  const tokenAl = () => {
    for (const store of ['localStorage', 'sessionStorage']) for (const key of ['kage95_token', 'token', 'jwt']) {
      try { const token = window[store].getItem(key); if (token) return token; } catch { /* kapalı depolama */ }
    }
    return null;
  };
  async function istek(path, options = {}) {
    const token = tokenAl();
    if (!token) throw new Error('Premium özelliklerini kullanmak için giriş yap.');
    const r = await fetch(api + path, { ...options, signal: AbortSignal.timeout(30_000), headers: { Authorization: `Bearer ${token}`, ...(options.body ? { 'Content-Type': 'application/json' } : {}) } });
    if (!r.ok) {
      const data = await r.json().catch(() => ({}));
      throw new Error(data.message || (r.status === 401 ? 'Oturumun sona ermiş. Yeniden giriş yap.' : 'İşlem şu anda tamamlanamıyor.'));
    }
    return r;
  }
  async function indir(path, options, fileName) {
    const response = await istek(path, options);
    const url = URL.createObjectURL(await response.blob());
    const a = document.createElement('a'); a.href = url; a.download = fileName; a.click();
    setTimeout(() => URL.revokeObjectURL(url), 60000);
  }
  window.KageIndirme = {
    async tek(id) {
      try { await indir(`/premium/bolum/${id}/indir`, {}, `kage95-bolum-${id}.zip`); }
      catch (err) { alert(err.message); }
    },
    sec(bolumler) {
      const d = document.createElement('dialog');
      d.style.cssText = 'max-width:460px;width:90%;max-height:80vh;background:var(--bg-kart);color:var(--text-ana);border:1px solid var(--border-renk);border-radius:12px;padding:20px';
      const h = document.createElement('h2'); h.textContent = 'Bölümleri seç'; d.append(h);
      const info = document.createElement('p'); info.textContent = 'En fazla 20 bölüm seçebilirsin. Premium üyelik gerekir.'; d.append(info);
      const list = document.createElement('div'); list.style.cssText = 'max-height:45vh;overflow:auto';
      for (const b of bolumler) { const label = document.createElement('label'); label.style.cssText = 'display:block;padding:8px'; const c = document.createElement('input'); c.type = 'checkbox'; c.value = b.id; label.append(c, document.createTextNode(` Bölüm ${b.bolumNo}${b.bolumAdi ? ' · ' + b.bolumAdi : ''}`)); list.append(label); }
      d.append(list);
      const status = document.createElement('p'); status.setAttribute('role', 'status'); d.append(status);
      const download = document.createElement('button'); download.className = 'btn-ana'; download.textContent = 'Seçilenleri indir';
      download.onclick = async () => { const ids = [...list.querySelectorAll('input:checked')].map(x => Number(x.value)); if (!ids.length || ids.length > 20) { status.textContent = '1–20 bölüm seç.'; return; } download.disabled = true; status.textContent = 'İndirme hazırlanıyor…'; try { await indir('/premium/toplu-indir', { method: 'POST', body: JSON.stringify({ bolumler: ids }) }, 'kage95-bolumler.zip'); d.close(); } catch (e) { status.textContent = e.message; } finally { download.disabled = false; } };
      const close = document.createElement('button'); close.className = 'btn-ikincil'; close.textContent = 'Kapat'; close.onclick = () => d.close(); d.append(download, close); d.addEventListener('close', () => d.remove()); document.body.append(d); d.showModal();
    }
  };
  const giris = document.querySelector('#premium-giris');
  if (giris && tokenAl()) giris.hidden = true;
  const durum = document.querySelector('#uyelik');
  if (!durum) {
    if (!location.pathname.endsWith('read.html')) return;
    const a = document.createElement('a');
    a.textContent = location.pathname.endsWith('read.html') ? 'Premium · Bölüm araçları' : 'Premium · 9,99 TL/ay';
    a.href = '/premium.html' + location.search;
    a.style.cssText = 'display:block;text-align:center;padding:12px;background:#211c32;color:#e2d6ff';
    document.body.prepend(a);
    return;
  }
  istek('/premium/durum').then(r => r.json()).then(v => { durum.textContent = v.premium ? 'Premium üyeliğin aktif.' : 'Aktif premium üyeliğin bulunmuyor.'; }).catch(e => { durum.textContent = e.message; });
  const id = Number(new URLSearchParams(location.search).get('chapter'));
  if (!Number.isSafeInteger(id) || id < 1) return;
  document.querySelector('#bolum-araclari').hidden = false;
  document.querySelector('#yonlendirme').hidden = true;
  document.querySelector('#bolum-baslik').textContent = 'Okumakta olduğun bölüm için araçlar';
  const output = document.querySelector('#yanit');
  document.querySelector('#indir').addEventListener('click', async e => {
    e.target.disabled = true; output.textContent = 'İndirme hazırlanıyor…';
    try {
      const response = await istek(`/premium/bolum/${id}/indir`);
      const url = URL.createObjectURL(await response.blob());
      const a = document.createElement('a'); a.href = url; a.download = `kage95-bolum-${id}.zip`; a.click();
      setTimeout(() => URL.revokeObjectURL(url), 60_000); output.textContent = 'İndirme hazır.';
    } catch (err) { output.textContent = err.message; }
    finally { e.target.disabled = false; }
  });
  document.querySelector('#soru-form').addEventListener('submit', async e => {
    e.preventDefault(); const button = document.querySelector('#sor'); button.disabled = true; output.textContent = 'Seçtiğin sayfa okunuyor…';
    try {
      const response = await istek(`/premium/bolum/${id}/sor`, { method: 'POST', body: JSON.stringify({ soru: document.querySelector('#soru').value.trim(), sayfa: Number(document.querySelector('#sayfa').value) }) });
      const result = await response.json(); output.textContent = `Sayfa ${result.sayfa}\n${result.yanit}`;
    } catch (err) { output.textContent = err.message; }
    finally { button.disabled = false; }
  });
})();
