# V1-RMD-900 - Fiş düğmesinde onaylı uyarı: ilk basım orijinaldir, sonrakiler KOPYA yazar

- Task ID: V1-RMD-900
- Status: InProgress
- Assignee: claude-code-session_019vXeYXYQSTiLW1J4UDYBY6
- Work type: implementation
- Surface state: Existing

## Goal

Ürün sahibi kararı (Semih): kasiyer ekranındaki "Fişi yazdır" düğmesi basmadan önce onaylı uyarı gösterir: "İlk basım orijinaldir, sonrakiler KOPYA yazar." Düğmeye ilk basış uyarıyı açar, ikinci basış ("Evet, yazdır") yazdırır; uyarıdan vazgeçilebilir. Fiş yazdırma yalnız Cashier ekranında vardır (React kasada karşılığı yok).

## Owned surface

- `evidence/V1-RMD-900/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-receipt.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Onaylı uyarı: ilk tık uyarıyı açar ve yazdırmaz, ikinci tık yazdırır, vazgeç uyarıyı kapatır
2. Mutasyon ve iki bağımsız qa incelemesi

## Out of scope

- Sunucu davranışı (orijinal/kopya kararı), React kasa (fiş düğmesi yok)

## Dependencies

- None

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
