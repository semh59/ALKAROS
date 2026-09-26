# V1-RMD-324 - Relay connector öldüğünde durum paneli sonsuza kadar "Bağlı" gösteriyordu

- Task ID: V1-RMD-324
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) K17 bulgusu: `RelayConnectorStatusPublisher` (connector container'ının içinde çalışan `BackgroundService`) `qr_ordering.relay_connector_status` satırını her 5 saniyede bir yazıyor; `PostgresRelayConnectorStatusReporter.CurrentStatus` (api container'ının okuduğu taraf) bu satırın `updated_at`'ını hiç okumuyor, hiç bayatlık kontrolü yapmıyordu. Connector container'ının kendisi (publisher dahil) çökerse, son yazılan durum (genelde "Running") satırda kalıcı olarak donuyor ve api bunu sonsuza kadar gerçekmiş gibi okuyup gösteriyordu.

## Owned surface

- `plan/v1/remediation/V1-RMD-324-relay-connector-status-staleness.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Integrations/QrRelay/LocalConnector/RelayConnectorStatus.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Integrations/QrRelay/PublicGateway/PostgresRelayConnectorStatusReporter.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/contracts.ts
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/RelaySettings.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/RelaySettings.test.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Integrations/QrRelay/PublicGateway/PostgresRelayConnectorStatusReporterTests.cs

## In scope

1. `RelayConnectorState`'e yeni bir değer: `Unknown` — yalnız okuyucu tarafında üretilir (yazıcı `RelayConnectorSupervisor` bunu hiç bilmez).
2. `PostgresRelayConnectorStatusReporter`: `updated_at`'ı okur, `now - updated_at > 30s` (publisher'ın kendi 5s aralığının 6 katı — normal jitter'ı tolere eder, gerçekten ölü bir container'ı tek bir kontrolde yakalar) ise durumu `Unknown`'a çevirir. Deterministik test için "now" enjekte edilebilir hâle getirildi.
3. İstemci: `contracts.ts`'in `connectorState` union'ı ve `RelaySettings.tsx`'in Türkçe etiket haritası 4. değeri kapsayacak şekilde genişletildi (harita zaten TypeScript-exhaustive tasarlanmıştı — yeni değer eklenmeden derleme hatası verirdi).

## Out of scope

- `cloudflared`'in kendi QUIC bağlantı durumunun (gerçekten Cloudflare'e bağlı mı) izlenmesi — `RelayConnectorState`'in kendi belge yorumu bunun bilinçli olarak kapsam dışı olduğunu zaten söylüyor.
- Connector container'ının kendisinin otomatik yeniden başlatılması — bu görev yalnız DURUM RAPORLAMASININ doğruluğunu kapsıyor.

## Dependencies

- None

## Acceptance evidence

Host/Integrations testleri (UTF8 Postgres 18), gerçek bir veritabanına karşı: yeni testler `ARowStaleByMoreThanTheThresholdReportsUnknownInsteadOfTheFrozenLastState` (31 saniye eski bir "Running" satırı → `Unknown`) ve `ARowWithinTheThresholdStillReportsItsRealState` (29 saniye eski → hâlâ `Running`, eşik simetrik doğrulandı). `ALKAROS.QrRelay.PublicGateway.Tests` 23/23 (2 yeni), `ALKAROS.QrRelay.LocalConnector.Tests` 9/9, regresyon yok.

PosTerminal: yeni vitest testi (`RelaySettings.test.tsx`) sunucudan `connectorState: "Unknown"` döndüğünde ekranın gerçekten "Bağlayıcı durumu bilinmiyor (bağlantı kesilmiş olabilir)" Türkçe metnini gösterdiğini, ham "Unknown" dizisinin asla ekrana çıkmadığını doğruluyor. Tüm PosTerminal vitest paketi: 29/29 dosya, 219/219 test (2 yeni) yeşil. `tsc --noEmit` temiz (yeni union üyesi eklenmeden derleme hatası olacağı önceden belgelenmiş exhaustive-map deseni doğrulandı).

Mutasyon kontrolü: `PostgresRelayConnectorStatusReporter`'ın bayatlık kontrolü satırı geçici olarak kaldırıldı — yeni test gerçekten kırmızı oldu (`Expected: Unknown, Actual: Running`); dosya `diff` ile birebir orijinaline geri getirildi, tüm paket tekrar yeşil.

## Handoff

- None
