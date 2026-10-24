using Microsoft.AspNetCore.Mvc;
using Kage95.Backend.Services;

namespace Kage95.Backend.Controllers
{
    /// <summary>Seviye ödül kataloğu (anonim).</summary>
    [ApiController]
    [Route("api/oduller")]
    public class OdullerController : ControllerBase
    {
        [HttpGet]
        public IActionResult Getir() => Ok(new
        {
            seviyeler = OdulKatalogu.Seviyeler.Select(s => new { seviye = s.Seviye, ad = s.Ad, aciklama = s.Aciklama, odul = s.Odul }),
            renkler = OdulKatalogu.Renkler.Select(r => new { id = r.Id, ad = r.Ad, hex = r.Hex, seviye = r.Seviye }),
            fontlar = OdulKatalogu.Fontlar.Select(f => new { id = f.Id, ad = f.Ad, yigin = f.Yigin, seviye = f.Seviye }),
            stickerPaketleri = OdulKatalogu.StickerPaketleri.Select(p => new
            {
                id = p.Id, ad = p.Ad, seviye = p.Seviye,
                stickerlar = p.Stickerlar.Select(s => new { id = s.Id, ad = s.Ad })
            }),
            seviyeEsikleri = new { formul = "50*(L-1)*L", maksSeviye = PuanServisi.MaksSeviye },
            rutbeler = PuanServisi.Rutbeler.Select(r => new { seviye = r.Seviye, ad = r.Ad })
        });
    }
}
