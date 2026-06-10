using System.ComponentModel.DataAnnotations;

namespace ATOM.Models.Domain;

// ─────────────────────────────────────────────────────────────
// ENUM'LAR
// ─────────────────────────────────────────────────────────────

public enum KurumTur { Bakanlik, IlMudurlugu, Birim }
public enum TasinirKategori
{
    Kirtasiye, BilisimDonanim, BilisimYazilim, MobilYa,
    ElektrikliEv, Arac, TibbiBekipman, GuvenlikSistemi, Diger
}
public enum TalepDurumu
{
    Taslak, GonderildiIlOnay, IlOnaylandi, BakanlikAlindi,
    BakanlikInceliyor, KarsilandiKismi, KarsilandiTamamen, Reddedildi
}
public enum IhaleDurumu
{
    Hazirlaniyor, IlanEdildi, TeklifAliniyor, DegerlendiriliYor,
    Sonuclandi, Iptal, TamamlandiTeslimBekleniyor, KapandiTamamlandi
}
public enum TeklifDurumu { Gonderildi, Inceleniyor, Kabul, Red }
public enum SevkDurumu { Hazirlaniyor, Yolda, TeslimEdildi, Reddedildi }
public enum ZimmetDurumu { Aktif, Iade, Kayip, Hurda }
public enum BakimDurumu { Acik, DevamEdiyor, Tamamlandi, Iptal }
public enum OnayDurumu { Bekliyor, Onaylandi, Reddedildi }
public enum HurdaDurumu { Talep, Komisyon, Onaylandi, Imha }
public enum CinsiyetTur { Erkek, Kadin, Belirtilmemis }
public enum BildirimTur { Bilgi, Uyari, Hata, Basari }

// ─────────────────────────────────────────────────────────────
// KURUM HİYERARŞİSİ
// ─────────────────────────────────────────────────────────────

public class Kurum
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Ad { get; set; } = "";
    public string Kod { get; set; } = "";           // TR-06-ANKARAMEM
    public KurumTur Tur { get; set; }
    public string? UstKurumId { get; set; }
    public string Il { get; set; } = "";             // Boş = Merkez Bakanlık
    public string Ilce { get; set; } = "";
    public string Adres { get; set; } = "";
    public string Telefon { get; set; } = "";
    public string Email { get; set; } = "";
    public bool AktifMi { get; set; } = true;
    public DateTime OlusturmaTarihi { get; set; } = DateTime.UtcNow;
}

// ─────────────────────────────────────────────────────────────
// TAŞINIR KATEGORİ & TANIM
// ─────────────────────────────────────────────────────────────

public class TasinirTanim
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Kod { get; set; } = "";            // 795.01.01.01
    public string Ad { get; set; } = "";
    public string Aciklama { get; set; } = "";
    public TasinirKategori Kategori { get; set; }
    public string Birim { get; set; } = "Adet";      // Adet, Kg, Lt, M2 ...
    public bool AktifMi { get; set; } = true;
    public List<string> EtiketListesi { get; set; } = new();
}

// ─────────────────────────────────────────────────────────────
// DEPO
// ─────────────────────────────────────────────────────────────

public class Depo
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Ad { get; set; } = "";
    public string Kod { get; set; } = "";
    public string KurumId { get; set; } = "";
    public bool MerkezDepoMu { get; set; }
    public string SorumluId { get; set; } = "";
    public string Adres { get; set; } = "";
    public double KapasiteM2 { get; set; }
    public bool AktifMi { get; set; } = true;
    public DateTime OlusturmaTarihi { get; set; } = DateTime.UtcNow;
    public List<DepoStok> Stoklar { get; set; } = new();
}

public class DepoStok
{
    public string TasinirTanimId { get; set; } = "";
    public int Miktar { get; set; }
    public int MinEsik { get; set; }       // uyarı eşiği
    public DateTime SonGuncelleme { get; set; } = DateTime.UtcNow;
    public decimal BirimMaliyet { get; set; }
}

