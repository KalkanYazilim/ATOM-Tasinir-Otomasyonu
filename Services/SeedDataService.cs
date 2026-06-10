using ATOM.Models.Accounts;
using ATOM.Models.Domain;
using BC = BCrypt.Net.BCrypt;

namespace ATOM.Services;

public class SeedDataHostedService : IHostedService
{
    private readonly IServiceProvider _sp;
    public SeedDataHostedService(IServiceProvider sp) => _sp = sp;

    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = _sp.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IAtomDataService>();

        await SeedKurumlar(svc);
        await SeedKullanicilar(svc);
        await SeedTasinirTanimlar(svc);
        await SeedDepolar(svc);
        await SeedFirmalar(svc);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    private static async Task SeedKurumlar(IAtomDataService svc)
    {
        var mevcut = await svc.KurumlariGetirAsync();
        if (mevcut.Count > 0) return;

        var bakanlik = new Kurum
        {
            Id = "k-bakanlik",
            Ad = "Bakanlık Merkez Teşkilatı",
            Kod = "MRK-001",
            Tur = KurumTur.Bakanlik,
            Il = "Ankara",
            Telefon = "0312 000 0000",
            Email = "merkez@bakanlik.gov.tr"
        };

        var iller = new[]
        {
            ("k-ankara", "Ankara İl Müdürlüğü", "06-ILM", "Ankara"),
            ("k-istanbul", "İstanbul İl Müdürlüğü", "34-ILM", "İstanbul"),
            ("k-izmir", "İzmir İl Müdürlüğü", "35-ILM", "İzmir"),
            ("k-bursa", "Bursa İl Müdürlüğü", "16-ILM", "Bursa"),
            ("k-antalya", "Antalya İl Müdürlüğü", "07-ILM", "Antalya"),
        };

        await svc.KurumKaydetAsync(bakanlik);
        foreach (var (id, ad, kod, il) in iller)
        {
            await svc.KurumKaydetAsync(new Kurum
            {
                Id = id, Ad = ad, Kod = kod,
                Tur = KurumTur.IlMudurlugu, UstKurumId = "k-bakanlik", Il = il
            });
        }
    }

    private static async Task SeedKullanicilar(IAtomDataService svc)
    {
        var mevcut = await svc.KullanicilariGetirAsync();
        if (mevcut.Count > 0) return;

        var kullanicilar = new List<AtomKullanici>
        {
            new() { Id = "u-admin", AdSoyad = "Sistem Yöneticisi", KullaniciAdi = "admin",
                    Email = "admin@atom.gov.tr", Rol = AtomRoller.SistemAdmin, KurumId = "k-bakanlik",
                    SifreHash = BC.HashPassword("Admin123!") },
            new() { Id = "u-merkez", AdSoyad = "Ahmet Yılmaz", KullaniciAdi = "merkez",
                    Email = "ahmet.yilmaz@bakanlik.gov.tr", Rol = AtomRoller.BakanlikMerkez, KurumId = "k-bakanlik",
                    SifreHash = BC.HashPassword("Merkez123!") },
            new() { Id = "u-satinalma", AdSoyad = "Fatma Kaya", KullaniciAdi = "satinalma",
                    Email = "fatma.kaya@bakanlik.gov.tr", Rol = AtomRoller.BakanlikSatinAlma, KurumId = "k-bakanlik",
                    SifreHash = BC.HashPassword("Satin123!") },
            new() { Id = "u-merkezdepo", AdSoyad = "Mehmet Demir", KullaniciAdi = "merkezdepo",
                    Email = "mehmet.demir@bakanlik.gov.tr", Rol = AtomRoller.MerkezDepoSorumlusu, KurumId = "k-bakanlik",
                    SifreHash = BC.HashPassword("Depo123!") },
            new() { Id = "u-ankara-il", AdSoyad = "Ali Çelik", KullaniciAdi = "ankara",
                    Email = "ali.celik@ankara.gov.tr", Rol = AtomRoller.IlMuduru, KurumId = "k-ankara",
                    SifreHash = BC.HashPassword("Ankara123!") },
            new() { Id = "u-istanbul-il", AdSoyad = "Zeynep Şahin", KullaniciAdi = "istanbul",
                    Email = "zeynep.sahin@istanbul.gov.tr", Rol = AtomRoller.IlMuduru, KurumId = "k-istanbul",
                    SifreHash = BC.HashPassword("Istanbul123!") },
            new() { Id = "u-ankara-depo", AdSoyad = "Hasan Öztürk", KullaniciAdi = "ankaradepo",
                    Email = "hasan.ozturk@ankara.gov.tr", Rol = AtomRoller.IlDepoSorumlusu, KurumId = "k-ankara",
                    SifreHash = BC.HashPassword("Depo123!") },
            new() { Id = "u-tedarikci1", AdSoyad = "Tedarikçi Firma Yetkilisi", KullaniciAdi = "tedarikci1",
                    Email = "yetkili@teknoform.com.tr", Rol = AtomRoller.Tedarikci, KurumId = "k-bakanlik", FirmaId = "f-teknoform",
                    SifreHash = BC.HashPassword("Firma123!") },
            new() { Id = "u-personel1", AdSoyad = "Ayşe Arslan", KullaniciAdi = "personel1",
                    Email = "ayse.arslan@ankara.gov.tr", Rol = AtomRoller.Personel, KurumId = "k-ankara",
                    SifreHash = BC.HashPassword("Personel123!") },
        };

        foreach (var k in kullanicilar) await svc.KullaniciKaydetAsync(k);
    }

