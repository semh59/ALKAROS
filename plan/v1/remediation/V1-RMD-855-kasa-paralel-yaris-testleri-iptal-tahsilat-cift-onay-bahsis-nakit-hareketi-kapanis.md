# V1-RMD-855 - Kasa paralel yarış testleri: iptal+tahsilat, iade çift onayı, bahşiş+tahsilat, nakit hareketi+oturum kapanışı

- Task ID: V1-RMD-855
- Status: Planned
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

G9 (V1-RMD-737 notu, F0431 R4): kasanın para yoluna ait eşzamanlılık testleri yok. Sunucuda kilitler var (fatura iptali ve tahsilat/bahşiş aynı fatura kilidini alır, iade kararı satır durumuna bakar, kapanış oturumu kilitler) ama dört yarışta 'ikisi birden başarılı olmaz' değişmezi sınanmıyor: (1) aynı faturaya iptal ve tahsilat; (2) aynı iade talebine iki yöneticinin aynı anda onayı; (3) faturayı tam ödeyen tahsilat ve bahşiş; (4) nakit çıkışı ve oturum kapanışı. Aynı kasa hareketine çift onay (CashMovementApprovalHttpTests.ParallelDecisionsOnOneRequestApplyExactlyOnce) ve çift kapanış zaten sınanıyor. Test kırmızı çıkarsa üretim kusuru ayrı göreve devredilir.

## Owned surface

- `evidence/V1-RMD-855/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Host/Experience/PaymentTender/PaymentTenderHttpTests.cs — iptal+tahsilat ve bahşiş+tahsilat yarışı.
  - tests/Host/Experience/Billing/PaymentRefundHttpTests.cs — iade talebine paralel çift onay.
  - tests/Host/Experience/CashSession/CashMovementApprovalHttpTests.cs — nakit çıkışı ile oturum kapanışı yarışı.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Her yarış gerçek HTTP ve gerçek Postgres ile, sonuç dalına göre değişmezi doğrulayan, birden çok turlu test olarak yazılır.
2. Mutasyon: fatura iptalinde ödeme kontrolü kaldırılır, bahşiş fatura kilidi kaldırılır, iade kararında durum şartı kaldırılır, nakit hareketinde oturum kilidi kaldırılır (cp+cmp ile geri alınır); yarış mutantı kalıcı yeşil kalırsa test güçlendirilir veya sınırı kanıtla yazılır.

## Out of scope

- Üretim kodu (kusur çıkarsa ayrı görev); aynı kasa hareketine çift onay ve çift kapanış (mevcut testler).

## Dependencies

- None

## Acceptance evidence

1. İlgili test projeleri gerçek Postgres (alkaros-test-pg, 55432) ile exit 0; çıktılar `evidence/V1-RMD-855/`.
2. Mutasyon kanıtı cp+cmp ile geri alınmış.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
