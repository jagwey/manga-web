using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Kage95.Backend.Data;
using Kage95.Backend.Models;

namespace Kage95.Backend.Services
{
    /// <summary>Bellek içi kayan pencere sınırı (anahtar başına). Singleton.</summary>
    public class IslemSinirlayici
    {
        private readonly ConcurrentDictionary<string, Queue<DateTime>> _kayitlar = new();

        /// <summary>İzin varsa true döner ve olayı sayar; sınır aşıldıysa false.</summary>
        public bool Dene(string anahtar, int maks, TimeSpan pencere)
        {
            var simdi = DateTime.UtcNow;
            if (_kayitlar.Count > 50000) Temizle(simdi);
            var kuyruk = _kayitlar.GetOrAdd(anahtar, _ => new Queue<DateTime>());
            lock (kuyruk)
            {
                while (kuyruk.Count > 0 && simdi - kuyruk.Peek() > pencere) kuyruk.Dequeue();
                if (kuyruk.Count >= maks) return false;
                kuyruk.Enqueue(simdi);
                return true;
            }
        }

        private void Temizle(DateTime simdi)
        {
            foreach (var kv in _kayitlar)
            {
                lock (kv.Value)
                {
                    if (kv.Value.Count == 0 || simdi - kv.Value.Peek() > TimeSpan.FromMinutes(30)) _kayitlar.TryRemove(kv.Key, out _);
                }
            }
        }
    }

    /// <summary>Yorum biçimlendirme işaretlerinin doğrulaması. Sunucu HTML üretmez; işaretler düz metin olarak saklanır.</summary>
    public static class YorumBicimi
    {
        public const int MaksIsaret = 20;
        private static readonly Regex Isaret = new(@"\*\*|\*|\|\||\[renk=(?:kirmizi|mavi|turuncu|mor|gri|sari)\]|\[/renk\]",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>Hata mesajı ya da null. Bilinmeyen renk adları işaret sayılmaz (serbest metin kalır).</summary>
        public static string? Dogrula(string ham)
        {
            var adet = 0;
            var derinlik = 0;
            foreach (Match m in Isaret.Matches(ham))
            {
                if (++adet > MaksIsaret) return $"Yorumda en fazla {MaksIsaret} biçimlendirme işareti kullanılabilir.";
                if (m.Value.StartsWith("[renk=", StringComparison.Ordinal))
                {
                    if (++derinlik > 1) return "Renk işaretleri iç içe kullanılamaz.";
                }
                else if (m.Value == "[/renk]" && derinlik > 0) derinlik--;
            }
            return null;
        }
    }

    public record YorumBilgi(int Id, int KullaniciId, string Metin, int UstId, int? BolumId, bool Yayinda, int? YanitId);

    /// <summary>Bölüm ve seri yorumları için ortak v2 mantığı (liste, doğrulama, silme).</summary>
    public class YorumServisi
    {
        public const int MaksHamUzunluk = 500;
        public const int KokBasinaYanit = 20;
        private readonly AppDbContext _db;
        private readonly TercihServisi _tercihler;

        public YorumServisi(AppDbContext db, TercihServisi tercihler)
        {
            _db = db;
            _tercihler = tercihler;
        }

        public static string Tur(bool manga) => manga ? "manga" : "bolum";

        /// <summary>(hata, kodlu metin)</summary>
        public static (string? Hata, string Kodlu) MetinHazirla(string? ham)
        {
            if (string.IsNullOrWhiteSpace(ham)) return ("Yorum metni boş bırakılamaz.", "");
            var t = ham.Trim();
            if (t.Length > MaksHamUzunluk) return ("Yorum en fazla 500 karakter olabilir.", "");
            var h = YorumBicimi.Dogrula(t);
            if (h != null) return (h, "");
            var kodlu = Controllers.YorumKurallari.Kodla(t);
            if (kodlu.Length > 1000) return ("Yorum çok fazla özel karakter içeriyor, lütfen kısaltın.", "");
            return (null, kodlu);
        }

