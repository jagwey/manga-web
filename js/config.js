// Tüm sayfaların ortak kullandığı API adresi. Yayına alırken yalnızca buradaki varsayılanı değiştirin.
// Geliştirme kolaylığı: tarayıcıda localStorage 'kage95_api' kaydı varsa o adres kullanılır.
(function () {
    // Geliştirme (örn. 5500 portu): API 5035 portunda aranır.
    // Canlı yayın (80, 443 veya portsuz alan adı): Aynı alan adı üzerinden /api kullanılır (Reverse Proxy / Nginx uyumlu).
    var port = location.port;
    var varsayilan;
    if (!port || port === '80' || port === '443' || location.protocol === 'https:') {
        varsayilan = (location.origin ? location.origin : ('http://' + (location.hostname || 'localhost'))) + '/api';
    } else {
        var sunucu = (location.hostname && location.protocol.indexOf('http') === 0) ? location.hostname : 'localhost';
        varsayilan = 'http://' + sunucu + ':5035/api';
    }
    var adres = varsayilan;
    try { adres = localStorage.getItem('kage95_api') || varsayilan; } catch (e) {}
    window.KAGE95_API_URL = adres;

// Sosyal bağlantılar: boş bırakılan görünmez. Gerçek davet/profil adreslerinizi buraya yazın.
window.KAGE95_SOSYAL = {
    discord: 'https://discord.gg/yxZZ7m2x8a',
    instagram: 'https://www.instagram.com/kage95.dev/',
    instagramAd: 'kage95.dev',
    eposta: 'noreply@kage95.com'
};
})();
