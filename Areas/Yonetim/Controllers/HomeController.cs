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
    public HomeController(IAtomDataService svc) => _svc = svc;

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
    public async Task<IActionResult> TasinirTanimDuzenle(TasinirTanim model)
    {
        await _svc.TasinirTanimKaydetAsync(model);
        TempData["Basari"] = "Taşınır tanımı kaydedildi.";
        return RedirectToAction(nameof(TasinirTanimlar));
    }
}
