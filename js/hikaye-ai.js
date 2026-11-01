(() => {
  'use strict';
  if (!window.KAGE95_HIKAYE_AI) return;
  // Anahtarsız ücretsiz YZ: Pollinations. Sırayla modeller denenir; hepsi düşerse kelime eşleşmesi kullanılır.
  const YZ_URL = 'https://text.pollinations.ai/openai';
  const MODELLER = ['openai-fast', 'mistral'];
  const api = (window.KAGE95_API_URL || '').replace(/\/$/, '');
  const MAX_ADAY = 80;
  const ORNEKLER = [
    'Başka bir dünyaya ışınlanan lise öğrencisi, büyü okulunda yetenekleri uyanıyor',
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

  const kok = (s) => String(s || '').toLocaleLowerCase('tr').replace(/[^\p{L}\p{N}\s]/gu, ' ');
  const DUR = new Set(['bir', 'bu', 'ile', 'ama', 'gibi', 'olan', 'için', 'daha', 'çok', 'sonra', 'diye', 'manga', 'hikaye', 'hikâye', 'vardı', 'biri', 'falan', 'ben', 'bir']);
  const kelimeler = (s) => [...new Set(kok(s).split(/\s+/).filter(k => k.length > 2 && !DUR.has(k)))];

  // Basit kelime puanı: ad eşleşmesi 3, açıklama eşleşmesi 1; kökü yakalamak için 5 harfe kadar önek.
  function puanla(manga, ks) {
    const ad = kok(manga.mangaAdi), ac = kok(manga.aciklama);
    let p = 0;
    for (const k of ks) { const o = k.slice(0, Math.max(4, Math.min(k.length, 6))); if (ad.includes(o)) p += 3; if (ac.includes(o)) p += 1; }
    return p;
  }

  async function katalog() {
    const hepsi = [];
    let sayfa = 1, toplam = 0;
    do {
      const r = await fetch(`${api}/mangas?page=${sayfa}&pageSize=100`);
      if (!r.ok) throw new Error('Manga kataloğu yüklenemedi.');
      const ogeler = await r.json();
      if (!Array.isArray(ogeler)) throw new Error('Katalog yanıtı geçersiz.');
      hepsi.push(...ogeler);
      toplam = Number(r.headers.get('X-Total-Count') || hepsi.length);
      sayfa++;
      if (!ogeler.length) break;
    } while (hepsi.length < toplam && sayfa <= 101);
    return hepsi.filter(m => m.yayinda !== false);
  }

  async function yzSirala(hikaye, adaylar, sinyal) {
    const liste = adaylar.map(m => `${m.id} | ${m.mangaAdi} | ${String(m.aciklama || '').replace(/\s+/g, ' ').slice(0, 220)}`).join('\n');
    const istek = [
      { role: 'system', content: 'Bir manga sitesinin arama asistanısın. Kullanıcı bir mangayı hikâyesinden hatırlamaya çalışıyor. Yalnızca verilen listeden, hikâyeye en çok uyan en fazla 6 seriyi seç. Uymayanı ekleme; hiçbiri uymuyorsa boş liste dön. Yanıtın yalnızca JSON olsun: {"sonuclar":[{"id":<sayı>,"neden":"<Türkçe, tek kısa cümle>"}]}' },
      { role: 'user', content: `Kullanıcının anlattığı hikâye:\n${hikaye}\n\nListe (id | ad | açıklama):\n${liste}` },
    ];
    let sonHata;
    for (const model of MODELLER) {
      try {
        const r = await fetch(YZ_URL, { method: 'POST', signal: sinyal, headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ model, messages: istek, response_format: { type: 'json_object' }, temperature: 0.2 }) });
        if (!r.ok) throw new Error('YZ ' + r.status);
        const j = await r.json();
        const metin = j.choices?.[0]?.message?.content || '';
        const veri = JSON.parse(metin.slice(metin.indexOf('{'), metin.lastIndexOf('}') + 1));
        const izinli = new Map(adaylar.map(m => [Number(m.id), m]));
        const cikti = (Array.isArray(veri.sonuclar) ? veri.sonuclar : [])
          .map(s => ({ m: izinli.get(Number(s.id)), neden: String(s.neden || '').slice(0, 200) }))
          .filter(s => s.m);
        return cikti.slice(0, 6);
      } catch (e) { if (e.name === 'AbortError') throw e; sonHata = e; }
    }
    throw sonHata || new Error('YZ yanıt vermedi');
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
    kutu.innerHTML = `<div class="hy-baslik"><h2>Hikâyeyle Manga Bul</h2><span class="hy-rozet">YZ</span></div>
      <p class="hy-alt">Adını hatırlamadığın manganın hikâyesini anlat, yapay zekâ kataloğumuzda onu bulsun.</p>
      <form><div class="hy-alan"><textarea aria-label="Manganın hikâyesi" minlength="10" maxlength="500" required placeholder="Örneğin: Başka bir dünyada uyanan bir kahraman…"></textarea><span class="hy-say" aria-hidden="true">0/500</span></div>
      <div class="hy-satir"><button class="btn-ana" type="submit">Manga bul</button><span class="manga-meta">En az 10 karakter. Metin ücretsiz bir YZ servisine (Pollinations) gönderilir.</span></div>
      <div class="hy-ornek" role="group" aria-label="Örnek hikâyeler"></div></form>
      <p class="hy-durum" role="status" aria-live="polite"></p>
      <ul class="hy-liste" aria-label="Bulunan seriler"></ul>
      <p class="hy-kaynak" hidden></p>`;
    hedef.prepend(kutu);
    const $ = (s) => kutu.querySelector(s);
    const form = $('form'), alan = $('textarea'), say = $('.hy-say'), durum = $('.hy-durum'), liste = $('.hy-liste'), kaynak = $('.hy-kaynak'), dugme = form.querySelector('button[type=submit]');
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
        durum.textContent = 'Katalog taranıyor…';
        const tum = await katalog();
        const adayli = tum.filter(m => m.aciklama);
        if (!adayli.length) { liste.replaceChildren(); durum.textContent = 'Aranabilir açıklama bulunamadı.'; return; }
        const ks = kelimeler(hikaye);
        const puanli = adayli.map(m => ({ m, p: puanla(m, ks) })).sort((a, b) => b.p - a.p);
        const adaylar = puanli.slice(0, MAX_ADAY).map(x => x.m);
        let sonuc, yz = true;
        durum.textContent = 'Yapay zekâ hikâyeni okuyor…';
        try { sonuc = await yzSirala(hikaye, adaylar, onceki.signal); }
        catch (err) {
          if (err.name === 'AbortError') return;
          console.warn('Hikâye YZ yedeğe düştü:', err);
          yz = false;
          sonuc = puanli.filter(x => x.p > 0).slice(0, 6).map(x => ({ m: x.m, neden: '' }));
        }
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
        durum.textContent = `${sonuc.length} öneri bulundu.`;
        kaynak.textContent = yz ? 'Öneriler ücretsiz yapay zekâ ile sıralandı.' : 'Yapay zekâ şu an yanıt vermedi; kelime eşleşmeleri gösteriliyor.';
        kaynak.hidden = false;
      } catch (err) {
        console.error('Hikâye araması:', err);
        liste.replaceChildren();
        hata('Arama şu anda yapılamadı. Normal manga aramasını kullanabilirsin.');
      } finally { dugme.disabled = false; }
    });
  }
  let bekliyor = false;
  const observer = new MutationObserver(() => { if (!bekliyor) { bekliyor = true; requestAnimationFrame(() => { bekliyor = false; baslat(); }); } });
  observer.observe(document.documentElement, { childList: true, subtree: true });
  baslat();
})();
