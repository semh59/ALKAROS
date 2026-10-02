# V1-RMD-492 - QNB gelen alış faturası çekme

- Task ID: V1-RMD-492
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-01

## Goal

QNB gelen kutusundaki alış faturalarını periyodik çekip `V1-RMD-489` taslak hattına beslemek. QNB gelen kutusu sözleşmesi doğrulanmamıştır; istemci sözleşmesi varsayımdır ve QNB'den farklı bir şey çıkarsa revize edilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-492-qnb-purchase-invoice-inbox.md`

## In scope

- Kesin yollar görev başlatılırken yazılır. `IQnbSoapClient` genişletmesi (gelen fatura listesi ve XML indirme), kimlik bilgisi mevcut QNB kayıt ekranından, periyodik çekme, ETTN ile mükerrer önleme (içeri alma hattı `ImportAsync` ETTN tekrarını zaten reddeder); gerçek QNB'ye karşı denenemez, sahte sunucuyla sınanır.

## Out of scope

- Gerçek QNB hesabıyla doğrulama; fatura onayı otomatikleştirme (onay hep yöneticidedir).

- Çekilen faturalar `V1-RMD-491` ekranında XML ile yüklenenlerle aynı listede görünür.

## Dependencies

- V1-RMD-489

## Acceptance evidence

- Testler, mutasyon kanıtı ve gerçek Host denemesi; çıktılar `evidence/V1-RMD-492/` altındadır.

## Handoff

- None
