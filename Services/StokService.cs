using ATOM.Models.Accounts;
using ATOM.Models.Domain;

namespace ATOM.Services;

/// <summary>Stok değişimlerini belgeli ve negatif-stok korumalı yapan tek kapı.</summary>
public interface IStokService
{
    /// <summary>Stoğa giriş yapar, hareket kaydı üretir.</summary>
    Task GirisYapAsync(StokHareketIstegi istek);

    /// <summary>Stoktan çıkış yapar. Yetersiz stokta StokYetersizException fırlatır.</summary>
    Task CikisYapAsync(StokHareketIstegi istek);

    /// <summary>Mevcut stok miktarını döner.</summary>
    Task<int> MevcutStokAsync(string depoId, string tasinirTanimId);

    /// <summary>Kritik eşik altındaki kalemler için bildirim üretir.</summary>
    Task KritikStokKontrolAsync(string depoId, string tasinirTanimId);
}

public class StokHareketIstegi
{
    public string DepoId { get; set; } = "";
    public string TasinirTanimId { get; set; } = "";
    public int Miktar { get; set; }
    public decimal BirimMaliyet { get; set; }
    public StokIslemTuru IslemTuru { get; set; }
    public string KaynakBelgeTur { get; set; } = "";
    public string KaynakBelgeId { get; set; } = "";
    public string KaynakBelgeNo { get; set; } = "";
    public string KullaniciId { get; set; } = "";
    public string KullaniciAdi { get; set; } = "";
    public string? TasinirKayitId { get; set; }
    public string Aciklama { get; set; } = "";
}

public class StokYetersizException : Exception
{
    public StokYetersizException(string mesaj) : base(mesaj) { }
}

public class StokService : IStokService
{
    private readonly IAtomDataService _data;
    private readonly IBildirimService _bildirim;

    public StokService(IAtomDataService data, IBildirimService bildirim)
    {
        _data = data;
        _bildirim = bildirim;
    }

    public async Task<int> MevcutStokAsync(string depoId, string tasinirTanimId)
    {
        var depo = await _data.DepoGetirAsync(depoId);
        return depo?.Stoklar.FirstOrDefault(s => s.TasinirTanimId == tasinirTanimId)?.Miktar ?? 0;
    }

    public async Task GirisYapAsync(StokHareketIstegi istek)
    {
        if (istek.Miktar <= 0) return;
        await _data.StokGuncelleAsync(istek.DepoId, istek.TasinirTanimId, istek.Miktar, istek.BirimMaliyet);
        var kalan = await MevcutStokAsync(istek.DepoId, istek.TasinirTanimId);
        await HareketKaydet(istek, giris: istek.Miktar, cikis: 0, kalan);
    }

    public async Task CikisYapAsync(StokHareketIstegi istek)
    {
        if (istek.Miktar <= 0) return;
        var mevcut = await MevcutStokAsync(istek.DepoId, istek.TasinirTanimId);
        if (mevcut < istek.Miktar)
        {
            var tanim = await _data.TasinirTanimGetirAsync(istek.TasinirTanimId);
            throw new StokYetersizException(
                $"Yetersiz stok: '{tanim?.Ad ?? istek.TasinirTanimId}' için mevcut {mevcut}, istenen {istek.Miktar}.");
        }

        await _data.StokGuncelleAsync(istek.DepoId, istek.TasinirTanimId, -istek.Miktar, istek.BirimMaliyet);
        var kalan = await MevcutStokAsync(istek.DepoId, istek.TasinirTanimId);
        await HareketKaydet(istek, giris: 0, cikis: istek.Miktar, kalan);
        await KritikStokKontrolAsync(istek.DepoId, istek.TasinirTanimId);
    }

    public async Task KritikStokKontrolAsync(string depoId, string tasinirTanimId)
    {
        var depo = await _data.DepoGetirAsync(depoId);
        var stok = depo?.Stoklar.FirstOrDefault(s => s.TasinirTanimId == tasinirTanimId);
        if (depo == null || stok == null || stok.MinEsik <= 0) return;
        if (stok.Miktar > stok.MinEsik) return;

        var tanim = await _data.TasinirTanimGetirAsync(tasinirTanimId);
        await _bildirim.KurumaBildirAsync(depo.KurumId,
            "Kritik Stok Uyarısı",
            $"{depo.Ad} deposunda '{tanim?.Ad}' kritik eşiğin altına düştü (kalan {stok.Miktar}, eşik {stok.MinEsik}).",
            BildirimTur.Uyari, $"/depo/Home/Detay/{depo.Id}", depo.Id, "Depo", "Yüksek",
            new[] { AtomRoller.MerkezDepoSorumlusu, AtomRoller.IlDepoSorumlusu, AtomRoller.IlMuduru });
    }

    private async Task HareketKaydet(StokHareketIstegi istek, int giris, int cikis, int kalan)
    {
        await _data.StokHareketKaydetAsync(new StokHareket
        {
            HareketNo = await _data.YeniNumaraUretAsync("SH"),
            DepoId = istek.DepoId,
            TasinirTanimId = istek.TasinirTanimId,
            TasinirKayitId = istek.TasinirKayitId,
            IslemTuru = istek.IslemTuru,
            GirisMiktar = giris,
            CikisMiktar = cikis,
            KalanMiktar = kalan,
            BirimMaliyet = istek.BirimMaliyet,
            ToplamTutar = istek.BirimMaliyet * (giris + cikis),
            KaynakBelgeTur = istek.KaynakBelgeTur,
            KaynakBelgeId = istek.KaynakBelgeId,
            KaynakBelgeNo = istek.KaynakBelgeNo,
            KullaniciId = istek.KullaniciId,
            KullaniciAdi = istek.KullaniciAdi,
            Aciklama = istek.Aciklama
        });
    }
}
