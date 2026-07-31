# V0-BKP-002 - Approve RPO and RTO targets

- Task ID: V0-BKP-002
- Status: Done
- Assignee: codex-v0-bkp-002
- Work type: decision
- Surface state: Existing

## Source basis

- PDF:II.2.23
- PDF:III.25

## Goal

İşletmenin veri kaybı ve kesinti toleransını ölçülebilir RPO/RTO acceptance target değerlerine dönüştürmek.

## Owned surface

- `docs/recovery/rpo-rto-targets.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Kritik veri sınıfları, yerel/tesis dışı ritim, geri yükleme önceliği, sorumlu onaylayıcı ve ölçüm yöntemi.

## Out of scope

- Yedekleme uygulaması veya desteklenmeyen garanti.

## Dependencies

- V0-BKP-001

## Deliverables

- V0-BKP-002 için bağlayıcı karar dokümanı.
- Pozitif/negatif örnekler ve rejected alternatives.
- Tüketici görevler için test edilebilir invariant/output listesi.

## Acceptance evidence

- Hedefler, ölçülen V0 geri yükleme kanıtına göre sayısal olarak onaylanır; ölçülmemiş garantiler mevcut değildir.

## Handoff

- V15-BKP-001
- V15-BKP-002
- V20-DRL-001
