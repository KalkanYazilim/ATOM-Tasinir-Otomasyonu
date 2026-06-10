using System.Security.Claims;
using ATOM.Models.Accounts;
using ATOM.Models.Domain;
using ATOM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATOM.Areas.Ihale.Controllers;

[Area("Ihale")]
[Authorize]
public class HomeController : Controller
{
    private readonly IAtomDataService _svc;
    public HomeController(IAtomDataService svc) => _svc = svc;

    private string KullaniciId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private string Rol => User.FindFirstValue(ClaimTypes.Role)!;

    public async Task<IActionResult> Index(string? durum = null)
    {
        var ihaleler = await _svc.IhaleleriGetirAsync();
        var firmalar = await _svc.FirmalariGetirAsync();

        // Tedarikçi yalnız kendi teklifini verdiği ihaleleri görür
        if (Rol == AtomRoller.Tedarikci)
        {
            var firmaId = User.FindFirstValue("FirmaId");
            ihaleler = ihaleler.Where(i =>
                i.Durum == IhaleDurumu.TeklifAliniyor || i.Durum == IhaleDurumu.IlanEdildi ||
                i.Teklifler.Any(t => t.FirmaId == firmaId)).ToList();
        }

        if (!string.IsNullOrEmpty(durum) && Enum.TryParse<IhaleDurumu>(durum, out var d))
            ihaleler = ihaleler.Where(i => i.Durum == d).ToList();

        ViewBag.Firmalar = firmalar.ToDictionary(f => f.Id, f => f.Ad);
        ViewBag.Durum = durum;
        return View(ihaleler.OrderByDescending(i => i.OlusturmaTarihi).ToList());
    }

    [HttpGet]
    [Authorize(Roles = $"{AtomRoller.BakanlikSatinAlma},{AtomRoller.SistemAdmin}")]
    public async Task<IActionResult> Yeni()
    {
        ViewBag.Tanimlar = await _svc.TasinirTanimlariGetirAsync();
        ViewBag.BekleyenTalepler = (await _svc.TalepleriGetirAsync())
            .Where(t => t.Durum == TalepDurumu.BakanlikInceliyor && t.BaglantiliIhaleId == null).ToList();
        return View(new ATOM.Models.Domain.Ihale());
    }

    [HttpPost]
    [Authorize(Roles = $"{AtomRoller.BakanlikSatinAlma},{AtomRoller.SistemAdmin}")]
    public async Task<IActionResult> Yeni(ATOM.Models.Domain.Ihale ihale, string[] tasinirIds, int[] miktarlar,
        string[] teknikOzellikler, string[] kaynakTalepIds)
    {
        ihale.IhaleNo = await _svc.YeniNumaraUretAsync("IH");
        ihale.OlusturanKullaniciId = KullaniciId;
        ihale.OlusturmaTarihi = DateTime.UtcNow;
        ihale.Durum = IhaleDurumu.Hazirlaniyor;
        ihale.Kalemler = new();
        ihale.KaynaklananTalepIds = kaynakTalepIds.Where(x => !string.IsNullOrEmpty(x)).ToList();

        for (int i = 0; i < tasinirIds.Length; i++)
        {
            if (!string.IsNullOrEmpty(tasinirIds[i]) && miktarlar[i] > 0)
                ihale.Kalemler.Add(new IhaleKalemi
                {
                    TasinirTanimId = tasinirIds[i],
                    Miktar = miktarlar[i],
                    TeknikOzellik = i < teknikOzellikler.Length ? teknikOzellikler[i] : ""
                });
        }

        await _svc.IhaleKaydetAsync(ihale);

        // Bağlantılı talepleri işaretle
        foreach (var talepId in ihale.KaynaklananTalepIds)
        {
            var talep = await _svc.TalepGetirAsync(talepId);
            if (talep != null) { talep.BaglantiliIhaleId = ihale.Id; await _svc.TalepKaydetAsync(talep); }
        }

        TempData["Basari"] = $"{ihale.IhaleNo} numaralı ihale oluşturuldu.";
        return RedirectToAction(nameof(Detay), new { id = ihale.Id });
    }

    public async Task<IActionResult> Detay(string id)
    {
        var ihale = await _svc.IhaleGetirAsync(id);
        if (ihale == null) return NotFound();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        var firmalar = await _svc.FirmalariGetirAsync();
        ViewBag.Tanimlar = tanimlar.ToDictionary(t => t.Id, t => new { t.Ad, t.Birim });
        ViewBag.Firmalar = firmalar.ToDictionary(f => f.Id, f => f.Ad);
        ViewBag.FirmaId = User.FindFirstValue("FirmaId");
        return View(ihale);
    }

