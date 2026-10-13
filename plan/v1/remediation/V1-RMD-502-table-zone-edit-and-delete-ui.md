# V1-RMD-502 - Masa ve bölge düzenleme, bölge silme ekranı

- Task ID: V1-RMD-502
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-01

## Goal

Sunucuda bölge güncelleme/silme ve masa güncelleme uçları hazırdır, arayüz yalnız oluşturur. Yanlış yazılmış bir masa numarası, kapasite veya bölge adı düzeltilemez, kullanılmayan bölge silinemez. Yönetici bunları ekrandan yapabilmelidir.

## Owned surface

- `plan/v1/remediation/V1-RMD-502-table-zone-edit-and-delete-ui.md`
- `evidence/V1-RMD-502/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/tables/TableWorkspace.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/tables/TableWorkspace.test.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/tables/tableApi.ts
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/tables/tableApi.test.ts
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/tables/models.ts
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/workspace.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/strings.ts
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Bölgenin adı, sırası ve etkinliğinin güncellenmesi; bölgenin silinmesi (güncel satır sürümüyle, onay istenerek).
- Masanın numarası, bölgesi, kapasitesi ve etkinliğinin güncellenmesi (güncel satır sürümüyle).
- `manage` yetkisi olmayan oturumda düğmeler görünmez; 409 için yenileme mesajı; ham sunucu metni gösterilmez.

## Out of scope

- Sunucu uçlarının değiştirilmesi; kat planı geometri düzenlemesi; "Birleşimi ayır" düğmesi (sunucu komutu verdiğinde zaten çalışır, yalnız vermediğinde kapalı gösterilir).

## Dependencies

- V1-RMD-114

## Acceptance evidence

- Testler ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-502/` altındadır.

## Handoff

- None
