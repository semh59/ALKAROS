# V12-QRT-004 - Relay setup guide for the platform operator

- Task ID: V12-QRT-004
- Status: Done
- Assignee: claude-session-01GGsiy81vQBnzkRKVfPsjMC
- Work type: documentation
- Surface state: Existing

## Source basis

- PO:2026-09-07

## Goal

Semih'in "Benim kendi domain ihtiyacım yok değil mi, peki ön rehber var
mı" sorusuna kalıcı bir cevap yazmak: ALKAROS için bir kerelik yapılacak
adımlar (domain, Cloudflare hesabı, API token, şifreleme anahtarı) ile
`/settings/relay`'den yapılacak adımı ve hâlâ otomatikleşmemiş kısmı
(`V12-QRT-001`) netçe ayırmak.

## Owned surface

- `docs/operations/qr-relay-setup-guide.md` (yeni)

## In scope

- Bir kerelik ALKAROS kurulum adımları (domain, Cloudflare API token
  kapsamı, `ALKAROS_SECRET_ENVELOPE_MASTER_KEY` üretimi).
- `/settings/relay` üzerinden token girişi (V12-QRT-003, bugün hazır).
- Restoran-başına otomasyonun (`V12-QRT-001`) henüz hazır olmadığının
  açık kaydı.

## Out of scope

- `V12-QRT-001`'in kendisi.

## Dependencies

- V12-QRT-003

## Deliverables

- `docs/operations/qr-relay-setup-guide.md`.

## Acceptance evidence

- Rehber, restoranın kendi domain/Cloudflare hesabına ihtiyacı olmadığını
  ve bugünkü test domain'inin (V0-QRG-001) production'da kullanılmayacağını
  açıkça yazar.
- `ALKAROS_SECRET_ENVELOPE_MASTER_KEY`'in gerçek üretim komutunu
  (`openssl rand -base64 32`) ve kaybolursa ne olacağını içerir.
- `python tools/plan-audit/plan_audit_tool.py validate`: sıfır hata.

## Handoff

- None
