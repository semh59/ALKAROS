# V1-RMD-342 - QNB VKN'si ve Token terminal kimliği artık gerçek biçim kontrolünden geçiyor

- Task ID: V1-RMD-342
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) orta seviye bulgusu: "QNB/Token kimlik bilgisi formlarında biçim
doğrulaması yok" (`QnbCredentialSettings.tsx:83`, `TokenTerminalSettings.tsx:102-104`). Doğrulandı ve denetimin
işaret ettiğinden daha derin çıktı: hem istemci hem SUNUCU tarafında sıfır biçim kontrolü vardı — sadece boş
alan kontrolü. Bir yönetici VKN alanına "abc" veya terminal kimliği alanına rastgele bir metin yazsa, hiçbir
uyarı almadan kaydediliyordu; bu, gerçek bir e-Fatura/terminal entegrasyonu devreye girdiğinde (V0-QNB-001/
V0-HUG-001 hâlâ Blocked) çok daha sonra, çok daha kafa karıştırıcı bir noktada patlayacak bir zaman bombasıydı.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/QnbCredentialSettings/QnbCredentialSettingsEndpoints.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/TokenTerminalSettings/TokenTerminalSettingsEndpoints.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/QnbCredentialSettings.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/TokenTerminalSettings.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/QnbCredentialSettings/QnbCredentialSettingsHttpTests.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/TokenTerminalSettings/TokenTerminalSettingsHttpTests.cs
- `plan/v1/remediation/V1-RMD-342-qnb-token-credential-format-validation.md`

## In scope

1. **QNB VKN:** `IsValidVergiTcKimlikNo` — GİB'in kendi sabit ulusal standardı (VKN: kurum, 10 hane; TCKN:
   gerçek kişi mükellef, 11 hane; her ikisi de yalnızca rakam, resmi bir checksum algoritması yayımlanmamış).
   Hem sunucuda (400 `VALIDATION_FAILED`) hem istemcide (gerçek zamanlı Türkçe uyarı, kaydet düğmesi devre dışı)
   uygulandı.
2. **Token terminal kimliği:** `IsValidTokenTerminalId` — ekranın KENDİ etiketinin ve yer tutucusunun zaten
   vaat ettiği biçim ("AV/AT ile başlar", `AV0000111044`): iki harfli önek (`AV`/`AT`, büyük/küçük harf
   duyarsız) + yalnızca rakam. Hem sunucuda hem istemcide uygulandı.
3. `merchantId`/`branchId` (Token) için biçim kontrolü EKLENMEDİ — bu kod tabanının kendi araştırması
   (`evidence/v0/integrations/V0-HUG-001/`) bu iki alan için doğrulanmış, güvenilir bir biçim belirtmiyor;
   icat edilmiş bir kısıtlama, gerçek bir değerin yanlışlıkla reddedilmesine yol açabilirdi.

## Out of scope

1. `merchantId`/`branchId` biçim doğrulaması — yukarıda gerekçelendirildiği gibi, güvenilir bir kaynak yok.
2. VKN/TCKN için bir sağlama toplamı (checksum) algoritması — GİB resmi olarak böyle bir algoritma
   yayımlamıyor; sadece hane sayısı ve rakam kontrolü uygulanabilir doğru kural.

## Dependencies

- None

## Acceptance evidence

- `tests/Host/Experience/QnbCredentialSettings/ALKAROS.Host.Experience.QnbCredentialSettings.Tests.csproj`:
  13/13 test geçti (5 yeni test dahil: 4 bozuk VKN reddi + 11 haneli TCKN kabulü).
- `tests/Host/Experience/TokenTerminalSettings/ALKAROS.Host.Experience.TokenTerminalSettings.Tests.csproj`:
  10/10 test geçti (5 yeni test dahil: 4 bozuk terminal kimliği reddi + küçük harf önek kabulü).
- Mutasyon kontrolü: her iki endpoint dosyası eski (biçim kontrolsüz) haline döndürüldü (`git stash`), yeni
  testlerin TAMAMI (8/8) beklenen şekilde kırmızıya döndü (`Expected: BadRequest, Actual: NoContent`). Dosyalar
  geri yüklendi, yeniden derleme sonrası her iki paket tekrar 13/13 ve 10/10 yeşile döndü.
- `npx tsc --noEmit`: sıfır hata. `npx vitest run` (PosTerminal'in tüm paketi): 31 dosya, 234 test, tümü yeşil
  (regresyon yok). `dotnet build src/Host/ALKAROS.Host.csproj`: sıfır hata, sıfır uyarı.

## Handoff

- None
