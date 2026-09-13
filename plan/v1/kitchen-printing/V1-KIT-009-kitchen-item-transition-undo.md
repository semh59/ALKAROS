# V1-KIT-009 - Mutfak kalemi için kısa pencereli geri alma (undo)

- Task ID: V1-KIT-009
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Goal

Rakip KDS araştırmasında (2026-09-13, `[[kitchen-redesign-requirements]]`)
bulunan, endüstri genelinde yaygın bir örüntü: bir mutfak kaleminin
yanlışlıkla ilerletilmesi durumunda kısa bir pencerede geri alınabilmesi
(genel KDS emsali: "Once five seconds have elapsed after an item
completion, the completion can no longer be undone... an Undo button
appears next to completed items"). ALKAROS'ta bugün bu yol tamamen kapalı:
`KitchenTicketItem.CanTransitionTo` yalnız ileri geçişe izin veriyor, tek
telafi yolu tüm bileti iptal etmek — bu hem orantısız hem `orders.send`
(Mutfak Şefi) gerektirdiği için düz "Mutfak Personeli" bunu bile yapamıyor.
Bu görev, bir kalemin **son geçişinden itibaren kısa bir zaman penceresi
içinde** bir aşama geri alınabilmesini domain'e ekler.

## Owned surface

- src/Modules/Kitchen/TicketLifecycle/KitchenTicket.cs,
  KitchenTicketItem.cs, KitchenEnums.cs (Sınırlı ek — V1-RMD-074
  sahipliğinde kalan dosyalar) — geri alma penceresi sabiti/mantığı, yeni
  bir `UndoLastTransition(DateTimeOffset now)` (veya benzeri) domain
  metodu.
- src/Host/Experience/KitchenOperations/KitchenOperationsEndpoints.cs,
  KitchenOperationsStore.cs, KitchenOperationsContracts.cs (Sınırlı ek —
  V1-RMD-082 sahipliğinde kalan dosyalar) — yeni bir undo ucu/hedef durum
  değeri; `TicketTransitionPermission` (V1-IAM-028) mantığına göre
  `kitchen.advance` yeterli olmalı (bu bir iptal değil, kendi hatasını
  düzeltme — Mutfak Personeli de kullanabilmeli).
- tests/Modules/Kitchen/TicketLifecycle/KitchenTicketTests.cs,
  tests/Host/Experience/KitchenOperations/KitchenOperationsHttpTests.cs
  (Sınırlı ek, ilgili dosyalar) — yeni testler.

## In scope

1. Yalnız kalemin **en son** geçişi geri alınabilir (çok adımlı geri
   sarma yok) ve yalnız o geçişten itibaren sabit bir süre penceresi
   içinde (öneri: 10 sn — kesin değer bu görevde kararlaştırılır).
2. Pencere dolduktan sonra istek 409/400 ile reddedilir (backend
   otoriter, istemci yalnız gösterir).
3. Ticket-seviyesi durumun geri alma sonrası yeniden hesaplanması —
   V1-KIT-007'nin auto-promote/auto-ready mantığıyla simetrik (bir kalem
   `Preparing`'den `Queued`'a dönerse ve bu ticket'ın örtük kabulünü
   sağlayan TEK kalemse, ticket da `Queued`'a dönmeli mi? Bu görevde
   netleştirilecek açık bir tasarım sorusu).
4. `kitchen.advance` izni yeterli — Mutfak Personeli kendi hatasını
   Mutfak Şefi'ne ihtiyaç duymadan düzeltebilmeli.

## Out of scope

- Aktör bazlı kısıtlama ("yalnız BEN yaptığım geçişi geri alabilirim") —
  domain bugün kalem geçişlerinde aktör/kullanıcı kimliği taşımıyor, bunu
  eklemek ayrı ve daha büyük bir iş; bu görev yalnız zaman penceresine
  dayanır (genel KDS emsaliyle aynı: kim yaptığından bağımsız, süre
  bazlı).
- Frontend arayüzü (`V1-KDS-003`).
- Ticket-seviyesi (mutfak şefinin "Kabul et"/"Hazır" gibi ayrı ticket
  transition'ları) için undo — yalnız kalem (item) seviyesi.

## Dependencies

- V1-KIT-007
- V1-IAM-028

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `dotnet test` → yeni testler dahil ilgili projeler yeşil, gerçek
  Postgres'e karşı; en az bir test pencere dolduktan sonra undo'nun
  reddedildiğini kanıtlar.
- Semih'in elle deneyebileceği senaryo: bir kalemi yanlışlıkla "Hazır"a
  ilerlet, birkaç saniye içinde geri al, kalemin "Hazırlanıyor"a
  döndüğünü doğrula; aynı işlemi pencere geçtikten sonra dene, reddedildiğini
  doğrula.

## Handoff

- None
