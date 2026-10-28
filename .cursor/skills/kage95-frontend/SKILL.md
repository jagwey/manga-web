---
name: kage95-frontend
description: Builds or edits Kage95 pages and React components inside index.html, read.html, login.html, admin.html and js/*.js while preserving the existing design language. Use for UI, sayfa, bileşen, tasarım, CSS, mobil görünüm or responsive work.
disable-model-invocation: true
---

# Kage95 Frontend

1. Hedefi `rg -n "<BilesenAdi|const <BilesenAdi"` ile bul; yalnızca o bileşenin satır aralığını oku.
2. En yakın benzer bileşeni ve kullandığı CSS sınıflarını/değişkenlerini örnek al. Yeni renk, boşluk veya font değeri ekleme.
3. Değişikliği aynı dosyada, mevcut kalıpla yap (React UMD, `React.useState`, `window.*` yardımcıları). Import, bundler, yeni kütüphane yok.
4. Görseller: `loading="lazy"`, sabit en-boy oranı, `alt`. Yükleniyor / boş / hata durumlarını ekle.
5. Kontrol:
   - `node deploy/araclar/denetle.mjs`
   - İlgili sahte e2e testi: `npm test --prefix tests/e2e -- <spec-adi>`
   - Mobil etkileniyorsa `--project=sahte-mobil`
6. Rapor: değişen dosya:satır, çalışan kontroller ve sonuçları.
