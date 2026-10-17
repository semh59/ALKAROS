# V1-RMD-590 - Göç listesi sınırı ve yazıcı formu bağlantı testi

- Task ID: V1-RMD-590
- Status: InProgress
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: validation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

V1-RMD-589 yeni bir göç (193) ekledi; göç listesi doğrulayıcısının en büyük sınırı (`PhaseBMax`) ve beklenen göç listesi testi bu numarayı bilmiyor, ayrıca mutfak ekranının yazıcı formunun rota düzeyinde sunucu uçlarına bağlandığını gösteren test yok. Bu görev ikisini kapatır.

## Owned surface

- `plan/v1/remediation/V1-RMD-590-migration-manifest-and-printer-route-test.md`
- `evidence/V1-RMD-590/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs - yalnız en büyük göç numarası
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Manifest/** - yalnız beklenen göç listesi ve sayısı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/workspace.test.tsx - yalnız yazıcı formu bağlantı testi
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- `PhaseBMax` değerinin 193 olması, beklenen göç listesi ve sayısı (192 giriş); yazıcı formunun yalnız yönetim yetkisiyle görünmesi ve kaydın doğru uca gidip listeyi yeniden okuması.

## Out of scope

- Yeni göç veya uç eklemek.

## Dependencies

- V1-RMD-589

## Acceptance evidence

1. Manifest testleri ve rota testi geçer; rota bağlantısı için mutasyon kanıtı V1-RMD-589 kaydındadır.
2. Çıktılar `evidence/V1-RMD-590/` altındadır.

## Handoff

- None
