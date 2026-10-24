using Kage95.Backend.Security;
using Kage95.Backend.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace Kage95.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Yetki("manga.yaz", "bolum.yaz")]
    public class UploadController : ControllerBase
    {
        private const int MaxFiles = 200;
        private const long MaxFileSize = 15L * 1024 * 1024;
        private const long MaxRequestSize = 150L * 1024 * 1024;

        private readonly IWebHostEnvironment _env;

        private readonly IDenetimServisi _denetim;
        private readonly IslemSinirlayici _sinir;

        public UploadController(IWebHostEnvironment env, IDenetimServisi denetim, IslemSinirlayici sinir)
        {
            _env = env;
            _denetim = denetim;
            _sinir = sinir;
        }

        [HttpPost]
        [RequestSizeLimit(MaxRequestSize)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestSize, ValueCountLimit = 2000)]
        public async Task<IActionResult> Upload([FromForm] List<IFormFile>? files)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? HttpContext.Connection.RemoteIpAddress?.ToString() ?? "x";
            if (!_sinir.Dene($"yukleme:{userId}", 10, TimeSpan.FromMinutes(1)))
                return StatusCode(429, new { message = "Çok sık yükleme yapıyorsunuz, lütfen biraz bekleyin." });

            if (files == null || files.Count == 0)
                return BadRequest(new { message = "Yüklenecek dosya seçilmedi." });
            if (files.Count > MaxFiles)
                return BadRequest(new { message = $"En fazla {MaxFiles} dosya yüklenebilir." });

            // Önce hepsini doğrula; biri geçersizse hiçbiri kaydedilmez.
            var checkedFiles = new List<(IFormFile File, string Ext)>();
            foreach (var f in files)
            {
                if (f.Length == 0)
                    return BadRequest(new { message = "Boş dosya yüklenemez." });
                if (f.Length > MaxFileSize)
                    return BadRequest(new { message = "Dosya başına en fazla 15 MB yüklenebilir." });

                var ext = Path.GetExtension(f.FileName ?? "").ToLowerInvariant();
                if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".gif"))
                    return BadRequest(new { message = "Yalnızca .jpg, .jpeg, .png, .webp ve .gif dosyaları yüklenebilir." });

                var header = new byte[12];
                int read;
                await using (var s = f.OpenReadStream())
                {
                    read = 0;
                    while (read < header.Length)
                    {
                        var n = await s.ReadAsync(header.AsMemory(read, header.Length - read));
                        if (n == 0) break;
                        read += n;
                    }
                }

                if (!MatchesExtension(header, read, ext))
                    return BadRequest(new { message = "Dosya içeriği uzantısıyla uyuşmuyor; geçerli bir görsel değil." });

                checkedFiles.Add((f, ext));
            }

            var dir = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "uploads");
            Directory.CreateDirectory(dir);

            var baseUrl = UploadDosyaServisi.GuvenliSunucuKoku(Request);
            var urls = new List<string>();
            var saved = new List<string>();
            try
            {
                foreach (var (file, ext) in checkedFiles)
                {
                    var name = Guid.NewGuid().ToString("N") + ext;
                    var path = Path.Combine(dir, name);
                    saved.Add(path); // kopyalama yarıda kalırsa kısmi dosya da silinsin
                    await using (var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                        await file.CopyToAsync(fs);
                    urls.Add($"{baseUrl}/uploads/{name}");
                }
            }
            catch
            {
                foreach (var p in saved) { try { System.IO.File.Delete(p); } catch { } }
                return StatusCode(500, new { message = "Dosyalar kaydedilirken bir hata oluştu." });
            }

            await _denetim.KaydetAsync(User, "yukleme.dosya", $"{urls.Count} görsel yüklendi",
                string.Join(", ", saved.Select(Path.GetFileName)));

            return Ok(new { urls });
        }

        private static bool MatchesExtension(byte[] h, int len, string ext)
        {
            switch (ext)
            {
                case ".jpg":
                case ".jpeg":
                    return len >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF;
                case ".png":
                    return len >= 8 && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47
                        && h[4] == 0x0D && h[5] == 0x0A && h[6] == 0x1A && h[7] == 0x0A;
                case ".gif":
                    return len >= 6 && h[0] == 'G' && h[1] == 'I' && h[2] == 'F' && h[3] == '8'
                        && (h[4] == '7' || h[4] == '9') && h[5] == 'a';
                case ".webp":
                    return len >= 12 && h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F'
                        && h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P';
                default:
                    return false;
            }
        }
    }
}
