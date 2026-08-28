# V1-RMD-030 - Menu-management desktop quality

- Task ID: V1-RMD-030
- Status: Done
- Assignee: /root
- Work type: implementation
- Surface state: Existing

## Goal

Production katalog çalışma alanını; eksiksiz kategori, vergi, ürün, modifier ve effective-price akışları,
authoritative doğrulama ve tutarlı operasyon yoğunluğuyla masaüstü liste/detay/editör sistemi olarak yeniden kurmak.

## Owned surface

- `src/Clients/PosTerminal/src/features/catalog/**`
- `evidence/V1-RMD-030/**`

## Dependencies

- V1-RMD-029
- V1-RMD-014
- V1-RMD-016

## Acceptance evidence

- Aranabilir/filtrelenebilir liste, kalıcı seçim, detay özeti ve isimli editör drawer; V1.1 yayınlama iddiası olmadan
  kategori, vergi profili, ürün kimliği/durumu, modifier atamaları ve effective-price zaman çizelgesini sunar.
- Oluşturma/düzenleme, doğrulama, conflict ve network hatasında form değerlerini korur; dependency'ler yüklenmeden kayıt
  etkinleşmez. Pasif ve geçerli fiyatı olmayan durumlar açıktır ve satılabilir gösterilmez.
- Masaüstü amaçlı liste/detay/editör yoğunluğu kullanır; tablet/mobil erişilebilir drill-down ve focus restoration
  sağlar. Loading, empty, busy, success, error, offline, stale, unauthorized ve conflict durumları sınırlıdır.
- Component/API testleri, tüm PosTerminal testleri, typecheck ve evidence kapsamlı build exit code `0` verir; otonom
  tarayıcı incelemesi görsel hiyerarşi, klavye, 44x44, kontrast, overflow ve breakpoint matrisini kapsar.

## Handoff

- V1-RMD-031
