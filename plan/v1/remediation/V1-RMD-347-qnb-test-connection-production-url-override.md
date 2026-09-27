# V1-RMD-347 - QNB "Bağlantıyı Test Et" artık ortam değişkeniyle üretim adresine yönlendirilebiliyor

- Task ID: V1-RMD-347
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) orta seviye bulgusu: "QNB 'Bağlantıyı Test Et' her zaman sabit
test-tenant URL'sine gidiyor, üretime geçiş noktası kodda işaretli değil." Doğrulandı: `QnbTestUserServiceUrl`
salt bir `const string` idi, üretime geçiş noktası yalnızca bir kod YORUMU olarak vardı — gerçek bir dağıtımın
bunu QNB'nin gerçek kiracı adresine yönlendirmesinin MEKANİK bir yolu yoktu; tek yol dosyayı düzenlemek ve yeniden
derlemekti.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/QnbCredentialSettings/QnbCredentialSettingsEndpoints.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/QnbCredentialSettings/QnbCredentialSettingsHttpTests.cs
- `plan/v1/remediation/V1-RMD-347-qnb-test-connection-production-url-override.md`

## In scope

1. Yeni `ResolveQnbUserServiceUrl()`: `ALKAROS_QNB_USER_SERVICE_URL` ortam değişkeni ayarlıysa onu kullanır,
   yoksa güvenli varsayılan olan test-tenant URL'sine düşer — bu kod tabanının zaten kurduğu, tam olarak bu tür
   "güvenli test varsayılanı, üretimde ortam değişkeniyle geçersiz kılınabilir" değerler için (`ALKAROS_BACKUP_DIR`,
   `ALKAROS_RESTORE_MAINTENANCE_CONNECTION`, vb.) kullandığı deseni izliyor. Bir kiracının `userService` URL'si
   bir yapılandırma değeridir, gizli bir bilgi değil — bu yüzden gizli anahtar deposu değil, bir ortam değişkeni
   doğru yer.
2. `/test-connection`'ın tek çağrı yeri artık `ResolveQnbUserServiceUrl()`'ü kullanıyor.

## Out of scope

1. `V14-QNB-001/002`'nin kendi kapsamı (belge gönderme/sorgulama) — bu görev yalnızca "Bağlantıyı Test Et"'in
   hedef URL'sini kapsıyor.

## Dependencies

- None

## Acceptance evidence

- `tests/Host/Experience/QnbCredentialSettings/ALKAROS.Host.Experience.QnbCredentialSettings.Tests.csproj`:
  14/14 test geçti (1 yeni test dahil: `TestConnectionUsesTheEnvironmentOverrideUrlWhenOneIsSet`) — GERÇEK yerel
  bir HTTP dinleyicisi QNB'nin yerini alıyor (yanıtında hiçbir SOAP `Fault` yok, bu yüzden `wsLogin`/`logout`
  ikisi de başarılı oluyor); başarı YALNIZCA override URL'si gerçekten kullanıldıysa mümkün — gerçek QNB test
  sunucusu sahte kimlik bilgilerine her zaman bir SOAP hatasıyla cevap veriyor (kardeş testin kendi kanıtı).
- Mutasyon kontrolü: `ResolveQnbUserServiceUrl()` çağrısı geçici olarak sabit `QnbTestUserServiceUrl`'e
  döndürüldü, yeni test GERÇEK bir ağ davranışı farkıyla kırmızıya döndü (gerçek QNB sunucusuna gitti, gerçek
  bir "kullanıcı adı veya parola hatalı olabilir" hatası aldı — override hiç kullanılmadı). Dosya geri
  yüklendi (`diff` ile bayt-bayt doğrulandı), paket yeniden derleme sonrası 14/14 yeşile döndü.

## Handoff

- None
