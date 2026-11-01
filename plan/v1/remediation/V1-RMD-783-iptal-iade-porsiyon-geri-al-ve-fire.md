# V1-RMD-783 - İptal ve iadede günlük menü porsiyonu geri alınsın, hazırlanmış kalem fire sayılsın

- Task ID: V1-RMD-783
- Status: Done
- Assignee: claude-code-session_01FMN5WVXi1HJCTEk5argkgZ
- Work type: implementation
- Surface state: Planned

## Goal

V1-RMD-777 ve V1-RMD-780 günlük menü sayaçlarını doldurdu: sipariş adedi porsiyon sayılıyor ve kalem tüketilince `consumed` artıyor. Tüketimden sonra stoku geri veren iki yol var ve ikisi de sayaca dokunmuyor: `SentItemVoidStore` (gönderilmiş kalem iptali) ve `PaymentRefunds` üzerinden `ReturnConsumedShareAsync` (stoka geri alınan iade). Stok geri gelir, `consumed` düşmez; menüde "kalan" olduğundan az görünür. Mutfakta hazırlanmaya başlanmış kalemin iptalinde stok geri verilmez ama porsiyon fire olarak görünmez.

Semih kararı: (1) iptal veya iadede stok geri verilince sayaç da düşsün, "sipariş adedi = porsiyon" kuralı aynen kalsın; (2) mutfakta hazırlanmış (hazırlanmaya başlanmış veya hazır) kalemin iptali ayrıca fire sayılsın; hazırlanmamış kalemin iptali yalnız geri alma olsun.

Çözüm:

1. Anahtarlı rezervasyon satırı (777/780: `daily_menu_item_id` + `portion_count`) iki toplam alan kazanır: `portions_returned` ve `portions_wasted`. Alanlar yalnız artar (kümülatif); satır durumu `Consumed` kalır. Böylece aynı iptal ikinci kez çağrılsa değişmez ve kısmi iade payla orantılı çalışır.
2. Yeni Inventory girişi tüketim sonrası geri alma ve fire için aynı işlemde satırı günceller ve `inventory.daily-menu-portion-adjusted.v1` outbox olayını yazar. İade yolu (`ReturnConsumedShareAsync`) stok payıyla aynı oranda porsiyonu geri alır; `SentItemVoidStore` Sent/Held kalemde porsiyonu tam geri alır, Preparing/Ready kalemde kalan porsiyonu fire yapar (mevcut kodun "hazırlanmış" tanımı; ikinci tanım yok).
3. Fire bir taşıma sayılır: `consumed` azalır, `waste` artar, `available` değişmez (porsiyon satılmadı ama kullanıldı). Yalnız `waste` eklense `available` iki kez düşerdi.
4. Menu tüketicisi olayı önce Reserved ve Consumed (idempotent) uygular, sonra yeni projector yöntemiyle farkı uygular; anahtar `(kalem, 'Returned'|'Wasted', rezervasyon)` ve tutar kümülatiftir, sıra dışı veya tekrar gelen teslim sayacı değiştirmez. Sayaç 0'ın altına inmez.
5. Rebuild, drift ve rapor aynı alanlardan hesaplar: `consumed = adet - geri alınan - fire`, `waste = Waste durumlu satırlar + fire alanı`. Rebuild uygulanmış olay kayıtlarını da yeniden kurar, böylece rebuild sonrası yeniden teslim çift düşürmez.
6. Yeni migration `menu.daily_menu_counter_applied_events.event_type` CHECK kısıtına `Returned` ve `Wasted` ekler (geri alınabilir).

## Owned surface

