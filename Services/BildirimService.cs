using ATOM.Models.Accounts;
using ATOM.Models.Domain;

namespace ATOM.Services;

public interface IBildirimService
{
    Task KullaniciyaBildirAsync(string kullaniciId, string baslik, string mesaj,
        BildirimTur tur = BildirimTur.Bilgi, string? link = null,
        string? kaynakId = null, string? kaynakTur = null, string oncelik = "Normal");

    /// <summary>Bir kuruma bağlı, belirtilen roldeki tüm kullanıcılara bildirir.</summary>
    Task KurumaBildirAsync(string kurumId, string baslik, string mesaj,
        BildirimTur tur, string? link, string? kaynakId, string? kaynakTur, string oncelik,
        IEnumerable<string> roller);

    /// <summary>Bakanlık merkez rollerine bildirir.</summary>
    Task BakanligaBildirAsync(string baslik, string mesaj, BildirimTur tur,
        string? link, string? kaynakId, string? kaynakTur, string oncelik = "Normal");
}

public class BildirimService : IBildirimService
{
    private readonly IAtomDataService _data;
    public BildirimService(IAtomDataService data) => _data = data;

    public async Task KullaniciyaBildirAsync(string kullaniciId, string baslik, string mesaj,
        BildirimTur tur = BildirimTur.Bilgi, string? link = null,
        string? kaynakId = null, string? kaynakTur = null, string oncelik = "Normal")
    {
        if (string.IsNullOrEmpty(kullaniciId)) return;
        await _data.BildirimKaydetAsync(new Bildirim
        {
            AliciKullaniciId = kullaniciId,
            Baslik = baslik, Mesaj = mesaj, Tur = tur,
            LinkUrl = link, KaynakId = kaynakId, KaynakTur = kaynakTur, Oncelik = oncelik
        });
    }

    public async Task KurumaBildirAsync(string kurumId, string baslik, string mesaj,
        BildirimTur tur, string? link, string? kaynakId, string? kaynakTur, string oncelik,
        IEnumerable<string> roller)
    {
        var rolSet = roller.ToHashSet();
        var kullanicilar = await _data.KullanicilariGetirAsync();
        var hedefler = kullanicilar.Where(k => k.AktifMi && k.KurumId == kurumId && rolSet.Contains(k.Rol));
        foreach (var k in hedefler)
            await KullaniciyaBildirAsync(k.Id, baslik, mesaj, tur, link, kaynakId, kaynakTur, oncelik);
    }

    public async Task BakanligaBildirAsync(string baslik, string mesaj, BildirimTur tur,
        string? link, string? kaynakId, string? kaynakTur, string oncelik = "Normal")
    {
        var kullanicilar = await _data.KullanicilariGetirAsync();
        var hedefler = kullanicilar.Where(k => k.AktifMi && AtomRoller.BakanlikRolleri.Contains(k.Rol));
        foreach (var k in hedefler)
            await KullaniciyaBildirAsync(k.Id, baslik, mesaj, tur, link, kaynakId, kaynakTur, oncelik);
    }
}
