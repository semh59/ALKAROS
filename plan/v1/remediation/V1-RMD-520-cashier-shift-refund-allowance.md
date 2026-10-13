# V1-RMD-520 - Kasiyer vardiya iade bütçesi (sunucu)

- Task ID: V1-RMD-520
- Status: InProgress
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-14

## Goal

PO kararı: müdür, her kasa vardiyası için kasiyere başlangıç kasası gibi bir iade bütçesi verir; vardiya başında bütçe 0'dır. Kasiyer bütçe içindeki iadeyi (nakit veya kartlı elle iade) kendisi tamamlar ve tamamladığı iade bütçeden düşer; bütçe yetmezse iade yine müdür onayına gider. Müdürün onayladığı iade bütçeden düşmez. Bütçe kasa oturumu kapanınca biter.

## Owned surface

- `plan/v1/remediation/V1-RMD-520-cashier-shift-refund-allowance.md`
- `evidence/V1-RMD-520/**`
- `database/migrations/V1/V1-RMD-520/185-refund-allowance.up.sql`
- `database/migrations/V1/V1-RMD-520/185-refund-allowance.down.sql`
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Manifest/ManifestTests.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Billing/PaymentRefunds.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Billing/BillingSplitApplication.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Billing/BillingSplitApplication.Allowance.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Payments/PaymentAggregate/PostgresRefundRequestRepository.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Billing/RefundAllowanceHttpTests.cs
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Kasa oturumuna iade bütçesi ve kullanılan tutar; bütçe kullanımı tek atomik güncellemeyle yapılır (yarış durumunda bütçe aşılmaz).
- İade isteği açılırken kasiyerin açık oturumunda yeterli bütçe varsa: nakit iade hemen tamamlanır (kasadan çıkış, ödeme ters kaydı, gerekirse hesap iptali, denetim kaydı); kartlı elle iade bütçe ayrılıp fiş numarası bekler. Bütçe yetmezse mevcut müdür onay akışı aynen sürer.
- `ck_refund_requests_decider_not_requester` yalnız bütçeden tamamlanan iade için gevşer (`approval_mode`).
- Müdür ucu: kasiyerin açık oturumuna bütçe verme/değiştirme (`payments.refund.approve`; kullanılandan az olamaz); kasiyer ucu: kalan bütçeyi okuma.
- Varsayım (PO'ya sorulmadı, kayıtlı): bütçe şimdilik mevcut tam iade isteklerine uygulanır; kısmi iade V1-RMD-519 zincirinde gelir.
- Bilinen sınır: bütçe ayrılıp kartlı iade hiç tamamlanmazsa ayrılan tutar vardiya sonuna kadar geri gelmez.
- Gerçek Host + Postgres HTTP testi.

## Out of scope

- Kullanıcı arayüzü (V1-RMD-521), kısmi iade, stoğa geri dönüş.

## Dependencies

- V1-RMD-508

## Acceptance evidence

- Her kabul koşulu için test ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-520/` altındadır.

## Handoff

- None
