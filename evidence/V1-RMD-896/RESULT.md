# V1-RMD-896 sonucu

`customer_account.balance_snapshots` günlük bakiye satırları artık 2 yıl sonra siliniyor. Eskiden hiç silinmiyordu (müşteri x işletme günü satırı birikir; rapor yalnız son 90 günü okur).

## Karar ve gerekçe
Ürün kararı (2026-11-01): fatura olmayan müşteri verisi son işlemden 2 yıl sonra saklanmaz; fatura kayıtları 10 yıl. Günlük bakiye, defterden türetilen müşteri başına bakiye geçmişidir ve fatura kaydı değildir; bu yüzden 2 yıl (730 gün). Ölçüt "son işlemden" değil kaydın kendi günüdür: satır, kendi günü bugünden 730 günden eskiyse silinir (karar metni müşteri verisinin son işlemden itibaren saklanmasıdır; türetilmiş günlük satırda kayıt tarihi ölçüt alındı). Defter, güncel bakiye (`balances`) ve fatura kayıtları etkilenmez; silinen satırları iş geri yazmaz (yalnız son `WindowDays` günü yazar); rapor yalnız son 90 günü okuduğundan etkilenmez, `DeletingOldSnapshotsDoesNotChangeTheReport` fazla silmeye karşı korur (rapor zaten eski satırı okumaz).

## Değişiklik
`IBalanceSnapshotStore.DeleteOlderThanAsync` (kesin eski; tekrarında zararsız). `balance-snapshot` bakım işi (6 saatte bir, Güvenlik ekranından da çalışır) yazdıklarının yanında `RetentionDays = 730`'dan eski satırları siler; sonuç metni "N gün için M bakiye kaydı yazıldı; K eski kayıt silindi." İş açıklaması buna göre güncellendi.

## Kanıt
- Kırmızı: red-1.log (özet metni beklentisi; silme testi kodla aynı adımda yazıldığından kırmızısı mutantla gösterildi: "işte silme yok" mutantı testi düşürdü).
- Yeşil: bakiye projeksiyonu 46/46 (final-store.log), yaşlandırma/bakım sınıfı 10/10 (final-job.log).
- Mutasyon (mutations.txt): işte silme yok, sınır bir gün erken/geç, kısa saklama (sabit 730 testle kilitlendi, ilk turda yaşadı), `<=`, tüm satırları silme: 7/7 öldü, cmp ile geri alındı.

## Sınır
- Saklama 2 yıl sabittir (ayar değil); ürün isterse ayara taşınır.
- Rapor yalnız 90 gün okuduğu için 100 gün ile 2 yıl arası satırlar raporda kullanılmaz; ürün kararına uyumlu olarak 2 yıl tutulur (karar kısaltmak isterse sabit tek yerde).

## İnceleme sonrası notlar
- `SnapshotDatesListOnlyDatesFromTheGivenDayOn` testinde `EndOfDay` kullanımı: silme testi ile aynı kesim anını kullanmak için, testin amacı değişmeden.
- `DailyBalancesAreKeptTwoYears` sabiti kilitler; sınır testleri sabiti kullandığı için "kısa saklama" mutantını yalnız bu test yakalar.
- Açık, ürün/ölçek: `snapshot_date` önde indeks yok (silme taraması; ölçek büyürse ayrı migration), silme tek ifade, anonimleştirilen müşterinin satırları 2 yıla kadar kalır, bakiye kayıtları KVKK politika tablosunda yok.
