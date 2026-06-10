using System.Security.Claims;
using ATOM.Models.Accounts;
using ATOM.Models.Domain;
using ATOM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATOM.Areas.Raporlama.Controllers;

[Area("Raporlama")]
[Authorize]
public class HomeController : Controller
{
    private readonly IAtomDataService _svc;
    public HomeController(IAtomDataService svc) => _svc = svc;

    private string Rol => User.FindFirstValue(ClaimTypes.Role)!;
    private string KurumId => User.FindFirstValue("KurumId")!;
    private bool Bakanlik => AtomRoller.BakanlikRolleri.Contains(Rol);

    // ─── CSV Export Yardımcısı ────────────────────────────────
    private static string CsvAlan(object? v)
    {
        var s = v?.ToString() ?? "";
        if (s.Contains(';') || s.Contains('"') || s.Contains('\n'))
            s = "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    private FileContentResult CsvDosya(IEnumerable<string> basliklar, IEnumerable<IEnumerable<object?>> satirlar, string dosyaAdi)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(string.Join(";", basliklar.Select(CsvAlan)));
        foreach (var satir in satirlar)
            sb.AppendLine(string.Join(";", satir.Select(CsvAlan)));
        var bom = new byte[] { 0xEF, 0xBB, 0xBF }; // Excel Türkçe uyumu
        var govde = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        return File(bom.Concat(govde).ToArray(), "text/csv", dosyaAdi);
    }

    // ─── Stok Raporu CSV ──────────────────────────────────────
    public async Task<IActionResult> StokCsv()
    {
        var depolar = await _svc.DepolariGetirAsync();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        var kurumlar = await _svc.KurumlariGetirAsync();
        if (!Bakanlik) depolar = depolar.Where(d => d.KurumId == KurumId).ToList();

        var satirlar = new List<IEnumerable<object?>>();
        foreach (var d in depolar)
        {
            var kurum = kurumlar.FirstOrDefault(k => k.Id == d.KurumId);
            foreach (var s in d.Stoklar)
            {
                var t = tanimlar.FirstOrDefault(x => x.Id == s.TasinirTanimId);
                satirlar.Add(new object?[] { kurum?.Ad, d.Ad, t?.Kod, t?.Ad, t?.Kategori, s.Miktar, t?.Birim,
                    s.MinEsik, s.BirimMaliyet, s.Miktar * s.BirimMaliyet,
                    (s.MinEsik > 0 && s.Miktar <= s.MinEsik) ? "KRİTİK" : "" });
            }
        }
        return CsvDosya(
            new[] { "Kurum", "Depo", "Hesap Kodu", "Taşınır", "Kategori", "Miktar", "Birim", "Min Eşik", "Birim Maliyet", "Toplam Değer", "Durum" },
            satirlar, $"stok-raporu-{DateTime.Now:yyyyMMdd}.csv");
    }

    // ─── Taşınır Kayıt (Envanter) CSV ─────────────────────────
    public async Task<IActionResult> EnvanterCsv()
    {
        var kayitlar = await _svc.TasinirKayitlariGetirAsync();
        if (!Bakanlik) kayitlar = kayitlar.Where(k => k.KurumId == KurumId).ToList();
        var satirlar = kayitlar.Select(k => (IEnumerable<object?>)new object?[]
        {
            k.BarKod, k.SicilNo, k.SeriNo, k.Cinsi, k.MarkaAdi, k.Modeli, k.BirimFiyat,
            k.AmbarAdi, k.HarBirimiAdi, k.IlAdi, k.IlKodu, k.FisNo, k.Durum,
            k.KurumGirisTarihi?.ToString("dd.MM.yyyy"), k.VerildigiYerBirim, k.TcNumarasi
        });
        return CsvDosya(
            new[] { "Barkod", "Sicil No", "Seri No", "Cinsi", "Marka", "Model", "Birim Fiyat",
                "Ambar", "Harcama Birimi", "İl", "İl Kodu", "Fiş No", "Durum", "Giriş Tarihi", "Verildiği Yer", "TC No" },
            satirlar, $"envanter-{DateTime.Now:yyyyMMdd}.csv");
    }

