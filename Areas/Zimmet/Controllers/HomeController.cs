using System.Security.Claims;
using ATOM.Models.Accounts;
using ATOM.Models.Domain;
using ATOM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATOM.Areas.Zimmet.Controllers;

[Area("Zimmet")]
[Authorize]
public class HomeController : Controller
{
    private readonly IAtomDataService _svc;
    private readonly ITasinirKayitService _kayit;
    private readonly IStokService _stok;
    private readonly IBildirimService _bildirim;
    private readonly IAuditService _audit;
    private readonly IImzaService _imza;
    private readonly BelgeService _belge;

    public HomeController(IAtomDataService svc, ITasinirKayitService kayit, IStokService stok,
        IBildirimService bildirim, IAuditService audit, IImzaService imza, BelgeService belge)
    {
        _svc = svc; _kayit = kayit; _stok = stok; _bildirim = bildirim; _audit = audit; _imza = imza; _belge = belge;
    }

    private string KullaniciId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private string AdSoyad => User.FindFirstValue("AdSoyad") ?? User.Identity?.Name ?? "";
    private string Rol => User.FindFirstValue(ClaimTypes.Role)!;
    private string KurumId => User.FindFirstValue("KurumId")!;
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    private bool Bakanlik => AtomRoller.BakanlikRolleri.Contains(Rol);
    private bool YetkiliKurum(string kurumId) => Bakanlik || kurumId == KurumId;
    private async Task<bool> ZimmeteYetkiliMi(ATOM.Models.Domain.Zimmet zimmet)
    {
        if (Rol == AtomRoller.Personel) return zimmet.PersonelId == KullaniciId;
        var depo = await _svc.DepoGetirAsync(zimmet.DepoId);
        return depo != null && YetkiliKurum(depo.KurumId);
    }
    private bool YetkiliPersonel(AtomKullanici personel) =>
        personel.AktifMi && personel.Rol == AtomRoller.Personel && YetkiliKurum(personel.KurumId);

    public async Task<IActionResult> Index(string? ara = null, string? durum = null)
    {
        var zimmetler = await _svc.ZimmetleriGetirAsync();
        var kullanicilar = await _svc.KullanicilariGetirAsync();
        var depolar = await _svc.DepolariGetirAsync();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();

        if (Rol == AtomRoller.Personel)
            zimmetler = zimmetler.Where(z => z.PersonelId == KullaniciId).ToList();
        else if (!Bakanlik)
        {
            var kurumDepolar = depolar.Where(d => d.KurumId == KurumId).Select(d => d.Id).ToHashSet();
            zimmetler = zimmetler.Where(z => kurumDepolar.Contains(z.DepoId)).ToList();
        }

        if (!string.IsNullOrEmpty(ara))
            zimmetler = zimmetler.Where(z => z.ZimmetNo.Contains(ara, StringComparison.OrdinalIgnoreCase)).ToList();
        if (!string.IsNullOrEmpty(durum) && Enum.TryParse<ZimmetDurumu>(durum, out var d))
            zimmetler = zimmetler.Where(z => z.Durum == d).ToList();

        ViewBag.Kullanicilar = kullanicilar.ToDictionary(k => k.Id, k => k.AdSoyad);
        ViewBag.Depolar = depolar.ToDictionary(x => x.Id, x => x.Ad);
        ViewBag.Tanimlar = tanimlar.ToDictionary(t => t.Id, t => t.Ad);
        ViewBag.Ara = ara; ViewBag.Durum = durum;
        return View(zimmetler.OrderByDescending(z => z.ZimmetTarihi).ToList());
    }

