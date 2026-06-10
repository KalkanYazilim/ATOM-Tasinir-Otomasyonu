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
    public HomeController(IAtomDataService svc) => _svc = svc;

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
    public async Task<IActionResult> Duzenle(TasinirKayit model)
    {
        var mevcut = await _svc.TasinirKayitGetirAsync(model.Id);
        var yeniMi = mevcut == null;

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
