# V11-UI-001 - Implement menu and recipe administration UI

- Task ID: V11-UI-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.21.1-I.21.4

## Goal

Statik/günlük menü ve değişmez tarif versiyonu oluşturma/aktivasyon için Türkçe ekranları uygulayın.

## Owned surface

- `src/Clients/Cashier/MenuRecipeAdmin/**`, `tests/Clients/Cashier/MenuRecipeAdmin/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Menü kompozisyonu, günlük öğe kurulumu, içerik/birim doğrulaması, sürüm aktivasyonu ve izin hataları.

## Out of scope

- Production yürütme, stok ayarlaması ve satın alma.

## Dependencies

- V11-MNU-001
- V11-MNU-003
- V11-RCP-001
- V1-IAM-002
- V0-CMP-005

## Deliverables

- `src/Clients/Cashier/MenuRecipeAdmin/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Public contract ve otomatik başarı/ret/concurrency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- UI başvurulan sürümü düzenleyemez; birim/boyut hatası görülüyor; kaydedilen veriler sunucudan aynı şekilde yeniden
  yüklenir.
- Yönetim ekranları `V0-CMP-005` kararındaki operations UI success criteria listesini karşılar.

## Handoff

- V11-UI-002
