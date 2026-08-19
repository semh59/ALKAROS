# V1-SEC-003 - Remove database passwords from host command lines

- Task ID: V1-SEC-003
- Status: Done
- Assignee: opencode-v1-sec-003
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C35

## Goal

Host database parolasinin komut satiri veya usage metniyle verilmesini
engellemek; yalniz environment tabanli secret resolution kullanmak.

## Owned surface

- `tests/Host/MigrationComposition/Program/ProgramArgumentTests.cs`
- `evidence/V1-SEC-003/**`
- C52 migration secret-redaction execution test surface is transferred to V1-SEC-004; this historical task remains
  closed.
- C70 (2026-08-16) konsolidasyonu: host Program.cs dosyası konsolide remediasyon yüzeyine taşındı (V1-RMD-001);
  bu historical task closed kalır.

## In scope

- `--db-password` argumentini ve buna ait usage bilgisini kaldirmak.
- `ALKAROS_DB_PASSWORD` eksik veya bos oldugunda typed startup failure
  davranisini ve secret'in outputa yazilmadigini test etmek.

## Out of scope

- Production secret provisioning, external secret manager entegrasyonu,
  database URL semantics veya psql invocation mekanizmasi.

## Dependencies

- V0-GOV-003
- V1-FND-004
- V1-SEC-001

## Deliverables

- Komut satiri parolasi kabul etmeyen host parser'i ve ilgili testler.

## Acceptance evidence

- `--db-password` ile baslatma non-zero exit verir.
- Parola yalniz environment'tan okunur; usage, stdout, stderr ve exception
  metninde secret degeri gorunmez.

## Handoff

- V20-INS-001
