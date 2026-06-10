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

    // ── Bildirim ──────────────────────────────────────────────
    Task<List<Bildirim>> BildirimleriGetirAsync(string kullaniciId);
    Task BildirimKaydetAsync(Bildirim bildirim);
    Task BildirimOkunduIsaretle(string bildirimId);

    // ── Numara Üreteci ────────────────────────────────────────
    Task<string> YeniNumaraUretAsync(string prefix);
}
