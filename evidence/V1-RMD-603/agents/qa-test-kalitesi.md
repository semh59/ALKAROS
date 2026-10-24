# qa-test-kalitesi - V1-RMD-603 Billing (SHA feb38368)

Kontrol: 120/120 kalem (varlık/atlama/sleep taraması hepsi; para ve durum kodu satır satır; göç ve istemci yalnız test adı/varlık). Skip/Ignore/Sleep/Delay: 0. Mutasyon: 59 denendi, 58 geçerli (H24 CS0162 derleme hatası sayılmadı), 26 KILLED, 32 SURVIVED; modül 35 (21/14), Host 24 (5/18/1 derleme), Host mutantları sınıf düzeyi filtreyle (hayatta kalma yalnız o filtre için kesin). Her tur restore=ok, mutants.tsv: agents/qa-test-kalitesi-mutants.tsv.
Koşular: Adjustments 17, BillFoundation 44, PaymentClosure 17, SplitDesign 29, StaticApps 120 geçti; Host.Experience.Billing 71/71 geçti (ajan koşusu, exit 0; ana oturum koşusunda BillingSplitHttpTests.cs:104 allocation sırası 1 kez kırmızı -> kararsız/sıra bağımlı test, bkz runs/RED-TESTS.md); Host.PaymentTender 1 kırmızı/52 geçti (deterministik, 3/3).

| No | Önem | Kanıt | Sorun |
|---|---|---|---|
| T1 | Yüksek | PaymentTenderHttpTests.cs:709; OrderSettlementService.cs:81 | ACheckThatWasAlreadySentToTheTillCompletesButItsTableIsNeverTouched kırmızı, deterministik: beklenen ("Occupied", null), gerçek ("Available", null). Kök neden (üretim mi test mi) doğrulanamadı; V1-RMD-714/716 masa değişikliği şüpheli. |
| T2 | Orta | M08, M11: AdjustmentCalculator.cs:65 | AdjustedTaxTotal hiçbir testte doğrulanmıyor (işaret ve negatif kelepçe mutantları hayatta). |
| T3 | Orta | M10: AdjustmentCalculator.cs:64 | AdjustedDiscountTotal mevcut bill indirimini içermiyor mutantı yakalanmıyor. |
| T4 | Orta | M07: AdjustmentCalculator.cs:58; H19 | İndirim == ödenecek tutar sınırı denenmiyor. |
| T5 | Orta | M12/M13: BillAdjustment.cs:48,146; H20 | Yüzde 100 sınır değeri sınanmıyor. |
| T6 | Orta | M01: BillMath.cs:10; H08: PaymentRefunds.cs:230 | AwayFromZero orta nokta testi yok (ToEven ile hepsi geçiyor). |
| T7 | Orta | M20,M21,M26: SplitEngine.cs:128,315,46 | Kişi başı KDV yalnız toplam olarak doğrulanıyor. |
| T8 | Yüksek | M24: SplitEngine.cs:302; H21 | Özel bölmede toplamın az olması kabul ediliyor, test yok (üretimde başka katmanda yakalanıp yakalanmadığı doğrulanamadı). |
| T9 | Orta | M17: BillPaymentClosureCalculator.cs:71; M18:62 | adjustedPayable==0 kapanışı ve ReconciliationRequired engelleyicisi, bahşirli tavan/over-allocation testsiz. |
| T10 | Yüksek | H16: BillingSplitStore.cs:223; H17:217 | Bahşiş: ödenmiş/iptal hesaba ret ve VoluntaryTip kapalıyken ret hiç sınanmıyor (indirim için var). |
| T11 | Orta | H15: BillingSplitStore.cs:155 | İndirim == tahsil edilen sınırı denenmiyor. |
| T12 | Orta | H18: BillingSplitStore.cs:115 | Etkin KDV oranı hesabı doğrulanmıyor. |
| T13 | Orta | H03,H06,H07: PaymentRefunds.cs:130,259 | Kalan iade 0 ve MaxAllowance/0 sınırları testsiz. |
| T14 | Orta | H05: PaymentRefunds.cs:356 | Reject'te kendi talebini reddetme yasağı testsiz. |
| T15 | Orta | H09: PaymentRefunds.cs:450 | Kısmi iade edilmiş ödemenin hesabı iptal engeli testsiz. |
| T16 | Orta | H12,H13,H14: BillCancellation.cs:112,114 | İptalde Unknown/ReconciliationRequired ve Refunded olmayan ödeme kümesi testsiz. |
| T17 | Orta | H22/H23: BillClosureService.cs:34,47 | Blockers.Count>0 ve iyimser yeniden deneme doğrudan testsiz (7 testlik dar filtre; tam koşuda yakalanmış olabilir). |
| T18 | Orta | M05: BillItem.cs:44 | 0 birim fiyat davranışı sınanmıyor. |
| T19 | Orta | BillReceiptHttpTests.cs:67; PaymentTenderHttpTests.cs:1263 | Fiş sıfır olmayan KDV ile sınanmıyor (tohum KDV 0); Semih senaryosu için uçtan uca test yok. |
| T20 | Düşük | BillingSplitHttpTests.cs:136 | Dört bölme modu tek testte; eşit bölme tutar bazında doğrulanmıyor. |
| T21 | Düşük | oxlint.log BillSplitWorkspace.tsx:145:313 | jsx-a11y label-has-associated-control. |
| T22 | Düşük | E2E 18 | Çalıştırılmadı; bahşiş kapalı/ödenmiş hesap senaryosu yok. |

Temiz: hesap toplamları/satır KDV, bölme tutarları (M22,M23,M25,M27,M28,M32,M33), bahşiş tavan dahil (M09), iptal gerekçesi (H10,H11), ürün bazlı iade (H01,H02,H04), servis ücreti ucu yok, eşzamanlı indirim/tahsilat, göç up/down gerçek PG, V1-RMD-298/410/112/506 gerilememiş.

Doğrulanamayanlar: T1 kök nedeni; PosTerminal billing vitest; Playwright E2E 04/18; Host mutantlarının tam proje sonucu; göç kısıtı ve Postgres depo SQL mutasyonu yapılmadı; performans kapsam dışı.
