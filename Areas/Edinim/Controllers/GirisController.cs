using System.Security.Claims;
using ATOM.Models.Accounts;
using ATOM.Models.Domain;
using ATOM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATOM.Areas.Edinim.Controllers;

/// <summary>Taşınır Mal Yönetmeliği'ndeki tüm giriş yöntemleri için tek modül.</summary>
[Area("Edinim")]
[Authorize(Roles = $"{AtomRoller.SistemAdmin},{AtomRoller.BakanlikMerkez},{AtomRoller.BakanlikSatinAlma},{AtomRoller.MerkezDepoSorumlusu},{AtomRoller.IlDepoSorumlusu},{AtomRoller.IlMuduru}")]
public class GirisController : Controller
{
    private readonly IAtomDataService _svc;
    private readonly IStokService _stok;
    private readonly ITasinirKayitService _kayit;
    private readonly IBildirimService _bildirim;
    private readonly IAuditService _audit;

    public GirisController(IAtomDataService svc, IStokService stok, ITasinirKayitService kayit,
        IBildirimService bildirim, IAuditService audit)
    { _svc = svc; _stok = stok; _kayit = kayit; _bildirim = bildirim; _audit = audit; }

    private string KullaniciId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private string AdSoyad => User.FindFirstValue("AdSoyad") ?? User.Identity?.Name ?? "";
    private string Rol => User.FindFirstValue(ClaimTypes.Role)!;
    private string KurumId => User.FindFirstValue("KurumId")!;
    private bool Bakanlik => AtomRoller.BakanlikRolleri.Contains(Rol);
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    public async Task<IActionResult> Index(string? yontem = null)
    {
        var girisler = await _svc.MalGirisleriGetirAsync();
        var depolar = await _svc.DepolariGetirAsync();
        if (!Bakanlik)
        {
            var kd = depolar.Where(d => d.KurumId == KurumId).Select(d => d.Id).ToHashSet();
            girisler = girisler.Where(g => kd.Contains(g.DepoId) || g.KurumId == KurumId).ToList();
        }
        if (!string.IsNullOrEmpty(yontem) && Enum.TryParse<GirisYontemi>(yontem, out var y))
            girisler = girisler.Where(g => g.Yontem == y).ToList();
        ViewBag.Depolar = depolar.ToDictionary(d => d.Id, d => d.Ad);
        ViewBag.Yontem = yontem;
        return View(girisler.OrderByDescending(g => g.Tarih).ToList());
    }

