# V12-ONL-004 - Publish channel catalog

- Task ID: V12-ONL-004
- Status: Done
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: integration
- Surface state: Planned

## Source basis

- PDF:I.34-I.37
- PDF:II.2.19
- PDF:II.7.4
- PDF:III.22

## Goal

Onaylanan menü/ürün projeksiyonunu, deterministik harici tanımlayıcılarla etkinleştirilmiş her çevrimiçi-order kanalına
yayınlayın.

## Owned surface

- `src/Modules/OnlineOrdering/CatalogPublishing/**`, `tests/Modules/OnlineOrdering/CatalogPublishing/**`,
  `database/migrations/V12/V12-ONL-004/**`
- `src/Host/Experience/OnlineOrdering/OnlineCatalogPublishingEndpoints.cs` — yöneticinin yayını başlattığı HTTP ucu
  (`integrations.manage`); bu görevle oluşturulan yeni dosya.
- `evidence/V12-ONL-004/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-25 "Sınırlı ek + yol
  notu" kararı):
  - src/Modules/OnlineOrdering/Yemeksepeti/ProductMapping/ (V12-MAP-001 sahipliğinde) — yalnız
    `FindOpenSkuForProductAsync`.
  - src/Modules/OnlineOrdering/Yemeksepeti/StatusSync/YemeksepetiPartnerClient.cs ve
    tests/Modules/OnlineOrdering/Yemeksepeti/StatusSync/ (V12-ONL-003 sahipliğinde) — satıcı kataloğu güncelleme
    çağrısı (`UpdateVendorCatalogAsync`, doğrulanmamış taslak) ve onun iki sahte handler testi.
  - src/Modules/OnlineOrdering/OnlineOrderingModule.cs (V12-MAP-001 sahipliğinde) — yayın servisi, kanal ve tüketici
    kayıtları.
  - src/Host/DualScreen/DualScreenApplication.cs — ucun kaydı ve eşlenmesi.
  - tests/Host/Experience/OnlineOrdering/ (V12-ONL-001 sahipliğinde) — uç için üç HTTP testi, oturum ve menü
    fixture yardımcıları, 066/079/147 fixture satırları.
  - database/MigrationComposition/order.json, src/Host/Composition/Migrations/MigrationManifest.cs ve
    tests/Host/MigrationComposition/Manifest/ManifestTests.cs — 147 numaralı migration konumu.
  - ALKAROS.slnx ve `dotnet restore`'un ürettiği packages.lock.json.
- Kaynak ve kapsam notu: yalnız kayıtlı kaynak developer.yemeksepeti.com/api-specifications kullanıldı. Bu kaynağa göre
  kararlı üretim çağrısı `PUT /v2/chains/{chain_id}/vendors/{vendor_id}/catalog` ile yalnız satıcı kataloğunda zaten
  bulunan ürünlerin fiyatını ve etkinliğini günceller; ürün eklemek pilot/beta çağrısıdır. Adlar, KDV, modifier ve iç
  kategoriler için alan yoktur. Bu yetenekler her yayında `UnsupportedCapabilities` olarak kaydedilir; modifier'lı
  veya fiyatsız ürünler tipli doğrulama hatasıyla dışarıda bırakılır. Menü ve katalog, şemalar arası salt okumayla
  okunur (V0-ARC-001); yeni modül kenarı eklenmedi. Sandbox kanıtı yoktur: V0-YSP-001 ve V20-INT-003 sahipliğinde açık
  kalır (`V12-GOV-004`).

## In scope

- Provider yeteneği contract, katalog projeksiyonu, bağımsız yayınlama, harici ID kalıcılığı, retry ve sonuç denetimi.

## Out of scope

- Stok kullanılabilirliği, fiyat sahipliği, gelen order webhook'lar ve operatör UI.

## Dependencies

- V12-MAP-001
- V11-MNU-001
- V11-MNU-002
- V11-MNU-003
- V1-CAT-001
- V1-CAT-002
- V0-CMP-002
- V0-YSP-001

## Deliverables

- Onaylanan her kanal için Provider'ye özel katalog yayıncısı.
- etkin provider'lar için Contract testleri ve gerçek sandbox kanıtları.
- Açık doğrulama hataları olarak kaydedilen desteklenmeyen provider yetenekleri.

## Acceptance evidence

- Aynı yayınlamanın tekrarlanması harici ürünün kopyalanmasına neden olmaz; eşlenen adlar, fiyatlar, vergi meta verileri
  ve değiştirici yapı, onaylanan provider yanıtıyla eşleşir.
- Kapanış kanıtı (2026-09-26, gerçek PostgreSQL 18 UTF8, port 56433). Test sayıları: modül 11/11, istemci 15/15,
  Host 29/29 (3'ü yeni HTTP testi).
  - Yayın, ürünleri kalıcı SKU'larla yazar ve outbox üzerinden teslim eder; teslimde sağlayıcıya SKU, fiyat ve
    etkinlik gider.
  - Aynı menünün tekrar yayını `Unchanged` olur, sağlayıcı yeniden çağrılmaz ve ürün başına tek eşleme kalır. Yani
    dış ürün kopyalanmaz.
  - Beş turda altı eşzamanlı yayından tam olarak biri kuyruğa girer.
  - Fiyat değişikliği yeni bir yayındır.
  - Modifier'lı ürün `ModifiersNotSupported`, fiyatsız ürün `PriceMissing` hatası verir.
  - Pasifleşen ürün yalnız kanal onu zaten tanıyorsa kapatılır.
  - Var olan manuel eşleme dış kimlik olarak kullanılır. Başka ürüne ait bir SKU asla devralınmaz
    (`ExternalIdUnavailable`).
  - Başarısız teslim kaydedilip yeniden denenir, teslim edilmiş yayın ikinci kez gönderilmez.
  - Uç oturumsuz isteğe 401, yetkisiz isteğe 403, bilinmeyen kanala Türkçe mesajla 400 döner.
  - Migration 147 geri alınıp yeniden uygulanır.
  - Kabulün "onaylanan provider yanıtıyla eşleşir" kısmı bu görevde karşılanmış sayılmaz: gerçek sağlayıcı yanıtı
    ancak sandbox erişimiyle, V20-INT-003'te kanıtlanabilir.
- Testlerin bulduğu gerçek hata: yayın, başka bir ürüne eşli katalog SKU'sunu MAP-001'in tarihli yeniden eşleme
  kuralıyla sessizce yeni ürüne devrediyordu; yayıncı artık sahibi olan bir SKU'yu devralmaz.
- Mutasyon kontrolü (dosyalar yedekten geri yüklenip `cmp` ile doğrulandı): `Unchanged` kontrolü kapatılınca 2, SKU
  sahipliği kontrolü kaldırılınca 1, teslim durumu kontrolü kaldırılınca 1, modifier kontrolü kapatılınca 1, kilit
  kaldırılınca 1 test kırmızıya döndü. Kilit mutasyonu ilk denemede hayatta kaldı; bunun üzerine yarış testi gerçek
  eşzamanlılığa çevrildi ve mutasyon iki koşuda da yakalandı.
- Kanıt: `evidence/V12-ONL-004/`.

## Handoff

- V12-ONL-005
- V12-OUI-001
