# V14-GOV-001 - Admit NFC channel and clarify GATE-V14-ENTRY scope

- Task ID: V14-GOV-001
- Status: Done
- Assignee: claude-session-01GGsiy81vQBnzkRKVfPsjMC
- Work type: decision
- Surface state: Existing

## Source basis

- PO:2026-09-07

## Goal

V1.4 kapsamına, harici sözleşme (Yapı Kredi provider, QR public relay)
bağımlılığı taşımayan bir NFC self-check-in kanalını yeni bir modül olarak
eklemek; `GATE-V14-ENTRY`'nin `plan/v1.4/README.md`'deki blok koşulunun
yalnız harici sözleşmeye gerçekten bağımlı modülleri kapsadığını, NFC gibi
bağımsız modülleri kapsamadığını açıkça kaydetmek.

## Owned surface

- `plan/v1.4/governance/V14-GOV-001-admit-nfc-channel-and-clarify-entry-gate.md`
- `plan/v1.4/README.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- `nfc-ordering` modülünün üst-seviye kapsamı ve bağımlılıkları
  (yalnızca `Done` olan `V1-ORD-001`, `V1-ORD-002`, `V1-TBL-001`).
- `plan/v1.4/README.md`'nin "Giriş koşulu" ve "Modüller" bölümlerinin
  güncellenmesi.

## Out of scope

- NFC implementasyon kodu (`V14-NFC-001`).
- `qr-*`, `online-ordering`, `channel-mapping`, `customer-web`,
  `reconciliation`, `reporting`, `shared-stock` modüllerinin kapsamı veya
  bağımlılığı — değişmedi, hâlâ `GATE-V14-ENTRY`'ye tabidir.

## Dependencies

- V1-ORD-001
- V1-ORD-002
- V1-TBL-001

## Onay

Approved by Semih — Founder/Product Owner — 2026-09-07 (bugünkü konuşma:
NFC + QR hibrit kanal kararı, QR'ın garson onayına bağlı kalması, NFC'nin
doğrudan/onaysız kabul edilmesi).

## Deliverables

- `plan/v1.4/README.md`'de: `nfc-ordering` modülü "Modüller" listesine
  eklendi; "Giriş koşulu" NFC'yi açıkça istisna olarak adlandıracak şekilde
  yeniden yazıldı.

## Acceptance evidence

- `plan/v1.4/README.md`, `GATE-V14-ENTRY` blok koşulunun yalnız
  `V0-YSP-001`/`V0-QRG-001`'e gerçekten bağımlı modülleri kapsadığını ve
  `nfc-ordering`'in bu koşuldan muaf olduğunu adıyla yazar.
- `qr-ordering`, `qr-security`, `qr-transport`, `online-ordering`,
  `channel-mapping`, `customer-web`, `reconciliation`, `reporting`,
  `shared-stock` modüllerinin task dosyalarında hiçbir değişiklik yok.

## Handoff

- V14-NFC-001
