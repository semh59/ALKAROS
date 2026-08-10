# V14-OUI-001 - Build online order operations UI

- Task ID: V14-OUI-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:II.2.19
- PDF:II.7.4
- PDF:III.22

## Goal

Etki alanı komutlarını atlamadan QR bekleyen siparişler ve harici kanal siparişleri için yetkili personele bir
operasyonel kuyruk verin.

## Owned surface

- `src/Apps/BackOffice/OnlineOperations/**`, `tests/Apps/BackOffice/OnlineOperations/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Kuyruk filtreleri, kaynak/status görünürlüğü, kabul etme/reddetme/iptal etme eylemleri, eşleme hataları ve retry
  status.

## Out of scope

- Etki alanı geçişi uygulaması, kanal yapılandırması ve mutabakat çözümü.

## Dependencies

- V14-QRO-003
- V14-ONL-003
- V14-ONL-004
- V14-MAP-002
- V0-CMP-005

## Deliverables

- Rol korumalı işlemler arayüzü.
- Yetkilendirme, eşzamanlılık, eski komut ve hata sunumu testleri.

## Acceptance evidence

- Her kullanıcı eylemi, sahip olan contract modülünü çağırır ve kalıcı sonucunu gösterir; eski veya yetkisiz eylemler
  order'yi değiştiremez.
- Queue ve action akışları `docs/compliance/accessibility-target.md`'deki operations UI success kriterleri listesini
  karşılar.

## Handoff

- V14-REC-001
- V15-REC-001
