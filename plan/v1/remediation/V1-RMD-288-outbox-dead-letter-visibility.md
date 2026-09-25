# V1-RMD-288 - Outbox ölü mektupları yöneticiye görünür ve yeniden kuyruğa alınabilir olur

- Task ID: V1-RMD-288
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-25

## Goal

`OutboxDispatcherHostedService` kalıcı başarısız olan olayları yalnızca günlüğe yazıyor (`LogDeadLetterRise`). Örneğin masa aktarımının sipariş ya da hesap tüketicisi kalıcı hata verirse kimse fark etmez ve olay sessizce kaybolur. Yönetici oturumlu, `security.manage` yetkili güvenlik yönetim grubuna salt-okur ölü mektup listesi ve seçili bir mesajı yeniden kuyruğa alan, denetim olayı yazan bir uç nokta eklenir (dry-run varsayılan).

## Owned surface

- `plan/v1/remediation/V1-RMD-288-outbox-dead-letter-visibility.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/SecurityAdministration/OutboxAdministration.cs
  (V1-RMD-266 sahipliğindeki klasöre eklenen yeni dosya)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/SecurityAdministration/SecurityAdministrationEndpoints.cs
  (yalnız `MapOutbox()` çağrısı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/SecurityAdministration/SecurityAdministrationHttpTests.cs

## In scope

1. Ölü mektup listesi, sayaç, tek mesajı yeniden kuyruğa alma, denetim olayı, yetki ve dry-run testleri.

## Out of scope

- Yönetim arayüzü ekranı.
- Tüketici hatalarının kök sebebini otomatik onarmak.

## Dependencies

- V1-RMD-266

## Acceptance evidence

- Host testleri (UTF8 Postgres 18): anonim 401, yetkisiz 403; gerçek bir ölü mektup listelenir; yeniden kuyruğa alma mesajı tekrar teslim edilebilir yapar ve denetim olayı yazar; dry-run hiçbir şeyi değiştirmez.
- `plan_audit_tool.py validate` temiz.

## Handoff

- None
