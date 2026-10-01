# V1-RMD-481 - Denetim kayıtlarının 10 yıl sonra bölüm bırakılarak silinmesi

- Task ID: V1-RMD-481
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-01

## Goal

`audit.audit_events` tablosu veritabanı tetikleyicisiyle yalnız eklemeye açıktır; satır güncellenemez ya da silinemez. KVKK veri envanteri denetim kayıtları için
10 yıl sonra anonimleştirmeyi öngörür, bu yüzden yerinde anonimleştirme mümkün değildir. `kvkk-retention` komutu ve `V15-KVK-002` bu tabloyu kapsam dışı bırakır.
Bu görev tabloyu yıla göre bölümlemeyi ve 10 yılı dolan bölümü bırakmayı (tüm bölümü silmeyi) tasarlar ve uygular. Tasarım, mevcut tetikleyici korumasını
zayıflatmamalı ve yasal saklama gerektiren kayıtları saklama süresinden önce silmemelidir.

## Owned surface

- `plan/v1/remediation/V1-RMD-481-audit-log-ten-year-disposal.md`

## In scope

- Kesin yollar ve kapsam görev başlatılırken bu bölümde yazılır.

## Out of scope

- Denetim kayıtlarının içeriğinin değiştirilmesi; `V15-KVK-002` alanları.

## Dependencies

- V15-KVK-002

## Acceptance evidence

- Testler ve gerçek Postgres denemesi; çıktılar `evidence/V1-RMD-481/` altındadır.

## Handoff

- None
