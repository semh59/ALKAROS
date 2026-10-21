# V1-RMD-681 - Sunucu uçtan uca testi: istemci hatası olay üretmemeli

- Task ID: V1-RMD-681
- Status: InProgress
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-21

## Goal

`RealClientErrorProducesNoEvent` kırmızıdır: 404 yanıtından sonra hata olayı tablosunda bir kayıt vardır. Kök neden bulunur; yanlış olay yazan kod ya da testin kendisi düzeltilir ve test yeşil olur.

## Owned surface

- `plan/v1/remediation/V1-RMD-681-server-end-to-end-client-error-test.md`
- `evidence/V1-RMD-681/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Observability/ServerEndToEnd/ServerEndToEndTests.cs - yalnız bu test
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Observability/ServerDetectors/ServerDetectors.cs - yalnız kök neden gerektirirse
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-680-composition-test-denial-sink-decorator.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Kırmızı testin kök nedeni ve düzeltmesi.

## Out of scope

- Audit EventStore testi: tek başına ve proje bazında yeşil (106/106); tüm çözüm koşusundaki kırmızı paylaşılan veritabanı çakışmasıdır, ayrı iş.

## Dependencies

- V1-RMD-680

## Acceptance evidence

1. ServerEndToEnd projesi yeşil; mutasyon kanıtı.
2. `evidence/V1-RMD-681/` altında çıktılar.

## Handoff

- V1-RMD-598
