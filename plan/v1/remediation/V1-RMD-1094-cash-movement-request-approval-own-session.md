# V1-RMD-1094 - Bekleyen nakit çıkış talebi yalnız talep sahibinin kendi kasa oturumunda onaylanır

- Task ID: V1-RMD-1094
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Semih kararı 2026-11-15: nakit hareketi yalnız işlemi yapanın KENDİ açık kasa oturumuna yazılır (V1-RMD-1092). Bu kuraldan önce başkasının oturumuna açılmış bekleyen nakit çıkış talepleri, onay ucu oturum sahibine bakmadığı için hâlâ onaylanıp o oturumun çekmecesinden para çıkarabiliyor. Düzeltme: onay ucu, talebin oturumunun sahibi talebi açan kişi değilse 409 CASH_MOVEMENT_REQUEST_NOT_OWN_SESSION döner; talep açık kalır, hiçbir defter satırı yazılmaz; yönetici talebi reddedebilir, talep sahibi geri çekebilir. Veri göçü yoktur: kural çalışma zamanında uygulanır, mevcut talepler silinmez veya değiştirilmez. 2026-11-03 kararları (çıkış limiti, onay eşiği, dört göz ayarı, yöneticinin kendi talebini onaylaması) değişmez. Kasa oturumu sayfası yeni kodu ham kod yerine Türkçe metinle gösterir.

## Owned surface

- `evidence/V1-RMD-1094/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/CashMovementStore.cs
  - src/Host/DualScreen/DualScreenApplication.CashMovements.cs
  - src/Host/Composition/Errors/ApiErrorCatalog.cs
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
  - tests/Host/Experience/CashSession/CashMovementApprovalHttpTests.cs
  - tests/Clients/StaticApps/cash-session-movement.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (client-build-gate)

## Out of scope

- Nakit çıkış limiti ve onay eşiği kuralları; ret ve geri çekme uçları; nakit giriş ve çıkış kaydı ucu (V1-RMD-1092); veri göçü

## Dependencies

- V1-RMD-1093

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
