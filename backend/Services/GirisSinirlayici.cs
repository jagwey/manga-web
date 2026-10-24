using System.Collections.Concurrent;

namespace Kage95.Backend.Services
{
    /// <summary>Bellek içi giriş denemesi sınırı: anahtar başına 15 dakikada 5 hatalı denemeden sonra engel.</summary>
    public interface IGirisSinirlayici
    {
        /// <summary>Engelliyse kalan süreyi döner.</summary>
        TimeSpan? Engelli(string anahtar);
        void HataKaydet(string anahtar);
        void Sifirla(string anahtar);
    }

    public class GirisSinirlayici : IGirisSinirlayici
    {
        private const int MaksHata = 5;
        private static readonly TimeSpan Pencere = TimeSpan.FromMinutes(15);
        private readonly ConcurrentDictionary<string, List<DateTime>> _hatalar = new();

        private static string Anahtar(string a) => a.Trim().ToLowerInvariant();

        public TimeSpan? Engelli(string anahtar)
        {
            if (!_hatalar.TryGetValue(Anahtar(anahtar), out var liste)) return null;
            lock (liste)
            {
                var sinir = DateTime.UtcNow - Pencere;
                liste.RemoveAll(t => t < sinir);
                if (liste.Count < MaksHata) return null;
                return liste[0] + Pencere - DateTime.UtcNow;
            }
        }

        public void HataKaydet(string anahtar)
        {
            if (_hatalar.Count > 1000) Temizle();
            var liste = _hatalar.GetOrAdd(Anahtar(anahtar), _ => new List<DateTime>());
            lock (liste) { liste.Add(DateTime.UtcNow); }
        }

        public void Sifirla(string anahtar) => _hatalar.TryRemove(Anahtar(anahtar), out _);

        private void Temizle()
        {
            var sinir = DateTime.UtcNow - Pencere;
            foreach (var kv in _hatalar)
            {
                lock (kv.Value)
                {
                    kv.Value.RemoveAll(t => t < sinir);
                    if (kv.Value.Count == 0) _hatalar.TryRemove(kv.Key, out _);
                }
            }
        }
    }
}
