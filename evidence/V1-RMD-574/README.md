# V1-RMD-574 - PosTerminal lint kapısı

Bulgu: `pnpm build` (Docker `api` imajının parçası) `oxlint` kapısında 5 hatayla duruyordu: V1-RMD-548 (yolculuk tablosu: 2 boş başlık + çubuk hücresi), V1-RMD-565 (hata listesi: 1 boş başlık) ve V1-RMD-531'den kalan `Cashier.tsx:72` (oturum referansı render sırasında yazılıyordu). Yerel vitest ve tsc bunları yakalamıyordu; yalnız Docker derlemesi yakaladı.

Yapılan
- Boş tablo başlıklarına "İşlem" / "Süre çubuğu" metni, çubuk hücresine `aria-hidden` (süre zaten yanındaki hücrede yazıyor).
- `Cashier.tsx`: `sessionRef` güncellemesi render yerine `useEffect` içinde.

Kabul koşulları
1. `oxlint ... --max-warnings=20 src` çıkış kodu 0 (test-runs.log). Mutasyon: 3 düzeltme tek tek geri alınınca lint 1 ile çıkıyor (mutations.log).
2. Testlerin tamamı ve tsc yeşil.

Bilinen sınırlar (doğrulanamadı / yapılmadı)
- 19 uyarı kaldı (sınır 20); bir uyarı daha eklenirse kapı kırılır. Uyarılar bu görevin kapsamı dışında.
- `sessionRef` artık etkiden sonra güncelleniyor: render ile etki arasında okunursa bir önceki oturum değerini görür; Cashier testleri yeşil ama gerçek tarayıcıda denenmedi.
- Her commit öncesi lint'in çalışmadığı bir boşluk var (yalnız Docker derlemesi çalıştırıyor); kapıya eklenmedi.
