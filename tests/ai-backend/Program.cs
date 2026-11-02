using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Kage95.Backend.Controllers;
using Kage95.Backend.Data;
using Kage95.Backend.Models;
using Kage95.Backend.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

int checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
    ["HikayeArama:Etkin"] = "true", ["KageAi:Etkin"] = "true", ["KageAi:Url"] = "https://ai.example.test/v1/manga/rank", ["KageAi:Secret"] = new string('a',40), ["Site:Url"] = "https://kage95.com"
}).Build();
using var cache = new MemoryCache(new MemoryCacheOptions());
var transport = new FakeHttp(); var clients = new Clients(transport);
var ai = new KageAiIstemcisi(clients,cfg); var quota = new AiKullanimKotasi(cache,cfg); var premium = new PremiumServisi(db);
db.Mangalar.AddRange(new Manga { Id=1, MangaAdi="Gizem", Aciklama="Hafızasını kaybeden karakter", Yayinda=true }, new Manga { Id=2, MangaAdi="Taslak", Yayinda=false }, new Manga { Id=3, MangaAdi="Silinmiş", Yayinda=true, SilinmeTarihi=DateTime.UtcNow });
db.Kullanicilar.Add(new Kullanici { Id=5, KullaniciAdi="test" });
db.Favoriler.Add(new Favori { KullaniciId=5, MangaId=1 });
await db.SaveChangesAsync();
var context = new DefaultHttpContext(); context.Connection.RemoteIpAddress = IPAddress.Loopback;
var c = new HikayeAramaController(db,new EvrenIstemcisi(clients,cfg),ai,quota,premium,cache,cfg,NullLogger<HikayeAramaController>.Instance) { ControllerContext = new ControllerContext { HttpContext=context } };
var q = new HikayeAramaController.Sorgu("Geçmişini hatırlamayan gizemli bir karakter");
var result = (OkObjectResult)await c.Ara(q,default);
using(var json = JsonDocument.Parse(JsonSerializer.Serialize(result.Value))) Check(json.RootElement.GetProperty("sonuclar").GetArrayLength()==1,"AI catalogue allowlist excludes invented, unpublished and deleted IDs");
Check(!transport.LastBody.Contains("Taslak") && !transport.LastBody.Contains("Silinmiş"),"Only published catalogue reaches AI");
await c.Ara(q,default); Check(transport.Calls==1,"Repeated query uses cache");
Check(await c.Ara(q with { FavorilerimiKullan=true },default) is UnauthorizedObjectResult,"Anonymous favorites rejected");
context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier,"5") },"test"));
await c.Ara(q with { FavorilerimiKullan=true },default); Check(transport.LastBody.Contains("preferences") && transport.Calls==2,"Own favorites produce separate cache entry");
db.Mangalar.Find(1)!.Yayinda=false; await db.SaveChangesAsync();
result=(OkObjectResult)await c.Ara(q,default);
using(var json=JsonDocument.Parse(JsonSerializer.Serialize(result.Value))) Check(json.RootElement.GetProperty("sonuclar").GetArrayLength()==0,"Unpublished item never returned from cached answer");
Check(!await premium.Etkin(5,default),"No subscription is not premium");
db.PremiumUyelikler.Add(new PremiumUyelik { KullaniciId=5,BitisUtc=DateTime.UtcNow.AddDays(-1) }); await db.SaveChangesAsync();
Check(!await premium.Etkin(5,default),"Expired premium rejected");
db.PremiumUyelikler.Find(5)!.BitisUtc=DateTime.UtcNow.AddDays(1); await db.SaveChangesAsync(); Check(await premium.Etkin(5,default),"Active premium accepted");
Check(quota.Kullan("guest-test",null) && quota.Kullan("guest-test",null) && quota.Kullan("guest-test",null) && !quota.Kullan("guest-test",null),"Guest daily quota enforced");
Check(Enumerable.Range(0,50).All(_ => quota.Kullan("premium-test",5,true)) && !quota.Kullan("premium-test",5,true),"Premium 50 requests enforced");
var temp = Path.Combine(Path.GetTempPath(), "kage-ai-test-"+Guid.NewGuid()); Directory.CreateDirectory(Path.Combine(temp,"uploads"));
try {
    var env=new Env { WebRootPath=temp }; var files=new PremiumDosyaServisi(env,cfg);
    var imageName=new string('a',32)+".png"; await File.WriteAllBytesAsync(Path.Combine(temp,"uploads",imageName),new byte[]{137,80,78,71});
    Check(files.YerelResim("/uploads/"+imageName)!=null,"Local image allowed");
    Check(files.YerelResim("/uploads/../appsettings.json")==null && files.YerelResim("/uploads/%2e%2e/x.png")==null && files.YerelResim("https://evil.test/uploads/"+imageName)==null,"Traversal and external URLs rejected");
    db.Mangalar.Find(1)!.Yayinda=true;
    db.Bolumler.Add(new Bolum { Id=11,MangaId=1 }); db.Sayfalar.Add(new Sayfa { Id=20,BolumId=11,SayfaNo=1,ResimURL="/uploads/"+imageName }); await db.SaveChangesAsync();
    var pc=new PremiumController(db,premium,files,ai,quota) { ControllerContext=new ControllerContext { HttpContext=context } };
    var download=await pc.Indir(11,default); Check(download is FileStreamResult,"Premium download returns archive"); if(download is FileStreamResult fs) await fs.FileStream.DisposeAsync();
    var bulk = await pc.TopluIndir(new PremiumController.TopluIstek(new[] { 11 }), default);
    Check(bulk is FileStreamResult, "Premium bulk download returns archive");
    if (bulk is FileStreamResult bulkFile) await bulkFile.FileStream.DisposeAsync();
    var answer=await pc.Sor(11,new PremiumController.BolumSorusu("Bu sayfada ne oluyor?"),default); Check(answer is OkObjectResult,"Premium page question calls vision API");
    db.PremiumUyelikler.Find(5)!.IptalEdildi=true; await db.SaveChangesAsync();
    Check((await pc.TopluIndir(new PremiumController.TopluIstek(new[] { 11 }), default) as ObjectResult)?.StatusCode == 403, "Cancelled membership cannot download in bulk");
    Check((await pc.Indir(11,default) as ObjectResult)?.StatusCode==403,"Cancelled membership cannot download");
    Check((await pc.Sor(11,new PremiumController.BolumSorusu("Bu sayfada ne oluyor?"),default) as ObjectResult)?.StatusCode==403,"Cancelled membership cannot ask AI");
    Check((pc.SatinAl() as ObjectResult)?.StatusCode==503,"Unconfigured payment cannot activate premium");
} finally { Directory.Delete(temp,true); }
Console.WriteLine($"{checks} backend checks passed (in-memory DB, mocked HTTPS; not live SQL/provider).");

sealed class FakeHttp : HttpMessageHandler {
    public int Calls; public string LastBody="";
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct) {
        Calls++; LastBody=await r.Content!.ReadAsStringAsync(ct);
        if(r.Headers.Authorization?.Scheme!="Bearer") throw new Exception("auth missing");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content=new StringContent(r.RequestUri!.AbsolutePath.Contains("chapter") ? "{\"yanit\":\"Yalnızca bu sayfa.\"}" : "{\"ids\":[999,1,2,3,1]}",System.Text.Encoding.UTF8,"application/json") };
    }
}
sealed class Clients(HttpMessageHandler handler) : IHttpClientFactory { public HttpClient CreateClient(string name) => new(handler,false); }
sealed class Env : IWebHostEnvironment {
 public string WebRootPath {get;set;}=""; public string EnvironmentName {get;set;}="Development"; public string ApplicationName {get;set;}="Tests"; public string ContentRootPath {get;set;}="";
 public IFileProvider WebRootFileProvider {get;set;}=new NullFileProvider(); public IFileProvider ContentRootFileProvider {get;set;}=new NullFileProvider();
}
