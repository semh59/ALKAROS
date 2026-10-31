# V1-RMD-779 sonuç

Yönetim ekranına "Masa QR" bölümü eklendi: yalnız `integrations.manage` yetkisi olan oturumda sekme görünür; masa listesi, QR üret / yenile / iptal et, QR'ı ekranda göster ve yazdır.

## Yapılanlar
- `src/Clients/PosTerminal/src/features/management-table-qr/` (istemci `/api/v1/management/qr-tables`, bölüm, `QrCodeSvg`, yazdırma CSS'i, testler); `sections.ts` kaydı, `strings.ts` metinleri.
- QR çizimi: `lean-qr@2.7.4` (Semih onaylı bağımlılık; MIT, sıfır çalışma zamanı bağımlılığı; `docs/engineering/third-party-license-policy.md` MIT'yi incelemesiz izinli sayar; `pnpm add --save-exact`, `package.json` + `pnpm-lock.yaml`, log: `pnpm-add.log`). Modül matrisinden `<svg><path>` çizilir, `innerHTML` yok.
- Ham token yalnız bileşen durumunda (üret/yenile yanıtındaki adres); depolama, telemetri, günlük ve metin olarak ekrana basma yok; pencere kapanınca silinir. Test: DOM'da token ve ana makine adı geçmez.
- Arayüz metni açık: QR yalnızca şimdi görünür, kaybolursa "Yenile" ile yeni QR basılır, eski anında geçersiz olur. Yenile ve iptal onay penceresi ister.
- Yazdırma: `window.print()`; `@media print` yalnız etiketi (masa, bölge, QR, yönerge) gösterir, Yönetim kabuğu gizlenir.
- Hata metinleri sunucunun Türkçe mesajıdır (`RELAY_NOT_READY` vb.), beklenmeyen hatada "İşlem tamamlanamadı."; ham hata gösterilmez.

## Kanıt
- `vitest-table-qr.log`: 15/15. `vitest-full.log`: 87 dosya, 823/823 geçti. `tsc.log`: exit 0. `vite-build.log`: derleme başarılı.
- QR doğruluğu: üretilen SVG, aynı metni kodlayan `lean-qr` matrisiyle modül modül eşit ve üç köşede ISO 18004 bulucu desen (7x7 halka + 3x3 merkez) doğrulanıyor.
- Mutasyonlar (`cp`+`cmp` ile geri alındı; `mut3-qr-transposed.log`: x/y yer değiştirmiş QR -> yönelim testi kırmızı): `mut1-qr-offset.log` (QR kaydırması bozuldu -> desen testi kırmızı), `mut2-rotate-no-confirm.log` (onaysız yenile -> onay testi kırmızı).
- `lint.log`: exit 1, 2 hata + 13 uyarı; hepsi bu görevin dosyaları dışında (`TerminalsPanel.tsx:36`, `CustomerDisplay.tsx:244` hata; diğerleri uyarı), `management-table-qr` için 0 bulgu. Bu iki hata HEAD'de zaten var, görev yüzeyi dışında olduğu için dokunulmadı.
- Gerçek tarayıcıda yazdırma çıktısı veya telefonla okutma denemesi yapılmadı (jsdom + yapı testleri); açık risk.

## İnceleme bulguları (qa-frontend, qa-test-kalitesi)
- Giderildi: sunucu yanıtı kaybolursa (zaman aşımı) liste her durumda yenilenir; yönelim testi (sabit koyu modül, zamanlama desenleri, simetrisizlik) ve transpoze mutantı eklendi; boş liste, yükleme hatası + yeniden dene, iptal/yenile sonrası liste yenileme, Esc ile kapatma ve depolamaya yazılmadığı testleri eklendi.
- Bilerek yapılmadı: pasif masa ekranda işaretlenmiyor (sunucu da engellemiyor); saat dilimi sabitlenmedi (projedeki diğer ekranlarla aynı `tr-TR`); tablo `scope` ve başlık hiyerarşisi mevcut bölümlerle aynı kalıp.
- QR doğruluğu bağımsız bir çözücüyle doğrulanamadı (repoda çözücü yok, yeni bağımlılık eklenmedi); kodlayıcının kendisi `lean-qr`, bileşenin çizimi ISO desenleri ve yönelimle denetleniyor.

## Açık risk
- QR'ın gerçek telefonla okunabilirliği ve yazdırma düzeni elle denenmedi; üretici matris doğrulandı ama sayfa kenar boşluğu/yazıcı ölçeği yazıcıya bağlıdır.
- Ham token yeniden gösterilemez; pencere erken kapanırsa "Yenile" gerekir (bilinçli, 778 kararı).
