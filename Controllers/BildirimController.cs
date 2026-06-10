using System.Security.Claims;
using ATOM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATOM.Controllers;

[Authorize]
public class BildirimController : Controller
{
    private readonly IAtomDataService _svc;
    public BildirimController(IAtomDataService svc) => _svc = svc;

    private string KullaniciId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public async Task<IActionResult> Index()
    {
        var bildirimler = await _svc.BildirimleriGetirAsync(KullaniciId);
        return View(bildirimler);
    }

    [HttpPost]
    public async Task<IActionResult> Oku(string id, string? donus = null)
    {
        await _svc.BildirimOkunduIsaretle(id);
        if (!string.IsNullOrEmpty(donus) && Url.IsLocalUrl(donus)) return Redirect(donus);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> TumunuOku()
    {
        var bildirimler = await _svc.BildirimleriGetirAsync(KullaniciId);
        foreach (var b in bildirimler.Where(x => !x.OkunduMu))
            await _svc.BildirimOkunduIsaretle(b.Id);
        return RedirectToAction(nameof(Index));
    }
}
