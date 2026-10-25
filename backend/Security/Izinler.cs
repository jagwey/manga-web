using Kage95.Backend.Models;

namespace Kage95.Backend.Security
{
    public record IzinTanimi(string Anahtar, string Ad, string Aciklama);

    public static class Roller
    {
        public const string Owner = "Owner";
        public const string Admin = "Admin";
        public const string User = "User";
    }

    /// <summary>İzin kataloğu ve kullanıcıdan izin çözümleme yardımcıları.</summary>
    public static class Izinler
    {
        public static readonly IReadOnlyList<IzinTanimi> Katalog = new List<IzinTanimi>
        {
            new("manga.yaz", "Seri ekle ve düzenle", "Yeni seri ekler, mevcut serileri ve kapak görsellerini düzenler."),
            new("manga.sil", "Seri sil", "Bir seriyi bölümleri, sayfaları ve yorumlarıyla birlikte siler."),
            new("bolum.yaz", "Bölüm ve sayfa ekle", "Seriye yeni bölüm ve sayfa görselleri ekler."),
            new("bolum.sil", "Bölüm sil", "Bir bölümü sayfaları ve yorumlarıyla birlikte siler."),
            new("kategori.yonet", "Kategorileri yönet", "Kategori ekler, yeniden adlandırır ve siler."),
            new("yorum.sil", "Yorumları denetle ve sil", "Tüm yorumları listeler ve uygunsuz olanları siler."),
            new("kullanici.gor", "Kullanıcıları görüntüle", "Kayıtlı kullanıcıların listesini görür."),
            new("kullanici.engelle", "Ban ve oturum yönetimi", "Kullanıcıyı süreli ya da süresiz banlar, banı kaldırır ve açık oturumlarını kapatır."),
            new("kullanici.duzenle", "Kullanıcı bilgileri, XP ve seviye", "Kullanıcının adını ve e-postasını değiştirir; XP ekler, düşer, XP'yi ya da seviyeyi doğrudan ayarlar."),
            new("kullanici.sifre", "Kullanıcı şifresi sıfırla", "Kullanıcıya yeni şifre belirler ya da geçici şifre üretir."),
            new("kullanici.sil", "Kullanıcı hesabı sil", "Bir kullanıcı hesabını ve ona bağlı verileri kalıcı olarak siler."),
            new("bolum.duzenle", "Bölüm ve sayfaları düzenle", "Bölüm adı/numarasını değiştirir; sayfaları sıralar, değiştirir ve siler."),
            new("kayit.gor", "Denetim kayıtlarını görüntüle", "Yönetim işlemlerinin kim tarafından ne zaman yapıldığını görür."),
            new("ayar.yonet", "Site ayarlarını yönet", "Duyuru, bakım modu, kayıt, yorum ve sohbet açık/kapalı ayarlarını değiştirir."),
            new("duyuru.yonet", "Duyuruları yönet", "Ana sayfadaki etiketli duyuruları ekler, düzenler ve siler."),
            new("sohbet.yonet", "Canlı sohbeti denetle", "Sohbet mesajlarını siler, kullanıcıları sohbette susturur ve susturmayı kaldırır.")
        };

        public static readonly IReadOnlyList<string> TumAnahtarlar = Katalog.Select(k => k.Anahtar).ToList();

        public static bool Gecerli(string anahtar) => TumAnahtarlar.Contains(anahtar);

        public static bool OwnerMi(Kullanici k) => string.Equals(k.Rol, Roller.Owner, StringComparison.Ordinal);
        public static bool AdminMi(Kullanici k) => string.Equals(k.Rol, Roller.Admin, StringComparison.Ordinal);

        /// <summary>Kullanıcının etkin izin listesi: Owner tümü, Admin kendi izinleri, diğerleri boş.</summary>
        public static List<string> Cozumle(Kullanici k)
        {
            if (OwnerMi(k)) return TumAnahtarlar.ToList();
            if (!AdminMi(k)) return new List<string>();
            return Ayristir(k.Yetkiler);
        }

        public static List<string> Ayristir(string? depolanan)
        {
            if (string.IsNullOrWhiteSpace(depolanan)) return new List<string>();
            return depolanan
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(Gecerli)
                .Distinct()
                .ToList();
        }

        public static string? Birlestir(IEnumerable<string> anahtarlar)
        {
            var liste = TumAnahtarlar.Where(a => anahtarlar.Contains(a)).ToList(); // katalog sırası, tekrarsız
            return liste.Count == 0 ? null : string.Join(",", liste);
        }

        public static bool Var(Kullanici k, string anahtar) => Cozumle(k).Contains(anahtar);

        /// <summary>Panele girebilir mi: Owner veya en az bir izni olan Admin.</summary>
        public static bool PaneleErisir(Kullanici k) => k.Aktif && Cozumle(k).Count > 0;
    }
}