    public async Task<IActionResult> Sarf()
    {
        var bakiyeler = await _svc.PersonelSarfBakiyeleriGetirAsync();
        var kullanicilar = await _svc.KullanicilariGetirAsync();
        var depolar = await _svc.DepolariGetirAsync();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();

        if (Rol == AtomRoller.Personel)
            bakiyeler = bakiyeler.Where(b => b.PersonelId == KullaniciId).ToList();
        else if (!Bakanlik)
            bakiyeler = bakiyeler.Where(b => b.KurumId == KurumId).ToList();

        ViewBag.Kullanicilar = kullanicilar.ToDictionary(k => k.Id, k => k.AdSoyad);
        ViewBag.Depolar = depolar.ToDictionary(d => d.Id, d => d.Ad);
        ViewBag.Tanimlar = tanimlar.ToDictionary(t => t.Id, t => t);
        return View(bakiyeler.Where(b => b.Miktar > 0).OrderByDescending(b => b.SonGuncelleme).ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SarfDus(string bakiyeId, int miktar, string aciklama)
    {
        var bakiye = (await _svc.PersonelSarfBakiyeleriGetirAsync()).FirstOrDefault(b => b.Id == bakiyeId);
        if (bakiye == null) return NotFound();
        if (Rol == AtomRoller.Personel && bakiye.PersonelId != KullaniciId) return Forbid();
        if (Rol != AtomRoller.Personel && !YetkiliKurum(bakiye.KurumId)) return Forbid();
        if (miktar <= 0 || miktar > bakiye.Miktar)
        {
            TempData["Hata"] = "Düşüm miktarı geçersiz.";
            return RedirectToAction(nameof(Sarf));
        }

        var belgeNo = await _svc.YeniNumaraUretAsync("PSD");
        await _stok.PersonelSarfDusAsync(new PersonelSarfIstegi
        {
            PersonelId = bakiye.PersonelId,
            KurumId = bakiye.KurumId,
            KaynakDepoId = bakiye.KaynakDepoId,
            TasinirTanimId = bakiye.TasinirTanimId,
            Miktar = miktar,
            KaynakBelgeNo = belgeNo,
            KullaniciId = KullaniciId,
            KullaniciAdi = AdSoyad,
            Aciklama = string.IsNullOrWhiteSpace(aciklama) ? "Son kullanıcı tüketim düşümü." : aciklama
        });
        await _audit.KaydetAsync(User, "PersonelSarf", "Düşüm", "PersonelSarfBakiye", bakiye.Id,
            $"{belgeNo} personel sarf düşümü ({miktar} adet)", ip: Ip);
        TempData["Basari"] = $"{belgeNo} numaralı sarf düşümü kaydedildi.";
        return RedirectToAction(nameof(Sarf));
    }

    [HttpGet]
    [Authorize(Roles = $"{AtomRoller.IlDepoSorumlusu},{AtomRoller.MerkezDepoSorumlusu},{AtomRoller.IlMuduru},{AtomRoller.SistemAdmin}")]
    public async Task<IActionResult> Yeni()
    {
        var depolar = await _svc.DepolariGetirAsync();
        var kullanicilar = await _svc.KullanicilariGetirAsync();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        var kayitlar = await _svc.TasinirKayitlariGetirAsync();

        if (!Bakanlik)
        {
            depolar = depolar.Where(d => d.KurumId == KurumId).ToList();
            kullanicilar = kullanicilar.Where(k => k.KurumId == KurumId).ToList();
        }
        var depoIds = depolar.Select(d => d.Id).ToHashSet();

        // Sadece ambarda olan tekil taşınırlar zimmete verilebilir
        ViewBag.UygunTasinirlar = kayitlar
            .Where(k => k.Durum == TasinirKayitDurumu.Ambarda && k.DepoId != null && depoIds.Contains(k.DepoId))
            .OrderBy(k => k.Cinsi).ToList();
        ViewBag.Depolar = depolar;
        ViewBag.Kullanicilar = kullanicilar.Where(k => k.AktifMi && k.Rol == AtomRoller.Personel).ToList();
        ViewBag.Tanimlar = tanimlar.ToDictionary(t => t.Id, t => t.Ad);
        return View(new ATOM.Models.Domain.Zimmet { DepoId = depolar.FirstOrDefault()?.Id ?? "" });
    }

    [HttpPost]
    [Authorize(Roles = $"{AtomRoller.IlDepoSorumlusu},{AtomRoller.MerkezDepoSorumlusu},{AtomRoller.IlMuduru},{AtomRoller.SistemAdmin}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Yeni(ATOM.Models.Domain.Zimmet zimmet, string[] tasinirKayitIds)
    {
        if (tasinirKayitIds == null || tasinirKayitIds.Length == 0)
        {
            TempData["Hata"] = "En az bir taşınır seçmelisiniz.";
            return RedirectToAction(nameof(Yeni));
        }
        var depo = await _svc.DepoGetirAsync(zimmet.DepoId);
        var personel = await _svc.KullaniciGetirAsync(zimmet.PersonelId);
        if (depo == null)
        {
            TempData["Hata"] = "Depo bulunamadı.";
            return RedirectToAction(nameof(Yeni));
        }
        if (!YetkiliKurum(depo.KurumId)) return Forbid();
        if (personel == null || !YetkiliPersonel(personel))
        {
            TempData["Hata"] = "Bu personele zimmet verme yetkiniz yok.";
            return RedirectToAction(nameof(Yeni));
        }

        zimmet.ZimmetNo = await _svc.YeniNumaraUretAsync("ZMT");
        zimmet.VerenKullaniciId = KullaniciId;
        zimmet.ZimmetTarihi = DateTime.UtcNow;
        zimmet.Durum = ZimmetDurumu.Aktif;
        zimmet.Kalemler = new();

        foreach (var kayitId in tasinirKayitIds.Where(x => !string.IsNullOrEmpty(x)))
        {
            var k = await _svc.TasinirKayitGetirAsync(kayitId);
            if (k == null || k.Durum != TasinirKayitDurumu.Ambarda) continue;
            if (k.DepoId != zimmet.DepoId || string.IsNullOrWhiteSpace(k.KurumId) || !YetkiliKurum(k.KurumId)) continue;

            zimmet.Kalemler.Add(new ZimmetKalemi
            {
                TasinirKayitId = k.Id,
                TasinirTanimId = k.TasinirTanimId ?? "",
                Miktar = 1,
                SeriNo = k.SeriNo, Barkod = k.BarKod, SicilNo = k.SicilNo,
                Marka = k.MarkaAdi, Model = k.Modeli,
                ItemDurumu = ZimmetDurumu.Aktif
            });

            // Tekil taşınır durumunu Zimmetli yap
            await _kayit.DurumDegistirAsync(k.Id, TasinirKayitDurumu.Zimmetli, "Zimmet",
                $"{zimmet.ZimmetNo} ile zimmetlendi.", KullaniciId, AdSoyad, zimmetId: zimmet.Id);
        }

        if (zimmet.Kalemler.Count == 0)
        {
            TempData["Hata"] = "Seçilen taşınırlar zimmete uygun değil (ambarda olmalı).";
            return RedirectToAction(nameof(Yeni));
        }

        zimmet.OnayGecmisi.Add(new OnayKaydi
        {
            KullaniciId = KullaniciId, KullaniciAdi = AdSoyad,
            Rol = Rol, Karar = OnayDurumu.Onaylandi, Asama = "Zimmet Oluşturma"
        });

        await _svc.ZimmetKaydetAsync(zimmet);
        await _bildirim.KullaniciyaBildirAsync(zimmet.PersonelId, "Yeni Zimmet",
            $"{zimmet.ZimmetNo} numaralı zimmet üzerinize tanımlandı ({zimmet.Kalemler.Count} kalem).",
            BildirimTur.Bilgi, $"/zimmet/Home/Detay/{zimmet.Id}", zimmet.Id, "Zimmet");
        await _audit.KaydetAsync(User, "Zimmet", "Oluşturma", "Zimmet", zimmet.Id, $"{zimmet.ZimmetNo} oluşturuldu", ip: Ip);

        TempData["Basari"] = $"{zimmet.ZimmetNo} numaralı zimmet oluşturuldu.";
        return RedirectToAction(nameof(Detay), new { id = zimmet.Id });
    }

    public async Task<IActionResult> Detay(string id)
    {
        var zimmet = await _svc.ZimmetGetirAsync(id);
        if (zimmet == null) return NotFound();
        if (!await ZimmeteYetkiliMi(zimmet)) return Forbid();

        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        var kullanicilar = await _svc.KullanicilariGetirAsync();
        var depolar = await _svc.DepolariGetirAsync();
        ViewBag.Tanimlar = tanimlar.ToDictionary(t => t.Id, t => t.Ad);
        ViewBag.Kullanicilar = kullanicilar.ToDictionary(k => k.Id, k => k.AdSoyad);
        ViewBag.Depolar = depolar.ToDictionary(d => d.Id, d => d.Ad);
        return View(zimmet);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Iade(string id, string aciklama, string iadeTuru = "Saglam")
    {
        var zimmet = await _svc.ZimmetGetirAsync(id);
        if (zimmet == null) return NotFound();
        var iadeRolYetkili = new[] { AtomRoller.IlMuduru, AtomRoller.IlDepoSorumlusu, AtomRoller.MerkezDepoSorumlusu, AtomRoller.SistemAdmin, AtomRoller.BakanlikMerkez }.Contains(Rol);
        if (zimmet.PersonelId != KullaniciId && !iadeRolYetkili) return Forbid();
        if (!await ZimmeteYetkiliMi(zimmet)) return Forbid();

        zimmet.Durum = ZimmetDurumu.Iade;
        zimmet.IadeTarihi = DateTime.UtcNow;
        zimmet.IadeAciklama = aciklama;

        foreach (var kalem in zimmet.Kalemler)
        {
            kalem.ItemDurumu = ZimmetDurumu.Iade;
            if (string.IsNullOrEmpty(kalem.TasinirKayitId)) continue;

            // İade türüne göre tekil taşınır hedef durumu
            var (hedef, islem, not) = iadeTuru switch
            {
                "Hasarli" => (TasinirKayitDurumu.Bakimda, "İade (Hasarlı)", "Hasarlı iade, bakıma yönlendirildi."),
                "Kayip" => (TasinirKayitDurumu.Hurda, "İade (Kayıp)", "Kayıp olarak bildirildi."),
                _ => (TasinirKayitDurumu.Ambarda, "İade", "Sağlam iade, ambara alındı.")
            };
            await _kayit.DurumDegistirAsync(kalem.TasinirKayitId, hedef, islem,
                $"{zimmet.ZimmetNo}: {not}", KullaniciId, AdSoyad, zimmetId: "",
                depoId: hedef == TasinirKayitDurumu.Ambarda ? zimmet.DepoId : null);
        }

        zimmet.OnayGecmisi.Add(new OnayKaydi
        {
            KullaniciId = KullaniciId, KullaniciAdi = AdSoyad,
            Rol = Rol, Karar = OnayDurumu.Onaylandi, Asama = $"İade ({iadeTuru})", Aciklama = aciklama
        });

        await _svc.ZimmetKaydetAsync(zimmet);
        await _audit.KaydetAsync(User, "Zimmet", "İade", "Zimmet", zimmet.Id, $"{zimmet.ZimmetNo} iade ({iadeTuru})", ip: Ip);
        TempData["Basari"] = "Zimmet iade edildi, taşınır durumları güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    // ─── Zimmet Fişi (yazdırılabilir) ─────────────────────────
    public async Task<IActionResult> Fis(string id)
    {
        var zimmet = await _svc.ZimmetGetirAsync(id);
        if (zimmet == null) return NotFound();
        if (!await ZimmeteYetkiliMi(zimmet)) return Forbid();

        var kullanicilar = await _svc.KullanicilariGetirAsync();
        var depolar = await _svc.DepolariGetirAsync();
        var kurumlar = await _svc.KurumlariGetirAsync();
        var personel = kullanicilar.FirstOrDefault(k => k.Id == zimmet.PersonelId);
        var depo = depolar.FirstOrDefault(d => d.Id == zimmet.DepoId);
        ViewBag.Personel = personel;
        ViewBag.Veren = kullanicilar.FirstOrDefault(k => k.Id == zimmet.VerenKullaniciId);
        ViewBag.Depo = depo;
        var kurum = kurumlar.FirstOrDefault(k => k.Id == (depo?.KurumId));
        ViewBag.Kurum = kurum;

        // Elektronik onay/imza (5070): yoksa üret
        var mevcutImza = (await _svc.ImzalariGetirAsync()).FirstOrDefault(i => i.BelgeId == zimmet.Id && i.BelgeTuru == "ZimmetFisi");
        if (mevcutImza == null)
        {
            var icerik = $"{zimmet.ZimmetNo}|{zimmet.PersonelId}|{string.Join(",", zimmet.Kalemler.Select(k => k.SicilNo))}|{zimmet.ZimmetTarihi:o}";
            mevcutImza = await _imza.BelgeImzalaAsync(User, "ZimmetFisi", zimmet.Id, zimmet.ZimmetNo, icerik, kurum?.Ad ?? "");
        }
        ViewBag.Imza = mevcutImza;
        return View(zimmet);
    }

    // ─── Zimmet Fişi Word (.docx) ─────────────────────────────
    public async Task<IActionResult> FisWord(string id)
    {
        var zimmet = await _svc.ZimmetGetirAsync(id);
        if (zimmet == null) return NotFound();
        if (!await ZimmeteYetkiliMi(zimmet)) return Forbid();
        var kullanicilar = await _svc.KullanicilariGetirAsync();
        var personel = kullanicilar.FirstOrDefault(k => k.Id == zimmet.PersonelId);

        var basliklar = new List<string> { "S.No", "Cinsi", "Marka/Model", "Sicil No", "Barkod", "Seri No" };
        int sira = 0;
        var satirlar = zimmet.Kalemler.Select(k => (IList<string>)new List<string>
        { (++sira).ToString(), k.Marka, $"{k.Marka} {k.Model}", k.SicilNo, k.Barkod, k.SeriNo });

        var icerik = $"{zimmet.ZimmetNo}|{zimmet.PersonelId}|{zimmet.ZimmetTarihi:o}";
        var imza = (await _svc.ImzalariGetirAsync()).FirstOrDefault(i => i.BelgeId == zimmet.Id && i.BelgeTuru == "ZimmetFisi")
                   ?? await _imza.BelgeImzalaAsync(User, "ZimmetFisi", zimmet.Id, zimmet.ZimmetNo, icerik, "");
        var bytes = _belge.WordTablo("TAŞINIR TESLİM (ZİMMET) BELGESİ",
            $"Zimmet No: {zimmet.ZimmetNo} · Teslim Alan: {personel?.AdSoyad} · Tarih: {zimmet.ZimmetTarihi:dd.MM.yyyy}",
            basliklar, satirlar, "Dayanak: Taşınır Mal Yönetmeliği – Zimmet Fişi", imza.DogrulamaKodu);
        return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"zimmet-{zimmet.ZimmetNo}.docx");
    }

    // ─── İade Fişi Word (.docx) ──────────────────────────────
    public async Task<IActionResult> IadeFisWord(string id)
    {
        var zimmet = await _svc.ZimmetGetirAsync(id);
        if (zimmet == null) return NotFound();
        if (!await ZimmeteYetkiliMi(zimmet)) return Forbid();
        if (zimmet.Durum != ZimmetDurumu.Iade)
        {
            TempData["Hata"] = "İade fişi yalnızca iade edilmiş zimmetler için üretilebilir.";
            return RedirectToAction(nameof(Detay), new { id });
        }

        var kullanicilar = await _svc.KullanicilariGetirAsync();
        var depolar = await _svc.DepolariGetirAsync();
        var personel = kullanicilar.FirstOrDefault(k => k.Id == zimmet.PersonelId);
        var iadeAlan = kullanicilar.FirstOrDefault(k => k.Id == zimmet.OnayGecmisi.LastOrDefault(o => o.Asama.StartsWith("İade"))?.KullaniciId)
                       ?? kullanicilar.FirstOrDefault(k => k.Id == zimmet.VerenKullaniciId);
        var depo = depolar.FirstOrDefault(d => d.Id == zimmet.DepoId);

        var basliklar = new List<string> { "S.No", "Barkod", "Sicil No", "Seri No", "Marka/Model", "İade Durumu", "Açıklama" };
        int sira = 0;
        var satirlar = zimmet.Kalemler.Select(k => (IList<string>)new List<string>
        {
            (++sira).ToString(),
            k.Barkod,
            k.SicilNo,
            k.SeriNo,
            $"{k.Marka} {k.Model}".Trim(),
            k.ItemDurumu.ToString(),
            k.HasarAciklama ?? ""
        });

        var icerik = $"{zimmet.ZimmetNo}|IADE|{zimmet.PersonelId}|{zimmet.IadeTarihi:o}|{string.Join(",", zimmet.Kalemler.Select(k => k.SicilNo))}";
        var imza = (await _svc.ImzalariGetirAsync()).FirstOrDefault(i => i.BelgeId == zimmet.Id && i.BelgeTuru == "IadeFisi")
                   ?? await _imza.BelgeImzalaAsync(User, "IadeFisi", zimmet.Id, $"{zimmet.ZimmetNo}-IADE", icerik, depo?.Ad ?? "");

        var altBilgi = $"Zimmet No: {zimmet.ZimmetNo} · İade Eden: {personel?.AdSoyad} · İade Alan: {iadeAlan?.AdSoyad} · İade Tarihi: {zimmet.IadeTarihi:dd.MM.yyyy}";
        if (!string.IsNullOrWhiteSpace(zimmet.IadeAciklama))
            altBilgi += $" · Açıklama: {zimmet.IadeAciklama}";

        var bytes = _belge.WordTablo("TAŞINIR İADE FİŞİ",
            altBilgi,
            basliklar, satirlar,
            "Dayanak: Taşınır Mal Yönetmeliği – zimmet iadesi, taşınırların kullanıcıdan ambara/bakıma/kayıp sürecine dönüş kaydı.",
            imza.DogrulamaKodu);
        return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"iade-{zimmet.ZimmetNo}.docx");
    }

    // ─── Bakım / Arıza ────────────────────────────────────────
    public async Task<IActionResult> Bakim()
    {
        var bakimlar = await _svc.BakimKayitlariGetirAsync();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        var kullanicilar = await _svc.KullanicilariGetirAsync();

        if (Rol == AtomRoller.Personel)
            bakimlar = bakimlar.Where(b => b.PersonelId == KullaniciId).ToList();
        else if (!Bakanlik)
        {
            var yetkiliPersonelIds = kullanicilar
                .Where(k => k.KurumId == KurumId)
                .Select(k => k.Id)
                .ToHashSet();
            bakimlar = bakimlar.Where(b => yetkiliPersonelIds.Contains(b.PersonelId)).ToList();
        }

        ViewBag.Tanimlar = tanimlar.ToDictionary(t => t.Id, t => t.Ad);
        ViewBag.Kullanicilar = kullanicilar.ToDictionary(k => k.Id, k => k.AdSoyad);
        // Personelin arıza bildirebileceği aktif zimmet kalemleri
        var zimmetler = await _svc.ZimmetleriGetirAsync();
        if (Rol != AtomRoller.Personel && !Bakanlik)
        {
            var depolar = await _svc.DepolariGetirAsync();
            var kurumDepolar = depolar.Where(d => d.KurumId == KurumId).Select(d => d.Id).ToHashSet();
            zimmetler = zimmetler.Where(z => kurumDepolar.Contains(z.DepoId)).ToList();
        }
        ViewBag.AktifZimmetler = zimmetler
            .Where(z => z.Durum == ZimmetDurumu.Aktif && (Rol != AtomRoller.Personel || z.PersonelId == KullaniciId))
            .ToList();
        return View(bakimlar.OrderByDescending(b => b.ArizaBildirmeTarihi).ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ArizaBildir(string zimmetId, string tasinirKayitId, string tasinirTanimId, string seriNo, string aciklama)
    {
        var zimmet = await _svc.ZimmetGetirAsync(zimmetId);
        if (zimmet == null || !await ZimmeteYetkiliMi(zimmet)) return Forbid();
        if (!zimmet.Kalemler.Any(k => k.TasinirKayitId == tasinirKayitId || k.TasinirTanimId == tasinirTanimId)) return Forbid();

        var bk = new BakimKaydi
        {
            BakimNo = await _svc.YeniNumaraUretAsync("BKM"),
            ZimmetId = zimmetId,
            PersonelId = KullaniciId,
            TasinirTanimId = tasinirTanimId,
            SeriNo = seriNo,
            ArizaBildirmeTarihi = DateTime.UtcNow,
            ArizaAciklama = aciklama,
            Durum = BakimDurumu.Acik
        };

        if (!string.IsNullOrEmpty(tasinirKayitId))
        {
            await _kayit.DurumDegistirAsync(tasinirKayitId, TasinirKayitDurumu.Bakimda, "Arıza Bildirimi",
                aciklama, KullaniciId, AdSoyad);
        }

        await _svc.BakimKaydiKaydetAsync(bk);
        await _bildirim.KurumaBildirAsync(KurumId, "Yeni Arıza Bildirimi",
            $"{bk.BakimNo}: {aciklama}", BildirimTur.Uyari, "/zimmet/Home/Bakim", bk.Id, "Bakim", "Yüksek",
            new[] { AtomRoller.Teknisyen, AtomRoller.IlDepoSorumlusu, AtomRoller.IlMuduru });
        TempData["Basari"] = $"{bk.BakimNo} arıza kaydı oluşturuldu.";
        return RedirectToAction(nameof(Bakim));
    }

    [HttpPost]
    [Authorize(Roles = $"{AtomRoller.Teknisyen},{AtomRoller.IlMuduru},{AtomRoller.IlDepoSorumlusu},{AtomRoller.SistemAdmin}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BakimTamamla(string id, string yapilanIslem, decimal maliyet, bool garantiKapsaminda, bool hurdaMi = false)
    {
        var bk = await _svc.BakimKaydiGetirAsync(id);
        if (bk == null) return NotFound();

        bk.Durum = BakimDurumu.Tamamlandi;
        bk.AtananTeknikId = KullaniciId;
        bk.TamamlanmaTarihi = DateTime.UtcNow;
        bk.YapilanIslem = yapilanIslem;
        bk.BakimMaliyeti = maliyet;
        bk.GarantiKapsaminaMi = garantiKapsaminda;

        await _svc.BakimKaydiKaydetAsync(bk);
        await _audit.KaydetAsync(User, "Bakim", "Tamamlama", "Bakim", bk.Id, $"{bk.BakimNo} tamamlandı", ip: Ip);
        TempData["Basari"] = "Bakım/Onarım kaydı tamamlandı.";
        return RedirectToAction(nameof(Bakim));
    }
}
