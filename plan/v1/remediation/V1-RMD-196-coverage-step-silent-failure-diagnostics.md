# V1-RMD-196 - "Full .NET tests" adımı, ilk gerçek koşusunda sessizce çöktü

- Task ID: V1-RMD-196
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

V1-RMD-194/195, önündeki iki kapıyı (plan/manifest doğrulaması, Markdown
lint) 9+ gündür ilk kez açtı. Ardından `Full .NET tests with line and
branch coverage measurement` adımı da ilk kez gerçekten çalıştı —
solution'daki HER test projesi kendi "Passed!" satırını yazdı (dahil
`ALKAROS.Host.Tests.dll` 135/135), sonra `dotnet-coverage collect`'in
kendi "Code coverage results: ..." satırından sonra HİÇBİR ek çıktı
olmadan `##[error]Process completed with exit code 1` ile çöktü. Log'da
`test -s ...` veya python coverage-özet adımının çalışıp çalışmadığına
dair hiçbir iz yoktu — sessiz bir çöküş.

Yerel tekrar üretim iki nedenle güvenilir olmadı: (1) bu makinede
`dotnet-coverage collect "dotnet test ALKAROS.slnx ..."` gerçek
Postgres bağlantı ayarları (`ALKAROS_TEST_PG_PORT=55432`) verilmeden
çalıştırıldı, testler bağlantı reddiyle başarısız oldu (CI'daki gerçek
sorunla ilgisiz); (2) bu makine tüm solution'ı coverage enstrümantasyonu
altında koşarken iki kez bellek yetersizliğiyle sonlandırıldı. Bu
sistemin kendisi (GitHub'ın barındırdığı runner değil), tam kapsamlı bir
yerel tekrar üretime güvenli değil.

Kör bir düzeltme tahmin etmek yerine, adımın kendisine tanı eklendi:
`dotnet-coverage collect`'in gerçek çıkış kodu, disk alanı, çıktı
dosyasının var/boyut durumu ve test-sonuç dizininin içeriği artık ayrıca
yazdırılıyor — bir sonraki gerçek CI koşusu kök nedeni kesin olarak
gösterecek (dotnet test'in kendisi mi başarısız oldu, coverage dosyası
mı boş/eksik kaldı, yoksa disk mi tükendi). `set -e`, bu blok için
bilinçli olarak `set -u` + `pipefail`'e gevşetildi — yalnız bu adımın
kendi teşhis çıktısını garanti altına almak için, kalıcı bir gevşetme
değil.

## Owned surface

- `plan/v1/remediation/V1-RMD-196-coverage-step-silent-failure-diagnostics.md` (yeni)
- Sınırlı ek:
  - .github/workflows/task-scope.yml (CI iş akışı sahipliğinde) —
    `Full .NET tests with line and branch coverage measurement`
    adımına yukarıdaki tanı çıktıları eklendi.

## Out of scope

- Kök nedenin kalıcı düzeltmesi: bir sonraki CI koşusunun gerçek tanı
  çıktısı gelmeden hangi düzeltmenin doğru olduğu bilinemez (test
  hatası mı, disk mi, coverage merge sorunu mu) — ayrı bir görev; bu
  görevin eklediği tanı çıktısı o görevin girdisi olacak.

## Dependencies

- V1-RMD-194
- V1-RMD-195

## Acceptance evidence

- `python3 -c "import yaml; yaml.safe_load(...)"` ile iş akışı YAML'ının
  sözdizimi doğrulandı.
- CI'ın kendi gerçek loguyla doğrulandı: `gh run view --log-failed` →
  tüm test projeleri (Host.Tests dahil 135/135) "Passed!" yazdı, hata
  yalnız `dotnet-coverage collect` sonrası, hiçbir teşhis çıktısı
  olmadan oluştu — bu görevin eklediği tanı satırları tam olarak bu
  boşluğu dolduruyor.
- Yerel tekrar üretim iki nedenle terk edildi (yukarıdaki Karar bölümü):
  yanlış Postgres ortam değişkenleri ve tekrarlanan bellek
  yetersizliği — ikisi de bu makineye özgü, CI'ın gerçek ortamıyla
  ilgisiz.

## Handoff

- None