        public async Task<YorumBilgi?> BilgiAsync(bool manga, int id)
        {
            if (manga)
                return await _db.MangaYorumlari.AsNoTracking().Where(y => y.Id == id)
                    .Select(y => new YorumBilgi(y.Id, y.KullaniciId, y.Metin, y.MangaId, null, y.Manga.Yayinda, y.YanitId)).FirstOrDefaultAsync();
            return await _db.Yorumlar.AsNoTracking().Where(y => y.Id == id)
                .Select(y => new YorumBilgi(y.Id, y.KullaniciId, y.Metin, y.Bolum.MangaId, y.BolumId, y.Bolum.Manga.Yayinda, y.YanitId)).FirstOrDefaultAsync();
        }

        /// <summary>Yanıt hedefini çözer: aynı bölüm/seride olmalı; yanıta yanıt kök yoruma bağlanır.</summary>
        public async Task<(int? KokId, string? Hata)> YanitHedefiAsync(bool manga, int ustId, int? yanitId)
        {
            if (yanitId == null) return (null, null);
            int? hedefUst; int? hedefYanit;
            if (manga)
            {
                var h = await _db.MangaYorumlari.AsNoTracking().Where(y => y.Id == yanitId).Select(y => new { U = (int?)y.MangaId, y.YanitId }).FirstOrDefaultAsync();
                hedefUst = h?.U; hedefYanit = h?.YanitId;
            }
            else
            {
                var h = await _db.Yorumlar.AsNoTracking().Where(y => y.Id == yanitId).Select(y => new { U = (int?)y.BolumId, y.YanitId }).FirstOrDefaultAsync();
                hedefUst = h?.U; hedefYanit = h?.YanitId;
            }
            if (hedefUst == null || hedefUst != ustId) return (null, "Yanıtlanacak yorum bulunamadı.");
            return (hedefYanit ?? yanitId, null);
        }

        private IQueryable<YorumDto> Proje(bool manga, int ustId, int uid)
        {
            if (manga)
                return _db.MangaYorumlari.AsNoTracking().Where(y => y.MangaId == ustId).Select(y => new YorumDto
                {
                    Id = y.Id, KullaniciId = y.KullaniciId, KullaniciAdi = y.Kullanici.KullaniciAdi, Metin = y.Metin, Tarih = y.Tarih,
                    BegeniSayisi = _db.YorumBegenileri.Count(b => b.Tur == "manga" && b.YorumId == y.Id),
                    BenBegendim = _db.YorumBegenileri.Any(b => b.Tur == "manga" && b.YorumId == y.Id && b.KullaniciId == uid),
                    BenimYorumum = y.KullaniciId == uid, Duzenlendi = y.Duzenlendi, Spoiler = y.Spoiler, YanitId = y.YanitId,
                    YanitSayisi = _db.MangaYorumlari.Count(r => r.YanitId == y.Id)
                });
            return _db.Yorumlar.AsNoTracking().Where(y => y.BolumId == ustId).Select(y => new YorumDto
            {
                Id = y.Id, KullaniciId = y.KullaniciId, KullaniciAdi = y.Kullanici.KullaniciAdi, Metin = y.Metin, Tarih = y.Tarih,
                BegeniSayisi = _db.YorumBegenileri.Count(b => b.Tur == "bolum" && b.YorumId == y.Id),
                BenBegendim = _db.YorumBegenileri.Any(b => b.Tur == "bolum" && b.YorumId == y.Id && b.KullaniciId == uid),
                BenimYorumum = y.KullaniciId == uid, Duzenlendi = y.Duzenlendi, Spoiler = y.Spoiler, YanitId = y.YanitId,
                YanitSayisi = _db.Yorumlar.Count(r => r.YanitId == y.Id)
            });
        }

