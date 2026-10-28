---
name: kage95-sqlserver-sorgu
description: Analyzes and optimizes Kage95 EF Core / SQL Server query and index performance. Use for yavaş sorgu, indeks, N+1, EF Core LINQ, execution plan, SQL Server or veritabanı performans work.
disable-model-invocation: true
---

# Kage95 SQL Server Sorgu

Veritabanı SQL Server (EF Core `UseSqlServer`). Migration yok; şema `backend/Veritabani_Kurulumu.sql` + `Program.cs` açılış bloğu.

1. **Sorguyu bul**: Yavaş uçtan controller/servisteki LINQ'e git. Üretilen SQL için `query.ToQueryString()` kullan (geçici, commit'leme).
2. **Ölç** (yalnızca yerel/test DB): SSMS/`sqlcmd` ile `SET STATISTICS IO, TIME ON;` ve gerçek yürütme planı. Bak: Table/Index Scan, Key Lookup, yüksek logical reads, Sort/Hash spill.
3. **Teşhis**:
   - Döngüde sorgu (N+1) → `Include`/projeksiyon veya toplu `Where(x => ids.Contains(x.Id))`
   - Gereksiz kolon → `Select` ile DTO projeksiyonu, salt okumada `AsNoTracking()`
   - `ToLower()`/fonksiyonlu filtre indeksi kullanamaz (`AlakayaGoreSirala` gibi) → maliyeti not et
   - Büyük `Skip` → keyset sayfalama değerlendir
4. **Öner**: Önce sorgu düzeltmesi, sonra indeks. İndeks için mevcut indeksleri kontrol et (`sys.indexes`), yazma maliyetini belirt.
5. **Doğrula**: Önce/sonra logical reads ve süre.

Kurallar:
- İndeks/şema değişikliğini uygulamadan önce onay al; `Program.cs` idempotent kalıbıyla (`IF NOT EXISTS (SELECT 1 FROM sys.indexes ...)`) öner.
- Canlı veritabanında komut çalıştırma. Bağlantı cümlesi ve parolaları çıktıya yazma.

Rapor:
```
Sorgu: <dosya:satır>
Sorun: <plan bulgusu>
Öneri: <LINQ/SQL/indeks>
Önce → Sonra: reads X → Y, süre X ms → Y ms
Risk: <yazma maliyeti/kilit>
```
