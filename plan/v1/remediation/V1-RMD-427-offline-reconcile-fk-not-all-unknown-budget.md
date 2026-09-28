# V1-RMD-427 - Çevrimdışı uzlaştırmada her yabancı anahtar hatasının "bütçe bulunamadı" sayılmaması

- Task ID: V1-RMD-427
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

V1-RMD-399 yetki denetimi sırasında görülen yanlış eşleme: `OfflineGrantReconciler.StoreAsync`, onay ve tekrar kaydının
yazımında oluşan her yabancı anahtar ihlalini (23503) "bütçe arada silindi" sayıp
`UnknownOfflineAuthorityBudgetException` atıyordu. Oysa yazım, bütçe dışında da yabancı anahtarlara dokunuyor
(istekçi, hizmet eden kullanıcı; V1-RMD-189). Var olmayan bir kullanıcıya bağlı bir işlem cihaza "çevrimdışı yetki
bütçesi bulunamadı; yeniden bağlanın" (404) diye dönüyor, gerçek hata gizleniyordu. V1-RMD-404'ten beri istekçi ve
hizmet eden kişi sunucudan okunduğu için bu durum bugün HTTP'den zor erişilir; ama eşleme yanlıştır.

Bu görev: yalnız bütçe kısıtının (`fk_offline_authority_replays_budget`) ihlali bütçe hatası sayılır; başka bir
yabancı anahtar ihlali olduğu gibi yukarı çıkar (Host onu mevcut Türkçe 503 `DATABASE_UNAVAILABLE` olarak eşler).

## Owned surface

- `plan/v1/remediation/V1-RMD-427-offline-reconcile-fk-not-all-unknown-budget.md`
- `evidence/V1-RMD-427/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/Authorization/Offline/OfflineGrantReconciler.cs
  (V1-IAM-022 sahipliğinde) — yalnız yabancı anahtar yakalamasının kısıt adına daraltılması
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Identity/Authorization/Offline/OfflineGrantReconcilerTests.cs
  (V1-IAM-022 sahipliğinde) — yeni test

## In scope

- Bütçe kısıtı dışındaki yabancı anahtar ihlalinin bütçe hatası olarak etiketlenmemesi.

## Out of scope

- Uç noktanın hata eşlemesi (değişmez).

## Dependencies

- V1-RMD-426

## Acceptance evidence

- `ALKAROS.Identity.Authorization.Tests` 215/215 ve `ALKAROS.Host.Experience.OfflineReconciliation.Tests` 9/9 (gerçek
  PostgreSQL 18, Release, 0 uyarı / 0 hata; `evidence/V1-RMD-427/tests.log`). Bütçenin arada silinmesi testi
  (`ABudgetDeletedBetweenTheLookupAndTheReplayInsertIsRejectedNotCrashed`) değişmeden geçer.
- Yeni test `AForeignKeyFailureOtherThanTheBudgetsIsNotReportedAsAnUnknownBudget`: modül şemasında kullanıcı yabancı
  anahtarları olmadığı için yalnız test süresince eklenen bir kısıt, "başvurulan kayıt yok" durumunu temsil eder;
  uzlaştırma bu kısıtın hatasını olduğu gibi verir, bütçe hatası atmaz. Üretim değişikliği geri alınınca kırmızı
  (bütçe hatası atılıyordu; `evidence/V1-RMD-427/red-without-fix.log`).
- Semih'in elle deneyebileceği senaryo: yok; bugün normal kullanımda bu duruma düşülmüyor, yalnız hatanın gerçek
  nedeni artık gizlenmiyor.

## Handoff

- None
