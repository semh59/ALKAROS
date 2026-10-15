# V1-RMD-559 - Bağımsız denetim düzeltmeleri

- Task ID: V1-RMD-559
- Status: InProgress
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

534, 552, 538, 554, 555 ve 547 için bağımsız denetimin bulduğu veri kaybı, çift gönderim ve host düşürme riskleri kapanır.

## Owned surface

- `plan/v1/remediation/V1-RMD-559-monitoring-audit-fixes.md`
- `evidence/V1-RMD-559/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Observability/CrashChannel/CrashChannel.cs - denetim düzeltmesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Observability/ServerDetectors/ServerDetectors.cs - denetim düzeltmesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Observability/SecurityDetectors/SecurityDetectors.cs - denetim düzeltmesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Observability/HealthSnapshot/HealthSnapshotJob.cs - denetim düzeltmesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Observability/ResourceProbes/ResourceProbes.cs - denetim düzeltmesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Observability/ErrorNotifications/ErrorNotifications.cs - denetim düzeltmesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Observability/CrashChannel/CrashChannelTests.cs - denetim düzeltmesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Observability/ServerDetectors/ServerDetectorsTests.cs - denetim düzeltmesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Observability/SecurityDetectors/SecurityDetectorsTests.cs - denetim düzeltmesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Observability/HealthSnapshot/HealthSnapshotTests.cs - denetim düzeltmesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Observability/ResourceProbes/ResourceProbesTests.cs - denetim düzeltmesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Observability/ErrorNotifications/ErrorNotificationsTests.cs - denetim düzeltmesi
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Tampon dosyası atomik yazılır, bozuk satır atlanır, boşaltma ve ekleme sıralanır; zaman aşımı hataları yakalanır.
- Bildirimde ağ kesintisi kalıcı Failed yapmaz; aynı anda tek dağıtıcı; retry_after ayrıştırma hataları dağıtıcıyı düşürmez.
- Gözlemci ve arka plan döngüleri hata yutmadan host düşürmez; saklama işi hatası olay olur; yetki reddi sink kaydı Singleton.

## Out of scope

- Çöküş kanalının Host akışına bağlanması ve gerçek Telegram (V1-RMD-557).

## Dependencies

- V1-RMD-534
- V1-RMD-538
- V1-RMD-552
- V1-RMD-554
- V1-RMD-555
- V1-RMD-547

## Acceptance evidence

1. Her düzeltme için hatayı gösteren test önce başarısız, sonra geçer; mutasyon kanıtı.
2. Çıktılar `evidence/V1-RMD-559/` altındadır.

## Handoff

- None
