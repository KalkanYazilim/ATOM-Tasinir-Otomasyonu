namespace ATOM.Models.Domain;

// ═════════════════════════════════════════════════════════════
// TAŞINIR GİRİŞ YÖNTEMLERİ (Taşınır Mal Yönetmeliği md. giriş işlemleri)
// ═════════════════════════════════════════════════════════════

public enum GirisYontemi
{
    SatinAlmaIhale,        // 4734 sayılı Kanun — ihale ile alım
    SatinAlmaDogrudanTemin,// 4734 md.22 — doğrudan temin
    KurumButcesiAlim,      // Kurumun kendi bütçesinden alım (avans/ön ödeme dahil)
    BagisYardim,           // Bağış ve yardım yoluyla giriş
    DevirGirisi,           // Bedelsiz devir alma (kurum içi/kurumlar arası)
    UretimImalat,          // Kurumun kendi imkanlarıyla üretim/imalat
    SayimFazlasi,          // Sayım fazlası taşınır girişi
    IadeGirisi,            // Kullanımdan iade ile giriş
    Musadere,              // Müsadere/zoralım/mahkeme kararı
    Diger                  // Diğer giriş yolları
}

public enum CikisYontemi
{
    TuketimSuretiyle,      // Sarf malzeme tüketim çıkışı
    KullanimaVerme,        // Zimmet (dayanıklı taşınır)
    DevirSuretiyle,        // Bedelsiz devir çıkışı
    SatisSuretiyle,        // 2886 sayılı Kanun — satış
    Hurdaya,               // Hurdaya ayırma
    KayipCalinma,          // Kayıp / çalınma / yok olma
    Fire,                  // Fire / kullanım dışı
    BagisHibe,             // Bağış/hibe ile çıkış
    Diger
}

public enum BelgeDurumu { Taslak, Onaylandi, Reddedildi, Iptal }

public static class GirisCikisYardimci
{
    public static string GirisAd(GirisYontemi y) => y switch
    {
        GirisYontemi.SatinAlmaIhale => "Satın Alma — İhale (4734 sK)",
        GirisYontemi.SatinAlmaDogrudanTemin => "Satın Alma — Doğrudan Temin (4734/22)",
        GirisYontemi.KurumButcesiAlim => "Kurum Bütçesinden Alım",
        GirisYontemi.BagisYardim => "Bağış ve Yardım",
        GirisYontemi.DevirGirisi => "Bedelsiz Devir (Giriş)",
        GirisYontemi.UretimImalat => "Üretim / İmalat",
        GirisYontemi.SayimFazlasi => "Sayım Fazlası",
        GirisYontemi.IadeGirisi => "İade Girişi",
        GirisYontemi.Musadere => "Müsadere / Zoralım",
        GirisYontemi.Diger => "Diğer Giriş",
        _ => y.ToString()
    };

    public static string CikisAd(CikisYontemi y) => y switch
    {
        CikisYontemi.TuketimSuretiyle => "Tüketim Suretiyle Çıkış",
        CikisYontemi.KullanimaVerme => "Kullanıma Verme (Zimmet)",
        CikisYontemi.DevirSuretiyle => "Bedelsiz Devir (Çıkış)",
        CikisYontemi.SatisSuretiyle => "Satış (2886 sK)",
        CikisYontemi.Hurdaya => "Hurdaya Ayırma",
        CikisYontemi.KayipCalinma => "Kayıp / Çalınma / Yok Olma",
        CikisYontemi.Fire => "Fire",
        CikisYontemi.BagisHibe => "Bağış / Hibe",
        CikisYontemi.Diger => "Diğer Çıkış",
        _ => y.ToString()
    };

