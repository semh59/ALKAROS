# V1-RMD-771 - Stok yayını farkı vakasının anahtarı yayın turuyla değişmesin

- Task ID: V1-RMD-771
- Status: Planned
- Assignee: claude-code-session_01FMN5WVXi1HJCTEk5argkgZ
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-739 bulgusu (V12-RMD-009, Yüksek). Kaynak çifti `AvailabilityNotDelivered` vaka anahtarına `desired_version` koyuyor. Ama `AvailabilityPublicationService.RefreshAsync` her turda yeni sürüm çekiyor ve `RecordObservationAsync` adet aynı olsa bile `desired_version`'ı ilerletiyor. Kanala ulaşmayan bir ürünün farkı sürdükçe her yayın turunda anahtar değişiyor: eski vaka "kaynakta görünmüyor" diye otomatik çözülüyor, yeni anahtarla yeni vaka açılıyor. Sonuç: aynı fark için tur başına bir vaka ve vaka seli; V12-RMD-009'un "aynı fark aynı vakayı bulur" amacı üretimde sağlanmıyor. Mevcut testler sürümü elle tohumlayıp yayıncıyı hiç çalıştırmadığı için bunu görmedi.

Çözüm: anahtar, istenen adedin ne zaman değiştiğini adlandırır (`desired_at`, adet değişmedikçe sabit kalır; `RecordObservationAsync` adet aynıyken `desired_at`'i korur). Yayıncı ve sürüm sıralaması değişmez.

## Owned surface

- `evidence/V1-RMD-771/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Reconciliation/OnlineOrders/AvailabilityNotDeliveredSourcePair.cs (V12-REC-001) — kararlı anahtar.
  - tests/Modules/Reconciliation/OnlineOrders/ (V12-REC-001) — testler.
  - plan/v1.2/online-ordering/V12-RMD-009-availability-case-per-divergence.md (V12-RMD-009) — anahtar tanımı.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Anahtar `online-availability:{kanal}:{ürün}:{desired_at mikrosaniye}` olur.
2. Gerçek `AvailabilityPublicationService` ile: adet değişmeden yapılan yayın turları aynı vakayı korur; adet değişince yeni vaka açılır.
3. V12-RMD-009 md'sinde anahtar açıklaması düzeltilir.

## Out of scope

- `AvailabilityPublicationService`, şema/migration, diğer kaynak çiftleri.

## Dependencies

- V12-RMD-009

## Acceptance evidence

1. Yeni test yayıncıyla gerçek turlar çalıştırır; düzeltmeden önce kırmızı, sonra yeşil. Reconciliation.OnlineOrders ve AvailabilityPublishing testleri gerçek Postgres'te yeşil.
2. Mutasyon: anahtarı yeniden `desired_version`'a döndürmek yeni testi kırmızıya çevirir; kaynak `cmp` ile geri alınır.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.

## Handoff

- None
