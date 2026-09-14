# V1-RMD-199 - Mutfak bilet geçiş izin kapısı: sayısal enum kaçağı

- Task ID: V1-RMD-199
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in isteğiyle ("Bağımsız ajanlarla backend, frontend, apı endpoint,
boundary ve database, rol yetki kontrolü yapalım") 6 boyutta paralel
bağımsız ajan denetimi çalıştırıldı, 2026-09-14. API uçları denetimi
GERÇEK, ampirik olarak kanıtlanmış bir yetki-atlama açığı buldu:
`KitchenOperationsEndpoints.TicketTransitionPermission`, hedef durumu
yalnız `"Cancelled"` string'ine (kırpılmış, case-insensitive) karşı
karşılaştırıyordu. Ama gerçek domain ayrıştırması
(`KitchenOperationsStore.ParseEnum`, `Enum.TryParse`) enum'un HAM SAYISAL
değerini de kabul ediyor — `KitchenTicketState.Cancelled` ordinal 4'tür.
Bir `TargetState: "4"` isteği izin kapısında "Cancelled değil" sanılıp
yalnız `kitchen.advance` isteniyordu, ama domain katmanı yine gerçekten
iptal ediyordu — `kitchen.advance`-yalnız (Mutfak Personeli) bir oturum,
`orders.send` gereksinimini sayısal bir hedef durumla atlatabiliyordu.
Bu, 2026-09-13'te bulunan/düzeltilen boşluk-kırpma açığıyla (V1-IAM-028'in
kendi task dosyasında belgeli) AYNI sınıftan, farklı bir temsil (string
yerine sayı) üzerinden hayatta kalan bir kaçak.

**Ampirik kanıt**: `git stash` ile eski koda dönülüp yeni testler
çalıştırıldı — `TargetState: "4"` isteği hem `/tickets/{id}/transition`
hem `/tickets/{id}/items/{id}/transition` uçlarında **200 OK** döndü ve
bileti/kalemi GERÇEKTEN iptal etti (beklenen: 403). `git stash pop` ile
düzeltme geri getirildi, 29/29 test yeşile döndü.

## Owned surface

- src/Host/Experience/KitchenOperations/KitchenOperationsEndpoints.cs
  (Sınırlı ek — V1-RMD-082 sahipliğinde kalan dosya) —
  `TicketTransitionPermission`'ın generic hale getirilip
  `KitchenOperationsStore.ParseEnum`'un kullandığı BİREBİR AYNI
  `Enum.TryParse`/`Enum.IsDefined` çiftini kullanması; artık bu kontrolün
  domain'in gerçek ayrıştırmasından hiçbir zaman sapamaması (yalnızca
  belirli temsilleri (boşluk, büyük/küçük harf) tek tek yamamak yerine).
- tests/Host/Experience/KitchenOperations/KitchenOperationsHttpTests.cs
  (Sınırlı ek) — mevcut whitespace/case teorisine `"4"` varyantı eklendi;
  aynı kaçağın kalem-seviyesi ucunda da (`/items/{id}/transition`) var
  olduğunu kanıtlayan yeni bir teori testi eklendi.

## In scope

1. `TicketTransitionPermission<TTargetState>` — artık `KitchenTicketState`
   veya `KitchenTicketItemState` için generic; string karşılaştırması
   yerine gerçek `Enum.TryParse<TTargetState>(targetState, true, out
   parsed) && Enum.IsDefined(parsed)` kullanıp ayrıştırılan değerin adının
   `"Cancelled"` olup olmadığına bakıyor — domain'in `ParseEnum`'uyla
   birebir aynı mantık, iki ayrı yerde iki ayrı yorumlama riski kalmıyor.
2. Her iki çağıran uç (`/tickets/{id}/transition`,
   `/tickets/{id}/items/{id}/transition`) doğru generic parametreyle
   (`KitchenTicketState`/`KitchenTicketItemState`) güncellendi.

## Out of scope

- Bu oturumun 6 boyutlu denetiminde bulunan diğer bulgular — her biri
  ayrı, dar kapsamlı bir remediation görevi olarak ele alınıyor
  (V1-RMD-200, V1-RMD-201, V1-RMD-202 — bkz. memory kaydı).

## Dependencies

- None

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → **0 uyarı, 0 hata**.
- `dotnet test tests/Host/Experience/KitchenOperations` → gerçek
  Postgres'e karşı **29/29 yeşil** (26 mevcut + 3 yeni: bilet-seviyesi
  `"4"` varyantı mevcut teoriye eklendi, kalem-seviyesi uç için yeni bir
  teori testi (`"Cancelled "` + `"4"`) eklendi).
- **Mutasyon/PoC kanıtı**: `git stash` ile düzeltme öncesi koda dönülüp
  yeni testler çalıştırıldı → 2 test GERÇEKTEN kırmızıya düştü (`Expected:
  Forbidden, Actual: OK` — hem bilet hem kalem ucunda). `git stash pop`
  ile geri getirilip 29/29 yeşile dönüldüğü doğrulandı.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.
- `python tools/consistency-audit/consistency_audit.py` → `clean`.

## Handoff

- None
