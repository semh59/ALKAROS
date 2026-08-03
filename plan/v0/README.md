# V0 - Validation and Architecture Closure

## Hedef

Kodlamadan önce domain, veri, mevzuat ve dış sistem belirsizliklerini kapatmak.

## Giriş koşulu

`GATE-V0-ENTRY` koşulları sağlanmalıdır.

## Çıkış kapısı

- Tüm V0 görevleri gerçek kanıtla `Done` veya tarihli/onaylı `NotApplicable` olur;
  açık `Blocked` görev kalmaz.
- Tüm `V0-DOM`, `V0-DAT`, `V0-ARC`, `V0-CMP`, recovery ve dış-sözleşme görevleri
  karar kaynakları ve named approver kanıtıyla kapanır.
- Hugin, QNB, Yemeksepeti, meal-card, printer ve QR relay görevleri gerçek
  sandbox/device/contract kanıtı olmadan V0 çıkışını geçiremez.
- Backup tool path'i disposable PostgreSQL 18 üzerinde doğrulanmış, RPO/RTO
  hedefi karar kaydına bağlanmıştır; application restore kanıtı V1.5'e aittir.
- Açık kritik karar yok; migration dependency graph çevrimsiz veya açıkça iki
  aşamalı.

## Modüller

`backup-recovery`, `compliance`, `data-architecture`, `document-baseline`,
`domain-contracts`, `governance`, `hugin-t300`, `licensing`, `meal-card`,
`platform-architecture`, `printing`, `qnb-esolutions`, `qr-relay`,
`security-baseline`, `yemeksepeti`.

Doğrulanan plan hacmi: 16 modül, 57 tek-sahip görev.
