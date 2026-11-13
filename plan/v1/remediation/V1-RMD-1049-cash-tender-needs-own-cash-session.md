# V1-RMD-1049 - Nakit tahsilat yalnız kasiyerin kendi açık kasa oturumuna alınır; yoksa "Önce kasayı açın" yönlendirmesi çıkar

- Task ID: V1-RMD-1049
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer7 kendi kasa oturumunu açmadan, terminalde yöneticinin açtığı oturuma nakit tahsilat alabildi; yönetici sonra nakit iade onayında "isteği yapan kasiyerin açık kasa oturumu yok" hatasını gördü (7. kasiyer denemesi). Nakit tahsilat artık yalnız çağıran kasiyerin kendi açık kasa oturumuna alınır: sunucu başkasının oturumuna nakit alımını Türkçe nedenle reddeder (iade onayındaki "kasiyerin kendi terminal oturumu" kuralıyla tutarlı), tahsilat ekranı bu durumda nakdi seçilemez bırakıp "Önce kasayı açın" yönlendirmesini gösterir. Semih kararı 2026-11-14. Müşteri cari tahsilatı (account-receipts) ve PosTerminal kasa durum rozeti bu görevin dışındadır.

## Owned surface

- `evidence/V1-RMD-1049/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/DualScreenApplication.CashSession.cs
  - src/Host/Composition/Errors/ApiErrorCatalog.cs
  - tests/Host/Experience/CashSession/CashSessionHttpTests.cs
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-cash-state.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1048

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
