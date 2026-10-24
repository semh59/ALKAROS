# V1-RMD-710 - Birleştirme ve ayırmanın doğruluğu

Kapanan V1-RMD-601 bulguları: B001, B002, B003, B004, B007 (ödeme sonrası kapanış), B008, N1 (B030 V1-RMD-711'e kaldı).

## Değişiklikler
- Migration 203 (`database/migrations/V1/V1-RMD-710/`): `table_merges.original_order_ids/original_bill_ids uuid[]` (geriye doldurma + down). Kanıt: `logs/migration-up-down.log` (geçici veritabanında up, ikinci up, down; veritabanı silindi).
- Birleştirme: boş/siparişsiz/hesapsız masa artık `Guid.Empty` yazmaz (B002); katılımcının tüm açık sipariş ve hesapları kaydedilir (B003); Rejected sipariş açık iş sayılmaz (N1).
- Ayırma: yalnız hâlâ açık kayıtlar geri döner (B008); ana masa işaretçisi geri verilenleri dışlayıp sıralı seçilir (B004); olay kimlik listelerini taşır, tüketiciler listeyi kullanır, eski tek kimlikli olay hâlâ işlenir.
- Ödeme (`OrderSettlementService`): birleşik masada işaretçi sıradaki açık siparişe geçer; son sipariş ödenince masa boşalır, birleşim kapanır, katılımcılar boşalır (B007).
- Sunucu, birleşim lideri için "Unmerge" komutunu yayınlar; PosTerminal'deki ölü pasif düğme kaldırıldı, gerçek düğme çıkar (B001).

## Test kanıtı (gerçek PostgreSQL, tümü exit 0)
TableMerge 35 (6 yeni), Host Tables 16 (MergeSettlementTests 2 yeni, FloorPlan testi genişletildi), CurrentPointers, Reservations, TableTransfer, TableLifecycle, PaymentTopology, Host manifest 17, vitest tables 34 + tsc 0. Günlükler `logs/`.
Semih senaryosu: `EveryOpenOrderAndBillOfAParticipantComesBackOnUnmerge`, `APrimaryWithoutItsOwnWorkDoesNotKeepPointingAtTheReturnedOrder` ve mevcut 3 masalı birleştir/ayır testi: ürünler doğru masaya döner.

## Mutasyon (`mutate.mjs`, `mutations.log`)
M1–M15 + C1 + M9b/M11b: M9 ve M11 derleme hatası verdi (geçersiz mutant), aslına uygun M9b/M11b ile yeniden koşuldu; hepsi KILLED, kaynak dosyalar her turda geri yüklendi ve doğrulandı.

## Açık kalan
- B030 (kasaya gönderilen siparişin table_id'de kalması) ve transfer/durum ucunun birleşimi bilmemesi V1-RMD-711 kapsamında; sipariş birleşim sırasında ana masada açılırsa ana masada kalır (kayıtlı sınırlama).
- Mevcut aktif birleşimlerin geriye doldurması yalnız tek kimlikli eski kayıtlar için geçerli.
