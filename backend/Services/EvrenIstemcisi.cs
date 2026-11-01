using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Kage95.Backend.Services;

// EVREN LLM geçidi; anahtar yalnızca sunucu yapılandırmasından okunur.
public sealed class EvrenIstemcisi(IHttpClientFactory clients, IConfiguration config)
{
    private const string BaseUrl = "https://evren-llmapi.ssyz.org.tr/v1/";
    public bool Hazir => config.GetValue<bool>("Evren:Etkin") && !string.IsNullOrWhiteSpace(config["Evren:ApiKey"]);

    private async Task<JsonDocument> Gonder(string yol, object govde, CancellationToken ct)
    {
        if (!Hazir) throw new InvalidOperationException("EVREN etkin değil veya anahtar yapılandırılmamış.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var istek = new HttpRequestMessage(HttpMethod.Post, BaseUrl + yol);
        istek.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config["Evren:ApiKey"]);
        istek.Content = JsonContent.Create(govde);
        using var yanit = await clients.CreateClient().SendAsync(istek, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        yanit.EnsureSuccessStatusCode();
        await using var akis = await yanit.Content.ReadAsStreamAsync(timeout.Token);
        return await JsonDocument.ParseAsync(akis, cancellationToken: timeout.Token);
    }

    public async Task<float[][]> Vektorler(string[] metinler, CancellationToken ct)
    {
        using var json = await Gonder("embeddings", new { model = "qwen3-embedding-8b", input = metinler }, ct);
        var data = json.RootElement.GetProperty("data").EnumerateArray().OrderBy(x => x.GetProperty("index").GetInt32()).ToArray();
        if (data.Length != metinler.Length) throw new InvalidDataException("Eksik embedding yanıtı.");
        return data.Select(x => x.GetProperty("embedding").EnumerateArray().Select(v => v.GetSingle()).ToArray()).ToArray();
    }

    public async Task<int[]> Sirala(string sorgu, string[] belgeler, CancellationToken ct)
    {
        using var json = await Gonder("rerank", new { model = "qwen3-reranker-8b", query = sorgu, documents = belgeler, top_n = belgeler.Length }, ct);
        var siralama = json.RootElement.GetProperty("results").EnumerateArray().Select(x => x.GetProperty("index").GetInt32()).ToArray();
        if (siralama.Any(i => i < 0 || i >= belgeler.Length) || siralama.Distinct().Count() != siralama.Length)
            throw new InvalidDataException("Geçersiz rerank yanıtı.");
        return siralama;
    }
}
