from pathlib import Path
import argparse, json, re, subprocess, sys
HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
TARGET = ROOT / 'plan/v1/remediation/V1-RMD-103-older-audit-remaining-high-medium-findings.md'
BASELINE_COMMIT = '114b8da4c527da6805b34ca89fd770613c5c6c7d'
NOTE = '''### Güncellik notu (V1-RMD-752 / F0908)

Aşağıdaki görev metni tarihsel kapsam ve kabul sözleşmesini korur; B1 altındaki bugün/hâlâ anlatımı görev tarihindeki durumu belirtir.
`src/Modules/Billing/SplitDesign/SplitEngine.cs` içindeki `CreateEqualSplit`, `CreateAmountSplit` ve `CreateCustomSplit`, `adjustment` verilince `AdjustedPayableAmount`/`AdjustedTaxTotal`, null iken `Bill.PayableAmount`/`Bill.TaxTotal` kullanır.
`src/Host/Experience/Billing/BillingSplitStore.cs` içindeki `SaveEqualAsync`, `SaveAmountsAsync` ve `SaveCustomAsync`, `LoadAdjustmentAsync` sonucunu engine'e taşır; `SaveItemsAsync`, adjustment mevcut ve `AdjustedPayableAmount != Bill.PayableAmount` ise kalem bölmeyi reddeder.
Bu not kalem bazlı indirim dağıtım politikası, yeni üretim değişikliği veya tüm ödeme akışının doğruluğu iddiası değildir.

'''


def validate(text):
    original_text = subprocess.check_output(['git', 'show', f'{BASELINE_COMMIT}:plan/v1/remediation/V1-RMD-103-older-audit-remaining-high-medium-findings.md'], cwd=ROOT, text=True, encoding='utf-8')
    if text.count(NOTE) != 1:
        raise ValueError('Current-flow note missing or altered')
    if text != original_text.replace('## Goal', NOTE + '## Goal', 1):
        raise ValueError('Historical contract changed or note inserted at wrong location')
    engine = (ROOT / 'src/Modules/Billing/SplitDesign/SplitEngine.cs').read_text(encoding='utf-8')
    for method in ['CreateEqualSplit', 'CreateAmountSplit', 'CreateCustomSplit']:
        body = member(engine, f'public static IReadOnlyList<BillAllocation> {method}(')
        for line in ('var totalPayable = adjustment?.AdjustedPayableAmount ?? bill.PayableAmount;',
                     'var totalTax = adjustment?.AdjustedTaxTotal ?? bill.TaxTotal;'):
            if line not in body:
                raise ValueError(f'{method} adjusted/null total path missing: {line}')
    store = (ROOT / 'src/Host/Experience/Billing/BillingSplitStore.cs').read_text(encoding='utf-8')
    for method, engine_call in [('SaveEqualAsync', 'CreateEqualSplit'), ('SaveAmountsAsync', 'CreateAmountSplit'),
                                ('SaveCustomAsync', 'CreateCustomSplit')]:
        body = member(store, f'public async Task<BillSplitDesignDto> {method}(')
        if 'var adjustment = await LoadAdjustmentAsync(bill, cancellationToken);' not in body:
            raise ValueError(f'{method} does not load the adjustment')
        if not re.search(rf'SplitEngine\.{engine_call}\([^;]*\badjustment\);', body):
            raise ValueError(f'{method} does not pass the adjustment to {engine_call}')
    save_items = member(store, 'public async Task<BillSplitDesignDto> SaveItemsAsync(')
    guard = ('if (adjustment is not null && adjustment.AdjustedPayableAmount != bill.PayableAmount)\n'
             '            throw new SplitItemsOnAdjustedBillException(billId);')
    if guard not in save_items or save_items.index(guard) > save_items.index('SplitEngine.CreateItemSplit('):
        raise ValueError('SaveItemsAsync guard missing or after the item split')
    endpoint = (ROOT / 'src/Host/Experience/Billing/BillingSplitApplication.cs').read_text(encoding='utf-8')
    if 'SplitItemsOnAdjustedBillException => (409, "ITEM_SPLIT_ON_ADJUSTED_BILL",' not in endpoint:
        raise ValueError('Item split rejection is not mapped to 409 ITEM_SPLIT_ON_ADJUSTED_BILL')


def member(source, signature):
    start = source.index(signature)
    return source[start:source.index('\n    }\n', start) + 6]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--document', type=Path, default=TARGET)
    args = parser.parse_args()
    try:
        validate(args.document.read_text(encoding='utf-8'))
    except (ValueError, OSError, subprocess.CalledProcessError) as error:
        print(json.dumps({'result': 'FAIL', 'error': str(error), 'tier': 'Document acceptance and source-shape guard only; not runtime proof'}))
        return 1
    print(json.dumps({'result': 'PASS', 'checks': ['current-note', 'historical-contract-preserved', 'engine-adjustment-null-path', 'engine-tax-null-path', 'store-handoff', 'item-split-guard', 'endpoint-409-mapping'], 'tier': 'Document acceptance and source-shape guard only; independent semantic review and runtime tests required'}))
    return 0


if __name__ == '__main__':
    sys.exit(main())