# V1-RMD-413 - İndirimli hesapta bölme tasarımının indirimli tutarı kullanması

- Task ID: V1-RMD-413
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

V1-RMD-393 para akışı denetimi F-11 (Orta): indirim uygulanmış bir hesapta bölme tasarımı indirimsiz ödenecek tutarı
(ör. 100) gösteriyor; bölme motoru ise indirimli tutarı (90) uyguluyor. Arayüzün zorunlu kıldığı toplamla (100) kayıt
motor tarafından, indirimli toplamla (90) kayıt ise depo doğrulaması tarafından reddediliyor (400). Sonuç: indirimli
hesap hiçbir şekilde bölünemiyor. Ürün bazında bölme ise indirimi hiç hesaba katmıyor.

Bu görev: bölme tasarımı yanıtı indirimli ödenecek tutarı ve vergiyi döner; depo doğrulaması kaydı aynı işlem içinde
hesabın düzeltme satırlarından hesaplanan indirimli toplamlarla karşılaştırır; indirim/ek ücret uygulanmış hesabın ürün
bazında bölünmesi anlaşılır bir Türkçe 409 ile reddedilir (tutar, kişi veya serbest bölme kullanılabilir).

## Owned surface

- `plan/v1/remediation/V1-RMD-413-split-design-adjusted-payable.md`
- `evidence/V1-RMD-413/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Billing/BillingSplitStore.cs ve
  src/Host/Experience/Billing/BillingSplitApplication.cs (V1-RMD-103 / V1-IAM-024 sahipliğinde) — yalnız tasarım
  yanıtındaki indirimli toplamlar, ürün bazında bölme reddi, yeni istisna ve Türkçe 409 eşlemesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Billing/SplitDesign/PostgresSplitDesignRepository.cs
  (V1-RMD-027 sahipliğinde) — yalnız toplam doğrulamasının indirimli toplamlara geçmesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Billing/SplitDesign/ALKAROS.Billing.SplitDesign.Tests.csproj
  (V1-RMD-027 sahipliğinde) — yalnız test veritabanına 021-bill-adjustments.up.sql bağlantısı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Billing/BillingSplitHttpTests.cs (V1-WTR-023
  sahipliğinde) — yeni testler

## In scope

- `GET`/kayıt yanıtlarında `payableAmount` ve `taxTotal` indirimli değerler.
- Depo toplam doğrulaması `billing.bill_adjustments` üzerinden indirimli toplamlar.
- Düzeltme uygulanmış hesapta ürün bazında bölme → 409 `ITEM_SPLIT_ON_ADJUSTED_BILL`.

## Out of scope

- Ürün bazında bölmede indirimin kalemlere dağıtılması (ayrı ürün kararı).
- Ödeme/tahsilat tarafı (zaten indirimli tutarı kullanıyor).

## Dependencies

- V1-RMD-412

## Acceptance evidence

- `ALKAROS.Host.Experience.Billing.Tests` 21/21 ve `ALKAROS.Billing.SplitDesign.Tests` 29/29 (gerçek PostgreSQL 18,
  Release, 0 uyarı / 0 hata; `evidence/V1-RMD-413/tests.log`). SplitDesign test veritabanına düzeltme tablosu
  bağlandı; bağlanmadan depo testleri tablo bulunamadı hatası veriyordu.
- Yeni test `ASplitDesignOnADiscountedBillUsesTheDiscountedPayable`: 275'lik hesaba 25 indirim → tasarım 250 gösterir;
  100 + 150 tutar bölmesi 200 ile kaydedilir, dağıtım vergisi tasarımın indirimli vergisine eşittir; ürün bazında bölme
  409 `ITEM_SPLIT_ON_ADJUSTED_BILL` döner ve mevcut dağıtımlar değişmez. Üretim değişikliği geri alınınca kırmızı
  (beklenen 250, gelen 275); yalnız depo değişikliği geri alınınca da kırmızı (kayıt 400 `VALIDATION_FAILED`)
  (`evidence/V1-RMD-413/red-without-fix.log`).
- V1-RMD-393 probe'u P14 düzeltilmiş kopyada geçer (`evidence/V1-RMD-413/money-flow-probes-after-fix.log`). Aynı
  koşudaki P06 (F-10) ve P08 (F-12) açık bulgulardır; P07 ve P09 ön koşulları F-04 düzeltmesinden (V1-RMD-409) beri
  erişilemez.
- Semih'in elle deneyebileceği senaryo: 100 TL'lik hesaba 10 TL indirim uygulayıp "Hesabı böl" ekranını açın; ekran
  90 TL gösterir ve 40 + 50 bölme kaydedilir. Aynı hesapta ürün bazında bölme "İndirim veya ek ücret uygulanmış hesap
  ürün bazında bölünemez; tutar, kişi ya da serbest bölme kullanın." uyarısını verir.

## Handoff

- None
