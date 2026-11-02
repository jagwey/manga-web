using System.IO.Compression;
using System.Security.Claims;
using Kage95.Backend.Data;
using Kage95.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Kage95.Backend.Controllers;
[ApiController, Route("api/premium")]
public sealed class PremiumController(AppDbContext db, PremiumServisi premium, PremiumDosyaServisi files, KageAiIstemcisi ai, AiKullanimKotasi kota) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet("plan")]
    public IActionResult Plan() => Ok(new { fiyatKurus = 999, paraBirimi = "TRY", donem = "ay", satinAlmaAcik = false });
    [Authorize, HttpGet("durum")]
    public async Task<IActionResult> Durum(CancellationToken ct) => Ok(new { premium = await premium.Etkin(UserId, ct) });
    [Authorize, HttpPost("satin-al"), EnableRateLimiting("hikaye")]
    public IActionResult SatinAl() => StatusCode(503, new { message = "Premium satışları henüz açılmadı. Ödeme bağlantısı yakında eklenecek." });

    [Authorize, HttpGet("bolum/{id:int}/indir"), EnableRateLimiting("hikaye")]
    public async Task<IActionResult> Indir(int id, CancellationToken ct)
    {
        if (!await premium.Etkin(UserId, ct)) return StatusCode(403, new { message = "Bölüm indirmek için aktif premium üyelik gerekir." });
        var urls = await db.Sayfalar.AsNoTracking().Where(s => s.BolumId == id && s.Bolum.Manga.Yayinda)
            .OrderBy(s => s.SayfaNo).Select(s => s.ResimURL).Take(301).ToListAsync(ct);
        if (urls.Count == 0) return NotFound();
        if (urls.Count > 300) return BadRequest(new { message = "Bu bölüm indirme sınırını aşıyor." });
        var paths = urls.Select(files.YerelResim).ToArray();
        if (paths.Any(p => p == null)) return Conflict(new { message = "Bu bölümün bazı sayfaları yerel arşivde yok; indirme henüz hazır değil." });
        if (paths.Sum(p => new FileInfo(p!).Length) > 100L * 1024 * 1024) return BadRequest(new { message = "Bölüm 100 MB indirme sınırını aşıyor." });
        // Büyük arşivler RAM'e alınmaz; geçici dosya yanıt tamamlanınca silinir.
        var stream = new FileStream(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        try
        {
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var entry = zip.CreateEntry($"{i + 1:D4}{Path.GetExtension(paths[i])}", CompressionLevel.NoCompression);
                    await using var output = entry.Open();
                    await using var input = System.IO.File.OpenRead(paths[i]!);
                    await input.CopyToAsync(output, ct);
                }
            }
            stream.Position = 0;
            return File(stream, "application/zip", $"kage95-bolum-{id}.zip");
        }
        catch { await stream.DisposeAsync(); throw; }
    }

    public sealed record TopluIstek(int[]? Bolumler);
    [Authorize, HttpPost("toplu-indir"), EnableRateLimiting("hikaye"), RequestSizeLimit(2048)]
    public async Task<IActionResult> TopluIndir(TopluIstek body, CancellationToken ct)
    {
        if (!await premium.Etkin(UserId, ct)) return StatusCode(403, new { message = "Toplu indirmek için aktif premium üyelik gerekir." });
        var ids = body.Bolumler?.Distinct().ToArray() ?? [];
        if (ids.Length is < 1 or > 20 || ids.Any(id => id < 1)) return BadRequest(new { message = "1–20 bölüm seçebilirsin." });
        var count = await db.Bolumler.AsNoTracking().Where(b => ids.Contains(b.Id) && b.Manga.Yayinda).Select(b => b.MangaId).ToListAsync(ct);
        if (count.Count != ids.Length || count.Distinct().Count() != 1) return NotFound(new { message = "Bölümler bulunamadı." });
        var archive = new FileStream(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        try
        {
            using (var zip = new ZipArchive(archive, ZipArchiveMode.Create, true))
            {
                long total = 0;
                foreach (var id in ids)
                {
                    var result = await Indir(id, ct);
                    if (result is not FileStreamResult file) { zip.Dispose(); await archive.DisposeAsync(); return result; }
                    await using var input = file.FileStream;
                    total += input.Length;
                    if (total > 200L * 1024 * 1024) { zip.Dispose(); await archive.DisposeAsync(); return BadRequest(new { message = "Toplam indirme 200 MB sınırını aşıyor." }); }
                    var entry = zip.CreateEntry($"bolum-{id}.zip", CompressionLevel.NoCompression);
                    await using var output = entry.Open();
                    await input.CopyToAsync(output, ct);
                }
            }
            archive.Position = 0;
            return File(archive, "application/zip", "kage95-bolumler.zip");
        }
        catch { await archive.DisposeAsync(); throw; }
    }

    public sealed record BolumSorusu(string? Soru, int Sayfa = 1);
    [Authorize, HttpPost("bolum/{id:int}/sor"), EnableRateLimiting("hikaye"), RequestSizeLimit(4096)]
    public async Task<IActionResult> Sor(int id, BolumSorusu body, CancellationToken ct)
    {
        if (!await premium.Etkin(UserId, ct)) return StatusCode(403, new { message = "Bölüme soru sormak için aktif premium üyelik gerekir." });
        var question = body.Soru?.Trim() ?? "";
        if (question.Length < 5 || question.Length > 500 || body.Sayfa < 1) return BadRequest(new { message = "5–500 karakterlik soru ve geçerli sayfa numarası gir." });
        var page = await db.Sayfalar.AsNoTracking().Where(s => s.BolumId == id && s.Bolum.Manga.Yayinda)
            .OrderBy(s => s.SayfaNo).Skip(body.Sayfa - 1).Select(s => s.ResimURL).FirstOrDefaultAsync(ct);
        if (page == null) return NotFound();
        var path = files.YerelResim(page);
        if (path == null || new FileInfo(path).Length > 1_500_000) return Conflict(new { message = "Bu sayfa AI için hazır değil (yerel JPEG/PNG/WebP, en fazla 1,5 MB gerekir)." });
        if (!kota.Kullan($"bolum:{UserId}", UserId, true)) return StatusCode(429, new { message = "Bugünkü bölüm sorusu hakkın doldu." });
        try
        {
            var mime = Path.GetExtension(path).ToLowerInvariant() switch { ".png" => "image/png", ".webp" => "image/webp", _ => "image/jpeg" };
            var answer = await ai.BolumSor(question, Convert.ToBase64String(await System.IO.File.ReadAllBytesAsync(path, ct)), mime, ct);
            return Ok(new { yanit = answer, sayfa = body.Sayfa });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return StatusCode(503, new { message = "Bölüm asistanı şu an yanıt veremiyor. Daha sonra yeniden dene." }); }
    }
}
