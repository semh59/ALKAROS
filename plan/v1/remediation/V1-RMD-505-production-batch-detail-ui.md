# V1-RMD-505 - Üretim partisi ayrıntı ekranı

- Task ID: V1-RMD-505
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-01

## Goal

Sunucuda üretim partisinin tüketimlerini (hangi stok kaleminden ne kadar düşüldü), çıktılarını ve fire kayıtlarını okuma uçları hazırdır; arayüz partiyi yalnız listeler ve durumunu değiştirir. Yönetici bir partinin stoğa gerçekte ne yaptığını göremez.

## Owned surface

- `plan/v1/remediation/V1-RMD-505-production-batch-detail-ui.md`
- `evidence/V1-RMD-505/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-purchasing-production/ProductionSection.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-purchasing-production/ProductionSection.test.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-purchasing-production/api.ts
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-purchasing-production/models.ts
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/strings.ts
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Parti satırında "Ayrıntı": tüketimler (stok kalemi adı, miktar, birim, fire çarpanı), çıktılar ve partiye bağlı fire kayıtları.
- Hata kodları Türkçe metne çevrilir, ham sunucu metni gösterilmez.

## Out of scope

- Sunucu uçlarının değiştirilmesi; partinin tarif sürümünü değiştirme ucu (`recipe-version`) ekranı.

## Dependencies

- V1-RMD-114

## Acceptance evidence

- Testler ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-505/` altındadır.

## Handoff

- None
