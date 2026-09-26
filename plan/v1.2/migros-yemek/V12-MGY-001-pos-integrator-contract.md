# V12-MGY-001 - Migros Yemek POS entegratörü başvurusu ve API sözleşmesi

- Task ID: V12-MGY-001
- Status: Blocked
- Assignee: Unassigned
- Work type: validation
- Surface state: Planned

## Source basis

- PO:2026-09-26

## Goal

Migros Yemek'in herkese açık teknik belgesi yok. Restoran panelinde "Restoran Bilgileri → POS Entegrasyonları"
bölümünde listelenen POS firmaları API anahtarıyla bağlanıyor. ALKAROS'un bu listeye girmesi ve API
belgesinin, test ortamının alınması.

## Owned surface

- `evidence/V12-MGY-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Migros Yemek'e POS entegratörü başvurusu ve belge/test ortamı erişimi.
2. Kimlik doğrulama, sipariş alma yolu (webhook ya da çekme), durumlar, iptal nedenleri ve menü sözleşmesinin gerçek test ortamıyla doğrulanması.

## Out of scope

- Kod; uygulama V12-MGY-002'de.

## Dependencies

- V12-GOV-006

## Blocker

- Migros Yemek teknik belgesi yalnız onaylı POS entegratörlerine veriliyor; başvuru ve onay yok.
- Görev ancak Migros Yemek başvuruyu onaylayıp belgeyi ve test erişimini verdiğinde `Planned` olur.

## Acceptance evidence

- İlgili test projeleri gerçek Postgres 18 üzerinde yeşil; mutasyon kontrolü `evidence/V12-MGY-001/` altında.
- `task_scope_tool.py --task-id V12-MGY-001 --diff-base <InProgress commit>` exit 0.
- Migros Yemek'in yazılı belgesi ve gerçek test ortamı çıktıları.

## Handoff

- None
