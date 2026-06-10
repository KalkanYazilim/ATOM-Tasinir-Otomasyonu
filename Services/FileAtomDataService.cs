using System.Text.Json;
using ATOM.Models.Accounts;
using ATOM.Models.Domain;

namespace ATOM.Services;

public class FileAtomDataService : IAtomDataService
{
    private readonly string _basePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private static readonly Dictionary<string, SemaphoreSlim> _fileLocks = new();
    private static SemaphoreSlim FileLock(string key)
    {
        lock (_fileLocks) { return _fileLocks.TryGetValue(key, out var s) ? s : (_fileLocks[key] = new SemaphoreSlim(1, 1)); }
    }

    public FileAtomDataService(IWebHostEnvironment env, IConfiguration config)
    {
        var overridePath = config["AppData:Path"] ?? Environment.GetEnvironmentVariable("ATOM_APP_DATA_PATH");
        _basePath = overridePath ?? Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(_basePath);
    }

    private string FilePath(string name) => Path.Combine(_basePath, $"{name}.json");

    private async Task<List<T>> OkuAsync<T>(string dosya)
    {
        var path = FilePath(dosya);
        var lk = FileLock(dosya);
        await lk.WaitAsync();
        try
        {
            if (!File.Exists(path)) return new List<T>();
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<List<T>>(json, _json) ?? new List<T>();
        }
        finally { lk.Release(); }
    }

