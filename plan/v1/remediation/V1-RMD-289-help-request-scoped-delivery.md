# V1-RMD-289 - Yardım çağrısı yalnız yetkili terminallere gider

- Task ID: V1-RMD-289
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-25

## Goal

`HelpRequestExperience.cs` çağrıyı `Clients.All` ile tüm bağlı istemcilere yayınlıyor; bağlanan her istemci başkasının masa çağrılarını alabilir. Yayın kasa rolündeki terminallerin grubuna daraltılır ve hub bağlantısı ilgili izinle yetkilendirilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-289-help-request-scoped-delivery.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/HelpRequests/HelpRequestExperience.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/HelpRequests/HelpRequestHub.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/HelpRequests/HelpRequestHubTests.cs
  (yeni dosya, klasör V1-WTR-014 tarafından zaten sahiplenilmiş)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/HelpRequests/HelpRequestTestDatabase.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/HelpRequests/ALKAROS.Host.Experience.HelpRequests.Tests.csproj
  (yalnız `Microsoft.AspNetCore.App` çerçeve referansı)

## In scope

1. Grup tabanlı yayın, hub bağlantı yetkisi, testler.

## Out of scope

- İstemci bağlantı dayanıklılığı (`V1-RMD-286`).

## Dependencies

- V1-RMD-286

## Acceptance evidence

### Yerel doğrulama (görevin literal iddiasını yanlışlayan bulgu)

`HelpRequestHub.cs`'in kendi belge yorumu zaten şunu söylüyor: 'Same flat-broadcast shape as WaiterOrderStatusHub for the same reason: there is no manager-to-terminal assignment tracked anywhere to target one specific device.' Doğrulandı: yardım çağrısını yükselten terminal (garson cihazı) ile yöneticinin kendi oturumunun bağlı olduğu terminal (bir kasa) tamamen farklı, ilişkisiz kimlik uzaylarında. Literal bir 'terminale göre grupla' uygulaması, çoğu yöneticinin HİÇBİR çağrıyı almamasına yol açardı — bu sınıfın kendi tasarım notunu ve `LowStockAlertHub`'ın aynı sınıftaki restoran-geneli `Clients.All` deseninin (düşük stok da tek bir terminale değil tüm işletmeye ait) ihlali olurdu.

### Karar ve uygulama

Terminale göre gruplama yapılmadı (yukarıdaki bulgu nedeniyle yanlış olurdu). Bunun yerine, aynı sonucu (yalnız yetkili bağlantılar alır) daha savunmacı şekilde ifade eden bir değişiklik yapıldı: `Clients.All` (hub'a bağlı HERKESİ zımnen kapsayan) yerine, yalnız `OnConnectedAsync`'te kimlik doğrulaması BAŞARILI olan bağlantıların katıldığı açık bir `RecipientsGroup` (`Clients.Group(...)`). Bugün işlevsel sonuç aynı (tüm bağlantılar zaten kimlik doğrulamalı), ama gelecekte bu kapıya yapılacak bir gevşetme artık yayın kapsamını sessizce genişletemez.

Ayrıca daha önce hiç var olmayan gerçek bir bağlantı-seviyesi test eklendi (`HelpRequestHttpTests.cs`'in kendi yorumu: 'The SignalR broadcast side ... is not independently asserted here'): `HelpRequestHub.OnConnectedAsync`'i gerçek Postgres'e karşı, sahte `HubCallerContext`/`IGroupManager` ile (Hub birim testi için resmi desteklenen yöntem — `Context`/`Groups` genel ayarlanabilir özellikler) doğrudan çalıştırıyor. Yeni bir istemci NuGet paketi gerektirmiyor (Host-only, gerçek bir ağ round-trip'i değil).

### Kanıt

- Host testleri (UTF8 Postgres 18): `HelpRequests` paketi 11/11 (4 yeni). Çerezsiz bağlantı reddedilir; yalnız kasiyer çerezi de reddedilir; gerçek bir yönetici oturumu bağlanır ve `RecipientsGroup`'a katılır; gerçek bir süpervizör oturumu da aynı şekilde katılır.
- Mutasyon kontrolü: `HelpRequestHub.cs`/`HelpRequestExperience.cs` eski hâline döndürülünce test projesi derlenmedi (`RecipientsGroup` bulunamadı) — testlerin yeni koda gerçekten bağlı olduğunu kanıtlıyor.
- `plan_audit_tool.py validate` ve `consistency_audit.py` temiz.

## Handoff

- None
