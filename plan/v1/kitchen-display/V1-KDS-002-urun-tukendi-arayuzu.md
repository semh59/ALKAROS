# V1-KDS-002 - Mutfak ekranında "Ürün Tükendi Bildir" arayüzü

- Task ID: V1-KDS-002
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Goal

`V1-KIT-008`'in ürettiği `POST /kitchen-operations/products/{id}/suspend`
ucunu ve `V1-IAM-029`'un ürettiği "Mutfak Şefi" rolünü Mutfak ekranına
bağlar. Yalnız Mutfak Şefi oturumunda görünen bir "Ürün Tükendi Bildir"
aksiyonu: açık biletlerdeki ürünleri listeler, seçileni 86'lar, sonucu
(varsa yönetici bildirimi gittiğini) ekranda gösterir. Mockup'ta
(`kitchen-redesign-requirements.md`'de bağlantısı olan prototip artifact'ı)
denenen akışın gerçek koda geçirilmiş hali.

## Owned surface

- src/Clients/PosTerminal/src/features/kitchen-operations/** (Sınırlı ek
  — V1-KDS-001 sahipliğinde kalan dosyalar) — bu görev yalnız 86
  aksiyonuna özgü yeni bileşen(ler) ve `kitchenApi.ts`'e yeni bir çağrı
  ekler, V1-KDS-001'in ürettiği dosya yapısını bozmaz.

## Out of scope

- `V1-KIT-008`/`V1-IAM-029`'un kendisi — bu görev yalnız zaten var olan
  uca/izne bir istemci ekler.

## Dependencies

- V1-KIT-008
- V1-IAM-029
- V1-KDS-001

## Acceptance evidence

- `cd src/Clients/PosTerminal && npx tsc --noEmit` → 0 hata.
- `cd src/Clients/PosTerminal && npx vitest run` → tüm proje yeşil.
- Semih'in elle deneyebileceği senaryo: Mutfak Şefi izniyle bir ürünü
  ekrandan 86'la, Catalog Management'ta ürünün pasif göründüğünü ve
  (plana aykırıysa) yöneticiye giden bildirim kaydını doğrula; "Mutfak
  Personeli" izniyle aynı butonun kilitli olduğunu doğrula.

## Handoff

- None
