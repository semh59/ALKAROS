# V1-RMD-503 - Tedarikçi bilgisi düzenleme ekranı

- Task ID: V1-RMD-503
- Status: InProgress
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-01

## Goal

Sunucuda tedarikçi ayrıntısı okuma ve güncelleme uçları hazırdır; arayüz tedarikçiyi yalnız oluşturur ve etkin/pasif yapar. Yanlış girilmiş vergi numarası, telefon veya e-posta düzeltilemez. Yönetici tedarikçi bilgisini ekrandan güncelleyebilmelidir.

## Owned surface

- `plan/v1/remediation/V1-RMD-503-supplier-edit-ui.md`
- `evidence/V1-RMD-503/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-purchasing-production/PurchasingSection.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-purchasing-production/PurchasingSection.test.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-purchasing-production/api.ts
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-purchasing-production/models.ts
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/strings.ts
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Tedarikçi satırında "Düzenle": ayrıntıyı sunucudan okuyup ad, vergi numarası, vergi dairesi, telefon ve e-postayı güncelleme.
- Boş alan boş değer olarak gönderilir; hata kodları Türkçe metne çevrilir, ham sunucu metni gösterilmez.

## Out of scope

- Sunucu uçlarının değiştirilmesi; tedarikçi kodunun değiştirilmesi (sunucu sözleşmesinde yok).

## Dependencies

- V1-RMD-114

## Acceptance evidence

- Testler ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-503/` altındadır.

## Handoff

- None
