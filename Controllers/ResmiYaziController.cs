using System.Security.Claims;
using ATOM.Models.Accounts;
using ATOM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATOM.Controllers;

/// <summary>EBYS / e-Yazışma hazır resmi yazı üretimi (Resmî Yazışma Yönetmeliği + 5070).</summary>
[Authorize(Roles = $"{AtomRoller.SistemAdmin},{AtomRoller.BakanlikMerkez},{AtomRoller.BakanlikSatinAlma},{AtomRoller.IlMuduru}")]
public class ResmiYaziController : Controller
{
    private readonly IAtomDataService _svc;
    private readonly BelgeService _belge;
    private readonly IImzaService _imza;
    private readonly IAuditService _audit;

    public ResmiYaziController(IAtomDataService svc, BelgeService belge, IImzaService imza, IAuditService audit)
    { _svc = svc; _belge = belge; _imza = imza; _audit = audit; }

    private string KurumId => User.FindFirstValue("KurumId")!;
    private string AdSoyad => User.FindFirstValue("AdSoyad") ?? User.Identity?.Name ?? "";

    [HttpGet("/resmi-yazi")]
    public async Task<IActionResult> Index()
    {
        var kurum = await _svc.KurumGetirAsync(KurumId);
        ViewBag.KurumAdi = kurum?.Ad ?? "Bakanlık";
        ViewBag.ImzaAd = AdSoyad;
        return View();
    }

    [HttpPost("/resmi-yazi")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Olustur(string kurum, string konu, string ilgi, string muhatap,
        string govde, string imzaAd, string imzaUnvan)
    {
        var sayi = await _svc.YeniNumaraUretAsync("RY");
        var icerik = $"{sayi}|{konu}|{muhatap}|{govde}";
        var imza = await _imza.BelgeImzalaAsync(User, "ResmiYazi", sayi, sayi, icerik, kurum, "E-İmza");
        var bytes = _belge.WordResmiYazi(kurum, sayi, konu, ilgi, muhatap, govde, imzaAd, imzaUnvan, imza.DogrulamaKodu);
        await _audit.KaydetAsync(User, "ResmiYazi", "Oluşturma", "ResmiYazi", sayi, $"{sayi} resmi yazı üretildi");
        return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"ResmiYazi-{sayi}.docx");
    }
}
