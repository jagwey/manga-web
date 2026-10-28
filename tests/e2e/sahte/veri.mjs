// Sahte API yanıtları. Alan adları backend/Models/*Dtos.cs ile aynıdır (camelCase).
const tarih = '2026-10-01T12:00:00';
const sayfa = (renk, no) =>
    'data:image/svg+xml,' + encodeURIComponent(
        `<svg xmlns="http://www.w3.org/2000/svg" width="800" height="1200"><rect width="100%" height="100%" fill="${renk}"/><text x="50%" y="50%" font-size="96" text-anchor="middle" fill="#fff">${no}</text></svg>`);

export const mangalar = [
    {
        id: 1, mangaAdi: 'Test Ninja', slug: 'test-ninja', aciklama: 'Sahte test mangası.', kapakResmiURL: sayfa('#334', 'K1'),
        yazar: 'Yazar Bir', cizer: 'Çizer Bir', durum: 'DevamEdiyor', eklenmeTarihi: tarih, puan: 8.4, oySayisi: 12,
        okunmaSayisi: '1.2B', kategoriler: ['Aksiyon'], bolumSayisi: 2, yayinda: true, oneCikan: true,
        ilkBolumId: 101, sonBolumId: 102, sonBolumNo: 2, sonBolumTarihi: tarih,
    },
    {
        id: 2, mangaAdi: 'Deneme Kılıcı', slug: 'deneme-kilici', aciklama: 'İkinci sahte manga.', kapakResmiURL: sayfa('#433', 'K2'),
        yazar: 'Yazar İki', cizer: null, durum: 'Tamamlandi', eklenmeTarihi: tarih, puan: null, oySayisi: 0,
        okunmaSayisi: '300', kategoriler: ['Macera'], bolumSayisi: 1, yayinda: true, oneCikan: false,
        ilkBolumId: 201, sonBolumId: 201, sonBolumNo: 1, sonBolumTarihi: tarih,
    },
];

export const mangaDetay = {
    ...mangalar[0],
    oneCikanResimURL: null,
    bolumler: [
        { id: 101, bolumNo: 1, bolumAdi: 'Başlangıç', yayinTarihi: tarih, sayfaSayisi: 3 },
        { id: 102, bolumNo: 2, bolumAdi: 'Devam', yayinTarihi: tarih, sayfaSayisi: 3 },
    ],
};

export const bolumSayfalari = {
    mangaId: 1, mangaAdi: 'Test Ninja', bolumId: 101, bolumNo: 1, bolumAdi: 'Başlangıç',
    sayfalar: [sayfa('#255', '1'), sayfa('#525', '2'), sayfa('#552', '3')],
    oncekiBolumId: null, sonrakiBolumId: 102,
};

export const kategoriler = [
    { id: 1, kategoriAdi: 'Aksiyon', mangaSayisi: 1 },
    { id: 2, kategoriAdi: 'Macera', mangaSayisi: 1 },
];

export const girisYaniti = {
    token: 'sahte.jwt.token', id: 7, kullaniciAdi: 'testci', email: 'testci@example.test', rol: 'Uye',
    profilResmiURL: null, yetkiler: [], emailDogrulandi: true,
};
