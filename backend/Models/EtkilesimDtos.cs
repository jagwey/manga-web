using System.Text.Json.Serialization;

namespace Kage95.Backend.Models
{
    /// <summary>Puan özeti. Null alanlar da JSON'a yazılır (ortalama/benimPuanim: null).</summary>
    public class PuanOzetiDto
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public decimal? Ortalama { get; set; }
        public int OySayisi { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public int? BenimPuanim { get; set; }
        public int PuanKazanildi { get; set; }
    }

    public class PuanVerRequest
    {
        public int? Deger { get; set; }
    }
}
