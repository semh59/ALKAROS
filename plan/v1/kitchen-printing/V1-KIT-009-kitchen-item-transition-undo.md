# V1-KIT-009 - Mutfak kalemi için kısa pencereli geri alma (undo)

- Task ID: V1-KIT-009
- Status: Done
- Assignee: Claude Sonnet 5
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
3. **Karar (bu görevde netleştirildi): ticket-seviyesi durum geri alma
   sonrası OTOMATİK DÜŞÜRÜLMEZ.** Domain'de zaten hiçbir yerde bir
   "demotion" yolu yok (tek regresyon auto-cancel — tüm kalemler iptal
   olunca); yeni bir düşürme kuralı icat etmek (birden fazla kalemden
   hangisinin ticket aşamasını "belirlediği" gibi kendi kenar durumlarını
   getirirdi) — ekranın zaten kalem durumunu doğrudan gösterdiği, ticket
   durumunu bir üst sınır olarak okumadığı göz önüne alınınca gereksiz.
   Gerekçe `KitchenTicket.UndoItemStatus`'un kendi doc comment'inde.
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

- `dotnet build ALKAROS.slnx -c Debug` → **Oluşturma başarılı oldu. 0
  Uyarı, 0 Hata.**
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres dotnet
  test tests/Modules/Kitchen/TicketLifecycle/ALKAROS.Kitchen.TicketLifecycle.Tests.csproj
  -c Release` → **Başarılı! Başarısız: 0, Başarılı: 28, Atlanan: 0,
  Toplam: 28** (24 mevcut + 4 yeni: pencere içinde geri alma +
  `ReadyAt`/`ServedAt` doğru temizlenmesi, Served→Ready geri alırken
  erken `ReadyAt`'ın korunması, pencere dolduktan sonra reddedilme,
  `Queued`'dan geri almanın reddedilmesi).
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres dotnet
  test tests/Host/Experience/KitchenOperations/ALKAROS.Host.Experience.KitchenOperations.Tests.csproj
  -c Release` → **Başarılı! Başarısız: 0, Başarılı: 15, Atlanan: 0,
  Toplam: 15** (13 mevcut + 2 yeni: `kitchen.advance`-yalnız oturumun
  kendi hatasını `orders.send` olmadan geri alabildiği, `Queued`'dan geri
  almanın 409 DOMAIN_CONFLICT verdiği).
- `python tools/consistency-audit/consistency_audit.py` → `clean`.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.
- Uygulanan tasarım: `KitchenTicketItem.UndoWindow` = 10 sn,
  `CanUndo(now)`/`Undo(timestamp)`; `KitchenTicket.UndoItemStatus`;
  yeni `POST .../items/{itemId}/undo` (`UndoKitchenItemV1`),
  `kitchen.advance` izniyle korunuyor; `KitchenOperationsStore.UndoItemAsync`
  `TransitionItemAsync`'in aynı state-sync/bildirim yolunu yeniden
  kullanıyor (Served→Ready geri alma "hazır" bildirimini doğru şekilde
  yeniden tetikliyor).
- Semih'in elle deneyebileceği senaryo: bir kalemi yanlışlıkla "Hazır"a
  ilerlet, birkaç saniye içinde geri al, kalemin "Hazırlanıyor"a
  döndüğünü doğrula; aynı işlemi 10 saniye geçtikten sonra dene,
  reddedildiğini doğrula.

## Handoff

- None
