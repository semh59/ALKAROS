# V1-RMD-1063 - Kasa oturumu sayfası oturum sahibini adıyla söyler ve başkasının oturumunda kimin kapatacağını yazar

- Task ID: V1-RMD-1063
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Terminalde başka kullanıcının açık kasa oturumu varken kasa oturumu sayfası oturumu açan kişi yerine giriş yapanın adını yazıyor, kasiyer oturumu kapatamayınca ne yapacağını öğrenemiyor ve yönetici girişi terminal başka tarayıcıya bağlıyken (TERMINAL_KEY_MISMATCH) "Bu işlem için yetkiniz yok" diye yanlış açıklanıyor. Sunucu oturum sahibinin adını veren bir uç nokta sunar (kasa çekmecesi yetkisi ister); sayfa başlıkta oturum sahibinin adını yazar, oturum başkasınındaysa sahibin adını ve "oturumu açan kasiyer ya da bir yönetici kapatmalı" cümlesini gösterir, yetkisiz kullanıcının sayım ve kapatma düğmesini nedeniyle birlikte pasif tutar, yöneticiye kendi yetkisiyle kapatabildiğini söyler ve terminal anahtarı uyuşmazlığı doğru Türkçe cümleyle anlatılır. Backend yetki kuralı (sahip ya da cash.session.override) değişmez.

## Owned surface

- `evidence/V1-RMD-1063/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/DualScreenApplication.CashSession.cs
  - src/Host/DualScreen/DualScreenStore.cs
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
  - tests/Host/Experience/CashSession/CashCloseSafetyHttpTests.cs
  - tests/Clients/StaticApps/cash-session-owner.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. Sunucu testi (CashCloseSafetyHttpTests) ve tests/Clients/StaticApps tam koşusu

## Out of scope

- Listede olmayan ekranlar ve dosyalar; üst başlıktaki "Kasa" rozeti ve tahsilat sayfası metni (V1-RMD-1064)

## Dependencies

- V1-RMD-1062

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. Host CashSession test projesi exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
