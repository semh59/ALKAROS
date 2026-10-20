# V1-RMD-653 - Performans bütçesi değişikliğinde denetim kaydının eylemden önce yazılması

- Task ID: V1-RMD-653
- Status: InProgress
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Denetim kaydı 10 yıl saklanır (Ürün Sahibi kararı 2026-10-20) ve iz kaybı kabul edilmez. Performans bütçesi ekleme/değiştirme ve kaldırma izleme veri kaynağında yapılıyor, denetim kaydı ise ayrı kaynakta yazılıyor; ikisi tek işlemde olamaz. Bu yüzden denetim kaydı değişiklikten önce yazılır: yazılamazsa bütçe değişmez. Kaldırmada kayıt, bütçenin var olduğu doğrulandıktan sonra yazılır, olmayan bütçe için kayıt oluşmaz. Kaynak: V1-RMD-595 bulgusu F01 (performans bütçesi çağrı noktaları).

## Owned surface

- `plan/v1/remediation/V1-RMD-653-audit-before-performance-budget-change.md`
- `evidence/V1-RMD-653/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-652-audit-atomic-write-customer-anonymization.md - yalnız Handoff satırı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Observability/PerformanceBudgetEndpoints/PerformanceBudgetEndpoints.cs - yalnız denetim yazımı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Observability/PerformanceBudgetHttpTests.cs - yalnız denetim atomikliği testi
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Ekleme/değiştirme ve kaldırma uç noktalarında sıra: doğrulama, denetim kaydı, değişiklik.

## Out of scope

- Gönderilmiş kalem iptali ve elle yazılmış kayıt satırları (ayrı görevler).

## Dependencies

- V1-RMD-652

## Acceptance evidence

1. Önce kırmızı sonra yeşil gerçek PostgreSQL testi: denetim yazımı hata verirse bütçe eklenmez/kaldırılmaz; olmayan bütçenin kaldırılması denetim satırı üretmez.
2. Mutasyon kanıtı ve çıktılar `evidence/V1-RMD-653/` altındadır.

## Handoff

- None