        /// <summary>Düz liste: sayfadaki kök yorumlar (sirala: yeni | begeni), her kökün hemen ardından en çok 20 yanıtı. Toplam = kök sayısı.</summary>
        public async Task<(List<YorumDto> Liste, int Toplam)> ListeAsync(bool manga, int ustId, int? kullaniciId, string? sirala, int page, int pageSize)
        {
            var uid = kullaniciId ?? 0;
            var q = Proje(manga, ustId, uid);
            var kok = q.Where(d => d.YanitId == null);
            var toplam = await kok.CountAsync();
            var siraliKok = (sirala ?? "").Trim().ToLowerInvariant() == "begeni"
                ? kok.OrderByDescending(d => d.BegeniSayisi).ThenByDescending(d => d.Tarih).ThenByDescending(d => d.Id)
                : kok.OrderByDescending(d => d.Tarih).ThenByDescending(d => d.Id);
            var kokler = await siraliKok.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            var liste = new List<YorumDto>();
            if (kokler.Count > 0)
            {
                var kokIdler = kokler.Select(k => k.Id).ToList();
                List<(int Id, int Kok)> hafif;
                if (manga)
                    hafif = (await _db.MangaYorumlari.AsNoTracking().Where(y => y.YanitId != null && kokIdler.Contains(y.YanitId.Value))
                        .OrderBy(y => y.Tarih).ThenBy(y => y.Id).Select(y => new { y.Id, Kok = y.YanitId!.Value }).ToListAsync())
                        .Select(x => (x.Id, x.Kok)).ToList();
                else
                    hafif = (await _db.Yorumlar.AsNoTracking().Where(y => y.YanitId != null && kokIdler.Contains(y.YanitId.Value))
                        .OrderBy(y => y.Tarih).ThenBy(y => y.Id).Select(y => new { y.Id, Kok = y.YanitId!.Value }).ToListAsync())
                        .Select(x => (x.Id, x.Kok)).ToList();
                var secilen = hafif.GroupBy(h => h.Kok).ToDictionary(g => g.Key, g => g.Take(KokBasinaYanit).Select(x => x.Id).ToList());
                var yanitIdler = secilen.Values.SelectMany(v => v).ToList();
                var yanitlar = yanitIdler.Count == 0 ? new Dictionary<int, YorumDto>()
                    : (await q.Where(d => yanitIdler.Contains(d.Id)).ToListAsync()).ToDictionary(d => d.Id);
                foreach (var k in kokler)
                {
                    liste.Add(k);
                    if (secilen.TryGetValue(k.Id, out var ids))
                        foreach (var id in ids) if (yanitlar.TryGetValue(id, out var yd)) liste.Add(yd);
                }
            }
            await StilEkleAsync(liste);
            return (liste, toplam);
        }

        public async Task<YorumDto?> TekAsync(bool manga, int ustId, int id, int kullaniciId)
        {
            var d = await Proje(manga, ustId, kullaniciId).Where(x => x.Id == id).FirstOrDefaultAsync();
            if (d != null) await StilEkleAsync(new List<YorumDto> { d });
            return d;
        }

        public async Task StilEkleAsync(List<YorumDto> liste)
        {
            if (liste.Count == 0) return;
            var stiller = await _tercihler.CozAsync(liste.Select(d => d.KullaniciId));
            foreach (var d in liste) d.AdStili = stiller.GetValueOrDefault(d.KullaniciId);
        }

        /// <summary>Yorumu, yanıtlarını, beğenilerini, şikayetlerini ve puan satırlarını siler. Silinen yorum kimliklerini döner.</summary>
        public async Task<List<int>> SilAsync(bool manga, int id)
        {
            var tur = Tur(manga);
            var idler = new List<int> { id };
            if (manga)
                idler.AddRange(await _db.MangaYorumlari.AsNoTracking().Where(y => y.YanitId == id).Select(y => y.Id).ToListAsync());
            else
                idler.AddRange(await _db.Yorumlar.AsNoTracking().Where(y => y.YanitId == id).Select(y => y.Id).ToListAsync());

            await using var tx = await _db.Database.BeginTransactionAsync();
            await _db.YorumBegenileri.Where(b => b.Tur == tur && idler.Contains(b.YorumId)).ExecuteDeleteAsync();
            await _db.YorumSikayetleri.Where(b => b.Tur == tur && idler.Contains(b.YorumId)).ExecuteDeleteAsync();
            if (manga) await _db.MangaYorumlari.Where(y => idler.Contains(y.Id)).ExecuteDeleteAsync();
            else await _db.Yorumlar.Where(y => idler.Contains(y.Id)).ExecuteDeleteAsync();
            await tx.CommitAsync();

            var onek = manga ? "mangayorum:" : "yorum:";
            await PuanServisi.YorumPuaniniSilAsync(_db, idler.Select(i => onek + i));
            return idler;
        }
    }
}
