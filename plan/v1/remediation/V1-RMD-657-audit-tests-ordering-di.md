# V1-RMD-657 - Denetim modülü test güçlendirme, okuma sıralaması ve DI ömrü

- Task ID: V1-RMD-657
- Status: InProgress
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Denetim kaydı 10 yıl saklanır (Ürün Sahibi kararı 2026-10-20). V1-RMD-595 bulguları F15, F16, F18 ve F21 kapanır. F15: modül test veritabanı yalnız bölümsüz şemayı (015) kuruyordu, üretim bölümlü (173); testler artık 173'ü de uygular. F16: alan gidiş-dönüş, sıralama, 5000 sınırı, tür süzgeci, toplu yazımın temizlenmesi ve atomikliği, tireli anahtar temizliği ve kayıtçı kaydı testle korunur. F18: `AuditModule` mağaza ve temizleyiciyi tekil (singleton) kaydeder (Host'taki dört `TryAddSingleton` ile uyumlu), kayıt testi eklenir. F21: okuma sıralaması belirleyici olur (`occurred_at, id`).

## Owned surface

- `plan/v1/remediation/V1-RMD-657-audit-tests-ordering-di.md`
- `evidence/V1-RMD-657/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-656-audit-shared-writer-module-sites.md - yalnız Handoff satırı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Audit/AuditModule.cs - yalnız kayıt ömrü
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Audit/EventStore/PostgresAuditEventStore.cs - yalnız okuma sıralaması
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Audit/EventStore/ALKAROS.Audit.EventStore.Tests.csproj - yalnız 173 göçü ve AuditModule derleme girdisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Audit/EventStore/AuditTests.cs - yalnız yeni testler
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Audit/EventStore/AuditSanitizerTests.cs - yalnız yeni testler
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Yukarıdaki dört bulgunun kod ve test değişiklikleri; test veritabanında bölümlü şema.

## Out of scope

- Belge ve küçük tutarsızlık bulguları (F22, F23, F25, F26) ayrı görevde.

## Dependencies

- V1-RMD-656

## Acceptance evidence

1. Her yeni test, ilgili mutantı yakalar (mutasyon kanıtı); eski testlerle birlikte hepsi bölümlü şemada başarılı.
2. Mutasyon kanıtı ve çıktılar `evidence/V1-RMD-657/` altındadır.

## Handoff

- None
