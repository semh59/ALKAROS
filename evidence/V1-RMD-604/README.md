# V1-RMD-604 - Payments doğrulaması (V1-RMD-739 programının pilotu)

**Sonuç:** Payments diliminde 33 bitmiş görev tek tek (kabul, kod gerçeği, algoritma, test gücü, kanıt, karar doğruluğu) incelendi. 15 TAMAM, 17 DÜZELTİLMELİ, 1 YENİDEN AÇ. Hiçbirinde Kritik bulgu yok; düzeltilmeli olanların çoğunda Orta bulgu var.

## Yöntem ve maliyet (ölçülen)
- Defter: `ledger.py` → `ledger.jsonl` (33 görev; 16'sında kanıt klasörü yok, 21'inde günlük yok; bunlar eski görevler).
- 4 ajan paralel (8-9 görev), her biri salt okunur; derleme ve test koşmadı.
- Ajan harcaması 616 bin token, her ajan 4-9 dakika. Görev başına ~19 bin token.
- Ana oturumda bulgular kodla yeniden doğrulandı (aşağıda).

## Kararlar
| Karar | Görevler |
|---|---|
| TAMAM (15) | V13-CSH-003, V13-PAY-002, V13-PAY-005, V1-RMD-229, 230, 294, 298, 352, 401, 409, 412, 507, 508, 524, 649 |
| DÜZELTİLMELİ (17) | V13-HUG-005, V13-ALC-001, V13-ALC-003, V13-PUI-001, V13-PAY-001, V13-PAY-003, V13-PAY-004, V1-RMD-258, 264, 276, 282, 283, 342, 520, 646, 726, 736 |
| YENİDEN AÇ (1) | V1-RMD-727 |

## Ana oturumca kodla doğrulanan bulgular
1. **Doğrulandı - 342:** QNB VKN/TCKN alanı yalnız hane sayısı kontrol ediyor (`QnbCredentialSettingsEndpoints.cs:205`), kod yorumu "algoritma yok" diyor; oysa aynı depoda `CustomerTaxIdentity.cs` VKN mod-9 ve TCKN kontrol hanelerini uyguluyor. İç tutarsızlık.
2. **Doğrulandı - 283/412:** "Kart çekilmedi" çözümü bekleyen "kart çekildi" bildirimini kapatmıyor (`ManualPaymentResolutionService.cs:52-70`); bildirim Pending kalır ve fiş numarası kilitli kalır (`143...up.sql:26-27`).
3. **Doğrulandı - 282:** Siparişi ödemede tamamlayan çağrı aktör vermiyor (`OrderSettlementService.cs:63`); sipariş geçmişinde "kim ödedi" yok.
4. **Doğrulandı - 276:** `BillClosureService` hesap kilidi almıyor (`PaymentClosure/*.cs` içinde kilit yok); kapanış ile yeni belirsiz kart denemesi arasında yarış olasılığı (koşulmadı).
5. **Doğrulandı - 727:** İade fiş numarası (`refund_requests.slip_number`) benzersiz değil; elle iade tamamlama (`PaymentRefunds.cs:409`) isteyen-tamamlayan ayrımı yapmıyor; reddedilen/tamamlanmayan kart iadesinde bütçe serbest bırakılmıyor (520 ile aynı kök). Bunlar 727'de bilinçli ertelenmiş ama takip görevi yok.
6. **Doğrulandı - 726/736:** Süpürücü yalnız tahsisi olan hesapları seçiyor; %100 indirimli (tahsissiz) hesabın kapanışı başarısız olursa yeniden denenmez. Süpürücü kapanış sonrası sipariş tamamlama hatasını da yeniden denemez.
7. **Kısmen doğru - V13-PAY-001:** Onaylanan tutar ≤ verilen para kontrolü var, ≤ talep edilen kontrolü yok (`Payment.cs:69-71`); etkisi düşük.
8. **Çürütülen/azaltılan:** V13-ALC-003 "ölü kod" iddiası: RefundIntent bilinçli bağlanmamış ve gerekçeli izin listesinde (dış bağımlılık V13-HUG-003); bulgu değil.

## Doğrulanamayanlar (Semih veya dış kaynak gerekir)
- Mevzuat: ÖKC iade belgesi, sıfır bedelli hesabın mali karşılığı, EFT'nin belge etkisi, denetim kaydı saklama süresi/KVKK dayanağı.
- PO niyeti: süpervizörün iadeyi onaylayabilmesi ve iade bütçesi verebilmesi (görevlerde "müdür" yazıyor, göç süpervizöre de veriyor).
- Token terminal kimliği biçimi ve küçük harf davranışı (canlı API yok).

## Eksik kanıt
V13-HUG-005, V13-PUI-001, V13-PAY-001..003, V1-RMD-258, 264, 276, 282, 283, 294, 298, 342, 352 için kanıt klasörü yok (eski görevler); günlük sayıları çalıştırılarak yeniden doğrulanmadı.