    private static async Task SeedTasinirTanimlar(IAtomDataService svc)
    {
        var mevcut = await svc.TasinirTanimlariGetirAsync();
        if (mevcut.Count > 0) return;

        var tanimlar = new List<TasinirTanim>
        {
            new() { Id = "tt-001", Kod = "795.05.01.01", Ad = "Lazer Yazıcı", Kategori = TasinirKategori.BilisimDonanim, Birim = "Adet", Aciklama = "A4 lazer yazıcı" },
            new() { Id = "tt-002", Kod = "795.05.01.02", Ad = "Fotokopi Makinesi", Kategori = TasinirKategori.BilisimDonanim, Birim = "Adet" },
            new() { Id = "tt-003", Kod = "795.05.01.03", Ad = "Kart Okuyuculu Çok Fonksiyonlu Baskı Makinesi", Kategori = TasinirKategori.BilisimDonanim, Birim = "Adet", Aciklama = "Kart okuyuculu toplu baskı makinesi" },
            new() { Id = "tt-004", Kod = "795.05.02.01", Ad = "Masaüstü Bilgisayar", Kategori = TasinirKategori.BilisimDonanim, Birim = "Adet" },
            new() { Id = "tt-005", Kod = "795.05.02.02", Ad = "Dizüstü Bilgisayar", Kategori = TasinirKategori.BilisimDonanim, Birim = "Adet" },
            new() { Id = "tt-006", Kod = "795.05.02.03", Ad = "Tablet Bilgisayar", Kategori = TasinirKategori.BilisimDonanim, Birim = "Adet" },
            new() { Id = "tt-007", Kod = "795.05.03.01", Ad = "Monitör (24 inç)", Kategori = TasinirKategori.BilisimDonanim, Birim = "Adet" },
            new() { Id = "tt-008", Kod = "795.05.04.01", Ad = "UPS (1000VA)", Kategori = TasinirKategori.BilisimDonanim, Birim = "Adet" },
            new() { Id = "tt-009", Kod = "795.05.05.01", Ad = "Ağ Anahtarı (24 Port)", Kategori = TasinirKategori.BilisimDonanim, Birim = "Adet" },
            new() { Id = "tt-010", Kod = "795.03.01.01", Ad = "Masa (Çalışma Masası)", Kategori = TasinirKategori.MobilYa, Birim = "Adet" },
            new() { Id = "tt-011", Kod = "795.03.01.02", Ad = "Ofis Koltuğu", Kategori = TasinirKategori.MobilYa, Birim = "Adet" },
            new() { Id = "tt-012", Kod = "795.03.01.03", Ad = "Dosya Dolabı (Çelik)", Kategori = TasinirKategori.MobilYa, Birim = "Adet" },
            new() { Id = "tt-013", Kod = "795.02.01.01", Ad = "A4 Fotokopi Kağıdı (500 Yaprak)", Kategori = TasinirKategori.Kirtasiye, Birim = "Paket" },
            new() { Id = "tt-014", Kod = "795.02.01.02", Ad = "Toner (Lazer Yazıcı)", Kategori = TasinirKategori.Kirtasiye, Birim = "Adet" },
            new() { Id = "tt-015", Kod = "795.06.01.01", Ad = "IP Kamera (Güvenlik)", Kategori = TasinirKategori.GuvenlikSistemi, Birim = "Adet" },
            new() { Id = "tt-016", Kod = "795.06.01.02", Ad = "Geçiş Kontrol Terminali", Kategori = TasinirKategori.GuvenlikSistemi, Birim = "Adet" },
            new() { Id = "tt-017", Kod = "795.04.01.01", Ad = "Klima (Duvar Tipi 12.000 BTU)", Kategori = TasinirKategori.ElektrikliEv, Birim = "Adet" },
            new() { Id = "tt-018", Kod = "795.04.01.02", Ad = "Buzdolabı (Büro Tipi)", Kategori = TasinirKategori.ElektrikliEv, Birim = "Adet" },
        };

        foreach (var t in tanimlar) await svc.TasinirTanimKaydetAsync(t);
    }

