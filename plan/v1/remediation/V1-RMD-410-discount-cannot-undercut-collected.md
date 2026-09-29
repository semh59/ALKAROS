# V1-RMD-410 - Kısmi ödemeden sonra indirimin ödenecek tutarı tahsil edilenin altına düşürmesini engellemek

- Task ID: V1-RMD-410
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

V1-RMD-393 para akışı denetimi F-05 (Yüksek): kısmi ödemeden sonra uygulanan indirim, ödenecek tutarı zaten tahsil
edilenin altına düşürebiliyor (80 alındı, 30 indirim → ödenecek 70, kalan −10). Fazla tahsilat hiçbir yerde
kaydedilmiyor ve normal arayüzden erişilebiliyor. İndirim yalnız hesap durumuna bakıyor; ödemeler hesap durumunu
kapanışa kadar değiştirmediği için kısmen ödenmiş hesap hâlâ indirime açık görünüyor.

Bu görev: indirim, ödeme dağıtımının hesap başına kilidini (`payment-allocation:{billId}`) aynı işlemde alır, hesabın
şu ana kadar dağıtılmış (tahsil edilmiş) toplamını okur ve indirimli ödenecek tutar bu toplamın altına düşecekse
indirimi reddeder. Aynı kilit, eşzamanlı bir tahsilatla yarışı da kapatır (tahsilat da bu kilit altında indirimleri
okur).

## Owned surface

- `plan/v1/remediation/V1-RMD-410-discount-cannot-undercut-collected.md`
- `evidence/V1-RMD-410/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Billing/BillingSplitStore.cs ve
  src/Host/Experience/Billing/BillingSplitApplication.cs (V1-RMD-103 / V1-IAM-024 sahipliğinde) — yalnız indirimdeki
  tahsilat tabanı kontrolü, yeni istisna ve Türkçe 409 eşlemesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Billing/BillingSplitHttpTests.cs (V1-WTR-023
  sahipliğinde) — yeni test ve bir dağıtım tohumlama yardımcısı

## In scope

- İndirimli ödenecek tutar < dağıtılmış toplam → 409 `DISCOUNT_BELOW_COLLECTED`, indirim yazılmaz.
- Dağıtılmış toplamın altına inmeyen indirim (ör. kalan tutarın içinde) eskisi gibi uygulanır.

## Out of scope

- İade akışı (V13-ALC-004).
- Bölme tasarımının indirimsiz tutarı göstermesi (V1-RMD-393 F-11) — ayrı görev.

## Dependencies

- V1-RMD-409

## Acceptance evidence

- `ALKAROS.Host.Experience.Billing.Tests` (gerçek PostgreSQL 18, Release, 0 uyarı / 0 hata): 20/20
  (`evidence/V1-RMD-410/tests.log`).
- Yeni test `ADiscountThatWouldUndercutWhatWasAlreadyCollectedIsRefused`: 275'lik hesaptan 250 gerçek dağıtımla
  alınmışken 50 indirim 409 `DISCOUNT_BELOW_COLLECTED` (yazılmaz), 20 indirim uygulanır (ödenecek 255, bir düzeltme
  satırı). Üretim değişikliği geri alınınca kırmızı (409 beklenirken 200; aynı dosya).
- V1-RMD-393 denetim probe'ları P02 (F-05) ve P12 (indirim ile tam ödemenin yarışı) düzeltilmiş kopyada geçer
  (`evidence/V1-RMD-410/money-flow-probes-after-fix.log`).
- Semih'in elle deneyebileceği senaryo: 100 TL'lik hesaptan 80 TL alındıktan sonra 30 TL indirim denenince "Bu indirim,
  hesaptan şimdiye kadar alınan tutarın altına iniyor; uygulanamaz." görülür; 20 TL'ye kadar indirim uygulanabilir.

## Handoff

- None