    private async Task YazAsync<T>(string dosya, List<T> liste)
    {
        var path = FilePath(dosya);
        var lk = FileLock(dosya);
        await lk.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(liste, _json);
            await File.WriteAllTextAsync(path, json);
        }
        finally { lk.Release(); }
    }

    private async Task UpsertAsync<T>(string dosya, T item, Func<T, string> idSelector)
    {
        var liste = await OkuAsync<T>(dosya);
        var idx = liste.FindIndex(x => idSelector(x) == idSelector(item));
        if (idx >= 0) liste[idx] = item; else liste.Add(item);
        await YazAsync(dosya, liste);
    }

    // ── Kullanıcılar ──────────────────────────────────────────
    public Task<List<AtomKullanici>> KullanicilariGetirAsync() => OkuAsync<AtomKullanici>("kullanicilar");
    public async Task<AtomKullanici?> KullaniciGetirAsync(string id) => (await KullanicilariGetirAsync()).FirstOrDefault(x => x.Id == id);
    public async Task<AtomKullanici?> KullaniciAdiylaGetirAsync(string ad) => (await KullanicilariGetirAsync()).FirstOrDefault(x => x.KullaniciAdi.ToLower() == ad.ToLower());
    public Task KullaniciKaydetAsync(AtomKullanici k) => UpsertAsync("kullanicilar", k, x => x.Id);
    public async Task KullaniciSilAsync(string id) { var l = await KullanicilariGetirAsync(); l.RemoveAll(x => x.Id == id); await YazAsync("kullanicilar", l); }

    // ── Kurumlar ──────────────────────────────────────────────
    public Task<List<Kurum>> KurumlariGetirAsync() => OkuAsync<Kurum>("kurumlar");
    public async Task<Kurum?> KurumGetirAsync(string id) => (await KurumlariGetirAsync()).FirstOrDefault(x => x.Id == id);
    public Task KurumKaydetAsync(Kurum k) => UpsertAsync("kurumlar", k, x => x.Id);

    // ── Firma ──────────────────────────────────────────────────
    public Task<List<Firma>> FirmalariGetirAsync() => OkuAsync<Firma>("firmalar");
    public async Task<Firma?> FirmaGetirAsync(string id) => (await FirmalariGetirAsync()).FirstOrDefault(x => x.Id == id);
    public Task FirmaKaydetAsync(Firma f) => UpsertAsync("firmalar", f, x => x.Id);

    // ── Taşınır Tanım ─────────────────────────────────────────
    public Task<List<TasinirTanim>> TasinirTanimlariGetirAsync() => OkuAsync<TasinirTanim>("tasinir-tanimlar");
    public async Task<TasinirTanim?> TasinirTanimGetirAsync(string id) => (await TasinirTanimlariGetirAsync()).FirstOrDefault(x => x.Id == id);
    public Task TasinirTanimKaydetAsync(TasinirTanim t) => UpsertAsync("tasinir-tanimlar", t, x => x.Id);

    // ── Depo ──────────────────────────────────────────────────
    public Task<List<Depo>> DepolariGetirAsync() => OkuAsync<Depo>("depolar");
    public async Task<Depo?> DepoGetirAsync(string id) => (await DepolariGetirAsync()).FirstOrDefault(x => x.Id == id);
    public Task DepoKaydetAsync(Depo d) => UpsertAsync("depolar", d, x => x.Id);
    public async Task StokGuncelleAsync(string depoId, string tasinirTanimId, int miktarDelta, decimal birimMaliyet = 0)
    {
        var depolar = await DepolariGetirAsync();
        var depo = depolar.FirstOrDefault(d => d.Id == depoId);
        if (depo == null) return;
        var stok = depo.Stoklar.FirstOrDefault(s => s.TasinirTanimId == tasinirTanimId);
        if (stok == null) { stok = new DepoStok { TasinirTanimId = tasinirTanimId }; depo.Stoklar.Add(stok); }
        stok.Miktar = Math.Max(0, stok.Miktar + miktarDelta);
        if (birimMaliyet > 0) stok.BirimMaliyet = birimMaliyet;
        stok.SonGuncelleme = DateTime.UtcNow;
        await YazAsync("depolar", depolar);
    }

    // ── İhtiyaç Talebi ────────────────────────────────────────
    public Task<List<IhtiyacTalebi>> TalepleriGetirAsync() => OkuAsync<IhtiyacTalebi>("talepler");
    public async Task<IhtiyacTalebi?> TalepGetirAsync(string id) => (await TalepleriGetirAsync()).FirstOrDefault(x => x.Id == id);
    public Task TalepKaydetAsync(IhtiyacTalebi t) => UpsertAsync("talepler", t, x => x.Id);

    // ── İhale ─────────────────────────────────────────────────
    public Task<List<Ihale>> IhaleleriGetirAsync() => OkuAsync<Ihale>("ihaleler");
    public async Task<Ihale?> IhaleGetirAsync(string id) => (await IhaleleriGetirAsync()).FirstOrDefault(x => x.Id == id);
    public Task IhaleKaydetAsync(Ihale i) => UpsertAsync("ihaleler", i, x => x.Id);

    // ── Mal Kabul ─────────────────────────────────────────────
    public Task<List<MalKabul>> MalKabulleriGetirAsync() => OkuAsync<MalKabul>("mal-kabuller");
    public async Task<MalKabul?> MalKabulGetirAsync(string id) => (await MalKabulleriGetirAsync()).FirstOrDefault(x => x.Id == id);
    public Task MalKabulKaydetAsync(MalKabul mk) => UpsertAsync("mal-kabuller", mk, x => x.Id);

    // ── Sevk ──────────────────────────────────────────────────
    public Task<List<Sevk>> SevkleriGetirAsync() => OkuAsync<Sevk>("sevkler");
    public async Task<Sevk?> SevkGetirAsync(string id) => (await SevkleriGetirAsync()).FirstOrDefault(x => x.Id == id);
    public Task SevkKaydetAsync(Sevk s) => UpsertAsync("sevkler", s, x => x.Id);

    // ── Zimmet ────────────────────────────────────────────────
    public Task<List<Zimmet>> ZimmetleriGetirAsync() => OkuAsync<Zimmet>("zimmetler");
    public async Task<Zimmet?> ZimmetGetirAsync(string id) => (await ZimmetleriGetirAsync()).FirstOrDefault(x => x.Id == id);
    public Task ZimmetKaydetAsync(Zimmet z) => UpsertAsync("zimmetler", z, x => x.Id);

    // ── Bakım ─────────────────────────────────────────────────
    public Task<List<BakimKaydi>> BakimKayitlariGetirAsync() => OkuAsync<BakimKaydi>("bakim-kayitlar");
    public async Task<BakimKaydi?> BakimKaydiGetirAsync(string id) => (await BakimKayitlariGetirAsync()).FirstOrDefault(x => x.Id == id);
    public Task BakimKaydiKaydetAsync(BakimKaydi k) => UpsertAsync("bakim-kayitlar", k, x => x.Id);

    // ── Hurda ─────────────────────────────────────────────────
    public Task<List<HurdaKaydi>> HurdaKayitlariGetirAsync() => OkuAsync<HurdaKaydi>("hurda-kayitlar");
    public async Task<HurdaKaydi?> HurdaKaydiGetirAsync(string id) => (await HurdaKayitlariGetirAsync()).FirstOrDefault(x => x.Id == id);
    public Task HurdaKaydiKaydetAsync(HurdaKaydi k) => UpsertAsync("hurda-kayitlar", k, x => x.Id);

    // ── Bildirim ──────────────────────────────────────────────
    public async Task<List<Bildirim>> BildirimleriGetirAsync(string kullaniciId)
        => (await OkuAsync<Bildirim>("bildirimler")).Where(b => b.AliciKullaniciId == kullaniciId).OrderByDescending(b => b.Tarih).ToList();

    public Task BildirimKaydetAsync(Bildirim b) => UpsertAsync("bildirimler", b, x => x.Id);

    public async Task BildirimOkunduIsaretle(string bildirimId)
    {
        var liste = await OkuAsync<Bildirim>("bildirimler");
        var b = liste.FirstOrDefault(x => x.Id == bildirimId);
        if (b != null) { b.OkunduMu = true; await YazAsync("bildirimler", liste); }
    }

    // ── Numara Üreteci ────────────────────────────────────────
    public async Task<string> YeniNumaraUretAsync(string prefix)
    {
        var sayaclar = await OkuAsync<NumaraSayac>("numaralar");
        var sayac = sayaclar.FirstOrDefault(s => s.Prefix == prefix);
        if (sayac == null) { sayac = new NumaraSayac { Prefix = prefix }; sayaclar.Add(sayac); }
        sayac.Sayac++;
        await YazAsync("numaralar", sayaclar);
        return $"{prefix}-{DateTime.UtcNow.Year}-{sayac.Sayac:D6}";
    }

    private class NumaraSayac
    {
        public string Prefix { get; set; } = "";
        public int Sayac { get; set; }
    }
}
