using System.Security.Claims;
using ATOM.Models.Accounts;
using ATOM.Models.Domain;
using ATOM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATOM.Areas.Depo.Controllers;

[Area("Depo")]
[Authorize]
public class HomeController : Controller
{
    private readonly IAtomDataService _svc;
    private readonly IStokService _stok;
    private readonly ITasinirKayitService _kayit;
    private readonly IBildirimService _bildirim;
    private readonly IAuditService _audit;

    public HomeController(IAtomDataService svc, IStokService stok,
        ITasinirKayitService kayit, IBildirimService bildirim, IAuditService audit)
    {
        _svc = svc; _stok = stok; _kayit = kayit; _bildirim = bildirim; _audit = audit;
    }

    private string KullaniciId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private string AdSoyad => User.FindFirstValue("AdSoyad") ?? User.Identity?.Name ?? "";
    private string Rol => User.FindFirstValue(ClaimTypes.Role)!;
    private string KurumId => User.FindFirstValue("KurumId")!;
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    // ─── Depo Listesi ──────────────────────────────────────────
    public async Task<IActionResult> Index()
    {
        var depolar = await _svc.DepolariGetirAsync();
        var kurumlar = await _svc.KurumlariGetirAsync();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();

        if (!AtomRoller.BakanlikRolleri.Contains(Rol))
            depolar = depolar.Where(d => d.KurumId == KurumId).ToList();

        ViewBag.Kurumlar = kurumlar.ToDictionary(k => k.Id, k => k.Ad);
        ViewBag.Tanimlar = tanimlar.ToDictionary(t => t.Id, t => t.Ad);
        return View(depolar);
    }

    // ─── Depo Detay ───────────────────────────────────────────
    public async Task<IActionResult> Detay(string id)
    {
        var depo = await _svc.DepoGetirAsync(id);
        if (depo == null) return NotFound();
        if (!AtomRoller.BakanlikRolleri.Contains(Rol) && depo.KurumId != KurumId) return Forbid();

        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        ViewBag.Tanimlar = tanimlar.ToDictionary(t => t.Id, t => t);
        return View(depo);
    }

    // ─── Stok Kartı / Hareket Geçmişi ─────────────────────────
    public async Task<IActionResult> StokKart(string depoId, string tanimId)
    {
        var depo = await _svc.DepoGetirAsync(depoId);
        if (depo == null) return NotFound();
        if (!AtomRoller.BakanlikRolleri.Contains(Rol) && depo.KurumId != KurumId) return Forbid();

        var hareketler = (await _svc.StokHareketleriGetirAsync())
            .Where(h => h.DepoId == depoId && h.TasinirTanimId == tanimId)
            .OrderByDescending(h => h.Tarih).ToList();
        var tanim = await _svc.TasinirTanimGetirAsync(tanimId);
        var stok = depo.Stoklar.FirstOrDefault(s => s.TasinirTanimId == tanimId);

        ViewBag.Depo = depo;
        ViewBag.Tanim = tanim;
        ViewBag.MevcutStok = stok?.Miktar ?? 0;
        return View(hareketler);
    }

    // ─── Mal Kabul ─────────────────────────────────────────────
    [Authorize(Roles = $"{AtomRoller.MerkezDepoSorumlusu},{AtomRoller.IlDepoSorumlusu},{AtomRoller.SistemAdmin}")]
    public async Task<IActionResult> MalKabuller()
    {
        var malKabuller = await _svc.MalKabulleriGetirAsync();
        var firmalar = await _svc.FirmalariGetirAsync();
        var depolar = await _svc.DepolariGetirAsync();

        if (!AtomRoller.BakanlikRolleri.Contains(Rol))
        {
            var kurumDepolar = depolar.Where(d => d.KurumId == KurumId).Select(d => d.Id).ToHashSet();
            malKabuller = malKabuller.Where(mk => kurumDepolar.Contains(mk.DepoId)).ToList();
        }

        ViewBag.Firmalar = firmalar.ToDictionary(f => f.Id, f => f.Ad);
        ViewBag.Depolar = depolar.ToDictionary(d => d.Id, d => d.Ad);
        return View(malKabuller.OrderByDescending(mk => mk.TeslimTarihi).ToList());
    }

