# V0-ARC-005 - Define settings ownership and secret classification

- Task ID: V0-ARC-005
- Status: Done
- Assignee: codex-v0-arc-005
- Work type: decision
- Surface state: Existing

## Source basis

- PDF:III.27

## Goal

Configurable değerleri module owner, scope, validation, history ve secret-storage yasağına göre sınıflandırmak.

## Owned surface

- `docs/architecture/settings-ownership.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- İş ayarları, cihaz/provider referansları, UI tercihleri, yeniden başlatma gereksinimleri ve değişiklik denetimi.

## Out of scope

- Gizli değerler, özellik uygulaması ve yönetici UI.

## Dependencies

- V0-ARC-001
- V0-CMP-003

## Deliverables

- V0-ARC-005 için tek decision record: kaynak + erişim tarihi + onaylayan + seçilen sonuç + reddedilen alternatifler +
  etkilenen task kimlikleri.
- Pozitif/negatif örnekler ve rejected alternatives.
- Tüketici görevler için test edilebilir invariant/output listesi.

## Acceptance evidence

- Bilinen her ayarın sahibi/tipi/kapsamı/varsayılan/doğrulaması vardır; kimlik bilgileri açıkça genel ayarlardan hariç
  tutulur.

## Handoff

- V1-SET-001
- V15-SEC-001
