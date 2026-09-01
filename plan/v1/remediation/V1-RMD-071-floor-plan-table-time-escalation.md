# V1-RMD-071 - Floor plan table time-on-screen escalation

- Task ID: V1-RMD-071
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-08-31

## Goal

PosTerminal masa çalışma alanında dolu masalar için geçen süreyi yalnızca metin olarak göstermek yerine, belirli bir eşiği aşan masalarda görsel uyarı rengi eklemek. Süre hesaplaması zaten doğru; üzerine sarı ve kırmızı eşik renklendirmesi eklenir.

## Owned surface

- `plan/v1/remediation/V1-RMD-071-floor-plan-table-time-escalation.md`
- `src/Clients/PosTerminal/src/features/tables/**`
- `evidence/V1-RMD-071/**`

## In scope

- `elapsedLabel` çıktısının yanına eşik durumunu veren yardımcı bir fonksiyon; eşikler dosya sabiti olarak tanımlanır.
- Masa kartı alt bilgisi ve masa ayrıntısı geçen süre alanı için sarı ve kırmızı uyarı sınıfı uygulaması.
- Yalnızca dolu masalarda uyarı gösterilmesi; boş ve rezerve masalar etkilenmez.
- Eşik sınıflarını doğrulayan Vitest testi.

## Out of scope

- Masa domain modelini, API sözleşmesini veya rezervasyon davranışını değiştirmek.
- Mutfak bileti süre renklendirmesi; o `V1-RMD-069` kapsamındadır.

## Dependencies

- V1-RMD-070

## Deliverables

- Eşik aşımında sarı ve kırmızı uyarı rengi gösteren masa geçen süre göstergesi.

## Acceptance evidence

- `pnpm --dir src/Clients/PosTerminal typecheck`, `pnpm --dir src/Clients/PosTerminal test` ve `pnpm --dir src/Clients/PosTerminal build` sıfır çıkış kodu verir.
- Semih uzun süredir dolu bir masanın kırmızı, yeni açılmış masanın nötr renkte gösterildiğini doğrular.

## Handoff

- V1-RMD-072
