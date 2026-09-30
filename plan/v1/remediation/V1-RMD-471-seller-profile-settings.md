# V1-RMD-471 - Fatura için satıcı (işletme) bilgileri ayarı

- Task ID: V1-RMD-471
- Status: InProgress
- Assignee: claude-code-session_01Vj7DgrFRRgpSMFfXpfSjwx
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-09-30

## Goal

Faturanın satıcı bölümü (ticari ünvan, VKN, vergi dairesi, adres) bugün hiçbir yerde saklanmıyor; yalnız işletme adı
ayarı ve QNB kimlik bilgisindeki VKN var. `V1-RMD-470` tabloyu açtı; bu görev bilgileri okuyup yazan uç noktaları ve
"Entegrasyon ayarları" ekranında (QNB kimlik bilgisinin yanında, aynı oturum ve `integrations.manage` yetkisiyle) "Fatura
bilgileri" kartını ekler. VKN 10 haneli, TCKN 11 haneli doğrulanır; eksik bilgiyle fatura taslağı açılmaz.

## Owned surface

- `plan/v1/remediation/V1-RMD-471-seller-profile-settings.md`
- `src/Host/Experience/InvoiceSettings/**`
- `src/Clients/PosTerminal/src/features/invoice-settings/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/QnbCredentialSettings.tsx - yalnız yeni kartın yerleştirilmesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/QnbCredentialSettings.test.tsx - yalnız kartın varlığı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/strings.ts - yalnız yeni kartın Türkçe metinleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/QnbCredentialSettings/SellerProfileSettingsHttpTests.cs - yalnız yeni uç noktaların testleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs - yalnız yeni uç noktaların ve servislerin kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tools/consistency-audit/unreachable_services_allowlist.json - yalnız `ISellerProfileStore` ve gerçeklemesinin geçici kaydının kaldırılması

## In scope

- `GET`/`PUT` `/api/v1/terminals/{terminalId}/invoice-settings/seller-profile` (yetki: `integrations.manage`, QNB kimlik bilgisi uç
  noktalarıyla aynı oturum kalıbı), doğrulama ve Türkçe hata gerekçeleri; "Entegrasyon ayarları" içinde kart (yükleniyor, boş, hata).
- Testler: uç nokta (oturum, yetki, doğrulama, kalıcılık), istemci (Türkçe, axe).

## Out of scope

- Fatura taslağı üretimi (`V1-RMD-470`); QNB kimlik bilgileri (mevcut ekran); Yönetim alanı bölümü.

## Dependencies

- V1-RMD-470

## Acceptance evidence

- Testler ve gerçek Host denemesi (bilgi kaydedilir, yeniden açılınca görünür); çıktılar `evidence/V1-RMD-471/` altındadır.

## Handoff

- None
