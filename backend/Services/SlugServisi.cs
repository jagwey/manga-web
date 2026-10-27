using System.Text.RegularExpressions;

namespace Kage95.Backend.Services
{
    public static class SlugServisi
    {
        private static readonly Regex GecersizKarakterler = new(@"[^a-z0-9]+", RegexOptions.Compiled);
        private static readonly Regex CiftTire = new(@"-+", RegexOptions.Compiled);

        /// <summary>
        /// Manga adı gibi metinleri SEO uyumlu, temiz URL slug'ına dönüştürür.
        /// Örnek: "Chainsaw Man" -> "chainsaw-man", "Örnek Manga 1" -> "ornek-manga-1"
        /// </summary>
        public static string Olustur(string? metin)
        {
            if (string.IsNullOrWhiteSpace(metin)) return "";

            var s = metin.Trim();

            // Türkçe karakterleri ve büyük-küçük İ/I dönüşümlerini düzelt
            s = s.Replace("İ", "i")
                 .Replace("I", "i")
                 .Replace("ı", "i")
                 .Replace("ğ", "g")
                 .Replace("Ğ", "g")
                 .Replace("ü", "u")
                 .Replace("Ü", "u")
                 .Replace("ş", "s")
                 .Replace("Ş", "s")
                 .Replace("ö", "o")
                 .Replace("Ö", "o")
                 .Replace("ç", "c")
                 .Replace("Ç", "c");

            s = s.ToLowerInvariant();

            // Harf ve rakam dışındaki karakterleri tireye çevir
            s = GecersizKarakterler.Replace(s, "-");
            s = CiftTire.Replace(s, "-");
            s = s.Trim('-');

            return s;
        }

        /// <summary>
        /// Slug çakışırsa sonuna -2, -3 ekler; ad latin harf içermiyorsa "manga" kullanılır.
        /// </summary>
        public static string Benzersiz(string? ad, Func<string, bool> varMi)
        {
            var taban = Olustur(ad);
            if (string.IsNullOrEmpty(taban)) taban = "manga";
            var aday = taban;
            for (var n = 2; varMi(aday); n++) aday = taban + "-" + n;
            return aday;
        }
    }
}
