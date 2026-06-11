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
        await SeedUlusalAileSosyalHizmetKurumVeDepolari(svc);
        await SeedGercekciUlkeGeneliCanliVeri(svc);
        await SeedFirmalar(svc);
        await SeedTasinirKayitlar(svc);
        await SeedIslemler(svc);
    }

    // ═══ OPERASYONEL ÖRNEK VERİLER ═══════════════════════════
    private static async Task SeedIslemler(IAtomDataService svc)
    {
        if ((await svc.TalepleriGetirAsync()).Count > 0) return;
        var now = DateTime.UtcNow;

        // ── Talepler (farklı durumlarda) ──
        var talepler = new List<IhtiyacTalebi>
        {
            new() { Id="t-1", TalepNo="T-2026-000001", TalepciKurumId="k-ankara", TalepciKullaniciId="u-ankara-il",
                TalepTarihi=now.AddDays(-25), GerekceAciklama="Yeni personel için bilgisayar ihtiyacı", OncelikSeviyesi="Yüksek",
                Durum=TalepDurumu.GonderildiIlOnay, Kalemler=new(){ new(){ TasinirTanimId="tt-004", TalepMiktari=10 }, new(){ TasinirTanimId="tt-007", TalepMiktari=10 } } },
            new() { Id="t-2", TalepNo="T-2026-000002", TalepciKurumId="k-ankara", TalepciKullaniciId="u-ankara-il",
                TalepTarihi=now.AddDays(-20), GerekceAciklama="Yazıcı toner stoğu tükendi", OncelikSeviyesi="Acil",
                Durum=TalepDurumu.IlOnaylandi, Kalemler=new(){ new(){ TasinirTanimId="tt-014", TalepMiktari=40 } } },
            new() { Id="t-3", TalepNo="T-2026-000003", TalepciKurumId="k-istanbul", TalepciKullaniciId="u-istanbul-il",
                TalepTarihi=now.AddDays(-15), GerekceAciklama="Kart okuyuculu baskı makinesi ihtiyacı (il geneli)", OncelikSeviyesi="Yüksek",
                Durum=TalepDurumu.BakanlikInceliyor, BakanlikAlinmaTarihi=now.AddDays(-12),
                Kalemler=new(){ new(){ TasinirTanimId="tt-003", TalepMiktari=15 } } },
            new() { Id="t-4", TalepNo="T-2026-000004", TalepciKurumId="k-izmir", TalepciKullaniciId="u-izmir-il",
                TalepTarihi=now.AddDays(-10), GerekceAciklama="Klima ihtiyacı", OncelikSeviyesi="Normal",
                Durum=TalepDurumu.KarsilandiTamamen, KapanmaTarihi=now.AddDays(-3), KarsilamaTuru="Sevk",
                Kalemler=new(){ new(){ TasinirTanimId="tt-017", TalepMiktari=5, KarsilananMiktar=5 } } },
            new() { Id="t-5", TalepNo="T-2026-000005", TalepciKurumId="k-bursa", TalepciKullaniciId="u-bursa-il",
                TalepTarihi=now.AddDays(-5), GerekceAciklama="Ofis mobilyası talebi", OncelikSeviyesi="Düşük",
                Durum=TalepDurumu.Taslak, Kalemler=new(){ new(){ TasinirTanimId="tt-010", TalepMiktari=20 }, new(){ TasinirTanimId="tt-011", TalepMiktari=20 } } },
            new() { Id="t-6", TalepNo="T-2026-000006", TalepciKurumId="k-antalya", TalepciKullaniciId="u-antalya-il",
                TalepTarihi=now.AddDays(-30), GerekceAciklama="Güvenlik kamerası talebi", OncelikSeviyesi="Yüksek",
                Durum=TalepDurumu.Reddedildi, RedGerekce="Bütçe yetersiz, gelecek döneme ertelendi.",
                Kalemler=new(){ new(){ TasinirTanimId="tt-015", TalepMiktari=12 } } },
        };
        foreach (var t in talepler)
        {
            t.OnayGecmisi.Add(new OnayKaydi { KullaniciAdi="Sistem", Rol="Sistem", Karar=OnayDurumu.Onaylandi, Asama="Oluşturma", Tarih=t.TalepTarihi });
            await svc.TalepKaydetAsync(t);
        }

        // ── İhaleler ──
        var ihaleler = new List<Ihale>
        {
            new() { Id="ih-1", IhaleNo="IH-2026-000001", Baslik="Kart Okuyuculu Baskı Makinesi Alımı", Aciklama="Ülke geneli illere dağıtım için",
                Durum=IhaleDurumu.TeklifAliniyor, OlusturmaTarihi=now.AddDays(-18), IlanTarihi=now.AddDays(-15), TeklifSonTarihi=now.AddDays(10),
                Yontem="Açık İhale", YaklasikMaliyet=1275000, TahminiButce=1300000, OlusturanKullaniciId="u-satinalma",
                Kalemler=new(){ new(){ TasinirTanimId="tt-003", Miktar=15, TeknikOzellik="A3/A4, kart okuyuculu, 40+ ppm" } },
                KaynaklananTalepIds=new(){"t-3"},
                Teklifler=new(){
                    new(){ FirmaId="f-ofistech", ToplamTutar=1245000, Durum=TeklifDurumu.Gonderildi, VerilmeTarihi=now.AddDays(-8),
                        Kalemler=new(){ new(){ TasinirTanimId="tt-003", Miktar=15, BirimFiyat=83000, Marka="Canon", Model="iR-ADV C3826" } } },
                    new(){ FirmaId="f-teknoform", ToplamTutar=1290000, Durum=TeklifDurumu.Gonderildi, VerilmeTarihi=now.AddDays(-6),
                        Kalemler=new(){ new(){ TasinirTanimId="tt-003", Miktar=15, BirimFiyat=86000, Marka="Xerox", Model="VersaLink C7000" } } },
                } },
            new() { Id="ih-2", IhaleNo="IH-2026-000002", Baslik="Masaüstü Bilgisayar Alımı", Aciklama="50 adet masaüstü bilgisayar",
                Durum=IhaleDurumu.Sonuclandi, OlusturmaTarihi=now.AddDays(-40), IlanTarihi=now.AddDays(-37), TeklifSonTarihi=now.AddDays(-25),
                SonuclanmaTarihi=now.AddDays(-20), Yontem="Açık İhale", YaklasikMaliyet=425000, TahminiButce=450000, OlusturanKullaniciId="u-satinalma",
                KazananFirmaId="f-teknoform",
                Kalemler=new(){ new(){ TasinirTanimId="tt-004", Miktar=50 } },
                Teklifler=new(){ new(){ Id="tk-2a", FirmaId="f-teknoform", ToplamTutar=420000, Durum=TeklifDurumu.Kabul, VerilmeTarihi=now.AddDays(-28),
                    Kalemler=new(){ new(){ TasinirTanimId="tt-004", Miktar=50, BirimFiyat=8400, Marka="Dell", Model="OptiPlex 7010" } } } } },
            new() { Id="ih-3", IhaleNo="IH-2026-000003", Baslik="Klima Alımı (Doğrudan Temin)", Aciklama="Acil klima ihtiyacı",
                Durum=IhaleDurumu.Hazirlaniyor, OlusturmaTarihi=now.AddDays(-3), Yontem="Doğrudan Temin", YaklasikMaliyet=140000, TahminiButce=150000,
                OlusturanKullaniciId="u-satinalma", Kalemler=new(){ new(){ TasinirTanimId="tt-017", Miktar=10 } } },
        };
        ihaleler[1].Teklifler[0].Id = "tk-2a"; ihaleler[1].KazananTeklifId = "tk-2a";
        foreach (var i in ihaleler) await svc.IhaleKaydetAsync(i);

        // ── Mal Kabuller ──
        await svc.MalKabulKaydetAsync(new MalKabul { Id="mk-1", MalKabulNo="MK-2026-000001", IhaleId="ih-2", FirmaId="f-teknoform",
            DepoId="d-merkez", TeslimTarihi=now.AddDays(-18), KabulEdenKullaniciId="u-merkezdepo", Durum=OnayDurumu.Onaylandi,
            IrsaliyeNo="IRS-2026-5521", FaturaNo="FTR-2026-8842", FaturaTutari=420000, TifNo="TIF-2026-000010", TasinirKayitUretildiMi=true,
            Kalemler=new(){ new(){ TasinirTanimId="tt-004", SiparisEdilen=50, TeslimEdilen=50, KabulEdilen=50, BirimFiyat=8400, Marka="Dell", Model="OptiPlex 7010", DemirbasMi=true } } });
        await svc.MalKabulKaydetAsync(new MalKabul { Id="mk-2", MalKabulNo="MK-2026-000002", FirmaId="f-guventek",
            DepoId="d-merkez", TeslimTarihi=now.AddDays(-2), KabulEdenKullaniciId="u-merkezdepo", Durum=OnayDurumu.Bekliyor,
            IrsaliyeNo="IRS-2026-5599", FaturaNo="FTR-2026-9001", FaturaTutari=96000,
            Kalemler=new(){ new(){ TasinirTanimId="tt-015", SiparisEdilen=12, TeslimEdilen=12, KabulEdilen=10, Reddedilen=2, RedGerekce="2 adet hasarlı", BirimFiyat=8000, Marka="Hikvision", Model="DS-2CD2", DemirbasMi=true } } });

        // ── Sevkler ──
        await svc.SevkKaydetAsync(new Sevk { Id="sv-1", SevkNo="SVK-2026-000001", KaynakDepoId="d-merkez", HedefDepoId="d-ankara", HedefKurumId="k-ankara",
            SevkTarihi=now.AddDays(-12), Durum=SevkDurumu.TeslimEdildi, GercekVarisTarihi=now.AddDays(-10), OlusturanKullaniciId="u-merkezdepo",
            TasimaciAdi="MNG Kargo", AracPlaka="06 ABC 123", IrsaliyeNo="SVK-IRS-001",
            Kalemler=new(){ new(){ TasinirTanimId="tt-004", Miktar=15, TeslimAlinan=15 } } });
        await svc.SevkKaydetAsync(new Sevk { Id="sv-2", SevkNo="SVK-2026-000002", KaynakDepoId="d-merkez", HedefDepoId="d-istanbul", HedefKurumId="k-istanbul",
            SevkTarihi=now.AddDays(-2), Durum=SevkDurumu.Hazirlaniyor, OlusturanKullaniciId="u-merkezdepo",
            TasimaciAdi="Aras Kargo", AracPlaka="34 XYZ 789",
            Kalemler=new(){ new(){ TasinirTanimId="tt-001", Miktar=10 } } });

        // ── Zimmetler (tekil taşınır kayıtlarından) ──
        var kayitlar = await svc.TasinirKayitlariGetirAsync();
        var ankaraAmbar = kayitlar.Where(k => k.DepoId == "d-ankara" && k.Durum == TasinirKayitDurumu.Ambarda).Take(3).ToList();
        if (ankaraAmbar.Count > 0)
        {
            var zimmet = new Zimmet { Id="zm-1", ZimmetNo="ZMT-2026-000001", DepoId="d-ankara", PersonelId="u-personel1",
                VerenKullaniciId="u-ankara-depo", ZimmetTarihi=now.AddDays(-8), Durum=ZimmetDurumu.Aktif, TeslimAlanImzaDurumu="İmzalandı",
                Kalemler=ankaraAmbar.Select(k => new ZimmetKalemi { TasinirKayitId=k.Id, TasinirTanimId=k.TasinirTanimId??"", Miktar=1,
                    SeriNo=k.SeriNo, Barkod=k.BarKod, SicilNo=k.SicilNo, Marka=k.MarkaAdi, Model=k.Modeli }).ToList() };
            zimmet.OnayGecmisi.Add(new OnayKaydi { KullaniciAdi="Hasan Öztürk", Rol=AtomRoller.IlDepoSorumlusu, Karar=OnayDurumu.Onaylandi, Asama="Zimmet Oluşturma", Tarih=now.AddDays(-8) });
            await svc.ZimmetKaydetAsync(zimmet);
            foreach (var k in ankaraAmbar) { k.Durum = TasinirKayitDurumu.Zimmetli; k.ZimmetId = "zm-1"; await svc.TasinirKayitKaydetAsync(k); }
        }

        // ── Bakım/Arıza ──
        await svc.BakimKaydiKaydetAsync(new BakimKaydi { Id="bk-1", BakimNo="BKM-2026-000001", PersonelId="u-personel1",
            TasinirTanimId="tt-004", SeriNo="SN-DE123456", ArizaBildirmeTarihi=now.AddDays(-6), ArizaAciklama="Açılmıyor, güç sorunu", Durum=BakimDurumu.Acik });
        await svc.BakimKaydiKaydetAsync(new BakimKaydi { Id="bk-2", BakimNo="BKM-2026-000002", PersonelId="u-personel1",
            TasinirTanimId="tt-001", SeriNo="SN-HP998877", ArizaBildirmeTarihi=now.AddDays(-20), ArizaAciklama="Kağıt sıkışması sürekli", Durum=BakimDurumu.Tamamlandi,
            AtananTeknikId="u-teknisyen", TamamlanmaTarihi=now.AddDays(-15), YapilanIslem="Merdane değişimi", BakimMaliyeti=850, GarantiKapsaminaMi=false });

        // ── Hurda ──
        await svc.HurdaKaydiKaydetAsync(new HurdaKaydi { Id="hr-1", HurdaNo="HRD-2026-000001", KurumId="k-ankara", TalepEdenId="u-ankara-depo",
            TalepTarihi=now.AddDays(-10), Durum=HurdaDurumu.Komisyon, DusumTuru="Hurda", DepoId="d-ankara", KomisyonTarihi=now.AddDays(-5),
            KomisyonKarari="Ekonomik ömrünü doldurmuş, hurdaya ayrılması uygundur.", Gerekce="10 yıllık, tamiri ekonomik değil",
            Kalemler=new(){ new(){ TasinirTanimId="tt-001", Miktar=3, DurumAciklama="Onarılamaz" } } });
        await svc.HurdaKaydiKaydetAsync(new HurdaKaydi { Id="hr-2", HurdaNo="HRD-2026-000002", KurumId="k-istanbul", TalepEdenId="u-istanbul-il",
            TalepTarihi=now.AddDays(-3), Durum=HurdaDurumu.Talep, DusumTuru="Kayıp", Gerekce="Sayımda eksik çıktı, kayıp tutanağı düzenlendi",
            Kalemler=new(){ new(){ TasinirTanimId="tt-006", Miktar=1, DurumAciklama="Kayıp" } } });

        // ── Sayım ──
        var sayim = new SayimKaydi { Id="sy-1", SayimNo="SYM-2026-000001", KurumId="k-ankara", DepoId="d-ankara", Baslik="2026 Yıl Sonu Sayımı",
            BaslangicTarihi=now.AddDays(-4), Durum=SayimDurumu.OnayBekliyor, OlusturanKullaniciId="u-ankara-depo",
            Kalemler=new(){
                new(){ TasinirTanimId="tt-001", KaydiMiktar=25, FiiliMiktar=24 },
                new(){ TasinirTanimId="tt-004", KaydiMiktar=35, FiiliMiktar=35 },
                new(){ TasinirTanimId="tt-013", KaydiMiktar=120, FiiliMiktar=125 },
            } };
        await svc.SayimKaydetAsync(sayim);

        // ── Devir ──
        await svc.DevirKaydetAsync(new DevirKaydi { Id="dv-1", DevirNo="DVR-2026-000001", Tur=DevirTuru.KurumlarArasi,
            KaynakKurumId="k-ankara", KaynakDepoId="d-ankara", HedefKurumId="k-istanbul", HedefDepoId="d-istanbul",
            DevirTarihi=now.AddDays(-2), Durum=DevirDurumu.AlanOnayiBekliyor, OlusturanKullaniciId="u-ankara-depo",
            Gerekce="İhtiyaç fazlası taşınırın bedelsiz devri (TMY Genel Tebliği Sayı:1)",
            Kalemler=new(){ new(){ TasinirTanimId="tt-013", Miktar=20, BirimMaliyet=85 } } });

        // ── Bildirimler ──
        await svc.BildirimKaydetAsync(new Bildirim { AliciKullaniciId="u-merkez", Baslik="Bakanlık İncelemesi Bekleyen Talep",
            Mesaj="T-2026-000003 numaralı talep bakanlık incelemesinde.", Tur=BildirimTur.Bilgi, Tarih=now.AddDays(-12), LinkUrl="/talep", Oncelik="Yüksek" });
        await svc.BildirimKaydetAsync(new Bildirim { AliciKullaniciId="u-merkezdepo", Baslik="Onay Bekleyen Mal Kabul",
            Mesaj="MK-2026-000002 mal kabulü onayınızı bekliyor.", Tur=BildirimTur.Uyari, Tarih=now.AddDays(-2), LinkUrl="/depo/Home/MalKabuller", Oncelik="Yüksek" });
        await svc.BildirimKaydetAsync(new Bildirim { AliciKullaniciId="u-personel1", Baslik="Yeni Zimmet",
            Mesaj="ZMT-2026-000001 numaralı zimmet üzerinize tanımlandı.", Tur=BildirimTur.Bilgi, Tarih=now.AddDays(-8), LinkUrl="/zimmet", OkunduMu=true });

        // ── Audit örnekleri ──
        await svc.AuditKaydetAsync(new AuditLog { KullaniciAdi="Mehmet Demir", Rol=AtomRoller.MerkezDepoSorumlusu, Modul="MalKabul",
            Islem="Onay", KayitTur="MalKabul", KayitId="mk-1", Aciklama="MK-2026-000001 onaylandı, 50 demirbaş kaydı üretildi", Tarih=now.AddDays(-18), Ip="10.0.0.21" });
        await svc.AuditKaydetAsync(new AuditLog { KullaniciAdi="Mehmet Demir", Rol=AtomRoller.MerkezDepoSorumlusu, Modul="Sevk",
            Islem="Oluşturma", KayitTur="Sevk", KayitId="sv-1", Aciklama="SVK-2026-000001 sevk oluşturuldu", Tarih=now.AddDays(-12), Ip="10.0.0.21" });

        // ── Taşıtlar (237 sayılı Taşıt Kanunu) ──
        await svc.TasitKaydetAsync(new Tasit { Id="ta-1", Plaka="06 ATOM 06", KurumId="k-ankara", Marka="Ford", Model="Focus",
            ModelYili=2022, SasiNo="NM0AXXTTFAB12345", MotorNo="M9DA-998877", Renk="Beyaz", Yakit=YakitTuru.Dizel, Sinif="Binek",
            Durum=TasitDurumu.Aktif, EdinimBedeli=850000, EdinimTarihi=now.AddYears(-2), GuncelKm=48500,
            TahsisEdilenKullaniciId="u-ankara-il", TahsisBirim="İl Müdürlüğü Makam", TahsisTarihi=now.AddYears(-2),
            MuayeneBitisTarihi=now.AddDays(20), SigortaBitisTarihi=now.AddDays(45), KaskoBitisTarihi=now.AddDays(120),
            YakitKayitlari=new(){ new(){ Tarih=now.AddDays(-10), Litre=42, Tutar=1680, Km=48000, Istasyon="Petrol Ofisi", KullaniciAdi="Ali Çelik" } },
            BakimKayitlari=new(){ new(){ Tarih=now.AddDays(-60), IslemTuru="Periyodik", Aciklama="40.000 km bakımı", Tutar=3200, Km=40000, Servis="Yetkili Servis" } } });
        await svc.TasitKaydetAsync(new Tasit { Id="ta-2", Plaka="34 ATOM 34", KurumId="k-istanbul", Marka="Fiat", Model="Doblo",
            ModelYili=2021, SasiNo="ZFA26300012345", MotorNo="199A-445566", Renk="Gri", Yakit=YakitTuru.Dizel, Sinif="Kamyonet",
            Durum=TasitDurumu.Aktif, EdinimBedeli=650000, EdinimTarihi=now.AddYears(-3), GuncelKm=92000,
            MuayeneBitisTarihi=now.AddDays(200), SigortaBitisTarihi=now.AddDays(15),
            KazaKayitlari=new(){ new(){ Tarih=now.AddDays(-90), Yer="İstanbul E-5", Aciklama="Hafif maddi hasarlı", KusurVar=false, HasarBedeli=12000, SurucuAdi="Zeynep Şahin" } } });
        await svc.TasitKaydetAsync(new Tasit { Id="ta-3", Plaka="06 ATOM 07", KurumId="k-ankara", Marka="Renault", Model="Master",
            ModelYili=2019, SasiNo="VF1MA000012399", MotorNo="M9T-112233", Renk="Beyaz", Yakit=YakitTuru.Dizel, Sinif="Minibüs",
            Durum=TasitDurumu.Bakimda, EdinimBedeli=720000, EdinimTarihi=now.AddYears(-5), GuncelKm=185000,
            MuayeneBitisTarihi=now.AddDays(-5), SigortaBitisTarihi=now.AddDays(90) });
    }

    private static async Task SeedGercekciUlkeGeneliCanliVeri(IAtomDataService svc)
    {
        var kurumlar = await svc.KurumlariGetirAsync();
        var depolar = await svc.DepolariGetirAsync();
        var tanimlar = await svc.TasinirTanimlariGetirAsync();
        var kullanicilar = await svc.KullanicilariGetirAsync();
        var stokHareketler = await svc.StokHareketleriGetirAsync();
        var personelSarfBakiyeler = await svc.PersonelSarfBakiyeleriGetirAsync();
        var tasinirKayitlar = await svc.TasinirKayitlariGetirAsync();
        var zimmetler = await svc.ZimmetleriGetirAsync();
        var rnd = new Random(20260611);
        var soyadlar = new[] { "Yılmaz", "Kaya", "Demir", "Şahin", "Çelik", "Yıldız", "Arslan", "Koç", "Aydın", "Öztürk" };
        var adlar = new[] { "Ayşe", "Fatma", "Zeynep", "Elif", "Merve", "Mehmet", "Mustafa", "Ali", "Emre", "Hasan" };
        var sarfTanimlar = tanimlar.Where(t => !t.DemirbasMi).ToList();
        var demirbasTanimlar = tanimlar.Where(t => t.DemirbasMi).ToList();

        foreach (var kurum in kurumlar.Where(k => k.Tur == KurumTur.IlMudurlugu && k.AktifMi))
        {
            var slug = ToAsciiSlug(kurum.Il);
            for (var i = 1; i <= 3; i++)
            {
                var id = $"u-{slug}-personel-{i:00}";
                if (kullanicilar.Any(k => k.Id == id)) continue;
                var adSoyad = $"{adlar[(i + kurum.Il.Length) % adlar.Length]} {soyadlar[(i * 3 + kurum.Il.Length) % soyadlar.Length]}";
                var kullanici = new AtomKullanici
                {
                    Id = id,
                    AdSoyad = adSoyad,
                    KullaniciAdi = $"{slug}.personel{i}",
                    Email = $"{slug}.personel{i}@aile.gov.tr",
                    Rol = AtomRoller.Personel,
                    KurumId = kurum.Id,
                    Telefon = $"0 5{rnd.Next(30, 59)} {rnd.Next(100, 999)} {rnd.Next(10, 99)} {rnd.Next(10, 99)}",
                    SifreHash = BC.HashPassword("Personel123!")
                };
                await svc.KullaniciKaydetAsync(kullanici);
                kullanicilar.Add(kullanici);
            }
        }

        foreach (var depo in depolar.Where(d => d.AktifMi))
        {
            if (depo.Stoklar.Count > 0) continue;
            var isMerkez = depo.MerkezDepoMu || depo.KurumId == "k-bakanlik";
            var isAna = depo.Kod.EndsWith("-ANA", StringComparison.OrdinalIgnoreCase) || depo.Ad.Contains("İl Deposu", StringComparison.OrdinalIgnoreCase);
            var carpan = isMerkez ? 8 : isAna ? 3 : 1;

            foreach (var t in sarfTanimlar)
            {
                var miktar = Math.Max(5, rnd.Next(12, 90) * carpan);
                depo.Stoklar.Add(new DepoStok
                {
                    TasinirTanimId = t.Id,
                    Miktar = miktar,
                    MinEsik = Math.Max(3, miktar / 5),
                    BirimMaliyet = t.Id switch
                    {
                        "tt-013" => 95,
                        "tt-014" => 620,
                        "tt-019" => 7,
                        "tt-020" => 18,
                        _ => rnd.Next(10, 120)
                    },
                    SonGuncelleme = DateTime.UtcNow.AddDays(-rnd.Next(1, 90))
                });
            }

            foreach (var t in demirbasTanimlar.OrderBy(_ => rnd.Next()).Take(isMerkez ? 8 : isAna ? 5 : 3))
            {
                var miktar = Math.Max(1, rnd.Next(1, 8) * carpan);
                depo.Stoklar.Add(new DepoStok
                {
                    TasinirTanimId = t.Id,
                    Miktar = miktar,
                    MinEsik = Math.Max(1, miktar / 6),
                    BirimMaliyet = t.Id switch
                    {
                        "tt-004" => 18500,
                        "tt-005" => 27500,
                        "tt-001" => 7200,
                        "tt-010" => 4200,
                        "tt-011" => 2600,
                        _ => rnd.Next(1500, 35000)
                    },
                    SonGuncelleme = DateTime.UtcNow.AddDays(-rnd.Next(1, 120))
                });
            }
            await svc.DepoKaydetAsync(depo);
        }

        var tekilSayac = tasinirKayitlar.Count + 5000;
        foreach (var depo in depolar.Where(d => d.AktifMi && !tasinirKayitlar.Any(k => k.DepoId == d.Id)).Take(489))
        {
            var kurum = kurumlar.FirstOrDefault(k => k.Id == depo.KurumId);
            var il = kurum?.Il ?? "Ankara";
            var ilKod = depo.Kod.Split('-').Skip(1).FirstOrDefault() ?? "00";
            var demirbasStoklar = depo.Stoklar.Where(s => demirbasTanimlar.Any(t => t.Id == s.TasinirTanimId)).Take(3).ToList();
            foreach (var stok in demirbasStoklar)
            {
                var tanim = demirbasTanimlar.First(t => t.Id == stok.TasinirTanimId);
                tekilSayac++;
                var kayit = new TasinirKayit
                {
                    Id = $"tk-{depo.Id}-{stok.TasinirTanimId}-{tekilSayac}",
                    BarKod = $"BK{ilKod}{tekilSayac:D7}",
                    Cinsi = tanim.Ad,
                    Aciklama = $"{depo.Ad} envanterinde kayıtlı {tanim.Ad}",
                    MarkaAdi = tanim.Id switch
                    {
                        "tt-004" => "Dell",
                        "tt-005" => "Lenovo",
                        "tt-001" => "HP",
                        "tt-010" => "Bürotime",
                        "tt-011" => "Koleksiyon",
                        _ => "Kamu Standardı"
                    },
                    Modeli = tanim.Id switch
                    {
                        "tt-004" => "OptiPlex 7010",
                        "tt-005" => "ThinkPad E15",
                        "tt-001" => "LaserJet Pro M404",
                        "tt-010" => "Çalışma Masası",
                        "tt-011" => "Ergonomik Koltuk",
                        _ => "2026"
                    },
                    OlcuAdi = "Adet",
                    SicilNo = $"255.{ilKod}.{tekilSayac:D6}",
                    SeriNo = $"SN-{ToAsciiSlug(il).ToUpperInvariant()[..Math.Min(3, ToAsciiSlug(il).Length)]}-{rnd.Next(100000, 999999)}",
                    BirimFiyat = stok.BirimMaliyet,
                    FisNo = $"TIF-{DateTime.UtcNow.Year}-{rnd.Next(1000, 9999)}",
                    FisIlkDurum = "Satın Alma Girişi",
                    FisSonDurum = "Ambarda",
                    Tarih = DateTime.UtcNow.AddDays(-rnd.Next(10, 720)),
                    AmbarAdi = depo.Ad,
                    KurumGirisIslemi = "Satın Alma",
                    KurumGirisTarihi = DateTime.UtcNow.AddDays(-rnd.Next(10, 720)),
                    IlkGirisTarihi = DateTime.UtcNow.AddDays(-rnd.Next(10, 720)),
                    LimitDurumu = stok.BirimMaliyet >= 10000 ? "Limit Üstü" : "Limit Altı",
                    HarBirimiAdi = "Destek Hizmetleri",
                    HarBirimiKodu = $"38.{ilKod}.00.01",
                    IlAdi = il,
                    IlKodu = ilKod,
                    SayKod = $"SAY-{DateTime.UtcNow.Year}",
                    SayAdi = $"{DateTime.UtcNow.Year} Yılı Sayımı",
                    TasinirTanimId = tanim.Id,
                    DepoId = depo.Id,
                    KurumId = depo.KurumId,
                    Durum = TasinirKayitDurumu.Ambarda,
                    HareketGecmisi = new()
                    {
                        new TasinirHareket
                        {
                            Tarih = DateTime.UtcNow.AddDays(-rnd.Next(10, 720)),
                            IslemTuru = "Kurum Girişi",
                            Aciklama = "Gerçekçi açılış envanteri olarak kayda alındı.",
                            KullaniciAdi = "Sistem Yöneticisi",
                            YeniDurum = "Ambarda"
                        }
                    }
                };
                tasinirKayitlar.Add(kayit);
                await svc.TasinirKayitKaydetAsync(kayit);
            }
        }

        foreach (var kurum in kurumlar.Where(k => k.Tur == KurumTur.IlMudurlugu).Take(60))
        {
            var personel = kullanicilar.FirstOrDefault(k => k.KurumId == kurum.Id && k.Rol == AtomRoller.Personel);
            if (personel == null || zimmetler.Any(z => z.PersonelId == personel.Id && z.Durum == ZimmetDurumu.Aktif)) continue;
            var kayitlar = tasinirKayitlar.Where(k => k.KurumId == kurum.Id && k.Durum == TasinirKayitDurumu.Ambarda && !string.IsNullOrWhiteSpace(k.DepoId)).Take(2).ToList();
            if (!kayitlar.Any()) continue;
            var zimmet = new Zimmet
            {
                Id = $"zm-seed-{personel.Id}",
                ZimmetNo = await svc.YeniNumaraUretAsync("ZMT"),
                DepoId = kayitlar.First().DepoId ?? "",
                PersonelId = personel.Id,
                VerenKullaniciId = "u-admin",
                ZimmetTarihi = DateTime.UtcNow.AddDays(-rnd.Next(1, 180)),
                Durum = ZimmetDurumu.Aktif,
                TeslimAlanImzaDurumu = "İmzalandı",
                TeslimYeri = kurum.Ad,
                Aciklama = "Gerçekçi ülke geneli örnek personel zimmeti.",
                Kalemler = kayitlar.Select(k => new ZimmetKalemi
                {
                    TasinirKayitId = k.Id,
                    TasinirTanimId = k.TasinirTanimId ?? "",
                    Miktar = 1,
                    SeriNo = k.SeriNo,
                    Barkod = k.BarKod,
                    SicilNo = k.SicilNo,
                    Marka = k.MarkaAdi,
                    Model = k.Modeli
                }).ToList()
            };
            zimmet.OnayGecmisi.Add(new OnayKaydi
            {
                KullaniciId = "u-admin",
                KullaniciAdi = "Sistem Yöneticisi",
                Rol = AtomRoller.SistemAdmin,
                Karar = OnayDurumu.Onaylandi,
                Asama = "Zimmet Oluşturma",
                Tarih = zimmet.ZimmetTarihi
            });
            await svc.ZimmetKaydetAsync(zimmet);
            zimmetler.Add(zimmet);
            foreach (var kayit in kayitlar)
            {
                kayit.Durum = TasinirKayitDurumu.Zimmetli;
                kayit.ZimmetId = zimmet.Id;
                kayit.FisSonDurum = "Zimmet";
                kayit.VerildigiYerBirim = personel.AdSoyad;
                kayit.HareketGecmisi.Add(new TasinirHareket
                {
                    Tarih = zimmet.ZimmetTarihi,
                    IslemTuru = "Zimmet",
                    Aciklama = $"{zimmet.ZimmetNo} ile {personel.AdSoyad} adlı personele zimmetlendi.",
                    KullaniciAdi = "Sistem Yöneticisi",
                    OncekiDurum = "Ambarda",
                    YeniDurum = "Zimmetli"
                });
                await svc.TasinirKayitKaydetAsync(kayit);
            }
        }

        foreach (var depo in depolar.Where(d => d.Stoklar.Count > 0).Take(120))
        {
            if (stokHareketler.Any(h => h.DepoId == depo.Id && h.IslemTuru == StokIslemTuru.AcilisGirisi)) continue;
            foreach (var stok in depo.Stoklar.Take(6))
            {
                await svc.StokHareketKaydetAsync(new StokHareket
                {
                    HareketNo = await svc.YeniNumaraUretAsync("SH"),
                    DepoId = depo.Id,
                    TasinirTanimId = stok.TasinirTanimId,
                    IslemTuru = StokIslemTuru.AcilisGirisi,
                    GirisMiktar = stok.Miktar,
                    KalanMiktar = stok.Miktar,
                    BirimMaliyet = stok.BirimMaliyet,
                    ToplamTutar = stok.BirimMaliyet * stok.Miktar,
                    KaynakBelgeTur = "Seed",
                    KaynakBelgeNo = $"ACLS-{DateTime.UtcNow.Year}-{depo.Kod}",
                    KullaniciId = "u-admin",
                    KullaniciAdi = "Sistem Yöneticisi",
                    Aciklama = "Gerçekçi ülke geneli açılış stoğu."
                });
            }
        }

        if (personelSarfBakiyeler.Count == 0)
        {
            foreach (var kurum in kurumlar.Where(k => k.Tur == KurumTur.IlMudurlugu).Take(20))
            {
                var personel = kullanicilar.FirstOrDefault(k => k.KurumId == kurum.Id && k.Rol == AtomRoller.Personel);
                var depo = depolar.FirstOrDefault(d => d.KurumId == kurum.Id && d.Stoklar.Any(s => sarfTanimlar.Any(t => t.Id == s.TasinirTanimId)));
                if (personel == null || depo == null) continue;
                foreach (var stok in depo.Stoklar.Where(s => sarfTanimlar.Any(t => t.Id == s.TasinirTanimId)).Take(2))
                {
                    var miktar = Math.Min(Math.Max(1, stok.Miktar / 20), 8);
                    var belgeNo = await svc.YeniNumaraUretAsync("PSH");
                    await svc.PersonelSarfBakiyeKaydetAsync(new PersonelSarfBakiye
                    {
                        PersonelId = personel.Id,
                        KurumId = kurum.Id,
                        KaynakDepoId = depo.Id,
                        TasinirTanimId = stok.TasinirTanimId,
                        Miktar = miktar,
                        SonGuncelleme = DateTime.UtcNow.AddDays(-rnd.Next(1, 30))
                    });
                    await svc.PersonelSarfHareketKaydetAsync(new PersonelSarfHareket
                    {
                        HareketNo = belgeNo,
                        PersonelId = personel.Id,
                        KurumId = kurum.Id,
                        KaynakDepoId = depo.Id,
                        TasinirTanimId = stok.TasinirTanimId,
                        IslemTuru = PersonelSarfIslemTuru.Verme,
                        GirisMiktar = miktar,
                        KalanMiktar = miktar,
                        KaynakBelgeNo = belgeNo,
                        KullaniciId = "u-admin",
                        KullaniciAdi = "Sistem Yöneticisi",
                        Aciklama = "Son kullanıcıya verilmiş gerçekçi sarf örneği."
                    });
                }
            }
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    private static async Task SeedUlusalAileSosyalHizmetKurumVeDepolari(IAtomDataService svc)
    {
        var kurumlar = await svc.KurumlariGetirAsync();
        var depolar = await svc.DepolariGetirAsync();
        var bakanlik = kurumlar.FirstOrDefault(k => k.Tur == KurumTur.Bakanlik)
            ?? kurumlar.FirstOrDefault(k => k.Id == "k-bakanlik");

        if (bakanlik == null)
        {
            bakanlik = new Kurum
            {
                Id = "k-bakanlik",
                Ad = "Bakanlık Merkez Teşkilatı",
                Kod = "ASHB-MRK",
                Tur = KurumTur.Bakanlik,
                Il = "Ankara",
                Telefon = "0312 000 0000",
                Email = "merkez@aile.gov.tr"
            };
            await svc.KurumKaydetAsync(bakanlik);
            kurumlar.Add(bakanlik);
        }

        var iller = new (int Plaka, string Il)[]
        {
            (1, "Adana"), (2, "Adıyaman"), (3, "Afyonkarahisar"), (4, "Ağrı"), (5, "Amasya"),
            (6, "Ankara"), (7, "Antalya"), (8, "Artvin"), (9, "Aydın"), (10, "Balıkesir"),
            (11, "Bilecik"), (12, "Bingöl"), (13, "Bitlis"), (14, "Bolu"), (15, "Burdur"),
            (16, "Bursa"), (17, "Çanakkale"), (18, "Çankırı"), (19, "Çorum"), (20, "Denizli"),
            (21, "Diyarbakır"), (22, "Edirne"), (23, "Elazığ"), (24, "Erzincan"), (25, "Erzurum"),
            (26, "Eskişehir"), (27, "Gaziantep"), (28, "Giresun"), (29, "Gümüşhane"), (30, "Hakkari"),
            (31, "Hatay"), (32, "Isparta"), (33, "Mersin"), (34, "İstanbul"), (35, "İzmir"),
            (36, "Kars"), (37, "Kastamonu"), (38, "Kayseri"), (39, "Kırklareli"), (40, "Kırşehir"),
            (41, "Kocaeli"), (42, "Konya"), (43, "Kütahya"), (44, "Malatya"), (45, "Manisa"),
            (46, "Kahramanmaraş"), (47, "Mardin"), (48, "Muğla"), (49, "Muş"), (50, "Nevşehir"),
            (51, "Niğde"), (52, "Ordu"), (53, "Rize"), (54, "Sakarya"), (55, "Samsun"),
            (56, "Siirt"), (57, "Sinop"), (58, "Sivas"), (59, "Tekirdağ"), (60, "Tokat"),
            (61, "Trabzon"), (62, "Tunceli"), (63, "Şanlıurfa"), (64, "Uşak"), (65, "Van"),
            (66, "Yozgat"), (67, "Zonguldak"), (68, "Aksaray"), (69, "Bayburt"), (70, "Karaman"),
            (71, "Kırıkkale"), (72, "Batman"), (73, "Şırnak"), (74, "Bartın"), (75, "Ardahan"),
            (76, "Iğdır"), (77, "Yalova"), (78, "Karabük"), (79, "Kilis"), (80, "Osmaniye"),
            (81, "Düzce")
        };

        var depoTurleri = new (string Suffix, string Ad, double KapasiteM2)[]
        {
            ("ana", "İl Müdürlüğü Ana Deposu", 450),
            ("shm", "Sosyal Hizmet Merkezi Deposu", 180),
            ("cocuk", "Çocuk Hizmetleri Kuruluş Deposu", 220),
            ("yasli", "Huzurevi ve Yaşlı Bakım Kuruluş Deposu", 240),
            ("engelli", "Engelli Bakım ve Rehabilitasyon Kuruluş Deposu", 240),
            ("kadin", "Kadın Konukevi ve ŞÖNİM Deposu", 160)
        };

        foreach (var (plaka, il) in iller)
        {
            var ilKurum = kurumlar.FirstOrDefault(k =>
                k.Tur == KurumTur.IlMudurlugu &&
                string.Equals(k.Il, il, StringComparison.OrdinalIgnoreCase));

            if (ilKurum == null)
            {
                ilKurum = new Kurum
                {
                    Id = $"k-il-{plaka:00}",
                    Ad = $"{il} Aile ve Sosyal Hizmetler İl Müdürlüğü",
                    Kod = $"{plaka:00}-ASHIM",
                    Tur = KurumTur.IlMudurlugu,
                    UstKurumId = bakanlik.Id,
                    Il = il,
                    Adres = $"{il} Aile ve Sosyal Hizmetler İl Müdürlüğü",
                    Email = $"{ToAsciiSlug(il)}@aile.gov.tr"
                };
                await svc.KurumKaydetAsync(ilKurum);
                kurumlar.Add(ilKurum);
            }

            foreach (var (suffix, ad, kapasite) in depoTurleri)
            {
                var depoKod = $"DEP-{plaka:00}-{suffix.ToUpperInvariant()}";
                var depoAd = $"{il} {ad}";
                var mevcutDepo = depolar.Any(d =>
                    string.Equals(d.Kod, depoKod, StringComparison.OrdinalIgnoreCase) ||
                    (d.KurumId == ilKurum.Id && string.Equals(d.Ad, depoAd, StringComparison.OrdinalIgnoreCase)));
                if (mevcutDepo) continue;

                var depo = new Depo
                {
                    Id = $"d-{plaka:00}-{suffix}",
                    Ad = depoAd,
                    Kod = depoKod,
                    KurumId = ilKurum.Id,
                    MerkezDepoMu = false,
                    Adres = $"{ilKurum.Ad} - {ad}",
                    KapasiteM2 = kapasite,
                    AktifMi = true
                };
                await svc.DepoKaydetAsync(depo);
                depolar.Add(depo);
            }
        }

        var merkezDepoVar = depolar.Any(d => d.KurumId == bakanlik.Id && d.MerkezDepoMu);
        if (!merkezDepoVar)
        {
            await svc.DepoKaydetAsync(new Depo
            {
                Id = "d-merkez",
                Ad = "Bakanlık Merkez Deposu",
                Kod = "DEP-MRK-001",
                KurumId = bakanlik.Id,
                MerkezDepoMu = true,
                Adres = "Bakanlık Merkez Teşkilatı - Ankara",
                KapasiteM2 = 2500,
                AktifMi = true
            });
        }
    }

    private static string ToAsciiSlug(string value)
    {
        return value
            .Replace("Ç", "C").Replace("Ğ", "G").Replace("İ", "I").Replace("I", "I").Replace("Ö", "O").Replace("Ş", "S").Replace("Ü", "U")
            .ToLowerInvariant()
            .Replace("ç", "c").Replace("ğ", "g").Replace("ı", "i").Replace("ö", "o").Replace("ş", "s").Replace("ü", "u")
            .Replace("â", "a").Replace("î", "i").Replace("û", "u")
            .Replace(" ", "").Replace("-", "");
    }

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
            new() { Id = "u-teknisyen", AdSoyad = "Murat Teknik", KullaniciAdi = "teknisyen",
                    Email = "murat.teknik@ankara.gov.tr", Rol = AtomRoller.Teknisyen, KurumId = "k-ankara",
                    SifreHash = BC.HashPassword("Teknik123!") },
            new() { Id = "u-izmir-il", AdSoyad = "Kemal Aydın", KullaniciAdi = "izmir",
                    Email = "kemal.aydin@izmir.gov.tr", Rol = AtomRoller.IlMuduru, KurumId = "k-izmir",
                    SifreHash = BC.HashPassword("Izmir123!") },
            new() { Id = "u-bursa-il", AdSoyad = "Selin Yıldız", KullaniciAdi = "bursa",
                    Email = "selin.yildiz@bursa.gov.tr", Rol = AtomRoller.IlMuduru, KurumId = "k-bursa",
                    SifreHash = BC.HashPassword("Bursa123!") },
            new() { Id = "u-antalya-il", AdSoyad = "Emre Koç", KullaniciAdi = "antalya",
                    Email = "emre.koc@antalya.gov.tr", Rol = AtomRoller.IlMuduru, KurumId = "k-antalya",
                    SifreHash = BC.HashPassword("Antalya123!") },
            new() { Id = "u-personel2", AdSoyad = "Burak Şimşek", KullaniciAdi = "personel2",
                    Email = "burak.simsek@istanbul.gov.tr", Rol = AtomRoller.Personel, KurumId = "k-istanbul",
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
            new() { Id = "tt-001", Kod = "795.05.01.01", Ad = "Lazer Yazıcı", Kategori = TasinirKategori.BilisimDonanim, Birim = "Adet", Aciklama = "A4 lazer yazıcı", DemirbasMi = true, KritikEsik = 20 },
            new() { Id = "tt-002", Kod = "795.05.01.02", Ad = "Fotokopi Makinesi", Kategori = TasinirKategori.BilisimDonanim, Birim = "Adet", DemirbasMi = true, KritikEsik = 10 },
            new() { Id = "tt-003", Kod = "795.05.01.03", Ad = "Kart Okuyuculu Çok Fonksiyonlu Baskı Makinesi", Kategori = TasinirKategori.BilisimDonanim, Birim = "Adet", Aciklama = "Kart okuyuculu toplu baskı makinesi", DemirbasMi = true, KritikEsik = 5 },
            new() { Id = "tt-004", Kod = "795.05.02.01", Ad = "Masaüstü Bilgisayar", Kategori = TasinirKategori.BilisimDonanim, Birim = "Adet", DemirbasMi = true, KritikEsik = 30 },
            new() { Id = "tt-005", Kod = "795.05.02.02", Ad = "Dizüstü Bilgisayar", Kategori = TasinirKategori.BilisimDonanim, Birim = "Adet", DemirbasMi = true, KritikEsik = 15 },
            new() { Id = "tt-006", Kod = "795.05.02.03", Ad = "Tablet Bilgisayar", Kategori = TasinirKategori.BilisimDonanim, Birim = "Adet", DemirbasMi = true, KritikEsik = 10 },
            new() { Id = "tt-007", Kod = "795.05.03.01", Ad = "Monitör (24 inç)", Kategori = TasinirKategori.BilisimDonanim, Birim = "Adet", DemirbasMi = true, KritikEsik = 20 },
            new() { Id = "tt-008", Kod = "795.05.04.01", Ad = "UPS (1000VA)", Kategori = TasinirKategori.BilisimDonanim, Birim = "Adet", DemirbasMi = true, KritikEsik = 10 },
            new() { Id = "tt-009", Kod = "795.05.05.01", Ad = "Ağ Anahtarı (24 Port)", Kategori = TasinirKategori.BilisimDonanim, Birim = "Adet", DemirbasMi = true, KritikEsik = 5 },
            new() { Id = "tt-010", Kod = "795.03.01.01", Ad = "Masa (Çalışma Masası)", Kategori = TasinirKategori.MobilYa, Birim = "Adet", DemirbasMi = true, KritikEsik = 15 },
            new() { Id = "tt-011", Kod = "795.03.01.02", Ad = "Ofis Koltuğu", Kategori = TasinirKategori.MobilYa, Birim = "Adet", DemirbasMi = true, KritikEsik = 15 },
            new() { Id = "tt-012", Kod = "795.03.01.03", Ad = "Dosya Dolabı (Çelik)", Kategori = TasinirKategori.MobilYa, Birim = "Adet", DemirbasMi = true, KritikEsik = 10 },
            new() { Id = "tt-013", Kod = "795.02.01.01", Ad = "A4 Fotokopi Kağıdı (500 Yaprak)", Kategori = TasinirKategori.Kirtasiye, Birim = "Paket", DemirbasMi = false, KritikEsik = 100 },
            new() { Id = "tt-014", Kod = "795.02.01.02", Ad = "Toner (Lazer Yazıcı)", Kategori = TasinirKategori.Kirtasiye, Birim = "Adet", DemirbasMi = false, KritikEsik = 50 },
            new() { Id = "tt-015", Kod = "795.06.01.01", Ad = "IP Kamera (Güvenlik)", Kategori = TasinirKategori.GuvenlikSistemi, Birim = "Adet", DemirbasMi = true, KritikEsik = 8 },
            new() { Id = "tt-016", Kod = "795.06.01.02", Ad = "Geçiş Kontrol Terminali", Kategori = TasinirKategori.GuvenlikSistemi, Birim = "Adet", DemirbasMi = true, KritikEsik = 5 },
            new() { Id = "tt-017", Kod = "795.04.01.01", Ad = "Klima (Duvar Tipi 12.000 BTU)", Kategori = TasinirKategori.ElektrikliEv, Birim = "Adet", DemirbasMi = true, KritikEsik = 10 },
            new() { Id = "tt-018", Kod = "795.04.01.02", Ad = "Buzdolabı (Büro Tipi)", Kategori = TasinirKategori.ElektrikliEv, Birim = "Adet", DemirbasMi = true, KritikEsik = 5 },
            new() { Id = "tt-019", Kod = "795.02.02.01", Ad = "Kalem (Tükenmez)", Kategori = TasinirKategori.Kirtasiye, Birim = "Adet", DemirbasMi = false, KritikEsik = 200 },
            new() { Id = "tt-020", Kod = "795.02.02.02", Ad = "Dosya Klasörü", Kategori = TasinirKategori.Kirtasiye, Birim = "Adet", DemirbasMi = false, KritikEsik = 150 },
            new() { Id = "tt-021", Kod = "254.01.01.01", Ad = "Binek Araç (Sedan)", Kategori = TasinirKategori.Arac, Birim = "Adet", DemirbasMi = true, KritikEsik = 2 },
            new() { Id = "tt-022", Kod = "255.01.01.01", Ad = "Projeksiyon Cihazı", Kategori = TasinirKategori.BilisimDonanim, Birim = "Adet", DemirbasMi = true, KritikEsik = 5 },
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

    private static async Task SeedTasinirKayitlar(IAtomDataService svc)
    {
        var mevcut = await svc.TasinirKayitlariGetirAsync();
        if (mevcut.Count > 0) return;

        var rnd = new Random(42);
        var cinsler = new[]
        {
            ("Lazer Yazıcı", "HP", "LaserJet Pro M404", 3500m, "tt-001"),
            ("Masaüstü Bilgisayar", "Dell", "OptiPlex 7090", 8500m, "tt-004"),
            ("Dizüstü Bilgisayar", "Lenovo", "ThinkPad E15", 12000m, "tt-005"),
            ("Monitör 24\"", "Samsung", "S24R350", 2200m, "tt-007"),
            ("Fotokopi Makinesi", "Canon", "iR 2630", 15000m, "tt-002"),
            ("Ofis Koltuğu", "Bürotime", "Ergo-X", 1800m, "tt-011"),
        };
        var iller = new[] { ("ANKARA","06","k-ankara","Ankara İl Deposu","d-ankara"), ("İSTANBUL","34","k-istanbul","İstanbul İl Deposu","d-istanbul") };
        var harBirimleri = new[] { ("İdari ve Mali İşler Şube Md.","38.06.00.01"), ("Bilgi İşlem Şube Md.","38.06.00.02"), ("Destek Hizmetleri Md.","38.06.00.03") };

        var kayitlar = new List<TasinirKayit>();
        int sicilSayac = 1000;

        foreach (var (ilAd, ilKod, kurumId, ambar, depoId) in iller)
        {
            for (int i = 0; i < 12; i++)
            {
                var (cins, marka, model, fiyat, tanimId) = cinsler[rnd.Next(cinsler.Length)];
                var (harAd, harKod) = harBirimleri[rnd.Next(harBirimleri.Length)];
                var zimmetli = rnd.Next(100) < 45;
                var girisTarih = DateTime.UtcNow.AddDays(-rnd.Next(30, 900));
                sicilSayac++;

                kayitlar.Add(new TasinirKayit
                {
                    BarKod = $"BK{ilKod}{sicilSayac:D6}",
                    Cinsi = cins,
                    Aciklama = $"{cins} - {ilAd} envanteri",
                    MarkaAdi = marka,
                    Modeli = model,
                    OlcuAdi = "Adet",
                    SicilNo = $"253.{ilKod}.{sicilSayac}",
                    SeriNo = $"SN-{marka.Substring(0, 2).ToUpper()}{rnd.Next(100000, 999999)}",
                    BirimFiyat = fiyat,
                    FisNo = $"TIF-{girisTarih.Year}-{rnd.Next(1, 500):D4}",
                    FisIlkDurum = "Giriş - Satın Alma",
                    FisSonDurum = zimmetli ? "Zimmet" : "Ambarda",
                    Tarih = girisTarih,
                    VerildigiYerBirim = zimmetli ? harAd : "",
                    TcNumarasi = zimmetli ? rnd.NextInt64(10000000000, 99999999999).ToString() : "",
                    AmbarAdi = ambar,
                    KurumGirisIslemi = "Satın Alma",
                    KurumGirisTarihi = girisTarih,
                    IlkGirisTarihi = girisTarih,
                    LimitDurumu = fiyat >= 10000 ? "Limit Üstü" : "Limit Altı",
                    HarBirimiAdi = harAd,
                    HarBirimiKodu = harKod,
                    IlAdi = ilAd,
                    IlKodu = ilKod,
                    SayKod = $"SAY-{girisTarih.Year}",
                    SayAdi = $"{girisTarih.Year} Yılı Sayımı",
                    ProjeNumarasi = rnd.Next(100) < 30 ? $"PRJ-{rnd.Next(1000, 9999)}" : "",
                    TasinirTanimId = tanimId,
                    KurumId = kurumId,
                    DepoId = depoId,
                    Durum = zimmetli ? TasinirKayitDurumu.Zimmetli : TasinirKayitDurumu.Ambarda,
                    OlusturmaTarihi = girisTarih,
                    GuncellemeTarihi = girisTarih,
                    HareketGecmisi = new List<TasinirHareket>
                    {
                        new() { Tarih = girisTarih, IslemTuru = "Kurum Girişi", Aciklama = "Satın alma yoluyla kayda alındı.",
                                KullaniciAdi = "Sistem (Seed)", YeniDurum = "Ambarda" }
                    }
                });
            }
        }

        await svc.TasinirKayitlariTopluKaydetAsync(kayitlar);
    }
}
