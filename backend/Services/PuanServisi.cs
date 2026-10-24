using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Kage95.Backend.Data;
using Kage95.Backend.Models;

namespace Kage95.Backend.Services
{
    public record KazanimSonucu(int Kazanilan, string? Mesaj);

    /// <summary>
    /// Kullanıcı puan sistemi. Puan bir defterdir (PuanKayitlari): toplam = SUM(Puan).
    /// Tüm sabitler ve kurallar bu sınıftadır; hata fırlatmaz (puan hatası ana işlemi bozmaz).
    /// </summary>
    public static class PuanServisi
    {
        // ---- Eylemler ----
        public const string BolumOku = "bolum_oku";
        public const string BolumBasla = "bolum_basla";   // 0 puanlık sayaç kaydı; liderlikte sayılmaz
        public const string Yorum = "yorum";
        public const string PuanVer = "puan_ver";
        public const string KutuphaneEkle = "kutuphane_ekle";
        public const string GunlukGiris = "gunluk_giris";
        public const string Seri = "seri";
        public const string ProfilResmi = "profil_resmi";

        // ---- Puanlar ----
        public const int BolumOkuPuan = 10;
        public const int YorumPuan = 5;
        public const int PuanVerPuan = 2;
        public const int KutuphaneEklePuan = 3;
        public const int GunlukGirisPuan = 2;
        public const int SeriPuan = 10;
        public const int SeriGunSayisi = 7;
        public const int ProfilResmiPuan = 10;

        // ---- Sınırlar (adet / gün) ----
        public const int GunlukBolum = 10;
        public const int GunlukYorum = 5;
        public const int GunlukPuanVer = 10;
        public const int GunlukKutuphane = 5;
        public const int GunlukTavan = 250;
        public const int YorumMinUzunluk = 20;
        public const int YorumMinFarkliKarakter = 6;
        public const int SayfaBasinaSaniye = 4;
        public const int MaksOkumaSuresiSaniye = 90;

        private sealed record Kural(int Puan, bool TekSeferlik, int GunlukAdet);

        private static readonly Dictionary<string, Kural> Kurallar = new()
        {
            [BolumOku] = new(BolumOkuPuan, true, GunlukBolum),
            [Yorum] = new(YorumPuan, true, GunlukYorum),
            [PuanVer] = new(PuanVerPuan, true, GunlukPuanVer),
            [KutuphaneEkle] = new(KutuphaneEklePuan, true, GunlukKutuphane),
            [GunlukGiris] = new(GunlukGirisPuan, true, 0),
            [Seri] = new(SeriPuan, true, 0),
            [ProfilResmi] = new(ProfilResmiPuan, true, 0),
        };

        // ---- Seviye: L seviyesine ulasmak icin gereken toplam puan = 50*(L-1)*L (en yuksek seviye 50) ----
        public const int MaksSeviye = 50;
        public static int SeviyeIcinGereken(int seviye) => 50 * (Math.Max(1, seviye) - 1) * Math.Max(1, seviye);

        public static int Seviye(int toplam)
        {
            var l = 1;
            while (l < MaksSeviye && toplam >= SeviyeIcinGereken(l + 1)) l++;
            return l;
        }

        /// <summary>Sonraki seviye icin kalan puan; en ust seviyedeyse 0.</summary>
        public static int SonrakiSeviyeIcinKalan(int toplam)
        {
            var l = Seviye(toplam);
            return l >= MaksSeviye ? 0 : SeviyeIcinGereken(l + 1) - toplam;
        }

        // Rutbeler seviyeye baglidir: Cirak (1-2), Genc Golge (3-5), Golge (6-11), Usta (12-24), Efsane (25+)
        public static readonly (int Seviye, string Ad)[] Rutbeler =
        {
            (1, "Çırak"), (3, "Genç Gölge"), (6, "Gölge"), (12, "Usta"), (25, "Efsane")
        };

