using System.Security.Claims;
using ATOM.Models.Accounts;
using ATOM.Models.Domain;
using ATOM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATOM.Areas.Kayit.Controllers;

[Area("Kayit")]
[Authorize]
public class HomeController : Controller
{
    private readonly IAtomDataService _svc;
    private readonly IDosyaService _dosya;
    private readonly BelgeService _belge;
    public HomeController(IAtomDataService svc, IDosyaService dosya, BelgeService belge)
    {
        _svc = svc; _dosya = dosya; _belge = belge;
    }

    private string KullaniciId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private string KullaniciAdSoyad => User.FindFirstValue("AdSoyad") ?? User.Identity?.Name ?? "";
    private string Rol => User.FindFirstValue(ClaimTypes.Role)!;
    private string KurumId => User.FindFirstValue("KurumId")!;

    // ─── Liste + Arama + Filtre ───────────────────────────────
    public async Task<IActionResult> Index(string? ara = null, string? durum = null,
        string? ilKodu = null, string? harBirimiKodu = null, int sayfa = 1)
    {
        var kayitlar = await _svc.TasinirKayitlariGetirAsync();
        var depolar = await _svc.DepolariGetirAsync();

        // Kurum bazlı görünürlük
        if (!AtomRoller.BakanlikRolleri.Contains(Rol))
            kayitlar = kayitlar.Where(k => k.KurumId == KurumId).ToList();

        if (!string.IsNullOrWhiteSpace(ara))
        {
            var q = ara.Trim().ToLowerInvariant();
            kayitlar = kayitlar.Where(k =>
                k.BarKod.ToLower().Contains(q) ||
                k.SicilNo.ToLower().Contains(q) ||
                k.SeriNo.ToLower().Contains(q) ||
                k.Aciklama.ToLower().Contains(q) ||
                k.Cinsi.ToLower().Contains(q) ||
                k.MarkaAdi.ToLower().Contains(q) ||
                k.TcNumarasi.Contains(q)).ToList();
        }

        if (!string.IsNullOrEmpty(durum) && Enum.TryParse<TasinirKayitDurumu>(durum, out var d))
            kayitlar = kayitlar.Where(k => k.Durum == d).ToList();

        if (!string.IsNullOrEmpty(ilKodu))
            kayitlar = kayitlar.Where(k => k.IlKodu == ilKodu).ToList();

        if (!string.IsNullOrEmpty(harBirimiKodu))
            kayitlar = kayitlar.Where(k => k.HarBirimiKodu == harBirimiKodu).ToList();

        var sirali = kayitlar.OrderByDescending(k => k.GuncellemeTarihi).ToList();

        // Sayfalama
        const int sayfaBoyut = 25;
        var toplam = sirali.Count;
        var sayfalanmis = sirali.Skip((sayfa - 1) * sayfaBoyut).Take(sayfaBoyut).ToList();

        ViewBag.Depolar = depolar.ToDictionary(x => x.Id, x => x.Ad);
        ViewBag.Ara = ara;
        ViewBag.Durum = durum;
        ViewBag.IlKodu = ilKodu;
        ViewBag.HarBirimiKodu = harBirimiKodu;
        ViewBag.Sayfa = sayfa;
        ViewBag.ToplamSayfa = (int)Math.Ceiling(toplam / (double)sayfaBoyut);
        ViewBag.ToplamKayit = toplam;
        ViewBag.ToplamDeger = sirali.Sum(k => k.BirimFiyat);

        // Filtre seçenekleri
        ViewBag.IlListesi = (await _svc.TasinirKayitlariGetirAsync())
            .Where(k => !string.IsNullOrEmpty(k.IlKodu))
            .GroupBy(k => k.IlKodu).Select(g => new { Kod = g.Key, Ad = g.First().IlAdi })
            .OrderBy(x => x.Ad).ToList();

        return View(sayfalanmis);
    }

    // ─── Detay ────────────────────────────────────────────────
    public async Task<IActionResult> Detay(string id)
    {
        var kayit = await _svc.TasinirKayitGetirAsync(id);
        if (kayit == null) return NotFound();
        if (!AtomRoller.BakanlikRolleri.Contains(Rol) && kayit.KurumId != KurumId) return Forbid();
        return View(kayit);
    }

