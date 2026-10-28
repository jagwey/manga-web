---
name: kage95-kod-inceleme
description: Reviews only the changed lines of Kage95 code for bugs, security and performance issues. Use for code review, inceleme, diff kontrolü or before commit/PR.
disable-model-invocation: true
---

# Kage95 Kod İnceleme

Kapsam: `git diff --stat` ile dosyaları gör, sonra `git diff -U3 <dosya>` ile yalnızca değişen kısımları oku. Değişmemiş koda yorum yapma.

Kontrol listesi:
- **Hata**: null, sınır durumu, eksik `await`, yanlış HTTP kodu, yutulan hata, frontend'de tanımsız değişken.
- **Sözleşme**: DTO alan adı, rota veya şema istemeden değişti mi.
- **Güvenlik**: ham SQL'de string birleştirme, `dangerouslySetInnerHTML`, eksik `[Authorize]`/yetki kontrolü, IDOR, hız sınırı olmayan giriş ucu, dosya yükleme tür/boyut/yol kontrolü, koda/loga sızan gizli bilgi.
- **Performans**: N+1, `AsNoTracking` eksikliği, sınırsız liste, büyük görsel.

Çıktı (önem sırasıyla, bulgu başına tek satır):
```
path:line: 🔴 kritik: <sorun>. <düzeltme>.
path:line: 🟡 öneri: <sorun>. <düzeltme>.
```
Bulgu yoksa "Sorun bulunamadı." Övgü ve özet tekrarı yok.
