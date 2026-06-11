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
    private bool YetkiliDepo(Depo depo) => Bakanlik || depo.KurumId == KurumId;
    private bool YetkiliPersonel(AtomKullanici personel) =>
        personel.AktifMi && personel.Rol == AtomRoller.Personel && (Bakanlik || personel.KurumId == KurumId);

    public async Task<IActionResult> Index()
    {
        var depolar = await _svc.DepolariGetirAsync();
        var tanimlar = await _svc.TasinirTanimlariGetirAsync();
        var kayitlar = await _svc.TasinirKayitlariGetirAsync();
        var kullanicilar = await _svc.KullanicilariGetirAsync();
        var kurumlar = await _svc.KurumlariGetirAsync();
        var zimmetler = await _svc.ZimmetleriGetirAsync();
        var personelSarfBakiyeleri = await _svc.PersonelSarfBakiyeleriGetirAsync();

        if (!Bakanlik)
        {
            depolar = depolar.Where(d => d.KurumId == KurumId).ToList();
            kullanicilar = kullanicilar.Where(k => k.KurumId == KurumId).ToList();
        }
        var depoIds = depolar.Select(d => d.Id).ToHashSet();

        ViewBag.Tanimlar = tanimlar.ToDictionary(t => t.Id, t => t);
        ViewBag.Kurumlar = kurumlar.ToDictionary(k => k.Id, k => k.Ad);
        ViewBag.KurumDetaylari = kurumlar.ToDictionary(k => k.Id, k => k);
        ViewBag.Personeller = kullanicilar.Where(k => k.AktifMi && k.Rol == AtomRoller.Personel).ToList();
        var personelIds = kullanicilar.Where(k => k.AktifMi && k.Rol == AtomRoller.Personel).Select(k => k.Id).ToHashSet();
        var yetkiliDepolar = depolar.ToDictionary(d => d.Id, d => d);
        ViewBag.PersonelZimmetleri = zimmetler
            .Where(z => z.Durum == ZimmetDurumu.Aktif && personelIds.Contains(z.PersonelId))
            .GroupBy(z => z.PersonelId)
            .ToDictionary(g => g.Key, g => g.ToList());
        ViewBag.PersonelSarfBakiyeleri = personelSarfBakiyeleri
            .Where(b => b.Miktar > 0 && personelIds.Contains(b.PersonelId))
            .GroupBy(b => b.PersonelId)
            .ToDictionary(g => g.Key, g => g.ToList());
        ViewBag.PersonelBagliDepolari = personelIds.ToDictionary(
            id => id,
            id =>
            {
                var zimmetDepolari = zimmetler
                    .Where(z => z.Durum == ZimmetDurumu.Aktif && z.PersonelId == id && yetkiliDepolar.ContainsKey(z.DepoId))
                    .Select(z => z.DepoId);
                var sarfDepolari = personelSarfBakiyeleri
                    .Where(b => b.Miktar > 0 && b.PersonelId == id && yetkiliDepolar.ContainsKey(b.KaynakDepoId))
                    .Select(b => b.KaynakDepoId);
                return zimmetDepolari.Concat(sarfDepolari).Distinct().ToList();
            });
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
        if (!YetkiliDepo(kaynak) || !YetkiliDepo(hedef)) return Json(new { ok = false, msg = "Bu depo işlemi için yetkiniz yok." });
        if (kaynakDepoId == hedefDepoId) return Json(new { ok = false, msg = "Kaynak ve hedef depo aynı olamaz." });
        if (miktar <= 0) return Json(new { ok = false, msg = "Miktar geçersiz." });

        var mevcut = await _stok.MevcutStokAsync(kaynakDepoId, tanimId);
        if (mevcut < miktar) return Json(new { ok = false, msg = $"Yetersiz stok (mevcut {mevcut})." });
        var birimMaliyet = kaynak.Stoklar.FirstOrDefault(s => s.TasinirTanimId == tanimId)?.BirimMaliyet ?? 0;

        var sevk = new Sevk
        {
            SevkNo = await _svc.YeniNumaraUretAsync("SVK"),
            KaynakDepoId = kaynakDepoId, HedefDepoId = hedefDepoId, HedefKurumId = hedef.KurumId,
            OlusturanKullaniciId = KullaniciId, SevkTarihi = DateTime.UtcNow, GercekVarisTarihi = DateTime.UtcNow,
            Durum = SevkDurumu.TeslimEdildi,
            Aciklama = "Operasyon panosundan sürükle-bırak ile anlık depo transferi olarak oluşturuldu.",
            Kalemler = new() { new SevkKalemi { TasinirTanimId = tanimId, Miktar = miktar, TeslimAlinan = miktar } }
        };
        sevk.OnayGecmisi.Add(new OnayKaydi { KullaniciId = KullaniciId, KullaniciAdi = AdSoyad, Rol = Rol, Karar = OnayDurumu.Onaylandi, Asama = "Anlık Sevk (Pano)" });
        await _stok.CikisYapAsync(new StokHareketIstegi
        {
            DepoId = kaynakDepoId, TasinirTanimId = tanimId, Miktar = miktar, BirimMaliyet = birimMaliyet, IslemTuru = StokIslemTuru.SevkCikisi,
            KaynakBelgeTur = "Sevk", KaynakBelgeId = sevk.Id, KaynakBelgeNo = sevk.SevkNo,
            KullaniciId = KullaniciId, KullaniciAdi = AdSoyad, Aciklama = $"{sevk.SevkNo} sevk çıkışı (pano)"
        });
        await _stok.GirisYapAsync(new StokHareketIstegi
        {
            DepoId = hedefDepoId, TasinirTanimId = tanimId, Miktar = miktar, BirimMaliyet = birimMaliyet, IslemTuru = StokIslemTuru.SevkGirisi,
            KaynakBelgeTur = "Sevk", KaynakBelgeId = sevk.Id, KaynakBelgeNo = sevk.SevkNo,
            KullaniciId = KullaniciId, KullaniciAdi = AdSoyad, Aciklama = $"{sevk.SevkNo} sevk girişi (pano)"
        });
        await _svc.SevkKaydetAsync(sevk);
        await _bildirim.KurumaBildirAsync(hedef.KurumId, "Anlık Sevk Tamamlandı",
            $"{sevk.SevkNo} numaralı sevk operasyon panosundan hedef depoya işlendi.", BildirimTur.Bilgi, "/depo/Home/Sevkler",
            sevk.Id, "Sevk", "Normal", new[] { AtomRoller.IlDepoSorumlusu, AtomRoller.IlMuduru });
        await _audit.KaydetAsync(User, "Operasyon", "Sevk", "Sevk", sevk.Id, $"{sevk.SevkNo} pano sevk", ip: Ip);
        return Json(new { ok = true, msg = $"{sevk.SevkNo} tamamlandı; kaynak stok düştü, hedef stok eklendi." });
    }

    // ─── Sürükle: Tekil Demirbaş → Depo (Sicil/barkod bazlı sevk) ─
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DemirbasSevkYap(string tasinirKayitId, string hedefDepoId)
    {
        var kayit = await _svc.TasinirKayitGetirAsync(tasinirKayitId);
        var hedef = await _svc.DepoGetirAsync(hedefDepoId);
        if (kayit == null || hedef == null) return Json(new { ok = false, msg = "Demirbaş veya hedef depo bulunamadı." });
        if (kayit.Durum != TasinirKayitDurumu.Ambarda) return Json(new { ok = false, msg = "Demirbaş ambarda değil; önce zimmet/iade durumunu kontrol edin." });
        if (string.IsNullOrWhiteSpace(kayit.DepoId) || kayit.DepoId == hedefDepoId) return Json(new { ok = false, msg = "Kaynak ve hedef depo aynı olamaz." });

        var kaynak = await _svc.DepoGetirAsync(kayit.DepoId);
        if (kaynak == null) return Json(new { ok = false, msg = "Kaynak depo bulunamadı." });
        if (!YetkiliDepo(kaynak) || !YetkiliDepo(hedef)) return Json(new { ok = false, msg = "Bu demirbaş sevki için yetkiniz yok." });
        if (string.IsNullOrWhiteSpace(kayit.TasinirTanimId)) return Json(new { ok = false, msg = "Demirbaş katalog tanımı eksik." });

        var birimMaliyet = kaynak.Stoklar.FirstOrDefault(s => s.TasinirTanimId == kayit.TasinirTanimId)?.BirimMaliyet ?? kayit.BirimFiyat;
        var sevk = new Sevk
        {
            SevkNo = await _svc.YeniNumaraUretAsync("SVK"),
            KaynakDepoId = kaynak.Id,
            HedefDepoId = hedef.Id,
            HedefKurumId = hedef.KurumId,
            OlusturanKullaniciId = KullaniciId,
            SevkTarihi = DateTime.UtcNow,
            GercekVarisTarihi = DateTime.UtcNow,
            Durum = SevkDurumu.TeslimEdildi,
            Aciklama = $"Operasyon panosundan tekil demirbaş sevki. Sicil: {kayit.SicilNo}, Barkod: {kayit.BarKod}",
            Kalemler = new()
            {
                new SevkKalemi
                {
                    TasinirTanimId = kayit.TasinirTanimId,
                    Miktar = 1,
                    TeslimAlinan = 1,
                    TasinirKayitIds = new() { kayit.Id }
                }
            }
        };
        sevk.OnayGecmisi.Add(new OnayKaydi { KullaniciId = KullaniciId, KullaniciAdi = AdSoyad, Rol = Rol, Karar = OnayDurumu.Onaylandi, Asama = "Tekil Demirbaş Sevki (Pano)" });

        await _stok.CikisYapAsync(new StokHareketIstegi
        {
            DepoId = kaynak.Id,
            TasinirTanimId = kayit.TasinirTanimId,
            TasinirKayitId = kayit.Id,
            Miktar = 1,
            BirimMaliyet = birimMaliyet,
            IslemTuru = StokIslemTuru.SevkCikisi,
            KaynakBelgeTur = "Sevk",
            KaynakBelgeId = sevk.Id,
            KaynakBelgeNo = sevk.SevkNo,
            KullaniciId = KullaniciId,
            KullaniciAdi = AdSoyad,
            Aciklama = $"{sevk.SevkNo} tekil demirbaş sevk çıkışı ({kayit.SicilNo})"
        });
        await _stok.GirisYapAsync(new StokHareketIstegi
        {
            DepoId = hedef.Id,
            TasinirTanimId = kayit.TasinirTanimId,
            TasinirKayitId = kayit.Id,
            Miktar = 1,
            BirimMaliyet = birimMaliyet,
            IslemTuru = StokIslemTuru.SevkGirisi,
            KaynakBelgeTur = "Sevk",
            KaynakBelgeId = sevk.Id,
            KaynakBelgeNo = sevk.SevkNo,
            KullaniciId = KullaniciId,
            KullaniciAdi = AdSoyad,
            Aciklama = $"{sevk.SevkNo} tekil demirbaş sevk girişi ({kayit.SicilNo})"
        });

        var hedefKurum = await _svc.KurumGetirAsync(hedef.KurumId);
        kayit.DepoId = hedef.Id;
        kayit.KurumId = hedef.KurumId;
        kayit.AmbarAdi = hedef.Ad;
        kayit.IlAdi = hedefKurum?.Il ?? kayit.IlAdi;
        kayit.GuncellemeTarihi = DateTime.UtcNow;
        kayit.FisSonDurum = "Ambarda";
        kayit.HareketGecmisi.Add(new TasinirHareket
        {
            IslemTuru = "Demirbaş Sevk",
            Aciklama = $"{sevk.SevkNo} ile {kaynak.Ad} deposundan {hedef.Ad} deposuna sevk edildi.",
            KullaniciId = KullaniciId,
            KullaniciAdi = AdSoyad,
            OncekiDurum = "Ambarda",
            YeniDurum = "Ambarda"
        });
        await _svc.TasinirKayitKaydetAsync(kayit);
        await _svc.SevkKaydetAsync(sevk);
        await _audit.KaydetAsync(User, "Operasyon", "Demirbaş Sevk", "Sevk", sevk.Id, $"{sevk.SevkNo} {kayit.SicilNo} pano demirbaş sevk", ip: Ip);

        return Json(new { ok = true, msg = $"{kayit.SicilNo} sicilli demirbaş {hedef.Ad} deposuna aktarıldı." });
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
        if (!YetkiliPersonel(personel)) return Json(new { ok = false, msg = "Bu personele zimmet verme yetkiniz yok." });

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

    // ─── Sürükle: Personel zimmeti → Bağlı depo (Zimmet iadesi) ─
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ZimmetIadeYap(string zimmetId, string tasinirKayitId, string hedefDepoId)
    {
        var zimmet = await _svc.ZimmetGetirAsync(zimmetId);
        var kayit = await _svc.TasinirKayitGetirAsync(tasinirKayitId);
        var hedef = await _svc.DepoGetirAsync(hedefDepoId);
        if (zimmet == null || kayit == null || hedef == null) return Json(new { ok = false, msg = "Zimmet/demirbaş/depo bulunamadı." });
        if (zimmet.DepoId != hedefDepoId) return Json(new { ok = false, msg = "Bu demirbaş yalnızca zimmetin bağlı olduğu depoya iade edilebilir." });
        if (!YetkiliDepo(hedef)) return Json(new { ok = false, msg = "Bu depoya iade alma yetkiniz yok." });

        var kalem = zimmet.Kalemler.FirstOrDefault(k => k.TasinirKayitId == tasinirKayitId && k.ItemDurumu == ZimmetDurumu.Aktif);
        if (kalem == null) return Json(new { ok = false, msg = "Aktif zimmet kalemi bulunamadı." });

        kalem.ItemDurumu = ZimmetDurumu.Iade;
        kalem.HasarAciklama = "Operasyon panosundan sürükle-bırak ile depoya iade edildi.";
        if (zimmet.Kalemler.All(k => k.ItemDurumu != ZimmetDurumu.Aktif))
        {
            zimmet.Durum = ZimmetDurumu.Iade;
            zimmet.IadeTarihi = DateTime.UtcNow;
            zimmet.IadeAciklama = "Operasyon panosundan tüm kalemler iade edildi.";
        }
        zimmet.OnayGecmisi.Add(new OnayKaydi
        {
            KullaniciId = KullaniciId,
            KullaniciAdi = AdSoyad,
            Rol = Rol,
            Karar = OnayDurumu.Onaylandi,
            Asama = "Zimmet İade (Pano)",
            Aciklama = $"{kayit.SicilNo} sicilli demirbaş {hedef.Ad} deposuna iade edildi."
        });

        await _kayit.DurumDegistirAsync(kayit.Id, TasinirKayitDurumu.Ambarda, "Zimmet İade",
            $"{zimmet.ZimmetNo} zimmetinden operasyon panosunda iade alındı.", KullaniciId, AdSoyad, zimmetId: "", depoId: hedefDepoId);
        await _svc.ZimmetKaydetAsync(zimmet);
        await _audit.KaydetAsync(User, "Operasyon", "Zimmet İade", "Zimmet", zimmet.Id,
            $"{zimmet.ZimmetNo} {kayit.SicilNo} pano iade", ip: Ip);
        return Json(new { ok = true, msg = $"{kayit.SicilNo} sicilli demirbaş {hedef.Ad} deposuna iade alındı." });
    }

    // ─── Sürükle: Sarf → Personel (Personel adına tüketim çıkışı) ─
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TuketimYap(string depoId, string tanimId, int miktar, string personelId, string kullanimYeri)
    {
        var depo = await _svc.DepoGetirAsync(depoId);
        var personel = await _svc.KullaniciGetirAsync(personelId);
        var tanim = await _svc.TasinirTanimGetirAsync(tanimId);
        if (depo == null || personel == null || tanim == null) return Json(new { ok = false, msg = "Depo/personel/taşınır bulunamadı." });
        if (!YetkiliDepo(depo) || !YetkiliPersonel(personel)) return Json(new { ok = false, msg = "Bu tüketim çıkışı için yetkiniz yok." });
        if (tanim.DemirbasMi) return Json(new { ok = false, msg = "Demirbaş tüketim çıkışı yapılamaz; zimmet veya düşüm süreci kullanılmalı." });
        if (miktar <= 0) return Json(new { ok = false, msg = "Miktar geçersiz." });

        var mevcut = await _stok.MevcutStokAsync(depoId, tanimId);
        if (mevcut < miktar) return Json(new { ok = false, msg = $"Yetersiz stok (mevcut {mevcut})." });

        var belgeNo = await _svc.YeniNumaraUretAsync("TKT");
        await _stok.PersonelSarfVerAsync(new PersonelSarfIstegi
        {
            KaynakDepoId = depoId,
            TasinirTanimId = tanimId,
            Miktar = miktar,
            PersonelId = personelId,
            KurumId = personel.KurumId,
            KaynakBelgeNo = belgeNo,
            KullaniciId = KullaniciId,
            KullaniciAdi = AdSoyad,
            Aciklama = $"Personel sarf teslimi — Personel: {personel.AdSoyad}, Kullanım: {kullanimYeri}"
        });
        await _audit.KaydetAsync(User, "Operasyon", "Tüketim", "StokHareket", belgeNo,
            $"{belgeNo} pano personel sarf teslimi ({personel.AdSoyad}, {miktar} adet)", ip: Ip);
        return Json(new { ok = true, msg = $"{belgeNo} sarf teslimi yapıldı; malzeme {personel.AdSoyad} üzerinde takip ediliyor." });
    }

    // ─── Sürükle: Personel sarf bakiyesi → Bağlı depo (Sarf iadesi) ─
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PersonelSarfIadeYap(string bakiyeId, string hedefDepoId, int miktar)
    {
        var bakiye = (await _svc.PersonelSarfBakiyeleriGetirAsync()).FirstOrDefault(b => b.Id == bakiyeId);
        var hedef = await _svc.DepoGetirAsync(hedefDepoId);
        if (bakiye == null || hedef == null) return Json(new { ok = false, msg = "Personel sarf bakiyesi veya depo bulunamadı." });
        if (bakiye.KaynakDepoId != hedefDepoId) return Json(new { ok = false, msg = "Sarf malzeme yalnızca personele verildiği bağlı depoya iade edilebilir." });
        if (!YetkiliDepo(hedef)) return Json(new { ok = false, msg = "Bu depoya iade alma yetkiniz yok." });
        if (miktar <= 0 || miktar > bakiye.Miktar) return Json(new { ok = false, msg = "İade miktarı geçersiz." });

        var belgeNo = await _svc.YeniNumaraUretAsync("PSI");
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
            Aciklama = "Operasyon panosundan bağlı depoya sarf iadesi."
        });
        await _stok.GirisYapAsync(new StokHareketIstegi
        {
            DepoId = hedefDepoId,
            TasinirTanimId = bakiye.TasinirTanimId,
            Miktar = miktar,
            IslemTuru = StokIslemTuru.Duzeltme,
            KaynakBelgeTur = "PersonelSarfIade",
            KaynakBelgeId = belgeNo,
            KaynakBelgeNo = belgeNo,
            KullaniciId = KullaniciId,
            KullaniciAdi = AdSoyad,
            Aciklama = "Personelden bağlı depoya sarf iadesi."
        });
        await _audit.KaydetAsync(User, "Operasyon", "Personel Sarf İade", "PersonelSarfBakiye", bakiye.Id,
            $"{belgeNo} pano sarf iadesi ({miktar})", ip: Ip);
        return Json(new { ok = true, msg = $"{belgeNo} sarf iadesi bağlı depoya alındı." });
    }

    // ─── Sürükle: Ürün → Hurda alanı (Hurda talebi) ───────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HurdaYap(string depoId, string tanimId, int miktar, string? tasinirKayitId, string gerekce)
    {
        var depo = await _svc.DepoGetirAsync(depoId);
        if (depo == null) return Json(new { ok = false, msg = "Depo bulunamadı." });
        if (!YetkiliDepo(depo)) return Json(new { ok = false, msg = "Yetkiniz yok." });

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
