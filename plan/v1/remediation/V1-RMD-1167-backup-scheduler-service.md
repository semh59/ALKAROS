# V1-RMD-1167 - Otomatik yedek servisi: saatlik döküm, günlük temel yedek, doğrulama, budama ve ayarlanabilir makine dışı kopya

- Task ID: V1-RMD-1167
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasa denemesi D2 bulgusu (doğrulandı: hacimde yalnız iki elle alınmış döküm, makine dışı kopya boş, son temel yedek 1 Eylül, WAL arşivi 2556 dosya / 40 GB ve hiçbir betik budamıyor): yedek hâlâ elle alınan bir işlemdir. Bu görev `compose.yaml` içine `watchdog` kalıbında, `restart: unless-stopped` ile yığınla birlikte kalkan bir `backup-scheduler` servisi ekler (Windows'ta ayrıca Görev Zamanlayıcı kaydı gerektirmez; Docker açılınca kalkar). `compose.ops.yaml` servisleri `ops` profilinde olduğundan `up` ile başlamaz, bu yüzden oraya konmaz.

1. Sıklık env ile ayarlanır; varsayılanlar: kontrol aralığı 300 sn, mantıksal döküm (`backup.sh`) 60 dk, fiziksel temel yedek (`basebackup.sh`) 24 saat. Zamanı gelen iş, birimdeki son geçerli yapıtın değiştirilme zamanından hesaplanır (yeniden başlatma sırasında boşuna yedek alınmaz); başarısız deneme sonraki kontrolde yeniden denenir.
2. Doğrulama: döküm için `sha256sum -c` ve `pg_restore --list` (boş olmayan içindekiler); temel yedek için `sha256sum -c`, `gzip -t` ve `tar -xzO backup_label` (başlangıç WAL dosyası okunabilmeli; `pg_verifybackup` SHA256SUMS yan dosyasını kabul etmediği için kullanılmaz). Doğrulanamayan yapıt silinir (sayılmaz), betik sıfırdan farklı çıkar ve `operations.backups` tablosuna `Failed` satırı + hata metni yazılır; döngü durmaz.
3. Her deneme `operations.backups` satırı yazar (`backup_type = Full`, `metadata.kind` = `logical` | `base`; `metadata` içinde `offsite` = `disabled` | `copied` | `failed`, `walArchiveBytes`, `walArchiveLimitBytes`). Mevcut alarm altyapısı bu satırları okur (V1-RMD-1168).
4. Saklama (tek sayılarla, gerekçeli): temel yedek `ALKAROS_BASE_BACKUP_KEEP` = 2, alt sınırı 2 (bir temel yedek bozuk çıkabilir; `rpo-rto-targets.md` §3 de bir önceki temelle çakışma ister). WAL, korunan en eski temel yedeğin kendi `backup_label` dosyasındaki BAŞLANGIÇ WAL dosyasından eski olanlar `pg_archivecleanup` ile silinir; budama yalnız yeni temel yedek doğrulandıktan sonra yapılır; `backup_label` okunamazsa hiçbir şey silinmez. Dökümler: son 48 saatin tamamı, 30 güne kadar her UTC gününün ilk dökümü (runbook'taki sayılar). WAL arşivi `ALKAROS_WAL_ARCHIVE_MAX_GB` = 10 sınırını aşarsa bu metadata'ya yazılır (sınır: 2 temel yedek ≈ 2 gün x en kötü 288 parça x 16 MB ≈ 9,2 GB; ölçülen kullanım ~0,5 GB/gün).
5. Makine dışı kopya: `ALKAROS_OFFSITE_COPY_TARGET` boş değilse compose bu sunucu yolunu `/offsite-copy` olarak bağlar (sürücü harfi yolu, örn. `E:/yedek`); boşsa kopya kapalıdır ve `offsite=disabled` yazılır, hayali hedef yoktur. Her kontrolde yeni döküm, temel yedek ve WAL dosyaları hedefe geçici ad + `mv` ile kopyalanır, `cmp`/`sha256sum -c` ile doğrulanır, hedef yazılamazsa `offsite=failed`. Hedef aynı saklama kuralıyla budanır (sonsuz büyümez). Mevcut `api` şifreli dışa yükleme işi (`alkaros-offsite-backup`) ayrı kalır ve değişmez.
6. Betik kipleri: varsayılan döngü, `once` (tek tur; kendini sınamak ve elle koşmak için), `prune` (yalnız budama). Kapanışta SIGTERM yakalanır.
7. Windows ölçümü (bu görev sırasında yapıldı): sürücü harfi yolu bağlanır; UNC yolu (`//localhost/C$/...`) bağlanır görünür ama yazılan dosya gerçek klasöre ULAŞMAZ. Bu yüzden desteklenen hedef sürücü harfidir; UNC yazılmaz.

## Owned surface

- `evidence/V1-RMD-1167/**`
- `deploy/docker/backup-scheduler.sh` — servis betiği (LF satır sonu).
- `deploy/docker/backup-scheduler-selfcheck.sh` — tek kullanımlık PostgreSQL 18 kümesinde uçtan uca ve budama kanıtı (LF).
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - compose.yaml
  - tests/Host/MigrationComposition/Composition/DeployFilesTests.cs
  - deploy/docker/README.md
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test (selfcheck ve DeployFilesTests betik yokken kırmızı), düzeltme, mutasyon (`cp` + `cmp` ile geri alınır; budama sınırı, doğrulama adımı, yalnız-doğrulanınca-buda koşulu, backup_label okunamazsa silmeme)
2. İki bağımsız qa incelemesi
3. Selfcheck: tek kullanımlık küme (`archive_mode=on`), `operations.backups` tablosu migration 029'dan kurulur; 3 tur sonunda yalnız 2 temel yedek kalır, korunan en eski temel yedekten PITR (`restore-pitr.sh`) hâlâ çalışır; bozuk `pg_restore` (PATH gölgesi) `Failed` satırı + sıfırdan farklı çıkış üretir; kopya hedefi yazılamazsa `offsite=failed`, hedef boşsa `disabled`
4. DeployFilesTests: servis `compose.yaml` içinde, `restart: unless-stopped`, TLS sertifika doğrulama listesinde, parola ortam değişkeninde değil gizli dosyada; `tests/Deployment/test_container_contract.py` değişmeden yeşil
5. Canlı yığına DOKUNULMAZ: `compose up` yok; canlı WAL birimi yalnız salt okunur bağlı `pg_archivecleanup -n` ile ölçülür

## Out of scope

- `operations.backups` okuyan alarmlar (V1-RMD-1168), Türkçe runbook ve doküman güncellemeleri (V1-RMD-1169)
- Mevcut şifreli dışa yükleme işini (`OffsiteBackupMaintenanceJob`) değiştirmek
- Canlı `alkaros_restore` veritabanını silmek (Semih'in kararı)

## Dependencies

- V1-RMD-1165

## Acceptance evidence

1. Kırmızı çıktı, yeşil çıktı, mutant listesi
2. Selfcheck çıktısı: 3 tur, kalan temel yedek sayısı, budanan/korunan WAL listesi, PITR sonucu, hata yolları
3. Canlı WAL biriminde `pg_archivecleanup -n` ile kaç dosyanın serbest kalacağı (silme yok)
4. DeployFilesTests ve `test_container_contract.py` exit code 0; plan audit ve consistency audit 0 hata 0 uyarı
5. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