    [HttpGet]
    [Authorize(Roles = $"{AtomRoller.MerkezDepoSorumlusu},{AtomRoller.IlDepoSorumlusu},{AtomRoller.SistemAdmin}")]
    public async Task<IActionResult> YeniMalKabul(string? ihaleId = null)
    {
        var ihaleler = (await _svc.IhaleleriGetirAsync())
            .Where(i => i.Durum == IhaleDurumu.Sonuclandi).ToList();
        var firmalar = await _svc.FirmalariGetirAsync();
        var depolar = await _svc.DepolariGetirAsync();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();

        if (!AtomRoller.BakanlikRolleri.Contains(Rol))
            depolar = depolar.Where(d => d.KurumId == KurumId).ToList();

        ViewBag.Ihaleler = ihaleler;
        ViewBag.Firmalar = firmalar.ToDictionary(f => f.Id, f => f.Ad);
        ViewBag.Depolar = depolar;
        ViewBag.Tanimlar = tanimlar.ToDictionary(t => t.Id, t => t.Ad);

        var mk = new MalKabul { IhaleId = ihaleId ?? "" };
        if (!string.IsNullOrEmpty(ihaleId))
        {
            var ihale = await _svc.IhaleGetirAsync(ihaleId);
            if (ihale != null)
            {
                mk.FirmaId = ihale.KazananFirmaId ?? "";
                mk.Kalemler = ihale.Kalemler.Select(k => new MalKabulKalemi
                {
                    TasinirTanimId = k.TasinirTanimId,
                    SiparisEdilen = k.Miktar
                }).ToList();
            }
        }

        return View(mk);
    }

    [HttpPost]
    [Authorize(Roles = $"{AtomRoller.MerkezDepoSorumlusu},{AtomRoller.IlDepoSorumlusu},{AtomRoller.SistemAdmin}")]
    public async Task<IActionResult> YeniMalKabul(MalKabul mk,
        string[] tasinirIds, int[] siparisEdilen, int[] teslimEdilen, int[] kabulEdilen,
        decimal[] birimFiyatlar, string[] seriNolar, string[] markalar, string[] modeller)
    {
        mk.MalKabulNo = await _svc.YeniNumaraUretAsync("MK");
        mk.KabulEdenKullaniciId = KullaniciId;
        mk.TeslimTarihi = DateTime.UtcNow;
        mk.Durum = OnayDurumu.Bekliyor;
        mk.Kalemler = new();

        for (int i = 0; i < tasinirIds.Length; i++)
        {
            mk.Kalemler.Add(new MalKabulKalemi
            {
                TasinirTanimId = tasinirIds[i],
                SiparisEdilen = siparisEdilen[i],
                TeslimEdilen = teslimEdilen[i],
                KabulEdilen = kabulEdilen[i],
                Reddedilen = teslimEdilen[i] - kabulEdilen[i],
                BirimFiyat = birimFiyatlar[i],
                SeriNo = i < seriNolar.Length ? seriNolar[i] : "",
                Marka = i < markalar.Length ? markalar[i] : "",
                Model = i < modeller.Length ? modeller[i] : ""
            });
        }

        await _svc.MalKabulKaydetAsync(mk);
        TempData["Basari"] = $"{mk.MalKabulNo} mal kabulü oluşturuldu. Onay bekliyor.";
        return RedirectToAction(nameof(MalKabuller));
    }

