namespace ATOM.Models.Domain;

// ═════════════════════════════════════════════════════════════
// STOK HAREKETİ — her stok değişimi belgeli kayıt
// ═════════════════════════════════════════════════════════════

public enum StokIslemTuru
{
    SatinAlmaGirisi, DevirGirisi, BagisGirisi, SayimFazlasi,
    SevkCikisi, SevkGirisi, ZimmetCikisi, ZimmetIadesi,
    HurdaDusum, Kayip, BakimCikisi, BakimDonusu, Duzeltme, AcilisGirisi
}

public class StokHareket
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string HareketNo { get; set; } = "";
    public string DepoId { get; set; } = "";
    public string TasinirTanimId { get; set; } = "";
    public string? TasinirKayitId { get; set; }
    public StokIslemTuru IslemTuru { get; set; }
    public int GirisMiktar { get; set; }
    public int CikisMiktar { get; set; }
    public int KalanMiktar { get; set; }
    public decimal BirimMaliyet { get; set; }
    public decimal ToplamTutar { get; set; }
    public string KaynakBelgeTur { get; set; } = "";   // MalKabul, Sevk, Zimmet, Hurda, Sayim, Devir
    public string KaynakBelgeId { get; set; } = "";
    public string KaynakBelgeNo { get; set; } = "";
    public DateTime Tarih { get; set; } = DateTime.UtcNow;
    public string KullaniciId { get; set; } = "";
    public string KullaniciAdi { get; set; } = "";
    public string Aciklama { get; set; } = "";

    public static string IslemAdi(StokIslemTuru t) => t switch
    {
        StokIslemTuru.SatinAlmaGirisi => "Satın Alma Girişi",
        StokIslemTuru.DevirGirisi => "Devir Girişi",
        StokIslemTuru.BagisGirisi => "Bağış Girişi",
        StokIslemTuru.SayimFazlasi => "Sayım Fazlası",
        StokIslemTuru.SevkCikisi => "Sevk Çıkışı",
        StokIslemTuru.SevkGirisi => "Sevk Girişi",
        StokIslemTuru.ZimmetCikisi => "Zimmet Çıkışı",
        StokIslemTuru.ZimmetIadesi => "Zimmet İadesi",
        StokIslemTuru.HurdaDusum => "Hurda/Düşüm",
        StokIslemTuru.Kayip => "Kayıp",
        StokIslemTuru.BakimCikisi => "Bakım Çıkışı",
        StokIslemTuru.BakimDonusu => "Bakım Dönüşü",
        StokIslemTuru.Duzeltme => "Düzeltme",
        StokIslemTuru.AcilisGirisi => "Açılış Girişi",
        _ => t.ToString()
    };
}

// ═════════════════════════════════════════════════════════════
// SAYIM
// ═════════════════════════════════════════════════════════════

public enum SayimDurumu { Acik, DevamEdiyor, OnayBekliyor, Tamamlandi, Iptal }

public class SayimKaydi
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SayimNo { get; set; } = "";
    public int Yil { get; set; } = DateTime.UtcNow.Year;
    public string KurumId { get; set; } = "";
    public string DepoId { get; set; } = "";
    public string Baslik { get; set; } = "";
    public DateTime BaslangicTarihi { get; set; } = DateTime.UtcNow;
    public DateTime? BitisTarihi { get; set; }
    public SayimDurumu Durum { get; set; } = SayimDurumu.Acik;
    public string OlusturanKullaniciId { get; set; } = "";
    public List<KomisyonUyesi> KomisyonUyeleri { get; set; } = new();
    public List<SayimKalemi> Kalemler { get; set; } = new();
    public List<OnayKaydi> OnayGecmisi { get; set; } = new();
    public string? TutanakNotu { get; set; }
}

public class SayimKalemi
{
    public string TasinirTanimId { get; set; } = "";
    public int KaydiMiktar { get; set; }     // sistemdeki
    public int FiiliMiktar { get; set; }     // sayımda bulunan
    public int Fark => FiiliMiktar - KaydiMiktar;
    public int HasarliMiktar { get; set; }
    public bool YeriDegismis { get; set; }
    public string Aciklama { get; set; } = "";
}

// ═════════════════════════════════════════════════════════════
// DEVİR
// ═════════════════════════════════════════════════════════════

public enum DevirTuru { KurumIci, KurumlarArasi, DepodanDepoya, HarcamaBirimi }
public enum DevirDurumu { Taslak, OnayBekliyor, AlanOnayiBekliyor, Tamamlandi, Reddedildi }

public class DevirKaydi
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string DevirNo { get; set; } = "";
    public DevirTuru Tur { get; set; }
    public string KaynakKurumId { get; set; } = "";
    public string KaynakDepoId { get; set; } = "";
    public string HedefKurumId { get; set; } = "";
    public string HedefDepoId { get; set; } = "";
    public string HedefHarBirimi { get; set; } = "";
    public DateTime DevirTarihi { get; set; } = DateTime.UtcNow;
    public DevirDurumu Durum { get; set; } = DevirDurumu.Taslak;
    public string OlusturanKullaniciId { get; set; } = "";
    public string Gerekce { get; set; } = "";
    public List<DevirKalemi> Kalemler { get; set; } = new();
    public List<OnayKaydi> OnayGecmisi { get; set; } = new();
}

public class DevirKalemi
{
    public string? TasinirKayitId { get; set; }
    public string TasinirTanimId { get; set; } = "";
    public int Miktar { get; set; }
    public string SeriNo { get; set; } = "";
    public decimal BirimMaliyet { get; set; }
}

// ═════════════════════════════════════════════════════════════
// AUDIT LOG — kullanıcıya gösterilebilen işlem geçmişi
// ═════════════════════════════════════════════════════════════

public class AuditLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Tarih { get; set; } = DateTime.UtcNow;
    public string KullaniciId { get; set; } = "";
    public string KullaniciAdi { get; set; } = "";
    public string Rol { get; set; } = "";
    public string Modul { get; set; } = "";       // Talep, İhale, Depo, Zimmet ...
    public string Islem { get; set; } = "";        // Oluşturma, Güncelleme, Onay, Red, Silme
    public string KayitTur { get; set; } = "";
    public string KayitId { get; set; } = "";
    public string? OncekiDeger { get; set; }
    public string? YeniDeger { get; set; }
    public string Aciklama { get; set; } = "";
    public string? Ip { get; set; }
}
