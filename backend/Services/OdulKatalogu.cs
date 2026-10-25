using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Kage95.Backend.Services
{
    public record OdulRenk(string Id, string Ad, string Hex, int Seviye, string Kategori);
    public record OdulFont(string Id, string Ad, string Yigin, int Seviye, string Kategori);
    public record OdulEfekt(string Id, string Ad, string Aciklama, int Seviye);
    public record OdulUnvan(string Ad, string Kategori, int Seviye);
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
        public const int OzelRenkSeviye = 15;
        public const int FontSeviye = 5;
        public const int RozetSeviye = 20;
        public const int UnvanSeviye = 30;
        public const int UnvanMaksUzunluk = 24;

        public static readonly IReadOnlyList<OdulRenk> Renkler = new List<OdulRenk>
        {
            new("kirmizi", "Kırmızı", "#e53935", 3, "Temel"),
            new("mavi", "Mavi", "#4aa3ff", 3, "Temel"),
            new("turuncu", "Turuncu", "#ff9800", 3, "Temel"),
            new("mor", "Mor", "#a56bff", 3, "Temel"),
            new("gri", "Gri", "#9aa0a6", 3, "Temel"),
            new("sari", "Sarı", "#ffd54f", 3, "Temel"),
            new("turkuaz", "Turkuaz", "#26c6da", 3, "Temel"),
            new("sakura", "Sakura", "#ff9ec7", 6, "Pastel"),
            new("lavanta", "Lavanta", "#c3a6ff", 6, "Pastel"),
            new("nane", "Nane", "#7fe0c0", 6, "Pastel"),
            new("seftali", "Şeftali", "#ffb98a", 6, "Pastel"),
            new("kehribar", "Kehribar", "#ffb300", 10, "Derin"),
            new("gumus", "Gümüş", "#cfd8dc", 10, "Derin"),
            new("mercan", "Mercan", "#ff7f6e", 10, "Derin"),
            new("yesim", "Yeşim", "#3ccf91", 10, "Derin"),
            new("gece", "Gece mavisi", "#6c8cff", 10, "Derin"),
            new("kage", "Kage altını", "#f2c14e", 25, "Efsane"),
            new("kanay", "Kan ayı", "#ff3b5c", 25, "Efsane"),
            new("ruh", "Ruh mavisi", "#8ef6ff", 25, "Efsane"),
        };

        // Yigin: frontend js/adstili.js ile ayni tutulur (Google yazi tipleri orada tembel yuklenir).
        public static readonly IReadOnlyList<OdulFont> Fontlar = new List<OdulFont>
        {
            new("varsayilan", "Varsayılan", "inherit", 1, "Temel"),
            new("yuvarlak", "Yuvarlak", "'Nunito', system-ui, sans-serif", 5, "Temel"),
            new("genis", "Geniş", "'Rubik', system-ui, sans-serif", 8, "Temel"),
            new("serif", "Serif", "Georgia, 'Times New Roman', Times, serif", 5, "Klasik"),
            new("kitap", "Kitap", "'Lora', Georgia, serif", 8, "Klasik"),
            new("zarif", "Zarif", "'Playfair Display', Georgia, serif", 18, "Klasik"),
            new("mono", "Mono", "'Courier New', Consolas, Menlo, monospace", 5, "Kod"),
            new("terminal", "Terminal", "'JetBrains Mono', Consolas, monospace", 12, "Kod"),
            new("elyazisi", "El yazısı", "'Segoe Print', 'Bradley Hand', 'Comic Sans MS', cursive", 5, "Manga"),
            new("kalem", "Kalem", "'Patrick Hand', 'Comic Sans MS', cursive", 8, "Manga"),
            new("defter", "Defter", "'Kalam', 'Comic Sans MS', cursive", 12, "Manga"),
            new("kaligrafi", "Kaligrafi", "'Caveat', 'Segoe Print', cursive", 15, "Manga"),
            new("baskin", "Baskın", "'Bangers', Impact, sans-serif", 25, "Manga"),
        };

        /// <summary>Kullanici adi efektleri (tek secim). Frontend sinifi: as-ef-{Id}.</summary>
        public static readonly IReadOnlyList<OdulEfekt> Efektler = new List<OdulEfekt>
        {
            new("murekkep", "Mürekkep", "Adının altında fırça darbesi.", 10),
            new("aura", "Aura", "Adın hafifçe parlar.", 10),
            new("golge", "Gölge", "Adının arkasına koyu bir gölge düşer.", 12),
            new("hiz", "Hız çizgisi", "Manga hız çizgileri adının arkasından geçer.", 15),
            new("ton", "Ton noktası", "Adının arkasında manga tram deseni.", 18),
            new("nabiz", "Aura nabzı", "Parlama nefes alır gibi atar.", 20),
            new("varak", "Altın varak", "Adın altın yaldızla yazılır.", 25),
            new("alev", "Kage alevi", "Adının içinde renkler akar.", 40),
        };

        /// <summary>Hazir unvanlar. Bunlarin disindaki metinler ozel unvan sayilir (seviye 30 + filtre).</summary>
        public static readonly IReadOnlyList<OdulUnvan> Unvanlar = new List<OdulUnvan>
        {
            new("Çaylak Okur", "Okur", 1),
            new("Sayfa Avcısı", "Okur", 3),
            new("Bölüm Bekçisi", "Okur", 5),
            new("Cilt Koleksiyoncusu", "Okur", 8),
            new("Gece Okuru", "Okur", 11),
            new("Arşiv Ustası", "Okur", 18),
            new("Efsanevi Okur", "Okur", 25),
            new("Gölge Genin", "Gölge yolu", 2),
            new("Gölge Chunin", "Gölge yolu", 6),
            new("Gölge Jonin", "Gölge yolu", 12),
            new("Anbu", "Gölge yolu", 16),
            new("Sannin", "Gölge yolu", 24),
            new("Kage", "Gölge yolu", 35),
            new("Kage 95", "Gölge yolu", 95),
            new("Sohbet Ruhu", "Topluluk", 4),
            new("Yorum Ustası", "Topluluk", 9),
            new("Spoiler Bekçisi", "Topluluk", 14),
            new("Teori Ustası", "Topluluk", 20),
            new("Sensei", "Topluluk", 28),
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
            new("okur", "Okur", 4, Liste("okur",
                ("yeni", "Yeni bölüm"), ("yerimi", "Yer imi"), ("spoiler", "Spoiler yok"), ("panel", "Panel"), ("devam", "Devamı gelsin"), ("bekle", "Bekliyorum"),
                ("agla", "Ağladım"), ("gul", "Kahkaha"), ("teori", "Teorim var"), ("unlem", "Olamaz!!"), ("okundu", "Okudum"), ("kupa", "Başyapıt"))),
            new("manga", "Manga dili", 22, Liste("manga",
                ("ter", "Ter damlası"), ("damar", "Öfke damarı"), ("balon", "Konuşma balonu"), ("dusunce", "Düşünce balonu"), ("patlama", "Patlama"), ("parilti", "Parıltı"),
                ("hiz", "Hız çizgileri"), ("ruh", "Ruhum çıktı"), ("kizar", "Kızardım"), ("kalpgoz", "Aşık oldum"), ("sok", "Şok"), ("sessiz", "Sessizlik"))),
            new("kage", "Kage", 40, Liste("kage",
                ("sapka", "Kage şapkası"), ("kunai", "Kunai"), ("parsomen", "Parşömen"), ("muhur", "Mühür"), ("torii", "Torii"), ("katana", "Katana"),
                ("oni", "Oni maskesi"), ("yildirim", "Yıldırım"), ("ruzgar", "Rüzgar"), ("kale", "Kale"), ("goz", "Keskin göz"), ("amblem", "Kage 95"))),
        };

        /// <summary>Odul yolu: kataloglardan seviye seviye uretilir, boylece liste hic eskimez.</summary>
        public static readonly IReadOnlyList<OdulSeviye> Seviyeler = SeviyeleriUret();

        private static IReadOnlyList<OdulSeviye> SeviyeleriUret()
        {
            var ogeler = new List<(int Sv, string Tur, string Ad)>();
            ogeler.AddRange(Unvanlar.Select(u => (u.Seviye, "Unvan", u.Ad)));
            ogeler.AddRange(Renkler.Select(r => (r.Seviye, "Ad rengi", r.Ad)));
            ogeler.AddRange(Fontlar.Where(f => f.Id != "varsayilan").Select(f => (f.Seviye, "Yazı tipi", f.Ad)));
            ogeler.AddRange(Efektler.Select(e => (e.Seviye, "Ad efekti", e.Ad)));
            ogeler.AddRange(StickerPaketleri.Select(p => (p.Seviye, "Sticker paketi", p.Ad)));
            ogeler.Add((OzelRenkSeviye, "Ad rengi", "Kendi rengini seç"));
            ogeler.Add((RozetSeviye, "Çerçeve", "Mühür çerçevesi"));
            ogeler.Add((UnvanSeviye, "Unvan", "Kendi unvanını yaz"));
            return ogeler.GroupBy(o => o.Sv).OrderBy(g => g.Key).Select(g =>
            {
                var turler = g.GroupBy(o => o.Tur).Select(t => $"{t.Key}: {string.Join(", ", t.Select(o => o.Ad))}").ToList();
                return new OdulSeviye(g.Key, string.Join(" · ", g.Select(o => o.Tur).Distinct()), string.Join(" — ", turler), turler);
            }).ToList();
        }

        public static OdulRenk? Renk(string? id) => Renkler.FirstOrDefault(r => r.Id == id);

        /// <summary>Ozel ad rengi "#rrggbb" ise ve koyu/acik zeminde okunabiliyorsa kucuk harfli hex'i, degilse null dondurur.</summary>
        public static string? OzelRenk(string? hex)
        {
            if (hex == null || hex.Length != 7 || hex[0] != '#') return null;
            if (!int.TryParse(hex.AsSpan(1), System.Globalization.NumberStyles.HexNumber, null, out var v)) return null;
            double Kanal(int c) { var s = c / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
            var parlaklik = 0.2126 * Kanal((v >> 16) & 255) + 0.7152 * Kanal((v >> 8) & 255) + 0.0722 * Kanal(v & 255);
            return parlaklik is >= 0.06 and <= 0.75 ? hex.ToLowerInvariant() : null;
        }
        public static OdulFont? Font(string? id) => Fontlar.FirstOrDefault(f => f.Id == id);
        public static OdulEfekt? Efekt(string? id) => Efektler.FirstOrDefault(e => e.Id == id);
        public static OdulUnvan? HazirUnvan(string? ad) => string.IsNullOrWhiteSpace(ad) ? null : Unvanlar.FirstOrDefault(u => u.Ad == ad.Trim());

        /// <summary>Unvan icin gereken seviye: hazir unvansa kendi seviyesi, degilse ozel unvan seviyesi.</summary>
        public static int UnvanGerekenSeviye(string unvan) => HazirUnvan(unvan)?.Seviye ?? UnvanSeviye;
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
