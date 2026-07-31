# V20-UAT-001 - Accept service workflows

- Task ID: V20-UAT-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Planned

## Source basis

- PDF:I.45-I.54

## Goal

Release candidate üzerinde cashier, waiter, Table, Order, kitchen, QR, online-order operations ve printing
workflow'ları için named user acceptance toplamak.

## Owned surface

- `release/evidence/uat/service/**`
- Bu görev ürün kodunu veya acceptance sonucunu değiştiremez.

## In scope

- Role-based scenario script'leri, success/failure flow'ları, concurrent Table/Order state'leri, kitchen routing,
  QR PendingConfirmation, Yemeksepeti kabul/ret/iptal kuyruğu ve printer recovery.

## Out of scope

- Payment closure, inventory accounting, defect fix ve production kullanımı.

## Dependencies

- V20-REL-001
- V20-INT-005
- V20-INT-006
- V20-INT-003
- V14-OUI-001
- V0-CMP-005

## Deliverables

- Çalıştırılmış named scenario script'leri, participant sign-off kayıtları ve defect reference listesi.

## Acceptance evidence

- Her zorunlu senaryonun geçiş kanıtı ve adlandırılmış kabulü vardır; başarısız scenario script'leri kabul edildi olarak
  işaretlenmek yerine engellemeye devam eder.
- Yetkili restaurant personeli online-order kuyruğunda kabul, ret ve iptal senaryolarını gerçek provider
  certification sonucu üzerinde tamamlar; arayüz sonucu authoritative Order status ile eşleşir.
- Kullanıcı akışları `V0-CMP-005` kararındaki surface-specific accessibility kriterlerini karşılar.
- `V20-REL-001` kanıtlı `NotApplicable` ise release adayı paketlemesi beklenmez; scenario çalıştırma ve kabul kanıtı
  kuralı yine geçerlidir.

## Handoff

- V20-UAT-003
