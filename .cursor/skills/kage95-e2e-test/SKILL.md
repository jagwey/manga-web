---
name: kage95-e2e-test
description: Writes and runs Kage95 Playwright end-to-end tests (sahte API and mobile projects). Use for Playwright, e2e, uçtan uca test, tarayıcı testi or adding a test for a page.
disable-model-invocation: true
---

# Kage95 E2E Test

Konum: `tests/e2e/`. Sunucu: `sunucu.mjs` (statik, 5500). Sahte API: `sahte/api.mjs` + `sahte/veri.mjs`.

| Proje | Ne | Veritabanı |
|---|---|---|
| `sahte-masaustu`, `sahte-mobil` | `@sahte` etiketli; `/api/**` `page.route` ile taklit | Yok |

Çalıştırma:
```
npm ci --prefix tests/e2e
npx --prefix tests/e2e playwright install chromium
npm test --prefix tests/e2e                              # tümü
npm test --prefix tests/e2e -- anasayfa --project=sahte-mobil
```

Yeni test:
1. Gereken API yanıtını `sahte/veri.mjs`'e ekle; alan adları `backend/Models/*Dtos.cs` ile aynı (camelCase).
2. `sahteApiKur(page, { ... })` ile varsayılanları ezerek test yaz. Seçicide önce rol/etiket/metin, sonra `id`.
3. Her test sayfa hatası olmadığını `sayfaHatalari` ile doğrular.

Kurallar:
- Sahte testler gerçek backend'e veya veritabanına istek atmaz; atarsa test başarısız olmalı.
- Gerçek backend testleri ayrı projede ve yalnızca test veritabanıyla çalışır (henüz yok, Paket 2).
- Hata çıktısında yalnızca ilk başarısız test ve hata satırını özetle; trace/rapor dosyalarını bağlama alma.
