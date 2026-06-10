using System.Security.Claims;
using ATOM.Models.Accounts;
using ATOM.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using BC = BCrypt.Net.BCrypt;

namespace ATOM.Controllers;

public class AuthController : Controller
{
    private readonly IAtomDataService _svc;
    public AuthController(IAtomDataService svc) => _svc = svc;

    [HttpGet("/giris")]
    public IActionResult Giris(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Dashboard");
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost("/giris")]
    public async Task<IActionResult> Giris(string kullaniciAdi, string sifre, string? returnUrl = null)
    {
        var kullanici = await _svc.KullaniciAdiylaGetirAsync(kullaniciAdi);
        if (kullanici == null || !kullanici.AktifMi || !BC.Verify(sifre, kullanici.SifreHash))
        {
            ModelState.AddModelError("", "Kullanıcı adı veya şifre hatalı.");
            return View();
        }

        kullanici.SonGirisTarihi = DateTime.UtcNow;
        await _svc.KullaniciKaydetAsync(kullanici);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, kullanici.Id),
            new(ClaimTypes.Name, kullanici.KullaniciAdi),
            new("AdSoyad", kullanici.AdSoyad),
            new(ClaimTypes.Role, kullanici.Rol),
            new("KurumId", kullanici.KurumId),
        };
        if (kullanici.FirmaId != null) claims.Add(new("FirmaId", kullanici.FirmaId));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) });

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost("/cikis")]
    public async Task<IActionResult> Cikis()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Giris));
    }

    [HttpGet("/erisim-yok")]
    public IActionResult ErisimYok() => View();
}
