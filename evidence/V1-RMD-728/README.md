# V1-RMD-728 - Kırmızı ödeme testi ve masa onarımı (603 B003, B038, N1, N2)

**Sonuç:** Kapanış kapısını kırmızı tutan ödeme testi yeşil (53/53), bölme tahsis sırası artık deterministik, Billing Host testleri 77/77. Üretim davranışı yalnız tahsis sırasında değişti.

## Bulgu bulgu
- **B003 (üretim kusuru değil, test tohumu):** `ACheckThatWasAlreadySentToTheTillCompletesButItsTableIsNeverTouched` tohumu, gerçek "kasaya gönder" yolunun yazdığı `table_sent_checks` satırını yazmıyordu; masa onarımı siparişi kapalı sayıp masayı serbest bırakıyordu. Test tohumuna `MarkCheckSentAsync` eklendi; test kırmızıdan yeşile döndü (`logs/tender-b003.log`).
- **B038 + N2 (gerçek):** `PostgresSplitDesignRepository.GetAllocationsByBillIdAsync` yalnız `created_at` ile sıralıyordu; aynı işlemde yazılan satırlarda eşitlikte sıra belirsizdi. `bill_allocation_id` ayırıcısı eklendi; yeni test `AllocationsSavedTogetherComeBackInAStableOrder` eşit zamanlı 6 satırla sıralamayı doğrular. Mutant (ayırıcıyı kaldır) KILLED (`mutations.log`).
- **N1 (kısmen gerçek, değiştirilmedi):** Onarım (`RepairPointerDriftAsync`) gerçekten masa durumunu da yazıyor: hayalet işaretçisi olan masayı Available yapar, mevcut test `RepairFixesAGhostPointerButNeverFreesATableMarkedOccupiedByHand` bunu bilerek bekler; yalnız durum uyuşmazlığı olan (elle Occupied) masaya dokunmaz. Arayüz yorumu (`ITablePointerProjector`) bunu zaten doğru anlatıyor. Çelişen tek yer `src/Host/Experience/Tables/TablePointerRepairHostedService.cs:9-10` yorumu ("Status is left to the staff"); dosya bu görevin yüzeyi dışında olduğu için düzeltilmedi. Davranışı değiştirmek ürün kararıdır ve istenmedi.

## Kanıt (logs/)
PaymentTender 53/53 (önceden 52/53), Host Billing 77/77, mutasyon 1/1 KILLED, kaynak geri yüklendi.

## Açık
`TablePointerRepairHostedService` sınıf yorumu bayat (N1); düzeltmesi Host/Tables yüzeyi olan bir göreve bırakıldı.
