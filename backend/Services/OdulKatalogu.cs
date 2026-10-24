using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Kage95.Backend.Services
{
    public record OdulRenk(string Id, string Ad, string Hex, int Seviye);
    public record OdulFont(string Id, string Ad, string Yigin, int Seviye);
    public record OdulSticker(string Id, string Ad);
    public record OdulStickerPaketi(string Id, string Ad, int Seviye, IReadOnlyList<OdulSticker> Stickerlar);
    public record OdulSeviye(int Seviye, string Ad, string Aciklama, IReadOnlyList<string> Odul);

    /// <summary>
    /// Seviye odulleri (yalnizca kozmetik) icin TEK katalog. Sunucu dogrulamasi ve GET /api/oduller buradan beslenir.
    /// Sticker gorselleri frontend'de cizilir; burada yalnizca ASCII kimlikler tutulur.
    /// </summary>
    public static class OdulKatalogu
    {
        public const int AdRengiSeviye = 3;
        public const int FontSeviye = 5;
        public const int ParlakSeviye = 10;
        public const int AnimasyonSeviye = 20;
        public const int RozetSeviye = 20;
        public const int UnvanSeviye = 30;
        public const int UnvanMaksUzunluk = 24;

        public static readonly IReadOnlyList<OdulRenk> Renkler = new List<OdulRenk>
        {
            new("kirmizi", "Kırmızı", "#e53935", 3),
            new("mavi", "Mavi", "#4aa3ff", 3),
            new("turuncu", "Turuncu", "#ff9800", 3),
            new("mor", "Mor", "#a56bff", 3),
            new("gri", "Gri", "#9aa0a6", 3),
            new("sari", "Sarı", "#ffd54f", 3),
            new("turkuaz", "Turkuaz", "#26c6da", 3),
            new("kehribar", "Kehribar", "#ffb300", 10),
            new("gumus", "Gümüş", "#cfd8dc", 10),
            new("mercan", "Mercan", "#ff7f6e", 10),
        };

        public static readonly IReadOnlyList<OdulFont> Fontlar = new List<OdulFont>
        {
            new("varsayilan", "Varsayılan", "inherit", 1),
            new("serif", "Serif", "Georgia, 'Times New Roman', Times, serif", 5),
            new("mono", "Mono", "'Courier New', Consolas, Menlo, monospace", 5),
            new("elyazisi", "El yazısı", "'Segoe Print', 'Bradley Hand', 'Comic Sans MS', cursive", 5),
        };

        private static IReadOnlyList<OdulSticker> Liste(string paket, params (string Id, string Ad)[] ogeler)
            => ogeler.Select(o => new OdulSticker(paket + ":" + o.Id, o.Ad)).ToList();

        public static readonly IReadOnlyList<OdulStickerPaketi> StickerPaketleri = new List<OdulStickerPaketi>
        {
            new("sade", "Sade", 8, Liste("sade",
                ("ok", "Ok"), ("kalp", "Kalp"), ("yildiz", "Yıldız"), ("tebrik", "Tebrik"), ("gulumse", "Gülümse"), ("uzgun", "Üzgün"),
                ("saskin", "Şaşkın"), ("kilic", "Kılıç"), ("kitap", "Kitap"), ("cay", "Çay"), ("uyku", "Uyku"), ("bravo", "Bravo"))),
            new("golge", "Gölge", 15, Liste("golge",
                ("golge", "Gölge"), ("maske", "Maske"), ("ay", "Ay"), ("kaplan", "Kaplan"), ("shuriken", "Shuriken"), ("fener", "Fener"),
                ("sakura", "Sakura"), ("ejderha", "Ejderha"), ("daga", "Hançer"), ("tilsim", "Tılsım"), ("bulut", "Bulut"), ("ates", "Ateş"))),
        };

        public static readonly IReadOnlyList<OdulSeviye> Seviyeler = new List<OdulSeviye>
        {
            new(3, "Ad rengi", "Kullanıcı adının rengini paletten seçebilirsin.", new[] { "ad rengi (7 renk)" }),
            new(5, "Sohbet yazı tipi", "Sohbet mesajların için yazı tipi seçebilirsin.", new[] { "yazı tipi: serif, mono, el yazısı" }),
            new(8, "Sade sticker paketi", "Sohbette kullanabileceğin 12 sticker.", new[] { "sticker paketi: Sade" }),
            new(10, "Parlak kullanıcı adı", "Kullanıcı adın parlar; ad rengi paleti kehribar, gümüş ve mercanla genişler.", new[] { "parlak ad", "ek renkler: kehribar, gümüş, mercan" }),
            new(15, "Gölge sticker paketi", "Sohbette kullanabileceğin 12 sticker daha.", new[] { "sticker paketi: Gölge" }),
            new(20, "Animasyonlu parlama ve rozet", "Parlama hareketlenir; kullanıcı adının yanında rozet çerçevesi görünür.", new[] { "animasyonlu parlama", "rozet çerçevesi" }),
            new(30, "Özel unvan", "Adının yanında görünecek 24 karakterlik kendi unvanını yazabilirsin.", new[] { "özel unvan metni" }),
        };

        public static OdulRenk? Renk(string? id) => Renkler.FirstOrDefault(r => r.Id == id);
        public static OdulFont? Font(string? id) => Fontlar.FirstOrDefault(f => f.Id == id);
        public static OdulStickerPaketi? Paket(string? id) => StickerPaketleri.FirstOrDefault(p => p.Id == id);

        /// <summary>"paket:sticker" kimligini cozer; gecersizse null.</summary>
        public static (OdulStickerPaketi Paket, OdulSticker Sticker)? Sticker(string? id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 40) return null;
            var i = id.IndexOf(':');
            if (i <= 0) return null;
            var paket = Paket(id[..i]);
            var st = paket?.Stickerlar.FirstOrDefault(s => s.Id == id);
            return paket == null || st == null ? null : (paket, st);
        }

        // ---------- Unvan dogrulamasi ----------

        private static readonly Regex UnvanKarakter = new(@"^[\p{L}\p{Nd} .,'!&\-]+$", RegexOptions.Compiled);
        private static readonly Regex UrlBenzeri = new(@"(https?|ftp)|www|://|\.(com|net|org|xyz|io|tr|ru|me|co|gg|ly|tk)\b|\bdiscord", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly string[] KisaKufur = { "amk", "aq", "sik", "got", "pic", "oc", "sg", "mk" };
        private static readonly string[] UzunKufur =
        {
            "orospu", "yarrak", "amcik", "sikeyim", "sikik", "siktir", "ibne", "gavat", "pezevenk", "kahpe", "serefsiz", "puşt", "pust",
            "fuck", "shit", "bitch", "asshole", "dick", "nigger", "faggot", "cunt"
        };

        private static string Sadelestir(string s)
        {
            var d = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in d)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                sb.Append(c == 'ı' || c == 'İ' ? 'i' : char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        /// <summary>Unvan hatasi (Turkce mesaj) ya da null.</summary>
        public static string? UnvanHatasi(string? unvan)
        {
            if (string.IsNullOrWhiteSpace(unvan)) return "Unvan boş olamaz.";
            var t = unvan.Trim();
            if (t.Length > UnvanMaksUzunluk) return $"Unvan en fazla {UnvanMaksUzunluk} karakter olabilir.";
            if (!UnvanKarakter.IsMatch(t)) return "Unvan yalnızca harf, rakam, boşluk ve . , ' ! & - işaretlerini içerebilir.";
            if (UrlBenzeri.IsMatch(t)) return "Unvan bağlantı ya da site adresi içeremez.";
            var sade = Sadelestir(t);
            var kelimeler = Regex.Split(sade, @"[^a-z0-9]+").Where(k => k.Length > 0).ToList();
            if (kelimeler.Any(k => KisaKufur.Contains(k))) return "Unvan uygunsuz ifade içeremez.";
            var bitisik = new string(sade.Where(char.IsLetterOrDigit).ToArray());
            if (UzunKufur.Any(k => bitisik.Contains(Sadelestir(k)))) return "Unvan uygunsuz ifade içeremez.";
            return null;
        }
    }
}
