# V1-KIT-007 - Bir kalem hazırlanmaya başlayınca bileti örtük kabul etme

- Task ID: V1-KIT-007
- Status: Done
- Assignee: Claude Sonnet 5
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
- docs/domain/lifecycle-transition-contracts.md (Sınırlı ek — V0-DOM-001
  sahipliğinde kalan, Semih onaylı resmi karar kaydı) — bağımsız denetimde
  (2026-09-13) bulundu: bu dokümanın KitchenTicket "Allowed transitions"
  satırı yalnız `Queued→Accepted`'ı listeliyordu, yeni gerçek çalışma-zamanı
  geçişi olan `Queued→Preparing` (örtük kabul) eklenmedi. Satır ve narrative
  örnek (`Positive:` bölümü) güncellendi; PDF kaynağı/onay tarihi
  değişmedi, yalnız zaten Semih onaylı olan bu davranışın dokümana
  yansıtılması eklendi.

## Out of scope

- Mevcut ayrı "Kabul et" ticket-transition ucu (`POST
  /tickets/{id}/transition` ile `TargetState: "Accepted"`) kaldırılmıyor —
  hâlâ geçerli bir yol olarak kalır (ör. bir yönetici tüm bileti tek
  seferde kabul etmek isterse), yalnızca artık zorunlu ilk adım değil.
- Frontend değişikliği (`V1-KDS-001`) — bu görev yalnız domain'i düzeltir.

## Dependencies

- None

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → **Oluşturma başarılı oldu. 0
  Uyarı, 0 Hata.**
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres dotnet
  test tests/Modules/Kitchen/TicketLifecycle/ALKAROS.Kitchen.TicketLifecycle.Tests.csproj
  -c Release` → gerçek Postgresql'e karşı **Başarılı! Başarısız: 0,
  Başarılı: 24, Atlanan: 0, Toplam: 24** (21 mevcut + 3 yeni:
  `QueuedTicketIsImplicitlyAcceptedWhenItsOnlyItemStartsPreparing`,
  `QueuedTicketWithMultipleItemsIsImplicitlyAcceptedWhenOneItemStartsPreparing`,
  `ExplicitAcceptStillWorksAndItsAcceptedAtIsNotOverwrittenByLaterItemProgress`).
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres dotnet
  test tests/Host/Experience/KitchenOperations/ALKAROS.Host.Experience.KitchenOperations.Tests.csproj
  -c Release` → gerçek Postgresql'e karşı geçti (test daha sonra
  V1-IAM-028'de `KitchenAdvanceOnlySessionCanAdvanceButNotCancel` olarak
  yeniden adlandırıldı ve kitchen.advance-yalnız senaryosunu da kapsayacak
  şekilde genişletildi — bkz. o görevin Acceptance evidence'ı).
- `python tools/consistency-audit/consistency_audit.py` → `clean`.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı. **Düzeltme notu (2026-09-13, bağımsız denetim sonrası):**
  `docs/domain/lifecycle-transition-contracts.md`'nin bu görevle senkron
  olmadığı bulundu ve yukarıdaki Owned surface'a eklenerek güncellendi;
  audit bu ek dosyayla birlikte yeniden çalıştırılıp tekrar 0 hata/0
  uyarı doğrulandı.
- Semih'in elle deneyebileceği senaryo: yeni bir sipariş gönder (ticket
  `Queued`), mutfak ekranından tek bir kalemi doğrudan "Hazırlanıyor"a al,
  ticket'ın "Kabul Edildi" adımını atlamadan "Hazırlanıyor" durumuna
  geçtiğini doğrula (bu davranış artık `ItemStartedDirectlyFromQueuedTicketImplicitlyAcceptsTheTicket`
  ile gerçek HTTP çağrısıyla kanıtlanmış durumda).

## Handoff

- None
