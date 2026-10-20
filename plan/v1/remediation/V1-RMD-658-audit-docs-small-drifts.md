# V1-RMD-658 - Denetim modülü belge ve küçük tutarsızlıkların kapatılması

- Task ID: V1-RMD-658
- Status: InProgress
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: documentation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

V1-RMD-595 bulguları F22, F23, F25 ve F26 kapanır. F22: denetim kaydının saklama tanımı tek yerde yazılır (DisposalMatrix girdisi satır anonimleştirme değil yıllık bölüm silme demektir; vergi ve hukuki bekletme süreleri Ürün Sahibi kararı bekleyen açık madde olarak belgelenir). F23: eskimiş yorumlar ve belge çelişkileri düzeltilir (Program.cs yorumu, module-dependency-rules.md çapraz kesen satırı, kod yorumlarındaki PDF ve görev numarası kalıntıları, V1-OPS-001 yüzey durumu). F25: üçüncü taraf lisans politikası belgelenir; paket kilit dosyası sürüm farkı ve Host kenar listesi kapsamı denetlenip sonuç yazılır. F26: kanıt klasörü olmayan üç eski Done görev (V1-RMD-057, 072, 103) kayıt altına alınır.

## Owned surface

- `plan/v1/remediation/V1-RMD-658-audit-docs-small-drifts.md`
- `evidence/V1-RMD-658/**`
- `docs/engineering/third-party-license-policy.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-657-audit-tests-ordering-di.md - yalnız Handoff satırı
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/operations/V1-OPS-001-audit-foundation.md - yalnız Surface state satırı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Program.cs - yalnız bir yorum
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Audit/EventStore/AuditEvent.cs - yalnız yorum
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Audit/EventStore/PostgresAuditEventStore.cs - yalnız yorum
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Security/DataProtectionRetention/DisposalMatrix.cs - yalnız yorum
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/architecture/module-dependency-rules.md - yalnız çapraz kesen satırı ve Host kenar notu
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/compliance/kvkk-retention-runbook.md - yalnız saklama tanımı bölümü
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Yukarıdaki dört bulgunun belge ve yorum düzeltmeleri; davranış değişmez.

## Out of scope

- Vergi saklama süresi ve hukuki bekletme kararı (Ürün Sahibi/mevzuat); yalnız açık madde olarak yazılır.

## Dependencies

- V1-RMD-657

## Acceptance evidence

1. Her düzeltme `evidence/V1-RMD-658/README.md` içinde bulgu numarasıyla eşlenir; derleme ve Audit ile mimari testleri yeşil.
2. plan-audit ve consistency-audit sıfır hata sıfır uyarı.

## Handoff

- None