    [HttpPost]
    [Authorize(Roles = $"{AtomRoller.MerkezDepoSorumlusu},{AtomRoller.SistemAdmin},{AtomRoller.BakanlikMerkez}")]
    public async Task<IActionResult> MalKabulOnayla(string id, bool onayla, string aciklama)
    {
        var mk = await _svc.MalKabulGetirAsync(id);
        if (mk == null) return NotFound();

        mk.Durum = onayla ? OnayDurumu.Onaylandi : OnayDurumu.Reddedildi;
        mk.OnayGecmisi.Add(new OnayKaydi
        {
            KullaniciId = KullaniciId,
            KullaniciAdi = User.FindFirstValue("AdSoyad") ?? "",
            Rol = Rol, Karar = onayla ? OnayDurumu.Onaylandi : OnayDurumu.Reddedildi,
            Asama = "Mal Kabul Onayı", Aciklama = aciklama
        });

        if (onayla)
        {
            var tanimlar = await _svc.TasinirTanimlariGetirAsync();
            if (string.IsNullOrEmpty(mk.TifNo)) mk.TifNo = await _svc.YeniNumaraUretAsync("TIF");

            // Tüm kabul edilen kalemler stoğa girer (belgeli hareket)
            foreach (var kalem in mk.Kalemler.Where(k => k.KabulEdilen > 0))
            {
                await _stok.GirisYapAsync(new StokHareketIstegi
                {
                    DepoId = mk.DepoId, TasinirTanimId = kalem.TasinirTanimId,
                    Miktar = kalem.KabulEdilen, BirimMaliyet = kalem.BirimFiyat,
                    IslemTuru = StokIslemTuru.SatinAlmaGirisi,
                    KaynakBelgeTur = "MalKabul", KaynakBelgeId = mk.Id, KaynakBelgeNo = mk.MalKabulNo,
                    KullaniciId = KullaniciId, KullaniciAdi = AdSoyad,
                    Aciklama = $"{mk.MalKabulNo} mal kabul girişi"
                });
            }

            // Demirbaş kalemler için tekil TasinirKayit üret
            var uretilen = await _kayit.MalKabuldenUretAsync(mk, KullaniciId, AdSoyad);
            mk.TasinirKayitUretildiMi = uretilen > 0;

            // İhale teslim durumu
            if (!string.IsNullOrEmpty(mk.IhaleId))
            {
                var ihale = await _svc.IhaleGetirAsync(mk.IhaleId);
                if (ihale != null) { ihale.Durum = IhaleDurumu.KapandiTamamlandi; await _svc.IhaleKaydetAsync(ihale); }
            }

            await _bildirim.KurumaBildirAsync(KurumId, "Mal Kabul Onaylandı",
                $"{mk.MalKabulNo} mal kabulü onaylandı, stok güncellendi. {uretilen} adet demirbaş kaydı üretildi.",
                BildirimTur.Basari, "/depo/Home/MalKabuller", mk.Id, "MalKabul", "Normal",
                new[] { AtomRoller.MerkezDepoSorumlusu, AtomRoller.IlDepoSorumlusu });
        }

        await _svc.MalKabulKaydetAsync(mk);
        await _audit.KaydetAsync(User, "MalKabul", onayla ? "Onay" : "Red", "MalKabul", mk.Id,
            $"{mk.MalKabulNo} {(onayla ? "onaylandı" : "reddedildi")}", ip: Ip);
        TempData["Basari"] = onayla ? "Mal kabul onaylandı, stok ve demirbaş kayıtları güncellendi." : "Mal kabul reddedildi.";
        return RedirectToAction(nameof(MalKabuller));
    }

    // ─── Sevk ─────────────────────────────────────────────────
    public async Task<IActionResult> Sevkler()
    {
        var sevkler = await _svc.SevkleriGetirAsync();
        var depolar = await _svc.DepolariGetirAsync();
        var kurumlar = await _svc.KurumlariGetirAsync();

        if (!AtomRoller.BakanlikRolleri.Contains(Rol))
        {
            var kurumDepolar = depolar.Where(d => d.KurumId == KurumId).Select(d => d.Id).ToHashSet();
            sevkler = sevkler.Where(s => kurumDepolar.Contains(s.KaynakDepoId) || kurumDepolar.Contains(s.HedefDepoId)).ToList();
        }

        ViewBag.Depolar = depolar.ToDictionary(d => d.Id, d => d.Ad);
        ViewBag.Kurumlar = kurumlar.ToDictionary(k => k.Id, k => k.Ad);
        return View(sevkler.OrderByDescending(s => s.SevkTarihi).ToList());
    }

    [HttpGet]
    [Authorize(Roles = $"{AtomRoller.MerkezDepoSorumlusu},{AtomRoller.BakanlikMerkez},{AtomRoller.SistemAdmin}")]
    public async Task<IActionResult> YeniSevk()
    {
        var depolar = await _svc.DepolariGetirAsync();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        var kurumlar = await _svc.KurumlariGetirAsync();
        ViewBag.Depolar = depolar;
        ViewBag.Tanimlar = tanimlar.ToDictionary(t => t.Id, t => t.Ad);
        ViewBag.Kurumlar = kurumlar.ToDictionary(k => k.Id, k => k.Ad);
        return View(new Sevk());
    }

