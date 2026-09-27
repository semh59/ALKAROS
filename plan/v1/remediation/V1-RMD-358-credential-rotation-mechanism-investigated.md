# V1-RMD-358 - Relay/QNB/Token kimlik bilgisi rotasyon mekanizması incelendi, ayrı bir özellik görevi olarak kapsam dışına alındı

- Task ID: V1-RMD-358
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: investigation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) düşük seviye bulgusu: harici entegrasyon kimlik bilgileri
(Cloudflare Relay API token, QNB e-Fatura, Token/Beko terminal kimlik bilgisi) için bir "rotasyon mekanizması"
yok. Kod incelemesiyle doğrulandı: bulgu doğru ve üç entegrasyonun ÜÇÜNDE de tutarlı.

- `PostgresRelayCredentialStore` (`ALKAROS.QrOrdering.RelayCredential`)
- `PostgresQnbCredentialStore` (`src/Modules/Invoicing/Qnb/CredentialRegistration/`)
- `PostgresTokenTerminalCredentialStore` (`src/Modules/Payments/Token/TerminalCredential/`)

Üçü de AYNI deseni izliyor: `credential_key` üzerinde tek satırlı bir `ON CONFLICT ... DO UPDATE` upsert. Yeni
bir kimlik bilgisi kaydedildiğinde eskisi ANINDA ve GERİ DÖNÜŞSÜZ üzerine yazılıyor — ne bir geçmiş/versiyon
tablosu, ne yeni bilgi işe yaramadan eskiyi elde tutan bir örtüşme penceresi, ne bir rotasyon denetim izi, ne de
personelin "bu kimlik bilgisi ne zaman kaydedildi, süresi ne zaman doluyor" diye sorabileceği bir hatırlatma
ekranı var.

Bu görevin kapsamında düzeltilmedi, çünkü gerçek bir rotasyon mekanizması (aşağıdakilerin hepsi, yalnızca biri
değil):

1. Veritabanı şeması: her üç kimlik bilgisi tablosu için versiyonlu bir geçmiş (hangi sürüm ne zaman etkin
   oldu, kim değiştirdi) — üç yeni migration.
2. Her üç `SaveAsync` metodunun kendisi: şu anki "eskiyi anında üzerine yaz" davranışından, "yeni kimlik bilgisi
   harici servise karşı doğrulanana kadar eskisini etkin tut, sonra kes" akışına — üç ayrı, servise özgü
   doğrulama çağrısı gerektirir (Cloudflare API token'ı test etmek, QNB "Bağlantıyı Test Et"i tekrar kullanmak,
   Token/Beko'nun kendi terminal ping'i).
3. Personel arayüzü: üç Ayarlar ekranının (`RelaySettings.tsx`, `QnbCredentialSettings.tsx`,
   `TokenTerminalSettings.tsx`) üçünde de "son rotasyon ne zamandı, bir sonraki ne zaman önerilir" bilgisini
   gösteren, ve rotasyonu güvenle tetikleyen yeni bir akış.
4. Denetim izi: her rotasyonun kim/ne zaman/hangi eski-yeni çift için yapıldığının kaydı.

Bu, düşük seviyeli TEK bir düzeltmenin değil, kendi Task ID'sine, kendi plan bölümüne ve muhtemelen birden
fazla alt göreve ihtiyaç duyan, ileriye dönük bir ÖZELLİK. Bu oturumun standart disiplini (bkz. V1-RMD-341,
V1-RMD-346, V1-RMD-351, V1-RMD-357) — orantısız büyüklükteki bulguları sahte bir hızlı yamayla "çözülmüş"
göstermek yerine dürüstçe incelenmiş ve kapsam dışına alınmış olarak işaretlemek — burada da uygulandı.

## Owned surface

- `plan/v1/remediation/V1-RMD-358-credential-rotation-mechanism-investigated.md`

## In scope

- Yalnızca inceleme; kod değişikliği yok.

## Out of scope

- Gerçek rotasyon mekanizmasının kendisi — Semih istediğinde ayrı bir gelecek görev/plan dalı olarak
  başlatılmalı: üç entegrasyonun (Relay/QNB/Token) üçü için ortak bir "kimlik bilgisi yaşam döngüsü" tasarımı
  önerilir, her biri için ayrı ayrı değil, çünkü üçü de aynı tek-satır upsert desenini paylaşıyor.

## Dependencies

- None

## Acceptance evidence

- Kod incelemesi: `PostgresRelayCredentialStore.SaveCloudflareApiTokenAsync`,
  `PostgresQnbCredentialStore.SaveAsync`, `PostgresTokenTerminalCredentialStore.SaveAsync` — üçü de
  `ON CONFLICT (credential_key) DO UPDATE` ile tek satırı geri dönüşsüz üzerine yazıyor, doğrulandı.
- Üç ilgili Ayarlar ekranı (`RelaySettings.tsx`, `QnbCredentialSettings.tsx`, `TokenTerminalSettings.tsx`) ve
  ilgili uç noktalar (`RelaySettingsEndpoints.cs`, `QnbCredentialSettingsEndpoints.cs`,
  `TokenTerminalSettingsEndpoints.cs`) tarandı — hiçbirinde rotasyon/versiyon/geçmiş kavramı yok, doğrulandı.

## Handoff

- None
