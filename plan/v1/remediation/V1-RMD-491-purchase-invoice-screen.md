# V1-RMD-491 - Alış faturaları yönetim ekranı

- Task ID: V1-RMD-491
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-01

## Goal

Yönetim ekranına "Alış faturaları" bölümü: XML yükleme, taslak listesi, satır satır hammadde eşleştirme (çevrim katsayısıyla), onay ve reddet. Tüm metinler Türkçe.

## Owned surface

- `plan/v1/remediation/V1-RMD-491-purchase-invoice-screen.md`

## In scope

- Kesin yollar görev başlatılırken yazılır. Bölüm `purchasing.manage` yetkisiyle `management/sections.ts` içine eklenir; eşleştirilmemiş satır sayısı görünür, onay yalnız tüm satırlar eşleşince açılır; onayda giriş yapılacak depo/konum yönetici tarafından seçilir.

## Out of scope

- Backend (`V1-RMD-489`, `V1-RMD-490`); QNB gelen kutusu.

## Dependencies

- V1-RMD-490

## Acceptance evidence

- Testler, mutasyon kanıtı ve gerçek Host denemesi; çıktılar `evidence/V1-RMD-491/` altındadır.

## Handoff

- V1-RMD-492
