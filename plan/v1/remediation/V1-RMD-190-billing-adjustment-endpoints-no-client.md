# V1-RMD-190 - `/discount`, `/tip`, `/adjustments`'ın hiçbir istemcisi yok

- Task ID: V1-RMD-190
- Status: NotApplicable
- Assignee: Claude Sonnet 5
- Work type: decision
- Surface state: Existing

## Source basis

- PO:2026-09-12

## Goal

2026-09-12 tarihli beş-ajanlı bağımsız Garson audit'inin API endpoint
boyutundaki bulgusunu ele alır: `POST /bills/{billId}/discount`,
`POST /bills/{billId}/tip`, `GET /bills/{billId}/adjustments`
(`src/Host/Experience/Billing/BillingSplitApplication.cs`) sunucu
tarafında gerçek, tam test edilmiş, çalışan uç noktalar (V1-RMD-103'ün
B1 bulgusuyla wired edilmişti) — ama hiçbir istemci (WaiterPwa,
PosTerminal/Cashier) hiçbirini hiç çağırmıyor.

Karar kimliği: PO:2026-09-12 (Semih'in "Tümünü düzeltme planı yapalım.
Sırayla bana sormadan bitir" talimatı altında verilmiş bir mühendislik
kararı).

Bu, `/comp` ve `/transfer-server`'ın daha önce aynı şekilde bulunup
V1-RMD-177'de kapatıldığı durumdan yapısal olarak farklı: o ikisinin
istemci tarafı, mevcut sheet'lere birer düğme/akış eklemekle (görece
küçük bir iş) çözülmüştü. Burada indirim/bahşiş/düzeltme geçmişi,
kasiyer tarafında hiç var olmayan bir UI YÜZEYİ gerektiriyor — tutar
girişi, gerekçe seçimi (`bills.discount` grant-class akışının kendi
onay/yönetici-eskalasyon UI'ı dahil), düzeltme geçmişi listesi. Bu bir
"düzeltme" değil, tasarım gerektiren yeni bir özellik işi — AGENTS.md'nin
"gizli kapsam genişletme yasaktır" kuralına birebir girer; bir
remediation görevi içine sıkıştırılıp aceleye getirilirse hem tasarım
hem test kapsamı zarar görür. Bu yüzden şimdilik bilinçli bir kapsam
kararı olarak kayda geçiriliyor: sunucu tarafı zaten hazır ve test
kapsamlı bekliyor; kasiyer tarafı UI'ı kendi tasarımını (Semih'in
onayıyla) gerektiren ayrı bir özellik görevi olmalı.

## Owned surface

- `plan/v1/remediation/V1-RMD-190-billing-adjustment-endpoints-no-client.md` (yeni)

## Out of scope

- PosTerminal/Cashier'a indirim/bahşiş/düzeltme-geçmişi UI'ı eklemek —
  ayrı bir özellik görevi (tasarım + Semih onayı gerektirir).
- WaiterPwa'ya aynı UI'ı eklemek — bu üç uç nokta zaten kasiyer/yönetici
  işi (`bills.discount` grant-class), garson tarafının kapsamında değil.

## Dependencies

- V1-RMD-103

## Acceptance evidence

- Karar kimliği: bu görevin kendisi (V1-RMD-190), 2026-09-12.
- Doğrulama: `grep -rn "discount\|/tip\|adjustments" src/Clients` —
  `PosTerminal/src/routes/Cashier.tsx`'te tek eşleşme salt okunur
  `activeOrder.discountTotal` görüntüleme satırı; `WaiterPwa`'da tek
  eşleşme açıklayıcı bir yorum satırı. Hiçbir gerçek `fetch`/`api()`
  çağrısı üç uç noktanın hiçbirine gitmiyor.
- Kod/artifact üretilmeme nedeni: yukarıdaki Goal bölümü — bu bir hata
  değil, tasarım gerektiren eksik bir özellik; bu görevin kapsamı
  yalnızca bulguyu kayda geçirmek.

## Handoff

- None
