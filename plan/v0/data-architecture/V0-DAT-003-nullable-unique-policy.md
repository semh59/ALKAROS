# V0-DAT-003 - Define nullable uniqueness policy

- Task ID: V0-DAT-003
- Status: Done
- Assignee: codex-v0-dat-003
- Work type: decision
- Surface state: Existing

## Source basis

- PDF:II.0-II.1
- PDF:III.0-III.2
- PDF:II.13-II.15
- PDF:III.29-III.40

## Goal

Nullable kolon içeren tüm unique kurallar için PostgreSQL uyumlu tek enforcement yaklaşımı seçmek.

## Owned surface

- `docs/data/nullable-unique-policy.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Zone-table number, stock balance location ve diğer nullable composite unique alanlar.

## Out of scope

- PaymentAllocation dışındaki domain davranışlarını yeniden tasarlamak.

## Dependencies

- V0-DAT-001

## Deliverables

- V0-DAT-003 için bağlayıcı karar veya contract dokümanı.
- En az iki pozitif ve iki negatif örnek.
- Tüketici görevler için açık input/output ve invariant listesi.

## Acceptance evidence

- Her nullable unique için `NULLS NOT DISTINCT`, partial index veya NOT NULL kararı ve gerekçesi var.

## Handoff

- None