    // ─── Personel Zimmet Raporu ───────────────────────────────
    public async Task<IActionResult> ZimmetRaporu()
    {
        var zimmetler = await _svc.ZimmetleriGetirAsync();
        var kullanicilar = await _svc.KullanicilariGetirAsync();
        var depolar = await _svc.DepolariGetirAsync();
        if (!Bakanlik)
        {
            var kd = depolar.Where(d => d.KurumId == KurumId).Select(d => d.Id).ToHashSet();
            zimmetler = zimmetler.Where(z => kd.Contains(z.DepoId)).ToList();
        }
        ViewBag.Kullanicilar = kullanicilar.ToDictionary(k => k.Id, k => k.AdSoyad);
        ViewBag.Depolar = depolar.ToDictionary(d => d.Id, d => d.Ad);
        return View(zimmetler.OrderByDescending(z => z.ZimmetTarihi).ToList());
    }

    // ─── Stok Hareket Dökümü ──────────────────────────────────
    public async Task<IActionResult> StokHareketDokumu(string? depoId = null)
    {
        var hareketler = await _svc.StokHareketleriGetirAsync();
        var depolar = await _svc.DepolariGetirAsync();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        if (!Bakanlik)
        {
            var kd = depolar.Where(d => d.KurumId == KurumId).Select(d => d.Id).ToHashSet();
            hareketler = hareketler.Where(h => kd.Contains(h.DepoId)).ToList();
        }
        if (!string.IsNullOrEmpty(depoId)) hareketler = hareketler.Where(h => h.DepoId == depoId).ToList();

        ViewBag.Depolar = depolar.ToDictionary(d => d.Id, d => d.Ad);
        ViewBag.Tanimlar = tanimlar.ToDictionary(t => t.Id, t => t.Ad);
        ViewBag.DepoListesi = (Bakanlik ? depolar : depolar.Where(d => d.KurumId == KurumId)).ToList();
        ViewBag.DepoId = depoId;
        return View(hareketler.OrderByDescending(h => h.Tarih).Take(1000).ToList());
    }

    public async Task<IActionResult> Index()
    {
        var depolar = await _svc.DepolariGetirAsync();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        var zimmetler = await _svc.ZimmetleriGetirAsync();
        var ihaleler = await _svc.IhaleleriGetirAsync();
        var talepler = await _svc.TalepleriGetirAsync();
        var bakimlar = await _svc.BakimKayitlariGetirAsync();
        var kurumlar = await _svc.KurumlariGetirAsync();

        if (!AtomRoller.BakanlikRolleri.Contains(Rol))
        {
            depolar = depolar.Where(d => d.KurumId == KurumId).ToList();
            talepler = talepler.Where(t => t.TalepciKurumId == KurumId).ToList();
        }

        var vm = new RaporlamaViewModel
        {
            // Stok raporu
            StokRaporu = depolar.Select(d => new DepoStokRapor
            {
                DepoAdi = d.Ad,
                KurumAdi = kurumlar.FirstOrDefault(k => k.Id == d.KurumId)?.Ad ?? "",
                Satirlar = d.Stoklar.Select(s => new StokSatir
                {
                    TasinirAdi = tanimlar.FirstOrDefault(t => t.Id == s.TasinirTanimId)?.Ad ?? s.TasinirTanimId,
                    Kategori = tanimlar.FirstOrDefault(t => t.Id == s.TasinirTanimId)?.Kategori.ToString() ?? "",
                    Miktar = s.Miktar,
                    MinEsik = s.MinEsik,
                    BirimMaliyet = s.BirimMaliyet,
                    ToplamDeger = s.Miktar * s.BirimMaliyet,
                    KritikMi = s.Miktar <= s.MinEsik && s.MinEsik > 0
                }).ToList()
            }).ToList(),

            // İhale özeti
            IhaleOzeti = new IhaleOzetiRapor
            {
                Toplam = ihaleler.Count,
                Aktif = ihaleler.Count(i => i.Durum == IhaleDurumu.TeklifAliniyor || i.Durum == IhaleDurumu.IlanEdildi),
                Tamamlanan = ihaleler.Count(i => i.Durum == IhaleDurumu.KapandiTamamlandi),
                ToplamButce = ihaleler.Sum(i => i.TahminiButce),
                GerceklesmeOrani = ihaleler.Count == 0 ? 0 :
                    ihaleler.Count(i => i.Durum == IhaleDurumu.KapandiTamamlandi) * 100.0 / ihaleler.Count
            },

            // Talep özeti
            TalepOzeti = new TalepOzetiRapor
            {
                Toplam = talepler.Count,
                Bekleyen = talepler.Count(t => t.Durum == TalepDurumu.BakanlikAlindi || t.Durum == TalepDurumu.BakanlikInceliyor),
                Karsilanan = talepler.Count(t => t.Durum == TalepDurumu.KarsilandiTamamen || t.Durum == TalepDurumu.KarsilandiKismi),
                Reddedilen = talepler.Count(t => t.Durum == TalepDurumu.Reddedildi)
            },

            // Bakım özeti
            BakimOzeti = new BakimOzetiRapor
            {
                Toplam = bakimlar.Count,
                Acik = bakimlar.Count(b => b.Durum == BakimDurumu.Acik),
                Tamamlanan = bakimlar.Count(b => b.Durum == BakimDurumu.Tamamlandi),
                ToplamMaliyet = bakimlar.Where(b => b.BakimMaliyeti.HasValue).Sum(b => b.BakimMaliyeti!.Value),
                GarantiKapsaminda = bakimlar.Count(b => b.GarantiKapsaminaMi)
            },

            // Il bazlı stok karşılaştırma
            IlBazliStok = kurumlar.Where(k => k.Tur == KurumTur.IlMudurlugu).Select(k =>
            {
                var kurumDepolar = depolar.Where(d => d.KurumId == k.Id).ToList();
                return new IlStokKarsilastirma
                {
                    IlAdi = k.Il,
                    ToplamAdet = kurumDepolar.SelectMany(d => d.Stoklar).Sum(s => s.Miktar),
                    ToplamDeger = kurumDepolar.SelectMany(d => d.Stoklar).Sum(s => s.Miktar * s.BirimMaliyet),
                    KritikSayisi = kurumDepolar.SelectMany(d => d.Stoklar).Count(s => s.Miktar <= s.MinEsik && s.MinEsik > 0)
                };
            }).OrderByDescending(i => i.ToplamAdet).ToList()
        };

        return View(vm);
    }
}

