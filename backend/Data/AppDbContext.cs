using Microsoft.EntityFrameworkCore;
using Kage95.Backend.Models;

namespace Kage95.Backend.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Kullanici> Kullanicilar => Set<Kullanici>();
        public DbSet<Manga> Mangalar => Set<Manga>();
        public DbSet<Kategori> Kategoriler => Set<Kategori>();
        public DbSet<MangaKategori> MangaKategorileri => Set<MangaKategori>();
        public DbSet<Bolum> Bolumler => Set<Bolum>();
        public DbSet<Sayfa> Sayfalar => Set<Sayfa>();
        public DbSet<Yorum> Yorumlar => Set<Yorum>();
        public DbSet<Favori> Favoriler => Set<Favori>();
        public DbSet<OkumaGecmisi> OkumaGecmisi => Set<OkumaGecmisi>();
        public DbSet<DenetimKaydi> DenetimKayitlari => Set<DenetimKaydi>();
        public DbSet<SiteAyari> SiteAyarlari => Set<SiteAyari>();
        public DbSet<MangaYorumu> MangaYorumlari => Set<MangaYorumu>();
        public DbSet<MangaPuani> MangaPuanlari => Set<MangaPuani>();
        public DbSet<BolumPuani> BolumPuanlari => Set<BolumPuani>();
        public DbSet<PuanKaydi> PuanKayitlari => Set<PuanKaydi>();
        public DbSet<YorumBegeni> YorumBegenileri => Set<YorumBegeni>();
        public DbSet<YorumSikayeti> YorumSikayetleri => Set<YorumSikayeti>();
        public DbSet<Duyuru> Duyurular => Set<Duyuru>();
        public DbSet<SohbetMesaji> SohbetMesajlari => Set<SohbetMesaji>();
        public DbSet<SohbetSusturma> SohbetSusturmalari => Set<SohbetSusturma>();
        public DbSet<KullaniciTercihi> KullaniciTercihleri => Set<KullaniciTercihi>();
        public DbSet<Bildirim> Bildirimler => Set<Bildirim>();
        public DbSet<ForumKonu> ForumKonulari => Set<ForumKonu>();
        public DbSet<ForumYanit> ForumYanitlari => Set<ForumYanit>();
        public DbSet<ForumBegeni> ForumBegenileri => Set<ForumBegeni>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Kullanicilar
            modelBuilder.Entity<Kullanici>(entity =>
            {
                entity.ToTable("Kullanicilar");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.KullaniciAdi).HasMaxLength(50).IsRequired();
                entity.HasIndex(e => e.KullaniciAdi).IsUnique();
                entity.Property(e => e.Email).HasMaxLength(100).IsRequired();
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.SifreHash).HasMaxLength(256).IsRequired();
                entity.Property(e => e.Rol).HasMaxLength(20).HasDefaultValue("User");
                entity.Property(e => e.ProfilResmiURL).HasMaxLength(500);
                entity.Property(e => e.Yetkiler).HasMaxLength(1000);
                entity.Property(e => e.GoogleSubject).HasMaxLength(200);
                entity.HasIndex(e => e.GoogleSubject).IsUnique().HasFilter("[GoogleSubject] IS NOT NULL");
                entity.Property(e => e.DiscordUserId).HasMaxLength(100);
                entity.HasIndex(e => e.DiscordUserId).IsUnique().HasFilter("[DiscordUserId] IS NOT NULL");
                entity.Property(e => e.Aktif).HasDefaultValue(true);
                entity.Property(e => e.BanNedeni).HasMaxLength(500);
                entity.Property(e => e.TokenSurumu).HasDefaultValue(0);
                entity.Property(e => e.EmailDogrulandi).HasDefaultValue(false);
                entity.Property(e => e.KayitTarihi).HasDefaultValueSql("SYSDATETIME()");
            });

            // Mangalar
            modelBuilder.Entity<Manga>(entity =>
            {
                entity.ToTable("Mangalar");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.MangaAdi).HasMaxLength(200).IsRequired();
                entity.HasIndex(e => e.MangaAdi).IsUnique();
                entity.Property(e => e.Slug).HasMaxLength(250);
                entity.HasIndex(e => e.Slug);
                entity.Property(e => e.KapakResmiURL).HasMaxLength(500);
                entity.Property(e => e.OneCikanResimURL).HasMaxLength(500);
                entity.Property(e => e.Yazar).HasMaxLength(100);
                entity.Property(e => e.Cizer).HasMaxLength(100);
                entity.Property(e => e.Durum).HasMaxLength(50).HasDefaultValue("DevamEdiyor");
                entity.Property(e => e.EklenmeTarihi).HasDefaultValueSql("SYSDATETIME()");
                entity.Property(e => e.Puan).HasColumnType("decimal(3,1)");
                entity.Property(e => e.GuncellenmeTarihi).HasDefaultValueSql("SYSUTCDATETIME()");
                entity.Property(e => e.Yayinda).HasDefaultValue(true);
                entity.Property(e => e.OneCikan).HasDefaultValue(false);
                entity.HasQueryFilter(e => e.SilinmeTarihi == null);
            });

            // Denetim kayıtları
            modelBuilder.Entity<DenetimKaydi>(entity =>
            {
                entity.ToTable("DenetimKayitlari");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.YapanAdi).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Eylem).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Hedef).HasMaxLength(200).IsRequired();
                entity.Property(e => e.Detay).HasMaxLength(1000);
                entity.Property(e => e.Tarih).HasDefaultValueSql("SYSUTCDATETIME()");
                entity.HasIndex(e => e.Tarih);
            });

            // Site ayarları
            modelBuilder.Entity<SiteAyari>(entity =>
            {
                entity.ToTable("SiteAyarlari");
                entity.HasKey(e => e.Anahtar);
                entity.Property(e => e.Anahtar).HasMaxLength(100);
                entity.Property(e => e.Deger).HasMaxLength(1000).IsRequired();
            });

            // Kategoriler
            modelBuilder.Entity<Kategori>(entity =>
            {
                entity.ToTable("Kategoriler");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.KategoriAdi).HasMaxLength(50).IsRequired();
                entity.HasIndex(e => e.KategoriAdi).IsUnique();
            });

            // MangaKategorileri (Many-to-Many)
            modelBuilder.Entity<MangaKategori>(entity =>
            {
                entity.ToTable("MangaKategorileri");
                entity.HasKey(e => new { e.MangaId, e.KategoriId });

                entity.HasOne(e => e.Manga)
                      .WithMany(m => m.MangaKategorileri)
                      .HasForeignKey(e => e.MangaId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Kategori)
                      .WithMany(k => k.MangaKategorileri)
                      .HasForeignKey(e => e.KategoriId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Bolumler
            modelBuilder.Entity<Bolum>(entity =>
            {
                entity.ToTable("Bolumler");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.BolumNo).HasColumnType("decimal(6,2)").IsRequired();
                entity.Property(e => e.BolumAdi).HasMaxLength(200);
                entity.Property(e => e.YayinTarihi).HasDefaultValueSql("SYSDATETIME()");
                entity.HasIndex(e => new { e.MangaId, e.BolumNo }).IsUnique();
                entity.HasQueryFilter(e => e.Manga.SilinmeTarihi == null);

                entity.HasOne(e => e.Manga)
                      .WithMany(m => m.Bolumler)
                      .HasForeignKey(e => e.MangaId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Sayfalar
            modelBuilder.Entity<Sayfa>(entity =>
            {
                entity.ToTable("Sayfalar");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.SayfaNo).IsRequired();
                entity.Property(e => e.ResimURL).HasMaxLength(1000).IsRequired();
                entity.HasIndex(e => new { e.BolumId, e.SayfaNo }).IsUnique();

                entity.HasOne(e => e.Bolum)
                      .WithMany(b => b.Sayfalar)
                      .HasForeignKey(e => e.BolumId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Yorumlar
            modelBuilder.Entity<Yorum>(entity =>
            {
                entity.ToTable("Yorumlar");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Metin).HasMaxLength(1000).IsRequired();
                entity.Property(e => e.Tarih).HasDefaultValueSql("SYSDATETIME()");
                entity.Property(e => e.BegeniSayisi).HasDefaultValue(0);

                entity.HasOne(e => e.Kullanici)
                      .WithMany(k => k.Yorumlar)
                      .HasForeignKey(e => e.KullaniciId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Bolum)
                      .WithMany(b => b.Yorumlar)
                      .HasForeignKey(e => e.BolumId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Seri yorumları
            modelBuilder.Entity<MangaYorumu>(entity =>
            {
                entity.ToTable("MangaYorumlari");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Metin).HasMaxLength(1000).IsRequired();
                entity.Property(e => e.Tarih).HasDefaultValueSql("SYSUTCDATETIME()");
                entity.HasIndex(e => new { e.MangaId, e.Tarih });
                entity.HasOne(e => e.Manga).WithMany().HasForeignKey(e => e.MangaId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Kullanici).WithMany().HasForeignKey(e => e.KullaniciId).OnDelete(DeleteBehavior.NoAction);
            });

            // Seri puanları
            modelBuilder.Entity<MangaPuani>(entity =>
            {
                entity.ToTable("MangaPuanlari", t => t.HasCheckConstraint("CK_MangaPuanlari_Deger", "[Deger] BETWEEN 1 AND 5"));
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Tarih).HasDefaultValueSql("SYSUTCDATETIME()");
                entity.HasIndex(e => new { e.MangaId, e.KullaniciId }).IsUnique();
                entity.HasOne(e => e.Manga).WithMany().HasForeignKey(e => e.MangaId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Kullanici).WithMany().HasForeignKey(e => e.KullaniciId).OnDelete(DeleteBehavior.NoAction);
            });

            // Bölüm puanları
            modelBuilder.Entity<BolumPuani>(entity =>
            {
                entity.ToTable("BolumPuanlari", t => t.HasCheckConstraint("CK_BolumPuanlari_Deger", "[Deger] BETWEEN 1 AND 5"));
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Tarih).HasDefaultValueSql("SYSUTCDATETIME()");
                entity.HasIndex(e => new { e.BolumId, e.KullaniciId }).IsUnique();
                entity.HasOne(e => e.Bolum).WithMany().HasForeignKey(e => e.BolumId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Kullanici).WithMany().HasForeignKey(e => e.KullaniciId).OnDelete(DeleteBehavior.NoAction);
            });

            // Puan defteri
            modelBuilder.Entity<PuanKaydi>(entity =>
            {
                entity.ToTable("PuanKayitlari");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Eylem).HasMaxLength(40).IsRequired();
                entity.Property(e => e.Referans).HasMaxLength(60);
                entity.Property(e => e.Tarih).HasDefaultValueSql("SYSUTCDATETIME()");
                entity.HasIndex(e => new { e.KullaniciId, e.Tarih }).HasDatabaseName("IX_PuanKayitlari_KullaniciId_Tarih");
                entity.HasIndex(e => e.Eylem).HasDatabaseName("IX_PuanKayitlari_Eylem");
                entity.HasIndex(e => new { e.KullaniciId, e.Eylem, e.Referans }).IsUnique()
                      .HasFilter("[Referans] IS NOT NULL").HasDatabaseName("UX_PuanKayitlari_Kullanici_Eylem_Referans");
                entity.HasOne(e => e.Kullanici).WithMany().HasForeignKey(e => e.KullaniciId).OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<Yorum>(e => { e.Property(x => x.Spoiler).HasDefaultValue(false); e.HasIndex(x => x.YanitId); });
            modelBuilder.Entity<MangaYorumu>(e => { e.Property(x => x.Spoiler).HasDefaultValue(false); e.HasIndex(x => x.YanitId); });

            modelBuilder.Entity<YorumBegeni>(e =>
            {
                e.ToTable("YorumBegenileri");
                e.Property(x => x.Tur).HasMaxLength(10).IsUnicode(false);
                e.Property(x => x.Tarih).HasDefaultValueSql("SYSUTCDATETIME()");
                e.HasIndex(x => new { x.Tur, x.YorumId, x.KullaniciId }).IsUnique();
            });
            modelBuilder.Entity<YorumSikayeti>(e =>
            {
                e.ToTable("YorumSikayetleri");
                e.Property(x => x.Tur).HasMaxLength(10).IsUnicode(false);
                e.Property(x => x.Durum).HasMaxLength(10).IsUnicode(false).HasDefaultValue("acik");
                e.Property(x => x.Neden).HasMaxLength(200).IsRequired();
                e.Property(x => x.Tarih).HasDefaultValueSql("SYSUTCDATETIME()");
                e.HasIndex(x => new { x.Tur, x.YorumId, x.SikayetEdenId }).IsUnique();
            });
            modelBuilder.Entity<Duyuru>(e =>
            {
                e.ToTable("Duyurular");
                e.Property(x => x.Baslik).HasMaxLength(150).IsRequired();
                e.Property(x => x.Etiket).HasMaxLength(10).IsUnicode(false).HasDefaultValue("bilgi");
                e.Property(x => x.Baglanti).HasMaxLength(300);
                e.Property(x => x.Aktif).HasDefaultValue(true);
                e.Property(x => x.Sira).HasDefaultValue(0);
                e.Property(x => x.Tarih).HasDefaultValueSql("SYSUTCDATETIME()");
            });
            modelBuilder.Entity<SohbetMesaji>(e =>
            {
                e.ToTable("SohbetMesajlari");
                e.Property(x => x.Metin).HasMaxLength(300).IsRequired();
                e.Property(x => x.StickerId).HasMaxLength(40).IsUnicode(false);
                e.Property(x => x.Tarih).HasDefaultValueSql("SYSUTCDATETIME()");
                e.Property(x => x.Silindi).HasDefaultValue(false);
                e.HasIndex(x => x.Tarih);
                e.HasOne(x => x.Kullanici).WithMany().HasForeignKey(x => x.KullaniciId).OnDelete(DeleteBehavior.NoAction);
            });
            modelBuilder.Entity<SohbetSusturma>(e =>
            {
                e.ToTable("SohbetSusturmalari");
                e.HasKey(x => x.KullaniciId);
                e.Property(x => x.KullaniciId).ValueGeneratedNever();
                e.Property(x => x.Neden).HasMaxLength(200);
                e.Property(x => x.Tarih).HasDefaultValueSql("SYSUTCDATETIME()");
            });
            modelBuilder.Entity<KullaniciTercihi>(e =>
            {
                e.ToTable("KullaniciTercihleri");
                e.HasKey(x => x.KullaniciId);
                e.Property(x => x.KullaniciId).ValueGeneratedNever();
                e.Property(x => x.AdRengi).HasMaxLength(20);
                e.Property(x => x.MesajFontu).HasMaxLength(20);
                e.Property(x => x.AdEfekti).HasMaxLength(20);
                e.Property(x => x.Unvan).HasMaxLength(24);
            });
            modelBuilder.Entity<Bildirim>(e =>
            {
                e.ToTable("Bildirimler");
                e.HasKey(x => x.Id);
                e.Property(x => x.Tur).HasMaxLength(20).IsUnicode(false).HasDefaultValue("bilgi");
                e.Property(x => x.Baslik).HasMaxLength(250).IsRequired();
                e.Property(x => x.Baglanti).HasMaxLength(300);
                e.Property(x => x.Okundu).HasDefaultValue(false);
                e.Property(x => x.Tarih).HasDefaultValueSql("SYSUTCDATETIME()");
                e.HasIndex(x => new { x.KullaniciId, x.Okundu, x.Tarih });
            });
            modelBuilder.Entity<ForumKonu>(e =>
            {
                e.ToTable("ForumKonulari");
                e.HasKey(x => x.Id);
                e.Property(x => x.Kategori).HasMaxLength(20).IsUnicode(false);
                e.Property(x => x.Baslik).HasMaxLength(120).IsRequired();
                e.Property(x => x.Icerik).HasMaxLength(4000).IsRequired();
                e.HasIndex(x => new { x.Kategori, x.SonYanitTarihi });
                e.Property(x => x.BolumNo).HasColumnType("decimal(6,2)");
                e.HasIndex(x => x.MangaId);
            });
            modelBuilder.Entity<ForumYanit>(e =>
            {
                e.ToTable("ForumYanitlari");
                e.HasKey(x => x.Id);
                e.Property(x => x.Icerik).HasMaxLength(4000).IsRequired();
                e.HasIndex(x => new { x.KonuId, x.Tarih });
            });
            modelBuilder.Entity<ForumBegeni>(e =>
            {
                e.ToTable("ForumBegenileri");
                e.HasKey(x => x.Id);
                e.HasIndex(x => new { x.KonuId, x.KullaniciId }).IsUnique();
            });

            // Favoriler
            modelBuilder.Entity<Favori>(entity =>
            {
                entity.ToTable("Favoriler");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.EklenmeTarihi).HasDefaultValueSql("SYSDATETIME()");
                entity.HasIndex(e => new { e.KullaniciId, e.MangaId }).IsUnique();

                entity.HasOne(e => e.Kullanici)
                      .WithMany(k => k.Favoriler)
                      .HasForeignKey(e => e.KullaniciId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Manga)
                      .WithMany(m => m.Favoriler)
                      .HasForeignKey(e => e.MangaId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // OkumaGecmisi
            modelBuilder.Entity<OkumaGecmisi>(entity =>
            {
                entity.ToTable("OkumaGecmisi");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.SonSayfaNo).HasDefaultValue(1);
                entity.Property(e => e.GuncellenmeTarihi).HasDefaultValueSql("SYSDATETIME()");
                entity.HasIndex(e => new { e.KullaniciId, e.MangaId }).IsUnique();

                entity.HasOne(e => e.Kullanici)
                      .WithMany(k => k.OkumaGecmisleri)
                      .HasForeignKey(e => e.KullaniciId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Manga)
                      .WithMany()
                      .HasForeignKey(e => e.MangaId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.SonOkunanBolum)
                      .WithMany()
                      .HasForeignKey(e => e.SonOkunanBolumId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