    // ─── Yeni / Düzenle ───────────────────────────────────────
    [HttpGet]
    [Authorize(Roles = $"{AtomRoller.MerkezDepoSorumlusu},{AtomRoller.IlDepoSorumlusu},{AtomRoller.SistemAdmin},{AtomRoller.BakanlikMerkez}")]
    public async Task<IActionResult> Duzenle(string? id = null)
    {
        await DropdownDoldur();
        if (id != null)
        {
            var kayit = await _svc.TasinirKayitGetirAsync(id);
            if (kayit == null) return NotFound();
            return View(kayit);
        }
        return View(new TasinirKayit
        {
            KurumId = KurumId,
            IlkGirisTarihi = DateTime.UtcNow,
            KurumGirisTarihi = DateTime.UtcNow,
            Tarih = DateTime.UtcNow
        });
    }

    [HttpPost]
    [Authorize(Roles = $"{AtomRoller.MerkezDepoSorumlusu},{AtomRoller.IlDepoSorumlusu},{AtomRoller.SistemAdmin},{AtomRoller.BakanlikMerkez}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Duzenle(TasinirKayit model, IFormFile? resim)
    {
        var mevcut = await _svc.TasinirKayitGetirAsync(model.Id);
        var yeniMi = mevcut == null;

        // Resim yükleme
        var resimUrl = await _dosya.ResimKaydetAsync(resim, "tasinir");
        if (resimUrl != null)
        {
            model.ResimUrl = resimUrl;
            model.Resimler ??= new();
            model.Resimler.Add(resimUrl);
        }
        else if (!yeniMi)
        {
            model.ResimUrl = mevcut!.ResimUrl;
            model.Resimler = mevcut.Resimler;
        }

        model.GuncellemeTarihi = DateTime.UtcNow;
        if (yeniMi)
        {
            model.OlusturmaTarihi = DateTime.UtcNow;
            model.HareketGecmisi.Add(new TasinirHareket
            {
                IslemTuru = "Kayıt Oluşturma",
                Aciklama = "Taşınır kayda alındı.",
                KullaniciId = KullaniciId,
                KullaniciAdi = KullaniciAdSoyad,
                YeniDurum = model.Durum.ToString()
            });
        }
        else
        {
            model.OlusturmaTarihi = mevcut!.OlusturmaTarihi;
            model.HareketGecmisi = mevcut.HareketGecmisi;
            if (mevcut.Durum != model.Durum)
            {
                model.HareketGecmisi.Add(new TasinirHareket
                {
                    IslemTuru = "Durum Değişikliği",
                    KullaniciId = KullaniciId,
                    KullaniciAdi = KullaniciAdSoyad,
                    OncekiDurum = mevcut.Durum.ToString(),
                    YeniDurum = model.Durum.ToString()
                });
            }
        }

        await _svc.TasinirKayitKaydetAsync(model);
        TempData["Basari"] = yeniMi ? "Taşınır kaydı oluşturuldu." : "Taşınır kaydı güncellendi.";
        return RedirectToAction(nameof(Detay), new { id = model.Id });
    }

    // ─── Etiket (barkod + QR yazdırılabilir) ──────────────────
    public async Task<IActionResult> Etiket(string id)
    {
        var kayit = await _svc.TasinirKayitGetirAsync(id);
        if (kayit == null) return NotFound();
        if (!AtomRoller.BakanlikRolleri.Contains(Rol) && kayit.KurumId != KurumId) return Forbid();
        return View(kayit);
    }

    // ─── Excel Export ─────────────────────────────────────────
    public async Task<IActionResult> ExcelExport()
    {
        var kayitlar = await _svc.TasinirKayitlariGetirAsync();
        if (!AtomRoller.BakanlikRolleri.Contains(Rol))
            kayitlar = kayitlar.Where(k => k.KurumId == KurumId).ToList();

        var basliklar = new List<string> { "Barkod", "Sicil No", "Seri No", "Cinsi", "Marka", "Model",
            "Birim Fiyat", "Ambar", "Harcama Birimi", "İl", "Fiş No", "Durum", "Giriş Tarihi", "Verildiği Yer", "TC No" };
        var satirlar = kayitlar.Select(k => (IList<object?>)new List<object?>
        {
            k.BarKod, k.SicilNo, k.SeriNo, k.Cinsi, k.MarkaAdi, k.Modeli, k.BirimFiyat, k.AmbarAdi,
            k.HarBirimiAdi, k.IlAdi, k.FisNo, k.Durum.ToString(),
            k.KurumGirisTarihi?.ToString("dd.MM.yyyy"), k.VerildigiYerBirim,
            KvkkHelper.MaskeleTc(k.TcNumarasi, User)
        });
        var bytes = _belge.ExcelTablo("Taşınır Envanteri", basliklar, satirlar);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"envanter-{DateTime.Now:yyyyMMdd}.xlsx");
    }

    // ─── PDF Export ───────────────────────────────────────────
    public async Task<IActionResult> PdfExport()
    {
        var kayitlar = await _svc.TasinirKayitlariGetirAsync();
        if (!AtomRoller.BakanlikRolleri.Contains(Rol))
            kayitlar = kayitlar.Where(k => k.KurumId == KurumId).ToList();

        var basliklar = new List<string> { "Barkod", "Sicil No", "Cinsi", "Marka/Model", "Birim Fiyat", "Ambar", "Durum" };
        var satirlar = kayitlar.Take(500).Select(k => (IList<string>)new List<string>
        {
            k.BarKod, k.SicilNo, k.Cinsi, $"{k.MarkaAdi} {k.Modeli}",
            k.BirimFiyat.ToString("N2"), k.AmbarAdi, k.Durum.ToString()
        });
        var bytes = _belge.PdfTablo("TAŞINIR ENVANTER LİSTESİ",
            $"Kayıt Sayısı: {kayitlar.Count} · Tarih: {DateTime.Now:dd.MM.yyyy}",
            basliklar, satirlar,
            "Dayanak: Taşınır Mal Yönetmeliği (5018 sayılı Kanun) – Dayanıklı Taşınır Listesi", yatay: true);
        return File(bytes, "application/pdf", $"envanter-{DateTime.Now:yyyyMMdd}.pdf");
    }

    // ─── CSV İçe Aktarma (TKYS uyumlu) ────────────────────────
    [HttpGet]
    [Authorize(Roles = $"{AtomRoller.MerkezDepoSorumlusu},{AtomRoller.IlDepoSorumlusu},{AtomRoller.SistemAdmin},{AtomRoller.BakanlikMerkez}")]
    public IActionResult Import() => View();

    [HttpPost]
    [Authorize(Roles = $"{AtomRoller.MerkezDepoSorumlusu},{AtomRoller.IlDepoSorumlusu},{AtomRoller.SistemAdmin},{AtomRoller.BakanlikMerkez}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(IFormFile? dosya, bool onayla = false)
    {
        if (dosya == null || dosya.Length == 0)
        {
            TempData["Hata"] = "Lütfen bir CSV dosyası seçin.";
            return View();
        }

        var satirlar = new List<string>();
        using (var reader = new StreamReader(dosya.OpenReadStream(), System.Text.Encoding.UTF8))
        {
            string? line;
            while ((line = await reader.ReadLineAsync()) != null) satirlar.Add(line);
        }
        if (satirlar.Count < 2)
        {
            TempData["Hata"] = "Dosyada veri satırı yok.";
            return View();
        }

        // Başlık eşleme (esnek: noktalı virgül veya virgül)
        var ayrac = satirlar[0].Contains(';') ? ';' : ',';
        var basliklar = satirlar[0].TrimStart('﻿').Split(ayrac).Select(b => b.Trim().ToLowerInvariant()).ToList();
        int Idx(params string[] adlar) => basliklar.FindIndex(b => adlar.Any(a => b == a));

        int iBarkod = Idx("bar_kod", "barkod"), iSicil = Idx("sicil_no", "sicilno"), iSeri = Idx("seri_no", "serino"),
            iCinsi = Idx("cinsi", "aciklama", "ad"), iMarka = Idx("markaadi", "marka"), iModel = Idx("modeli", "model"),
            iFiyat = Idx("birim_fiyat", "birimfiyat"), iAmbar = Idx("ambar_adi", "ambar"),
            iHarAd = Idx("har_birimi_adi"), iHarKod = Idx("har_birimi_kodu"),
            iIlAd = Idx("iladi", "il"), iIlKod = Idx("ilkoduv", "ilkodu"), iFis = Idx("fis_no", "fisno"),
            iTc = Idx("tc_numarasi", "tcno"), iProje = Idx("projenumarasi", "projeno");

        var onizleme = new List<TasinirKayit>();
        var hatalar = new List<string>();
        var mevcutBarkodlar = (await _svc.TasinirKayitlariGetirAsync()).Select(k => k.BarKod).Where(b => !string.IsNullOrEmpty(b)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var mevcutSiciller = (await _svc.TasinirKayitlariGetirAsync()).Select(k => k.SicilNo).Where(s => !string.IsNullOrEmpty(s)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (int r = 1; r < satirlar.Count; r++)
        {
            if (string.IsNullOrWhiteSpace(satirlar[r])) continue;
            var c = satirlar[r].Split(ayrac);
            string Al(int idx) => (idx >= 0 && idx < c.Length) ? c[idx].Trim().Trim('"') : "";

            var barkod = Al(iBarkod);
            var sicil = Al(iSicil);
            var cinsi = Al(iCinsi);

            if (string.IsNullOrEmpty(cinsi)) { hatalar.Add($"Satır {r + 1}: Cinsi/Açıklama boş, atlandı."); continue; }
            if (!string.IsNullOrEmpty(barkod) && (mevcutBarkodlar.Contains(barkod) || onizleme.Any(o => o.BarKod.Equals(barkod, StringComparison.OrdinalIgnoreCase))))
            { hatalar.Add($"Satır {r + 1}: Barkod '{barkod}' zaten kayıtlı/tekrar, atlandı."); continue; }
            if (!string.IsNullOrEmpty(sicil) && (mevcutSiciller.Contains(sicil) || onizleme.Any(o => o.SicilNo.Equals(sicil, StringComparison.OrdinalIgnoreCase))))
            { hatalar.Add($"Satır {r + 1}: Sicil '{sicil}' zaten kayıtlı/tekrar, atlandı."); continue; }

            decimal.TryParse(Al(iFiyat).Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var fiyat);
            onizleme.Add(new TasinirKayit
            {
                BarKod = barkod, SicilNo = sicil, SeriNo = Al(iSeri), Cinsi = cinsi, Aciklama = cinsi,
                MarkaAdi = Al(iMarka), Modeli = Al(iModel), BirimFiyat = fiyat, AmbarAdi = Al(iAmbar),
                HarBirimiAdi = Al(iHarAd), HarBirimiKodu = Al(iHarKod), IlAdi = Al(iIlAd), IlKodu = Al(iIlKod),
                FisNo = Al(iFis), TcNumarasi = Al(iTc), ProjeNumarasi = Al(iProje),
                KurumId = KurumId, Durum = TasinirKayitDurumu.Ambarda,
                IlkGirisTarihi = DateTime.UtcNow,
                HareketGecmisi = new List<TasinirHareket> { new() { IslemTuru = "CSV İçe Aktarma", Aciklama = "Toplu import", KullaniciId = KullaniciId, KullaniciAdi = KullaniciAdSoyad, YeniDurum = "Ambarda" } }
            });
        }

        if (onayla && onizleme.Count > 0)
        {
            await _svc.TasinirKayitlariTopluKaydetAsync(onizleme);
            TempData["Basari"] = $"{onizleme.Count} kayıt içe aktarıldı. {hatalar.Count} satır atlandı.";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Onizleme = onizleme;
        ViewBag.Hatalar = hatalar;
        ViewBag.DosyaYuklendi = true;
        return View();
    }

    private async Task DropdownDoldur()
    {
        var depolar = await _svc.DepolariGetirAsync();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        if (!AtomRoller.BakanlikRolleri.Contains(Rol))
            depolar = depolar.Where(d => d.KurumId == KurumId).ToList();
        ViewBag.Depolar = depolar;
        ViewBag.Tanimlar = tanimlar;
        ViewBag.Durumlar = Enum.GetValues<TasinirKayitDurumu>();
    }
}
