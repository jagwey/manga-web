namespace Kage95.Backend.Models
{
    public class OneCikanYazRequest
    {
        public string? Baslik { get; set; }
        public string? Ozet { get; set; }
        public string? Etiket { get; set; }
        public string? GorselUrl { get; set; }
        public string? Baglanti { get; set; }
        public bool? Aktif { get; set; }
        public int? Sira { get; set; }
        public DateTimeOffset? BitisTarihi { get; set; }
        /// <summary>true ise mevcut bitiş tarihi kaldırılır (PUT).</summary>
        public bool? BitisiKaldir { get; set; }
    }
}
