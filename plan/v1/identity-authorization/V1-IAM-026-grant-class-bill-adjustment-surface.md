# V1-IAM-026 - Grant-class bill adjustment surface (void/comp/discount)

- Task ID: V1-IAM-026
- Status: Blocked
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Goal

`bills.void`, `bills.comp` ve `bills.discount` —
`docs/domain/authorization-model.md` §3'ün TEK grant-class permission
ailesi, dalganın (`V1-IAM-016..025`) davranışsal sıkılaştırma, devir çözücü
ve manager karar yüzeyi testlerinde kullanılan kanonik örnek — hiçbir canlı
HTTP endpoint'ine bağlı değil. `Billing.Adjustments` domain modülü
(`AdjustmentCalculator`, `BillAdjustment.CreateDiscountPercentage`/
`CreateDiscountAmount`, `IBillAdjustmentRepository`) yalnız indirim/hizmet
bedeli/kuver/bahşiş modelliyor; void (gönderilmiş bir kalemi iptal etmek) ve
comp (teslim edilmiş bir kalemi sıfır fiyatlamak) domain modeli hiç yok —
bunlar sipariş kalemi durum makinesiyle etkileşmeyi gerektirir (Orders/Kitchen
yüzeyi), Billing.Adjustments'ın kapsamında değil. Bu görev: (a) void/comp için
gereken minimum domain modelini tasarlar (muhtemelen Orders/Kitchen'daki
mevcut kalem durum geçişleriyle entegre, `BillAdjustment` ailesine bir
üçüncü kategori eklemek yerine); (b) `bills.discount` için zaten var olan
`AdjustmentCalculator` mantığını kullanan bir Experience endpoint'i açar; (c)
her ikisini `IAuthorizationGrantService.RequestAsync`'e bağlar (doğrudan izni
olmayan çağıran 403 yerine `pending` alır); (d) `TableContractMapper`
dışında (Tables hiçbir komutta grant-erişilebilir değil, model §3) bills/cash
tarafında "tutulan izin ∪ ulaşılabilir istek" gösterimini istemciye ekler.
Bu, V1-IAM-025'in denetiminde bulundu (o görev yalnız "kablolama" kapsamlıydı;
bu, gerçek bir para-dokunan iş özelliğidir) ve bağımsız denetimin **B1**
bulgusuyla aynı köke sahiptir (`docs/engineering/v1-independent-audit.md`).

## Owned surface

- `plan/v1/identity-authorization/V1-IAM-026-grant-class-bill-adjustment-surface.md`
- `evidence/V1-IAM-026/**`
- Bu görev, başka bir task'in owned surface alanını değiştiremez.

## In scope

- Semih'in onayı: void/comp'un domain modeli nerede yaşar (Billing.Adjustments'a
  üçüncü bir `AdjustmentType` mi, yoksa Orders/Kitchen kalem durumuna bağlı ayrı
  bir model mi) — bu bir iş kararı, implementasyondan önce netleşmeli.
- `bills.discount`, `bills.void`, `bills.comp` için bir Experience endpoint'i;
  çağıran doğrudan izne sahip değilse `IAuthorizationGrantService.RequestAsync`
  ile `pending` yanıt (grant id + idempotency key); onay sonrası aynı
  idempotency key ile yeniden gönderim tamamlanır.
- Devir çözücü (`DelegationEscalationResolver`) ve davranışsal kapının
  (`BehaviouralTighteningGate`) bu endpoint üzerinden gerçek bir HTTP yolunda
  tetiklendiğini kanıtlayan entegrasyon testleri (remediation planının C5'i).
- İstemci tarafında (Bills/cash aksiyonları, Tables sözleşmesi değil) "tutulan
  izin ∪ ulaşılabilir yetki isteği" gösterimi — "Void (onay gerekiyor)" gibi.

## Out of scope

- Tables `AllowedCommands` — model §3'e göre hiçbir masa komutu grant-erişilebilir
  değil; bu görev Tables sözleşmesini değiştirmez.
- Yeni izin kodu tanımlanmaz; `bills.void`/`bills.comp`/`bills.discount` ile
  migration 043'te zaten sağlanmıştır.

## Dependencies

- V1-IAM-025

## Blocker

- Void/comp domain modelinin nerede yaşayacağı (Billing.Adjustments'ın
  genişletilmesi mi, Orders/Kitchen kalem durumuyla entegre ayrı bir model mi)
  bir iş kararı; para-dokunan bir akışı etkilediği için Semih onayı gerekir.
  Ancak bu karar alınıp `## Onay` bloğuyla kaydedildiğinde ve dokunulacak
  dosyalar (muhtemelen `src/Modules/Billing/Adjustments/**`,
  `src/Host/Experience/Billing/**` veya yeni bir Orders/Kitchen yüzeyi) için
  custody devri ilgili görevlere eklenip `plan/AUDIT_MANIFEST.json` yeniden
  üretildiğinde ve `validate` ile `verify-manifest` temiz kaldığında görev
  `Planned` yapılabilir.

## Acceptance evidence

- (Semih'in tasarım kararı alındıktan ve implementasyon tamamlandıktan sonra
  doldurulur.)

## Handoff

- V1-GOV-072
