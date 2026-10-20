# V1-RMD-650 - Yönetim işlemlerinde denetim kaydının eylemden önce veya aynı işlemde yazılması

- Task ID: V1-RMD-650
- Status: InProgress
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Denetim kaydı 10 yıl saklanır (Ürün Sahibi kararı 2026-10-20) ve iz kaybı kabul edilmez. Yönetim ekranının üç işlemi eylemi yaptıktan sonra denetim yazıyordu; denetim yazılamazsa eylem izsiz kalıyordu. Ölü mektubu yeniden kuyruğa alma: güncelleme ve denetim tek işlemde. Gizli anahtar sürümü işlemleri (başlat, döndür, iptal, geri al): kayıt veritabanında olmadığı için denetim eylemden önce yazılır; denetim yazılamazsa anahtar durumu değişmez. Eski siparişleri toplu kapatma: kapatma başlamadan önce niyet kaydı yazılır; yazılamazsa hiçbir sipariş kapatılmaz. Kaynak: V1-RMD-595 bulgusu F01 (yönetim işlemleri çağrı noktaları).

## Owned surface

- `plan/v1/remediation/V1-RMD-650-audit-before-admin-actions.md`
- `evidence/V1-RMD-650/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-649-audit-atomic-write-manual-card-resolution.md - yalnız Handoff satırı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/SecurityAdministration/OutboxAdministration.cs - yalnız denetim yazımı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/SecurityAdministration/SecretRotationAdministration.cs - yalnız denetim yazımı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/SecurityAdministration/OrderBacklogAdministration.cs - yalnız denetim yazımı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/SecurityAdministration/SecurityAdministrationHttpTests.cs - yalnız denetim atomikliği testleri
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Üç yönetim işlemi için yukarıdaki sıra; denetim yazımının veritabanı düzeyinde zorlanan hatasıyla (test tetikleyicisi) kanıtlanan geri alma / hiç başlamama davranışı.

## Out of scope

- Gözlemlenebilirlik dedektörleri, saklama servisleri, anonimleştirme, tanılama paketi ve şüpheli giriş kaydı (ayrı görevler).

## Dependencies

- V1-RMD-649

## Acceptance evidence

1. Önce kırmızı sonra yeşil gerçek PostgreSQL testi: denetim yazımı hata verirse ölü mektup kuyruğa alınmaz, gizli anahtar durumu değişmez, hiçbir sipariş kapatılmaz.
2. Mutasyon kanıtı ve çıktılar `evidence/V1-RMD-650/` altındadır.

## Handoff

- None
