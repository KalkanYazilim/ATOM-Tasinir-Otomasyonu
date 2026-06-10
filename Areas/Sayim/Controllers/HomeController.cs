using System.Security.Claims;
using ATOM.Models.Accounts;
using ATOM.Models.Domain;
using ATOM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATOM.Areas.Sayim.Controllers;

[Area("Sayim")]
[Authorize]
public class HomeController : Controller
{
    private readonly IAtomDataService _svc;
    private readonly IStokService _stok;
    private readonly IBildirimService _bildirim;
    private readonly IAuditService _audit;

    public HomeController(IAtomDataService svc, IStokService stok, IBildirimService bildirim, IAuditService audit)
    {
        _svc = svc; _stok = stok; _bildirim = bildirim; _audit = audit;
    }

    private string KullaniciId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private string AdSoyad => User.FindFirstValue("AdSoyad") ?? User.Identity?.Name ?? "";
    private string Rol => User.FindFirstValue(ClaimTypes.Role)!;
    private string KurumId => User.FindFirstValue("KurumId")!;
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    public async Task<IActionResult> Index()
    {
        var sayimlar = await _svc.SayimlariGetirAsync();
        var depolar = await _svc.DepolariGetirAsync();
        if (!AtomRoller.BakanlikRolleri.Contains(Rol))
            sayimlar = sayimlar.Where(s => s.KurumId == KurumId).ToList();
        ViewBag.Depolar = depolar.ToDictionary(d => d.Id, d => d.Ad);
        return View(sayimlar.OrderByDescending(s => s.BaslangicTarihi).ToList());
    }

    [HttpGet]
    [Authorize(Roles = $"{AtomRoller.IlDepoSorumlusu},{AtomRoller.MerkezDepoSorumlusu},{AtomRoller.IlMuduru},{AtomRoller.SistemAdmin}")]
    public async Task<IActionResult> Yeni()
    {
        var depolar = await _svc.DepolariGetirAsync();
        if (!AtomRoller.BakanlikRolleri.Contains(Rol))
            depolar = depolar.Where(d => d.KurumId == KurumId).ToList();
        ViewBag.Depolar = depolar;
        return View();
    }

