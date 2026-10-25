using Kage95.Backend.Data;
using Microsoft.EntityFrameworkCore;

namespace Kage95.Backend.Services
{
    /// <summary>
    /// Silinen manga önce yalnızca işaretlenir (SilinmeTarihi); bu süre içinde geri alınabilir.
    /// Süre dolunca bölüm, sayfa, yorum, okuma geçmişi kayıtları ve yüklenmiş dosyalarıyla birlikte kalıcı silinir.
    /// </summary>
    public static class MangaSilmeServisi
    {
        public static readonly TimeSpan BeklemeSuresi = TimeSpan.FromHours(1);

        public static async Task KaliciSilAsync(AppDbContext db, IWebHostEnvironment env, int id, CancellationToken iptal = default)
        {
            var silinecek = await db.Mangalar.IgnoreQueryFilters().AsNoTracking()
                .Where(m => m.Id == id)
                .Select(m => new { m.KapakResmiURL, m.OneCikanResimURL })
                .FirstOrDefaultAsync(iptal);
            if (silinecek == null) return;

            var dosyaUrlleri = await db.Sayfalar.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.Bolum.MangaId == id).Select(s => s.ResimURL).ToListAsync(iptal);
            dosyaUrlleri.Add(silinecek.KapakResmiURL ?? string.Empty);
            dosyaUrlleri.Add(silinecek.OneCikanResimURL ?? string.Empty);

            // OkumaGecmisi -> Mangalar/Bolumler FK'leri veritabanında cascade olmayabilir; önce elle temizle.
            var silinenYorumlar = await db.Yorumlar.IgnoreQueryFilters().AsNoTracking().Where(y => y.Bolum.MangaId == id).Select(y => "yorum:" + y.Id).ToListAsync(iptal);
            silinenYorumlar.AddRange(await db.MangaYorumlari.IgnoreQueryFilters().AsNoTracking().Where(y => y.MangaId == id).Select(y => "mangayorum:" + y.Id).ToListAsync(iptal));
            await using (var tx = await db.Database.BeginTransactionAsync(iptal))
            {
                await db.OkumaGecmisi.IgnoreQueryFilters()
                    .Where(o => o.MangaId == id || o.SonOkunanBolum.MangaId == id)
                    .ExecuteDeleteAsync(iptal);
                await db.Mangalar.IgnoreQueryFilters().Where(m => m.Id == id).ExecuteDeleteAsync(iptal);
                await tx.CommitAsync(iptal);
            }
            await PuanServisi.YorumPuaniniSilAsync(db, silinenYorumlar);

            await UploadDosyaServisi.TemizleAsync(db, env, dosyaUrlleri);
        }
    }

    /// <summary>Bekleme süresi dolan silinmiş mangaları birkaç dakikada bir kalıcı siler.</summary>
    public class MangaCopTemizlikServisi : BackgroundService
    {
        private readonly IServiceScopeFactory _fabrika;
        public MangaCopTemizlikServisi(IServiceScopeFactory fabrika) { _fabrika = fabrika; }

        protected override async Task ExecuteAsync(CancellationToken durdur)
        {
            try { await Task.Delay(TimeSpan.FromMinutes(1), durdur); } catch (OperationCanceledException) { return; }
            while (!durdur.IsCancellationRequested)
            {
                try
                {
                    using var scope = _fabrika.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
                    var sinir = DateTime.UtcNow - MangaSilmeServisi.BeklemeSuresi;
                    var idler = await db.Mangalar.IgnoreQueryFilters().AsNoTracking()
                        .Where(m => m.SilinmeTarihi != null && m.SilinmeTarihi <= sinir)
                        .Select(m => m.Id).ToListAsync(durdur);
                    foreach (var id in idler)
                        await MangaSilmeServisi.KaliciSilAsync(db, env, id, durdur);
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex) { Console.WriteLine($"[Manga çöp temizliği] {ex.Message}"); }
                try { await Task.Delay(TimeSpan.FromMinutes(5), durdur); } catch (OperationCanceledException) { return; }
            }
        }
    }
}
