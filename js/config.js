// Tüm sayfaların ortak kullandığı API adresi. Yayına alırken yalnızca buradaki varsayılanı değiştirin.
// Geliştirme kolaylığı: tarayıcıda localStorage 'kage95_api' kaydı varsa o adres kullanılır.
(function () {
    var varsayilan = 'http://localhost:5035/api';
    var adres = varsayilan;
    try { adres = localStorage.getItem('kage95_api') || varsayilan; } catch (e) {}
    window.KAGE95_API_URL = adres;

// Sosyal bağlantılar: boş bırakılan görünmez. Gerçek davet/profil adreslerinizi buraya yazın.
window.KAGE95_SOSYAL = {
    discord: 'https://discord.com/',
    instagram: 'https://www.instagram.com/'
};
})();
