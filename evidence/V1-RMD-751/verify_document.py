from pathlib import Path
import argparse
import json
import sys

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
TARGET = ROOT / 'plan/v1/remediation/V1-RMD-237-bill-discount-audit-event-write-path.md'
NOTE = '''### Güncellik notu (V1-RMD-751 / F0388)

Aşağıdaki görev metni tarihsel kapsam ve kabul sözleşmesini korur; endpoint dönüşünden sonra ayrı audit çağrısı anlatımı güncel akış değildir.
`src/Host/Experience/Billing/BillingSplitApplication.cs` içindeki `MapBillingSplitApi` discount işleyicisi audit bağımlılığını `src/Host/Experience/Billing/BillingSplitStore.cs` içindeki `ApplyDiscountAsync` metoduna iletir.
Yeni indirimde `AddAsync` ve `bill.discount.applied` için `AppendAsync` yazımları aynı connection/transaction ile `CommitAsync` öncesinde yapılır; eşleşen replay yeni audit yazmadan döner.
`void`/`comp` bu tarihsel görevin kapsamı dışındadır. Bu not üretim değişikliği veya yeni test başarısı iddiası değildir.

'''


def validate(text):
    original = (HERE / 'original-document.md').read_text(encoding='utf-8')
    if text.count(NOTE) != 1:
        raise ValueError('Current-flow note missing or altered')
    if text != original.replace('## Goal\n', NOTE + '## Goal\n', 1):
        raise ValueError('Historical contract changed or note inserted at wrong location')
    store = (ROOT / 'src/Host/Experience/Billing/BillingSplitStore.cs').read_text(encoding='utf-8')
    start = store.index('public async Task<(BillAdjustment Adjustment, AdjustedBillSummary Summary)> ApplyDiscountAsync') if 'public async Task<(BillAdjustment Adjustment, AdjustedBillSummary Summary)> ApplyDiscountAsync' in store else store.index(' ApplyDiscountAsync(')
    end = store.index('\n    public ', start + 1)
    body = store[start:end]
    positions = [body.index(value) for value in ['BeginTransactionAsync', 'if (replay is not null)', '_adjustments.AddAsync(adjustment, connection, lockTransaction, cancellationToken)', 'await auditEvents.AppendAsync(', 'connection, lockTransaction, cancellationToken);\n        await lockTransaction.CommitAsync']]
    if positions != sorted(positions):
        raise ValueError('Expected transaction/replay/write ordering changed')
    endpoint = (ROOT / 'src/Host/Experience/Billing/BillingSplitApplication.cs').read_text(encoding='utf-8')
    discount = endpoint[endpoint.index('billsGroup.MapPost("/{billId:guid}/discount"'):endpoint.index('// V1-WTR-020:')]
    if 'billId, request, principal.UserId, auditEvents, context.TraceIdentifier' not in discount or '.AppendAsync(' in discount:
        raise ValueError('Endpoint audit handoff changed')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--document', type=Path, default=TARGET)
    args = parser.parse_args()
    try:
        validate(args.document.read_text(encoding='utf-8'))
    except (ValueError, OSError) as error:
        print(json.dumps({'result': 'FAIL', 'error': str(error), 'tier': 'Document acceptance and source-shape guard only; not runtime proof'}))
        return 1
    print(json.dumps({'result': 'PASS', 'checks': ['current-note', 'historical-contract-preserved', 'transaction-write-order', 'endpoint-handoff'], 'tier': 'Document acceptance and source-shape guard only; independent semantic review and runtime tests required'}))
    return 0


if __name__ == '__main__':
    sys.exit(main())
