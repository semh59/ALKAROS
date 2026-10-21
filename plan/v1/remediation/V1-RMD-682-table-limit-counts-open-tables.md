# V1-RMD-682 - Masa sınırı doğru sayar ve masa açarken Türkçe uyarı verir

- Task ID: V1-RMD-682
- Status: Done
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-21

## Goal

V1-RMD-598 bulguları N1 ve R1 kapanır. Garson başına masa sınırı, o garsonun açık masalarını (ödenmemiş, iptal ve ret dışı) sayar; servis edilmiş ama ödenmemiş masa da sayılır. Garson yeni bir masa açarken veya masayı devralırken sınırdaysa işlem Türkçe uyarıyla durur; sınır 0 ise (varsayılan) hiçbir şey değişmez. Aynı masaya ek sipariş sınıra takılmaz.

## Owned surface

- `plan/v1/remediation/V1-RMD-682-table-limit-counts-open-tables.md`
- `evidence/V1-RMD-682/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/SuggestedWaiterResolver.cs - yalnız sayım ve sayaç yardımcısı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/OrderManagementEndpoints.cs - yalnız masa açma ve devir sınır denetimi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Orders/TableDraft/OrderManagementTableDraftHttpTests.cs - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/PendingOrderNotifications/SignalRPendingOrderAnnouncerTests.cs - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-598-settings-quality-audit.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Doğru sayım, masa açma ve toplu devirde sınır denetimi, Türkçe uyarı metni; testler (sınır 3, 4. masa, aynı masaya ek sipariş, sınır 0, servis edilmiş masa).

## Out of scope

- Ayar düzenleme ekranı (V1-RMD-684); sınırın istemcide rozet olarak gösterilmesi (V1-RMD-684).

## Dependencies

- V1-RMD-598

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-682/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-683
