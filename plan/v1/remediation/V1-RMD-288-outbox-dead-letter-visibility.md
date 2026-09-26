# V1-RMD-288 - Outbox ölü mektupları yöneticiye görünür ve yeniden kuyruğa alınabilir olur

- Task ID: V1-RMD-288
- Status: Done
- Assignee: Claude Sonnet 5
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

- Host testleri (UTF8 Postgres 18): `SecurityAdministration` paketi 26/26 (4 yeni). Yeni: anonim 401, salt-okur yetkili (`reports.view`) ve `security.manage`'a sahip `supervisor:` cihazı reddediliyor; gerçek bir ölü mektup (`status='dead'`, 3 deneme, hata metni) listeleniyor; varsayılan çağrı kuru çalıştırma (durum değişmiyor); `dryRun=false` mesajı `pending`'e alıyor, deneme sayacını sıfırlıyor ve denetim olayı yazıyor; aynı mesaj ikinci kez istenince (artık `dead` değil) `404`; bilinmeyen kimlik `404`.
- Mimari sınır testleri (pytest): 10/10.
- Mutasyon kontrolü: `OutboxAdministration.cs` ve `SecurityAdministrationEndpoints.cs`'teki `MapOutbox()` çağrısı geri alınınca test projesi derlenmedi (`OutboxAdministration` adı bulunamadı) — testlerin yeni koda gerçekten bağlı olduğunu kanıtlıyor.
- `plan_audit_tool.py validate` ve `consistency_audit.py` temiz.
- Kapsam notu: tüketici hatasının kök sebebi onarılmıyor (görevin kapsamı dışında); yeniden kuyruğa alınan mesaj kök sebep düzelmediyse yeniden ölebilir, bu beklenen davranış.

## Handoff

- None
