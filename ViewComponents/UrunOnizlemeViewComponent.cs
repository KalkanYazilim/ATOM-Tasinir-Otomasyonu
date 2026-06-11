using System.Text.Json;
using ATOM.Services;
using Microsoft.AspNetCore.Mvc;

namespace ATOM.ViewComponents;

/// <summary>Tüm sayfalarda ürün (taşınır tanım) id → resim/ad haritasını ve fareyi takip eden
/// resim önizleme balonunu sağlar. data-tanim-id veya data-urun-resim taşıyan her öğede çalışır.</summary>
public class UrunOnizlemeViewComponent : ViewComponent
{
    private readonly IAtomDataService _svc;
    public UrunOnizlemeViewComponent(IAtomDataService svc) => _svc = svc;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        var harita = tanimlar.ToDictionary(
            t => t.Id,
            t => new { ad = t.Ad, resim = t.ResimUrl ?? "", kategori = t.Kategori.ToString() });
        var json = JsonSerializer.Serialize(harita);
        return View(model: json);
    }
}
