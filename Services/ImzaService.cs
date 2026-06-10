using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ATOM.Models.Domain;

namespace ATOM.Services;

/// <summary>5070 sayılı Elektronik İmza Kanunu'na hazır elektronik onay + belge doğrulama.</summary>
public interface IImzaService
{
    Task<ElektronikImza> BelgeImzalaAsync(ClaimsPrincipal user, string belgeTuru, string belgeId,
        string belgeNo, string belgeIcerik, string kurum, string imzaTipi = "Elektronik Onay");
    string Hash(string icerik);
}

public class ImzaService : IImzaService
{
    private readonly IAtomDataService _data;
    public ImzaService(IAtomDataService data) => _data = data;

    public string Hash(string icerik)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(icerik));
        return Convert.ToHexString(bytes);
    }

    public async Task<ElektronikImza> BelgeImzalaAsync(ClaimsPrincipal user, string belgeTuru, string belgeId,
        string belgeNo, string belgeIcerik, string kurum, string imzaTipi = "Elektronik Onay")
    {
        var hash = Hash(belgeIcerik);
        // Kısa doğrulama kodu (hash'ten türetilmiş, okunabilir)
        var kod = $"ATOM-{hash.Substring(0, 4)}-{hash.Substring(4, 4)}-{hash.Substring(8, 4)}";

        var imza = new ElektronikImza
        {
            DogrulamaKodu = kod,
            BelgeTuru = belgeTuru,
            BelgeId = belgeId,
            BelgeNo = belgeNo,
            BelgeHash = hash,
            ImzalayanKullaniciId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "",
            ImzalayanAdSoyad = user.FindFirstValue("AdSoyad") ?? user.Identity?.Name ?? "",
            ImzalayanRol = user.FindFirstValue(ClaimTypes.Role) ?? "",
            ImzaTipi = imzaTipi,
            Kurum = kurum
        };
        await _data.ImzaKaydetAsync(imza);
        return imza;
    }
}
