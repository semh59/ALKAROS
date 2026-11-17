# V1-RMD-1151 - Ödeme öncesi "Hesap" ön fişi: mali olmayan bilgi fişi, ayrı sayaç, masa akışı ve Bekleyen hesaplar'dan tek dokunuşla

- Task ID: V1-RMD-1151
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Planned

## Goal

Ürün sahibi kararı (Semih, 2026-11-18, V1-RMD-1150 madde 1): kasiyer ödenmemiş bir hesabın "Hesap" ön fişini basabilir. Belge mali olmayan bilgi fişidir ("Bu belge mali fiş değildir"), mali değer iddiası taşımaz, ödenmiş hesabın orijinal/kopya akışından (`billing.bills.receipt_first_printed_at`) tamamen ayrı bir tür ve sayaç kullanır; ön fiş basmak ödenmiş hesabın orijinal hakkını tüketmez.

1. Yeni uç `POST /api/v1/terminals/{terminalId}/orders/{orderId}/pre-receipt` (`payments.take`, gövdede `IdempotencyKey`). Uç hesap AÇMAZ: siparişin açık hesabı yoksa satırlar siparişten, varsa hesaptan okunur (hesap açmak masaya ürün eklemeyi ve birleştirmeyi kapatır). Ödenmiş hesabı olan sipariş reddedilir (`BILL_NOT_UNPAID`), terminal sahipliği olan hesap için başka terminal reddedilir.
2. Sayaç ayrı bir denetim türüdür (`order.pre-receipt.printed`); sıra numarası sipariş satırı kilidi altında mevcut kayıtlar sayılarak verilir ve aynı anahtarla tekrar aynı numarayı döndürür. Migration yoktur.
3. PosTerminal: Bekleyen hesaplar satırında ve masa akışında "Hesap fişi" düğmesi (48 px, 14 px); tek dokunuşta uç çağrılır, fiş tarayıcı yazdırma penceresiyle basılır; hata kodları Türkçe karşılığa çevrilir.
4. Fişte "Bu belge mali fiş değildir" ibaresi ve ön fiş numarası vardır; "mali müşavirle teyit edilecek" notu `docs/domain/bill-pre-receipt.md` içindedir (dış bağımlılık: ibare ve e-Adisyon uygulanabilirliği).

## Owned surface

- `evidence/V1-RMD-1151/**`
- `docs/domain/bill-pre-receipt.md`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/DualScreenApplication.Receipt.cs
  - src/Clients/PosTerminal/src/features/pending-checks/pre-receipt.css
  - src/Clients/PosTerminal/src/features/pending-checks/preReceipt.test.ts
  - src/Clients/PosTerminal/src/features/pending-checks/preReceipt.ts
  - tests/Host/Experience/PaymentTender/BillReceiptHttpTests.cs
  - src/Clients/PosTerminal/src/strings.ts
  - src/Clients/PosTerminal/src/features/pending-checks/PendingChecksWorkspace.tsx
  - src/Clients/PosTerminal/src/features/pending-checks/PendingChecksWorkspace.test.tsx
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.tsx
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Gerçek Postgres testleri: hesapsız sipariş, hesaplı sipariş (ek kalemler ve ödenen), ödenmiş hesap reddi, sayaç ayrılığı (orijinal hakkı tüketilmez), aynı anahtarla tekrar, yetki, başka terminal, eşzamanlı basımlar
2. PosTerminal testleri, kırmızı test, düzeltme, mutasyon
3. İki bağımsız qa incelemesi

## Out of scope

- Mali fiş, e-Adisyon, yazıcı donanımı; ödenmiş hesabın fişi (V1-RMD-887); satış ekranının Tahsil et düğmesi ve hesap bölme sayfası

## Dependencies

- V1-RMD-1150

## Acceptance evidence

1. Kırmızı/yeşil test çıktıları ve mutant listesi
2. İki ardışık bağımsız qa-* incelemesi
3. Host PaymentTender (filtreli), PosTerminal testleri ve istemci üretim derlemesi exit code 0

## Handoff

- None
