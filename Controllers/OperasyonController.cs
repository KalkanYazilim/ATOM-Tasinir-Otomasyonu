using System.Security.Claims;
using ATOM.Models.Accounts;
using ATOM.Models.Domain;
using ATOM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATOM.Controllers;

/// <summary>Sürükle-bırak görsel operasyon merkezi. Tüm işlemler mevcut servisleri kullanır (bypass yok).</summary>
[Authorize(Roles = $"{AtomRoller.SistemAdmin},{AtomRoller.BakanlikMerkez},{AtomRoller.MerkezDepoSorumlusu},{AtomRoller.IlMuduru},{AtomRoller.IlDepoSorumlusu}")]
public class OperasyonController : Controller
{
    private readonly IAtomDataService _svc;
    private readonly IStokService _stok;
    private readonly ITasinirKayitService _kayit;
    private readonly IBildirimService _bildirim;
    private readonly IAuditService _audit;

    public OperasyonController(IAtomDataService svc, IStokService stok, ITasinirKayitService kayit,
        IBildirimService bildirim, IAuditService audit)
    { _svc = svc; _stok = stok; _kayit = kayit; _bildirim = bildirim; _audit = audit; }

    private string KullaniciId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private string AdSoyad => User.FindFirstValue("AdSoyad") ?? User.Identity?.Name ?? "";
    private string Rol => User.FindFirstValue(ClaimTypes.Role)!;
    private string KurumId => User.FindFirstValue("KurumId")!;
    private bool Bakanlik => AtomRoller.BakanlikRolleri.Contains(Rol);
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    public async Task<IActionResult> Index()
    {
        var depolar = await _svc.DepolariGetirAsync();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        var kayitlar = await _svc.TasinirKayitlariGetirAsync();
        var kullanicilar = await _svc.KullanicilariGetirAsync();
        var kurumlar = await _svc.KurumlariGetirAsync();

        if (!Bakanlik)
        {
            depolar = depolar.Where(d => d.KurumId == KurumId).ToList();
            kullanicilar = kullanicilar.Where(k => k.KurumId == KurumId).ToList();
        }
        var depoIds = depolar.Select(d => d.Id).ToHashSet();

        ViewBag.Tanimlar = tanimlar.ToDictionary(t => t.Id, t => t);
        ViewBag.Kurumlar = kurumlar.ToDictionary(k => k.Id, k => k.Ad);
        ViewBag.Personeller = kullanicilar.Where(k => k.AktifMi && k.Rol == AtomRoller.Personel).ToList();
        // Depo başına ambardaki tekil demirbaşlar
        ViewBag.DepoTekiller = kayitlar
            .Where(k => k.Durum == TasinirKayitDurumu.Ambarda && k.DepoId != null && depoIds.Contains(k.DepoId))
            .GroupBy(k => k.DepoId!).ToDictionary(g => g.Key, g => g.ToList());
        return View(depolar);
    }

    // ─── Sürükle: Depo → Depo (Sevk oluştur) ──────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SevkYap(string kaynakDepoId, string hedefDepoId, string tanimId, int miktar)
    {
        var kaynak = await _svc.DepoGetirAsync(kaynakDepoId);
        var hedef = await _svc.DepoGetirAsync(hedefDepoId);
        if (kaynak == null || hedef == null) return Json(new { ok = false, msg = "Depo bulunamadı." });
        if (!Bakanlik && kaynak.KurumId != KurumId) return Json(new { ok = false, msg = "Bu depo için yetkiniz yok." });
        if (kaynakDepoId == hedefDepoId) return Json(new { ok = false, msg = "Kaynak ve hedef depo aynı olamaz." });
        if (miktar <= 0) return Json(new { ok = false, msg = "Miktar geçersiz." });

        var mevcut = await _stok.MevcutStokAsync(kaynakDepoId, tanimId);
        if (mevcut < miktar) return Json(new { ok = false, msg = $"Yetersiz stok (mevcut {mevcut})." });

        var sevk = new Sevk
        {
            SevkNo = await _svc.YeniNumaraUretAsync("SVK"),
            KaynakDepoId = kaynakDepoId, HedefDepoId = hedefDepoId, HedefKurumId = hedef.KurumId,
            OlusturanKullaniciId = KullaniciId, SevkTarihi = DateTime.UtcNow, Durum = SevkDurumu.Hazirlaniyor,
            Aciklama = "Operasyon panosundan sürükle-bırak ile oluşturuldu.",
            Kalemler = new() { new SevkKalemi { TasinirTanimId = tanimId, Miktar = miktar } }
        };
        await _stok.CikisYapAsync(new StokHareketIstegi
        {
            DepoId = kaynakDepoId, TasinirTanimId = tanimId, Miktar = miktar, IslemTuru = StokIslemTuru.SevkCikisi,
            KaynakBelgeTur = "Sevk", KaynakBelgeId = sevk.Id, KaynakBelgeNo = sevk.SevkNo,
            KullaniciId = KullaniciId, KullaniciAdi = AdSoyad, Aciklama = $"{sevk.SevkNo} sevk çıkışı (pano)"
        });
        await _svc.SevkKaydetAsync(sevk);
        await _bildirim.KurumaBildirAsync(hedef.KurumId, "Sevk Yolda",
            $"{sevk.SevkNo} numaralı sevk size yönlendirildi (pano).", BildirimTur.Bilgi, "/depo/Home/Sevkler",
            sevk.Id, "Sevk", "Normal", new[] { AtomRoller.IlDepoSorumlusu, AtomRoller.IlMuduru });
        await _audit.KaydetAsync(User, "Operasyon", "Sevk", "Sevk", sevk.Id, $"{sevk.SevkNo} pano sevk", ip: Ip);
        return Json(new { ok = true, msg = $"{sevk.SevkNo} sevk oluşturuldu, kaynak stok düştü." });
    }

