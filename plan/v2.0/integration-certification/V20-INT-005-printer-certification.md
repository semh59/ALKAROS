# V20-INT-005 - Certify printer models

- Task ID: V20-INT-005
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Planned

## Source basis

- PDF:I.6.6
- PDF:I.16-I.17

## Goal

Onaylanan her yazıcı modelini ve aktarımını yönlendirme, kodlama, kağıt hatası ve retry davranışı açısından onaylayın.

## Owned surface

- `release/evidence/integrations/printers/**`
- Bu görev printing implementation kodunu değiştiremez.

## In scope

- Model/ürün yazılımı/aktarım envanteri, Türkçe karakterler, kes/besle, bağlantıyı kes, kağıttan çıkar, yeniden başlat,
  retry ve fiziksel kopya gözlemi.

## Out of scope

- Yazıcı sürücüsü uygulaması, mutfak bileti içerik kuralları ve mali cihazlar.

## Dependencies

- V0-PRN-001
- V1-KIT-002
- V1-KIT-003
- V1-KIT-004

## Deliverables

- Fiziksel cihaz matrisi, örnek çıktılar ve retry gözlem günlüğü.

## Acceptance evidence

- Etkinleştirilen her model, okunabilir yönlendirilmiş çıktı üretir ve arıza/retry davranışı, belgelenen en az bir kez
  sınırlama ve operatör kurtarma prosedürüyle eşleşir.

## Handoff

- V20-UAT-001
- V20-GAT-002
