using System.Security.Claims;
using ATOM.Models.Accounts;
using ATOM.Models.Domain;
using ATOM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATOM.Areas.Talep.Controllers;

[Area("Talep")]
[Authorize]
public class HomeController : Controller
{
    private readonly IAtomDataService _svc;
    public HomeController(IAtomDataService svc) => _svc = svc;

    private string KullaniciId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private string Rol => User.FindFirstValue(ClaimTypes.Role)!;
    private string KurumId => User.FindFirstValue("KurumId")!;

    public async Task<IActionResult> Index(string? durum = null, string? ara = null)
    {
        var talepler = await _svc.TalepleriGetirAsync();
        var kurumlar = await _svc.KurumlariGetirAsync();

        // Rol bazlı filtre
        if (!AtomRoller.BakanlikRolleri.Contains(Rol))
            talepler = talepler.Where(t => t.TalepciKurumId == KurumId).ToList();

        if (!string.IsNullOrEmpty(durum) && Enum.TryParse<TalepDurumu>(durum, out var d))
            talepler = talepler.Where(t => t.Durum == d).ToList();

        if (!string.IsNullOrEmpty(ara))
            talepler = talepler.Where(t => t.TalepNo.Contains(ara, StringComparison.OrdinalIgnoreCase)
                || t.GerekceAciklama.Contains(ara, StringComparison.OrdinalIgnoreCase)).ToList();

        ViewBag.Kurumlar = kurumlar.ToDictionary(k => k.Id, k => k.Ad);
        ViewBag.Ara = ara;
        ViewBag.Durum = durum;
        return View(talepler.OrderByDescending(t => t.TalepTarihi).ToList());
    }

    [HttpGet]
    public async Task<IActionResult> Yeni()
    {
        ViewBag.Tanimlar = await _svc.TasinirTanimlariGetirAsync();
        return View(new IhtiyacTalebi { TalepciKurumId = KurumId, TalepciKullaniciId = KullaniciId });
    }

    [HttpPost]
    public async Task<IActionResult> Yeni(IhtiyacTalebi talep, string[] tasinirIds, int[] miktarlar, string[] aciklamalar)
    {
        talep.TalepNo = await _svc.YeniNumaraUretAsync("T");
        talep.TalepciKurumId = KurumId;
        talep.TalepciKullaniciId = KullaniciId;
        talep.TalepTarihi = DateTime.UtcNow;
        talep.Durum = TalepDurumu.Taslak;
        talep.Kalemler = new();

        for (int i = 0; i < tasinirIds.Length; i++)
        {
            if (!string.IsNullOrEmpty(tasinirIds[i]) && miktarlar[i] > 0)
                talep.Kalemler.Add(new TalepKalemi
                {
                    TasinirTanimId = tasinirIds[i],
                    TalepMiktari = miktarlar[i],
                    Aciklama = i < aciklamalar.Length ? aciklamalar[i] : ""
                });
        }

        if (talep.Kalemler.Count == 0)
        {
            ModelState.AddModelError("", "En az bir taşınır kalemi eklemelisiniz.");
            ViewBag.Tanimlar = await _svc.TasinirTanimlariGetirAsync();
            return View(talep);
        }

        await _svc.TalepKaydetAsync(talep);
        TempData["Basari"] = $"{talep.TalepNo} numaralı talep kaydedildi.";
        return RedirectToAction(nameof(Detay), new { id = talep.Id });
    }

    public async Task<IActionResult> Detay(string id)
    {
        var talep = await _svc.TalepGetirAsync(id);
        if (talep == null) return NotFound();
        if (!AtomRoller.BakanlikRolleri.Contains(Rol) && talep.TalepciKurumId != KurumId) return Forbid();

        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        var kurumlar = await _svc.KurumlariGetirAsync();
        ViewBag.Tanimlar = tanimlar.ToDictionary(t => t.Id, t => t.Ad);
        ViewBag.Kurumlar = kurumlar.ToDictionary(k => k.Id, k => k.Ad);
        return View(talep);
    }

