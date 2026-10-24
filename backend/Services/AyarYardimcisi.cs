namespace Kage95.Backend.Services
{
    public class AyarlarDto
    {
        public bool KayitAcik { get; set; } = true;
        public bool YorumAcik { get; set; } = true;
        public bool BakimModu { get; set; }
        public string BakimMesaji { get; set; } = string.Empty;
        public bool DuyuruAcik { get; set; }
        public string DuyuruMetni { get; set; } = string.Empty;
        public bool SohbetAcik { get; set; } = true;
    }

    public static class AyarYardimcisi
    {
        public static async Task<AyarlarDto> OkuAsync(ISiteAyarServisi servis)
        {
            var t = await servis.TumuAsync();
            bool B(string a, bool v) => t.TryGetValue(a, out var d) ? d == "1" : v;
            string S(string a) => t.TryGetValue(a, out var d) ? d : string.Empty;
            return new AyarlarDto
            {
                KayitAcik = B("kayitAcik", true),
                YorumAcik = B("yorumAcik", true),
                BakimModu = B("bakimModu", false),
                BakimMesaji = S("bakimMesaji"),
                DuyuruAcik = B("duyuruAcik", false),
                DuyuruMetni = S("duyuruMetni"),
                SohbetAcik = B("sohbetAcik", true)
            };
        }
    }
}