    [HttpPost]
    [Authorize(Roles = $"{AtomRoller.IlDepoSorumlusu},{AtomRoller.MerkezDepoSorumlusu},{AtomRoller.IlMuduru},{AtomRoller.SistemAdmin}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Yeni(string depoId, string baslik)
    {
        var depo = await _svc.DepoGetirAsync(depoId);
        if (depo == null) { TempData["Hata"] = "Depo bulunamadı."; return RedirectToAction(nameof(Yeni)); }

        var sayim = new SayimKaydi
        {
            SayimNo = await _svc.YeniNumaraUretAsync("SYM"),
            KurumId = depo.KurumId,
            DepoId = depoId,
            Baslik = string.IsNullOrEmpty(baslik) ? $"{depo.Ad} Sayımı" : baslik,
            OlusturanKullaniciId = KullaniciId,
            Durum = SayimDurumu.DevamEdiyor
        };

        // Kaydi stoğu çek
        foreach (var s in depo.Stoklar)
        {
            sayim.Kalemler.Add(new SayimKalemi
            {
                TasinirTanimId = s.TasinirTanimId,
                KaydiMiktar = s.Miktar,
                FiiliMiktar = s.Miktar // başlangıçta eşit
            });
        }

        await _svc.SayimKaydetAsync(sayim);
        await _audit.KaydetAsync(User, "Sayim", "Oluşturma", "Sayim", sayim.Id, $"{sayim.SayimNo} başlatıldı", ip: Ip);
        TempData["Basari"] = $"{sayim.SayimNo} sayımı başlatıldı.";
        return RedirectToAction(nameof(Detay), new { id = sayim.Id });
    }

    public async Task<IActionResult> Detay(string id)
    {
        var sayim = await _svc.SayimGetirAsync(id);
        if (sayim == null) return NotFound();
        if (!AtomRoller.BakanlikRolleri.Contains(Rol) && sayim.KurumId != KurumId) return Forbid();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        var depo = await _svc.DepoGetirAsync(sayim.DepoId);
        ViewBag.Tanimlar = tanimlar.ToDictionary(t => t.Id, t => t.Ad);
        ViewBag.Depo = depo;
        return View(sayim);
    }

    public async Task<IActionResult> Tutanak(string id)
    {
        var sayim = await _svc.SayimGetirAsync(id);
        if (sayim == null) return NotFound();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        var depo = await _svc.DepoGetirAsync(sayim.DepoId);
        var kurum = depo != null ? await _svc.KurumGetirAsync(depo.KurumId) : null;
        ViewBag.Tanimlar = tanimlar.ToDictionary(t => t.Id, t => t.Ad);
        ViewBag.Depo = depo; ViewBag.Kurum = kurum;
        return View(sayim);
    }

    [HttpPost]
    [Authorize(Roles = $"{AtomRoller.IlDepoSorumlusu},{AtomRoller.MerkezDepoSorumlusu},{AtomRoller.IlMuduru},{AtomRoller.SistemAdmin}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FiiliKaydet(string id, string[] tanimIds, int[] fiiliMiktarlar, int[] hasarliMiktarlar)
    {
        var sayim = await _svc.SayimGetirAsync(id);
        if (sayim == null) return NotFound();

        for (int i = 0; i < tanimIds.Length; i++)
        {
            var kalem = sayim.Kalemler.FirstOrDefault(k => k.TasinirTanimId == tanimIds[i]);
            if (kalem != null)
            {
                kalem.FiiliMiktar = fiiliMiktarlar[i];
                kalem.HasarliMiktar = i < hasarliMiktarlar.Length ? hasarliMiktarlar[i] : 0;
            }
        }
        sayim.Durum = SayimDurumu.OnayBekliyor;
        await _svc.SayimKaydetAsync(sayim);
        TempData["Basari"] = "Fiili sayım kaydedildi, fark onayı bekleniyor.";
        return RedirectToAction(nameof(Detay), new { id });
    }

    [HttpPost]
    [Authorize(Roles = $"{AtomRoller.IlMuduru},{AtomRoller.MerkezDepoSorumlusu},{AtomRoller.BakanlikMerkez},{AtomRoller.SistemAdmin}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FarkOnayla(string id, string tutanakNotu)
    {
        var sayim = await _svc.SayimGetirAsync(id);
        if (sayim == null) return NotFound();

        // Farklar için stok düzeltme hareketi
        foreach (var k in sayim.Kalemler.Where(x => x.Fark != 0))
        {
            if (k.Fark > 0)
                await _stok.GirisYapAsync(new StokHareketIstegi
                {
                    DepoId = sayim.DepoId, TasinirTanimId = k.TasinirTanimId, Miktar = k.Fark, IslemTuru = StokIslemTuru.SayimFazlasi,
                    KaynakBelgeTur = "Sayim", KaynakBelgeId = sayim.Id, KaynakBelgeNo = sayim.SayimNo,
                    KullaniciId = KullaniciId, KullaniciAdi = AdSoyad, Aciklama = $"{sayim.SayimNo} sayım fazlası"
                });
            else
                try {
                    await _stok.CikisYapAsync(new StokHareketIstegi
                    {
                        DepoId = sayim.DepoId, TasinirTanimId = k.TasinirTanimId, Miktar = -k.Fark, IslemTuru = StokIslemTuru.Duzeltme,
                        KaynakBelgeTur = "Sayim", KaynakBelgeId = sayim.Id, KaynakBelgeNo = sayim.SayimNo,
                        KullaniciId = KullaniciId, KullaniciAdi = AdSoyad, Aciklama = $"{sayim.SayimNo} sayım noksanı"
                    });
                } catch (StokYetersizException) { }
        }

        sayim.Durum = SayimDurumu.Tamamlandi;
        sayim.BitisTarihi = DateTime.UtcNow;
        sayim.TutanakNotu = tutanakNotu;
        sayim.OnayGecmisi.Add(new OnayKaydi { KullaniciId = KullaniciId, KullaniciAdi = AdSoyad, Rol = Rol,
            Karar = OnayDurumu.Onaylandi, Asama = "Sayım Farkı Onayı", Aciklama = tutanakNotu });
        await _svc.SayimKaydetAsync(sayim);
        await _audit.KaydetAsync(User, "Sayim", "Onay", "Sayim", sayim.Id, $"{sayim.SayimNo} farkları onaylandı", ip: Ip);
        TempData["Basari"] = "Sayım farkları onaylandı, stok düzeltildi.";
        return RedirectToAction(nameof(Detay), new { id });
    }
}
