# V1-RMD-282 - Ödeme siparişi kapatır (`Completed`)

- Task ID: V1-RMD-282
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-24

## Goal

`docs/design/modules/check-and-table.md` (`V1-ORD-006`) kök sebebi zaten yazmıştı: "hiçbir sipariş
kapanmıyor". Garson kalemi "servis edildi" diye işaretlemiyor, mutfak yalnız kalem durumunu yansıtıyor;
sipariş `Ready → Served → Completed` yolunu hiç yürümüyor ve sonsuza dek `Submitted` kalıyordu. Somut zararlar:

1. Garson devri (`orders.transfer-server`) "terminal olmayan tüm siparişleri" devrediyor: kapanmayan tüm geçmiş
   yeni garsona geçiyordu.
2. Önerilen garson (`SuggestedWaiterResolver`) açık sipariş yükünü sayıyor: eski siparişler yükü sonsuza dek şişiriyordu.
3. Saklama süpürmesi yalnız `Completed`/`Cancelled`/`Rejected` siparişlerin notlarını anonimleştiriyor: personel
   siparişlerinin notları (kişisel bilgi taşıyabilir) süresiz duruyordu.

Semih'in kararı (2026-09-24): **ödeme siparişi kapatır**, mutfak bitmemiş olsa da (ödeme mali kapanıştır; önce
öde akışlarında servisten önce gelir; kalemler kendi mutfak biletlerinde yaşar). Ve masaya bağlı (kasaya
gönderilmemiş) bir hesap ödenirse masa serbest kalır (parti ödedi ve gitti).

`Order.CompleteOnPayment`: `Submitted`/`Accepted`/`Preparing`/`Ready`/`Served` durumlarından `Completed`'a geçer;
`Draft`/`PendingConfirmation`/`Rejected`/`Cancelled` durumlarında geçmez; durum geçmişine "Ödeme tamamlandı" ve
işlemi yapan yazılır; zaten `Completed` ise değişmez. `OrderSettlementService` (Host Orders alanı): bir hesap `Paid`
olunca, siparişin TÜM hesapları `Paid`/`Cancelled` ise (en az biri `Paid`) siparişi kapatır; sipariş hâlâ masanın
bağlı hesabıysa aynı işlemde masa işaretçisini temizler ve `Occupied` masayı `Available` yapar. Kasaya gönderilip
masadan kopmuş hesabın masasına ASLA dokunmaz (yeni müşteri oturmuş olabilir). Genel tahsilat ve nakit tahsilat
uç noktaları hesabı kapattıktan sonra çağırır; en iyi çaba (para zaten kaydedildi; hata ödemeyi bozmaz).

## Owned surface

- `plan/v1/remediation/V1-RMD-282-payment-completes-the-order.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/OrderSettlementService.cs
  (V1-ORD-006 ailesindeki Orders Host klasörüne eklenen yeni dosya)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/OrderManagementEndpoints.cs
  (yalnız servisin kaydı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Orders/OrderAggregate/Order.cs
  (V1-ORD-001 sahipliğinde kalır — yalnız `CompleteOnPayment` ve ortak geçiş yöntemi)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.Payments.cs
  (V13-PUI-001 sahipliğinde kalır — yalnız kapatma yardımcısına sipariş kapatma)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.CashSession.cs
  (V13-CSH-004 sahipliğinde kalır — yalnız nakit tahsilatta aynı çağrı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Orders/OrderAggregate/OrderDomainTests.cs
  (V1-ORD-001 sahipliğinde — 2 yeni test)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/PaymentTender/PaymentTenderHttpTests.cs
  (V13-PUI-001 sahipliğinde — 3 yeni test, gerçekçi `Submitted` sipariş ve yardımcılar)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/14-till-queue-of-sent-checks.spec.js
  (V1-RMD-280 ile eklendi — siparişin `Completed` olduğu doğrulaması)

## In scope

1. Yeni geçiş, kapatma servisi, iki tahsilat yolunda tetik, testler.

## Out of scope

- Hesap yeniden açılırsa siparişi geri açmak (`Completed` terminaldir; nadir, ayrı görev).
- Var olan (geçmişte kapanmamış) siparişleri geriye dönük kapatmak: bunun için ayrı, dikkatli bir veri görevi
  gerekir (hesabı `Paid` olanları taramak).
- Kalemlerin "servis edildi" işaretlenmesi ve sipariş `Served` durumu.

## Dependencies

- V1-ORD-006
- V1-RMD-276
- V1-RMD-279

## Acceptance evidence

- Order aggregate testleri 138/138 (yeni: her durum için izin/ret, geçmiş kaydı gerekçe/işlemci/`ClosedAt`,
  idempotent). PaymentTender (UTF8 Postgres 18) 27/27, yeni: masaya bağlı hesabın son ödemesi siparişi
  `Completed` yapar ve masa `Available`/işaretçi boş; kasaya gönderilmiş hesap tamamlanır ama masa (yeni müşteri,
  `Occupied`) DEĞİŞMEZ; başka açık hesabı olan sipariş açık kalır. **Mutasyon kontrolü:** sipariş kaydı devre dışı
  bırakılınca iki test `Completed` beklerken `Submitted` gördü; geri alınınca geçti.
- Cashier E2E 32/32 (gerçek Host + Chromium): tam ödenen hesabın siparişi sunucuda `Completed`. CashSession 14/14.
- `consistency_audit.py`, `plan_audit_tool.py validate`, modül sınır testleri temiz.

## Handoff

- None
