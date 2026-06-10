using System.Security.Claims;
using ATOM.Models.Accounts;
using ATOM.Models.Domain;
using ATOM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATOM.Areas.Zimmet.Controllers;

[Area("Zimmet")]
[Authorize]
public class HomeController : Controller
{
    private readonly IAtomDataService _svc;
    public HomeController(IAtomDataService svc) => _svc = svc;

    private string KullaniciId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private string Rol => User.FindFirstValue(ClaimTypes.Role)!;
    private string KurumId => User.FindFirstValue("KurumId")!;

    public async Task<IActionResult> Index(string? ara = null)
    {
        var zimmetler = await _svc.ZimmetleriGetirAsync();
        var kullanicilar = await _svc.KullanicilariGetirAsync();
        var depolar = await _svc.DepolariGetirAsync();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();

        if (Rol == AtomRoller.Personel)
            zimmetler = zimmetler.Where(z => z.PersonelId == KullaniciId).ToList();
        else if (!AtomRoller.BakanlikRolleri.Contains(Rol))
        {
            var kurumDepolar = depolar.Where(d => d.KurumId == KurumId).Select(d => d.Id).ToHashSet();
            zimmetler = zimmetler.Where(z => kurumDepolar.Contains(z.DepoId)).ToList();
        }

        if (!string.IsNullOrEmpty(ara))
            zimmetler = zimmetler.Where(z => z.ZimmetNo.Contains(ara, StringComparison.OrdinalIgnoreCase)).ToList();

        ViewBag.Kullanicilar = kullanicilar.ToDictionary(k => k.Id, k => k.AdSoyad);
        ViewBag.Depolar = depolar.ToDictionary(d => d.Id, d => d.Ad);
        ViewBag.Tanimlar = tanimlar.ToDictionary(t => t.Id, t => t.Ad);
        ViewBag.Ara = ara;
        return View(zimmetler.OrderByDescending(z => z.ZimmetTarihi).ToList());
    }

    [HttpGet]
    [Authorize(Roles = $"{AtomRoller.IlDepoSorumlusu},{AtomRoller.MerkezDepoSorumlusu},{AtomRoller.IlMuduru},{AtomRoller.SistemAdmin}")]
    public async Task<IActionResult> Yeni()
    {
        var depolar = await _svc.DepolariGetirAsync();
        var kullanicilar = await _svc.KullanicilariGetirAsync();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();

        if (!AtomRoller.BakanlikRolleri.Contains(Rol))
        {
            depolar = depolar.Where(d => d.KurumId == KurumId).ToList();
            kullanicilar = kullanicilar.Where(k => k.KurumId == KurumId).ToList();
        }

        ViewBag.Depolar = depolar;
        ViewBag.Kullanicilar = kullanicilar.Where(k => k.AktifMi).ToList();
        ViewBag.Tanimlar = tanimlar.ToDictionary(t => t.Id, t => t.Ad);
        return View(new ATOM.Models.Domain.Zimmet { DepoId = depolar.FirstOrDefault()?.Id ?? "" });
    }

    [HttpPost]
    [Authorize(Roles = $"{AtomRoller.IlDepoSorumlusu},{AtomRoller.MerkezDepoSorumlusu},{AtomRoller.IlMuduru},{AtomRoller.SistemAdmin}")]
    public async Task<IActionResult> Yeni(ATOM.Models.Domain.Zimmet zimmet, string[] tasinirIds, int[] miktarlar, string[] seriNolar, string[] markalar)
    {
        zimmet.ZimmetNo = await _svc.YeniNumaraUretAsync("ZMT");
        zimmet.VerenKullaniciId = KullaniciId;
        zimmet.ZimmetTarihi = DateTime.UtcNow;
        zimmet.Durum = ZimmetDurumu.Aktif;
        zimmet.Kalemler = new();

        for (int i = 0; i < tasinirIds.Length; i++)
        {
            if (!string.IsNullOrEmpty(tasinirIds[i]) && miktarlar[i] > 0)
            {
                zimmet.Kalemler.Add(new ZimmetKalemi
                {
                    TasinirTanimId = tasinirIds[i],
                    Miktar = miktarlar[i],
                    SeriNo = i < seriNolar.Length ? seriNolar[i] : "",
                    Marka = i < markalar.Length ? markalar[i] : ""
                });
                await _svc.StokGuncelleAsync(zimmet.DepoId, tasinirIds[i], -miktarlar[i]);
            }
        }

        zimmet.OnayGecmisi.Add(new OnayKaydi
        {
            KullaniciId = KullaniciId,
            KullaniciAdi = User.FindFirstValue("AdSoyad") ?? "",
            Rol = Rol, Karar = OnayDurumu.Onaylandi,
            Asama = "Zimmet Oluşturma"
        });

        await _svc.ZimmetKaydetAsync(zimmet);
        TempData["Basari"] = $"{zimmet.ZimmetNo} numaralı zimmet oluşturuldu.";
        return RedirectToAction(nameof(Detay), new { id = zimmet.Id });
    }

