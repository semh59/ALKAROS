# V0-MCD-001 - Validate meal-card provider contract

- Task ID: V0-MCD-001
- Status: Blocked
- Assignee: Unassigned
- Work type: validation
- Surface state: Planned

## Source basis

- PDF:I.6
- PDF:I.6.2
- PDF:II.2.14
- PDF:II.3.10
- PDF:II.5.10
- PDF:III.17
- EXT:GIB-TK2-4.0

## Goal

Desteklenecek meal-card provider'larını belirlemek ve payment, cancellation/refund, commission, statement ve settlement
contract'larını doğrulamak.

## Owned surface

- `evidence/v0/integrations/V0-MCD-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Provider erişimi, kimlik bilgisi modeli, işlem kimliği, bilinmeyen durum, ekstre formatı, ödeme süresi ve ücretler.

## Out of scope

- Production adapter yazmak veya private provider contract kanıtı olmadan provider seçmek.

## Dependencies

- V0-DOM-003
- V0-ARC-003

## Blocker

- İncelenecek provider adayları, imzalı provider contract, credential ve sandbox veya cihaz erişimi çalışma alanında
  yoktur; approved provider listesi bu görevin çıktısıdır.
- Görev ancak provider adayları iş sahibi tarafından adlandırıldığında ve her aday için contract ile kullanılabilir
  sandbox/cihaz erişimi sağlandığında `Planned` durumuna alınabilir. Gerçek transcript'ler `Done` acceptance kanıtıdır.

## Deliverables

- V0-MCD-001 için tarihli resmi/business evidence paketi.
- Desteklenen ve desteklenmeyen kapsam listesi.
- Doğrulanamayan madde için blocker; varsayımsal sonuç yok.

## Acceptance evidence

- Onaylanmış en az bir provider'ın resmi contract/sandbox kanıtı vardır; bilinmeyen/kullanılamayan provider'lar açıkça
  desteklenmemektedir.

## Handoff

- V12-MCD-001
- V12-MCD-002
- V12-MCD-003
