using System.Security.Claims;
using ATOM.Services;
using Microsoft.AspNetCore.Mvc;

namespace ATOM.ViewComponents;

public class BildirimCaniViewComponent : ViewComponent
{
    private readonly IAtomDataService _svc;
    public BildirimCaniViewComponent(IAtomDataService svc) => _svc = svc;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var kullaniciId = (UserClaimsPrincipal as ClaimsPrincipal)?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(kullaniciId)) return View(0);
        var bildirimler = await _svc.BildirimleriGetirAsync(kullaniciId);
        return View(bildirimler.Count(b => !b.OkunduMu));
    }
}
