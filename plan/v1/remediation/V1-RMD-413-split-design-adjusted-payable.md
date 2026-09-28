# V1-RMD-413 - İndirimli hesapta bölme tasarımının indirimli tutarı kullanması

- Task ID: V1-RMD-413
- Status: InProgress
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

- Kapanışta doldurulacak.

## Handoff

- None