    [HttpGet]
    public async Task<IActionResult> Yeni(GirisYontemi yontem = GirisYontemi.KurumButcesiAlim)
    {
        await DropdownDoldur();
        ViewBag.SecilenYontem = yontem;
        return View(new MalGirisBelgesi { Yontem = yontem, KurumId = KurumId, Tarih = DateTime.UtcNow });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Yeni(MalGirisBelgesi model,
        string[] tasinirIds, int[] miktarlar, decimal[] birimFiyatlar, string[] markalar, string[] modeller)
    {
        var depo = await _svc.DepoGetirAsync(model.DepoId);
        if (depo == null) { TempData["Hata"] = "Depo seçilmeli."; await DropdownDoldur(); ViewBag.SecilenYontem = model.Yontem; return View(model); }
        if (!Bakanlik && depo.KurumId != KurumId) return Forbid();

        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        model.Kalemler = new();
        for (int i = 0; i < (tasinirIds?.Length ?? 0); i++)
        {
            if (string.IsNullOrEmpty(tasinirIds[i]) || miktarlar[i] <= 0) continue;
            var tanim = tanimlar.FirstOrDefault(t => t.Id == tasinirIds[i]);
            model.Kalemler.Add(new MalGirisKalemi
            {
                TasinirTanimId = tasinirIds[i], Miktar = miktarlar[i],
                BirimFiyat = i < birimFiyatlar.Length ? birimFiyatlar[i] : 0,
                Marka = i < markalar.Length ? markalar[i] : "",
                Model = i < modeller.Length ? modeller[i] : "",
                DemirbasMi = tanim?.DemirbasMi ?? false
            });
        }
        if (model.Kalemler.Count == 0) { TempData["Hata"] = "En az bir kalem ekleyin."; await DropdownDoldur(); ViewBag.SecilenYontem = model.Yontem; return View(model); }

        model.GirisNo = await _svc.YeniNumaraUretAsync("GR");
        model.TifNo = await _svc.YeniNumaraUretAsync("TIF");
        model.KurumId = depo.KurumId;
        model.OlusturanKullaniciId = KullaniciId;
        model.Tarih = DateTime.UtcNow;
        model.Durum = BelgeDurumu.Onaylandi;

        // Stok girişi (yönteme göre hareket türü)
        var islemTuru = model.Yontem switch
        {
            GirisYontemi.BagisYardim => StokIslemTuru.BagisGirisi,
            GirisYontemi.DevirGirisi => StokIslemTuru.DevirGirisi,
            GirisYontemi.SayimFazlasi => StokIslemTuru.SayimFazlasi,
            GirisYontemi.IadeGirisi => StokIslemTuru.ZimmetIadesi,
            _ => StokIslemTuru.SatinAlmaGirisi
        };
        foreach (var k in model.Kalemler)
            await _stok.GirisYapAsync(new StokHareketIstegi
            {
                DepoId = model.DepoId, TasinirTanimId = k.TasinirTanimId, Miktar = k.Miktar, BirimMaliyet = k.BirimFiyat,
                IslemTuru = islemTuru, KaynakBelgeTur = "MalGiris", KaynakBelgeId = model.Id, KaynakBelgeNo = model.GirisNo,
                KullaniciId = KullaniciId, KullaniciAdi = AdSoyad, Aciklama = $"{GirisCikisYardimci.GirisAd(model.Yontem)} — {model.GirisNo}"
            });

        // Demirbaş kalemler için tekil TasinirKayit (mal kabul mantığıyla)
        var sahteMk = new MalKabul
        {
            MalKabulNo = model.GirisNo, TifNo = model.TifNo, DepoId = model.DepoId, FirmaId = model.FirmaId ?? "",
            TeslimTarihi = model.Tarih,
            Kalemler = model.Kalemler.Select(k => new MalKabulKalemi
            {
                TasinirTanimId = k.TasinirTanimId, KabulEdilen = k.Miktar, BirimFiyat = k.BirimFiyat,
                Marka = k.Marka, Model = k.Model, DemirbasMi = k.DemirbasMi, SeriNoListesi = k.SeriNoListesi,
                GarantiBitisTarihi = k.GarantiBitisTarihi
            }).ToList()
        };
        var uretilen = await _kayit.MalKabuldenUretAsync(sahteMk, KullaniciId, AdSoyad);
        model.TasinirKayitUretildiMi = uretilen > 0;

        model.OnayGecmisi.Add(new OnayKaydi { KullaniciId = KullaniciId, KullaniciAdi = AdSoyad, Rol = Rol,
            Karar = OnayDurumu.Onaylandi, Asama = GirisCikisYardimci.GirisAd(model.Yontem) });
        await _svc.MalGirisKaydetAsync(model);
        await _audit.KaydetAsync(User, "MalGiris", "Giriş", "MalGiris", model.Id,
            $"{model.GirisNo} — {GirisCikisYardimci.GirisAd(model.Yontem)} ({model.Kalemler.Sum(k => k.Miktar)} adet)", ip: Ip);
        TempData["Basari"] = $"{model.GirisNo} girişi yapıldı (TİF: {model.TifNo}), stok arttı, {uretilen} demirbaş kaydı üretildi.";
        return RedirectToAction(nameof(Detay), new { id = model.Id });
    }

    public async Task<IActionResult> Detay(string id)
    {
        var g = await _svc.MalGirisGetirAsync(id);
        if (g == null) return NotFound();
        if (!Bakanlik && g.KurumId != KurumId) return Forbid();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        var depolar = await _svc.DepolariGetirAsync();
        var firmalar = await _svc.FirmalariGetirAsync();
        ViewBag.Tanimlar = tanimlar.ToDictionary(t => t.Id, t => t.Ad);
        ViewBag.Depolar = depolar.ToDictionary(d => d.Id, d => d.Ad);
        ViewBag.Firmalar = firmalar.ToDictionary(f => f.Id, f => f.Ad);
        return View(g);
    }

    private async Task DropdownDoldur()
    {
        var depolar = await _svc.DepolariGetirAsync();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        var firmalar = await _svc.FirmalariGetirAsync();
        var ihaleler = await _svc.IhaleleriGetirAsync();
        if (!Bakanlik) depolar = depolar.Where(d => d.KurumId == KurumId).ToList();
        ViewBag.Depolar = depolar;
        ViewBag.Tanimlar = tanimlar.Where(t => t.AktifMi).ToList();
        ViewBag.Firmalar = firmalar.Where(f => f.AktifMi).ToList();
        ViewBag.Ihaleler = ihaleler.Where(i => i.Durum == IhaleDurumu.Sonuclandi || i.Durum == IhaleDurumu.KapandiTamamlandi).ToList();
        ViewBag.Yontemler = Enum.GetValues<GirisYontemi>();
    }
}
