using Microsoft.Extensions.Configuration;
using Kage95.Backend.Controllers;
using Kage95.Backend.Data;
using Kage95.Backend.Models;
using Kage95.Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
var opt = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase("test").Options;
using var db = new AppDbContext(opt);
db.Mangalar.AddRange(new Manga { Id = 1, MangaAdi = "Ejderha Dünyası", Aciklama = "Ejderha ile yaşayan kahraman", Yayinda = true }, new Manga { Id = 2, MangaAdi = "Ejderha gizli", Yayinda = false });
await db.SaveChangesAsync();
var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["HikayeArama:Etkin"] = "true" }).Build();
using var cache = new MemoryCache(new MemoryCacheOptions());
var evren = new EvrenIstemcisi(new HttpClientFactoryStub(), cfg);
var controller = new HikayeAramaController(db, evren, cache, cfg, NullLogger<HikayeAramaController>.Instance);
var result = await controller.Ara(new("Ejderha ile yaşayan kahraman"), CancellationToken.None) as OkObjectResult ?? throw new Exception("Arama başarısız");
var json = JsonSerializer.Serialize(result.Value, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
if (!json.Contains("Ejderha Dünyası") || json.Contains("Ejderha gizli") || !json.Contains("kelime")) throw new Exception("Sonuç veya gizlilik hatası: " + json);
if (await controller.Ara(new("kısa"), CancellationToken.None) is not BadRequestObjectResult) throw new Exception("Sorgu doğrulama hatası");
Console.WriteLine("Hikâye araması backend kontrolleri başarılı.");
sealed class HttpClientFactoryStub : IHttpClientFactory { public HttpClient CreateClient(string name) => new(); }

