using System.Net.Http.Headers;
using System.Net.Http.Json;
namespace Kage95.Backend.Services;

public sealed class KageAiIstemcisi(IHttpClientFactory clients, IConfiguration config)
{
    public bool Etkin => config.GetValue<bool>("KageAi:Etkin");
    public sealed record Aday(int Id, string Text);
    private sealed record Yanit(int[] Ids);
    private sealed record BolumYanit(string Yanit);
    public async Task<string> BolumSor(string question, string imageBase64, string mimeType, CancellationToken ct)
    {
        if (!Etkin || !Uri.TryCreate(config["KageAi:Url"], UriKind.Absolute, out var url) || url.Scheme != "https") throw new InvalidOperationException();
        var secret = config["KageAi:Secret"];
        if (string.IsNullOrEmpty(secret) || secret.Length < 32) throw new InvalidOperationException();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(23));
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(url, "/v1/chapter/ask"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        request.Content = JsonContent.Create(new { question, imageBase64, mimeType });
        using var response = await clients.CreateClient("KageAi").SendAsync(request, timeout.Token);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BolumYanit>(timeout.Token);
        if (string.IsNullOrWhiteSpace(result?.Yanit)) throw new InvalidDataException();
        return result.Yanit;
    }
    public async Task<int[]> Sirala(string query, Aday[] candidates, string preferences, CancellationToken ct)
    {
        if (!Uri.TryCreate(config["KageAi:Url"], UriKind.Absolute, out var url) || url.Scheme != "https" || !string.IsNullOrEmpty(url.UserInfo))
            throw new InvalidOperationException("KageAi:Url HTTPS olmalı.");
        var secret = config["KageAi:Secret"];
        if (string.IsNullOrEmpty(secret) || secret.Length < 32) throw new InvalidOperationException("KageAi anahtarı eksik.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(23));
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        request.Content = JsonContent.Create(new { query, candidates, preferences });
        using var response = await clients.CreateClient("KageAi").SendAsync(request, timeout.Token);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<Yanit>(timeout.Token);
        if (result?.Ids == null) throw new InvalidDataException("Geçersiz AI yanıtı.");
        var allowed = candidates.Select(x => x.Id).ToHashSet();
        return result.Ids.Where(allowed.Contains).Distinct().Take(6).ToArray();
    }
}
