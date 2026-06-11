using System.Security.Claims;
using ATOM.Models.Accounts;
using ATOM.Models.Domain;
using ATOM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATOM.Areas.Edinim.Controllers;

/// <summary>Satış, bağış-hibe, fire/yok olma gibi çıkış yöntemleri. (Tüketim/Zimmet/Devir/Hurda kendi modüllerinde.)</summary>
[Area("Edinim")]
[Authorize(Roles = $"{AtomRoller.SistemAdmin},{AtomRoller.BakanlikMerkez},{AtomRoller.MerkezDepoSorumlusu},{AtomRoller.IlDepoSorumlusu},{AtomRoller.IlMuduru}")]
public class CikisController : Controller
{
    private readonly IAtomDataService _svc;
    private readonly IStokService _stok;
    private readonly IAuditService _audit;
    private readonly IBildirimService _bildirim;

    public CikisController(IAtomDataService svc, IStokService stok, IAuditService audit, IBildirimService bildirim)
    { _svc = svc; _stok = stok; _audit = audit; _bildirim = bildirim; }

    private string KullaniciId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private string AdSoyad => User.FindFirstValue("AdSoyad") ?? User.Identity?.Name ?? "";
    private string Rol => User.FindFirstValue(ClaimTypes.Role)!;
    private string KurumId => User.FindFirstValue("KurumId")!;
    private bool Bakanlik => AtomRoller.BakanlikRolleri.Contains(Rol);
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    public async Task<IActionResult> Index()
    {
        var cikislar = await _svc.MalCikislariGetirAsync();
        var depolar = await _svc.DepolariGetirAsync();
        if (!Bakanlik)
        {
            var kd = depolar.Where(d => d.KurumId == KurumId).Select(d => d.Id).ToHashSet();
            cikislar = cikislar.Where(c => kd.Contains(c.DepoId)).ToList();
        }
        ViewBag.Depolar = depolar.ToDictionary(d => d.Id, d => d.Ad);
        return View(cikislar.OrderByDescending(c => c.Tarih).ToList());
    }

    [HttpGet]
    public async Task<IActionResult> Yeni(CikisYontemi yontem = CikisYontemi.SatisSuretiyle)
    {
        await DropdownDoldur();
        ViewBag.SecilenYontem = yontem;
        return View(new MalCikisBelgesi { Yontem = yontem, KurumId = KurumId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Yeni(MalCikisBelgesi model, string[] tasinirIds, int[] miktarlar)
    {
        var depo = await _svc.DepoGetirAsync(model.DepoId);
        if (depo == null) { TempData["Hata"] = "Depo seçilmeli."; await DropdownDoldur(); ViewBag.SecilenYontem = model.Yontem; return View(model); }
        if (!Bakanlik && depo.KurumId != KurumId) return Forbid();

        model.Kalemler = new();
        for (int i = 0; i < (tasinirIds?.Length ?? 0); i++)
            if (!string.IsNullOrEmpty(tasinirIds[i]) && miktarlar[i] > 0)
                model.Kalemler.Add(new MalCikisKalemi { TasinirTanimId = tasinirIds[i], Miktar = miktarlar[i] });
        if (model.Kalemler.Count == 0) { TempData["Hata"] = "En az bir kalem ekleyin."; await DropdownDoldur(); ViewBag.SecilenYontem = model.Yontem; return View(model); }

        // Stok yeterlilik (negatif engel)
        foreach (var k in model.Kalemler)
        {
            var mevcut = await _stok.MevcutStokAsync(model.DepoId, k.TasinirTanimId);
            if (mevcut < k.Miktar)
            {
                var t = await _svc.TasinirTanimGetirAsync(k.TasinirTanimId);
                TempData["Hata"] = $"Yetersiz stok: '{t?.Ad}' mevcut {mevcut}, istenen {k.Miktar}.";
                await DropdownDoldur(); ViewBag.SecilenYontem = model.Yontem; return View(model);
            }
        }

        model.CikisNo = await _svc.YeniNumaraUretAsync("CK");
        model.TifNo = await _svc.YeniNumaraUretAsync("TIF");
        model.KurumId = depo.KurumId;
        model.OlusturanKullaniciId = KullaniciId;
        model.Tarih = DateTime.UtcNow;
        model.Durum = BelgeDurumu.Onaylandi;

        var islemTuru = model.Yontem switch
        {
            CikisYontemi.SatisSuretiyle => StokIslemTuru.HurdaDusum,
            CikisYontemi.KayipCalinma => StokIslemTuru.Kayip,
            CikisYontemi.Fire => StokIslemTuru.Duzeltme,
            CikisYontemi.BagisHibe => StokIslemTuru.HurdaDusum,
            _ => StokIslemTuru.Duzeltme
        };
        foreach (var k in model.Kalemler)
            await _stok.CikisYapAsync(new StokHareketIstegi
            {
                DepoId = model.DepoId, TasinirTanimId = k.TasinirTanimId, Miktar = k.Miktar, IslemTuru = islemTuru,
                KaynakBelgeTur = "MalCikis", KaynakBelgeId = model.Id, KaynakBelgeNo = model.CikisNo,
                KullaniciId = KullaniciId, KullaniciAdi = AdSoyad, Aciklama = $"{GirisCikisYardimci.CikisAd(model.Yontem)} — {model.CikisNo}"
            });

        model.OnayGecmisi.Add(new OnayKaydi { KullaniciId = KullaniciId, KullaniciAdi = AdSoyad, Rol = Rol,
            Karar = OnayDurumu.Onaylandi, Asama = GirisCikisYardimci.CikisAd(model.Yontem) });
        await _svc.MalCikisKaydetAsync(model);
        await _audit.KaydetAsync(User, "MalCikis", "Çıkış", "MalCikis", model.Id,
            $"{model.CikisNo} — {GirisCikisYardimci.CikisAd(model.Yontem)} ({model.Kalemler.Sum(k => k.Miktar)} adet)", ip: Ip);
        TempData["Basari"] = $"{model.CikisNo} çıkışı yapıldı (TİF: {model.TifNo}), stok düştü.";
        return RedirectToAction(nameof(Detay), new { id = model.Id });
    }

    public async Task<IActionResult> Detay(string id)
    {
        var c = await _svc.MalCikisGetirAsync(id);
        if (c == null) return NotFound();
        if (!Bakanlik && c.KurumId != KurumId) return Forbid();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        var depolar = await _svc.DepolariGetirAsync();
        ViewBag.Tanimlar = tanimlar.ToDictionary(t => t.Id, t => t.Ad);
        ViewBag.Depolar = depolar.ToDictionary(d => d.Id, d => d.Ad);
        return View(c);
    }

    private async Task DropdownDoldur()
    {
        var depolar = await _svc.DepolariGetirAsync();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        if (!Bakanlik) depolar = depolar.Where(d => d.KurumId == KurumId).ToList();
        ViewBag.Depolar = depolar;
        ViewBag.Tanimlar = tanimlar.Where(t => t.AktifMi).ToList();
        ViewBag.Yontemler = new[] { CikisYontemi.SatisSuretiyle, CikisYontemi.BagisHibe, CikisYontemi.Fire, CikisYontemi.KayipCalinma, CikisYontemi.Diger };
    }
}
