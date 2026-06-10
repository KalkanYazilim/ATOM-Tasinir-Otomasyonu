using System.Security.Claims;
using ATOM.Models.Accounts;
using ATOM.Models.Domain;
using ATOM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATOM.Areas.Hurda.Controllers;

[Area("Hurda")]
[Authorize]
public class HomeController : Controller
{
    private readonly IAtomDataService _svc;
    private readonly IStokService _stok;
    private readonly ITasinirKayitService _kayit;
    private readonly IBildirimService _bildirim;
    private readonly IAuditService _audit;
    private readonly BelgeService _belge;

    public HomeController(IAtomDataService svc, IStokService stok,
        ITasinirKayitService kayit, IBildirimService bildirim, IAuditService audit, BelgeService belge)
    {
        _svc = svc; _stok = stok; _kayit = kayit; _bildirim = bildirim; _audit = audit; _belge = belge;
    }

    public async Task<IActionResult> KomisyonTutanak(string id)
    {
        var h = await _svc.HurdaKaydiGetirAsync(id);
        if (h == null) return NotFound();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        var basliklar = new List<string> { "S.No", "Taşınır", "Seri No", "Miktar", "Durum Açıklama" };
        int sira = 0;
        var satirlar = h.Kalemler.Select(k => (IList<string>)new List<string>
        { (++sira).ToString(), tanimlar.FirstOrDefault(t => t.Id == k.TasinirTanimId)?.Ad ?? k.TasinirTanimId, k.SeriNo, k.Miktar.ToString(), k.DurumAciklama });
        var bytes = _belge.WordTablo($"{h.DusumTuru.ToUpper()} KOMİSYON TUTANAĞI",
            $"Belge No: {h.HurdaNo} · Tür: {h.DusumTuru} · Komisyon Kararı: {h.KomisyonKarari} · Tarih: {(h.KomisyonTarihi ?? h.TalepTarihi):dd.MM.yyyy}",
            basliklar, satirlar, "Dayanak: Taşınır Mal Yönetmeliği – Kayıttan Düşme Teklif ve Onay Tutanağı");
        return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"{h.DusumTuru}-Tutanak-{h.HurdaNo}.docx");
    }

    private string KullaniciId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private string AdSoyad => User.FindFirstValue("AdSoyad") ?? User.Identity?.Name ?? "";
    private string Rol => User.FindFirstValue(ClaimTypes.Role)!;
    private string KurumId => User.FindFirstValue("KurumId")!;
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    public async Task<IActionResult> Index(string? durum = null)
    {
        var kayitlar = await _svc.HurdaKayitlariGetirAsync();
        var kurumlar = await _svc.KurumlariGetirAsync();
        if (!AtomRoller.BakanlikRolleri.Contains(Rol))
            kayitlar = kayitlar.Where(h => h.KurumId == KurumId).ToList();
        if (!string.IsNullOrEmpty(durum) && Enum.TryParse<HurdaDurumu>(durum, out var d))
            kayitlar = kayitlar.Where(h => h.Durum == d).ToList();

        ViewBag.Kurumlar = kurumlar.ToDictionary(k => k.Id, k => k.Ad);
        ViewBag.Durum = durum;
        return View(kayitlar.OrderByDescending(h => h.TalepTarihi).ToList());
    }

    [HttpGet]
    [Authorize(Roles = $"{AtomRoller.IlDepoSorumlusu},{AtomRoller.MerkezDepoSorumlusu},{AtomRoller.IlMuduru},{AtomRoller.SistemAdmin}")]
    public async Task<IActionResult> Yeni()
    {
        var depolar = await _svc.DepolariGetirAsync();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        var kayitlar = await _svc.TasinirKayitlariGetirAsync();
        if (!AtomRoller.BakanlikRolleri.Contains(Rol))
        {
            depolar = depolar.Where(d => d.KurumId == KurumId).ToList();
            kayitlar = kayitlar.Where(k => k.KurumId == KurumId).ToList();
        }
        ViewBag.Depolar = depolar;
        ViewBag.Tanimlar = tanimlar.ToDictionary(t => t.Id, t => t.Ad);
        ViewBag.UygunTasinirlar = kayitlar.Where(k => k.Durum == TasinirKayitDurumu.Ambarda || k.Durum == TasinirKayitDurumu.Bakimda).ToList();
        return View(new HurdaKaydi());
    }

    [HttpPost]
    [Authorize(Roles = $"{AtomRoller.IlDepoSorumlusu},{AtomRoller.MerkezDepoSorumlusu},{AtomRoller.IlMuduru},{AtomRoller.SistemAdmin}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Yeni(HurdaKaydi model, string[] tasinirKayitIds, string[] tasinirIds, int[] miktarlar, string[] aciklamalar)
    {
        model.HurdaNo = await _svc.YeniNumaraUretAsync("HRD");
        model.KurumId = KurumId;
        model.TalepEdenId = KullaniciId;
        model.TalepTarihi = DateTime.UtcNow;
        model.Durum = HurdaDurumu.Talep;
        model.Kalemler = new();

        // Tekil taşınır kalemleri
        foreach (var kid in (tasinirKayitIds ?? Array.Empty<string>()).Where(x => !string.IsNullOrEmpty(x)))
        {
            var k = await _svc.TasinirKayitGetirAsync(kid);
            if (k == null) continue;
            model.Kalemler.Add(new HurdaKalemi { TasinirKayitId = k.Id, TasinirTanimId = k.TasinirTanimId ?? "", Miktar = 1, SeriNo = k.SeriNo, DurumAciklama = "Tekil demirbaş" });
        }
        // Sarf/miktar kalemleri
        for (int i = 0; i < (tasinirIds?.Length ?? 0); i++)
        {
            if (!string.IsNullOrEmpty(tasinirIds[i]) && miktarlar[i] > 0)
                model.Kalemler.Add(new HurdaKalemi { TasinirTanimId = tasinirIds[i], Miktar = miktarlar[i], DurumAciklama = i < aciklamalar.Length ? aciklamalar[i] : "" });
        }

        if (model.Kalemler.Count == 0)
        {
            TempData["Hata"] = "En az bir kalem eklemelisiniz.";
            return RedirectToAction(nameof(Yeni));
        }

        await _svc.HurdaKaydiKaydetAsync(model);
        await _bildirim.BakanligaBildirAsync($"Yeni {model.DusumTuru} Talebi",
            $"{model.HurdaNo} numaralı {model.DusumTuru.ToLower()} talebi oluşturuldu.",
            BildirimTur.Bilgi, "/hurda", model.Id, "Hurda");
        await _audit.KaydetAsync(User, "Hurda", "Oluşturma", "HurdaKaydi", model.Id, $"{model.HurdaNo} oluşturuldu", ip: Ip);
        TempData["Basari"] = $"{model.HurdaNo} numaralı talep oluşturuldu.";
        return RedirectToAction(nameof(Detay), new { id = model.Id });
    }

    public async Task<IActionResult> Detay(string id)
    {
        var h = await _svc.HurdaKaydiGetirAsync(id);
        if (h == null) return NotFound();
        if (!AtomRoller.BakanlikRolleri.Contains(Rol) && h.KurumId != KurumId) return Forbid();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        ViewBag.Tanimlar = tanimlar.ToDictionary(t => t.Id, t => t.Ad);
        return View(h);
    }

    [HttpPost]
    [Authorize(Roles = $"{AtomRoller.IlMuduru},{AtomRoller.MerkezDepoSorumlusu},{AtomRoller.BakanlikMerkez},{AtomRoller.SistemAdmin}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> KomisyonKarar(string id, string karar, string komisyonKarari)
    {
        var h = await _svc.HurdaKaydiGetirAsync(id);
        if (h == null) return NotFound();
        h.Durum = HurdaDurumu.Komisyon;
        h.KomisyonTarihi = DateTime.UtcNow;
        h.KomisyonKarari = komisyonKarari;
        h.OnayGecmisi.Add(new OnayKaydi { KullaniciId = KullaniciId, KullaniciAdi = AdSoyad, Rol = Rol,
            Karar = OnayDurumu.Onaylandi, Asama = "Komisyon Kararı", Aciklama = komisyonKarari });
        await _svc.HurdaKaydiKaydetAsync(h);
        TempData["Basari"] = "Komisyon kararı kaydedildi, üst onaya hazır.";
        return RedirectToAction(nameof(Detay), new { id });
    }

    [HttpPost]
    [Authorize(Roles = $"{AtomRoller.BakanlikMerkez},{AtomRoller.SistemAdmin}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Onayla(string id, bool onayla, string aciklama)
    {
        var h = await _svc.HurdaKaydiGetirAsync(id);
        if (h == null) return NotFound();

        if (!onayla)
        {
            h.Durum = HurdaDurumu.Talep;
            h.OnayGecmisi.Add(new OnayKaydi { KullaniciId = KullaniciId, KullaniciAdi = AdSoyad, Rol = Rol,
                Karar = OnayDurumu.Reddedildi, Asama = "Üst Onay", Aciklama = aciklama });
            await _svc.HurdaKaydiKaydetAsync(h);
            TempData["Basari"] = "Talep reddedildi.";
            return RedirectToAction(nameof(Detay), new { id });
        }

        h.Durum = HurdaDurumu.Onaylandi;
        if (string.IsNullOrEmpty(h.TifNo)) h.TifNo = await _svc.YeniNumaraUretAsync("TIF");
        h.OnayMakami = AdSoyad;
        h.OnayGecmisi.Add(new OnayKaydi { KullaniciId = KullaniciId, KullaniciAdi = AdSoyad, Rol = Rol,
            Karar = OnayDurumu.Onaylandi, Asama = "Üst Onay", Aciklama = aciklama });

        // Stoktan ve tekil kayıttan düş
        var hedefDurum = h.DusumTuru == "Kayıp" ? TasinirKayitDurumu.Hurda : TasinirKayitDurumu.Hurda;
        var islemTuru = h.DusumTuru == "Kayıp" ? StokIslemTuru.Kayip : StokIslemTuru.HurdaDusum;

        foreach (var kalem in h.Kalemler)
        {
            if (!string.IsNullOrEmpty(kalem.TasinirKayitId))
            {
                await _kayit.DurumDegistirAsync(kalem.TasinirKayitId, hedefDurum, h.DusumTuru,
                    $"{h.HurdaNo}: {h.Gerekce}", KullaniciId, AdSoyad);
            }
            else if (!string.IsNullOrEmpty(h.DepoId))
            {
                try
                {
                    await _stok.CikisYapAsync(new StokHareketIstegi
                    {
                        DepoId = h.DepoId, TasinirTanimId = kalem.TasinirTanimId, Miktar = kalem.Miktar,
                        IslemTuru = islemTuru, KaynakBelgeTur = "Hurda", KaynakBelgeId = h.Id, KaynakBelgeNo = h.HurdaNo,
                        KullaniciId = KullaniciId, KullaniciAdi = AdSoyad, Aciklama = $"{h.HurdaNo} {h.DusumTuru}"
                    });
                }
                catch (StokYetersizException) { /* tekil olmayan, stok yoksa atla */ }
            }
        }

        await _svc.HurdaKaydiKaydetAsync(h);
        await _audit.KaydetAsync(User, "Hurda", "Onay", "HurdaKaydi", h.Id, $"{h.HurdaNo} onaylandı, düşüm yapıldı", ip: Ip);
        TempData["Basari"] = $"{h.HurdaNo} onaylandı, stok ve kayıtlardan düşüldü.";
        return RedirectToAction(nameof(Detay), new { id });
    }
}
