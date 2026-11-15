# V1-RMD-1110 - Kasa kapanışında sayımı düzeltme ve hazır fark gerekçeleri

- Task ID: V1-RMD-1110
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer deneyimi turu: kapanış fark teyidi ekranında yanlış yazılan sayım düzeltilemiyor ve fark açıklaması boş kutuya elle yazılıyor. Sunucu, sayım aşamasında yeni sayım kaydını kabul eder ve kapanışta en son sayımı kullanır (CashSessionPolicy.ValidateCanRecordCount ve sayım deposu, kodla doğrulandı). Bu yüzden fark teyidi ekranına "Sayımı düzelt" eklenir (sayım ekranına döner, önceki tutar yazılı gelir) ve fark açıklamasının altına hazır gerekçe düğmeleri gelir (dokunuş metni kutuya yazar; kasiyer düzenleyebilir; yönetici onay akışı ve sunucu sözleşmesi değişmez).

## Owned surface

- `evidence/V1-RMD-1110/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
  - tests/Clients/StaticApps/cash-session-close-recount.test.js
  - tests/Clients/StaticApps/cash-session-close.test.js
  - tests/Clients/StaticApps/cash-session-text-size.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (client-build-gate)

## Out of scope

- Sunucu sözleşmesi; tolerans kuralı; kapanış sonrası ekranı

## Dependencies

- V1-RMD-1109

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
