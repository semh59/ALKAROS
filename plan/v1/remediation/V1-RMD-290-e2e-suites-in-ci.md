# V1-RMD-290 - Cashier ve WaiterPwa E2E paketleri CI'da koşar

- Task ID: V1-RMD-290
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-25

## Goal

Cashier (33) ve WaiterPwa (43) E2E senaryoları yalnızca yerelde koşuyor; bu yüzden CI yeşil olsa da kasa ve garson akışlarını uçtan uca bozan bir değişiklik fark edilmez. `.github/workflows/task-scope.yml`'e (ya da ayrı bir işe) UTF8 Postgres 18, Host ve PosTerminal derlemesi, Playwright tarayıcıları ve iki paketin koşusu eklenir; başarısız testte rapor ve izler artifact olarak yüklenir.

## Owned surface

- `plan/v1/remediation/V1-RMD-290-e2e-suites-in-ci.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): .github/workflows/task-scope.yml
  (yalnız E2E işi)

## In scope

1. E2E işi, artifact yükleme, kilitli araç sürümleri, çalışma süresi sınırı.

## Out of scope

- Yeni E2E senaryoları yazmak (E2E ana planının 4–6. fazları).

## Dependencies

- V13-RMD-003

## Acceptance evidence

- CI koşusu 36160379565 (master, `e2e` işi): Cashier 33/33, WaiterPwa 43/43 yeşil; aynı koşuda PosTerminal 201 test ve Python mimari testleri 215 geçti.
- Kırmızı yol: `ci-e2e-red-check` dalında Cashier 01 spec'ine bilerek başarısız doğrulama eklendi; koşu 36168831021'de `e2e` işi failure (1 failed, 32 passed). Dal silindi.
- Başarısız koşuda rapor ve izler `e2e-reports` artifact'ı olarak yüklenir (14 gün).
- `plan_audit_tool.py validate` temiz.

## Handoff

- None
