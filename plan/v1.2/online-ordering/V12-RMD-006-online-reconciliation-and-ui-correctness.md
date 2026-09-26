# V12-RMD-006 - Online mutabakat, rapor ve operasyon ekranı bulgularını kapat

- Task ID: V12-RMD-006
- Status: Done
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

2026-09-26 bağımsız Faz 3 denetiminin mutabakat, rapor ve ekran bulgularını kapatmak (V12-REC-001, V12-RPT-001,
V12-OUI-001; Semih: "en küçük hata bile kritik"):

- Genel vaka uç noktası (`/transition`) bir `OnlineOrderMismatch` vakasını `Resolved` yapabiliyor. Bu yol,
  online çözümün istediği notu ve "fark sürüyor mu" kontrolünü atlıyor.
- `ProviderEventFailed`, `LocallyAcceptedProviderUnknown` ve `AvailabilityNotDelivered` kaynak çiftleri
  `Dismissed` vakayı dışlamıyor. Kapatılan vaka her taramada yeniden açılıyor.
- Ürün stoğu yok ya da ürün bulunamadı diye reddedilen bir sipariş, kalem referansı okunamadığında sağlayıcıya
  iptal gönderilmeden `providerCancellationRequested = true` olarak kaydediliyor. Vaka "iptali yeniden gönder"
  eylemiyle hiçbir zaman kapanmıyor.
- Taramanın satır sınırı aşıldığında hata "Kaynak okunamadı." diye raporlanıyor; asıl neden gizleniyor.
- Kanal raporunun ret sorgusu tarih sınırı olmadan bütün gelen kutusunu tarıyor. Durum kovalarının siparişleri
  tam bölüştüğü de kontrol edilmiyor; yeni bir durum sessizce hiçbir kovaya düşmüyor.
- Online operasyon ekranında geç dönen eski bir yenileme, daha yeni sonucun üzerine yazabiliyor.
- Sağlayıcının bilinmeyen durumlu olayları (`UnknownStatus`) sorunlar listesinde görünmüyor.
- `MigrationManifest.cs` açıklaması "031-148 as of V12-ONL-005" diyor; güncel değil.

## Owned surface

- `plan/v1.2/online-ordering/V12-RMD-006-online-reconciliation-and-ui-correctness.md`
- `evidence/V12-RMD-006/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-26 kararı):
  - src/Host/Experience/Reconciliation/ReconciliationCaseEndpoints.cs (V1-RMD-250) — online vakanın genel yoldan
    çözülmesinin reddi.
  - src/Modules/Reconciliation/OnlineOrders/ (V12-REC-001) — `Dismissed` dışlaması, satır sınırı nedeni.
  - src/Host/Experience/OnlineOrdering/ (V12-ONL-002, V12-OUI-001) — iptal isteği bayrağının doğruluğu,
    `UnknownStatus` sorun listesinde.
  - src/Modules/Reporting/Channels/ (V12-RPT-001) — ret sorgusunun tarih sınırı, kova bölüşüm kontrolü.
  - src/Clients/PosTerminal/src/features/online-operations/ (V12-OUI-001) — eski yanıt koruması,
    `UnknownStatus` etiketi.
  - src/Host/Composition/Migrations/MigrationManifest.cs — açıklama.
  - tests/Modules/Reconciliation/OnlineOrders/ tests/Modules/Reporting/Channels/ tests/Host/Experience/Reconciliation/
    ve tests/Host/Experience/OnlineOrdering/ — testler ve fikstürler.

## In scope

1. Genel `/transition`, `OnlineOrderMismatch` vakası için `Resolved` isteğini 409 `USE_ONLINE_RESOLUTION` ile
   reddeder; mesaj Türkçedir. `Dismissed` ve diğer durumlar eskisi gibi çalışır.
2. Üç otomatik kaynak çifti, aynı anahtarla `Dismissed` bir vaka varken yeni vaka açmaz.
3. Reddedilen siparişte `providerCancellationRequested`, iptal gerçekten kuyruğa alındıysa `true` olur. Kalem
   referansı okunamadıysa vaka "olayı yeniden işle" eylemini alır.
4. Tarama satır sınırını aşan kaynak, "Kaynakta çok fazla fark var" nedeniyle raporlanır.
5. Ret sorgusu `received_at < bitiş` ile sınırlanır; sonuç değişmez. Rapor kontrolü, her gün satırında
   bekleyen + kabul + ret + iptal = alınan olduğunu doğrular (`BucketsPartitionOrders`). Bu koşul
   `IsBalanced`'a dahildir.
6. Ekran her yenilemeye sıra numarası verir. Daha yeni bir istek başladıysa eski yanıt ekrana yazılmaz.
7. `UnknownStatus` olaylar sorunlar listesinde "Bilinmeyen sağlayıcı durumu" etiketiyle görünür.
8. `MigrationManifest.cs` açıklaması düzeltilir.

## Out of scope

- Sağlayıcının gerçek davranışı (V0-YSP-001 `Blocked`).

## Dependencies

- V12-RMD-008

## Deliverables

- Kod ve testler.

## Acceptance evidence

- İlgili test projeleri ve PosTerminal testleri yeşil; mutasyon kontrolü `evidence/V12-RMD-006/` altında.
- `task_scope_tool.py --task-id V12-RMD-006 --diff-base <InProgress commit>` exit 0.

## Handoff

- None
