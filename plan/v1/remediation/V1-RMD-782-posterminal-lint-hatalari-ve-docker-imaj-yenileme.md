# V1-RMD-782 - PosTerminal lint hataları giderilsin, Docker imajları bugünkü kodla yenilensin

- Task ID: V1-RMD-782
- Status: Planned
- Assignee: claude-code-session_01FMN5WVXi1HJCTEk5argkgZ
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-781 raporu: Docker imaj derlemesi `pnpm build` içindeki `pnpm run lint` adımında iki hatayla kırılıyor, bu yüzden canlı yığın 2026-10-17 imajlarıyla çalışıyor ve sonraki işler (QR yönetimi, Draft kuralı, seçenek zorunluluğu, menü sayaçları vb.) canlıda yok.

1. `TerminalsPanel.tsx`: tablo başlığındaki boş `<th />` (`jsx-a11y/control-has-associated-label`) erişilebilir ad taşımıyor. Eylem sütununa görünmez ama okunabilir ad verilir.
2. `CustomerDisplay.tsx`: eşleştirme kodunun "kaç dakika sonra yenilenir" metni render içinde `Date.now()` çağırıyor (`react/purity`). Dakika sayısı, eşleştirme kodu alındığı anda hesaplanıp durumda tutulur; metin ve davranış aynı kalır.

Ardından imajlar bugünkü HEAD ile yeniden derlenir, yığın mevcut volume'lar korunarak yeniden oluşturulur, yeni migration'ların uygulandığı doğrulanır ve kasa akışı canlıda denenir.

## Owned surface

- `evidence/V1-RMD-782/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/management-security-system/ (V1-RMD-450) — boş sütun başlığına ad, test.
  - src/Clients/PosTerminal/src/routes/CustomerDisplay.tsx (V1-RMD-097) — render içi saat okuması kaldırılır.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. İki lint hatasının kök nedenle giderilmesi; kural kapatma veya bastırma yok; `pnpm run lint` 0 hata; `vitest` ve `tsc` yeşil.
2. Başka dosyada lint hatası çıkarsa yalnız o hata için plan genişletilir.
3. Docker imajlarının yeniden derlenmesi, servislerin volume silmeden yeniden oluşturulması, migration uygulandığının doğrulanması.
4. Canlı doğrulama: sağlık, katalog, taslak, onay, kasaya gönderme, hesap, nakit ödeme; Draft siparişin kasaya gönderimi 409 `ORDER_NOT_SUBMITTED`; yönetici QR uç noktaları erişilebilir; tarayıcıda kasa akışı.

## Out of scope

- Mevcut uyarılar (limit `--max-warnings=20`, şu an 15), `strings.ts`, kural yapılandırması, volume silme, test verisi temizliği.

## Dependencies

- V1-RMD-781

## Acceptance evidence

1. Kırmızı: lint 2 hata. Yeşil: lint 0 hata, vitest ve tsc exit 0.
2. Imaj derlemesi exit 0; yığın sağlıklı; migration kayıtları bugünkü sürümde.
3. Canlı akış gerçek yanıt kodlarıyla `evidence/V1-RMD-782/` altında.
4. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
5. qa-frontend ve qa-test-kalitesi incelemeleri sırayla yapılır, bulgular giderilir.

## Handoff

- None
