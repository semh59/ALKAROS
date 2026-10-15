# V1-RMD-546 - Veritabanı derin izleme

- Task ID: V1-RMD-546
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

En yavaş sorgular, kilitler, bağlantı doluluğu ve tablo şişmesi veritabanının kendi istatistiklerinden okunarak izlenir.

## Owned surface

- `plan/v1/remediation/V1-RMD-546-postgres-deep-monitoring.md`
- `evidence/V1-RMD-546/**`
- `src/Modules/Observability/PostgresInsight/**`
- `tests/Modules/Observability/PostgresInsight/**`
- `database/migrations/V1/V1-RMD-546/**`
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- `pg_stat_statements` ile en yavaş ve en sık sorgular, kilit zincirleri, bekleyen işlemler, ölü satır oranı.
- Eklentinin açılması dağıtım yapılandırmasında ayrı ve açık bir adımdır.

## Out of scope

- Yok.

## Dependencies

- V1-RMD-533

## Acceptance evidence

- Her kabul koşulu için test ve mutasyon kanıtı; gerçek Postgres testi; çıktılar `evidence/V1-RMD-546/` altındadır.

## Handoff

- None
