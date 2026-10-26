// Temiz adresler: /forum, /manga/12 ... Uygulama içte #/yol ile çalışır; adres çubuğu yol olarak gösterilir.
(function () {
    var Y = window.K95Yol = {};
    function anaMi(p) { return p === '/' || p === '' || p === '/index.html'; }

    // Güncel rota, eski #/yol biçiminde
    Y.hash = function () {
        var h = location.hash || '';
        if (h.indexOf('#/') === 0) return h;
        var p = location.pathname;
        if (anaMi(p)) return h;
        return '#' + p + location.search;
    };

    function temizle() {
        var h = location.hash || '';
        if (h.indexOf('#/') === 0) {
            try { history.replaceState(history.state, '', h === '#/' ? '/' : h.slice(1)); } catch (e) {}
        } else if (location.pathname === '/index.html' && !h) {
            try { history.replaceState(history.state, '', '/' + location.search); } catch (e) {}
        }
    }

    function git(yol) {
        if (!yol || yol.charAt(0) !== '/') yol = '/';
        var simdi = location.pathname + location.search;
        if (yol !== simdi) {
            try { history.pushState(null, '', yol); } catch (e) { location.hash = '#' + yol; return; }
        }
        window.dispatchEvent(new Event('hashchange'));
    }
    Y.git = git;

    document.addEventListener('click', function (e) {
        if (e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
        var a = e.target.closest ? e.target.closest('a[href]') : null;
        if (!a || (a.target && a.target !== '_self')) return;
        var href = a.getAttribute('href');
        if (!href || href.indexOf('#/') !== 0) return;
        e.preventDefault();
        git(href.slice(1));
    }, true);

    window.addEventListener('hashchange', function () { setTimeout(temizle, 0); });
    window.addEventListener('popstate', function () { window.dispatchEvent(new Event('hashchange')); });
    temizle();
})();