    [Authorize(Roles = $"{AtomRoller.BakanlikSatinAlma},{AtomRoller.BakanlikMerkez},{AtomRoller.SistemAdmin}")]
    public async Task<IActionResult> TeklifKiyas(string id)
    {
        var ihale = await _svc.IhaleGetirAsync(id);
        if (ihale == null) return NotFound();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        var firmalar = await _svc.FirmalariGetirAsync();

        ViewBag.Tanimlar = tanimlar.ToDictionary(t => t.Id, t => new { t.Ad, t.Birim });
        ViewBag.Firmalar = firmalar.ToDictionary(f => f.Id, f => f.Ad);
        ViewBag.EnDusukTeklif = ihale.Teklifler.Any() ? ihale.Teklifler.Min(t => t.ToplamTutar) : 0m;
        return View(ihale);
    }

    [HttpPost]
    [Authorize(Roles = $"{AtomRoller.BakanlikSatinAlma},{AtomRoller.SistemAdmin}")]
    public async Task<IActionResult> Ilan(string id)
    {
        var ihale = await _svc.IhaleGetirAsync(id);
        if (ihale == null) return NotFound();
        ihale.Durum = IhaleDurumu.IlanEdildi;
        ihale.IlanTarihi = DateTime.UtcNow;
        await _svc.IhaleKaydetAsync(ihale);
        TempData["Basari"] = "İhale ilan edildi. Firmalar teklif verebilir.";
        return RedirectToAction(nameof(Detay), new { id });
    }

    [HttpPost]
    [Authorize(Roles = $"{AtomRoller.BakanlikSatinAlma},{AtomRoller.SistemAdmin}")]
    public async Task<IActionResult> TeklifAc(string id, DateTime sonTarih)
    {
        var ihale = await _svc.IhaleGetirAsync(id);
        if (ihale == null) return NotFound();
        ihale.Durum = IhaleDurumu.TeklifAliniyor;
        ihale.TeklifSonTarihi = sonTarih;
        await _svc.IhaleKaydetAsync(ihale);
        TempData["Basari"] = "Teklif alma süreci başlatıldı.";
        return RedirectToAction(nameof(Detay), new { id });
    }

    [HttpPost]
    [Authorize(Roles = $"{AtomRoller.Tedarikci}")]
    public async Task<IActionResult> TeklifVer(string id, decimal toplamTutar, string aciklama,
        string[] tasinirIds, int[] miktarlar, decimal[] birimFiyatlar, string[] markalar, string[] modeller)
    {
        var ihale = await _svc.IhaleGetirAsync(id);
        if (ihale == null) return NotFound();
        if (ihale.Durum != IhaleDurumu.TeklifAliniyor) return BadRequest("Teklif alma süreci aktif değil.");

        var firmaId = User.FindFirstValue("FirmaId") ?? "";
        var teklif = new IhaleTeklif
        {
            FirmaId = firmaId,
            VerilmeTarihi = DateTime.UtcNow,
            ToplamTutar = toplamTutar,
            Durum = TeklifDurumu.Gonderildi,
            Aciklama = aciklama,
            Kalemler = new()
        };

        for (int i = 0; i < tasinirIds.Length; i++)
        {
            teklif.Kalemler.Add(new TeklifKalemi
            {
                TasinirTanimId = tasinirIds[i],
                Miktar = miktarlar[i],
                BirimFiyat = birimFiyatlar[i],
                Marka = i < markalar.Length ? markalar[i] : "",
                Model = i < modeller.Length ? modeller[i] : ""
            });
        }

        ihale.Teklifler.Add(teklif);
        await _svc.IhaleKaydetAsync(ihale);
        TempData["Basari"] = "Teklifiniz başarıyla iletildi.";
        return RedirectToAction(nameof(Detay), new { id });
    }

    [HttpPost]
    [Authorize(Roles = $"{AtomRoller.BakanlikSatinAlma},{AtomRoller.SistemAdmin}")]
    public async Task<IActionResult> Sonuclandir(string id, string kazananTeklifId, string degerlendirmeNotu)
    {
        var ihale = await _svc.IhaleGetirAsync(id);
        if (ihale == null) return NotFound();

        var kazananTeklif = ihale.Teklifler.FirstOrDefault(t => t.Id == kazananTeklifId);
        if (kazananTeklif == null) return BadRequest();

        ihale.Durum = IhaleDurumu.Sonuclandi;
        ihale.KazananTeklifId = kazananTeklifId;
        ihale.KazananFirmaId = kazananTeklif.FirmaId;
        ihale.SonuclanmaTarihi = DateTime.UtcNow;

        foreach (var t in ihale.Teklifler)
            t.Durum = t.Id == kazananTeklifId ? TeklifDurumu.Kabul : TeklifDurumu.Red;

        ihale.OnayGecmisi.Add(new OnayKaydi
        {
            KullaniciId = KullaniciId,
            KullaniciAdi = User.FindFirstValue("AdSoyad") ?? "",
            Rol = Rol, Karar = OnayDurumu.Onaylandi,
            Asama = "İhale Sonuçlandırma", Aciklama = degerlendirmeNotu
        });

        await _svc.IhaleKaydetAsync(ihale);
        TempData["Basari"] = "İhale sonuçlandırıldı. Kazanan firma bilgilendirildi.";
        return RedirectToAction(nameof(Detay), new { id });
    }
}

