# V1-RMD-1168 - Yedek alarmları: tür bazında başarısızlık ve bayatlama, makine dışı kopya ve WAL arşivi büyüklüğü

- Task ID: V1-RMD-1168
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-1167 servisi `operations.backups` satırlarını `metadata.kind` ile yazar. Mevcut `backup.failed` yalnız en son satıra baktığı için günlük temel yedeğin başarısızlığı bir sonraki saatlik dökümle gizlenir; `backup.stale` tek eşiktir ve durmuş bir temel yedek (WAL'ın sınırsız büyümesi, D2'nin asıl arızası) saatlik döküm sayesinde görünmez. Bu görev mevcut `DomainDetectors` altyapısını (hata ekranı + Telegram) şöyle genişletir:

1. `backup.failed`: tür bazında (`metadata.kind`; türü olmayan elle/API satırı kendi grubudur) en son satır `Failed` ise açık.
2. `backup.stale`: tür bazında; mantıksal döküm eşiği yeni seçenek `LogicalBackupMaxAgeHours` = 3, diğer türler mevcut `BackupMaxAgeHours` = 26. Boş tablo susar (mevcut davranış korunur).
3. Yeni `backup.offsite_unavailable`: en son tamamlanmış mantıksal dökümün `metadata.offsite` değeri `failed` ise Hata, `disabled` ise Uyarı (hedef ayarlanmamış).
4. Yeni `backup.wal_archive_oversize`: en son satırın `walArchiveBytes` değeri `walArchiveLimitBytes` değerini aşıyorsa Hata. Disk doluluk ölçümü: `HealthProbe` yalnız `api` kapsayıcısının kök dosya sistemini ölçer; Docker Desktop'ta bu sanal diskin sanal büyüklüğüdür (bu makinede 1007 GB toplam, 853 GB boş görünürken gerçek C: sürücüsünde 167 GB boş) ve gerçek sürücü dolmasına kör kalır; bu yüzden disk eşiği sürücü yüzdesine değil WAL arşivinin mutlak büyüklüğüne bağlanır.

## Owned surface

- `evidence/V1-RMD-1168/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Observability/DomainDetectors/DomainDetectors.cs
  - tests/Host/Observability/DomainDetectors/DomainDetectorsTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon (`cp` + `cmp`; tür bazı, eşikler, disabled/failed ayrımı, aşım karşılaştırması)
2. İki bağımsız qa incelemesi
3. Testler: gizlenen temel yedek başarısızlığı artık açık; iyileşince çözülür; tür bazlı bayatlama; mevcut elle/API satırı ve boş tablo davranışı değişmez; offsite `disabled` Uyarı, `failed` Hata, `copied` sessiz; WAL aşımı açılır/çözülür
4. `ALKAROS.Architecture.*` ve ModuleBoundaries testleri yeşil

## Out of scope

- Yeni tablo veya migration; `HealthProbe` ve sağlık anlık görüntüsü
- `OffsiteBackupMaintenanceJob` devre dışı kalması (kendi bakım ekranı gösterir; olay üretmez, ayrı bulgu olarak raporlanır)

## Dependencies

- V1-RMD-1167

## Acceptance evidence

1. Kırmızı, yeşil ve mutant çıktıları
2. DomainDetectors, mimari ve sınır testleri exit code 0; plan audit ve consistency audit 0 hata 0 uyarı
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
