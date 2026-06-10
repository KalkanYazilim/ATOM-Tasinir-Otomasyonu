using System.Security.Claims;
using ATOM.Models.Accounts;
using ATOM.Models.Domain;
using ATOM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATOM.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly IAtomDataService _svc;
    public DashboardController(IAtomDataService svc) => _svc = svc;

    public async Task<IActionResult> Index()
    {
        var kullaniciId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var rol = User.FindFirstValue(ClaimTypes.Role)!;
        var kurumId = User.FindFirstValue("KurumId")!;

        var talepler = await _svc.TalepleriGetirAsync();
        var ihaleler = await _svc.IhaleleriGetirAsync();
        var depolar = await _svc.DepolariGetirAsync();
        var zimmetler = await _svc.ZimmetleriGetirAsync();
        var bakimlar = await _svc.BakimKayitlariGetirAsync();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        var bildirimler = await _svc.BildirimleriGetirAsync(kullaniciId);
        var kurumlar = await _svc.KurumlariGetirAsync();

        // Rol bazlı depo filtresi
        var gorunenDepolar = AtomRoller.BakanlikRolleri.Contains(rol)
            ? depolar
            : depolar.Where(d => d.KurumId == kurumId).ToList();

        var vm = new DashboardViewModel
        {
            Rol = rol,
            KurumId = kurumId,

            // Talepler
            ToplamTalep = AtomRoller.BakanlikRolleri.Contains(rol)
                ? talepler.Count
                : talepler.Count(t => t.TalepciKurumId == kurumId),
            BekleyenTalep = AtomRoller.BakanlikRolleri.Contains(rol)
                ? talepler.Count(t => t.Durum == TalepDurumu.BakanlikAlindi || t.Durum == TalepDurumu.BakanlikInceliyor)
                : talepler.Count(t => t.TalepciKurumId == kurumId && t.Durum == TalepDurumu.GonderildiIlOnay),

            // İhaleler
            AktifIhale = ihaleler.Count(i => i.Durum == IhaleDurumu.TeklifAliniyor || i.Durum == IhaleDurumu.IlanEdildi),
            ToplamIhale = ihaleler.Count,

            // Depolar
            ToplamDepoStokAdedi = gorunenDepolar.SelectMany(d => d.Stoklar).Sum(s => s.Miktar),
            KritikStokSayisi = gorunenDepolar.SelectMany(d => d.Stoklar)
                .Count(s => s.Miktar <= s.MinEsik && s.MinEsik > 0),

            // Zimmet
            AktifZimmet = zimmetler.Count(z => z.Durum == ZimmetDurumu.Aktif),

            // Bakım
            AcikBakim = bakimlar.Count(b => b.Durum == BakimDurumu.Acik),

            // Bildirimler
            OkunmamisBildirim = bildirimler.Count(b => !b.OkunduMu),
            SonBildirimler = bildirimler.Take(5).ToList(),

            // Son Talepler
            SonTalepler = (AtomRoller.BakanlikRolleri.Contains(rol)
                ? talepler
                : talepler.Where(t => t.TalepciKurumId == kurumId))
                .OrderByDescending(t => t.TalepTarihi).Take(5).ToList(),

            // Son İhaleler
            SonIhaleler = ihaleler.OrderByDescending(i => i.OlusturmaTarihi).Take(5).ToList(),

            // Stok özeti (grafik için)
            DepoStokOzeti = gorunenDepolar.Select(d => new DepoStokOzet
            {
                DepoAdi = d.Ad,
                ToplamKalem = d.Stoklar.Count,
                ToplamAdet = d.Stoklar.Sum(s => s.Miktar),
                ToplamDeger = d.Stoklar.Sum(s => s.Miktar * s.BirimMaliyet),
                KritikSayisi = d.Stoklar.Count(s => s.Miktar <= s.MinEsik && s.MinEsik > 0)
            }).ToList(),

            // Kategori bazlı stok (pasta grafik)
            KategoriBazliStok = gorunenDepolar
                .SelectMany(d => d.Stoklar)
                .GroupBy(s => tanimlar.FirstOrDefault(t => t.Id == s.TasinirTanimId)?.Kategori ?? TasinirKategori.Diger)
                .Select(g => new KategoriStok { Kategori = g.Key.ToString(), Adet = g.Sum(x => x.Miktar) })
                .ToList(),

            // Aylık talep trendi (son 6 ay)
            AylikTalepTrendi = Enumerable.Range(0, 6)
                .Select(i => DateTime.UtcNow.AddMonths(-5 + i))
                .Select(ay => new AylikTrend
                {
                    Ay = ay.ToString("MMM yy"),
                    Sayi = talepler.Count(t => t.TalepTarihi.Year == ay.Year && t.TalepTarihi.Month == ay.Month)
                }).ToList(),

            Kurumlar = kurumlar,
        };

        return View(vm);
    }
}

public class DashboardViewModel
{
    public string Rol { get; set; } = "";
    public string KurumId { get; set; } = "";
    public int ToplamTalep { get; set; }
    public int BekleyenTalep { get; set; }
    public int AktifIhale { get; set; }
    public int ToplamIhale { get; set; }
    public int ToplamDepoStokAdedi { get; set; }
    public int KritikStokSayisi { get; set; }
    public int AktifZimmet { get; set; }
    public int AcikBakim { get; set; }
    public int OkunmamisBildirim { get; set; }
    public List<Bildirim> SonBildirimler { get; set; } = new();
    public List<IhtiyacTalebi> SonTalepler { get; set; } = new();
    public List<Ihale> SonIhaleler { get; set; } = new();
    public List<DepoStokOzet> DepoStokOzeti { get; set; } = new();
    public List<KategoriStok> KategoriBazliStok { get; set; } = new();
    public List<AylikTrend> AylikTalepTrendi { get; set; } = new();
    public List<Kurum> Kurumlar { get; set; } = new();
}

public class DepoStokOzet
{
    public string DepoAdi { get; set; } = "";
    public int ToplamKalem { get; set; }
    public int ToplamAdet { get; set; }
    public decimal ToplamDeger { get; set; }
    public int KritikSayisi { get; set; }
}

public class KategoriStok { public string Kategori { get; set; } = ""; public int Adet { get; set; } }
public class AylikTrend { public string Ay { get; set; } = ""; public int Sayi { get; set; } }