// ─────────────────────────────────────────────────────────────
// İHTİYAÇ TALEBİ
// ─────────────────────────────────────────────────────────────

public class IhtiyacTalebi
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TalepNo { get; set; } = "";        // T-2026-001234
    public string TalepciKurumId { get; set; } = "";
    public string TalepciKullaniciId { get; set; } = "";
    public DateTime TalepTarihi { get; set; } = DateTime.UtcNow;
    public string GerekceAciklama { get; set; } = "";
    public string OncelikSeviyesi { get; set; } = "Normal"; // Acil, Yüksek, Normal, Düşük
    public TalepDurumu Durum { get; set; } = TalepDurumu.Taslak;
    public List<TalepKalemi> Kalemler { get; set; } = new();
    public List<OnayKaydi> OnayGecmisi { get; set; } = new();
    public List<string> EkDosyalar { get; set; } = new();
    public string? BaglantiliIhaleId { get; set; }
    public string? RedGerekce { get; set; }
    public DateTime? BakanlikAlinmaTarihi { get; set; }
    public DateTime? KapanmaTarihi { get; set; }
}

public class TalepKalemi
{
    public string TasinirTanimId { get; set; } = "";
    public int TalepMiktari { get; set; }
    public int? KarsilananMiktar { get; set; }
    public string Aciklama { get; set; } = "";
    public string TeknikOzellik { get; set; } = "";
}

// ─────────────────────────────────────────────────────────────
// İHALE
// ─────────────────────────────────────────────────────────────

public class Ihale
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string IhaleNo { get; set; } = "";        // IH-2026-0042
    public string Baslik { get; set; } = "";
    public string Aciklama { get; set; } = "";
    public IhaleDurumu Durum { get; set; } = IhaleDurumu.Hazirlaniyor;
    public DateTime OlusturmaTarihi { get; set; } = DateTime.UtcNow;
    public DateTime? IlanTarihi { get; set; }
    public DateTime? TeklifSonTarihi { get; set; }
    public DateTime? SonuclanmaTarihi { get; set; }
    public string OlusturanKullaniciId { get; set; } = "";
    public string TeknikSartname { get; set; } = "";
    public decimal TahminiButce { get; set; }
    public List<IhaleKalemi> Kalemler { get; set; } = new();
    public List<string> KaynaklananTalepIds { get; set; } = new();
    public List<IhaleTeklif> Teklifler { get; set; } = new();
    public string? KazananFirmaId { get; set; }
    public string? KazananTeklifId { get; set; }
    public List<OnayKaydi> OnayGecmisi { get; set; } = new();
    public string? SozlesmeDosyasi { get; set; }
    public string? IptalGerekce { get; set; }
}

public class IhaleKalemi
{
    public string TasinirTanimId { get; set; } = "";
    public int Miktar { get; set; }
    public string TeknikOzellik { get; set; } = "";
    public string Aciklama { get; set; } = "";
}

public class IhaleTeklif
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FirmaId { get; set; } = "";
    public DateTime VerilmeTarihi { get; set; } = DateTime.UtcNow;
    public decimal ToplamTutar { get; set; }
    public TeklifDurumu Durum { get; set; } = TeklifDurumu.Gonderildi;
    public string Aciklama { get; set; } = "";
    public List<TeklifKalemi> Kalemler { get; set; } = new();
    public string? DegerlendirmeNotu { get; set; }
}

public class TeklifKalemi
{
    public string TasinirTanimId { get; set; } = "";
    public int Miktar { get; set; }
    public decimal BirimFiyat { get; set; }
    public decimal ToplamFiyat => Miktar * BirimFiyat;
    public string Marka { get; set; } = "";
    public string Model { get; set; } = "";
    public int TeslimSuresiGun { get; set; }
    public string GarantiSuresi { get; set; } = "";
}

// ─────────────────────────────────────────────────────────────
// MAL KABUL (Depoya Giriş)
// ─────────────────────────────────────────────────────────────

