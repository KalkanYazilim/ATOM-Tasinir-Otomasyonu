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
    private readonly BelgeService _belge;
    public HomeController(IAtomDataService svc, BelgeService belge)
    {
        _svc = svc; _belge = belge;
    }

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

    // ─── Stok Raporu Excel ────────────────────────────────────
    public async Task<IActionResult> StokExcel()
    {
        var depolar = await _svc.DepolariGetirAsync();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        var kurumlar = await _svc.KurumlariGetirAsync();
        if (!Bakanlik) depolar = depolar.Where(d => d.KurumId == KurumId).ToList();

        var satirlar = new List<IList<object?>>();
        foreach (var d in depolar)
        {
            var kurum = kurumlar.FirstOrDefault(k => k.Id == d.KurumId);
            foreach (var s in d.Stoklar)
            {
                var t = tanimlar.FirstOrDefault(x => x.Id == s.TasinirTanimId);
                satirlar.Add(new List<object?> { kurum?.Ad, d.Ad, t?.Kod, t?.Ad, t != null ? TasinirKategoriHelper.DisplayName(t.Kategori) : "",
                    s.Miktar, t?.Birim, s.MinEsik, s.BirimMaliyet, s.Miktar * s.BirimMaliyet,
                    (s.MinEsik > 0 && s.Miktar <= s.MinEsik) ? "KRİTİK" : "Normal" });
            }
        }
        var bytes = _belge.ExcelTablo("Stok Raporu",
            new List<string> { "Kurum", "Depo", "Hesap Kodu", "Taşınır", "Kategori", "Miktar", "Birim", "Min Eşik", "Birim Maliyet", "Toplam Değer", "Durum" },
            satirlar);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"stok-raporu-{DateTime.Now:yyyyMMdd}.xlsx");
    }

    // ─── Stok Raporu PDF ──────────────────────────────────────
    public async Task<IActionResult> StokPdf()
    {
        var depolar = await _svc.DepolariGetirAsync();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        if (!Bakanlik) depolar = depolar.Where(d => d.KurumId == KurumId).ToList();

        var satirlar = new List<IList<string>>();
        foreach (var d in depolar)
            foreach (var s in d.Stoklar)
            {
                var t = tanimlar.FirstOrDefault(x => x.Id == s.TasinirTanimId);
                satirlar.Add(new List<string> { d.Ad, t?.Ad ?? "", s.Miktar.ToString(), t?.Birim ?? "",
                    s.BirimMaliyet.ToString("N2"), (s.Miktar * s.BirimMaliyet).ToString("N2") });
            }
        var bytes = _belge.PdfTablo("AMBAR STOK / SAYIM LİSTESİ", $"Tarih: {DateTime.Now:dd.MM.yyyy}",
            new List<string> { "Depo", "Taşınır", "Miktar", "Birim", "Birim Maliyet", "Toplam Değer" },
            satirlar, "Dayanak: Taşınır Mal Yönetmeliği – Ambar Stok / Taşınır İcmal Cetveli");
        return File(bytes, "application/pdf", $"stok-raporu-{DateTime.Now:yyyyMMdd}.pdf");
    }

    // ─── Taşınır İcmal Cetveli (hesap kodu bazlı, dinamik) ────
    private async Task<List<IcmalSatir>> IcmalVerisiAsync()
    {
        var depolar = await _svc.DepolariGetirAsync();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        if (!Bakanlik) depolar = depolar.Where(d => d.KurumId == KurumId).ToList();

        // Hesap kodunun ilk 3 segmenti = taşınır hesap grubu (ör. 255.01.01 → 255 Demirbaşlar)
        return depolar.SelectMany(d => d.Stoklar)
            .GroupBy(s => tanimlar.FirstOrDefault(t => t.Id == s.TasinirTanimId)?.Kod ?? "Tanımsız")
            .Select(g =>
            {
                var tanim = tanimlar.FirstOrDefault(t => t.Id == g.First().TasinirTanimId);
                return new IcmalSatir
                {
                    HesapKodu = g.Key,
                    Ad = tanim?.Ad ?? g.Key,
                    Kategori = tanim != null ? TasinirKategoriHelper.DisplayName(tanim.Kategori) : "",
                    ToplamMiktar = g.Sum(x => x.Miktar),
                    ToplamDeger = g.Sum(x => x.Miktar * x.BirimMaliyet)
                };
            }).OrderBy(x => x.HesapKodu).ToList();
    }

    public async Task<IActionResult> IcmalCetveli()
    {
        var veri = await IcmalVerisiAsync();
        ViewBag.GenelToplam = veri.Sum(x => x.ToplamDeger);
        return View(veri);
    }

    public async Task<IActionResult> IcmalCetveliExcel()
    {
        var veri = await IcmalVerisiAsync();
        var basliklar = new List<string> { "Hesap Kodu", "Taşınır", "Kategori", "Toplam Miktar", "Toplam Değer (₺)" };
        var satirlar = veri.Select(x => (IList<object?>)new List<object?> { x.HesapKodu, x.Ad, x.Kategori, x.ToplamMiktar, x.ToplamDeger });
        var bytes = _belge.ExcelTablo("Taşınır İcmal", basliklar, satirlar);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"tasinir-icmal-{DateTime.Now:yyyyMMdd}.xlsx");
    }

    public async Task<IActionResult> IcmalCetveliWord()
    {
        var veri = await IcmalVerisiAsync();
        var basliklar = new List<string> { "Hesap Kodu", "Taşınır", "Miktar", "Değer (₺)" };
        var satirlar = veri.Select(x => (IList<string>)new List<string> { x.HesapKodu, x.Ad, x.ToplamMiktar.ToString("N0"), x.ToplamDeger.ToString("N2") });
        var bytes = _belge.WordTablo("TAŞINIR İCMAL CETVELİ",
            $"Genel Toplam: {veri.Sum(x => x.ToplamDeger):N2} ₺ · Tarih: {DateTime.Now:dd.MM.yyyy}",
            basliklar, satirlar, "Dayanak: Taşınır Mal Yönetmeliği – Taşınır Yönetim Hesabı Cetvelleri (5018 sayılı Kanun)");
        return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"tasinir-icmal-{DateTime.Now:yyyyMMdd}.docx");
    }

    // ─── Taşınır Yönetim Hesabı Cetveli (yıl/dönem bazlı) ─────
    private async Task<List<YonetimHesabiSatir>> YonetimHesabiVerisiAsync(int yil)
    {
        var depolar = await _svc.DepolariGetirAsync();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        var hareketler = await _svc.StokHareketleriGetirAsync();

        if (!Bakanlik)
        {
            depolar = depolar.Where(d => d.KurumId == KurumId).ToList();
            var yetkiliDepolar = depolar.Select(d => d.Id).ToHashSet();
            hareketler = hareketler.Where(h => yetkiliDepolar.Contains(h.DepoId)).ToList();
        }

        var yilHareketleri = hareketler.Where(h => h.Tarih.Year == yil).ToList();
        var mevcutStok = depolar.SelectMany(d => d.Stoklar)
            .GroupBy(s => s.TasinirTanimId)
            .ToDictionary(g => g.Key, g => new
            {
                Miktar = g.Sum(x => x.Miktar),
                Deger = g.Sum(x => x.Miktar * x.BirimMaliyet)
            });

        var tumTanimIds = mevcutStok.Keys
            .Concat(yilHareketleri.Select(h => h.TasinirTanimId))
            .Distinct()
            .ToList();

        return tumTanimIds.Select(tanimId =>
        {
            var tanim = tanimlar.FirstOrDefault(t => t.Id == tanimId);
            var hareket = yilHareketleri.Where(h => h.TasinirTanimId == tanimId).ToList();
            var girisMiktar = hareket.Sum(h => h.GirisMiktar);
            var cikisMiktar = hareket.Sum(h => h.CikisMiktar);
            var girisDeger = hareket.Where(h => h.GirisMiktar > 0).Sum(h => h.ToplamTutar);
            var cikisDeger = hareket.Where(h => h.CikisMiktar > 0).Sum(h => h.ToplamTutar);
            var donemSonuMiktar = mevcutStok.TryGetValue(tanimId, out var stok) ? stok.Miktar : 0;
            var donemSonuDeger = stok?.Deger ?? 0m;

            return new YonetimHesabiSatir
            {
                HesapKodu = tanim?.Kod ?? "Tanımsız",
                TasinirAdi = tanim?.Ad ?? tanimId,
                Kategori = tanim != null ? TasinirKategoriHelper.DisplayName(tanim.Kategori) : "",
                Birim = tanim?.Birim ?? "",
                DevredenMiktar = donemSonuMiktar - girisMiktar + cikisMiktar,
                DevredenDeger = donemSonuDeger - girisDeger + cikisDeger,
                GirisMiktar = girisMiktar,
                GirisDeger = girisDeger,
                CikisMiktar = cikisMiktar,
                CikisDeger = cikisDeger,
                DonemSonuMiktar = donemSonuMiktar,
                DonemSonuDeger = donemSonuDeger
            };
        }).OrderBy(x => x.HesapKodu).ThenBy(x => x.TasinirAdi).ToList();
    }

    public async Task<IActionResult> YonetimHesabiCetveli(int? yil = null)
    {
        var seciliYil = yil ?? DateTime.Now.Year;
        var veri = await YonetimHesabiVerisiAsync(seciliYil);
        ViewBag.Yil = seciliYil;
        ViewBag.Yillar = Enumerable.Range(DateTime.Now.Year - 5, 7).OrderByDescending(x => x).ToList();
        return View(veri);
    }

    public async Task<IActionResult> YonetimHesabiExcel(int? yil = null)
    {
        var seciliYil = yil ?? DateTime.Now.Year;
        var veri = await YonetimHesabiVerisiAsync(seciliYil);
        var basliklar = new List<string>
        {
            "Hesap Kodu", "Taşınır", "Kategori", "Birim", "Devreden Miktar", "Devreden Değer",
            "Yıl İçi Giriş", "Giriş Değeri", "Yıl İçi Çıkış", "Çıkış Değeri", "Dönem Sonu Miktar", "Dönem Sonu Değer"
        };
        var satirlar = veri.Select(x => (IList<object?>)new List<object?>
        {
            x.HesapKodu, x.TasinirAdi, x.Kategori, x.Birim, x.DevredenMiktar, x.DevredenDeger,
            x.GirisMiktar, x.GirisDeger, x.CikisMiktar, x.CikisDeger, x.DonemSonuMiktar, x.DonemSonuDeger
        });
        var bytes = _belge.ExcelTablo("Taşınır Yönetim Hesabı", basliklar, satirlar);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"tasinir-yonetim-hesabi-{seciliYil}.xlsx");
    }

    public async Task<IActionResult> YonetimHesabiWord(int? yil = null)
    {
        var seciliYil = yil ?? DateTime.Now.Year;
        var veri = await YonetimHesabiVerisiAsync(seciliYil);
        var basliklar = new List<string>
        {
            "Hesap Kodu", "Taşınır", "Devreden", "Giriş", "Çıkış", "Dönem Sonu", "Dönem Sonu Değer"
        };
        var satirlar = veri.Select(x => (IList<string>)new List<string>
        {
            x.HesapKodu, x.TasinirAdi, x.DevredenMiktar.ToString("N0"), x.GirisMiktar.ToString("N0"),
            x.CikisMiktar.ToString("N0"), x.DonemSonuMiktar.ToString("N0"), x.DonemSonuDeger.ToString("N2")
        });
        var bytes = _belge.WordTablo("TAŞINIR YÖNETİM HESABI CETVELİ",
            $"{seciliYil} yılı · Toplam dönem sonu değer: {veri.Sum(x => x.DonemSonuDeger):N2} ₺",
            basliklar, satirlar, "Dayanak: 5018 sayılı Kanun, Taşınır Mal Yönetmeliği ve taşınır yönetim hesabı cetvelleri.");
        return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"tasinir-yonetim-hesabi-{seciliYil}.docx");
    }

    // ─── Muhasebe İcmali (hesap kodu bazlı borç/alacak) ───────
    private async Task<List<MuhasebeIcmalSatir>> MuhasebeIcmalVerisiAsync(int yil)
    {
        var depolar = await _svc.DepolariGetirAsync();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        var hareketler = await _svc.StokHareketleriGetirAsync();

        if (!Bakanlik)
        {
            var yetkiliDepolar = depolar.Where(d => d.KurumId == KurumId).Select(d => d.Id).ToHashSet();
            hareketler = hareketler.Where(h => yetkiliDepolar.Contains(h.DepoId)).ToList();
        }

        return hareketler.Where(h => h.Tarih.Year == yil)
            .GroupBy(h =>
            {
                var tanim = tanimlar.FirstOrDefault(t => t.Id == h.TasinirTanimId);
                return new
                {
                    HesapKodu = tanim?.Kod ?? "Tanımsız",
                    HesapAdi = tanim?.Kategori == TasinirKategori.Kirtasiye ? "Tüketim Malzemeleri" : "Dayanıklı Taşınırlar",
                    Kategori = tanim != null ? TasinirKategoriHelper.DisplayName(tanim.Kategori) : ""
                };
            })
            .Select(g => new MuhasebeIcmalSatir
            {
                HesapKodu = g.Key.HesapKodu,
                HesapAdi = g.Key.HesapAdi,
                Kategori = g.Key.Kategori,
                GirisMiktar = g.Sum(x => x.GirisMiktar),
                CikisMiktar = g.Sum(x => x.CikisMiktar),
                Borc = g.Where(x => x.GirisMiktar > 0).Sum(x => x.ToplamTutar),
                Alacak = g.Where(x => x.CikisMiktar > 0).Sum(x => x.ToplamTutar)
            })
            .OrderBy(x => x.HesapKodu)
            .ToList();
    }

    public async Task<IActionResult> MuhasebeIcmali(int? yil = null)
    {
        var seciliYil = yil ?? DateTime.Now.Year;
        var veri = await MuhasebeIcmalVerisiAsync(seciliYil);
        ViewBag.Yil = seciliYil;
        ViewBag.Yillar = Enumerable.Range(DateTime.Now.Year - 5, 7).OrderByDescending(x => x).ToList();
        return View(veri);
    }

    public async Task<IActionResult> MuhasebeIcmaliExcel(int? yil = null)
    {
        var seciliYil = yil ?? DateTime.Now.Year;
        var veri = await MuhasebeIcmalVerisiAsync(seciliYil);
        var basliklar = new List<string> { "Hesap Kodu", "Hesap Adı", "Kategori", "Giriş Miktar", "Çıkış Miktar", "Borç", "Alacak", "Net" };
        var satirlar = veri.Select(x => (IList<object?>)new List<object?>
        {
            x.HesapKodu, x.HesapAdi, x.Kategori, x.GirisMiktar, x.CikisMiktar, x.Borc, x.Alacak, x.Net
        });
        var bytes = _belge.ExcelTablo("Muhasebe İcmali", basliklar, satirlar);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"muhasebe-icmali-{seciliYil}.xlsx");
    }

    public async Task<IActionResult> MuhasebeIcmaliWord(int? yil = null)
    {
        var seciliYil = yil ?? DateTime.Now.Year;
        var veri = await MuhasebeIcmalVerisiAsync(seciliYil);
        var basliklar = new List<string> { "Hesap Kodu", "Hesap Adı", "Giriş", "Çıkış", "Borç", "Alacak", "Net" };
        var satirlar = veri.Select(x => (IList<string>)new List<string>
        {
            x.HesapKodu, x.HesapAdi, x.GirisMiktar.ToString("N0"), x.CikisMiktar.ToString("N0"),
            x.Borc.ToString("N2"), x.Alacak.ToString("N2"), x.Net.ToString("N2")
        });
        var bytes = _belge.WordTablo("TAŞINIR MUHASEBE İCMALİ",
            $"{seciliYil} yılı · Borç: {veri.Sum(x => x.Borc):N2} ₺ · Alacak: {veri.Sum(x => x.Alacak):N2} ₺",
            basliklar, satirlar,
            "Dayanak: Genel Yönetim Muhasebe Yönetmeliği, Merkezî Yönetim Muhasebe Yönetmeliği ve Taşınır Mal Yönetmeliği.");
        return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"muhasebe-icmali-{seciliYil}.docx");
    }

    // ─── Tedarikçi Performans Raporu ─────────────────────────
    public async Task<IActionResult> TedarikciPerformans()
    {
        var firmalar = await _svc.FirmalariGetirAsync();
        var ihaleler = await _svc.IhaleleriGetirAsync();

        var veri = firmalar.Select(f =>
        {
            var teklifliIhaleler = ihaleler.Where(i => i.Teklifler.Any(t => t.FirmaId == f.Id)).ToList();
            var kazanilan = ihaleler.Count(i => i.KazananFirmaId == f.Id);
            var teklifSayisi = teklifliIhaleler.Sum(i => i.Teklifler.Count(t => t.FirmaId == f.Id));
            var kabulTeklifleri = teklifliIhaleler.SelectMany(i => i.Teklifler).Where(t => t.FirmaId == f.Id && t.Durum == TeklifDurumu.Kabul).ToList();
            return new TedarikciPerformansSatir
            {
                FirmaId = f.Id,
                FirmaAdi = f.Ad,
                VergiNo = f.VergiNo,
                PuanOrtalama = f.PuanOrtalama,
                TamamlananIhale = f.TamamlananIhale,
                TeklifSayisi = teklifSayisi,
                KazanilanIhale = kazanilan,
                KazanmaOrani = teklifliIhaleler.Count == 0 ? 0 : kazanilan * 100.0 / teklifliIhaleler.Count,
                KabulEdilenTeklifTutari = kabulTeklifleri.Sum(t => t.ToplamTutar)
            };
        }).OrderByDescending(x => x.KazanilanIhale).ThenByDescending(x => x.PuanOrtalama).ToList();

        return View(veri);
    }

    // ─── Talep Karşılama Süresi Raporu ───────────────────────
    public async Task<IActionResult> TalepKarsilamaSuresi()
    {
        var talepler = await _svc.TalepleriGetirAsync();
        var kurumlar = await _svc.KurumlariGetirAsync();
        if (!Bakanlik) talepler = talepler.Where(t => t.TalepciKurumId == KurumId).ToList();

        var veri = talepler.Select(t => new TalepKarsilamaSatir
        {
            TalepNo = t.TalepNo,
            KurumAdi = kurumlar.FirstOrDefault(k => k.Id == t.TalepciKurumId)?.Ad ?? t.TalepciKurumId,
            TalepTarihi = t.TalepTarihi,
            KapanmaTarihi = t.KapanmaTarihi,
            Durum = t.Durum.ToString(),
            Oncelik = t.OncelikSeviyesi,
            KalemSayisi = t.Kalemler.Count,
            ToplamMiktar = t.Kalemler.Sum(k => k.TalepMiktari),
            KarsilananMiktar = t.Kalemler.Sum(k => k.KarsilananMiktar)
        }).OrderByDescending(x => x.TalepTarihi).ToList();

        ViewBag.OrtalamaGun = veri.Where(x => x.KarsilamaGun.HasValue).Select(x => x.KarsilamaGun!.Value).DefaultIfEmpty(0).Average();
        return View(veri);
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
                satirlar.Add(new object?[] { kurum?.Ad, d.Ad, t?.Kod, t?.Ad, t != null ? TasinirKategoriHelper.DisplayName(t.Kategori) : "", s.Miktar, t?.Birim,
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
                    Kategori = tanimlar.FirstOrDefault(t => t.Id == s.TasinirTanimId) is { } tanim ? TasinirKategoriHelper.DisplayName(tanim.Kategori) : "",
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

public class IcmalSatir
{
    public string HesapKodu { get; set; } = "";
    public string Ad { get; set; } = "";
    public string Kategori { get; set; } = "";
    public int ToplamMiktar { get; set; }
    public decimal ToplamDeger { get; set; }
}

public class YonetimHesabiSatir
{
    public string HesapKodu { get; set; } = "";
    public string TasinirAdi { get; set; } = "";
    public string Kategori { get; set; } = "";
    public string Birim { get; set; } = "";
    public int DevredenMiktar { get; set; }
    public decimal DevredenDeger { get; set; }
    public int GirisMiktar { get; set; }
    public decimal GirisDeger { get; set; }
    public int CikisMiktar { get; set; }
    public decimal CikisDeger { get; set; }
    public int DonemSonuMiktar { get; set; }
    public decimal DonemSonuDeger { get; set; }
}

public class MuhasebeIcmalSatir
{
    public string HesapKodu { get; set; } = "";
    public string HesapAdi { get; set; } = "";
    public string Kategori { get; set; } = "";
    public int GirisMiktar { get; set; }
    public int CikisMiktar { get; set; }
    public decimal Borc { get; set; }
    public decimal Alacak { get; set; }
    public decimal Net => Borc - Alacak;
}

public class TedarikciPerformansSatir
{
    public string FirmaId { get; set; } = "";
    public string FirmaAdi { get; set; } = "";
    public string VergiNo { get; set; } = "";
    public double PuanOrtalama { get; set; }
    public int TamamlananIhale { get; set; }
    public int TeklifSayisi { get; set; }
    public int KazanilanIhale { get; set; }
    public double KazanmaOrani { get; set; }
    public decimal KabulEdilenTeklifTutari { get; set; }
}

public class TalepKarsilamaSatir
{
    public string TalepNo { get; set; } = "";
    public string KurumAdi { get; set; } = "";
    public DateTime TalepTarihi { get; set; }
    public DateTime? KapanmaTarihi { get; set; }
    public string Durum { get; set; } = "";
    public string Oncelik { get; set; } = "";
    public int KalemSayisi { get; set; }
    public int ToplamMiktar { get; set; }
    public int KarsilananMiktar { get; set; }
    public int? KarsilamaGun => KapanmaTarihi.HasValue ? (int)(KapanmaTarihi.Value.Date - TalepTarihi.Date).TotalDays : null;
    public double KarsilamaOrani => ToplamMiktar == 0 ? 0 : KarsilananMiktar * 100.0 / ToplamMiktar;
}
