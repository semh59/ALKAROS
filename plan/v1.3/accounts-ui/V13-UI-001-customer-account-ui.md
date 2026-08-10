# V13-UI-001 - Implement customer and account UI

- Task ID: V13-UI-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.30

## Goal

Alan izinleri altında müşteri profili, hesap defteri, bakiye/yaşlanma ve hesap payment ekranlarını uygulayın.

## Owned surface

- `src/Clients/Cashier/CustomerAccounts/**`, `tests/Clients/Cashier/CustomerAccounts/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- PII erişimi, ledger görünümü, Bill charge, cash/card account receipt, Unknown reconciliation status, anonymization ve
  stale-balance göstergesi.

## Out of scope

- Invoice gönderimi ve gizlilik toplu yürütmesi.

## Dependencies

- V13-CST-001
- V13-ACC-001
- V13-ACC-002
- V13-ACC-003
- V13-ACC-004
- V13-ACC-005
- V13-ACC-006
- V13-ACC-007
- V13-ACC-008
- V1-IAM-002
- V0-CMP-005

## Deliverables

- `src/Clients/Cashier/CustomerAccounts/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Public contract/UI ve otomatik success/failure/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Yetkisiz PII sunucu tarafında gizlidir; görüntülenen bakiye projeksiyon rebuild ile eşleşiyor; tekrarlanan payment
  göndermenin bir etkisi vardır.
- Cash/card receipt sonucu AccountPayment durumunu ve reconciliation gereksinimini gösterir; Unknown sonuç success veya
  yeniden tahsil edilebilir olarak sunulmaz.
- Bill üzerindeki CustomerAccount tender yalnız `V13-ACC-008` route ve fiscal closure sonucu ile success gösterir.
- Customer account UI, `V0-CMP-005` kararındaki cashier success criteria listesini karşılar.
- `V13-ACC-008` kanıtlı `NotApplicable` ise CustomerAccount tender UI'da enable edilmez; kalan bakiye, receipt ve
  reconciliation gereksinimi gösterimi yine doğrulanır.

## Handoff

- V13-UI-002