public class MalKabul
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string MalKabulNo { get; set; } = "";
    public string IhaleId { get; set; } = "";
    public string FirmaId { get; set; } = "";
    public string DepoId { get; set; } = "";
    public DateTime TeslimTarihi { get; set; } = DateTime.UtcNow;
    public string KabulEdenKullaniciId { get; set; } = "";
    public List<MalKabulKalemi> Kalemler { get; set; } = new();
    public OnayDurumu Durum { get; set; } = OnayDurumu.Bekliyor;
    public List<OnayKaydi> OnayGecmisi { get; set; } = new();
    public string IrsaliyeNo { get; set; } = "";
    public string FaturaNo { get; set; } = "";
    public decimal FaturaTutari { get; set; }
    public string? Aciklama { get; set; }
}

public class MalKabulKalemi
{
    public string TasinirTanimId { get; set; } = "";
    public int SiparisEdilen { get; set; }
    public int TeslimEdilen { get; set; }
    public int KabulEdilen { get; set; }
    public int Reddedilen { get; set; }
    public string? RedGerekce { get; set; }
    public decimal BirimFiyat { get; set; }
    public string SeriNo { get; set; } = "";
    public string Marka { get; set; } = "";
    public string Model { get; set; } = "";
    public DateTime? GarantiBitisTarihi { get; set; }
}

// ─────────────────────────────────────────────────────────────
// SEVK (Depolar Arası / İllere Dağıtım)
// ─────────────────────────────────────────────────────────────

public class Sevk
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SevkNo { get; set; } = "";
    public string KaynakDepoId { get; set; } = "";
    public string HedefDepoId { get; set; } = "";
    public string HedefKurumId { get; set; } = "";
    public DateTime SevkTarihi { get; set; } = DateTime.UtcNow;
    public DateTime? TahminiVarisTarihi { get; set; }
    public DateTime? GercekVarisTarihi { get; set; }
    public SevkDurumu Durum { get; set; } = SevkDurumu.Hazirlaniyor;
    public string OlusturanKullaniciId { get; set; } = "";
    public string? TasimaciAdi { get; set; }
    public string? IrsaliyeNo { get; set; }
    public List<SevkKalemi> Kalemler { get; set; } = new();
    public List<OnayKaydi> OnayGecmisi { get; set; } = new();
    public string? Aciklama { get; set; }
    public string? KaynaklananTalepId { get; set; }
}

public class SevkKalemi
{
    public string TasinirTanimId { get; set; } = "";
    public int Miktar { get; set; }
    public int? TeslimAlinan { get; set; }
}

// ─────────────────────────────────────────────────────────────
// ZİMMET (Personele Dağıtım)
// ─────────────────────────────────────────────────────────────

public class Zimmet
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ZimmetNo { get; set; } = "";
    public string DepoId { get; set; } = "";
    public string PersonelId { get; set; } = "";
    public string VerenKullaniciId { get; set; } = "";
    public DateTime ZimmetTarihi { get; set; } = DateTime.UtcNow;
    public ZimmetDurumu Durum { get; set; } = ZimmetDurumu.Aktif;
    public List<ZimmetKalemi> Kalemler { get; set; } = new();
    public string? IadeTarihi { get; set; }
    public string? IadeAciklama { get; set; }
    public string? Aciklama { get; set; }
    public List<OnayKaydi> OnayGecmisi { get; set; } = new();
}

public class ZimmetKalemi
{
    public string TasinirTanimId { get; set; } = "";
    public int Miktar { get; set; }
    public string SeriNo { get; set; } = "";
    public string Marka { get; set; } = "";
    public string Model { get; set; } = "";
    public ZimmetDurumu ItemDurumu { get; set; } = ZimmetDurumu.Aktif;
}

// ─────────────────────────────────────────────────────────────
// BAKIM / ARIZAYI
// ─────────────────────────────────────────────────────────────

