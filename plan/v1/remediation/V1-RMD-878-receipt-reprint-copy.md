# V1-RMD-878 - Kapanmış hesabın fişini yeniden yazdırma: kayıtlı ve KOPYA işaretli (F0052)

- Task ID: V1-RMD-878
- Status: InProgress
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

Ürün kararı F0052 (Semih onayladı): ödenmiş hesabın bilgi fişi yeniden yazdırılabilir; her yeniden yazdırma denetim izine yazılır (kim, ne zaman, hangi hesap: bill.receipt.reprinted) ve fişte KOPYA yazar. Yeni yazıcı mimarisi yok: mevcut tarayıcı yazdırma akışı ve /receipt okuması kullanılır. Kararlar: izin payments.take (fişi okumakla aynı, yeni izin ve migration yok); yalnız Paid hesap (aksi 409 Türkçe ileti); sayı sınırı yok, her çağrı bir denetim olayı; ödeme anında ilk yazdırma değişmez (kayıtsız, KOPYA yok), ilk yazdırmadan sonra ve kapanmış hesap açıldığında eylem Fişi yeniden yazdır olur.

## Owned surface

- `evidence/V1-RMD-878/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/DualScreenApplication.Receipt.cs — POST receipt/reprint uç noktası ve ortak fiş oluşturucu
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js — Fişi yeniden yazdır eylemi ve KOPYA başlığı
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.css — KOPYA yazdırma stili
  - tests/Host/Experience/PaymentTender/BillReceiptHttpTests.cs — uç nokta testleri
  - tests/Host/Experience/PaymentTender/BillTerminalAccessTenderHttpTests.cs — terminal erişim rotası
  - tests/Clients/StaticApps/split-payment-receipt.test.js — ekran testleri
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. POST /api/v1/terminals/{terminalId}/billing/bills/{billId}/receipt/reprint: payments.take, yalnız Paid hesap, denetim olayı bill.receipt.reprinted (aktör, hesap numarası), fiş verisini döner
2. Kasa ekranı: kapanmış hesapta Fişi yeniden yazdır; fiş KOPYA başlığı taşır; ödeme anındaki ilk yazdırma eskisi gibi
3. Gerçek Postgres testleri, ekran testleri ve mutasyon

## Out of scope

- Mali fiş/yazıcı donanımı, yeniden yazdırma sayısı sınırı veya yönetici onayı, raporlama ekranı, sayaç (KOPYA 2)

## Dependencies

- None

## Acceptance evidence

1. Kırmızı/yeşil test çıktıları
2. Mutasyon sonuçları
3. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
