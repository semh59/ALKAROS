# V12-PUI-003 - Implement refund and payment exception UI

- Task ID: V12-PUI-003
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.26-I.29

## Goal

İzin verilen tam/kısmi geri ödeme, Bilinmeyen payment takibi ve mali/mutabakat status gösterimini uygulayın.

## Owned surface

- `src/Clients/Cashier/Payments/Exceptions/**`, `tests/Clients/Cashier/Payments/Exceptions/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Uygun tahsis seçimi, geri ödeme tutarı, nedeni, provider ilerleme durumu, belirsiz sonuç ve vaka bağlantısı.

## Out of scope

- Mutabakat çözüm eylemleri V1.5'e ayrılmıştır.

## Dependencies

- V12-ALC-003
- V12-ALC-004
- V12-HUG-002
- V12-HUG-003
- V12-FSC-001
- V12-REC-001
- V1-IAM-002
- V0-CMP-005

## Deliverables

- `src/Clients/Cashier/Payments/Exceptions/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Contract/UI ve otomatik success/failure/retry testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Kısmi geri ödeme önizlemesi sunucuyla eşleşir; retry bir işlem üretir; bilinmeyen sonuç hiçbir zaman kanıt olmadan
  tamamlanmış olarak görüntülenmez.
- Exception akışları `V0-CMP-005` kararındaki cashier success criteria ve approved exception kayıtlarını karşılar.
- `V12-REC-001` kanıtlı `NotApplicable` ise vaka bağlantılı mutabakat status gösterimi beklenmez; kısmi geri ödeme
  önizlemesi, retry ve bilinmeyen sonuç davranışı kendi kaynaklarıyla yine doğrulanır.

## Handoff

- V15-REC-002
