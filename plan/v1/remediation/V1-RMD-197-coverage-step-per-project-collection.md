# V1-RMD-197 - Coverage adımı, tüm solution'ı tek `collect`'te birleştiriyordu

- Task ID: V1-RMD-197
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

V1-RMD-196'nın eklediği teşhis, CI'ın gerçek iki koşusunda da hiç
basılmadı: `dotnet-coverage`'ın kendi "Code coverage results: ..."
satırından sonra sıfır ek çıktı, ardından adımın kendisi
başarısız oldu. Bu, script'in kendi `set -e`/mantık hatasıyla
çıkmadığını, sürecin DIŞARIDAN (OOM) sonlandırıldığını gösteriyor —
bu oturumun kendi makinesi de aynı işlemi (tüm solution'ı tek
`dotnet-coverage collect "dotnet test ALKAROS.slnx ..."` ile sarmalamak)
yerel olarak tekrar üretmeye çalışırken iki kez bellek yetersizliğiyle
sonlandırıldı — aynı desen.

Kök neden: ~90 test projesinin TÜMÜNÜN canlı coverage oturumunu bir
tek final merge için aynı anda bellekte tutmak. Düzeltme: her test
projesi kendi ayrı `dotnet-coverage collect` çağrısını alıyor (kendi
coverage dosyasını hemen diske yazıp oturumunu kapatıyor), sonda
`dotnet-coverage merge` zaten tamamlanmış XML dosyalarını birleştiriyor
— canlı oturumları değil. Bu, aynı sınıftan ölçeklenme sorunları için
standart bir çözüm.

## Owned surface

- `plan/v1/remediation/V1-RMD-197-coverage-step-per-project-collection.md` (yeni)
- Sınırlı ek:
  - .github/workflows/task-scope.yml (CI iş akışı sahipliğinde) —
    `Full .NET tests with line and branch coverage measurement` adımı,
    `find tests -name "*.Tests.csproj"` ile keşfedilen her projeyi
    ayrı `dotnet-coverage collect` çağrısıyla işleyip
    `$RUNNER_TEMP/coverage-parts/`'a yazacak, sonra
    `dotnet-coverage merge` ile birleştirecek şekilde yeniden yazıldı.
    Bir proje başarısız olursa döngü diğerlerine devam ediyor
    (coverage verisi kaybolmuyor), sonda toplu olarak raporlanıp adım
    başarısız oluyor.

## Out of scope

- Yok — tek adımın kendi coverage toplama stratejisi.

## Dependencies

- V1-RMD-196

## Acceptance evidence

- `python3 -c "import yaml; yaml.safe_load(...)"` ile YAML sözdizimi
  doğrulandı.
- Yerel olarak 3 küçük test projesiyle (`ALKAROS.Measurements.Tests`,
  `ALKAROS.Messaging.Tests`, `ALKAROS.Architecture.Tests`) döngü
  mantığı uçtan uca çalıştırıldı:
  - Her üçü de kendi `.cobertura.xml`'ini üretti (dahil, kasıtlı olarak
    yerel Postgres ortam değişkeni verilmeden başarısız bırakılan
    `ALKAROS.Messaging.Tests` — döngü onu doğru şekilde
    `failed_projects` listesine ekleyip DİĞER projelere devam etti,
    yine de kendi coverage dosyasını yazdı).
  - `dotnet-coverage merge` üç dosyayı tek bir 7.3MB'lık
    `coverage.cobertura.xml`'de birleştirdi, çıkış kodu 0.
  - Python `line-rate`/`branch-rate` ayrıştırma adımı birleşmiş dosya
    üzerinde temiz çalıştı.
- Tam ölçekli (90 proje) doğrulama, bir sonraki gerçek CI koşusunda
  yapılacak — bu makine tüm solution'ı coverage altında koşturmayı iki
  kez bellek yetersizliğiyle sonlandırdığı için burada denenmedi
  (V1-RMD-196'nın kendi Karar bölümündeki aynı gerekçe).

## Handoff

- None