public class BakimKaydi
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string BakimNo { get; set; } = "";
    public string ZimmetId { get; set; } = "";
    public string PersonelId { get; set; } = "";
    public string TasinirTanimId { get; set; } = "";
    public string SeriNo { get; set; } = "";
    public DateTime ArizaBildirmeTarihi { get; set; } = DateTime.UtcNow;
    public string ArizaAciklama { get; set; } = "";
    public BakimDurumu Durum { get; set; } = BakimDurumu.Acik;
    public string? AtananTeknikId { get; set; }
    public DateTime? BaslamaTarihi { get; set; }
    public DateTime? TamamlanmaTarihi { get; set; }
    public string? YapilanIslem { get; set; }
    public decimal? BakimMaliyeti { get; set; }
    public bool GarantiKapsaminaMi { get; set; }
    public string? Aciklama { get; set; }
}

// ─────────────────────────────────────────────────────────────
// HURDA / İMHA
// ─────────────────────────────────────────────────────────────

public class HurdaKaydi
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string HurdaNo { get; set; } = "";
    public string KurumId { get; set; } = "";
    public string TalepEdenId { get; set; } = "";
    public DateTime TalepTarihi { get; set; } = DateTime.UtcNow;
    public HurdaDurumu Durum { get; set; } = HurdaDurumu.Talep;
    public List<HurdaKalemi> Kalemler { get; set; } = new();
    public string Gerekce { get; set; } = "";
    public DateTime? KomisyonTarihi { get; set; }
    public string? KomisyonKarari { get; set; }
    public DateTime? ImhaTarihi { get; set; }
    public string? ImhaSekli { get; set; }
    public List<OnayKaydi> OnayGecmisi { get; set; } = new();
}

public class HurdaKalemi
{
    public string TasinirTanimId { get; set; } = "";
    public int Miktar { get; set; }
    public string SeriNo { get; set; } = "";
    public string DurumAciklama { get; set; } = "";
}

// ─────────────────────────────────────────────────────────────
// ONAY KAYDI (tüm iş akışlarında ortak)
// ─────────────────────────────────────────────────────────────

public class OnayKaydi
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string KullaniciId { get; set; } = "";
    public string KullaniciAdi { get; set; } = "";
    public string Rol { get; set; } = "";
    public OnayDurumu Karar { get; set; }
    public string? Aciklama { get; set; }
    public DateTime Tarih { get; set; } = DateTime.UtcNow;
    public string Asama { get; set; } = "";          // "IlOnay", "BakanlikOnay" vb.
}

// ─────────────────────────────────────────────────────────────
// FİRMA (Tedarikçi)
// ─────────────────────────────────────────────────────────────

public class Firma
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Ad { get; set; } = "";
    public string VergiNo { get; set; } = "";
    public string VergiDairesi { get; set; } = "";
    public string Adres { get; set; } = "";
    public string Telefon { get; set; } = "";
    public string Email { get; set; } = "";
    public string YetkiliKisi { get; set; } = "";
    public bool AktifMi { get; set; } = true;
    public DateTime KayitTarihi { get; set; } = DateTime.UtcNow;
    public double PuanOrtalama { get; set; }
    public int TamamlananIhale { get; set; }
    public List<string> KayitliKullaniciIds { get; set; } = new();
}

// ─────────────────────────────────────────────────────────────
// TAŞINIR KAYIT (TKYS / KBS resmi alanları)
// Her fiziksel demirbaşın tekil kaydı — sicil, barkod, seri vb.
// ─────────────────────────────────────────────────────────────

