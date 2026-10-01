# V1-RMD-478 - Satıcı bilgisi kaydetme ucunun idempotency anahtarını almasını sağlamak

- Task ID: V1-RMD-478
- Status: Done
- Assignee: claude-code-session_01Vj7DgrFRRgpSMFfXpfSjwx
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-01

## Goal

`V1-RMD-471` ile gelen `PUT /api/v1/terminals/{terminalId}/invoice-settings/seller-profile` ucu idempotency anahtarı almıyor; mimari sözleşme testi
(`MutatingEndpointIdempotencyTests`) CI'da kırmızı. API standardı her veri değiştiren isteğin `X-Idempotency-Key` başlığını kabul etmesini ister.
Uç bu başlığı kabul eder (kayıt zaten değiştirmenin kendisi tekrar edilebilir, anahtar yalnız sözleşmeyi karşılar) ve istemci her kaydetmede yeni bir anahtar gönderir.
İzin listesine eklenmez (liste yalnız küçülür).

## Owned surface

- `plan/v1/remediation/V1-RMD-478-seller-profile-idempotency-key.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/InvoiceSettings/InvoiceSettingsEndpoints.cs - yalnız başlık parametresi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/invoice-settings/sellerProfileApi.ts - yalnız başlığın gönderilmesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/invoice-settings/SellerProfileCard.test.tsx - yalnız başlık beklentisi

## In scope

- Başlık parametresi, istemcinin başlığı göndermesi, mimari sözleşme testinin yeşil olması.

## Out of scope

- Anahtara göre tekrar oynatma (kayıt zaten tekrar edilebilir).

## Dependencies

- V1-RMD-471

## Acceptance evidence

- `MutatingEndpointIdempotencyTests` yeşil, istemci testi başlığı doğrular; çıktılar `evidence/V1-RMD-478/` altındadır.

## Handoff

- None
