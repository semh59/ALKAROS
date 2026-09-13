# V1-IAM-029 - Mutfak Şefi rolü

- Task ID: V1-IAM-029
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Goal

Semih'in kararı (2026-09-13): mevcut "şef garson" (`supervisor`, önyüz kat
sorumlusu) ile karıştırılmayacak, bağımsız bir "Mutfak Şefi" (executive
chef) rolü açılır. Bu rol, düz "Mutfak Personeli"nin (`V1-IAM-028`)
yapamadığı her şeyi taşır: iptal/sorun bildir (mevcut `orders.send`),
yazdırma kurtarma onay/red (mevcut `kitchen.reprint`), ürün tükendi
bildirimi (`kitchen.availability.suspend`, `V1-KIT-008`). Bu görev,
V1-KIT-008'in ürettiği izin gerçekten var olduktan SONRA çalışır — aksi
halde erişilemeyen bir uca izin vermiş oluruz (bağımsız denetim Y2).

## Owned surface

- Yeni migration (`database/migrations/V1/V1-IAM-029/`) — `roles`
  tablosuna "Mutfak Şefi" satırı; `role_permissions`'a `orders.send`,
  `kitchen.advance`, `kitchen.reprint`, `kitchen.availability.suspend`
  atamaları.
- `docs/domain/authorization-model.md` — rol/izin tablosuna "Mutfak Şefi"
  sütunu.

## Out of scope

- Yazıcı/rota yönetimi izninin (`kitchen.routing.manage`) Mutfak Şefi'ne
  verilip verilmeyeceği — gereksinim dokümanında "muhtemelen ✅" olarak
  not edilmiş ama kesinleşmemiş açık bir soru; bu görev bunu içermez,
  Semih netleştirince ayrı bir migration/karar olarak eklenir.
- Frontend rol seçici/oturum açma akışına "Mutfak Şefi" seçeneğinin
  eklenmesi (`V1-KDS-002`).

## Dependencies

- V1-KIT-008

## Acceptance evidence

- Migration'ın ileri/geri (up/down) ikisi de boş veritabanında denenir.
- `dotnet test` → rol/izin okuma testleri (varsa mevcut
  `tests/Modules/Identity` projesindeki ilgili test sınıfı) yeni rolü
  doğru izin kümesiyle döndürür.
- Semih'in elle deneyebileceği senaryo: "Mutfak Şefi" rolüne atanmış bir
  kullanıcıyla oturum aç, hem bir kalemi iptal et hem bir ürünü 86'la,
  ikisinin de 200 döndüğünü doğrula.

## Handoff

- None
