# Restaurant POS Master Specification — source-bound baseline

> **Task:** V0-DOC-001
> **Status:** Done
> **Assignee:** codex-v0-doc-001
> **Work type:** decision
> **Source basis:** PDF:II.0-II.15, PDF:IV.0-IV.1, CORR:C1, CORR:C2, CORR:C3,
> CORR:C4, CORR:C5, CORR:C6, CORR:C7, CORR:C8, CORR:C9
> **Access date:** PDF source 2026-07-29; artifact verification 2026-08-02
> **Approver:** Semih — 2026-08-03
> **Decision type:** Document baseline decision (PDF-verified headings and
> corrections only)

## Status

Bu belge yalnız kaynak PDF'nin düzeltilebilir baseline'ıdır. Ürün gereksinimi,
işletme politikası veya provider davranışı eklemez; implementation ayrıntısı
uydurmaz. `V0-DOC-001` kapsamı PDF'de doğrulanmış heading/Correction
kayıtlarıyla sınırlıdır (C38 plan değişikliği, 2026-08-03).

## Verified PDF corrections

| Correction | Disposition | Owner | Status |
| --- | --- | --- | --- |
| `C1` | Migration-order forward FK — çözüm V0-DAT-001 kararında. | `V0-DAT-001` | Planned |
| `C2` | `NotReserved` `Draft/Submitted/PendingConfirmation` kapsar; katalogda tek tanım. | `V0-DAT-002` | Done |
| `C3` | `account_transactions.amount` pozitif magnitude + generated `direction`. | `V0-DOM-007` | Planned |
| `C4` | `payment_allocations` idempotency + cross-bill integrity; negatif allocation yok. | `V0-DOM-004` | Done |
| `C5` | QR `PendingConfirmation` → Table `Reserved`; `Accepted` → `Occupied`. | `V0-DOM-005` | Planned |
| `C6` | Settlement parent/child status aynı transaction'da güncellenir. | `V0-DAT-004` | Planned |
| `C7` | Beş discriminator sütunu için kanonik değer listeleri katalogda. | `V0-DAT-002` | Done |
| `C8` | `I.46` listesi 13 maddeden oluşur (14 değil). | `V0-DOC-001` | Done |
| `C9` | `waste_factor` sırası: recipe native unit'te çarpma, sonra unit conversion. | `V0-DOM-010` | Planned |

## Document-map correction

PDF'nin sayfa 2 haritası `II.0–II.16` gösterse de gerçek Part II içeriği
`II.15` ile biter. `II.16` yoktur (FIND-PDF-001); floor-map, zone etiketi veya
başka bir ürün gereksinimi bu hatadan türetilemez. Bu belgede `II.16` plan
gereksinimi olarak oluşturulmaz.

## Heading-count correction

IV.0/IV.1 heading sayımı FIND-IA-0004 ile 374 olarak doğrulandı;
`plan/VALIDATION_CONTRACT.md` aynı değeri kullanır (375 değeri düzeltildi,
C38). Doğrulama aracı 374 başlığı yeniden üretir.

## Open decision boundary

C1-C9 için yalnız sorun tanımı burada kayıtlıdır; çözüm içeriği yukarıdaki
sahip karar görevlerinin doğrulanmış record'larında bulunur. `Planned` durumlu
satırların kararları (C1, C3, C5, C6, C9) ilgili sahip görevler kapanmadan
GATE-V0-EXIT'te kabul edilmez. Bu belge bu görevlerin çözümlerini öne
çekmez ve onların yerine karar üretmez.

## Invariants for consumers

- Bu belgeden yeni product behavior türetilemez; yalnız kaynak bağı
  doğrulanabilir.
- `II.16` yoktur; heading sayısı 374'tür; `I.46` listesi 13 maddedir.
- C-satırlarının gerçek çözümleri sahip karar task'larında aranır.

## Affected tasks

- GATE-V0-EXIT (bu kayıtların kapanış denetimi).
- C-satırı sahipleri ve consumers (TRACEABILITY.md C1-C9 satırları).
