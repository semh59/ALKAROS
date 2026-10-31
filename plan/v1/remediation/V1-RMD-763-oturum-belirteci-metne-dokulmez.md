# V1-RMD-763 - Oturum belirteci nesne biçimlendirmesinde görünmez

- Task ID: V1-RMD-763
- Status: Planned
- Assignee: claude-code-session_01FMN5WVXi1HJCTEk5argkgZ
- Work type: remediation
- Surface state: Existing

## Goal

V1-RMD-739 bulgusu V1-IAM-013: `IssuedSessionToken` düz `record` olduğundan varsayılan `ToString()` `Token` alanını açık yazar; `LoginSuccess` bu nesneyi taşıdığı için bir günlük satırı, istisna mesajı veya hata ayıklayıcı çıktısı canlı oturum belirtecini sızdırabilir. Önceki Done görevi (V1-IAM-013) bunu "görünmez" diye iddia etmişti, HEAD'de karşılığı yok.

`IssuedSessionToken.ToString()` belirteç ve özetini yazmaz; `LoginSuccess.ToString()` bunu taşıdığı için o da güvenlidir.

## Owned surface

- `plan/v1/remediation/V1-RMD-763-oturum-belirteci-metne-dokulmez.md`
- `evidence/V1-RMD-763/**`
- Sınırlı ek: src/Modules/Identity/Authentication/SessionTokenIssuer.cs
- Sınırlı ek: tests/Modules/Identity/Authentication/SessionTokenIssuerTests.cs

## In scope

- `IssuedSessionToken` için belirteç ve özeti yazmayan `ToString()`.
- Testler: `IssuedSessionToken` ve onu taşıyan `LoginSuccess` metne dökülünce belirteç ve özet geçmez; `Token` özelliği yine değeri verir.

## Out of scope

- `SensitivePayload`/`SensitiveEnvelope` değişmezliği (V1-SEC-005).
- Günlük yazma kodunun taranması ve oturum kalıcılığı.

## Dependencies

- V1-IAM-013

## Acceptance evidence

1. Düzeltmeden önce yeni testler kırmızı olur; çıktı kaydedilir.
2. Düzeltmeden sonra `ALKAROS.Identity.Authentication.Tests` exit 0 geçer.
3. Mutasyon: `ToString()` kaldırılınca testler kırmızı olur; dosya hash'i geri yüklenir.
4. Kapsam aracı InProgress baseline'a karşı exit 0 verir; iki bağımsız incelemede artık bulgu kalmaz.

## Handoff

- None
