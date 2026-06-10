using ATOM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATOM.Controllers;

/// <summary>Elektronik onaylı belge doğrulama (5070 sayılı Kanun). Giriş gerektirmez.</summary>
[AllowAnonymous]
public class BelgeDogrulamaController : Controller
{
    private readonly IAtomDataService _svc;
    public BelgeDogrulamaController(IAtomDataService svc) => _svc = svc;

    [HttpGet("/belge-dogrula")]
    public async Task<IActionResult> Index(string? kod = null)
    {
        if (!string.IsNullOrEmpty(kod))
            ViewBag.Imza = await _svc.ImzaDogrulamaKoduylaGetirAsync(kod.Trim());
        ViewBag.Kod = kod;
        return View();
    }
}