public class TasinirKayit
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    // ── Tanımlama ──────────────────────────────────────────────
    public string BarKod { get; set; } = "";              // bar_kod
    public string Aciklama { get; set; } = "";            // aciklama
    public string Cinsi { get; set; } = "";               // cinsi
    public string EkOzellik { get; set; } = "";           // ekOzellik
    public string MarkaAdi { get; set; } = "";            // markaAdi
    public string Modeli { get; set; } = "";              // modeli
    public string OlcuAdi { get; set; } = "Adet";         // olcuAdi

    // ── Sicil / Seri ──────────────────────────────────────────
    public string SicilNo { get; set; } = "";             // sicil_no
    public string EskiSicilNo { get; set; } = "";         // eski_sicil_no
    public string SeriNo { get; set; } = "";              // seri_no

    // ── Mali ──────────────────────────────────────────────────
    public decimal BirimFiyat { get; set; }               // birim_fiyat

    // ── Fiş Bilgileri ─────────────────────────────────────────
    public string FisNo { get; set; } = "";               // fis_no
    public string FisIlkDurum { get; set; } = "";         // fis_ilk_durum
    public string FisSonDurum { get; set; } = "";         // fis_son_durum
    public DateTime? Tarih { get; set; }                  // tarih

    // ── Yer / Zimmet ──────────────────────────────────────────
    public string VerildigiYerBirim { get; set; } = "";   // verildigi_yer_birim
    public string TcNumarasi { get; set; } = "";          // tc_numarasi
    public string AmbarAdi { get; set; } = "";            // ambar_adi

    // ── Kurum Giriş ───────────────────────────────────────────
    public string KurumGirisIslemi { get; set; } = "";    // kurum_giris_islemi
    public DateTime? KurumGirisTarihi { get; set; }       // kurum_giris_tarihi
    public DateTime? IlkGirisTarihi { get; set; }         // ilk_giris_tarihi
    public string LimitDurumu { get; set; } = "";         // limit_durumu

    // ── Harcama Birimi ────────────────────────────────────────
    public string HarBirimiAdi { get; set; } = "";        // har_birimi_adi
    public string HarBirimiKodu { get; set; } = "";       // har_birimi_kodu

    // ── İl / Sayım ────────────────────────────────────────────
    public string IlAdi { get; set; } = "";               // iladi
    public string IlKodu { get; set; } = "";              // ilkoduv
    public string SayKod { get; set; } = "";              // saykod
    public string SayAdi { get; set; } = "";              // sayadi

    // ── Proje ─────────────────────────────────────────────────
    public string ProjeNumarasi { get; set; } = "";       // projeNumarasi

    // ── İlişki & İzlenebilirlik (ATOM iç bağları) ─────────────
    public string? TasinirTanimId { get; set; }           // katalog tanımı
    public string? DepoId { get; set; }
    public string? KurumId { get; set; }
    public string? ZimmetId { get; set; }
    public TasinirKayitDurumu Durum { get; set; } = TasinirKayitDurumu.Ambarda;
    public DateTime OlusturmaTarihi { get; set; } = DateTime.UtcNow;
    public DateTime GuncellemeTarihi { get; set; } = DateTime.UtcNow;
    public List<TasinirHareket> HareketGecmisi { get; set; } = new();
}

public enum TasinirKayitDurumu
{
    Ambarda, Zimmetli, Bakimda, Sevkte, Hurda, Dusum, Devir
}

public class TasinirHareket
{
    public DateTime Tarih { get; set; } = DateTime.UtcNow;
    public string IslemTuru { get; set; } = "";   // Giriş, Zimmet, İade, Sevk, Bakım, Hurda
    public string Aciklama { get; set; } = "";
    public string KullaniciId { get; set; } = "";
    public string KullaniciAdi { get; set; } = "";
    public string? OncekiDurum { get; set; }
    public string? YeniDurum { get; set; }
}

// ─────────────────────────────────────────────────────────────
// BİLDİRİM
// ─────────────────────────────────────────────────────────────

public class Bildirim
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string AliciKullaniciId { get; set; } = "";
    public string Baslik { get; set; } = "";
    public string Mesaj { get; set; } = "";
    public BildirimTur Tur { get; set; } = BildirimTur.Bilgi;
    public bool OkunduMu { get; set; }
    public DateTime Tarih { get; set; } = DateTime.UtcNow;
    public string? LinkUrl { get; set; }
    public string? KaynakId { get; set; }
    public string? KaynakTur { get; set; }
}