    [HttpPost]
    [Authorize(Roles = $"{AtomRoller.MerkezDepoSorumlusu},{AtomRoller.BakanlikMerkez},{AtomRoller.SistemAdmin}")]
    public async Task<IActionResult> YeniSevk(Sevk sevk, string[] tasinirIds, int[] miktarlar)
    {
        sevk.OlusturanKullaniciId = KullaniciId;
        sevk.SevkTarihi = DateTime.UtcNow;
        sevk.Durum = SevkDurumu.Hazirlaniyor;
        sevk.Kalemler = new();

        // Önce tüm kalemlerde stok yeterliliğini doğrula (kısmi düşmeyi önle)
        for (int i = 0; i < tasinirIds.Length; i++)
        {
            if (string.IsNullOrEmpty(tasinirIds[i]) || miktarlar[i] <= 0) continue;
            var mevcut = await _stok.MevcutStokAsync(sevk.KaynakDepoId, tasinirIds[i]);
            if (mevcut < miktarlar[i])
            {
                var tanim = await _svc.TasinirTanimGetirAsync(tasinirIds[i]);
                TempData["Hata"] = $"Yetersiz stok: '{tanim?.Ad}' için mevcut {mevcut}, istenen {miktarlar[i]}. Sevk oluşturulmadı.";
                return RedirectToAction(nameof(YeniSevk));
            }
            sevk.Kalemler.Add(new SevkKalemi { TasinirTanimId = tasinirIds[i], Miktar = miktarlar[i] });
        }

        if (sevk.Kalemler.Count == 0)
        {
            TempData["Hata"] = "En az bir kalem seçmelisiniz.";
            return RedirectToAction(nameof(YeniSevk));
        }

        sevk.SevkNo = await _svc.YeniNumaraUretAsync("SVK");

        // Kaynak depodan çıkış (belgeli, negatif korumalı)
        foreach (var kalem in sevk.Kalemler)
        {
            await _stok.CikisYapAsync(new StokHareketIstegi
            {
                DepoId = sevk.KaynakDepoId, TasinirTanimId = kalem.TasinirTanimId, Miktar = kalem.Miktar,
                IslemTuru = StokIslemTuru.SevkCikisi,
                KaynakBelgeTur = "Sevk", KaynakBelgeId = sevk.Id, KaynakBelgeNo = sevk.SevkNo,
                KullaniciId = KullaniciId, KullaniciAdi = AdSoyad, Aciklama = $"{sevk.SevkNo} sevk çıkışı"
            });
        }

        await _svc.SevkKaydetAsync(sevk);
        if (!string.IsNullOrEmpty(sevk.HedefKurumId))
            await _bildirim.KurumaBildirAsync(sevk.HedefKurumId, "Sevk Yolda",
                $"{sevk.SevkNo} numaralı sevk size yönlendirildi, teslim bekleniyor.",
                BildirimTur.Bilgi, "/depo/Home/Sevkler", sevk.Id, "Sevk", "Normal",
                new[] { AtomRoller.IlDepoSorumlusu, AtomRoller.IlMuduru });
        await _audit.KaydetAsync(User, "Sevk", "Oluşturma", "Sevk", sevk.Id, $"{sevk.SevkNo} sevk oluşturuldu", ip: Ip);
        TempData["Basari"] = $"{sevk.SevkNo} numaralı sevk oluşturuldu, kaynak stoktan düşüldü.";
        return RedirectToAction(nameof(Sevkler));
    }

    [HttpPost]
    public async Task<IActionResult> SevkTeslimAl(string id, string aciklama)
    {
        var sevk = await _svc.SevkGetirAsync(id);
        if (sevk == null) return NotFound();

        sevk.Durum = SevkDurumu.TeslimEdildi;
        sevk.GercekVarisTarihi = DateTime.UtcNow;
        sevk.OnayGecmisi.Add(new OnayKaydi
        {
            KullaniciId = KullaniciId,
            KullaniciAdi = User.FindFirstValue("AdSoyad") ?? "",
            Rol = Rol, Karar = OnayDurumu.Onaylandi,
            Asama = "Teslim Alma", Aciklama = aciklama
        });

        foreach (var kalem in sevk.Kalemler)
        {
            await _stok.GirisYapAsync(new StokHareketIstegi
            {
                DepoId = sevk.HedefDepoId, TasinirTanimId = kalem.TasinirTanimId, Miktar = kalem.Miktar,
                IslemTuru = StokIslemTuru.SevkGirisi,
                KaynakBelgeTur = "Sevk", KaynakBelgeId = sevk.Id, KaynakBelgeNo = sevk.SevkNo,
                KullaniciId = KullaniciId, KullaniciAdi = AdSoyad, Aciklama = $"{sevk.SevkNo} sevk teslim girişi"
            });
            kalem.TeslimAlinan = kalem.Miktar;
        }

        await _svc.SevkKaydetAsync(sevk);
        await _audit.KaydetAsync(User, "Sevk", "Teslim", "Sevk", sevk.Id, $"{sevk.SevkNo} teslim alındı", ip: Ip);
        TempData["Basari"] = "Sevk teslim alındı, hedef depo stoğu güncellendi.";
        return RedirectToAction(nameof(Sevkler));
    }
}

