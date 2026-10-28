// Bir bileşen çökerse tüm sayfa siyah kalmasın: hata yakalanır, yenile / ana sayfa seçenekleri gösterilir.
(function () {
    if (!window.React) return;
    var h = React.createElement;
    var dugme = { padding: '10px 18px', borderRadius: '999px', border: '1px solid #3a2f2f', background: '#1c1616', color: '#ede7e2', font: 'inherit', cursor: 'pointer', textDecoration: 'none' };

    class HataSiniri extends React.Component {
        constructor(p) { super(p); this.state = { hata: null }; }
        static getDerivedStateFromError(hata) { return { hata: hata }; }
        componentDidCatch(hata, bilgi) { try { console.error('[Kage 95] Arayüz hatası:', hata, bilgi && bilgi.componentStack); } catch (e) {} }
        render() {
            if (!this.state.hata) return this.props.children;
            return h('div', { role: 'alert', style: { maxWidth: '560px', margin: '12vh auto', padding: '32px 24px', textAlign: 'center', color: '#ede7e2', background: '#171313', border: '1px solid #2a2323', borderRadius: '16px', fontFamily: 'inherit' } },
                h('h1', { style: { fontSize: '1.25rem', margin: '0 0 8px' } }, 'Bir şeyler ters gitti'),
                h('p', { style: { color: '#a89f99', margin: '0 0 20px' } }, 'Bu sayfa yüklenirken beklenmeyen bir hata oluştu. Yenilemeyi dene; sorun sürerse bize bildir.'),
                h('div', { style: { display: 'flex', gap: '10px', justifyContent: 'center', flexWrap: 'wrap' } },
                    h('button', { type: 'button', style: dugme, onClick: function () { location.reload(); } }, 'Sayfayı yenile'),
                    h('a', { href: '/', style: dugme }, 'Ana sayfaya dön')));
        }
    }
    window.K95HataSiniri = HataSiniri;
})();
