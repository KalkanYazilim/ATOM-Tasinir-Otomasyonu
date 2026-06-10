namespace ATOM.Services;

public interface IDosyaService
{
    /// <summary>Resmi wwwroot/uploads altına kaydeder, göreli URL döner (/uploads/...).</summary>
    Task<string?> ResimKaydetAsync(IFormFile? dosya, string altKlasor);
}

public class DosyaService : IDosyaService
{
    private readonly IWebHostEnvironment _env;
    private static readonly string[] IzinliUzantilar = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    public DosyaService(IWebHostEnvironment env) => _env = env;

    public async Task<string?> ResimKaydetAsync(IFormFile? dosya, string altKlasor)
    {
        if (dosya == null || dosya.Length == 0) return null;
        var uzanti = Path.GetExtension(dosya.FileName).ToLowerInvariant();
        if (!IzinliUzantilar.Contains(uzanti)) return null;
        if (dosya.Length > 5 * 1024 * 1024) return null; // 5 MB sınır

        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var klasor = Path.Combine(webRoot, "uploads", altKlasor);
        Directory.CreateDirectory(klasor);

        var ad = $"{Guid.NewGuid():N}{uzanti}";
        var tamYol = Path.Combine(klasor, ad);
        using (var fs = new FileStream(tamYol, FileMode.Create))
            await dosya.CopyToAsync(fs);

        return $"/uploads/{altKlasor}/{ad}";
    }
}
