# V20-CMP-001 - Obtain compliance sign-off

- Task ID: V20-CMP-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Planned

## Source basis

- PDF:I.45-I.54
- PDF:II.11-II.12
- PDF:III.33-III.34
- CORR:C21
- EXT:QNB-API-PUBLIC

## Goal

Uygulanan release'nin doğrulanmış mali, faturalama, saklama ve gizlilik uygulanabilirlik kararlarıyla eşleştiğine dair
adlandırılmış, tarihli onayı toplayın.

## Owned surface

- `release/evidence/compliance/**`
- Bu görev mevzuat yorumu üretmez veya ürün kodunu değiştiremez.

## In scope

- Decision-to-implementation evidence, reviewer authority, exception register ve approval expiry.
- QNB cancellation yalnız private/partner evidence ile applicable ise değerlendirilir; aksi durumda named approver ile
  N/A veya blocker kaydedilir.

## Out of scope

- Kendi kendini onaylayan yasal yorumlar, entegrasyon uygulaması ve production'nin kullanıma sunulması.

## Dependencies

- V0-CMP-001
- V0-CMP-002
- V0-CMP-003
- V0-CMP-004
- V1-BIL-001
- V1-BIL-003
- V1-CAT-002
- V13-INV-002
- V12-FSC-003
- V15-KVK-001
- V15-KVK-002
- V20-INT-001
- V20-INT-002
- V20-UAT-001
- V20-UAT-002

## Deliverables

- İmzalı uyumluluk matrisi ve istisna kaydı.

## Acceptance evidence

- Uygulanabilir her yükümlülüğe, onay ve uygulama kanıtları adı verilmiştir; çözülmemiş veya süresi dolmuş herhangi bir
  onay kapıyı bloke eder.
- `V12-FSC-003` kanıtlı `NotApplicable` ise adisyon lifecycle yükümlülüğü sign-off'ta adlandırılmış dated decision ile
  kapanır; `V20-INT-001` veya `V20-INT-002` kanıtlı `NotApplicable` ise ilgili provider certification yükümlülüğü
  benzer şekilde ele alınır; kalan yükümlülükler onay ve uygulama kanıtlarıyla yine doğrulanır.
- `V20-UAT-002` kanıtlı `NotApplicable` ise finance/inventory acceptance onayı sign-off'ta adlandırılmış dated decision
  ile ele alınır; kalan yükümlülükler onay ve uygulama kanıtlarıyla yine doğrulanır.
- `V20-UAT-001` kanıtlı `NotApplicable` ise service flow acceptance onayı sign-off'ta adlandırılmış dated decision ile
  ele alınır; kalan yükümlülükler onay ve uygulama kanıtlarıyla yine doğrulanır.

## Handoff

- V20-GAT-002
- V20-REL-003
