namespace ATOM.Models.Accounts;

public static class AtomRoller
{
    public const string SistemAdmin = "SistemAdmin";
    public const string BakanlikMerkez = "BakanlikMerkez";
    public const string BakanlikSatinAlma = "BakanlikSatinAlma";
    public const string IlMuduru = "IlMuduru";
    public const string IlDepoSorumlusu = "IlDepoSorumlusu";
    public const string MerkezDepoSorumlusu = "MerkezDepoSorumlusu";
    public const string Teknisyen = "Teknisyen";
    public const string Personel = "Personel";
    public const string Tedarikci = "Tedarikci";

    public static readonly string[] Tumu = {
        SistemAdmin, BakanlikMerkez, BakanlikSatinAlma,
        IlMuduru, IlDepoSorumlusu, MerkezDepoSorumlusu,
        Teknisyen, Personel, Tedarikci
    };

    public static readonly string[] BakanlikRolleri = {
        SistemAdmin, BakanlikMerkez, BakanlikSatinAlma, MerkezDepoSorumlusu
    };

    public static string DisplayName(string rol) => rol switch
    {
        SistemAdmin => "Sistem Yöneticisi",
        BakanlikMerkez => "Bakanlık Merkez",
        BakanlikSatinAlma => "Bakanlık Satın Alma",
        IlMuduru => "İl Müdürü",
        IlDepoSorumlusu => "İl Depo Sorumlusu",
        MerkezDepoSorumlusu => "Merkez Depo Sorumlusu",
        Teknisyen => "Teknisyen",
        Personel => "Personel",
        Tedarikci => "Tedarikçi Firma",
        _ => rol
    };
}

public class AtomKullanici
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string AdSoyad { get; set; } = "";
    public string KullaniciAdi { get; set; } = "";
    public string Email { get; set; } = "";
    public string SifreHash { get; set; } = "";
    public string Rol { get; set; } = AtomRoller.Personel;
    public string KurumId { get; set; } = "";
    public string? FirmaId { get; set; }
    public bool AktifMi { get; set; } = true;
    public DateTime OlusturmaTarihi { get; set; } = DateTime.UtcNow;
    public DateTime? SonGirisTarihi { get; set; }
    public string Telefon { get; set; } = "";
    public string? Avatar { get; set; }
    public bool SifreDegistirmesiGerekiyor { get; set; }
}
