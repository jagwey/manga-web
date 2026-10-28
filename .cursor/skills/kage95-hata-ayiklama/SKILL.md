---
name: kage95-hata-ayiklama
description: Finds the root cause of a Kage95 bug, applies the smallest fix and verifies it with a targeted check. Use when the user reports hata, çalışmıyor, bozuldu, siyah ekran, 500, exception or a failing test.
disable-model-invocation: true
---

# Kage95 Hata Ayıklama

```
- [ ] Yeniden üret  - [ ] Kök neden  - [ ] En küçük düzeltme  - [ ] Hedefli kontrol  - [ ] Rapor
```

1. **Yeniden üret**: Hata metnini/stack trace'i al. Frontend siyah ekranı çoğunlukla `ReferenceError`: önce `node deploy/araclar/denetle.mjs`. Gerekirse sahte e2e testiyle tarayıcıda üret.
2. **Kök neden**: Stack trace'teki dosya:satırdan geriye doğru yalnızca ilgili kodu oku. Hipotezi tek cümle yaz, kanıtla doğrula.
3. **Düzeltme**: Yalnızca nedeni gider. Refactor, yeniden adlandırma, biçim değişikliği karıştırma. Hatayı `try/catch` ile yutma.
4. **Kontrol**: Hatayı yakalayan testi ekle veya mevcut testi çalıştır; önce kırmızı, sonra yeşil. Backend değiştiyse `dotnet build backend/backend.csproj -c Release -warnaserror`.
5. **Rapor**: Kök neden (1 cümle), değişen dosyalar, çalışan kontroller ve gerçek sonuçları.

Kurallar:
- Backend'i yalnızca yerel/test veritabanıyla başlat (açılışta şema SQL'i çalışır).
- Başarısız komutu nedenini anlamadan tekrar çalıştırma. 3 denemede çözülmezse dur ve bulguları raporla.
- Şema veya API sözleşmesi değişikliği gerekiyorsa önce plan sun.
