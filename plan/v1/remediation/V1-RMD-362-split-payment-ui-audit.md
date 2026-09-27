# V1-RMD-362 - Hesap Ödeme (Split Payment) modül denetimi: hata/uyarı rolleri, ödeme yöntemi grup semantiği

- Task ID: V1-RMD-362
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-27

## Goal

Modül-modül arayüz denetim sürecinin (plan/v1/ui-audit/UI_AUDIT_PROGRESS.md) 3. modülü: Cashier'ın Hesap
Ödeme (split-payment) sayfası (`src/Clients/Cashier/wwwroot/payments/split-payment/**`). On iki boyut
üzerinden tarandı; dört bağımsız, gerçek bulgu tespit edildi ve düzeltildi. Bu dosya bu oturumdaki en olgun
dosyalardan biri — odak korumasını (yöntem sekmesi/EFT onay kutusu yeniden çizimlerinde) zaten kendisi
dikkatle yönetiyordu; kalan boşluklar dar, tekrarlanan bir sınıftandı.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/25-split-payment-accessibility.spec.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/ui-audit/UI_AUDIT_PROGRESS.md
- `plan/v1/remediation/V1-RMD-362-split-payment-ui-audit.md`

## In scope

1. **[T1, Orta] Hata kutusu (`sp-alert-danger`) `role="alert"` taşımıyordu.** V1-RMD-345
   (CustomerWeb) ve V1-RMD-361'in (cash-session.js) aynı gün kapattığı aynı sınıftan boşluk, bu
   dosyaya hiç uğramamıştı.
2. **[T1, Orta] "Manuel mutabakat gerekiyor" uyarı banner'ı (`sp-alert-warning`) da `role="alert"`
   taşımıyordu.** Kilitli bir hesabın en önemli durum bildirimi, ekran okuyucuya hiç
   duyurulmuyordu.
3. **[T1, Düşük] İndirim/bahşiş başarı ve hata bildirimleri (`sp-alert-body`, çıplak) hiçbir role
   taşımıyordu.** Bir indirimin uygulanıp uygulanmadığı yalnızca ekrana bakan kasiyer tarafından
   görülüyordu.
4. **[T1, Orta] Ödeme yöntemi seçici üç düğmesi (Nakit/Kart/EFT) — mutlaka birinin seçili olduğu
   bir değer grubu — hiçbir ARIA gruplama/durum taşımıyordu**, yalnızca görsel `is-active` sınıfı
   vardı. Modül 1/2'nin sekme (view) desenlerinden farklı olarak bu bir DEĞER seçimi olduğu için
   `role="tab"` yerine standart `role="radiogroup"`/`"radio"` + `aria-checked` deseni kullanıldı.

## Out of scope

- Bu modülde ek bir ürün-katmanı (P1-P4) gözlemi bulunmadı; akış zaten rakip ürünlerle
  kıyaslanabilir basitlikte ve mevcut odak-koruma disiplini örnek alınacak düzeydeydi.

## Dependencies

- None

## Acceptance evidence

- `tests/E2E/Cashier` tam paketi (57 test, 25 numaralı yeni dosya dahil): 57/57 geçti, regresyon
  yok (split-payment'a dokunan 7 mevcut spec dosyası — 07/08/09/10/12/18/21 — dahil, 25/25 ayrıca
  ayrı çalıştırıldı).
- Mutation-check: `split-payment.js` `git stash` ile geri alındı, yeni 25 numaralı spesifikasyon
  GERÇEKTEN kırmızı oldu (`role="radiogroup"` eksik). `git stash pop` ile geri yüklendi, paket
  tekrar 57/57 yeşile döndü.
- `node --check` ile dosya sözdizimi doğrulandı; PosTerminal `corepack pnpm build` sıfır hata.

## Handoff

- None
