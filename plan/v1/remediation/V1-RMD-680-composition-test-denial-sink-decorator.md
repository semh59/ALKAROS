# V1-RMD-680 - Composition testi: yetkisiz erişim olay alıcısı dekoratörü

- Task ID: V1-RMD-680
- Status: InProgress
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-21

## Goal

`ServeContainerResolvesEveryModuleServiceFromTheModuleCatalog` kırmızıdır: Host, `IDenialEventSink` kaydını güvenlik dedektörü dekoratörüyle sarar (bilinçli), ama testin onaylı Host dekoratörleri listesinde bu yoktur. Liste, sarmalayıcıyı onaylayacak biçimde tamamlanır; test yeşil olur.

## Owned surface

- `plan/v1/remediation/V1-RMD-680-composition-test-denial-sink-decorator.md`
- `evidence/V1-RMD-680/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Composition/ProductionExperienceCompositionTests.cs - yalnız onaylı dekoratör listesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-679-decision-screen-names.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Onaylı dekoratör listesine `IDenialEventSink` → `ObservingDenialEventSink` girişi.

## Out of scope

- Audit EventStore ve ServerEndToEnd'deki ayrı kırmızı testler (ayrı inceleme).

## Dependencies

- V1-RMD-679

## Acceptance evidence

1. Composition test projesi yeşil; liste girişi silinince test kırmızıya döner (mutasyon).
2. `evidence/V1-RMD-680/` altında çıktılar.

## Handoff

- V1-RMD-598
