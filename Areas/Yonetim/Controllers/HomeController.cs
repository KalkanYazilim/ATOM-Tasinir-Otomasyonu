using System.Security.Claims;
using ATOM.Models.Accounts;
using ATOM.Models.Domain;
using ATOM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BC = BCrypt.Net.BCrypt;

namespace ATOM.Areas.Yonetim.Controllers;

[Area("Yonetim")]
[Authorize(Roles = $"{AtomRoller.SistemAdmin},{AtomRoller.BakanlikMerkez}")]
public class HomeController : Controller
{
    private readonly IAtomDataService _svc;
    private readonly IDosyaService _dosya;
    public HomeController(IAtomDataService svc, IDosyaService dosya)
    {
        _svc = svc; _dosya = dosya;
    }

    public async Task<IActionResult> Index()
    {
        var kullanicilar = await _svc.KullanicilariGetirAsync();
        var kurumlar = await _svc.KurumlariGetirAsync();
        var firmalar = await _svc.FirmalariGetirAsync();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        ViewBag.KullaniciSayisi = kullanicilar.Count;
        ViewBag.KurumSayisi = kurumlar.Count;
        ViewBag.FirmaSayisi = firmalar.Count;
        ViewBag.TanimSayisi = tanimlar.Count;
        return View();
    }

    public async Task<IActionResult> Kullanicilar()
    {
        var liste = await _svc.KullanicilariGetirAsync();
        var kurumlar = await _svc.KurumlariGetirAsync();
        ViewBag.Kurumlar = kurumlar.ToDictionary(k => k.Id, k => k.Ad);
        return View(liste.OrderBy(k => k.KullaniciAdi).ToList());
    }

    [HttpGet]
    public async Task<IActionResult> KullaniciDuzenle(string? id = null)
    {
        var kurumlar = await _svc.KurumlariGetirAsync();
        ViewBag.Kurumlar = kurumlar;
        ViewBag.Roller = AtomRoller.Tumu;
        var k = id != null ? await _svc.KullaniciGetirAsync(id) : new AtomKullanici();
        return View(k ?? new AtomKullanici());
    }

    [HttpPost]
    public async Task<IActionResult> KullaniciDuzenle(AtomKullanici model, string? yeniSifre)
    {
        if (!string.IsNullOrEmpty(yeniSifre))
            model.SifreHash = BC.HashPassword(yeniSifre);
        else
        {
            var mevcut = await _svc.KullaniciGetirAsync(model.Id);
            model.SifreHash = mevcut?.SifreHash ?? BC.HashPassword("Degistir123!");
        }

        await _svc.KullaniciKaydetAsync(model);
        TempData["Basari"] = "Kullanıcı kaydedildi.";
        return RedirectToAction(nameof(Kullanicilar));
    }

