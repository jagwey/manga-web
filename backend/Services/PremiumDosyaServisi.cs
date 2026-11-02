namespace Kage95.Backend.Services;
public sealed class PremiumDosyaServisi(IWebHostEnvironment env, IConfiguration config)
{
    public string? YerelResim(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            if (!Uri.TryCreate(config["Site:Url"], UriKind.Absolute, out var site) || uri.Host != site.Host || uri.Scheme != "https") return null;
            url = uri.AbsolutePath;
        }
        if (!url.StartsWith("/uploads/", StringComparison.Ordinal) || url.Contains('%')) return null;
        var name = url[9..];
        if (name != Path.GetFileName(name) || name.Contains('\\') || name.Contains('/') || name.Contains(':')) return null;
        if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(name), "N", out _)) return null;
        if (Path.GetExtension(name).ToLowerInvariant() is not (".jpg" or ".jpeg" or ".png" or ".webp")) return null;
        var root = Path.GetFullPath(Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "uploads"));
        var path = Path.GetFullPath(Path.Combine(root, name));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(path)) return null;
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 || (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) return null;
        return path;
    }
}
