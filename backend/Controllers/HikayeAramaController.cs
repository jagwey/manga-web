using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kage95.Backend.Data;
using Kage95.Backend.Models;
using Kage95.Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Kage95.Backend.Controllers;

[ApiController, Route("api/hikaye-arama")]
public sealed class HikayeAramaController(AppDbContext db, EvrenIstemcisi evren, IMemoryCache cache, IConfiguration config, ILogger<HikayeAramaController> log) : ControllerBase
{
    public sealed record Sorgu(string? Metin);
    [HttpGet("durum")]
    public IActionResult Durum() => Ok(new { etkin = config.GetValue<bool>("HikayeArama:Etkin") });

    [HttpPost, EnableRateLimiting("hikaye")]
    public async Task<IActionResult> Ara([FromBody] Sorgu sorgu, CancellationToken ct)
    {
        if (!config.GetValue<bool>("HikayeArama:Etkin")) return NotFound();
        var q = sorgu.Metin?.Trim() ?? "";
        if (q.Length < 10 || q.Length > 500) return BadRequest(new { message = "Hikâyeyi 10–500 karakterle anlat." });
        if (!evren.Hazir) return await KelimelerleAra(q, ct);
        var key = "hikaye:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(q)));
        try
        {
            if (!cache.TryGetValue(key, out int[]? ids) || ids == null)
            {
                var kaynak = await (from m in db.Mangalar.AsNoTracking().Where(m => m.Yayinda)
                                    join v in db.Set<MangaAiIndeks>().AsNoTracking() on m.Id equals v.MangaId
                                    select new { m.Id, m.MangaAdi, m.Aciklama, v.Vektor, v.IcerikHash }).ToListAsync(ct);
                var queryVec = (await evren.Vektorler(new[] { q }, ct))[0];
                var adaylar = kaynak.Select(x => new { x.Id, Metin = MangaAiMetni.Olustur(new Manga { MangaAdi = x.MangaAdi, Aciklama = x.Aciklama }), x.Vektor, x.IcerikHash })
                    .Where(x => MangaAiMetni.Hash(x.Metin) == x.IcerikHash)
                    .Select(x => new { x.Id, x.Metin, Skor = Benzerlik(queryVec, x.Vektor) })
                    .Where(x => x.Skor > -1).OrderByDescending(x => x.Skor).Take(25).ToArray();
                if (adaylar.Length == 0) return Ok(new { sonuclar = Array.Empty<object>(), mesaj = "Hikâye araması hazırlanıyor." });
                var siralama = await evren.Sirala(q, adaylar.Select(x => x.Metin).ToArray(), ct);
                ids = siralama.Take(12).Select(i => adaylar[i].Id).ToArray();
                cache.Set(key, ids, TimeSpan.FromMinutes(10));
            }
            var mangalar = await db.Mangalar.AsNoTracking().Where(m => m.Yayinda && ids.Contains(m.Id))
                .Select(m => new { m.Id, m.MangaAdi, m.Slug, m.KapakResmiURL, m.Aciklama }).ToListAsync(ct);
            return Ok(new { sonuclar = mangalar.OrderBy(m => Array.IndexOf(ids, m.Id)) });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            log.LogWarning("Hikâye araması kullanılamıyor: {Tur}", ex.GetType().Name);
            return await KelimelerleAra(q, ct);
        }
    }
    private async Task<IActionResult> KelimelerleAra(string q, CancellationToken ct)
    {
        var gereksiz = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "bir", "birisi", "gibi", "olan", "olanı", "diye", "sonra", "daha", "için", "ilgili", "manga", "serisi", "hikaye", "hikâye", "hatırlıyorum", "hatirliyorum", "vardı", "vardi", "oluyor", "ediyor", "falan", "the", "and", "ama", "çok", "böyle", "sonunda", "karakter", "biri" };
        var kelimeler = Regex.Matches(q.ToLowerInvariant(), @"[\p{L}\p{Nd}]{3,}").Select(x => x.Value)
            .Where(x => !gereksiz.Contains(x)).Distinct().OrderByDescending(x => x.Length).Take(6).ToArray();
        var adaylar = new Dictionary<int, (int Id, string MangaAdi, string? Slug, string? KapakResmiURL, string? Aciklama, int Puan)>();
        foreach (var kelime in kelimeler)
        {
            var rows = await db.Mangalar.AsNoTracking().Where(m => m.Yayinda && (m.MangaAdi.Contains(kelime) || (m.Aciklama != null && m.Aciklama.Contains(kelime))))
                .OrderByDescending(m => m.Id).Take(100).Select(m => new { m.Id, m.MangaAdi, m.Slug, m.KapakResmiURL, m.Aciklama }).ToListAsync(ct);
            foreach (var m in rows)
            {
                var score = m.MangaAdi.Contains(kelime, StringComparison.OrdinalIgnoreCase) ? 2 : 1;
                if (adaylar.TryGetValue(m.Id, out var eski)) score += eski.Puan;
                adaylar[m.Id] = (m.Id, m.MangaAdi, m.Slug, m.KapakResmiURL, m.Aciklama, score);
            }
        }
        return Ok(new { mod = "kelime", mesaj = "Kelime eşleşmeleri gösteriliyor.", sonuclar = adaylar.Values.OrderByDescending(x => x.Puan).ThenByDescending(x => x.Id).Take(12).Select(x => new { id = x.Id, mangaAdi = x.MangaAdi, slug = x.Slug, kapakResmiURL = x.KapakResmiURL, aciklama = x.Aciklama }) });
    }
    private static double Benzerlik(float[] q, string json)
    {
        try
        {
            var v = JsonSerializer.Deserialize<float[]>(json);
            if (v == null || v.Length != q.Length || v.Length == 0) return -1;
            double dot = 0, a = 0, b = 0;
            for (int i = 0; i < v.Length; i++) { dot += q[i] * v[i]; a += q[i] * q[i]; b += v[i] * v[i]; }
            return a > 0 && b > 0 ? dot / Math.Sqrt(a * b) : -1;
        }
        catch (JsonException) { return -1; }
    }
}
