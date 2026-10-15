# V1-RMD-545 - Sentetik sağlık sondası (kanarya)

- Task ID: V1-RMD-545
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

Sistem, gerçek kullanıcıdan önce sorunu görmek için her dakika kendi üzerinde betiklenmiş bir sipariş akışını dener ve süresini ölçer.

## Owned surface

- `plan/v1/remediation/V1-RMD-545-synthetic-canary.md`
- `evidence/V1-RMD-545/**`
- `src/Host/Observability/SyntheticProbes/**`
- `tests/Host/Observability/SyntheticProbes/**`
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Giriş, katalog, taslak sipariş aç-iptal et, sağlık ucu; tüm test verisi işaretli ve rapor/ciroya girmez.
- Başarısızlık veya yavaşlama hata olayı ve alarm olur.

## Out of scope

- Gerçek ödeme ve yazıcı çıktısı üretmek.

## Dependencies

- V1-RMD-533

## Acceptance evidence

- Her kabul koşulu için test ve mutasyon kanıtı; gerçek Postgres testi; çıktılar `evidence/V1-RMD-545/` altındadır.

## Handoff

- None