        public static string RutbeSeviyeden(int seviye)
        {
            var ad = Rutbeler[0].Ad;
            foreach (var r in Rutbeler) if (seviye >= r.Seviye) ad = r.Ad;
            return ad;
        }

        public static string Rutbe(int toplam) => RutbeSeviyeden(Seviye(toplam));

        /// <summary>Sonraki rütbenin adı ve kalan puan; en üst rütbedeyse (null, 0).</summary>
        public static (string? Ad, int Kalan) SonrakiRutbe(int toplam)
        {
            var l = Seviye(toplam);
            foreach (var r in Rutbeler) if (l < r.Seviye) return (r.Ad, SeviyeIcinGereken(r.Seviye) - toplam);
            return (null, 0);
        }

        // ---- Zaman (Europe/Istanbul günü) ----
        private static readonly TimeZoneInfo Tz = TzBul();

        private static TimeZoneInfo TzBul()
        {
            foreach (var id in new[] { "Europe/Istanbul", "Turkey Standard Time" })
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch { }
            }
            return TimeZoneInfo.CreateCustomTimeZone("TR", TimeSpan.FromHours(3), "TR", "TR");
        }

        public static DateTime YerelSimdi() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Tz);
        public static DateTime YerelGunBaslangicUtc(DateTime yerelGun)
            => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(yerelGun.Date, DateTimeKind.Unspecified), Tz);
        public static DateTime BugunBaslangicUtc() => YerelGunBaslangicUtc(YerelSimdi());
        private static string GunAnahtari(DateTime yerelGun) => yerelGun.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        /// <summary>Liderlik önbelleğini geçersiz kılmak için artan sürüm numarası.</summary>
        private static int _surum;
        public static int Surum => Volatile.Read(ref _surum);
        public static void Gecersizlestir() => Interlocked.Increment(ref _surum);

        // ---- Genel kazandırma ----

        public static async Task<KazanimSonucu> KazandirAsync(AppDbContext db, int kullaniciId, string eylem, string? referans)
        {
            try
            {
                if (kullaniciId <= 0 || !Kurallar.ContainsKey(eylem)) return new(0, null);
                await GunlukGirisAsync(db, kullaniciId);
                var kazanilan = await YazAsync(db, kullaniciId, eylem, referans);
                return kazanilan > 0 ? new(kazanilan, $"+{kazanilan} puan") : new(0, null);
            }
            catch
            {
                return new(0, null);
            }
        }

        /// <summary>Günde bir kez giriş puanı ve 7 günlük seri bonusu. Hata fırlatmaz; kazanılan puanı döner.</summary>
        public static async Task<int> GunlukGirisAsync(AppDbContext db, int kullaniciId)
        {
            try
            {
                var bugun = YerelSimdi();
                var anahtar = "gun:" + GunAnahtari(bugun);
                if (await db.PuanKayitlari.AsNoTracking().AnyAsync(p => p.KullaniciId == kullaniciId && p.Eylem == GunlukGiris && p.Referans == anahtar))
                    return 0;

                var kazanilan = await YazAsync(db, kullaniciId, GunlukGiris, anahtar);
                if (kazanilan <= 0) return 0;

                var gunler = await db.PuanKayitlari.AsNoTracking()
                    .Where(p => p.KullaniciId == kullaniciId && p.Eylem == GunlukGiris)
                    .OrderByDescending(p => p.Tarih).Select(p => p.Referans).Take(400).ToListAsync();
                var set = new HashSet<string>(gunler.Where(g => g != null).Select(g => g!));
                var seri = 0;
                for (var g = bugun.Date; set.Contains("gun:" + GunAnahtari(g)); g = g.AddDays(-1)) seri++;
                if (seri > 0 && seri % SeriGunSayisi == 0)
                    kazanilan += await YazAsync(db, kullaniciId, Seri, "seri:" + GunAnahtari(bugun));
                return kazanilan;
            }
            catch { return 0; }
        }

        private static async Task<int> YazAsync(AppDbContext db, int kullaniciId, string eylem, string? referans)
        {
            var kural = Kurallar[eylem];
            if (kural.TekSeferlik)
            {
                if (string.IsNullOrEmpty(referans) || referans.Length > 60) return 0;
                if (await db.PuanKayitlari.AsNoTracking().AnyAsync(p => p.KullaniciId == kullaniciId && p.Eylem == eylem && p.Referans == referans))
                    return 0;
            }

            var bas = BugunBaslangicUtc();
            if (kural.GunlukAdet > 0)
            {
                var adet = await db.PuanKayitlari.AsNoTracking()
                    .CountAsync(p => p.KullaniciId == kullaniciId && p.Eylem == eylem && p.Tarih >= bas);
                if (adet >= kural.GunlukAdet) return 0;
            }

            var bugunToplam = await db.PuanKayitlari.AsNoTracking()
                .Where(p => p.KullaniciId == kullaniciId && p.Tarih >= bas && p.Puan > 0)
                .SumAsync(p => (int?)p.Puan) ?? 0;
            var puan = Math.Min(kural.Puan, GunlukTavan - bugunToplam);
            if (puan <= 0) return 0;

            var kayit = new PuanKaydi { KullaniciId = kullaniciId, Eylem = eylem, Puan = puan, Referans = referans, Tarih = DateTime.UtcNow };
            db.PuanKayitlari.Add(kayit);
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                db.Entry(kayit).State = EntityState.Detached; // eşzamanlı çift kayıt (UNIQUE ihlali) ya da başka hata
                return 0;
            }
            Gecersizlestir();
            return puan;
        }

        // ---- Bölüm okuma ----

        /// <summary>
        /// İlerleme kaydında çağrılır. Bölümün ilk kaydında 0 puanlık "bolum_basla" yazar; son sayfaya,
        /// yeterli süre geçtikten sonra gelinmişse bolum_oku puanı verir.
        /// </summary>
        public static async Task<KazanimSonucu> OkumaIlerlemesiAsync(AppDbContext db, int kullaniciId, int bolumId, int sayfaNo)
        {
            try
            {
                var referans = "bolum:" + bolumId;
                var basla = await db.PuanKayitlari
                    .FirstOrDefaultAsync(p => p.KullaniciId == kullaniciId && p.Eylem == BolumBasla && p.Referans == referans);
                if (basla == null)
                {
                    var kayit = new PuanKaydi { KullaniciId = kullaniciId, Eylem = BolumBasla, Puan = 0, Referans = referans, Tarih = DateTime.UtcNow };
                    db.PuanKayitlari.Add(kayit);
                    try { await db.SaveChangesAsync(); }
                    catch (DbUpdateException) { db.Entry(kayit).State = EntityState.Detached; }
                    return new(0, null); // ilk kayıtta süre henüz geçmedi
                }

                var sayfalar = await db.Sayfalar.AsNoTracking().Where(s => s.BolumId == bolumId)
                    .GroupBy(s => s.BolumId).Select(g => new { Adet = g.Count(), Son = g.Max(s => s.SayfaNo) }).FirstOrDefaultAsync();
                if (sayfalar == null || sayfalar.Adet == 0 || sayfaNo < sayfalar.Son) return new(0, null);

                var gerekenSn = Math.Min(sayfalar.Adet * SayfaBasinaSaniye, MaksOkumaSuresiSaniye);
                if ((DateTime.UtcNow - basla.Tarih).TotalSeconds < gerekenSn) return new(0, null);

                return await KazandirAsync(db, kullaniciId, BolumOku, referans);
            }
            catch { return new(0, null); }
        }

        // ---- Yorum ----

        /// <summary>Yorum puanı: ham metin yeterince uzun/çeşitli olmalı ve kullanıcının önceki yorumlarından farklı olmalı.</summary>
        public static async Task<KazanimSonucu> YorumPuaniAsync(AppDbContext db, int kullaniciId, bool mangaYorumu, int yorumId, string hamMetin, string kodluMetin)
        {
            try
            {
                var metin = (hamMetin ?? string.Empty).Trim();
                if (metin.Length < YorumMinUzunluk || metin.Distinct().Count() < YorumMinFarkliKarakter) return new(0, null);

                var onceki = await db.Yorumlar.AsNoTracking().AnyAsync(y => y.KullaniciId == kullaniciId && y.Metin == kodluMetin && (mangaYorumu || y.Id != yorumId))
                    || await db.MangaYorumlari.AsNoTracking().AnyAsync(y => y.KullaniciId == kullaniciId && y.Metin == kodluMetin && (!mangaYorumu || y.Id != yorumId));
                if (onceki) return new(0, null);

                return await KazandirAsync(db, kullaniciId, Yorum, (mangaYorumu ? "mangayorum:" : "yorum:") + yorumId);
            }
            catch { return new(0, null); }
        }

        /// <summary>Silinen yorumların puanlarını geri alır (satırları siler). Hata fırlatmaz.</summary>
        public static async Task YorumPuaniniSilAsync(AppDbContext db, IEnumerable<string> referanslar)
        {
            try
            {
                var liste = referanslar.Distinct().ToList();
                if (liste.Count == 0) return;
                await db.PuanKayitlari.Where(p => p.Eylem == Yorum && p.Referans != null && liste.Contains(p.Referans)).ExecuteDeleteAsync();
                Gecersizlestir();
            }
            catch { }
        }

        public static Task YorumPuaniniSilAsync(AppDbContext db, bool mangaYorumu, int yorumId)
            => YorumPuaniniSilAsync(db, new[] { (mangaYorumu ? "mangayorum:" : "yorum:") + yorumId });

        // ---- Özet / açıklama ----

        public static string Aciklama(string eylem) => eylem switch
        {
            BolumOku => "Bölüm okudun",
            Yorum => "Yorum yaptın",
            PuanVer => "Puan verdin",
            KutuphaneEkle => "Kütüphaneye ekledin",
            GunlukGiris => "Günlük giriş",
            Seri => "7 günlük seri bonusu",
            ProfilResmi => "Profil resmi yükledin",
            _ => eylem
        };

        public static List<PuanKuraliDto> KuralListesi() => new()
        {
            new() { Eylem = BolumOku, Ad = "Bölüm oku", Puan = BolumOkuPuan, Kural = $"Bölümü sonuna kadar gerçekten oku (hızlı geçmek puan vermez). Bölüm başına bir kez, günde en fazla {GunlukBolum} bölüm." },
            new() { Eylem = Yorum, Ad = "Yorum yap", Puan = YorumPuan, Kural = $"En az {YorumMinUzunluk} karakterlik, daha önce yazmadığın bir yorum. Günde en fazla {GunlukYorum} yorum puan verir." },
            new() { Eylem = PuanVer, Ad = "Puan ver", Puan = PuanVerPuan, Kural = $"Seri ya da bölüme ilk kez puan ver. Hedef başına bir kez, günde en fazla {GunlukPuanVer} oy." },
            new() { Eylem = KutuphaneEkle, Ad = "Kütüphaneye ekle", Puan = KutuphaneEklePuan, Kural = $"Seri başına bir kez (çıkarıp eklemek tekrar puan vermez), günde en fazla {GunlukKutuphane} seri." },
            new() { Eylem = GunlukGiris, Ad = "Günlük giriş", Puan = GunlukGirisPuan, Kural = "Her gün siteye girdiğinde bir kez." },
            new() { Eylem = Seri, Ad = "Seri bonusu", Puan = SeriPuan, Kural = $"{SeriGunSayisi} gün üst üste girdiğinde, her {SeriGunSayisi} günde bir." },
            new() { Eylem = ProfilResmi, Ad = "Profil resmi", Puan = ProfilResmiPuan, Kural = "Profil resmi yüklediğinde bir kez." },
        };
    }
}