    /// <summary>Yöntemin mevzuat dayanağı.</summary>
    public static string GirisDayanak(GirisYontemi y) => y switch
    {
        GirisYontemi.SatinAlmaIhale => "4734 sayılı Kamu İhale Kanunu, Taşınır Mal Yönetmeliği",
        GirisYontemi.SatinAlmaDogrudanTemin => "4734 sayılı Kanun md.22, Taşınır Mal Yönetmeliği",
        GirisYontemi.KurumButcesiAlim => "5018 sayılı Kanun, Ön Ödeme Usul ve Esasları Hakkında Yönetmelik",
        GirisYontemi.BagisYardim => "Taşınır Mal Yönetmeliği — Bağış ve yardım yoluyla edinim",
        GirisYontemi.DevirGirisi => "Taşınır Mal Yönetmeliği Genel Tebliği (Sayı:1) — Bedelsiz devir",
        GirisYontemi.UretimImalat => "Taşınır Mal Yönetmeliği — İç imkanlarla üretim",
        GirisYontemi.SayimFazlasi => "Taşınır Mal Yönetmeliği — Sayım fazlası",
        GirisYontemi.IadeGirisi => "Taşınır Mal Yönetmeliği — İade işlemleri",
        GirisYontemi.Musadere => "İlgili kanunlar / mahkeme kararı, Taşınır Mal Yönetmeliği",
        GirisYontemi.Diger => "Taşınır Mal Yönetmeliği",
        _ => "Taşınır Mal Yönetmeliği"
    };
}

public class MalGirisBelgesi
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string GirisNo { get; set; } = "";          // GR-2026-000123
    public GirisYontemi Yontem { get; set; }
    public string DepoId { get; set; } = "";
    public string KurumId { get; set; } = "";
    public DateTime Tarih { get; set; } = DateTime.UtcNow;
    public string TifNo { get; set; } = "";
    public BelgeDurumu Durum { get; set; } = BelgeDurumu.Taslak;
    public string OlusturanKullaniciId { get; set; } = "";

    // Yönteme özgü alanlar
    public string? IhaleId { get; set; }               // SatinAlmaIhale
    public string? FirmaId { get; set; }               // satın alma
    public string FaturaNo { get; set; } = "";
    public DateTime? FaturaTarihi { get; set; }
    public decimal FaturaTutari { get; set; }
    public string IrsaliyeNo { get; set; } = "";
    public string ButceTertibi { get; set; } = "";     // bütçe alımı
    public string BagisYapan { get; set; } = "";       // bağış/yardım
    public string DayanakBelge { get; set; } = "";     // protokol/karar/yazı no
    public string Aciklama { get; set; } = "";
    public List<MalGirisKalemi> Kalemler { get; set; } = new();
    public List<OnayKaydi> OnayGecmisi { get; set; } = new();
    public bool TasinirKayitUretildiMi { get; set; }
}

public class MalGirisKalemi
{
    public string TasinirTanimId { get; set; } = "";
    public int Miktar { get; set; }
    public decimal BirimFiyat { get; set; }
    public bool DemirbasMi { get; set; }
    public string Marka { get; set; } = "";
    public string Model { get; set; } = "";
    public List<string> SeriNoListesi { get; set; } = new();
    public DateTime? GarantiBitisTarihi { get; set; }
}

public class MalCikisBelgesi
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CikisNo { get; set; } = "";
    public CikisYontemi Yontem { get; set; }
    public string DepoId { get; set; } = "";
    public string KurumId { get; set; } = "";
    public DateTime Tarih { get; set; } = DateTime.UtcNow;
    public string TifNo { get; set; } = "";
    public BelgeDurumu Durum { get; set; } = BelgeDurumu.Onaylandi;
    public string OlusturanKullaniciId { get; set; } = "";

    public string AliciBilgi { get; set; } = "";        // satış alıcısı / bağış alanı / devir hedefi
    public decimal SatisBedeli { get; set; }            // satış
    public string DayanakBelge { get; set; } = "";      // karar/onay/ihale no
    public string Aciklama { get; set; } = "";
    public List<MalCikisKalemi> Kalemler { get; set; } = new();
    public List<OnayKaydi> OnayGecmisi { get; set; } = new();
}

public class MalCikisKalemi
{
    public string? TasinirKayitId { get; set; }
    public string TasinirTanimId { get; set; } = "";
    public int Miktar { get; set; }
    public string SeriNo { get; set; } = "";
}
