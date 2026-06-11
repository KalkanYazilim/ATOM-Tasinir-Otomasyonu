using ATOM.Models.Accounts;
using ATOM.Models.Domain;

namespace ATOM.Services;

public interface IAtomDataService
{
    // ── Kullanıcılar ──────────────────────────────────────────
    Task<List<AtomKullanici>> KullanicilariGetirAsync();
    Task<AtomKullanici?> KullaniciGetirAsync(string id);
    Task<AtomKullanici?> KullaniciAdiylaGetirAsync(string kullaniciAdi);
    Task KullaniciKaydetAsync(AtomKullanici kullanici);
    Task KullaniciSilAsync(string id);

    // ── Kurumlar ──────────────────────────────────────────────
    Task<List<Kurum>> KurumlariGetirAsync();
    Task<Kurum?> KurumGetirAsync(string id);
    Task KurumKaydetAsync(Kurum kurum);

    // ── Firma ──────────────────────────────────────────────────
    Task<List<Firma>> FirmalariGetirAsync();
    Task<Firma?> FirmaGetirAsync(string id);
    Task FirmaKaydetAsync(Firma firma);

    // ── Taşınır Tanım ─────────────────────────────────────────
    Task<List<TasinirTanim>> TasinirTanimlariGetirAsync();
    Task<TasinirTanim?> TasinirTanimGetirAsync(string id);
    Task TasinirTanimKaydetAsync(TasinirTanim tanim);

    // ── Depo ──────────────────────────────────────────────────
    Task<List<Depo>> DepolariGetirAsync();
    Task<Depo?> DepoGetirAsync(string id);
    Task DepoKaydetAsync(Depo depo);
    Task StokGuncelleAsync(string depoId, string tasinirTanimId, int miktarDelta, decimal birimMaliyet = 0);

    // ── İhtiyaç Talebi ────────────────────────────────────────
    Task<List<IhtiyacTalebi>> TalepleriGetirAsync();
    Task<IhtiyacTalebi?> TalepGetirAsync(string id);
    Task TalepKaydetAsync(IhtiyacTalebi talep);

    // ── İhale ─────────────────────────────────────────────────
    Task<List<Ihale>> IhaleleriGetirAsync();
    Task<Ihale?> IhaleGetirAsync(string id);
    Task IhaleKaydetAsync(Ihale ihale);

    // ── Mal Kabul ─────────────────────────────────────────────
    Task<List<MalKabul>> MalKabulleriGetirAsync();
    Task<MalKabul?> MalKabulGetirAsync(string id);
    Task MalKabulKaydetAsync(MalKabul mk);

    // ── Sevk ──────────────────────────────────────────────────
    Task<List<Sevk>> SevkleriGetirAsync();
    Task<Sevk?> SevkGetirAsync(string id);
    Task SevkKaydetAsync(Sevk sevk);

    // ── Zimmet ────────────────────────────────────────────────
    Task<List<Zimmet>> ZimmetleriGetirAsync();
    Task<Zimmet?> ZimmetGetirAsync(string id);
    Task ZimmetKaydetAsync(Zimmet zimmet);

    // ── Bakım ─────────────────────────────────────────────────
    Task<List<BakimKaydi>> BakimKayitlariGetirAsync();
    Task<BakimKaydi?> BakimKaydiGetirAsync(string id);
    Task BakimKaydiKaydetAsync(BakimKaydi kayit);

    // ── Hurda ─────────────────────────────────────────────────
    Task<List<HurdaKaydi>> HurdaKayitlariGetirAsync();
    Task<HurdaKaydi?> HurdaKaydiGetirAsync(string id);
    Task HurdaKaydiKaydetAsync(HurdaKaydi kayit);

    // ── Taşınır Kayıt (TKYS) ──────────────────────────────────
    Task<List<TasinirKayit>> TasinirKayitlariGetirAsync();
    Task<TasinirKayit?> TasinirKayitGetirAsync(string id);
    Task TasinirKayitKaydetAsync(TasinirKayit kayit);
    Task TasinirKayitlariTopluKaydetAsync(IEnumerable<TasinirKayit> kayitlar);
    Task TasinirKayitSilAsync(string id);

    // ── Stok Hareket ──────────────────────────────────────────
    Task<List<StokHareket>> StokHareketleriGetirAsync();
    Task StokHareketKaydetAsync(StokHareket hareket);

    // ── Personel Sarf İzleme ─────────────────────────────────
    Task<List<PersonelSarfBakiye>> PersonelSarfBakiyeleriGetirAsync();
    Task PersonelSarfBakiyeKaydetAsync(PersonelSarfBakiye bakiye);
    Task<List<PersonelSarfHareket>> PersonelSarfHareketleriGetirAsync();
    Task PersonelSarfHareketKaydetAsync(PersonelSarfHareket hareket);

    // ── Sayım ─────────────────────────────────────────────────
    Task<List<SayimKaydi>> SayimlariGetirAsync();
    Task<SayimKaydi?> SayimGetirAsync(string id);
    Task SayimKaydetAsync(SayimKaydi sayim);

    // ── Devir ─────────────────────────────────────────────────
    Task<List<DevirKaydi>> DevirleriGetirAsync();
    Task<DevirKaydi?> DevirGetirAsync(string id);
    Task DevirKaydetAsync(DevirKaydi devir);

    // ── Taşıt ─────────────────────────────────────────────────
    Task<List<Tasit>> TasitlariGetirAsync();
    Task<Tasit?> TasitGetirAsync(string id);
    Task TasitKaydetAsync(Tasit tasit);

    // ── Elektronik İmza ───────────────────────────────────────
    Task<List<ElektronikImza>> ImzalariGetirAsync();
    Task<ElektronikImza?> ImzaDogrulamaKoduylaGetirAsync(string kod);
    Task ImzaKaydetAsync(ElektronikImza imza);

    // ── Mal Giriş / Çıkış Belgeleri ───────────────────────────
    Task<List<MalGirisBelgesi>> MalGirisleriGetirAsync();
    Task<MalGirisBelgesi?> MalGirisGetirAsync(string id);
    Task MalGirisKaydetAsync(MalGirisBelgesi belge);
    Task<List<MalCikisBelgesi>> MalCikislariGetirAsync();
    Task<MalCikisBelgesi?> MalCikisGetirAsync(string id);
    Task MalCikisKaydetAsync(MalCikisBelgesi belge);

    // ── Audit Log ─────────────────────────────────────────────
    Task<List<AuditLog>> AuditLoglariGetirAsync();
    Task AuditKaydetAsync(AuditLog log);

    // ── Bildirim ──────────────────────────────────────────────
    Task<List<Bildirim>> BildirimleriGetirAsync(string kullaniciId);
    Task BildirimKaydetAsync(Bildirim bildirim);
    Task BildirimOkunduIsaretle(string bildirimId);

    // ── Numara Üreteci ────────────────────────────────────────
    Task<string> YeniNumaraUretAsync(string prefix);
}