    private static async Task SeedDepolar(IAtomDataService svc)
    {
        var mevcut = await svc.DepolariGetirAsync();
        if (mevcut.Count > 0) return;

        var depolar = new List<Depo>
        {
            new() { Id = "d-merkez", Ad = "Bakanlık Merkez Deposu", Kod = "DEP-MRK-001",
                    KurumId = "k-bakanlik", MerkezDepoMu = true, SorumluId = "u-merkezdepo",
                    Adres = "Ankara Merkez", KapasiteM2 = 2000,
                    Stoklar = new List<DepoStok>
                    {
                        new() { TasinirTanimId = "tt-001", Miktar = 150, MinEsik = 20, BirimMaliyet = 3500 },
                        new() { TasinirTanimId = "tt-002", Miktar = 45, MinEsik = 10, BirimMaliyet = 15000 },
                        new() { TasinirTanimId = "tt-003", Miktar = 30, MinEsik = 5, BirimMaliyet = 85000 },
                        new() { TasinirTanimId = "tt-004", Miktar = 200, MinEsik = 30, BirimMaliyet = 8500 },
                        new() { TasinirTanimId = "tt-005", Miktar = 80, MinEsik = 15, BirimMaliyet = 12000 },
                        new() { TasinirTanimId = "tt-013", Miktar = 500, MinEsik = 100, BirimMaliyet = 85 },
                        new() { TasinirTanimId = "tt-014", Miktar = 300, MinEsik = 50, BirimMaliyet = 450 },
                    }
            },
            new() { Id = "d-ankara", Ad = "Ankara İl Deposu", Kod = "DEP-06-001",
                    KurumId = "k-ankara", MerkezDepoMu = false, SorumluId = "u-ankara-depo",
                    Adres = "Ankara İl Müdürlüğü", KapasiteM2 = 400,
                    Stoklar = new List<DepoStok>
                    {
                        new() { TasinirTanimId = "tt-001", Miktar = 25, MinEsik = 5, BirimMaliyet = 3500 },
                        new() { TasinirTanimId = "tt-004", Miktar = 35, MinEsik = 10, BirimMaliyet = 8500 },
                        new() { TasinirTanimId = "tt-013", Miktar = 120, MinEsik = 30, BirimMaliyet = 85 },
                    }
            },
            new() { Id = "d-istanbul", Ad = "İstanbul İl Deposu", Kod = "DEP-34-001",
                    KurumId = "k-istanbul", MerkezDepoMu = false, SorumluId = "u-istanbul-il",
                    Adres = "İstanbul İl Müdürlüğü", KapasiteM2 = 600,
                    Stoklar = new List<DepoStok>
                    {
                        new() { TasinirTanimId = "tt-001", Miktar = 40, MinEsik = 10, BirimMaliyet = 3500 },
                        new() { TasinirTanimId = "tt-005", Miktar = 15, MinEsik = 5, BirimMaliyet = 12000 },
                    }
            },
        };

        foreach (var d in depolar) await svc.DepoKaydetAsync(d);
    }

    private static async Task SeedFirmalar(IAtomDataService svc)
    {
        var mevcut = await svc.FirmalariGetirAsync();
        if (mevcut.Count > 0) return;

        var firmalar = new List<Firma>
        {
            new() { Id = "f-teknoform", Ad = "TeknoForm Bilişim A.Ş.", VergiNo = "1234567890",
                    VergiDairesi = "Ankara Kurumlar", Telefon = "0312 111 1111",
                    Email = "info@teknoform.com.tr", YetkiliKisi = "Tedarikçi Firma Yetkilisi",
                    PuanOrtalama = 4.5, TamamlananIhale = 12, KayitliKullaniciIds = new() { "u-tedarikci1" } },
            new() { Id = "f-ofistech", Ad = "OfisTech Yazıcı Sistemleri Ltd.", VergiNo = "9876543210",
                    VergiDairesi = "İstanbul Kurumlar", Telefon = "0212 222 2222",
                    Email = "info@ofistech.com.tr", YetkiliKisi = "İsmail Koç", PuanOrtalama = 4.2, TamamlananIhale = 7 },
            new() { Id = "f-guventek", Ad = "GüvenTek Güvenlik Sistemleri A.Ş.", VergiNo = "5555555555",
                    VergiDairesi = "Ankara Büyük Mükellefler", Telefon = "0312 333 3333",
                    Email = "info@guventek.com.tr", YetkiliKisi = "Serkan Yıldız", PuanOrtalama = 4.8, TamamlananIhale = 20 },
        };

        foreach (var f in firmalar) await svc.FirmaKaydetAsync(f);
    }
}
