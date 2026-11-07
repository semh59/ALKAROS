# V1-RMD-883 - Cari yaşlandırma: vade bilgisi ve veritabanı tarafında toplulaştırma (500 bin satır sınırı kalkar)

- Task ID: V1-RMD-883
- Status: Done
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

Yaşlandırma raporu iki kusur taşıyordu. (1) Yaşı işlem tarihine göre hesaplıyordu ve kova sütunları "0-30 gün" diye adlandırılıyordu; ledger satırlarında vade tarihi alanı yok, ama müşteri başına vade günü var (`customer_account.credit_terms.payment_term_days`, kredi politikası aynı kuralı kullanır). Rapor bu bilgiyle düzeltilir: kovalar açıkça "işlem tarihinden itibaren" etiketlenir; ayrıca her müşteri için "Vade (gün)" ve "Vadesi geçmiş" sütunları eklenir (açık borcun işlem tarihi + vade gününü geçen kısmı; vadesi tanımlı olmayan müşteride "—"). (2) Defter 500 bin satırı aşınca rapor hata veriyordu. FIFO kapatma, kümülatif toplam pencere fonksiyonlarıyla veritabanında hesaplanır (borç = max(0, kümülatif bakiye); açık kalem payı = kalemin kuyruğa eklediği tutarın, en eskiden başlayarak kapanmayan kısmı); sınır ve bellek içi hesaplayıcı kalkar, sonuç aynı kalır. Mevcut bellek içi hesaplayıcı testleri gerçek veritabanı testlerine taşınır.

## Owned surface

- `evidence/V1-RMD-883/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/CustomerAccounts/Aging/ReceivablesAging.cs — hesaplayıcı kalkar, vade alanları
  - src/Modules/CustomerAccounts/Aging/PostgresReceivablesAgingService.cs — SQL ile toplulaştırma
  - src/Host/Experience/CustomerAging/ReceivablesAgingEndpoints.cs — vade alanları
  - tests/Modules/CustomerAccounts/BalanceProjection/ReceivablesAgingCalculatorTests.cs — silinir, kapsamı servis testlerine taşınır
  - tests/Modules/CustomerAccounts/BalanceProjection/PostgresReceivablesAgingServiceTests.cs
  - tests/Modules/CustomerAccounts/BalanceProjection/PostgresAccountBalanceProjectionTests.cs — bağlantı yoğun testle aynı koleksiyon
  - tests/Modules/CustomerAccounts/BalanceProjection/ALKAROS.CustomerAccounts.BalanceProjection.Tests.csproj — kredi vadesi tablosu test şeması
  - tests/Host/Experience/CustomerAccounts/CustomerAgingHttpTests.cs
  - src/Clients/PosTerminal/src/features/management-customer-aging/models.ts
  - src/Clients/PosTerminal/src/features/management-customer-aging/CustomerAgingSection.tsx
  - src/Clients/PosTerminal/src/features/management-customer-aging/CustomerAgingSection.test.tsx
  - src/Clients/PosTerminal/src/strings.ts
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. SQL ile FIFO yaşlandırma (kova, fazla ödeme, vadesi geçmiş) ve 500 bin sınırının kalkması; ölü hesaplayıcının silinmesi
2. Vade günü ve vadesi geçmiş tutar API alanları, ekran sütunları, kova başlıkları ve açıklama metni
3. Testler (kova sınırları 29/30/31, 60/90 gün sınırları dahil), mutasyon, iki bağımsız qa incelemesi

## Out of scope

- Ledger'a işlem başına vade tarihi eklemek, geçmiş tarihli rapor, profil okuma (N+1) iyileştirmesi, tahsilat/ihtar akışı

## Dependencies

- None

## Acceptance evidence

1. Kırmızı/yeşil test çıktıları
2. Mutasyon sonuçları
3. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
