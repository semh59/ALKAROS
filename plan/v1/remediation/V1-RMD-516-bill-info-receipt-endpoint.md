# V1-RMD-516 - Bilgi fişi için hesap özeti ucu

- Task ID: V1-RMD-516
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-13

## Goal

Kasa sayfasından basılacak bilgi fişi (PO kararı: mali olmayan bilgi fişi) için hesabın kalemlerini, ayarlamalarını (indirim, hizmet bedeli, bahşiş), toplamlarını ve ödemelerini (tutar, yöntem, durum) tek yanıtta döndüren salt okunur uç: `GET /api/v1/terminals/{terminalId}/billing/bills/{billId}/receipt`. Kasiyer oturumu gerekir; veri yazılmaz.

## Owned surface

- `plan/v1/remediation/V1-RMD-516-bill-info-receipt-endpoint.md`
- `evidence/V1-RMD-516/**`
- `src/Host/DualScreen/DualScreenApplication.Receipt.cs`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/PaymentTender/BillReceiptHttpTests.cs
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Yanıt: hesap numarası, durum, açılış/kapanış zamanı, para birimi, kalemler (ad, miktar, birim fiyat, tutar), ayarlamalar (tür, tutar, indirim mi), ara toplam, ayarlı ödenecek tutar, vergi toplamı, ödemeler (tutar, yöntem: Nakit/Cari/Kart-EFT, durum, zaman). İptal edilmiş hesapta kalemler döner; iade edilmiş ödeme durumuyla görünür.
- Gerçek Host + Postgres HTTP testi: kalemler, indirim/bahşiş, yöntem türetme (nakit/cari/diğer), yetkisiz oturum, olmayan hesap.

## Out of scope

- Yazdırma arayüzü (V1-RMD-517), mali fiş, yazarkasa bağlantısı.

## Dependencies

- V1-RMD-512

## Acceptance evidence

- Her kabul koşulu için test ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-516/` altındadır.

## Handoff

- None
