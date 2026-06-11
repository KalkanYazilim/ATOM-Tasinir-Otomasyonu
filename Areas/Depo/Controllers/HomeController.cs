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
    private readonly BelgeService _belge;

    public HomeController(IAtomDataService svc, IStokService stok,
        ITasinirKayitService kayit, IBildirimService bildirim, IAuditService audit, BelgeService belge)
    {
        _svc = svc; _stok = stok; _kayit = kayit; _bildirim = bildirim; _audit = audit; _belge = belge;
    }

    private string KullaniciId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private string AdSoyad => User.FindFirstValue("AdSoyad") ?? User.Identity?.Name ?? "";
    private string Rol => User.FindFirstValue(ClaimTypes.Role)!;
    private string KurumId => User.FindFirstValue("KurumId")!;
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();
    private bool Bakanlik => AtomRoller.BakanlikRolleri.Contains(Rol);
    private bool YetkiliDepo(ATOM.Models.Domain.Depo depo) => Bakanlik || depo.KurumId == KurumId;
    private bool YetkiliPersonel(AtomKullanici personel) =>
        personel.AktifMi && personel.Rol == AtomRoller.Personel && (Bakanlik || personel.KurumId == KurumId);

    // ─── Depo Listesi ──────────────────────────────────────────
    public async Task<IActionResult> Index()
    {
        var depolar = await _svc.DepolariGetirAsync();
        var kurumlar = await _svc.KurumlariGetirAsync();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();

        if (!Bakanlik)
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
        if (!YetkiliDepo(depo)) return Forbid();

        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        ViewBag.Tanimlar = tanimlar.ToDictionary(t => t.Id, t => t);
        return View(depo);
    }

    // ─── Resmi Belgeler (Word) ────────────────────────────────
    public async Task<IActionResult> TifBelge(string id)
    {
        var mk = await _svc.MalKabulGetirAsync(id);
        if (mk == null) return NotFound();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        var depo = await _svc.DepoGetirAsync(mk.DepoId);
        if (depo == null) return NotFound();
        if (!YetkiliDepo(depo)) return Forbid();
        var basliklar = new List<string> { "S.No", "Taşınır", "Sipariş", "Teslim", "Kabul", "Red", "Birim Fiyat", "Tutar" };
        int sira = 0;
        var satirlar = mk.Kalemler.Select(k => (IList<string>)new List<string>
        {
            (++sira).ToString(),
            tanimlar.FirstOrDefault(t => t.Id == k.TasinirTanimId)?.Ad ?? k.TasinirTanimId,
            k.SiparisEdilen.ToString(), k.TeslimEdilen.ToString(), k.KabulEdilen.ToString(),
            k.Reddedilen.ToString(), k.BirimFiyat.ToString("N2"), (k.KabulEdilen * k.BirimFiyat).ToString("N2")
        });
        var bytes = _belge.WordTablo("TAŞINIR İŞLEM FİŞİ (TİF)",
            $"TİF No: {mk.TifNo} · Mal Kabul: {mk.MalKabulNo} · Depo: {depo?.Ad} · Tarih: {mk.TeslimTarihi:dd.MM.yyyy}",
            basliklar, satirlar, "Dayanak: Taşınır Mal Yönetmeliği – Taşınır İşlem Fişi (Giriş)");
        return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"TIF-{mk.MalKabulNo}.docx");
    }

    public async Task<IActionResult> MuayeneTutanak(string id)
    {
        var mk = await _svc.MalKabulGetirAsync(id);
        if (mk == null) return NotFound();
        var depo = await _svc.DepoGetirAsync(mk.DepoId);
        if (depo == null) return NotFound();
        if (!YetkiliDepo(depo)) return Forbid();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        var firma = (await _svc.FirmalariGetirAsync()).FirstOrDefault(f => f.Id == mk.FirmaId);
        var basliklar = new List<string> { "S.No", "Taşınır", "Sipariş", "Teslim", "Kabul", "Red", "Red Gerekçe" };
        int sira = 0;
        var satirlar = mk.Kalemler.Select(k => (IList<string>)new List<string>
        {
            (++sira).ToString(), tanimlar.FirstOrDefault(t => t.Id == k.TasinirTanimId)?.Ad ?? k.TasinirTanimId,
            k.SiparisEdilen.ToString(), k.TeslimEdilen.ToString(), k.KabulEdilen.ToString(), k.Reddedilen.ToString(), k.RedGerekce ?? ""
        });
        var bytes = _belge.WordTablo("MUAYENE VE KABUL TUTANAĞI",
            $"Mal Kabul: {mk.MalKabulNo} · Firma: {firma?.Ad} · Fatura: {mk.FaturaNo} · Tarih: {mk.TeslimTarihi:dd.MM.yyyy}",
            basliklar, satirlar, "Dayanak: Mal Alımları Denetim, Muayene ve Kabul İşlemlerine Dair Yönetmelik (4734/4735 sK)");
        return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"MuayeneKabul-{mk.MalKabulNo}.docx");
    }

    public async Task<IActionResult> SevkIrsaliye(string id)
    {
        var sevk = await _svc.SevkGetirAsync(id);
        if (sevk == null) return NotFound();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        var depolar = await _svc.DepolariGetirAsync();
        var kaynakDepo = depolar.FirstOrDefault(d => d.Id == sevk.KaynakDepoId);
        var hedefDepo = depolar.FirstOrDefault(d => d.Id == sevk.HedefDepoId);
        if ((kaynakDepo == null || !YetkiliDepo(kaynakDepo)) && (hedefDepo == null || !YetkiliDepo(hedefDepo))) return Forbid();
        var kaynak = depolar.FirstOrDefault(d => d.Id == sevk.KaynakDepoId)?.Ad;
        var hedef = depolar.FirstOrDefault(d => d.Id == sevk.HedefDepoId)?.Ad;
        var basliklar = new List<string> { "S.No", "Taşınır", "Miktar", "Teslim Alınan" };
        int sira = 0;
        var satirlar = sevk.Kalemler.Select(k => (IList<string>)new List<string>
        {
            (++sira).ToString(), tanimlar.FirstOrDefault(t => t.Id == k.TasinirTanimId)?.Ad ?? k.TasinirTanimId,
            k.Miktar.ToString(), (k.TeslimAlinan?.ToString() ?? "-")
        });
        var bytes = _belge.WordTablo("SEVK İRSALİYESİ",
            $"Sevk No: {sevk.SevkNo} · {kaynak} → {hedef} · Taşıyıcı: {sevk.TasimaciAdi} · Plaka: {sevk.AracPlaka} · Tarih: {sevk.SevkTarihi:dd.MM.yyyy}",
            basliklar, satirlar, "Dayanak: Taşınır Mal Yönetmeliği – Taşınır İşlem Fişi (Çıkış/Sevk)");
        return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"Sevk-{sevk.SevkNo}.docx");
    }

    // ─── Tüketim Malzemesi Çıkışı (sarf — Taşınır İstek/Çıkış) ─
    [HttpGet]
    [Authorize(Roles = $"{AtomRoller.MerkezDepoSorumlusu},{AtomRoller.IlDepoSorumlusu},{AtomRoller.IlMuduru},{AtomRoller.SistemAdmin}")]
    public async Task<IActionResult> TuketimCikisi()
    {
        var depolar = await _svc.DepolariGetirAsync();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        var kullanicilar = await _svc.KullanicilariGetirAsync();
        if (!Bakanlik)
        {
            depolar = depolar.Where(d => d.KurumId == KurumId).ToList();
            kullanicilar = kullanicilar.Where(k => k.KurumId == KurumId).ToList();
        }
        ViewBag.Depolar = depolar;
        ViewBag.Personeller = kullanicilar.Where(k => k.AktifMi && k.Rol == AtomRoller.Personel).ToList();
        // Sadece sarf (demirbaş olmayan) tanımlar
        ViewBag.SarfTanimlar = tanimlar.Where(t => !t.DemirbasMi && t.AktifMi).ToList();
        return View();
    }

    [HttpPost]
    [Authorize(Roles = $"{AtomRoller.MerkezDepoSorumlusu},{AtomRoller.IlDepoSorumlusu},{AtomRoller.IlMuduru},{AtomRoller.SistemAdmin}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TuketimCikisi(string depoId, string tanimId, int miktar, string birim, string kullanimYeri, string? personelId)
    {
        var depo = await _svc.DepoGetirAsync(depoId);
        if (depo == null) { TempData["Hata"] = "Depo bulunamadı."; return RedirectToAction(nameof(TuketimCikisi)); }
        if (!YetkiliDepo(depo)) return Forbid();
        AtomKullanici? personel = null;
        if (!string.IsNullOrWhiteSpace(personelId))
        {
            personel = await _svc.KullaniciGetirAsync(personelId);
            if (personel == null || !YetkiliPersonel(personel)) return Forbid();
        }

        var mevcut = await _stok.MevcutStokAsync(depoId, tanimId);
        if (mevcut < miktar)
        {
            var tnm = await _svc.TasinirTanimGetirAsync(tanimId);
            TempData["Hata"] = $"Yetersiz stok: '{tnm?.Ad}' mevcut {mevcut}, istenen {miktar}.";
            return RedirectToAction(nameof(TuketimCikisi));
        }

        var belgeNo = await _svc.YeniNumaraUretAsync("TKT");
        if (personel != null)
        {
            await _stok.PersonelSarfVerAsync(new PersonelSarfIstegi
            {
                PersonelId = personel.Id,
                KurumId = personel.KurumId,
                KaynakDepoId = depoId,
                TasinirTanimId = tanimId,
                Miktar = miktar,
                KaynakBelgeNo = belgeNo,
                KullaniciId = KullaniciId,
                KullaniciAdi = AdSoyad,
                Aciklama = $"Personel sarf teslimi — Birim: {birim}, Kullanım: {kullanimYeri}, Personel: {personel.AdSoyad}"
            });
        }
        else
        {
            await _stok.CikisYapAsync(new StokHareketIstegi
            {
                DepoId = depoId, TasinirTanimId = tanimId, Miktar = miktar, IslemTuru = StokIslemTuru.TuketimCikisi,
                KaynakBelgeTur = "Tüketim", KaynakBelgeId = belgeNo, KaynakBelgeNo = belgeNo,
                KullaniciId = KullaniciId, KullaniciAdi = AdSoyad,
                Aciklama = $"Tüketim çıkışı — Birim: {birim}, Kullanım: {kullanimYeri}"
            });
        }
        await _audit.KaydetAsync(User, "Tüketim", "Çıkış", "StokHareket", belgeNo, $"{belgeNo} tüketim çıkışı ({miktar} adet)", ip: Ip);
        TempData["Basari"] = $"{belgeNo} numaralı tüketim çıkışı yapıldı, stok düşüldü.";
        return RedirectToAction(nameof(TuketimCikisi));
    }

    // ─── Stok Kartı / Hareket Geçmişi ─────────────────────────
    public async Task<IActionResult> StokKart(string depoId, string tanimId)
    {
        var depo = await _svc.DepoGetirAsync(depoId);
        if (depo == null) return NotFound();
        if (!YetkiliDepo(depo)) return Forbid();

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

        if (!Bakanlik)
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

        if (!Bakanlik)
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
        var depo = await _svc.DepoGetirAsync(mk.DepoId);
        if (depo == null) { TempData["Hata"] = "Depo bulunamadı."; return RedirectToAction(nameof(YeniMalKabul)); }
        if (!YetkiliDepo(depo)) return Forbid();

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
        var depo = await _svc.DepoGetirAsync(mk.DepoId);
        if (depo == null) return NotFound();
        if (!YetkiliDepo(depo)) return Forbid();

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

        if (!Bakanlik)
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
        if (!Bakanlik) depolar = depolar.Where(d => d.KurumId == KurumId).ToList();
        ViewBag.Depolar = depolar;
        ViewBag.Tanimlar = tanimlar.ToDictionary(t => t.Id, t => t.Ad);
        ViewBag.Kurumlar = kurumlar.ToDictionary(k => k.Id, k => k.Ad);
        return View(new Sevk());
    }

    [HttpPost]
    [Authorize(Roles = $"{AtomRoller.MerkezDepoSorumlusu},{AtomRoller.BakanlikMerkez},{AtomRoller.SistemAdmin}")]
    public async Task<IActionResult> YeniSevk(Sevk sevk, string[] tasinirIds, int[] miktarlar)
    {
        var kaynakDepo = await _svc.DepoGetirAsync(sevk.KaynakDepoId);
        var hedefDepo = await _svc.DepoGetirAsync(sevk.HedefDepoId);
        if (kaynakDepo == null || hedefDepo == null)
        {
            TempData["Hata"] = "Kaynak veya hedef depo bulunamadı.";
            return RedirectToAction(nameof(YeniSevk));
        }
        if (!YetkiliDepo(kaynakDepo) || !YetkiliDepo(hedefDepo)) return Forbid();

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
        var hedefDepo = await _svc.DepoGetirAsync(sevk.HedefDepoId);
        if (hedefDepo == null) return NotFound();
        if (!YetkiliDepo(hedefDepo)) return Forbid();

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