- `evidence/V1-RMD-783/**`
- `database/migrations/V1/V1-RMD-783/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/BuildingBlocks/IntegrationContracts/DailyMenuCounterIntegrationEvents.cs (V1-RMD-777) — düzeltme olayı sözleşmesi.
  - src/Modules/Inventory/CrossChannelReservation/ (V12-STK-001) — satırda geri alma ve fire alanları, olay yazımı, koruyucu arayüzü.
  - src/Modules/Inventory/MovementReversal/StockMovementReversalService.cs (V11-INV-003) — stok payı geri verilince porsiyon payı.
  - src/Host/Experience/Orders/SentItemVoid/SentItemVoidStore.cs (V1-IAM-027) — iptalde geri alma veya fire.
  - src/Modules/Menu/CounterProjection/ (V11-MNU-002) — tüketici, projector, rebuild ve drift.
  - src/Modules/Reporting/MenuInventory/PostgresMenuInventoryReportingService.cs (V11-RPT-001) — rapor toplamı.
  - database/MigrationComposition/order.json (V1-FND-004) — yeni migration kaydı.
  - src/Host/Composition/Migrations/MigrationManifest.cs (V1-FND-004) — yalnız PhaseBMax.
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs (V1-GOV-007) — yalnız yeni migration kimliği.
  - tests/Host/MigrationComposition/Migrations/MigrationFileAndSchemaTests.cs (V1-RMD-698) — geri alma ve yeniden uygulama testi.
  - docs/architecture/module-dependency-rules.md (V0-ARC-001) — yeni olay kenarı.
  - tests/Modules/Menu/CounterProjection/ (V11-MNU-002) — uçtan uca sayaç testleri ve migration fixture.
  - tests/Modules/Inventory/CrossChannelReservation/ (V12-STK-001) — geri alma ve fire satır testleri.
  - tests/Modules/Inventory/MovementReversal/ (V11-INV-003) — fixture'a rezervasyon tablosu.
  - tests/Modules/Reporting/MenuInventory/ (V11-RPT-001) — rapor toplamı testi.
  - tests/Host/Experience/Orders/VoidSent/ (V1-IAM-027) — iptal testleri.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. `portions_returned` ve `portions_wasted` kümülatif alanları, tek yönlü ve idempotent; yalnız anahtarlı `Consumed` satırı güncellenir (çok stok eşlemeli üründe kardeş satırlara dokunulmaz).
2. Yeni outbox olayı ve Menu tarafında uygulanışı; sıra dışı, tekrar ve rebuild sonrası teslimde sayaç aynı kalır.
3. `SentItemVoidStore`: Sent/Held iptalinde porsiyon geri alma; Preparing/Ready iptalinde fire.
4. `ReturnConsumedShareAsync`: stok payı kadar porsiyon geri alma (kısmi iade dahil).
5. Rebuild, drift ve rapor aynı toplamı kullanır; `waste` raporu mevcut anlamını korur.
6. Migration 210 ve kayıt dosyaları.

## Out of scope

- `RestoreStock=false` iade (stok dönmez, sayaç değişmez; fire değildir), fatura iptali (stok dönmez), çevrimiçi sağlayıcı iptali (stoka dokunmaz).
- Rezervasyon durumu (`Reserved`) iptali: mevcut `Released`/`Waste` yolu zaten çalışıyor.
- Gece yarısı iş günü sınırı, rebuild kilidi, üretim partisi birimi, geçmiş veri doldurma.

## Dependencies

- V1-RMD-777
- V1-RMD-780

## Acceptance evidence

1. Kırmızı önce: Sent iptali `consumed`'ı düşürmüyor; Preparing iptali `waste`'i artırmıyor; kısmi iade `consumed`'ı düşürmüyor; QR kaleminin kabul sonrası iptali aynı. Düzeltmeden sonra yeşil.
2. Tekrar, sıra dışı teslim, rebuild sonrası yeniden teslim, çift iptal ve sıfırın altına düşme: sayaç, rebuild, drift ve rapor eşit.
3. Menu, Inventory, Orders, Reporting, Host (Orders, Billing, Kitchen), MigrationComposition, ApiConventions ve ModuleBoundaries testleri gerçek exit code ile `evidence/V1-RMD-783/` altında.
4. Mutasyon: çağrıyı kaldırmak, kümülatif üst sınırı kaldırmak, tüketici dalını kaldırmak, fire taşımasında `consumed` azaltmayı kaldırmak testleri kırar; kaynak `cmp` ile geri alınır.
5. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
6. qa-backend ve qa-test-kalitesi incelemeleri sırayla yapılır, bulgular giderilir.

## Handoff

- None
