# V1-RMD-790 - Sonuç

## Değişiklik (kasa kapanışı, sunucu tarafı)
- Kapanış tek işlemde: oturum satırı kilitlenir, beklenen nakit ve son sayım kilit altında okunur, oturum güncellenir, fark `ClosingDifference` defter satırı olarak yazılır (beklenen nakde girmez), denetim kaydı aynı işlemde atılır. Sayım kaydı aynı kilidi alır.
- Kapanış için en az bir sayım zorunlu ve kapanış tutarı son sayıma eşit olmalı (`CASH_COUNT_REQUIRED`, `CASH_COUNT_MISMATCH`).
- Sıfır olmayan her farkta 1-500 karakter açıklama zorunlu; tolerans üstü fark yönetici onayı ister. Tolerans ayarı `cash.close_variance_tolerance` (varsayılan 50 TL, tavan 500 TL; yazarken ve okurken uygulanır).
- Sayımı başlatma, kaydetme ve kapatma yalnız oturumun kasiyeri veya `cash.session.override` sahibi yönetici tarafından yapılır.
- Mutabakat yönetici işi ve dört göz: oturumu açan ve kapatan kişi, ortak "yönetici kendi onayı" ayarı kapalıyken mutabık edemez; açıksa kayıt `reconciled_self_approved` işaretlenir ve denetim kaydı bunu yazar.
- Ödenmemiş hesap ve sonucu belirsiz ödeme sayıları işletme genelinde okunur; engellemez, kasiyer onaylar (`AcknowledgeUnsettled`), onaylanmazsa kapanış geri alınır, onaylanırsa denetim kaydı sayıları taşır. `expected-cash` yanıtı sayıları ve toleransı da verir.
- Göç 213 (sütunlar, kısıtlar), `order.json`, `MigrationManifest`, üç test projesinin elle seçilmiş göç listeleri, `docs/domain/cash-session-design.md` ve `docs/architecture/settings-ownership.md` güncellendi; Türkçe hata kodları `ApiErrorCatalog`ta.

## Doğrulama (gerçek Postgres)
- Kırmızı: yeni `CashCloseSafetyHttpTests` eski kodla 20/21 başarısız (`red-close/`). Yeşil: 21/21 (`i-safety/`).
- Etkilenen mevcut testler yeni kurala uyduruldu (kapanıştan önce sayım, fark açıklaması): CashSessionHttpTests 28/28, CashMovementApprovalHttpTests 44/44, OpenBillsAndUncertain 1/1.
- Diğer paketler: Cash.Contracts 33, SessionLifecycle 28, TenderHandler 13, TenderComposition 9, EftTender 12, TransactionLedger 46, Host.Errors 223, SettingsManagementHttpTests 30, Manifest 18; hepsi yeşil, atlanan 0.
- Mutasyon (`mutation-summary.txt`): politika 8 (m1-m8) ve depo/servis/ayar 8 (s1-s8) mutant; hepsi öldü, her turda `cmp` ile birebir geri alındı. Derleme hatası veren iki tur (m8, s4) derleme olduğu için sayılmadı, geçerli mutantla yeniden koşuldu.

## İncelemeler
- qa-backend (4 bulgu), qa-guvenlik-yetki (10 bulgu); hepsi ele alındı:
  - Sayım tekrar-deneme tekilleştirmesi yalnız en yeni sayımla karşılaştırır (A-B-A testi); nakit iade `Refund` satırı da oturum kilidini alır, kapanmış oturuma düşmez (test); ödenmemiş sayılar kapanış işleminin içinde okunur; `Initiated` ödeme kod tabanının "çözülmemiş" tanımında (Pending/Unknown/ReconciliationRequired) olmadığı için eklenmedi.
  - Yöneticinin kendi oturumunda kendi onayıyla tolerans aşımı: engellenmedi (kasiyerin yetkili yönetici olduğu küçük işletmede tek kişi var), denetim kaydına `overrideBySessionCashier` yazılır (test). Yetkisiz kasiyerin override ilanı 403 (test), başka terminalin oturumu 404 (test), tolerans ayarını yönetici dışı değiştirme 403 (test).
  - Mutabakatta sayımı kaydeden kişi de açan/kapatan gibi dört göz kapsamında (politika + HTTP testi). Sayım notu, mutabakat notu ve onay gerekçesi 500 karakterle sınırlı (400, test). `expected-cash` artık ayar kaydı yazmaz.
  - Doğrulama turu: iade onayı oturum kapanışına yarışırsa 500 yerine 409 `NO_OPEN_CASH_SESSION` (BillingSplitApplication eşlemesi, kilit tutan gerçek yarış testi; plan commit ile kapsam eklendi).
  - Bırakılanlar (bilinçli): sayım başlatma/kaydetme için ayrı denetim olayı yok (satır `counted_by` taşır, kapanış ve mutabakat denetimli); açıklama alanında kontrol karakteri engeli yok (SQL parametreli, ekranda `textContent` ile basılması V1-RMD-792 kabul koşulu); `ck_cash_sessions_difference_explained` eski kapalı satırlar için `NOT VALID` (eski kayıtların açıklaması yoktur, yeni kapanışlar zorlanır).

## Açık risk / kapsam dışı
- Bu görevde ekran yok; kapanış ekranı V1-RMD-792'de. `CashSessionClosedEvent` outbox'a konmaz (tüketicisi yok), kayıt denetim olayıdır.
- Ödenmemiş hesap sayıları işletme geneli: hesap ve ödemeler terminale bağlı değil (şemada terminal sütunu yok).
