using Microsoft.AspNetCore.Mvc;
using Kage95.Backend.Services;

namespace Kage95.Backend.Controllers
{
    /// <summary>Herkese açık (kimliksiz) site ayarları: yalnızca 6 genel alan.</summary>
    [ApiController]
    [Route("api/ayarlar")]
    public class AyarlarController : ControllerBase
    {
        private readonly ISiteAyarServisi _ayarlar;

        public AyarlarController(ISiteAyarServisi ayarlar)
        {
            _ayarlar = ayarlar;
        }

        [HttpGet]
        public async Task<IActionResult> Getir() => Ok(await AyarYardimcisi.OkuAsync(_ayarlar));
    }
}
