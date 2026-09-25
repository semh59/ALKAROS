# V12-MAP-001 - Implement provider product mapping

- Task ID: V12-MAP-001
- Status: Done
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.34-I.37
- PDF:II.2.19
- PDF:II.7.4
- PDF:III.22

## Goal

provider ürün/değiştirici tanımlayıcılarını, açık eşlenmemiş davranışa sahip etkin dahili katalog öğeleriyle eşleyin.

## Owned surface

- `src/Modules/OnlineOrdering/Yemeksepeti/ProductMapping/**`,
  `tests/Modules/OnlineOrdering/Yemeksepeti/ProductMapping/**`, `database/migrations/V12/V12-MAP-001/**`
- `src/Modules/OnlineOrdering/ALKAROS.OnlineOrdering.csproj` — Online Ordering modülünün (module-dependency-rules.md
  satır 20) ilk kodu bu görevde yazıldığı için proje dosyası bu görevle oluşturuldu.
- `src/Modules/OnlineOrdering/OnlineOrderingModule.cs` — modülün DI kayıt noktası; sonraki V12-ONL görevleri kendi
  kayıtlarını sınırlı ek olarak buraya ekler.
- `evidence/V12-MAP-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan yazıldı; Semih 2026-09-25 "Sınırlı ek +
  yol notu" kararı):
  - src/Host/Composition/Modules/ModuleRegistry.cs ve src/Host/ALKAROS.Host.csproj — yeni modülün katalog kaydı ve
    proje referansı.
  - database/MigrationComposition/order.json, src/Host/Composition/Migrations/MigrationManifest.cs ve
    tests/Host/MigrationComposition/Manifest/ManifestTests.cs — 144 numaralı migration konumu.
  - tests/Host/MigrationComposition/Composition/HostModuleReachabilityTests.cs — modül sayısı 31'den 32'ye çıktı.
  - tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs — `ALKAROS.OnlineOrdering` derlemesi ve satır 20'nin
    Catalog kenarı onaylı listeye eklendi.
  - tools/consistency-audit/unreachable_services_allowlist.json — eşleme servisi için iki satır; ilk çalışma zamanı
    çağıranı V12-ONL-002 veya V12-ONL-004 bağladığında silinir.
  - ALKAROS.slnx ve `dotnet restore --force-evaluate`'in mekanik olarak güncellediği packages.lock.json dosyaları.
- Kaynak notu: modifier eşlemesi, herkese açık Partner API v2.0.2 sipariş kalemi şemasında modifier veya topping alanı
  olmadığı için uydurulmadı. Bunun yerine değiştirici doğrulaması şu kuralı uygular: aktif ve zorunlu seçimli
  (`min_selections > 0`) bir modifier grubu olan ürün bu kanala eşlenemez, sonradan böyle bir grup eklenirse de
  çözümlenmez. Hiçbir sandbox doğrulaması yoktur (V0-YSP-001 `Blocked`, `V12-GOV-004` waiver'ı).

## In scope

- Eşleme benzersizliği, etkin tarihler, değiştirici doğrulama ve eşlenmemiş reddetme.

## Out of scope

- Katalog dışa aktarma/güncelleme ve status senkronizasyonu.

## Dependencies

- V1-CAT-001
- V0-YSP-001

## Deliverables

- `src/Modules/OnlineOrdering/Yemeksepeti/ProductMapping/**` altında Goal kapsamını uygulayan production code ve
  task-specific automated test assets.
- Başarı, ret, replay/race ve güvenlik testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Bir dış ürün, bir aktif iç ürüne çözümlenir; eksik/belirsiz eşleme Order oluşturamaz.
- Kapanış kanıtı (2026-09-25, gerçek PostgreSQL 18 UTF8, port 56433): yeni test projesi 20/20 yeşil. Kapsanan
  durumlar: çözümleme; eşlenmemiş SKU; eşleme başlangıcından önceki an; yeniden eşlemede tarihçenin korunması; geriye
  dönük yeniden yazmanın reddi; replay; bir ürünün tek SKU ile yayını; pasif veya sonradan pasifleşen ürün; zorunlu
  modifier kuralı; çakışan tarihçede `Ambiguous`. Beş eşzamanlı eşlemeden yalnız biri kazanır; veritabanı da iki açık
  eşlemeyi 23505 ile reddeder. Migration 144 geri alınıp yeniden uygulanır. Bozuk SKU'lar ve SQL içeren SKU da test
  edildi. Çözümlenmeyen her sonuç `ProductId` vermez ya da `IsResolved=false` döner, böylece Order satırı oluşamaz.
- Mutasyon kontrolü (geri alındı, dosya birebir eşleşti): tablo kilidi kaldırılınca 1, zorunlu modifier kontrolü
  kapatılınca 2, belirsizlik kontrolü kapatılınca 1, `effective_from` koşulu kaldırılınca 2 test kırmızıya döndü.
- Bilinen sınır: servisin ilk çalışma zamanı çağıranı henüz yok (V12-ONL-002/004), bu yüzden reachability izin
  listesinde görev referansıyla duruyor.
- Kanıt: `evidence/V12-MAP-001/`.

## Handoff

- V12-ONL-002
