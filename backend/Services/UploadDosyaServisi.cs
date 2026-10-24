using System.Text.RegularExpressions;
using Kage95.Backend.Data;
using Kage95.Backend.Models;
using Kage95.Backend.Security;
using Microsoft.EntityFrameworkCore;

namespace Kage95.Backend.Services
{
    /// <summary>
    /// wwwroot/uploads altındaki yüklenmiş görsellerin güvenli temizliği.
    /// Yalnızca ^[0-9a-f]{32}\.(jpg|jpeg|png|webp|gif)$ biçimindeki adlar silinir; yol kaçışı mümkün değildir.
    /// </summary>
    public static class UploadDosyaServisi
    {
        private static readonly Regex UrlDeseni = new(
            @"^(?:https?://[^/\s]+)?/uploads/(?<ad>[0-9a-f]{32}\.(?:jpg|jpeg|png|webp|gif))\z",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>URL bizim yükleme biçimindeyse dosya adını, değilse null döner.</summary>
        public static string? DosyaAdi(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            var m = UrlDeseni.Match(url.Trim());
            return m.Success ? m.Groups["ad"].Value : null;
        }

        /// <summary>
        /// Verilen URL'lerin fiziksel dosyalarını siler. Veritabanı değişikliği KAYDEDİLDİKTEN sonra çağırın:
        /// hâlâ bir sayfa ya da kapak tarafından kullanılan dosyaya dokunulmaz. Hata fırlatmaz.
        /// </summary>
        public static async Task TemizleAsync(AppDbContext db, IWebHostEnvironment env, IEnumerable<string?> urller)
        {
            try
            {
                var adlar = urller.Select(DosyaAdi).Where(a => a != null).Select(a => a!).Distinct().ToList();
                if (adlar.Count == 0) return;

                var kok = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
                var dizin = Path.GetFullPath(Path.Combine(kok, "uploads"));
                var dizinOnEki = dizin.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

                foreach (var ad in adlar)
                {
                    // Çifte güvence: kayıttan sonra hâlâ kullanılıyorsa silme
                    var kullaniliyor = await db.Sayfalar.AsNoTracking().AnyAsync(s => s.ResimURL.Contains(ad))
                        || await db.Mangalar.AsNoTracking().AnyAsync(m => m.KapakResmiURL != null && m.KapakResmiURL.Contains(ad));
                    if (kullaniliyor) continue;

                    var tam = Path.GetFullPath(Path.Combine(dizin, ad));
                    if (!tam.StartsWith(dizinOnEki, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!string.Equals(Path.GetFileName(tam), ad, StringComparison.Ordinal)) continue;

                    try { if (File.Exists(tam)) File.Delete(tam); } catch { /* temizlik başarısızlığı isteği bozmaz */ }
                }
            }
            catch
            {
                // temizlik en iyi çabadır
            }
        }
    }

    /// <summary>Yayında olmayan içeriği görmeye yetkili olup olmadığını çözer.</summary>
    public static class IcerikErisimi
    {
        private static readonly string[] GizliGorenIzinler = { "manga.yaz", "manga.sil", "bolum.yaz", "bolum.sil", "bolum.duzenle" };

        public static async Task<bool> GizliGorebilirMiAsync(HttpContext http, AppDbContext db)
        {
            const string onbellek = "GizliIcerikGorebilir";
            if (http.Items[onbellek] is bool b) return b;

            bool sonuc = false;
            var k = http.Items["Kullanici"] as Kullanici;
            if (k == null && http.User.Identity?.IsAuthenticated == true
                && int.TryParse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id))
            {
                k = await db.Kullanicilar.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, http.RequestAborted);
            }
            if (k != null && k.Aktif)
            {
                sonuc = Izinler.Cozumle(k).Any(GizliGorenIzinler.Contains);
            }
            http.Items[onbellek] = sonuc;
            return sonuc;
        }
    }
}
