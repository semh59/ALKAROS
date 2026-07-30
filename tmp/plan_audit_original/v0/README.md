# V0 - Validation and Architecture Closure

## Hedef

Kodlamadan önce domain, veri, mevzuat ve dış sistem belirsizliklerini kapatmak.

## Giriş koşulu

Master PDF ve bu plan erişilebilir olmalıdır.

## Çıkış kapısı

- Bu sürüm altındaki 32 görev dosyasının tamamı `Done`.
- Tüm `V0-DOM`, `V0-DAT`, `V0-ARC` ve `V0-CMP` görevleri `Done`.
- Hugin, QNB, Yemeksepeti, printer ve QR relay için gerçek sözleşme kanıtı var.
- Backup hedefi ve restore yolu gerçek ortamda doğrulandı.
- Açık kritik karar yok; migration dependency graph çevrimsiz veya açıkça iki
  aşamalı.

## Modüller

`backup-recovery`, `compliance`, `data-architecture`, `document-baseline`,
`domain-contracts`, `hugin-t300`, `licensing`, `meal-card`,
`platform-architecture`, `printing`, `qnb-esolutions`, `qr-relay`,
`yemeksepeti`.

Doğrulanan plan hacmi: 13 modül, 32 tek-sahip görev.
