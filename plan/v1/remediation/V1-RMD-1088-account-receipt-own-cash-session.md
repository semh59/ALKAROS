# V1-RMD-1088 - Cari nakit tahsilat yalnız kasiyerin kendi açık kasa oturumuna alınır

- Task ID: V1-RMD-1088
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Semih kararı 2026-11-14: cari hesap tahsilatı nakitte kasiyerin KENDİ açık oturumunu ister. Bugün cash-tender ucu başkasının oturumunu 409 CASH_SESSION_NOT_OWNED ile reddediyor (V1-RMD-1049), ama cari tahsilat ucu (account-receipts) yalnız oturumun bu terminale ait olduğuna bakıyor: başka kullanıcının açık oturumuna nakit yazılabiliyor ve Cari hesaplar sayfası başkasının oturumunu "açık" sayıp Tahsil et düğmesini açıyor. Düzeltme: sunucu account-receipts ucunda aynı sahiplik kuralını (yönetici muafiyeti olmadan) uygular ve hiçbir tahsilat yazmaz; sayfa oturum sahibi çağıran değilse tahsilatı kapatır ve "kendi kasa oturumunuzu açın" der. Kasa rozeti (başlıktaki Kasa durumu) V1-RMD-1064 ve V1-RMD-1074 ile zaten aynı kaynaktan okunuyor; değişmez.

## Owned surface

- `evidence/V1-RMD-1088/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/CustomerAccounts/DualScreenApplication.CustomerAccounts.cs
  - src/Clients/Cashier/wwwroot/payments/customer-accounts/customer-accounts.js
  - tests/Host/Experience/CustomerAccounts/CustomerAccountsHttpTests.cs
  - tests/Clients/StaticApps/customer-accounts-flows.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (client-build-gate)

## Out of scope

- Kasa oturumu sayfası; PosTerminal kasa rozeti; kart ve cari borç yazma uçları

## Dependencies

- V1-RMD-1087

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
