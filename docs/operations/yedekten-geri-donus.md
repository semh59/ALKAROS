# Yedekten geri dönüş

> Kime: işletme sahibi ve teknik destek. Komutlar ALKAROS klasöründe (`compose.yaml` dosyasının olduğu klasör) çalıştırılır.
> Geri yükleme, ad değiştirme, yetki ve oturum adımları gerçek bir yedek dosyasıyla geçici bir veritabanı sunucusunda denendi; canlı veriye dokunulmadı (kanıt: `evidence/V1-RMD-1169/restore-trial.log`). `docker compose` ile yazılan komutların sarmalayıcıları (servis adları, birim adları) kodla karşılaştırıldı ama canlı yığında çalıştırılmadı; ilk gerçek geri dönüşte ilk önce Adım 3'ü deneyin.

## 1. Yedekler kendiliğinden alınır

`backup-scheduler` servisi sistemle birlikte açılır ve şunları yapar:

| Ne | Ne sıklıkla | Ne kadar saklanır |
| --- | --- | --- |
| Veritabanı dökümü (`.dump`) | saatte bir | son 48 saatin hepsi; 30 güne kadar her günün ilk dökümü |
| Günlük tam yedek (temel yedek) ve arada biriken kayıtlar (WAL) | günde bir | son 2 tam yedek ve onlara yetecek kayıtlar (yaklaşık 2 gün) |
| Makine dışı kopya | her 5 dakikada yeni dosyalar | aynı kurallarla budanır (aşağıda, bölüm 6) |

Her yedek alındıktan sonra bozuk olup olmadığı kontrol edilir; bozuksa atılır ve hata ekranında alarm çıkar. Yedekler Docker biriminde durur: `alkaros_alkaros-backups` (dökümler ve `base` klasörü) ve `alkaros_alkaros-wal-archive` (kayıtlar). Her döküm, yanında `.sha256` dosyasıyla birlikte saklanır; ikisi birlikte kopyalanmazsa geri yükleme reddedilir.

Yedek durursa ya da alınamazsa Yönetim hata ekranında ve (anahtarı girildiyse) Telegram'da alarm çıkar: "Yedekleme başarısız", "Yedek eski", "Makine dışı kopya kullanılamıyor" ve "Yedek kayıt arşivi sınırı aştı".

## 2. Ne zaman geri dönülür

Veri yanlışlıkla silindi veya bozuldu, sunucu diski bozuldu ya da elle yapılan bir işlem geri alınamıyor. İlk kural: **canlı veritabanına dokunmadan, ayrı bir veritabanına geri yükleyip kontrol edin; ancak sonra canlıya alın.**

## 3. Adım adım geri dönüş

**Adım 1 - Siparişleri durdurun.** Garson ve kasaya işlem almayı bırakmalarını söyleyin; açık hesapları kâğıda not edin. Geri dönüş, yedekten sonraki işlemleri geri getirmez.

**Adım 2 - Hangi yedeği kullanacağınızı seçin.** En yenisi genellikle doğrudur:

```sh
docker compose -f compose.yaml -f compose.ops.yaml run --rm backup -c "ls -lt /backups"
```

Listede en üstteki `alkaros_alkaros_<tarih>.dump` en yenisidir (tarih UTC, `20261118T171712Z` = 18 Kasım 2026 17:17:12 UTC; Türkiye saati 3 saat ileridir). Verinin bozulduğu andan ÖNCEKİ en yeni dökümü seçin.

