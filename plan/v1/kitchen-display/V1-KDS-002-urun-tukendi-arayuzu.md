# V1-KDS-002 - Mutfak ekranında "Ürün Tükendi Bildir" arayüzü

- Task ID: V1-KDS-002
- Status: Done
- Assignee: Claude Sonnet 5
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
- src/Clients/PosTerminal/src/routes/workspace.tsx (Sınırlı ek, paylaşılan
  — V1-KDS-001 sahipliğinde kalan dosya) — `KitchenRoute`'a
  `canSuspendAvailability` (kitchen.availability.suspend) ve
  `onSuspendProductAvailability` prop'larını ekler; V1-KDS-001'in kendi
  `canAdvance`/`canOperate` kapı mantığını değiştirmez.

## Out of scope

- `V1-KIT-008`/`V1-IAM-029`'un kendisi — bu görev yalnız zaten var olan
  uca/izne bir istemci ekler.

## Dependencies

- V1-KIT-008
- V1-IAM-029
- V1-KDS-001

## Acceptance evidence

- `cd src/Clients/PosTerminal && npx tsc --noEmit` → **0 hata** (doğrulandı).
- `cd src/Clients/PosTerminal && npx vitest run` → **Test Files 23 passed
  (23), Tests 153 passed (153)** — tüm proje, izole değil (4 yeni test:
  buton kilitli/etkin görünümü, `onSuspendProductAvailability` prop'u hiç
  verilmediğinde butonun tamamen gizlenmesi, açık biletlerden ürün seçip
  86'lama + plana-aykırı durumda "yöneticiye bildirim kaydı düşüldü"
  mesajının gösterilmesi, plana aykırı OLMAYAN durumda bu mesajın
  gösterilMEmesi).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı
  (doğrulandı).
- `python tools/consistency-audit/consistency_audit.py` → `clean`
  (doğrulandı; ilk yazımda 4 Türkçe-karakter-in-İngilizce-yorum ihlali
  bulundu ve düzeltildi).
- Uygulanan tasarım: header'da her zaman görünen (`onSuspendProductAvailability`
  tanımlıysa) ama yalnız `canSuspendAvailability` (kitchen.availability.
  suspend) ile etkin olan "Ürün Tükendi Bildir" düğmesi — kilitliyken görünür
  kalır, gizlenmez (bu görevin kendi kabul senaryosunun tam istediği gibi).
  Tıklanınca açık biletlerdeki (iptal edilmemiş kalemlerin) benzersiz
  ürünlerini listeleyen bir seçim modalı açılır; onaylanınca
  `KitchenOperationsClient.suspendProductAvailability` çağrılır, backend'in
  döndürdüğü `planConflict` alanına göre ekranda "...yöneticiye bildirim
  kaydı düşüldü" notu gösterilir ya da gösterilmez.
- Semih'in elle deneyebileceği senaryo: Mutfak Şefi izniyle bir ürünü
  ekrandan 86'la, Catalog Management'ta ürünün pasif göründüğünü ve
  (plana aykırıysa) yöneticiye giden bildirim kaydını doğrula; "Mutfak
  Personeli" izniyle aynı butonun (gizli değil, kilitli) göründüğünü
  doğrula.

## Handoff

- None
