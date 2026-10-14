# V1-RMD-527 - Kanal raporunda iadenin netlenmesi

- Task ID: V1-RMD-527
- Status: InProgress
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-14

## Goal

QR ve çevrimiçi kanal raporu, o günün siparişlerine bağlı hesaplardan geri ödenen tutarı ayrı bir satır alanı olarak gösterir; kabul edilen değerden net tutar okunabilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-527-channel-report-refund-netting.md`
- `evidence/V1-RMD-527/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Reporting/Channels/ChannelReportService.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Reporting/Channels/ChannelReportModels.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Reporting/Channels/ChannelReportTests.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Reporting/Channels/Fixtures/ChannelReportTestDatabase.cs
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Her kanal/gün satırına `RefundedValue`: siparişin hesabına bağlı ödemelerin ters kayıt toplamı; iade, siparişin oluştuğu günün satırına yazılır (ödeme yöntemi raporundaki gibi iade gününe değil, çünkü bu rapor sipariş gününe göre gruplanır).
- Mevcut alanların anlamı değişmez; `AcceptedValue` brüt kalır.

## Out of scope

- Ürün raporu netlemesi; yazarkasa/mali belge.

## Dependencies

- V1-RMD-524

## Acceptance evidence

- Her kabul koşulu için test ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-527/` altındadır.

## Handoff

- None
