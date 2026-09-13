# V1-KIT-007 - Bir kalem hazırlanmaya başlayınca bileti örtük kabul etme

- Task ID: V1-KIT-007
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in kararı (2026-09-13, "personele işini hızlandırsın sistem
istiyorum"): Mutfak ekranı yeniden tasarımında kalem bazlı ilerleme dört
aşamada kalacak (Bekliyor→Hazırlanıyor→Hazır→Servis Edildi), ayrı bir
"Kabul Et" tıklaması eklenmeyecek. Bağımsız denetimde (2026-09-13, K1)
bulunan gerçek boşluk: bugün `KitchenTicket.UpdateItemStatus`'un
auto-promote kuralı (`KitchenTicket.cs:191`) yalnız ticket ZATEN
`Accepted` ise bir kalemin `Preparing`'e geçişini ticket'a yansıtıyor;
ticket hâlâ `Queued` iken doğrudan bir kalemi `Preparing`'e almak
ticket'ı sonsuza dek `Queued` bırakabiliyor (kalemler Ready/Served olsa
bile). Bu görev, ticket `Queued` iken bir kalem `Preparing`'e geçtiğinde
ticket'ın da örtük olarak `Accepted` sayılıp aynı adımda `Preparing`'e
taşınmasını `KitchenTicket` domain modeline ekler.

## Owned surface

- src/Modules/Kitchen/TicketLifecycle/KitchenTicket.cs (Sınırlı ek —
  V1-RMD-074 sahipliğinde kalan dosya) —
  `UpdateItemStatus`'un ticket-durumu hesaplama bloğu: `Status == Queued
  && newItemStatus == Preparing` durumunda `newTicketStatus = Preparing`
  (mevcut `Accepted && Preparing → Preparing` kuralıyla birleştirilir).
  `AcceptedAt` alanının bu örtük geçişte de doldurulması (bugün yalnız
  `TransitionTo(Accepted)`'ta dolduruluyor).
- src/Modules/Kitchen/TicketLifecycle/KitchenTicketTests.cs (Sınırlı ek —
  V1-RMD-074 sahipliğinde kalan dosya) — yeni testler: (a) `Queued` bir
  ticket'ın tek kalemi `Preparing`'e alınınca ticket'ın `Preparing`
  olduğu ve `AcceptedAt`'ın dolduğu, (b) çok kalemli bir ticket'ta bir
  kalem `Preparing` diğerleri `Queued` iken ticket'ın yine `Preparing`
  olduğu, (c) mevcut `TransitionTo(Accepted)` yolunun (garson/kasiyer'in
  ayrı "Kabul et" çağrısı, hâlâ var olan bir yol) davranışının
  değişmediği.
- tests/Host/Experience/KitchenOperations/KitchenOperationsHttpTests.cs
  (Sınırlı ek — V1-RMD-082 sahipliğinde kalan dosya) — en az bir
  HTTP-seviyesi regresyon testi: `Queued` bir ticket'ın kalemi doğrudan
  `Preparing`'e geçirilince ticket DTO'sunun `status: "Preparing"`
  döndüğü.

## Out of scope

- Mevcut ayrı "Kabul et" ticket-transition ucu (`POST
  /tickets/{id}/transition` ile `TargetState: "Accepted"`) kaldırılmıyor —
  hâlâ geçerli bir yol olarak kalır (ör. bir yönetici tüm bileti tek
  seferde kabul etmek isterse), yalnızca artık zorunlu ilk adım değil.
- Frontend değişikliği (`V1-KDS-001`) — bu görev yalnız domain'i düzeltir.

## Dependencies

- None

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `dotnet test tests/Modules/Kitchen/ALKAROS.Kitchen.Tests.csproj` ve
  `tests/Host/Experience/KitchenOperations` → yeni testler dahil tümü
  yeşil.
- Semih'in elle deneyebileceği senaryo: yeni bir sipariş gönder (ticket
  `Queued`), mutfak ekranından tek bir kalemi doğrudan "Hazırlanıyor"a al,
  ticket kartının "Kabul Edildi" adımını atlamadan "Hazırlanıyor"
  gösterdiğini doğrula.

## Handoff

- None
