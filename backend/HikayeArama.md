# Hikâyeyle Manga Bul

Özellik varsayılan olarak kapalıdır. Mevcut katalog araması ve SQL Server değiştirilmez.

Yerel veya test ortamında etkinleştirmek için önce `MangaAiIndeks.sql` dosyasını ilgili test veritabanında çalıştırın. Otomatik şema değişikliği yapılmaz. Ardından sunucu ortam değişkenlerini `Evren__Etkin=true` ve `Evren__ApiKey=<EVREN LLM anahtarı>` olarak ayarlayın; anahtarı repoya veya tarayıcıya koymayın. EVREN hesabındaki kullanım şartları önceden kullanıcı tarafından kabul edilmelidir. Etkinleştirmeden önce sağlayıcının ücret ve kota koşullarını kontrol edin.

İndeksleyici yayınlanmış serilerden değişmiş kayıtları beş dakikada en fazla 16 adet olacak şekilde işler; açıklamaların ilk 1800 karakteri kullanılır. Sorgu 500 karakterle, istemci IP başına dakikada 5 istekle sınırlıdır. Sonuç kimlikleri 10 dakika bellekte saklanır ve yanıtta yayın durumu tekrar kontrol edilir. EVREN arızası normal aramayı etkilemez. Sunucu yeniden başlatıldığında bellek önbelleği sıfırlanır.

Bu sürüm yalnızca EVREN'i destekler; diğer sağlayıcıların vektörleri aynı uzayı paylaşmadığından rastgele modeller arasında geçiş yapmak mevcut indeksle güvenilir değildir. Gerçek EVREN erişimi, SQL Server ve tarayıcı testleri başarılı olmadan canlıda etkinleştirmeyin.
