# V1-RMD-754 - Düzeltilmiş vergisi değişen hesapta kalem bölmeyi açık 409 ile reddet

- Task ID: V1-RMD-754
- Status: Planned
- Assignee: TBD
- Work type: remediation
- Surface state: Existing

## Goal

F0908-PD1 kusurunu gider: bahşiş ve indirim ödenecek tutarı sıfırlayıp vergi toplamını değiştirdiğinde `SaveItemsAsync` kalem bölmeyi geçirip repository vergi kontrolünde genel 400 `VALIDATION_FAILED` döndürüyor. Koşul vergi toplamını da karşılaştırır; kasiyer 409 `ITEM_SPLIT_ON_ADJUSTED_BILL` mesajını görür. Kalem bazlı vergi dağıtımı politikası eklenmez (Semih kararı A, 2026-10-29).

## Owned surface

- `plan/v1/remediation/V1-RMD-754-kalem-bolme-vergi-uyumsuzlugu.md`
- `evidence/V1-RMD-754/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Billing/BillingSplitStore.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Billing/BillingSplitHttpTests.cs

## In scope

- `SaveItemsAsync` ret koşuluna `adjustment.AdjustedTaxTotal != bill.TaxTotal` eklenir; mevcut tutar koşulu korunur.
- Bahşiş ve eşit tutarlı indirim senaryosu için Host HTTP testi: 409 `ITEM_SPLIT_ON_ADJUSTED_BILL`, mevcut tahsisler değişmez.
- Düzeltme öncesi testin kırmızı (400), düzeltme sonrası yeşil olduğu gerçek çıktıyla kaydedilir; koşul geri alındığında testin tekrar kırıldığı mutasyon kanıtı tutulur.
- Host Billing tam süiti gerçek PostgreSQL ile çalıştırılır.

## Out of scope

- SplitEngine, repository, AdjustmentCalculator, exception filter ve endpoint değişikliği.
- Kalem bazlı indirim veya vergi dağıtımı politikası.
- V1-RMD-753 dışında başka test dosyası veya plan dosyası değişikliği.

## Dependencies

- V1-RMD-752

## Acceptance evidence

1. Yeni Host testi düzeltme öncesi 400 `VALIDATION_FAILED` ile kırmızı, düzeltme sonrası 409 `ITEM_SPLIT_ON_ADJUSTED_BILL` ile yeşildir; iki çıktı evidence altındadır.
2. Vergi koşulu kaldırıldığında test tekrar kırılır (mutasyon kanıtı, kaynak geri yüklenir).
3. Host Billing tam süiti gerçek PostgreSQL ile 0 başarısız; TRX ve log hash'i kayıtlıdır.
4. Build 0 hata 0 uyarı.
5. Plan, consistency ve task scope kontrolleri gerçek exit code ile kaydedilir.
6. İki bağımsız inceleme kod/test uyumunu ve kapsam sınırını doğrular.

## Handoff

- V1-RMD-753