**Adım 3 - Ayrı veritabanına geri yükleyin (canlıya dokunmaz).** Yaklaşık 1 dakika sürer (ölçüm: 15 MB'lık gerçek döküm için 27, 39, 46 ve 55 saniye; veri büyüdükçe uzar).

```sh
docker compose -f compose.yaml -f compose.ops.yaml run --rm backup /repo/deploy/docker/restore.sh /backups/alkaros_alkaros_<tarih>.dump alkaros_restore
```

Son satır `restore: ok target=alkaros_restore seconds=...` olmalıdır. **Komut başarıyla bitmediyse (çıkış kodu 0 değilse) Adım 4 ve 5'e geçmeyin;** yarım kalmış `alkaros_restore` kullanılmaz.

| Gördüğünüz | Anlamı | Ne yapılır |
| --- | --- | --- |
| `CHECKSUM MISMATCH` (çıkış 4) | dosya bozuk veya eksik kopyalanmış | bir önceki dökümü deneyin; hepsi bozuksa makine dışı kopya diskindeki dökümü deneyin (bölüm 5b) |
| `missing checksum sidecar` (çıkış 3) | `.sha256` dosyası yok | dökümle birlikte `.sha256` dosyasını da aynı klasöre koyun |
| `artifact not found` (çıkış 2) | dosya adı yanlış | Adım 2'deki listeden adı aynen kopyalayın |
| `pg_restore` hatası, disk dolu | geri yükleme yarıda kesildi | yer açın, komutu yeniden çalıştırın |

Komut, `alkaros_restore` adlı veritabanını önce siler ve yeniden kurar (önceki denemenizin kopyası gider); canlı `alkaros` veritabanını asla silmez (ona dokunmayı reddeder).

**Adım 4 - Geri yüklenen kopyayı kontrol edin.**

```sh
docker compose exec postgres psql -U alkaros -d alkaros_restore -c "SELECT count(*), max(created_at) FROM payments.payments" -c "SELECT count(*) FROM cash.cash_transactions"
```

Ödeme sayısı ve son ödeme zamanı beklediğiniz değere yakın mı bakın. Emin değilseniz teknik destek ile devam edin.

**Adım 5 - Canlıya alın (kesinti birkaç dakika sürer).** Sırayı bozmayın: servisleri durdurun, adları değiştirin, uygulama rolünün yetkilerini yeniden verin, eski oturumları iptal edin, sonra servisleri açın. `<tarih>` yerine bugünün tarihini rakamla yazın (örnek: `20261118`).

```sh
docker compose stop api connector watchdog backup-scheduler
docker compose exec postgres psql -U alkaros -d postgres -c "ALTER DATABASE alkaros RENAME TO alkaros_bozuk_<tarih>;" -c "ALTER DATABASE alkaros_restore RENAME TO alkaros;"
docker compose run --rm migrate
docker compose exec postgres psql -U alkaros -d alkaros -c "UPDATE identity.device_sessions SET revoked_at = now() WHERE revoked_at IS NULL;"
docker compose start api connector watchdog backup-scheduler
```

- Adları değiştirme komutunun ilki başarılı, ikincisi başarısız olursa `alkaros` adlı veritabanı kalmaz; hemen `docker compose exec postgres psql -U alkaros -d postgres -c "ALTER DATABASE alkaros_bozuk_<tarih> RENAME TO alkaros;"` ile eskisini geri adlandırın ve teknik destekle devam edin.
- `migrate` adımı zorunludur: geri yükleme yetkileri taşımaz, bu adım olmadan uygulama veritabanını okuyamaz (geçici sunucuda "permission denied for schema payments" hatası alındı; yetki bölümü çalıştırılınca okudu). Aynı adım, yedekten sonra eklenmiş yeni veritabanı değişikliklerini de uygular.
- Oturum iptali **servisler açılmadan önce** yapılmalıdır: yedek, sonradan iptal edilmiş oturumları geri getirebilir; servis açıkken iptal edilmemiş eski oturum anahtarları geçerli kalır.
- API'nin hazır olması yaklaşık 12 saniye sürer (kasa denemesinde ölçüldü). Açıldığını `https://<sunucu adresi>:8443/health/ready` adresinin `Ready` demesinden anlarsınız.
- Eski veritabanı `alkaros_bozuk_<tarih>` adıyla kalır; bir hafta emin olana kadar silmeyin.

**Adım 6 - Personel listesini kontrol edin.** Yönetim ekranında personel listesine bakın; yedekten sonra işten ayrılan kişi varsa pasifleştirin, hatalı kilitlenenlerin kilidini açın.

**Adım 7 - Silinmesi gereken veriler geri gelmiş olabilir (teknik destek yapar).** Yedek, KVKK saklama işinin silip anonimleştirdiği verileri geri getirebilir. Teknik destek, `docs/compliance/kvkk-retention-runbook.md` içindeki `kvkk-retention` (önce kuru çalıştırma, sonra `--apply`) ve `audit-disposal` işlerini yeniden çalıştırır; `audit-disposal` servisi kuru çalıştırmasız doğrudan uygular, bu yüzden önce runbook'taki kuru çalıştırma komutuyla bakılır. Yedek dosyalarında, makine dışı kopyada ve kayıt (WAL) arşivinde silinmiş veri, yedeğin saklama süresi (en çok 30 gün) dolana kadar kalır.

**Adım 8 - Ana anahtar ve gizli dosyalar.** `.env` dosyasındaki `ALKAROS_SECRET_ENVELOPE_MASTER_KEY` değeri yedeğin içinde değildir. O değer yoksa kayıtlı şifreli bilgiler (QR aktarma bilgileri vb.) okunamaz. Yedekte ayrıca şunlar da YOKTUR, ayrı ve güvenli bir yerde (kasa, parola yöneticisi) saklanmalıdır: `.env`, `deploy/docker` klasöründeki `db_password`, `app_db_password`, `restore_db_password`, `admin_password`, `db_tls.crt`, `db_tls.key`, `db_tls_ca.crt` dosyaları ve Caddy sertifika birimi (`caddy-data`; kaybolursa her cihaza yeni kök sertifika kurulur).

**Adım 9 - Eski veritabanını silme kararı sizindir.** Bir hafta sonra her şey yolundaysa teknik destek `alkaros_bozuk_<tarih>` veritabanını siler. Bu geri alınamaz; bu yüzden bu adımı yalnızca siz onaylayınca yapın. O veritabanında KVKK'ya göre silinmiş olması gereken veriler de bulunabilir; gereğinden uzun tutmayın.

## 4. Belirli bir ana dönmek (ileri düzey, yalnız teknik destek)

Saatlik döküm yetmiyorsa (örneğin "dün 14:05'e dön"), günlük tam yedek ve kayıtlardan ayrı bir veritabanı kümesinde o ana dönülebilir; komut ve ayrıntılar `docs/recovery/rpo-rto-targets.md` bölüm 3'tedir. Bu yol son 2 günü kapsar; süresi kayıt miktarına bağlıdır ve gerçek verinizle ölçülmemiştir. Tek kullanımlık deneme kümesinde (`deploy/docker/backup-scheduler-selfcheck.sh`, 400 satırlık yapay veri) en eski saklanan tam yedekten yeniden kurulan küme, en yeni kayda kadar tüm satırları buldu.

**Bu belge, o kümeyi canlıya alma adımlarını vermez; denenmemiştir.** Felakette canlıya alma yolu bölüm 3'teki döküm geri yüklemesidir; belirli bir ana ait veri gerekiyorsa teknik destek onu ayrı kümeden okuyup gerekeni elle taşır.

## 5. Yedek alınmıyor uyarısı çıkarsa

1. `docker compose logs --tail 50 backup-scheduler` komutunun son satırlarına bakın.
2. Servis kapalıysa: `docker compose up -d backup-scheduler`.
3. Yedek hemen alınsın isterseniz servisi yeniden başlatın: `docker compose restart backup-scheduler` (zamanı gelmiş yedek açılışta hemen alınır; başarısız bir yedek 30 dakika sonra kendiliğinden yeniden denenir).
4. Hata sürerse teknik destek ile görüşün; yedek hatalı ise eski yedekler silinmez (en yeni döküm hiçbir zaman silinmez).

## 5b. Bilgisayar yandı veya çalındı (yeni makine)

Yalnız makine dışı kopya diski kaldıysa: (1) yeni makinede `README` kurulum adımlarıyla yığını bir kez kurun; `.env` ile `deploy/docker` gizli dosyalarını eski yerlerinden (kasa, parola yöneticisi) geri koyun; ana anahtar (`ALKAROS_SECRET_ENVELOPE_MASTER_KEY`) eski değer olmalıdır. (2) Kopya diskini takın; seçtiğiniz dökümü ve `.sha256` dosyasını yedek birimine taşıyın (komut geçici birimde denendi; `E:` yerine diskin harfi, `<tarih>` yerine dökümün adındaki damga):

```sh
docker run --rm -v alkaros_alkaros-backups:/b -v E:/ALKAROS-yedek:/src:ro postgres:18 sh -c "cp /src/alkaros_alkaros_<tarih>.dump /src/alkaros_alkaros_<tarih>.dump.sha256 /b/"
```

(3) Bölüm 3'ün Adım 3'ünden devam edin. Yeni kurulumun boş `alkaros` veritabanı Adım 5'te `alkaros_bozuk_<tarih>` olur. Bu yol tam bir yeni makine kurulumu üzerinde denenmemiştir; ilk kullanımda teknik destekle yapın.

## 6. Makine dışı kopya hedefini ayarlama (Semih için)

Bilgisayar çalınır, yanar ya da diski bozulursa yalnız o bilgisayardaki yedekler de gider. Bu yüzden yedeklerin ikinci bir kopyası başka bir yerde olmalıdır. Hedef henüz ayarlı değilse hata ekranında "Makine dışı kopya kullanılamıyor" uyarısı görünür; bu uyarı, hedefi ayarlayınca kaybolur.

1. Sunucuya harici bir disk takın (USB disk yeterlidir) ve sürücü harfine bakın (örneğin `E:`). Diski şifreleyin (Windows'ta BitLocker); kopya **şifresiz** yazılır ve içinde bütün müşteri ve satış verisi vardır.
2. Diskte bir klasör açın, örneğin `E:\ALKAROS-yedek`.
3. `.env` dosyasına şu satırı ekleyin (eğik çizgiyle yazın):

   ```
   ALKAROS_OFFSITE_COPY_TARGET=E:/ALKAROS-yedek
   ```

4. Servisi yeniden başlatın: `docker compose up -d backup-scheduler`.
5. 10-15 dakika sonra Windows Gezgini'nde `E:\ALKAROS-yedek` klasörüne bakın: `.dump` dosyaları ve `base`, `wal` klasörleri görünmelidir. İlk seferde tam yedek yüzlerce MB olduğu için biraz sürer. Görünmüyorsa ayarlanan yol yanlıştır; bir sonraki adıma geçmeyin.
6. Bir saat içinde hata ekranındaki "Makine dışı kopya kullanılamıyor" uyarısı kaybolur.

Bilmeniz gerekenler (ölçüldü):

- **Ağ yolu (`\\sunucu\paylaşım`) çalışmaz.** Docker bunu kabul ediyormuş gibi görünür ama dosyalar o yere gitmez, sessizce başka bir yere yazılır. Yalnızca sürücü harfi kullanın. Ağ paylaşımına harf atanmış sürücüler (örneğin `Z:`) denenmedi; kullanmadan önce yukarıdaki 5. adımla gerçekten dosya geldiğini doğrulayın.
- **Disk takılı değilse yedek servisi hiç açılmaz** (Docker "yol bulunamadı" der) ve yerel yedek de alınmaz. Yedek durunca 3 saat içinde "yedek eski" alarmı çıkar. Diski takıp `docker compose up -d backup-scheduler` komutunu çalıştırın.
- Kopya disk dolarsa kopya başarısız olur ve uyarı çıkar; eski kopyalar yukarıdaki kurallarla kendiliğinden silinir.
- Kopya diski 2 günden uzun erişilemezse, o aralıktaki kayıt (WAL) zinciri kopar; bir sonraki tam yedek kopyalanınca zincir yeniden başlar. Yerel diskin dolmaması için yerel kayıtlar, kopya çalışmasa da budanır.

## 7. Sıklığı ve saklamayı değiştirme

Gerekirse `.env` dosyasına şu değişkenler yazılır, sonra `docker compose up -d backup-scheduler` çalıştırılır. Boş bırakılırsa varsayılan geçerlidir.

| Değişken | Varsayılan | Anlamı |
| --- | --- | --- |
| `ALKAROS_LOGICAL_BACKUP_MINUTES` | 60 | döküm aralığı (dakika) |
| `ALKAROS_BASE_BACKUP_HOURS` | 24 | tam yedek aralığı (saat) |
| `ALKAROS_BASE_BACKUP_KEEP` | 2 (en az 2) | saklanan tam yedek sayısı |
| `ALKAROS_DUMP_KEEP_HOURS` / `ALKAROS_DUMP_KEEP_DAILY_DAYS` | 48 / 30 | tüm dökümler kaç saat / günlük dökümler kaç gün |
| `ALKAROS_WAL_ARCHIVE_MAX_GB` | 10 | kayıt arşivi bu büyüklüğü aşarsa hata ekranında alarm |
| `ALKAROS_BACKUP_RETRY_MINUTES` | 30 | başarısız yedeğin yeniden deneme aralığı |

Uyarı: yedek eski alarmı mantıksal döküm için 3 saat, diğerleri için 26 saat sonra çıkar ve bu eşikler yukarıdaki ayarlara bağlı değildir; döküm aralığını 3 saatin, tam yedek aralığını 26 saatin üstüne çıkarırsanız yanlış alarm gelir.