    // ─── Sürükle: Demirbaş → Personel (Zimmet oluştur) ────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ZimmetYap(string tasinirKayitId, string personelId)
    {
        var k = await _svc.TasinirKayitGetirAsync(tasinirKayitId);
        var personel = await _svc.KullaniciGetirAsync(personelId);
        if (k == null || personel == null) return Json(new { ok = false, msg = "Kayıt/personel bulunamadı." });
        if (k.Durum != TasinirKayitDurumu.Ambarda) return Json(new { ok = false, msg = "Taşınır ambarda değil." });
        if (!Bakanlik && k.KurumId != KurumId) return Json(new { ok = false, msg = "Yetkiniz yok." });

        var zimmet = new Zimmet
        {
            ZimmetNo = await _svc.YeniNumaraUretAsync("ZMT"),
            DepoId = k.DepoId ?? "", PersonelId = personelId, VerenKullaniciId = KullaniciId,
            ZimmetTarihi = DateTime.UtcNow, Durum = ZimmetDurumu.Aktif,
            Aciklama = "Operasyon panosundan sürükle-bırak ile oluşturuldu.",
            Kalemler = new() { new ZimmetKalemi { TasinirKayitId = k.Id, TasinirTanimId = k.TasinirTanimId ?? "",
                Miktar = 1, SeriNo = k.SeriNo, Barkod = k.BarKod, SicilNo = k.SicilNo, Marka = k.MarkaAdi, Model = k.Modeli } }
        };
        zimmet.OnayGecmisi.Add(new OnayKaydi { KullaniciId = KullaniciId, KullaniciAdi = AdSoyad, Rol = Rol, Karar = OnayDurumu.Onaylandi, Asama = "Zimmet (Pano)" });
        await _kayit.DurumDegistirAsync(k.Id, TasinirKayitDurumu.Zimmetli, "Zimmet",
            $"{zimmet.ZimmetNo} ile zimmetlendi (pano).", KullaniciId, AdSoyad, zimmetId: zimmet.Id);
        await _svc.ZimmetKaydetAsync(zimmet);
        await _bildirim.KullaniciyaBildirAsync(personelId, "Yeni Zimmet",
            $"{zimmet.ZimmetNo}: {k.Cinsi} üzerinize tanımlandı.", BildirimTur.Bilgi, $"/zimmet/Home/Detay/{zimmet.Id}", zimmet.Id, "Zimmet");
        await _audit.KaydetAsync(User, "Operasyon", "Zimmet", "Zimmet", zimmet.Id, $"{zimmet.ZimmetNo} pano zimmet", ip: Ip);
        return Json(new { ok = true, msg = $"{zimmet.ZimmetNo} zimmet oluşturuldu." });
    }

    // ─── Sürükle: Ürün → Hurda alanı (Hurda talebi) ───────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HurdaYap(string depoId, string tanimId, int miktar, string? tasinirKayitId, string gerekce)
    {
        var depo = await _svc.DepoGetirAsync(depoId);
        if (depo == null) return Json(new { ok = false, msg = "Depo bulunamadı." });
        if (!Bakanlik && depo.KurumId != KurumId) return Json(new { ok = false, msg = "Yetkiniz yok." });

        var h = new HurdaKaydi
        {
            HurdaNo = await _svc.YeniNumaraUretAsync("HRD"),
            KurumId = depo.KurumId, DepoId = depoId, TalepEdenId = KullaniciId, TalepTarihi = DateTime.UtcNow,
            Durum = HurdaDurumu.Talep, DusumTuru = "Hurda",
            Gerekce = string.IsNullOrWhiteSpace(gerekce) ? "Operasyon panosundan hurda talebi." : gerekce,
            Kalemler = new() { new HurdaKalemi { TasinirKayitId = tasinirKayitId, TasinirTanimId = tanimId, Miktar = miktar < 1 ? 1 : miktar } }
        };
        await _svc.HurdaKaydiKaydetAsync(h);
        await _bildirim.BakanligaBildirAsync("Yeni Hurda Talebi", $"{h.HurdaNo} hurda talebi oluşturuldu (pano).",
            BildirimTur.Bilgi, "/hurda", h.Id, "Hurda");
        await _audit.KaydetAsync(User, "Operasyon", "Hurda", "HurdaKaydi", h.Id, $"{h.HurdaNo} pano hurda talebi", ip: Ip);
        return Json(new { ok = true, msg = $"{h.HurdaNo} hurda talebi oluşturuldu (komisyon/onay bekliyor)." });
    }
}
