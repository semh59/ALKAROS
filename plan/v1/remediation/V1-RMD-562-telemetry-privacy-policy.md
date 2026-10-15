# V1-RMD-562 - İzleme gizlilik politikası değişikliği

- Task ID: V1-RMD-562
- Status: Done
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

İzleme verisinde kişisel ve ödeme bağlamı ile hata kodları geçebilir; kart numarası, parola, token ve gizli anahtar hiçbir zaman saklanmaz.

## Owned surface

- `plan/v1/remediation/V1-RMD-562-telemetry-privacy-policy.md`
- `evidence/V1-RMD-562/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Observability/TelemetryPrivacy/TelemetryPrivacyGuard.cs - yalnız politika değişikliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Observability/TelemetryPrivacy/TelemetryPrivacyTests.cs - yalnız politika testleri
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- E-posta, telefon, IBAN ve kimlik numarası artık maskelenmez; kart numarası (Luhn), Bearer/JWT ve parola/token/gizli anahtar alanları saklanmaz.
- İzin listesi yerine yasak alan adı listesi.

## Out of scope

- Telegram mesajı (sabit künye kalır), dinleyicilerin kendisi.

## Dependencies

- V1-RMD-549

## Acceptance evidence

1. Kişisel veri örnekleri geçer, kart/token/parola örnekleri maskelenir veya düşer (test).
2. Çıktılar `evidence/V1-RMD-562/` altındadır.

## Handoff

- None
