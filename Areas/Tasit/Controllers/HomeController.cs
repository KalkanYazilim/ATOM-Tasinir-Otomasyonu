using System.Security.Claims;
using ATOM.Models.Accounts;
using ATOM.Models.Domain;
using ATOM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATOM.Areas.Tasit.Controllers;

[Area("Tasit")]
[Authorize]
public class HomeController : Controller
{
    private readonly IAtomDataService _svc;
    private readonly IDosyaService _dosya;
    private readonly IAuditService _audit;

    public HomeController(IAtomDataService svc, IDosyaService dosya, IAuditService audit)
    {
        _svc = svc; _dosya = dosya; _audit = audit;
    }

    private string AdSoyad => User.FindFirstValue("AdSoyad") ?? User.Identity?.Name ?? "";
    private string Rol => User.FindFirstValue(ClaimTypes.Role)!;
    private string KurumId => User.FindFirstValue("KurumId")!;
    private bool Bakanlik => AtomRoller.BakanlikRolleri.Contains(Rol);
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    public async Task<IActionResult> Index(string? durum = null, string? ara = null)
    {
        var tasitlar = await _svc.TasitlariGetirAsync();
        var kurumlar = await _svc.KurumlariGetirAsync();
        if (!Bakanlik) tasitlar = tasitlar.Where(t => t.KurumId == KurumId).ToList();
        if (!string.IsNullOrEmpty(durum) && Enum.TryParse<TasitDurumu>(durum, out var d))
            tasitlar = tasitlar.Where(t => t.Durum == d).ToList();
        if (!string.IsNullOrEmpty(ara))
            tasitlar = tasitlar.Where(t => t.Plaka.Contains(ara, StringComparison.OrdinalIgnoreCase)
                || t.Marka.Contains(ara, StringComparison.OrdinalIgnoreCase)).ToList();

        ViewBag.Kurumlar = kurumlar.ToDictionary(k => k.Id, k => k.Ad);
        ViewBag.Ara = ara; ViewBag.Durum = durum;
        // Yaklaşan muayene/sigorta uyarıları
        ViewBag.YaklasanMuayene = tasitlar.Count(t => t.MuayeneBitisTarihi.HasValue && t.MuayeneBitisTarihi.Value <= DateTime.UtcNow.AddDays(30));
        ViewBag.YaklasanSigorta = tasitlar.Count(t => t.SigortaBitisTarihi.HasValue && t.SigortaBitisTarihi.Value <= DateTime.UtcNow.AddDays(30));
        return View(tasitlar.OrderBy(t => t.Plaka).ToList());
    }

    public async Task<IActionResult> Detay(string id)
    {
        var t = await _svc.TasitGetirAsync(id);
        if (t == null) return NotFound();
        if (!Bakanlik && t.KurumId != KurumId) return Forbid();
        var kullanicilar = await _svc.KullanicilariGetirAsync();
        ViewBag.Kullanicilar = kullanicilar.ToDictionary(k => k.Id, k => k.AdSoyad);
        return View(t);
    }

    [HttpGet]
    [Authorize(Roles = $"{AtomRoller.IlDepoSorumlusu},{AtomRoller.MerkezDepoSorumlusu},{AtomRoller.IlMuduru},{AtomRoller.BakanlikMerkez},{AtomRoller.SistemAdmin}")]
    public async Task<IActionResult> Duzenle(string? id = null)
    {
        var kullanicilar = await _svc.KullanicilariGetirAsync();
        if (!Bakanlik) kullanicilar = kullanicilar.Where(k => k.KurumId == KurumId).ToList();
        ViewBag.Kullanicilar = kullanicilar.Where(k => k.AktifMi).ToList();
        if (id != null)
        {
            var t = await _svc.TasitGetirAsync(id);
            if (t == null) return NotFound();
            return View(t);
        }
        return View(new ATOM.Models.Domain.Tasit { KurumId = KurumId, ModelYili = DateTime.Now.Year });
    }

    [HttpPost]
    [Authorize(Roles = $"{AtomRoller.IlDepoSorumlusu},{AtomRoller.MerkezDepoSorumlusu},{AtomRoller.IlMuduru},{AtomRoller.BakanlikMerkez},{AtomRoller.SistemAdmin}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Duzenle(ATOM.Models.Domain.Tasit model, IFormFile? resim)
    {
        var mevcut = await _svc.TasitGetirAsync(model.Id);
        var resimUrl = await _dosya.ResimKaydetAsync(resim, "tasit");
        if (resimUrl != null) model.ResimUrl = resimUrl;
        else if (mevcut != null) model.ResimUrl = mevcut.ResimUrl;

        if (mevcut != null)
        {
            model.YakitKayitlari = mevcut.YakitKayitlari;
            model.BakimKayitlari = mevcut.BakimKayitlari;
            model.KazaKayitlari = mevcut.KazaKayitlari;
            model.OlusturmaTarihi = mevcut.OlusturmaTarihi;
        }
        if (string.IsNullOrEmpty(model.KurumId)) model.KurumId = KurumId;

        await _svc.TasitKaydetAsync(model);
        await _audit.KaydetAsync(User, "Taşıt", mevcut == null ? "Oluşturma" : "Güncelleme", "Tasit", model.Id, $"{model.Plaka} kaydedildi", ip: Ip);
        TempData["Basari"] = $"{model.Plaka} plakalı taşıt kaydedildi.";
        return RedirectToAction(nameof(Detay), new { id = model.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> YakitEkle(string id, decimal litre, decimal tutar, int km, string istasyon)
    {
        var t = await _svc.TasitGetirAsync(id);
        if (t == null) return NotFound();
        t.YakitKayitlari.Add(new TasitYakitKaydi { Litre = litre, Tutar = tutar, Km = km, Istasyon = istasyon, KullaniciAdi = AdSoyad });
        if (km > t.GuncelKm) t.GuncelKm = km;
        await _svc.TasitKaydetAsync(t);
        TempData["Basari"] = "Yakıt kaydı eklendi.";
        return RedirectToAction(nameof(Detay), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BakimEkle(string id, string islemTuru, string aciklama, decimal tutar, int km, string servis)
    {
        var t = await _svc.TasitGetirAsync(id);
        if (t == null) return NotFound();
        t.BakimKayitlari.Add(new TasitBakimKaydi { IslemTuru = islemTuru, Aciklama = aciklama, Tutar = tutar, Km = km, Servis = servis });
        if (km > t.GuncelKm) t.GuncelKm = km;
        await _svc.TasitKaydetAsync(t);
        TempData["Basari"] = "Bakım kaydı eklendi.";
        return RedirectToAction(nameof(Detay), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> KazaEkle(string id, string yer, string aciklama, bool kusurVar, decimal hasarBedeli, string surucuAdi)
    {
        var t = await _svc.TasitGetirAsync(id);
        if (t == null) return NotFound();
        t.KazaKayitlari.Add(new TasitKazaKaydi { Yer = yer, Aciklama = aciklama, KusurVar = kusurVar, HasarBedeli = hasarBedeli, SurucuAdi = surucuAdi });
        await _svc.TasitKaydetAsync(t);
        TempData["Basari"] = "Kaza kaydı eklendi.";
        return RedirectToAction(nameof(Detay), new { id });
    }
}
