# V0-DOM-006 - Define void complimentary and discount policy

- Task ID: V0-DOM-006
- Status: Done
- Assignee: codex-v0-dom-006
- Work type: decision
- Surface state: Existing

## Source basis

- PDF:II.2.5
- PDF:II.3.3
- PDF:II.5.2
- PDF:III.7

## Goal

Void, complimentary, discount, waste ve refund davranışlarını actor, approval, tax ve audit etkileriyle ayrı ayrı
tanımlamak.

## Owned surface

- `docs/domain/void-complimentary-discount-policy.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Mutfak/payment durumuna, neden kataloğuna, onay eşiklerine, bill/order etkilerine ve mali sonuçlara göre uygunluk.

## Out of scope

- Kampanya motoru, sadakat ve provider promosyonları.

## Dependencies

- V0-CMP-002
- V0-DOM-003

## Deliverables

- V0-DOM-006 için bağlayıcı karar dokümanı.
- Pozitif/negatif örnekler ve rejected alternatives.
- Tüketici görevler için test edilebilir invariant/output listesi.

## Acceptance evidence

- Aynı öğe, birbiriyle çelişen işlemlere göre sınıflandırılamaz; Her sıfır/negatif fiyat etkisinin bir yetkisi ve
  denetim kuralı vardır.

## Handoff

- V1-ORD-003
- V1-BIL-003
