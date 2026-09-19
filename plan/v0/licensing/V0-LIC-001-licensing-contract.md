# V0-LIC-001 - Define offline-safe licensing contract

- Task ID: V0-LIC-001
- Status: Blocked
- Assignee: codex-v0-lic-001
- Work type: decision
- Surface state: Existing

## Source basis

- PDF:II.2.24
- PDF:III.26
- PO:2026-09-18

## Goal

Tek seferlik license activation, machine binding, offline authorization, transfer, support update ve failure davranışını
tanımlamak.

## Owned surface

- `docs/licensing/licensing-contract.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Lisans imzası/karma, şube sınırı, kurulum aktarımı, saat geri alma, çevrimdışı işlem ve kurtarma sahipliği.

## Out of scope

- Abonelik faturalandırması, DRM bypass veya uzaktan kapatma davranışı işletme tarafından onaylanmadı.

## Dependencies

- V0-ARC-002
- V0-CMP-003

## Blocker

- `plan/GATES.md`'deki 2026-08-03 kullanıcı onaylı devir listesi (`V0_DEFERRED_TASKS`)
  bu görevi `V20` reopen stage'ine, "Gerçek license server ve lisans sözleşmesi kanıtı"
  şartıyla erteler: "Devredilen görev `Blocked` durumunda kalır ... görev ilgili aşama
  gate'inde gerçek kanıtla `Done` ... olur." Named product owner/legal approver'ın
  politika kararı 2026-09-18'de alındı ve `docs/licensing/licensing-contract.md`'ye
  yazıldı (aktivasyon birimi, machine binding, offline grace, transfer/kurtarma,
  geçersiz lisans davranışı) — ancak bu, gerçek bir çalışan lisans sunucusu ve gerçek
  bir imzalı lisans sözleşmesi kanıtı değildir. `V20-LIC-001` gerçek lisans sunucusunu
  inşa edip gerçek bir kurulumla bu politikayı doğruladığında görev `Done` yapılabilir.

## Deliverables

- V0-LIC-001 için tek decision record: kaynak + erişim tarihi + onaylayan + seçilen sonuç + reddedilen alternatifler +
  etkilenen task kimlikleri.
- Pozitif/negatif örnekler ve rejected alternatives.
- Tüketici görevler için test edilebilir invariant/output listesi.

## Acceptance evidence

- Lisanslama hizmetinin kaybı, ana restoranın faaliyetlerini beklenmedik bir şekilde durduramaz; geçersiz lisans
  davranışı ve kurtarma açıktır.

## Handoff

- V20-LIC-001
- V20-LIC-002
