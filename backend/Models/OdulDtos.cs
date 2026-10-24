namespace Kage95.Backend.Models
{
    /// <summary>Kullanıcının geçerli (seviyesi yeten) kozmetik tercihleri. Boş alanlar JSON'a yazılmaz.</summary>
    public class AdStiliDto
    {
        public string? AdRengi { get; set; }
        public string? AdRengiHex { get; set; }
        public string? MesajFontu { get; set; }
        public bool? Parlak { get; set; }
        public bool? Animasyon { get; set; }
        public bool? Rozet { get; set; }
        public string? Unvan { get; set; }
    }

    public class TercihGuncelleRequest
    {
        public string? AdRengi { get; set; }
        public string? MesajFontu { get; set; }
        public bool? Parlak { get; set; }
        public bool? Animasyon { get; set; }
        public bool? Rozet { get; set; }
        public string? Unvan { get; set; }
    }
}
