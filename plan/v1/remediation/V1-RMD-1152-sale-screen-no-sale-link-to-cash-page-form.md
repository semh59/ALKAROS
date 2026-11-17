# V1-RMD-1152 - Satış ekranında "Satış yok" bağlantısı: kasa sayfasındaki gerekçe formuna götürür, geldiği yere döner

- Task ID: V1-RMD-1152
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Ürün sahibi kararı (Semih, 2026-11-18, V1-RMD-1150 madde 2): satış ekranında "Satış yok" bağlantısı olur ve kasa sayfasındaki gerekçe formuna götürür; form satış ekranına konmaz (sunucu gerekçeyi ve çekmece vuruşundan önce denetim kaydını zorunlu tutar, bu güvence değişmez).

1. PosTerminal üst çubuğunda kasiyerin kendi kasa oturumu açıkken ve `cash.drawer.kick` yetkisi varken "Satış yok" bağlantısı görünür; kasa sayfasına `returnTo` (geldiği yer) ve `open=no-sale` ile gider.
2. Kasa sayfası `open=no-sale` ile açılınca satışsız açma formunu doğrudan gösterir; form kullanılamıyorsa (çekmece kapalı, yetki yok, oturum başkasında) Türkçe bir bilgi mesajı gösterir ve normal sayfa açılır.
3. Kasa sayfasının geri bağlantısı satış ekranından gelindiğinde "Satış ekranına dön" der (şimdi "Hesaba dön" yazar).
4. Sunucu sözleşmesi değişmez.

## Owned surface

- `evidence/V1-RMD-1152/**`
- `src/Clients/PosTerminal/src/routes/Cashier.no-sale-link.test.tsx`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
  - tests/Clients/StaticApps/cash-session-no-sale-shortcut.test.js
  - tests/Clients/StaticApps/cash-session-return-to.test.js
  - src/Clients/PosTerminal/src/features/cash-session-status/CashSessionStatus.tsx
  - src/Clients/PosTerminal/src/features/cash-session-status/CashSessionStatus.test.tsx
  - src/Clients/PosTerminal/src/features/cash-session-status/cashSessionStatusApi.ts
  - src/Clients/PosTerminal/src/strings.ts
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. PosTerminal ve istemci testleri

## Out of scope

- Satışsız açma formunun satış ekranına konması; sunucu tarafı; "Satış yok" sonrası otomatik yönlendirme

## Dependencies

- V1-RMD-1149

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi
3. PosTerminal ve tests/Clients/StaticApps testleri ile istemci üretim derlemesi exit code 0

## Handoff

- None
