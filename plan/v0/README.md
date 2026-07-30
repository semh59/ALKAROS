# V0 - Validation and Architecture Closure

## Hedef

Kodlamadan önce domain, veri, mevzuat ve dış sistem belirsizliklerini kapatmak.

## Giriş koşulu

`GATE-V0-ENTRY` koşulları sağlanmalıdır.

## Çıkış kapısı

- Uygulanabilir V0 görevleri `Done`; private kanıt bekleyen görevler açık
  `Blocked` ve bunları tüketen implementation görevleri başlamamış olmalıdır.
- Tüm `V0-DOM`, `V0-DAT`, `V0-ARC` ve `V0-CMP` görevleri `Done`.
- Hugin, QNB, Yemeksepeti, meal-card, printer ve QR relay görevleri ya gerçek kanıtla
  `Done` ya da açık kaldırılma koşuluyla `Blocked` durumundadır.
- Backup tool path'i disposable PostgreSQL 18 üzerinde doğrulanmış, RPO/RTO
  hedefi karar kaydına bağlanmıştır; application restore kanıtı V1.5'e aittir.
- Açık kritik karar yok; migration dependency graph çevrimsiz veya açıkça iki
  aşamalı.

## Modüller

`backup-recovery`, `compliance`, `data-architecture`, `document-baseline`,
`domain-contracts`, `hugin-t300`, `licensing`, `meal-card`,
`platform-architecture`, `printing`, `qnb-esolutions`, `qr-relay`,
`security-baseline`, `yemeksepeti`.

Doğrulanan plan hacmi: 14 modül, 42 tek-sahip görev.
