using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kage95.Backend.Data;
using Kage95.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Kage95.Backend.Services;

public static class MangaAiMetni
{
    public static string Olustur(Manga m) => $"{m.MangaAdi}\n{m.Aciklama}"[..Math.Min(($"{m.MangaAdi}\n{m.Aciklama}").Length, 1800)];
    public static string Hash(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
}

public sealed class MangaAiIndeksServisi(IServiceScopeFactory scopes, IConfiguration config, ILogger<MangaAiIndeksServisi> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            if (config.GetValue<bool>("Evren:Etkin"))
            {
                try { await Indeksle(stop); }
                catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
                catch (Exception ex) { log.LogWarning("AI indeksi güncellenemedi: {Tur}", ex.GetType().Name); }
            }
            await Task.Delay(TimeSpan.FromMinutes(5), stop);
        }
    }
    private async Task Indeksle(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var evren = scope.ServiceProvider.GetRequiredService<EvrenIstemcisi>();
        if (!evren.Hazir) return;
        var rows = await db.Mangalar.AsNoTracking().Where(x => x.Yayinda).Select(x => new { x.Id, x.MangaAdi, x.Aciklama }).ToListAsync(ct);
        var hashes = await db.Set<MangaAiIndeks>().AsNoTracking().ToDictionaryAsync(x => x.MangaId, x => x.IcerikHash, ct);
        var pending = rows.Select(x => new { x.Id, Metin = MangaAiMetni.Olustur(new Manga { MangaAdi = x.MangaAdi, Aciklama = x.Aciklama }) })
            .Select(x => new { x.Id, x.Metin, Hash = MangaAiMetni.Hash(x.Metin) })
            .Where(x => !hashes.TryGetValue(x.Id, out var hash) || hash != x.Hash).Take(16).ToArray();
        if (pending.Length == 0) return;
        var vectors = await evren.Vektorler(pending.Select(x => x.Metin).ToArray(), ct);
        for (var i = 0; i < pending.Length; i++)
        {
            var row = await db.Set<MangaAiIndeks>().FindAsync(new object[] { pending[i].Id }, ct);
            if (row == null) { row = new MangaAiIndeks { MangaId = pending[i].Id }; db.Add(row); }
            row.IcerikHash = pending[i].Hash;
            row.Vektor = JsonSerializer.Serialize(vectors[i]);
        }
        await db.SaveChangesAsync(ct);
    }
}