public class RaporlamaViewModel
{
    public List<DepoStokRapor> StokRaporu { get; set; } = new();
    public IhaleOzetiRapor IhaleOzeti { get; set; } = new();
    public TalepOzetiRapor TalepOzeti { get; set; } = new();
    public BakimOzetiRapor BakimOzeti { get; set; } = new();
    public List<IlStokKarsilastirma> IlBazliStok { get; set; } = new();
}

public class DepoStokRapor
{
    public string DepoAdi { get; set; } = "";
    public string KurumAdi { get; set; } = "";
    public List<StokSatir> Satirlar { get; set; } = new();
    public int ToplamAdet => Satirlar.Sum(s => s.Miktar);
    public decimal ToplamDeger => Satirlar.Sum(s => s.ToplamDeger);
    public int KritikSayisi => Satirlar.Count(s => s.KritikMi);
}

public class StokSatir
{
    public string TasinirAdi { get; set; } = "";
    public string Kategori { get; set; } = "";
    public int Miktar { get; set; }
    public int MinEsik { get; set; }
    public decimal BirimMaliyet { get; set; }
    public decimal ToplamDeger { get; set; }
    public bool KritikMi { get; set; }
}

public class IhaleOzetiRapor
{
    public int Toplam { get; set; }
    public int Aktif { get; set; }
    public int Tamamlanan { get; set; }
    public decimal ToplamButce { get; set; }
    public double GerceklesmeOrani { get; set; }
}

public class TalepOzetiRapor
{
    public int Toplam { get; set; }
    public int Bekleyen { get; set; }
    public int Karsilanan { get; set; }
    public int Reddedilen { get; set; }
}

public class BakimOzetiRapor
{
    public int Toplam { get; set; }
    public int Acik { get; set; }
    public int Tamamlanan { get; set; }
    public decimal ToplamMaliyet { get; set; }
    public int GarantiKapsaminda { get; set; }
}

public class IlStokKarsilastirma
{
    public string IlAdi { get; set; } = "";
    public int ToplamAdet { get; set; }
    public decimal ToplamDeger { get; set; }
    public int KritikSayisi { get; set; }
}
