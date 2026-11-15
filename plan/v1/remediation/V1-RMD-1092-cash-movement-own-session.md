# V1-RMD-1092 - Nakit giriş ve çıkış yalnız kasiyerin kendi açık kasa oturumuna yazılır

- Task ID: V1-RMD-1092
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Semih kararı 2026-11-15: kasa oturumuna nakit giriş ve nakit çıkış yalnız işlemi yapanın KENDİ açık oturumuna yazılır. Bugün cash-movements ucu (POST) oturumun yalnız bu terminale ait olduğuna bakıyor; oturum sahibini denetlemiyor, yani aynı terminalde başka kullanıcının açık oturumuna nakit giriş veya çıkış yazılabiliyor. Tahsilattaki (cash-tender, account-receipts) kural aynen uygulanır: oturum sahibi çağıran değilse 409 CASH_SESSION_NOT_OWNED döner, yönetici muafiyeti yoktur ve hiçbir hareket veya onay talebi yazılmaz. 2026-11-03 kararları (çıkış limiti, onay eşiği, yönetici onayı) değişmez; onay ve ret uçları dokunulmaz. Kasa oturumu sayfası oturum sahibi olmayan kullanıcıda Nakit Giriş ve Nakit Çıkış düğmelerini kapatır ve yanında "kendi kasa oturumunuzu açın" der; sunucu aynı kodla reddederse ham kod yerine Türkçe karşılığı görünür.

## Owned surface

- `evidence/V1-RMD-1092/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/DualScreenApplication.CashSession.cs
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
  - tests/Host/Experience/CashSession/CashSessionHttpTests.cs
  - tests/Host/Experience/CashSession/CashMovementApprovalHttpTests.cs
  - tests/Clients/StaticApps/cash-session-movement.test.js
  - tests/Clients/StaticApps/cash-session-owner.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (client-build-gate)

## Out of scope

- Nakit çıkış onay, ret ve geri çekme uçları; çıkış limiti ve onay eşiği kuralları; tahsilat uçları; PosTerminal kasa rozeti

## Dependencies

- V1-RMD-1091

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
