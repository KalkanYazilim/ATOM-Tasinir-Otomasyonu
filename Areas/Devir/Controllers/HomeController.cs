using System.Security.Claims;
using ATOM.Models.Accounts;
using ATOM.Models.Domain;
using ATOM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATOM.Areas.Devir.Controllers;

[Area("Devir")]
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

    public async Task<IActionResult> DevirFisi(string id)
    {
        var d = await _svc.DevirGetirAsync(id);
        if (d == null) return NotFound();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        var kurumlar = await _svc.KurumlariGetirAsync();
        var kaynak = kurumlar.FirstOrDefault(k => k.Id == d.KaynakKurumId)?.Ad;
        var hedef = kurumlar.FirstOrDefault(k => k.Id == d.HedefKurumId)?.Ad;
        var basliklar = new List<string> { "S.No", "Taşınır", "Miktar", "Birim Maliyet" };
        int sira = 0;
        var satirlar = d.Kalemler.Select(k => (IList<string>)new List<string>
        { (++sira).ToString(), tanimlar.FirstOrDefault(t => t.Id == k.TasinirTanimId)?.Ad ?? k.TasinirTanimId, k.Miktar.ToString(), k.BirimMaliyet.ToString("N2") });
        var bytes = _belge.WordTablo("BEDELSİZ DEVİR FİŞİ",
            $"Devir No: {d.DevirNo} · Tür: {d.Tur} · {kaynak} → {hedef} · Tarih: {d.DevirTarihi:dd.MM.yyyy}",
            basliklar, satirlar, "Dayanak: Taşınır Mal Yönetmeliği Genel Tebliği (Sayı:1) – İhtiyaç Fazlası Taşınır Devri");
        return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"Devir-{d.DevirNo}.docx");
    }

    private string KullaniciId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private string AdSoyad => User.FindFirstValue("AdSoyad") ?? User.Identity?.Name ?? "";
    private string Rol => User.FindFirstValue(ClaimTypes.Role)!;
    private string KurumId => User.FindFirstValue("KurumId")!;
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    public async Task<IActionResult> Index()
    {
        var devirler = await _svc.DevirleriGetirAsync();
        var depolar = await _svc.DepolariGetirAsync();
        var kurumlar = await _svc.KurumlariGetirAsync();
        if (!AtomRoller.BakanlikRolleri.Contains(Rol))
            devirler = devirler.Where(d => d.KaynakKurumId == KurumId || d.HedefKurumId == KurumId).ToList();
        ViewBag.Depolar = depolar.ToDictionary(d => d.Id, d => d.Ad);
        ViewBag.Kurumlar = kurumlar.ToDictionary(k => k.Id, k => k.Ad);
        return View(devirler.OrderByDescending(d => d.DevirTarihi).ToList());
    }

    [HttpGet]
    [Authorize(Roles = $"{AtomRoller.IlDepoSorumlusu},{AtomRoller.MerkezDepoSorumlusu},{AtomRoller.IlMuduru},{AtomRoller.BakanlikMerkez},{AtomRoller.SistemAdmin}")]
    public async Task<IActionResult> Yeni()
    {
        var depolar = await _svc.DepolariGetirAsync();
        var kurumlar = await _svc.KurumlariGetirAsync();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        ViewBag.Depolar = depolar;
        ViewBag.Kurumlar = kurumlar;
        ViewBag.Tanimlar = tanimlar.ToDictionary(t => t.Id, t => t.Ad);
        return View(new DevirKaydi());
    }

    [HttpPost]
    [Authorize(Roles = $"{AtomRoller.IlDepoSorumlusu},{AtomRoller.MerkezDepoSorumlusu},{AtomRoller.IlMuduru},{AtomRoller.BakanlikMerkez},{AtomRoller.SistemAdmin}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Yeni(DevirKaydi model, string[] tasinirIds, int[] miktarlar)
    {
        model.DevirNo = await _svc.YeniNumaraUretAsync("DVR");
        model.KaynakKurumId = KurumId;
        model.OlusturanKullaniciId = KullaniciId;
        model.DevirTarihi = DateTime.UtcNow;
        model.Durum = DevirDurumu.AlanOnayiBekliyor;
        model.Kalemler = new();

        for (int i = 0; i < (tasinirIds?.Length ?? 0); i++)
        {
            if (!string.IsNullOrEmpty(tasinirIds[i]) && miktarlar[i] > 0)
            {
                // Kaynak stok yeterlilik
                var mevcut = await _stok.MevcutStokAsync(model.KaynakDepoId, tasinirIds[i]);
                if (mevcut < miktarlar[i])
                {
                    var tanim = await _svc.TasinirTanimGetirAsync(tasinirIds[i]);
                    TempData["Hata"] = $"Yetersiz stok: '{tanim?.Ad}' mevcut {mevcut}, istenen {miktarlar[i]}.";
                    return RedirectToAction(nameof(Yeni));
                }
                model.Kalemler.Add(new DevirKalemi { TasinirTanimId = tasinirIds[i], Miktar = miktarlar[i] });
            }
        }

        if (model.Kalemler.Count == 0) { TempData["Hata"] = "En az bir kalem ekleyin."; return RedirectToAction(nameof(Yeni)); }

        await _svc.DevirKaydetAsync(model);
        if (!string.IsNullOrEmpty(model.HedefKurumId))
            await _bildirim.KurumaBildirAsync(model.HedefKurumId, "Devir Onayı Bekliyor",
                $"{model.DevirNo} numaralı devir kurumunuza yönlendirildi, onayınız bekleniyor.",
                BildirimTur.Bilgi, "/devir", model.Id, "Devir", "Normal",
                new[] { AtomRoller.IlMuduru, AtomRoller.IlDepoSorumlusu });
        await _audit.KaydetAsync(User, "Devir", "Oluşturma", "Devir", model.Id, $"{model.DevirNo} oluşturuldu", ip: Ip);
        TempData["Basari"] = $"{model.DevirNo} devir kaydı oluşturuldu, alan taraf onayı bekleniyor.";
        return RedirectToAction(nameof(Detay), new { id = model.Id });
    }

    public async Task<IActionResult> Detay(string id)
    {
        var d = await _svc.DevirGetirAsync(id);
        if (d == null) return NotFound();
        if (!AtomRoller.BakanlikRolleri.Contains(Rol) && d.KaynakKurumId != KurumId && d.HedefKurumId != KurumId) return Forbid();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        var depolar = await _svc.DepolariGetirAsync();
        var kurumlar = await _svc.KurumlariGetirAsync();
        ViewBag.Tanimlar = tanimlar.ToDictionary(t => t.Id, t => t.Ad);
        ViewBag.Depolar = depolar.ToDictionary(x => x.Id, x => x.Ad);
        ViewBag.Kurumlar = kurumlar.ToDictionary(x => x.Id, x => x.Ad);
        return View(d);
    }

    [HttpPost]
    [Authorize(Roles = $"{AtomRoller.IlMuduru},{AtomRoller.IlDepoSorumlusu},{AtomRoller.MerkezDepoSorumlusu},{AtomRoller.BakanlikMerkez},{AtomRoller.SistemAdmin}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AlanOnay(string id, bool onayla, string aciklama)
    {
        var d = await _svc.DevirGetirAsync(id);
        if (d == null) return NotFound();
        // Alan taraf yetkisi
        if (!AtomRoller.BakanlikRolleri.Contains(Rol) && d.HedefKurumId != KurumId) return Forbid();

        if (!onayla)
        {
            d.Durum = DevirDurumu.Reddedildi;
            d.OnayGecmisi.Add(new OnayKaydi { KullaniciId = KullaniciId, KullaniciAdi = AdSoyad, Rol = Rol,
                Karar = OnayDurumu.Reddedildi, Asama = "Alan Onayı", Aciklama = aciklama });
            await _svc.DevirKaydetAsync(d);
            TempData["Basari"] = "Devir reddedildi.";
            return RedirectToAction(nameof(Detay), new { id });
        }

        // Onaylandı → kaynak depodan çıkış, hedef depoya giriş
        foreach (var k in d.Kalemler)
        {
            try
            {
                await _stok.CikisYapAsync(new StokHareketIstegi
                {
                    DepoId = d.KaynakDepoId, TasinirTanimId = k.TasinirTanimId, Miktar = k.Miktar,
                    IslemTuru = StokIslemTuru.Duzeltme, KaynakBelgeTur = "Devir", KaynakBelgeId = d.Id, KaynakBelgeNo = d.DevirNo,
                    KullaniciId = KullaniciId, KullaniciAdi = AdSoyad, Aciklama = $"{d.DevirNo} devir çıkışı"
                });
            }
            catch (StokYetersizException ex) { TempData["Hata"] = ex.Message; return RedirectToAction(nameof(Detay), new { id }); }

            if (!string.IsNullOrEmpty(d.HedefDepoId))
                await _stok.GirisYapAsync(new StokHareketIstegi
                {
                    DepoId = d.HedefDepoId, TasinirTanimId = k.TasinirTanimId, Miktar = k.Miktar, BirimMaliyet = k.BirimMaliyet,
                    IslemTuru = StokIslemTuru.DevirGirisi, KaynakBelgeTur = "Devir", KaynakBelgeId = d.Id, KaynakBelgeNo = d.DevirNo,
                    KullaniciId = KullaniciId, KullaniciAdi = AdSoyad, Aciklama = $"{d.DevirNo} devir girişi"
                });

            if (!string.IsNullOrEmpty(k.TasinirKayitId))
                await _kayit.DurumDegistirAsync(k.TasinirKayitId, TasinirKayitDurumu.Ambarda, "Devir",
                    $"{d.DevirNo} ile devredildi.", KullaniciId, AdSoyad, depoId: d.HedefDepoId);
        }

        d.Durum = DevirDurumu.Tamamlandi;
        d.OnayGecmisi.Add(new OnayKaydi { KullaniciId = KullaniciId, KullaniciAdi = AdSoyad, Rol = Rol,
            Karar = OnayDurumu.Onaylandi, Asama = "Alan Onayı", Aciklama = aciklama });
        await _svc.DevirKaydetAsync(d);
        await _audit.KaydetAsync(User, "Devir", "Onay", "Devir", d.Id, $"{d.DevirNo} tamamlandı", ip: Ip);
        TempData["Basari"] = "Devir onaylandı ve tamamlandı, stoklar güncellendi.";
        return RedirectToAction(nameof(Detay), new { id });
    }
}
