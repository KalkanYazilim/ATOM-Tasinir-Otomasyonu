# ATOM – Taşınır Malzeme Otomasyonu

Bakanlık seviyesinde taşınır malzeme yönetim sistemi. ASP.NET Core 8 MVC, JSON dosya tabanlı, rol bazlı çok katmanlı onay süreçleri.

## Hızlı Başlangıç

```bash
cd ATOM
dotnet run
```
Tarayıcıdan `https://localhost:7xxx` adresine gidin.

## Demo Hesaplar

| Kullanıcı | Şifre | Rol |
|-----------|-------|-----|
| admin | Admin123! | Sistem Yöneticisi |
| merkez | Merkez123! | Bakanlık Merkez |
| satinalma | Satin123! | Bakanlık Satın Alma |
| merkezdepo | Depo123! | Merkez Depo Sorumlusu |
| ankara | Ankara123! | Ankara İl Müdürü |
| istanbul | Istanbul123! | İstanbul İl Müdürü |
| ankaradepo | Depo123! | Ankara Depo Sorumlusu |
| tedarikci1 | Firma123! | Tedarikçi Firma |
| personel1 | Personel123! | Personel |

## Özellikler

- **İhtiyaç Talebi:** İlden Bakanlığa çok aşamalı onay akışı
- **İhale Yönetimi:** İhale oluşturma → İlan → Teklif alma → Değerlendirme → Sonuçlandırma
- **Mal Kabul:** İhale kazananından depoya mal girişi + onay
- **Depo & Stok:** Merkez ve il depoları, kritik stok uyarıları
- **Sevk:** Depo-depo veya il'e dağıtım
- **Zimmet:** Personele taşınır dağıtımı + iade
- **Bakım/Arıza:** Arıza bildirimi → Teknik müdahale → Tamamlama
- **Hurda/İmha:** Komisyon süreci
- **Raporlama:** İl bazlı karşılaştırma, stok analizi, trend grafikleri

## Mimari

```
ATOM/
├── Areas/
│   ├── Talep/          # İhtiyaç talepleri
│   ├── Ihale/          # İhale yönetimi
│   ├── Depo/           # Depo, stok, mal kabul, sevk
│   ├── Zimmet/         # Zimmet ve bakım
│   ├── Raporlama/      # Analiz ve raporlar
│   └── Yonetim/        # Sistem yönetimi
├── Models/
│   ├── Domain/         # Tüm iş modelleri
│   └── Accounts/       # Kullanıcı ve roller
├── Services/           # Dosya tabanlı JSON servisi
└── App_Data/           # JSON veri dosyaları (git ignore)
```

## Roller

| Rol | Yetki |
|-----|-------|
| SistemAdmin | Tam erişim |
| BakanlikMerkez | Tüm talep ve raporlar |
| BakanlikSatinAlma | İhale yönetimi |
| MerkezDepoSorumlusu | Merkez depo, mal kabul, sevk |
| IlMuduru | İl talepler, onay |
| IlDepoSorumlusu | İl depo, zimmet |
| Teknisyen | Bakım/onarım |
| Personel | Kendi zimmetleri |
| Tedarikci | İhale teklif |
