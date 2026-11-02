namespace Kage95.Backend.Models;
public sealed class PremiumUyelik
{
    [System.ComponentModel.DataAnnotations.Key]
    public int KullaniciId { get; set; }
    public DateTime BitisUtc { get; set; }
    public bool IptalEdildi { get; set; }
}
