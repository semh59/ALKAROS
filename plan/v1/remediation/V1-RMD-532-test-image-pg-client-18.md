# V1-RMD-532 - Test imajında PostgreSQL 18 istemcisi

- Task ID: V1-RMD-532
- Status: InProgress
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-15

## Goal

Docker test imajındaki `pg_dump`/`psql` istemcisi test veritabanı sunucusuyla (PostgreSQL 18) aynı ana sürümde olur; yedek-geri yükleme tatbikatı testi ortam yüzünden düşmez.

## Owned surface

- `plan/v1/remediation/V1-RMD-532-test-image-pg-client-18.md`
- `evidence/V1-RMD-532/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): deploy/docker/Dockerfile - yalnız `test` aşamasındaki istemci kurulumu

## In scope

- `test` aşamasında PostgreSQL 18 istemcisi (PGDG deposu); üretim aşamaları değişmez.

## Out of scope

- Üretim imajları, compose dosyaları.

## Dependencies

- None

## Acceptance evidence

- SecurityAdministration test projesi Docker'da 29/29; çıktılar `evidence/V1-RMD-532/` altındadır.

## Handoff

- None
