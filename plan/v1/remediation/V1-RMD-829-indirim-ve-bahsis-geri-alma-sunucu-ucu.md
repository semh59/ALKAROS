# V1-RMD-829 - İndirim ve bahşiş geri alma: sunucu ucu

- Task ID: V1-RMD-829
- Status: InProgress
- Assignee: claude-code-session_019vXeYXjE3sM6VZKpRMj1L9
- Work type: implementation
- Surface state: Existing

## Goal

Kasa incelemesi K18: tahsilat sayfasında yanlış girilen indirim ya da bahşiş geri alınamıyor (yalnız eklenir). PO kararı: en küçük hata kritik, açık bırakma yok. Bu görev sunucu tarafıdır, arayüz V1-RMD-830.

Tasarım: `POST /api/v1/terminals/{terminalId}/bills/{billId}/adjustments/{adjustmentId}/undo`.
- Yetki: bahşiş girişi ile aynı (`RequireMutationAsync`, bills.split). Geri alma ödenecek tutarı yalnız ilk haline döndürür; kimseye ek yetki vermez.
- Yalnız indirim (`DiscountPercentage`, `DiscountAmount`) ve bahşiş (`Tip`) geri alınır; hizmet bedeli, kuver ve özel ücret `ADJUSTMENT_NOT_UNDOABLE` (400).
- Hesap durumu uygulama ucuyla aynı kuralı izler (`IsDiscountable`): ödenmiş, iptal edilmiş ya da ödeme alınmış hesapta `BillDiscountUnsupportedBillStateException`; hesap satırı ve ödeme tahsisi kilidi uygulama ucuyla aynıdır.
- Kayıt silinir (migration yok: tüm okuyucular - fatura, rapor, vardiya özeti, bölüştürme - otomatik tutarlı kalır); iz kaybolmaz: `bill.adjustment.undone` denetim olayı silinen kaydın tam görüntüsünü (tür, tutar, gerekçe, kim ekledi) ve geri alan kullanıcıyı tutar.
- Olmayan kalem `ADJUSTMENT_NOT_FOUND` (404); aynı geri alma tekrarı da 404 verir (istemci "zaten kaldırılmış" diye yeniler). Tekrar anahtarı alınmaz (uç doğal olarak idempotenttir: ikinci çağrı etkisizdir); `idempotency_allowlist.json` kaydı eklenir.
- Müşteri ekranı `PaymentChanged` ile bilgilendirilir; hesap satır sürümü artar.

## Owned surface

- `evidence/V1-RMD-829/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Billing/BillingSplitApplication.cs (V1-RMD-103) — uç.
  - src/Host/Experience/Billing/BillingSplitStore.cs (V1-RMD-057) — geri alma işlemi.
  - src/Host/Experience/Billing/BillingSplitContracts.cs (V1-RMD-057) — sonuç sözleşmesi.
  - src/Modules/Billing/Adjustments/IBillAdjustmentRepository.cs (V1-BIL-003) — bağlantı içinde silme.
  - src/Modules/Billing/Adjustments/PostgresBillAdjustmentRepository.cs (V1-BIL-003) — bağlantı içinde silme.
  - tests/Host/Experience/Billing/BillingSplitHttpTests.cs (V1-RMD-103) — uç testleri.
  - tests/Architecture/ApiConventions/idempotency_allowlist.json (V1-RMD-430) — kayıt.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Her kabul koşulu için önce kırmızı test, sonra düzeltme, sonra yeşil; mutasyon turu (`cp` + `cmp` ile geri alınır).
2. İngilizce kod ve test adları; kullanıcıya dönen mesajlar Türkçe, kod kararlı `UPPER_SNAKE`.

## Out of scope

- Arayüz (V1-RMD-830); hizmet bedeli/kuver geri alma; ödeme iadesi.

## Dependencies

- V1-RMD-802

## Acceptance evidence

1. Gerçek Postgres HTTP testleri: indirim geri alınınca ödenecek tutar eski haline döner; bahşiş geri alınınca düşer; bağımsız iki kalemden biri geri alınınca diğeri kalır; oturumsuz 401, yetkisiz 403; olmayan ve ikinci geri alma 404; geri alınamaz tür 400; ödenmiş hesapta durum hatası; denetim olayı silinen kaydı taşır; müşteri ekranı bilgilendirilir.
2. Mimari testler (ApiConventions, ModuleBoundaries) ve ilgili Billing/PaymentTender testleri yeşil.
3. Mutasyon: durum kontrolü, tür kontrolü, denetim olayı, kilit, silme koşulu (`bill_id` eşleşmesi) bozulunca ilgili test kırılır.
4. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
5. İki ardışık bağımsız qa-* incelemesi, bulgular giderilir, düzeltme sonrası doğrulama turu.

## Handoff

- None