    [HttpPost]
    public async Task<IActionResult> Gonder(string id)
    {
        var talep = await _svc.TalepGetirAsync(id);
        if (talep == null) return NotFound();
        if (talep.TalepciKullaniciId != KullaniciId && !AtomRoller.BakanlikRolleri.Contains(Rol)) return Forbid();
        if (talep.Durum != TalepDurumu.Taslak) return BadRequest("Yalnızca taslak talepler gönderilebilir.");

        talep.Durum = TalepDurumu.GonderildiIlOnay;
        talep.OnayGecmisi.Add(new OnayKaydi
        {
            KullaniciId = KullaniciId,
            KullaniciAdi = User.FindFirstValue("AdSoyad") ?? User.Identity!.Name!,
            Rol = Rol,
            Karar = OnayDurumu.Onaylandi,
            Asama = "Gönderim",
            Aciklama = "Talep onay sürecine gönderildi."
        });

        await _svc.TalepKaydetAsync(talep);
        await BildirimGonder("k-bakanlik", "Yeni İhtiyaç Talebi", $"{talep.TalepNo} numaralı talep bakanlık onayına geldi.", talep.Id, "Talep");
        TempData["Basari"] = "Talep başarıyla gönderildi.";
        return RedirectToAction(nameof(Detay), new { id });
    }

    [HttpPost]
    [Authorize(Roles = $"{AtomRoller.IlMuduru}")]
    public async Task<IActionResult> IlOnayla(string id, string aciklama)
    {
        var talep = await _svc.TalepGetirAsync(id);
        if (talep == null) return NotFound();
        if (talep.TalepciKurumId != KurumId) return Forbid();

        talep.Durum = TalepDurumu.IlOnaylandi;
        talep.OnayGecmisi.Add(new OnayKaydi
        {
            KullaniciId = KullaniciId,
            KullaniciAdi = User.FindFirstValue("AdSoyad") ?? "",
            Rol = Rol, Karar = OnayDurumu.Onaylandi,
            Asama = "İl Onayı", Aciklama = aciklama
        });

        await _svc.TalepKaydetAsync(talep);
        TempData["Basari"] = "Talep il müdürü tarafından onaylandı.";
        return RedirectToAction(nameof(Detay), new { id });
    }

    [HttpPost]
    [Authorize(Roles = $"{AtomRoller.BakanlikMerkez},{AtomRoller.SistemAdmin}")]
    public async Task<IActionResult> BakanlikOnayla(string id, string aciklama, bool onayla)
    {
        var talep = await _svc.TalepGetirAsync(id);
        if (talep == null) return NotFound();

        talep.Durum = onayla ? TalepDurumu.BakanlikInceliyor : TalepDurumu.Reddedildi;
        if (!onayla) talep.RedGerekce = aciklama;
        talep.BakanlikAlinmaTarihi ??= DateTime.UtcNow;

        talep.OnayGecmisi.Add(new OnayKaydi
        {
            KullaniciId = KullaniciId,
            KullaniciAdi = User.FindFirstValue("AdSoyad") ?? "",
            Rol = Rol, Karar = onayla ? OnayDurumu.Onaylandi : OnayDurumu.Reddedildi,
            Asama = "Bakanlık İncelemesi", Aciklama = aciklama
        });

        await _svc.TalepKaydetAsync(talep);
        TempData["Basari"] = onayla ? "Talep incelemeye alındı." : "Talep reddedildi.";
        return RedirectToAction(nameof(Detay), new { id });
    }

    private async Task BildirimGonder(string kurumId, string baslik, string mesaj, string kaynakId, string kaynakTur)
    {
        var kullanicilar = await _svc.KullanicilariGetirAsync();
        var hedefler = kullanicilar.Where(k => k.KurumId == kurumId && AtomRoller.BakanlikRolleri.Contains(k.Rol));
        foreach (var k in hedefler)
        {
            await _svc.BildirimKaydetAsync(new Bildirim
            {
                AliciKullaniciId = k.Id,
                Baslik = baslik, Mesaj = mesaj,
                Tur = BildirimTur.Bilgi,
                KaynakId = kaynakId, KaynakTur = kaynakTur
            });
        }
    }
}
