using System.Security.Claims;
using ATOM.Models.Domain;

namespace ATOM.Services;

public interface IAuditService
{
    Task KaydetAsync(ClaimsPrincipal user, string modul, string islem, string kayitTur,
        string kayitId, string aciklama, string? oncekiDeger = null, string? yeniDeger = null,
        string? ip = null);
}

public class AuditService : IAuditService
{
    private readonly IAtomDataService _data;
    public AuditService(IAtomDataService data) => _data = data;

    public async Task KaydetAsync(ClaimsPrincipal user, string modul, string islem, string kayitTur,
        string kayitId, string aciklama, string? oncekiDeger = null, string? yeniDeger = null,
        string? ip = null)
    {
        await _data.AuditKaydetAsync(new AuditLog
        {
            KullaniciId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "",
            KullaniciAdi = user.FindFirstValue("AdSoyad") ?? user.Identity?.Name ?? "",
            Rol = user.FindFirstValue(ClaimTypes.Role) ?? "",
            Modul = modul, Islem = islem, KayitTur = kayitTur, KayitId = kayitId,
            Aciklama = aciklama, OncekiDeger = oncekiDeger, YeniDeger = yeniDeger, Ip = ip
        });
    }
}