    // ─── Pasifleştirme (fiziksel silme yerine) ────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> KullaniciPasiflestir(string id)
    {
        var k = await _svc.KullaniciGetirAsync(id);
        if (k != null) { k.AktifMi = !k.AktifMi; await _svc.KullaniciKaydetAsync(k);
            TempData["Basari"] = k.AktifMi ? "Kullanıcı aktifleştirildi." : "Kullanıcı pasifleştirildi (silinmedi)."; }
        return RedirectToAction(nameof(Kullanicilar));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> KurumPasiflestir(string id)
    {
        var k = await _svc.KurumGetirAsync(id);
        if (k != null) { k.AktifMi = !k.AktifMi; await _svc.KurumKaydetAsync(k);
            TempData["Basari"] = k.AktifMi ? "Kurum aktifleştirildi." : "Kurum pasifleştirildi (silinmedi)."; }
        return RedirectToAction(nameof(Kurumlar));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FirmaPasiflestir(string id)
    {
        var f = await _svc.FirmaGetirAsync(id);
        if (f != null) { f.AktifMi = !f.AktifMi; await _svc.FirmaKaydetAsync(f);
            TempData["Basari"] = f.AktifMi ? "Firma aktifleştirildi." : "Firma pasifleştirildi (silinmedi)."; }
        return RedirectToAction(nameof(Firmalar));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TanimPasiflestir(string id)
    {
        var t = await _svc.TasinirTanimGetirAsync(id);
        if (t != null) { t.AktifMi = !t.AktifMi; await _svc.TasinirTanimKaydetAsync(t);
            TempData["Basari"] = t.AktifMi ? "Tanım aktifleştirildi." : "Tanım pasifleştirildi (silinmedi)."; }
        return RedirectToAction(nameof(TasinirTanimlar));
    }

    public async Task<IActionResult> Kurumlar()
    {
        var liste = await _svc.KurumlariGetirAsync();
        return View(liste.OrderBy(k => k.Tur).ThenBy(k => k.Ad).ToList());
    }

    [HttpGet]
    public async Task<IActionResult> KurumDuzenle(string? id = null)
    {
        var kurumlar = await _svc.KurumlariGetirAsync();
        ViewBag.Kurumlar = kurumlar;
        var k = id != null ? await _svc.KurumGetirAsync(id) : new Kurum();
        return View(k ?? new Kurum());
    }

    [HttpPost]
    public async Task<IActionResult> KurumDuzenle(Kurum model)
    {
        await _svc.KurumKaydetAsync(model);
        TempData["Basari"] = "Kurum kaydedildi.";
        return RedirectToAction(nameof(Kurumlar));
    }

    public async Task<IActionResult> Firmalar()
    {
        return View(await _svc.FirmalariGetirAsync());
    }

    [HttpGet]
    public async Task<IActionResult> FirmaDuzenle(string? id = null)
    {
        var f = id != null ? await _svc.FirmaGetirAsync(id) : new Firma();
        return View(f ?? new Firma());
    }

    [HttpPost]
    public async Task<IActionResult> FirmaDuzenle(Firma model)
    {
        await _svc.FirmaKaydetAsync(model);
        TempData["Basari"] = "Firma kaydedildi.";
        return RedirectToAction(nameof(Firmalar));
    }

    public async Task<IActionResult> AuditLog(string? modul = null)
    {
        var loglar = await _svc.AuditLoglariGetirAsync();
        if (!string.IsNullOrEmpty(modul))
            loglar = loglar.Where(l => l.Modul == modul).ToList();
        ViewBag.Moduller = (await _svc.AuditLoglariGetirAsync()).Select(l => l.Modul).Distinct().OrderBy(x => x).ToList();
        ViewBag.Modul = modul;
        return View(loglar.OrderByDescending(l => l.Tarih).Take(500).ToList());
    }

    public async Task<IActionResult> TasinirTanimlar()
    {
        return View(await _svc.TasinirTanimlariGetirAsync());
    }

    [HttpGet]
    public async Task<IActionResult> TasinirTanimDuzenle(string? id = null)
    {
        var t = id != null ? await _svc.TasinirTanimGetirAsync(id) : new TasinirTanim();
        return View(t ?? new TasinirTanim());
    }

    [HttpPost]
    public async Task<IActionResult> TasinirTanimDuzenle(TasinirTanim model, IFormFile? resim)
    {
        var resimUrl = await _dosya.ResimKaydetAsync(resim, "urun");
        if (resimUrl != null)
        {
            model.ResimUrl = resimUrl;
            model.Resimler ??= new();
            model.Resimler.Add(resimUrl);
        }
        else if (!string.IsNullOrEmpty(model.Id))
        {
            var mevcut = await _svc.TasinirTanimGetirAsync(model.Id);
            if (mevcut != null) { model.ResimUrl = mevcut.ResimUrl; model.Resimler = mevcut.Resimler; }
        }
        await _svc.TasinirTanimKaydetAsync(model);
        TempData["Basari"] = "Taşınır tanımı kaydedildi.";
        return RedirectToAction(nameof(TasinirTanimlar));
    }
}