    public async Task<IActionResult> Detay(string id)
    {
        var zimmet = await _svc.ZimmetGetirAsync(id);
        if (zimmet == null) return NotFound();
        if (Rol == AtomRoller.Personel && zimmet.PersonelId != KullaniciId) return Forbid();

        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        var kullanicilar = await _svc.KullanicilariGetirAsync();
        var depolar = await _svc.DepolariGetirAsync();
        ViewBag.Tanimlar = tanimlar.ToDictionary(t => t.Id, t => t.Ad);
        ViewBag.Kullanicilar = kullanicilar.ToDictionary(k => k.Id, k => k.AdSoyad);
        ViewBag.Depolar = depolar.ToDictionary(d => d.Id, d => d.Ad);
        return View(zimmet);
    }

    [HttpPost]
    public async Task<IActionResult> Iade(string id, string aciklama)
    {
        var zimmet = await _svc.ZimmetGetirAsync(id);
        if (zimmet == null) return NotFound();
        if (zimmet.PersonelId != KullaniciId && !new[] { AtomRoller.IlMuduru, AtomRoller.IlDepoSorumlusu, AtomRoller.SistemAdmin }.Contains(Rol)) return Forbid();

        zimmet.Durum = ZimmetDurumu.Iade;
        zimmet.IadeTarihi = DateTime.UtcNow.ToString("dd.MM.yyyy");
        zimmet.IadeAciklama = aciklama;

        foreach (var kalem in zimmet.Kalemler)
        {
            kalem.ItemDurumu = ZimmetDurumu.Iade;
            await _svc.StokGuncelleAsync(zimmet.DepoId, kalem.TasinirTanimId, kalem.Miktar);
        }

        zimmet.OnayGecmisi.Add(new OnayKaydi
        {
            KullaniciId = KullaniciId,
            KullaniciAdi = User.FindFirstValue("AdSoyad") ?? "",
            Rol = Rol, Karar = OnayDurumu.Onaylandi,
            Asama = "İade", Aciklama = aciklama
        });

        await _svc.ZimmetKaydetAsync(zimmet);
        TempData["Basari"] = "Zimmet iade edildi, stok güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    // ─── Bakım Arıza ──────────────────────────────────────────
    public async Task<IActionResult> Bakim()
    {
        var bakimlar = await _svc.BakimKayitlariGetirAsync();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        var kullanicilar = await _svc.KullanicilariGetirAsync();

        ViewBag.Tanimlar = tanimlar.ToDictionary(t => t.Id, t => t.Ad);
        ViewBag.Kullanicilar = kullanicilar.ToDictionary(k => k.Id, k => k.AdSoyad);
        return View(bakimlar.OrderByDescending(b => b.ArizaBildirmeTarihi).ToList());
    }

    [HttpPost]
    public async Task<IActionResult> ArizaBildir(string zimmetId, string tasinirTanimId, string seriNo, string aciklama)
    {
        var bk = new BakimKaydi
        {
            BakimNo = await _svc.YeniNumaraUretAsync("BKM"),
            ZimmetId = zimmetId,
            PersonelId = KullaniciId,
            TasinirTanimId = tasinirTanimId,
            SeriNo = seriNo,
            ArizaBildirmeTarihi = DateTime.UtcNow,
            ArizaAciklama = aciklama,
            Durum = BakimDurumu.Acik
        };

        await _svc.BakimKaydiKaydetAsync(bk);
        TempData["Basari"] = $"{bk.BakimNo} arıza kaydı oluşturuldu.";
        return RedirectToAction(nameof(Bakim));
    }

    [HttpPost]
    [Authorize(Roles = $"{AtomRoller.Teknisyen},{AtomRoller.IlMuduru},{AtomRoller.SistemAdmin}")]
    public async Task<IActionResult> BakimTamamla(string id, string yapilanIslem, decimal maliyet, bool garantiKapsaminda)
    {
        var bk = await _svc.BakimKaydiGetirAsync(id);
        if (bk == null) return NotFound();

        bk.Durum = BakimDurumu.Tamamlandi;
        bk.AtananTeknikId = KullaniciId;
        bk.TamamlanmaTarihi = DateTime.UtcNow;
        bk.YapilanIslem = yapilanIslem;
        bk.BakimMaliyeti = maliyet;
        bk.GarantiKapsaminaMi = garantiKapsaminda;

        await _svc.BakimKaydiKaydetAsync(bk);
        TempData["Basari"] = "Bakım/Onarım kaydı tamamlandı.";
        return RedirectToAction(nameof(Bakim));
    }
}

