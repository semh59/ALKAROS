# V12-ONL-005 - Publish channel availability

- Task ID: V12-ONL-005
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

Tek onaylı kullanılabilirlik projeksiyonundan satılabilir veya kullanılamıyor durumunu etkin çevrimiçi kanallara
yayınlayın.

## Owned surface

- `src/Modules/OnlineOrdering/AvailabilityPublishing/**`, `tests/Modules/OnlineOrdering/AvailabilityPublishing/**`,
  `database/migrations/V12/V12-ONL-005/**`
- `src/Host/Experience/OnlineOrdering/OnlineAvailabilityPublishingHostedService.cs` — yayını 30 saniyede bir çalıştıran
  arka plan servisi; bu görevle oluşturulan yeni dosya.
- `evidence/V12-ONL-005/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-25 "Sınırlı ek + yol
  notu" kararı):
  - src/Modules/OnlineOrdering/Yemeksepeti/ProductMapping/ (V12-MAP-001 sahipliğinde) — yalnız `ListOpenMappingsAsync`.
  - src/Modules/OnlineOrdering/OnlineOrderingModule.cs (V12-MAP-001 sahipliğinde) — kanal ve servis kaydı.
  - src/Host/DualScreen/DualScreenApplication.cs — arka plan servisinin kaydı.
  - database/MigrationComposition/order.json, src/Host/Composition/Migrations/MigrationManifest.cs ve
    tests/Host/MigrationComposition/Manifest/ManifestTests.cs — 148 numaralı migration konumu.
  - ALKAROS.slnx ve `dotnet restore`'un ürettiği packages.lock.json.
- Tasarım notu: tek onaylı kaynak V11-INV-007'nin rezerve/kullanılabilir bakiye projeksiyonudur
  (`inventory.stock_balances.available_quantity`, şemalar arası salt okuma). Bu sayede V12-STK-001 hold'u bütün
  kanallara hemen yansır. Ürünün satılabilir adedi, eşlenmiş stok kalemlerinin en azıdır (her biri çarpanına
  bölünür). Her gözlem, ürünün bakiye satırlarının `row_version` toplamını, yani yalnız artan bir kaynak sürümünü
  taşır. Durum yalnız kesin olarak daha yeni bir gözlemle değişir. Teslim, gönderdiği satırları kilitler: yeni bir
  gözlem teslimin bitmesini bekler ve sonraki turda gider. Hız sınırı olarak her tur kanal başına en fazla bir toplu
  sağlayıcı çağrısı yapılır. Kimlik bilgisi olmayan kanal atlanır. Yemeksepeti tarafında `quantity` gönderilir;
  bu doğrulanmamış taslaktır (V0-YSP-001 `Blocked`). Sandbox kanıtı V20-INT-003'te açık kalır (`V12-GOV-004`).

## In scope

- Kullanılabilirlik event tüketimi, provider azaltma, eş zamanlı güncellemeler, retry, eski/güncel olmayan olayların
  işlenmesi ve sapma tespiti.

## Out of scope

- Stok kesintisi, reçete hesaplama, katalog içeriği ve gelen order kabulü.

## Dependencies

- V12-STK-001
- V12-ONL-004
- V11-INV-007
- V11-MNU-002

## Deliverables

- Onaylanan her kanal için Provider'ye özel kullanılabilirlik yayıncısı.
- Contract, retry, hız sınırı ve eski event testleri.
- Etkin provider'lar için gerçek sandbox kanıtı.

## Acceptance evidence

- Son bölüm geçişi, etkinleştirilen her sandbox kanalına mantıksal olarak bir kez ulaşır; gecikmiş eski olaylar daha
  yeni bir durumun üzerine yazamaz.
- Kapanış kanıtı (2026-09-26, gerçek PostgreSQL 18 UTF8, port 56433): modül testleri 10/10 yeşil. Sonuçlar:
  - Son porsiyon başka bir kanalca tutulunca (gerçek V12-STK-001 hold'u) bu kanala tam olarak bir kez "0" gider;
    değişmeyen turlarda bir şey gitmez.
  - Satılabilir adet en kıt malzemeye göre hesaplanır.
  - Daha eski sürümlü gecikmiş gözlem yeni durumu ezemez.
  - Teslim sırasında gelen değişiklik sonraki turda gönderilir ve eski teslim onu geçersiz kılmaz.
  - Her tur kanal başına tek sınırlı toplu çağrı yapar.
  - Altı paralel teslimatçı bir ürünü iki kez göndermez.
  - Başarısız teslim kaydedilip yeniden denenir ve sapma olarak sorgulanabilir.
  - Devre dışı kanal için hiçbir şey hesaplanmaz.
  - Yemeksepeti kanalı yalnız eşlenmiş ürünleri tanır, yalnız `quantity` gönderir ve kimlik bilgisi olmadan
    kapalıdır.
  - Migration 148 geri alınıp yeniden uygulanır.
  - "Sandbox kanalı" kısmı karşılanmış sayılmaz; gerçek sağlayıcı kanıtı V20-INT-003'te açık kalır.
- Mutasyon kontrolü (dosya yedekten geri yüklenip `cmp` ile doğrulandı): bayat gözlem koruması kaldırılınca,
  satır kilidi kaldırılınca, toplu çağrı sınırı yok sayılınca, devre dışı kanal kontrolü kaldırılınca ve en kıt
  malzeme yerine en bol malzeme alınınca birer test kırmızıya döndü. Teslim güncellemesindeki sürüm koşulu, kilit
  nedeniyle hiç yanlış olamayacağı için ölü kod olarak kaldırıldı.
- Kanıt: `evidence/V12-ONL-005/`.

## Handoff

- V12-REC-001
- V12-RPT-001
