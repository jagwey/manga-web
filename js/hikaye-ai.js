(async () => {
  'use strict';
  if (!window.KAGE95_HIKAYE_AI) return;
  const api = (window.KAGE95_API_URL || '/api').replace(/\/$/, '');
  try {
    const r = await fetch(`${api}/hikaye-arama/durum`);
    if (!r.ok || !(await r.json()).etkin) return;
  } catch { return; }
  const ORNEKLER = [
    'Ana karakterin geçmişini hatırlamadığı, gizem ve psikolojik gerilim içeren bir manga arıyorum',
    'Hayatta kalmak için şeytanlarla savaşan genç bir kılıç ustası',
    'Sıradan bir çalışan bir anda en güçlü avcıya dönüşüyor',
  ];

  const CSS = `
  #ucretsiz-hikaye-ai{margin:24px 0;padding:clamp(16px,3vw,28px);border:1px solid var(--border-renk);border-radius:14px;background:var(--bg-kart,var(--bg-yuzey));position:relative;overflow:hidden}
  #ucretsiz-hikaye-ai::before{content:"";position:absolute;inset:0 0 auto 0;height:3px;background:linear-gradient(90deg,var(--vurgu-rengi),transparent)}
  #ucretsiz-hikaye-ai .hy-baslik{display:flex;align-items:center;gap:10px;flex-wrap:wrap;margin:0 0 4px}
  #ucretsiz-hikaye-ai .hy-baslik h2{margin:0}
  #ucretsiz-hikaye-ai .hy-rozet{font-size:12px;font-weight:700;letter-spacing:.04em;padding:3px 9px;border-radius:999px;border:1px solid var(--vurgu-rengi);color:var(--vurgu-parlak,var(--vurgu-rengi))}
  #ucretsiz-hikaye-ai .hy-alt{margin:0 0 14px;color:var(--text-ikincil)}
  #ucretsiz-hikaye-ai .hy-alan{position:relative}
  #ucretsiz-hikaye-ai .hy-ust{display:flex;align-items:center;gap:14px;margin-bottom:18px}
  #ucretsiz-hikaye-ai .hy-simge{width:44px;height:44px;display:grid;place-items:center;border:1px solid var(--border-renk);border-radius:13px;color:var(--vurgu-parlak);font-size:22px;background:var(--bg-yuzey)}
  #ucretsiz-hikaye-ai .hy-tercih{display:flex;align-items:center;gap:9px;color:var(--text-ikincil);font-size:14px;cursor:pointer}
  #ucretsiz-hikaye-ai .hy-tercih input{accent-color:var(--vurgu-rengi);width:16px;height:16px}
  #ucretsiz-hikaye-ai .hy-gizlilik{font-size:12px;color:var(--text-ikincil);margin:10px 0 0}
  #ucretsiz-hikaye-ai .hy-ornek-baslik{font-size:12px;color:var(--text-ikincil);margin:18px 0 0;font-weight:600}
  #ucretsiz-hikaye-ai .hy-liste:empty,#ucretsiz-hikaye-ai .hy-durum:empty{display:none}
  #ucretsiz-hikaye-ai textarea{width:100%;box-sizing:border-box;min-height:96px;padding:12px 14px 28px;border-radius:10px;resize:vertical;color:var(--text-ana);background:var(--bg-ana);border:1px solid var(--border-renk);font:inherit;font-size:16px;transition:border-color .15s}
  #ucretsiz-hikaye-ai textarea:focus{outline:2px solid var(--vurgu-rengi);outline-offset:1px;border-color:var(--vurgu-rengi)}
  #ucretsiz-hikaye-ai .hy-say{position:absolute;right:12px;bottom:8px;font-size:12px;color:var(--text-ikincil);pointer-events:none}
  #ucretsiz-hikaye-ai .hy-satir{display:flex;align-items:center;gap:10px;flex-wrap:wrap;margin-top:12px}
  #ucretsiz-hikaye-ai .hy-ornek{display:flex;gap:8px;flex-wrap:wrap;margin-top:10px}
  #ucretsiz-hikaye-ai .hy-ornek button{font:inherit;font-size:13px;padding:6px 12px;border-radius:999px;border:1px solid var(--border-renk);background:transparent;color:var(--text-ikincil);cursor:pointer;text-align:left}
  #ucretsiz-hikaye-ai .hy-ornek button:hover,#ucretsiz-hikaye-ai .hy-ornek button:focus-visible{border-color:var(--vurgu-rengi);color:var(--text-ana)}
  #ucretsiz-hikaye-ai .hy-durum{margin:14px 0 0;min-height:1.4em;color:var(--text-ikincil)}
  #ucretsiz-hikaye-ai .hy-durum.hata{color:var(--vurgu-parlak,var(--vurgu-rengi))}
  #ucretsiz-hikaye-ai .hy-liste{display:grid;gap:12px;margin:14px 0 0;padding:0;list-style:none;grid-template-columns:repeat(auto-fill,minmax(min(100%,340px),1fr))}
  #ucretsiz-hikaye-ai .hy-kart{display:flex;gap:12px;padding:10px;border:1px solid var(--border-renk);border-radius:12px;background:var(--bg-ana);color:inherit;text-decoration:none;transition:border-color .15s,transform .15s;animation:hy-gir .35s ease-out both}
  #ucretsiz-hikaye-ai .hy-kart:hover,#ucretsiz-hikaye-ai .hy-kart:focus-visible{border-color:var(--vurgu-rengi);transform:translateY(-2px)}
  #ucretsiz-hikaye-ai .hy-kapak{flex:0 0 64px;aspect-ratio:2/3;border-radius:8px;background:var(--bg-yuzey);overflow:hidden;position:relative}
  #ucretsiz-hikaye-ai .hy-kapak img{width:100%;height:100%;object-fit:cover;display:block}
  #ucretsiz-hikaye-ai .hy-sira{position:absolute;left:4px;top:4px;min-width:20px;height:20px;border-radius:6px;background:var(--vurgu-rengi);color:#fff;font-size:12px;font-weight:700;display:grid;place-items:center}
  #ucretsiz-hikaye-ai .hy-metin{min-width:0;display:flex;flex-direction:column;gap:4px}
  #ucretsiz-hikaye-ai .hy-ad{font-weight:700;color:var(--text-ana);overflow-wrap:anywhere}
  #ucretsiz-hikaye-ai .hy-neden{font-size:14px;color:var(--text-ikincil);display:-webkit-box;-webkit-line-clamp:3;-webkit-box-orient:vertical;overflow:hidden}
  #ucretsiz-hikaye-ai .hy-iskelet{height:96px;border-radius:12px;background:linear-gradient(90deg,var(--bg-yuzey),var(--border-renk),var(--bg-yuzey));background-size:200% 100%;animation:hy-kay 1.2s linear infinite}
  #ucretsiz-hikaye-ai .hy-kaynak{margin:12px 0 0;font-size:12px;color:var(--text-ikincil)}
  @keyframes hy-gir{from{opacity:0;transform:translateY(8px)}}
  @keyframes hy-kay{to{background-position:-200% 0}}
  @media (prefers-reduced-motion:reduce){#ucretsiz-hikaye-ai *{animation:none!important;transition:none!important}}`;

  function tokenAl() {
    for (const depo of ['localStorage', 'sessionStorage']) {
      for (const key of ['kage95_token', 'token', 'jwt']) {
        try { const token = window[depo].getItem(key); if (token) return token; } catch { /* kapalı depolama */ }
      }
    }
    return null;
  }

  function baslat() {
    if (document.getElementById('ucretsiz-hikaye-ai')) return;
    const hedef = document.querySelector('#seriler');
    if (!hedef) return;
    if (!document.getElementById('hikaye-ai-css')) {
      const st = document.createElement('style'); st.id = 'hikaye-ai-css'; st.textContent = CSS; document.head.append(st);
    }
    const kutu = document.createElement('section');
    kutu.id = 'ucretsiz-hikaye-ai';
    kutu.setAttribute('aria-label', 'Hikâyeyle manga bul');
    kutu.innerHTML = `<div class="hy-ust"><span class="hy-simge" aria-hidden="true">✦</span><div><div class="hy-baslik"><h2>Hikâyeyle Manga Bul</h2><span class="hy-rozet">KEŞFET</span></div><p class="hy-alt">Hatırladığın karakteri, dünyayı veya bir sahneyi anlat.</p></div></div>
      <form><div class="hy-alan"><textarea aria-label="Manganın hikâyesi" minlength="10" maxlength="500" required placeholder="Örneğin: Başka bir dünyada uyanan bir kahraman…"></textarea><span class="hy-say" aria-hidden="true">0/500</span></div>
      <div class="hy-satir"><button class="btn-ana" type="submit">Manga bul</button><span class="manga-meta">En az 10 karakter. Yalnızca kataloğumuzdaki mangalar önerilir.</span></div>
      <label class="hy-tercih"><input type="checkbox" class="hy-favori"> <span class="hy-favori-yazi"></span></label><p class="hy-gizlilik">Hikâye tarifi ve seçersen favori manga bilgileri öneri için AI sağlayıcısına gönderilir.</p>
      <p class="hy-ornek-baslik">Bir örnekle başla</p><div class="hy-ornek" role="group" aria-label="Örnek hikâyeler"></div></form>
      <p class="hy-durum" role="status" aria-live="polite"></p>
      <ul class="hy-liste" aria-label="Bulunan seriler"></ul>
      <p class="hy-kaynak" hidden></p>`;
    hedef.prepend(kutu);
    const $ = (s) => kutu.querySelector(s);
    const form = $('form'), alan = $('textarea'), say = $('.hy-say'), durum = $('.hy-durum'), liste = $('.hy-liste'), kaynak = $('.hy-kaynak'), dugme = form.querySelector('button[type=submit]');
    $('.hy-favori-yazi').textContent = tokenAl() ? 'Favorilerimi de kullan' : 'Favorilerimi de kullan (giriş gerekir)';
    const guncelleSay = () => { say.textContent = `${alan.value.length}/500`; };
    alan.addEventListener('input', guncelleSay);
    for (const o of ORNEKLER) {
      const b = document.createElement('button'); b.type = 'button'; b.textContent = o;
      b.addEventListener('click', () => { alan.value = o; guncelleSay(); alan.focus(); });
      $('.hy-ornek').append(b);
    }

    const hata = (m) => { durum.textContent = m; durum.classList.add('hata'); };
    let onceki;
    form.addEventListener('submit', async e => {
      e.preventDefault();
      onceki?.abort(); onceki = new AbortController();
      dugme.disabled = true; liste.replaceChildren(); kaynak.hidden = true; durum.classList.remove('hata');
      for (let i = 0; i < 3; i++) { const s = document.createElement('li'); s.className = 'hy-iskelet'; liste.append(s); }
      const hikaye = alan.value.trim();
      try {
        durum.textContent = 'Katalogda uygun mangalar aranıyor…';
        const token = tokenAl();
        const r = await fetch(`${api}/hikaye-arama`, {
          method: 'POST', signal: AbortSignal.any([onceki.signal, AbortSignal.timeout(30_000)]),
          headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
          body: JSON.stringify({ metin: hikaye, favorilerimiKullan: $('.hy-favori').checked })
        });
        const data = await r.json().catch(() => ({}));
        if (!r.ok) throw new Error(data.message || (r.status === 429 ? 'Arama sınırına ulaştın. Bir süre sonra yeniden dene.' : r.status === 401 ? 'Oturum açıp yeniden dene.' : 'Arama şu anda yapılamadı. Normal aramayı kullanabilirsin.'));
        if (!Array.isArray(data.sonuclar)) throw new Error('Arama yanıtı geçersiz.');
        const yz = data.mod !== 'kelime';
        const sonuc = data.sonuclar.map(m => ({ m, neden: '' }));
        liste.replaceChildren();
        if (!sonuc.length) { durum.textContent = 'Bu hikâyeye uyan bir seri bulamadık. Karakter, dünya ya da olay gibi daha fazla ayrıntı eklemeyi dene.'; return; }
        sonuc.forEach(({ m, neden }, i) => {
          const li = document.createElement('li');
          const a = document.createElement('a'); a.className = 'hy-kart'; a.style.animationDelay = `${i * 60}ms`;
          a.href = typeof window.mangaLink === 'function' ? window.mangaLink(m) : `#/manga/${encodeURIComponent(m.slug || m.id)}`;
          const kapak = document.createElement('span'); kapak.className = 'hy-kapak';
          if (m.kapakResmiURL) { const im = document.createElement('img'); im.src = m.kapakResmiURL; im.alt = ''; im.loading = 'lazy'; im.onerror = () => im.remove(); kapak.append(im); }
          const sira = document.createElement('span'); sira.className = 'hy-sira'; sira.textContent = i + 1; sira.setAttribute('aria-hidden', 'true'); kapak.append(sira);
          const metin = document.createElement('span'); metin.className = 'hy-metin';
          const ad = document.createElement('span'); ad.className = 'hy-ad'; ad.textContent = m.mangaAdi || 'Manga';
          const nd = document.createElement('span'); nd.className = 'hy-neden'; nd.textContent = neden || String(m.aciklama || '').slice(0, 140);
          metin.append(ad, nd); a.append(kapak, metin); li.append(a); liste.append(li);
        });
        durum.textContent = yz ? `${sonuc.length} öneri bulundu.` : `${sonuc.length} kelime eşleşmesi bulundu; sıralama hikâye uyumunu garanti etmez.`;
        kaynak.textContent = yz ? 'Öneriler yapay zekâ ile sıralandı.' : 'Yapay zekâ şu anda kullanılamıyor; kelime eşleşmeleri gösteriliyor.';
        kaynak.hidden = false;
      } catch (err) {
        console.error('Hikâye araması:', err);
        liste.replaceChildren();
        hata(err.name === 'TimeoutError' ? 'Arama zaman aşımına uğradı. Yeniden deneyebilirsin.' : err.message);
      } finally { dugme.disabled = false; }
    });
  }
  let bekliyor = false;
  const observer = new MutationObserver(() => { if (!bekliyor) { bekliyor = true; requestAnimationFrame(() => { bekliyor = false; baslat(); }); } });
  observer.observe(document.documentElement, { childList: true, subtree: true });
  baslat();
})();
