using System.Security.Claims;
using ATOM.Models.Accounts;

namespace ATOM.Services;

/// <summary>KVKK (6698) gereği kişisel veri maskeleme yardımcıları.</summary>
public static class KvkkHelper
{
    // TC görme yetkisi olan roller (taşınır kayıt/kontrol yetkilileri + yönetim)
    private static readonly HashSet<string> TcGorebilenRoller = new()
    {
        AtomRoller.SistemAdmin, AtomRoller.BakanlikMerkez,
        AtomRoller.IlMuduru, AtomRoller.IlDepoSorumlusu, AtomRoller.MerkezDepoSorumlusu
    };

    /// <summary>TC kimliği maskeler: 12345678901 -> 123******01</summary>
    public static string MaskeleTc(string? tc, ClaimsPrincipal? user)
    {
        if (string.IsNullOrWhiteSpace(tc)) return "";
        var rol = user?.FindFirstValue(ClaimTypes.Role);
        if (rol != null && TcGorebilenRoller.Contains(rol)) return tc;
        if (tc.Length < 5) return new string('*', tc.Length);
        return tc.Substring(0, 3) + new string('*', tc.Length - 5) + tc.Substring(tc.Length - 2);
    }

    /// <summary>Telefonu maskeler: 05551234567 -> 0555***4567</summary>
    public static string MaskeleTelefon(string? tel, ClaimsPrincipal? user)
    {
        if (string.IsNullOrWhiteSpace(tel)) return "";
        var rol = user?.FindFirstValue(ClaimTypes.Role);
        if (rol != null && TcGorebilenRoller.Contains(rol)) return tel;
        if (tel.Length < 8) return tel;
        return tel.Substring(0, 4) + "***" + tel.Substring(tel.Length - 4);
    }
}
