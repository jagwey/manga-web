// Servis çalışanı kaydı (PWA)
if ('serviceWorker' in navigator) {
    window.addEventListener('load', function () {
        navigator.serviceWorker.register('sw.js').then(function (reg) {
            reg.addEventListener('updatefound', function () {
                var newWorker = reg.installing;
                if (newWorker) {
                    newWorker.addEventListener('statechange', function () {
                        if (newWorker.state === 'installed' && navigator.serviceWorker.controller) {
                            if (window.sessionStorage && !sessionStorage.getItem('kage_yenilendi_' + Date.now().toString().slice(0, 6))) {
                                sessionStorage.setItem('kage_yenilendi_' + Date.now().toString().slice(0, 6), '1');
                                window.location.reload();
                            }
                        }
                    });
                }
            });
        }).catch(function () {});
    });
}
