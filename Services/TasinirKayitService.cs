using ATOM.Models.Domain;

namespace ATOM.Services;

public interface ITasinirKayitService
{
    /// <summary>Barkod benzersiz mi?</summary>
    Task<bool> BarkodBenzersizMiAsync(string barkod, string? haricId = null);
    Task<bool> SicilBenzersizMiAsync(string sicilNo, string? haricId = null);
    Task<bool> SeriBenzersizMiAsync(string seriNo, string marka, string model, string? haricId = null);

    /// <summary>Mal kabulden onaylanan demirbaş kalemleri için tekil TasinirKayit üretir.</summary>
    Task<int> MalKabuldenUretAsync(MalKabul mk, string kullaniciId, string kullaniciAdi);

    /// <summary>Tekil taşınırın durumunu değiştirir ve hareket geçmişine yazar.</summary>
    Task DurumDegistirAsync(string tasinirKayitId, TasinirKayitDurumu yeniDurum,
        string islemTuru, string aciklama, string kullaniciId, string kullaniciAdi,
        string? zimmetId = null, string? depoId = null);

    Task<string> YeniBarkodUretAsync(string ilKodu);
    Task<string> YeniSicilUretAsync(string ilKodu);
}

public class TasinirKayitService : ITasinirKayitService
{
    private readonly IAtomDataService _data;
    public TasinirKayitService(IAtomDataService data) => _data = data;

    public async Task<bool> BarkodBenzersizMiAsync(string barkod, string? haricId = null)
    {
        if (string.IsNullOrWhiteSpace(barkod)) return true;
        var hepsi = await _data.TasinirKayitlariGetirAsync();
        return !hepsi.Any(k => k.Id != haricId && k.BarKod.Equals(barkod, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> SicilBenzersizMiAsync(string sicilNo, string? haricId = null)
    {
        if (string.IsNullOrWhiteSpace(sicilNo)) return true;
        var hepsi = await _data.TasinirKayitlariGetirAsync();
        return !hepsi.Any(k => k.Id != haricId && k.SicilNo.Equals(sicilNo, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> SeriBenzersizMiAsync(string seriNo, string marka, string model, string? haricId = null)
    {
        if (string.IsNullOrWhiteSpace(seriNo)) return true;
        var hepsi = await _data.TasinirKayitlariGetirAsync();
        return !hepsi.Any(k => k.Id != haricId
            && k.SeriNo.Equals(seriNo, StringComparison.OrdinalIgnoreCase)
            && k.MarkaAdi.Equals(marka, StringComparison.OrdinalIgnoreCase)
            && k.Modeli.Equals(model, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<int> MalKabuldenUretAsync(MalKabul mk, string kullaniciId, string kullaniciAdi)
    {
        var depo = await _data.DepoGetirAsync(mk.DepoId);
        var kurum = depo != null ? await _data.KurumGetirAsync(depo.KurumId) : null;
        var tanimlar = await _data.TasinirTanimlariGetirAsync();
        var yeniKayitlar = new List<TasinirKayit>();

        foreach (var kalem in mk.Kalemler.Where(k => k.KabulEdilen > 0))
        {
            var tanim = tanimlar.FirstOrDefault(t => t.Id == kalem.TasinirTanimId);
            // Sadece demirbaş için tekil kayıt üret (sarf malzeme sadece stoğa girer)
            bool demirbas = kalem.DemirbasMi || (tanim?.DemirbasMi ?? false);
            if (!demirbas) continue;

            for (int i = 0; i < kalem.KabulEdilen; i++)
            {
                var ilKodu = kurum?.Kod?.Split('-').FirstOrDefault() ?? "00";
                var seri = i < kalem.SeriNoListesi.Count ? kalem.SeriNoListesi[i] : kalem.SeriNo;
                var barkod = i < kalem.BarkodListesi.Count ? kalem.BarkodListesi[i] : await YeniBarkodUretAsync(ilKodu);

                yeniKayitlar.Add(new TasinirKayit
                {
                    BarKod = barkod,
                    SicilNo = await YeniSicilUretAsync(ilKodu),
                    SeriNo = seri,
                    Cinsi = tanim?.Ad ?? "",
                    Aciklama = tanim?.Ad ?? "",
                    MarkaAdi = kalem.Marka,
                    Modeli = kalem.Model,
                    OlcuAdi = tanim?.Birim ?? "Adet",
                    BirimFiyat = kalem.BirimFiyat,
                    FisNo = mk.TifNo,
                    FisIlkDurum = "Giriş - Satın Alma",
                    FisSonDurum = "Ambarda",
                    Tarih = mk.TeslimTarihi,
                    AmbarAdi = depo?.Ad ?? "",
                    KurumGirisIslemi = "Satın Alma / Mal Kabul",
                    KurumGirisTarihi = mk.TeslimTarihi,
                    IlkGirisTarihi = mk.TeslimTarihi,
                    IlAdi = kurum?.Il ?? "",
                    IlKodu = ilKodu,
                    TasinirTanimId = kalem.TasinirTanimId,
                    KurumId = depo?.KurumId,
                    DepoId = mk.DepoId,
                    Durum = TasinirKayitDurumu.Ambarda,
                    HareketGecmisi = new List<TasinirHareket>
                    {
                        new() { IslemTuru = "Mal Kabul Girişi",
                                Aciklama = $"{mk.MalKabulNo} no'lu mal kabulden kayda alındı.",
                                KullaniciId = kullaniciId, KullaniciAdi = kullaniciAdi,
                                YeniDurum = "Ambarda" }
                    }
                });
            }
        }

        if (yeniKayitlar.Count > 0)
            await _data.TasinirKayitlariTopluKaydetAsync(yeniKayitlar);

        return yeniKayitlar.Count;
    }

    public async Task DurumDegistirAsync(string tasinirKayitId, TasinirKayitDurumu yeniDurum,
        string islemTuru, string aciklama, string kullaniciId, string kullaniciAdi,
        string? zimmetId = null, string? depoId = null)
    {
        var kayit = await _data.TasinirKayitGetirAsync(tasinirKayitId);
        if (kayit == null) return;

        var oncekiDurum = kayit.Durum;
        kayit.Durum = yeniDurum;
        kayit.GuncellemeTarihi = DateTime.UtcNow;
        if (zimmetId != null) kayit.ZimmetId = zimmetId == "" ? null : zimmetId;
        if (depoId != null) kayit.DepoId = depoId;
        kayit.FisSonDurum = yeniDurum.ToString();

        kayit.HareketGecmisi.Add(new TasinirHareket
        {
            IslemTuru = islemTuru,
            Aciklama = aciklama,
            KullaniciId = kullaniciId,
            KullaniciAdi = kullaniciAdi,
            OncekiDurum = oncekiDurum.ToString(),
            YeniDurum = yeniDurum.ToString()
        });

        await _data.TasinirKayitKaydetAsync(kayit);
    }

    public async Task<string> YeniBarkodUretAsync(string ilKodu)
    {
        var no = await _data.YeniNumaraUretAsync($"BK{ilKodu}");
        return no.Replace("-", "");
    }

    public async Task<string> YeniSicilUretAsync(string ilKodu)
    {
        return await _data.YeniNumaraUretAsync($"253.{ilKodu}");
    }
}
