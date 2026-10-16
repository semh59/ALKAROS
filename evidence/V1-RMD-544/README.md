# V1-RMD-544 - Performans bütçeleri

- Migration 190: `observability.perf_budgets` (metrik + ad başına p95 sınırı) ve `observability.perf_releases` (her sürümün ilk görüldüğü an; ilk kayıt değişmez).
- `PerformanceBudgetEvaluator` (dakikada bir, `PerformanceBudgetRunner`): V1-RMD-543 özetlerinden
  - Bütçe aşımı: son 3 tamamlanan dakikanın her birinde p95 bütçeyi aşar (dakikada en az 3 örnek) -> tek `perf.budget_exceeded` olayı; tekrar değerlendirme yeni satır eklemez (olay parmak izi tekil), düzelince olay çözülür. İstek için varsayılan bütçe p95 800 ms; tablodaki satır varsayılanı ezer ve başka metriklere (ör. `db_command`) de uygulanır.
  - Taban çizgisi: son 15 dakikanın p95'i, önceki 7 günün aynı saat dilimiyle (kovalar toplanarak) karşılaştırılır; >= 1,5 kat yavaşlama (en az 30 güncel, 100 taban örneği) -> `perf.slowdown`; sürüm son 24 saat içinde ilk görüldüyse `perf.regression_after_release` (Hata).
- Host: sürüm = Host derlemesinin bilgi sürümü (`DualScreenApplication.cs`); eşikler `Monitoring:PerformanceBudgets`.

Kanıt: `test-runs.log` (10/10 gerçek Postgres testi), `mutations.log` (11 mutant, hepsi öldü).

Doğrulanamadı / sınırlar:
- Bütçeleri ayarlamak için ekran veya uç nokta yok (bu görevin kapsamında değil); satırlar şimdilik doğrudan tabloya yazılır.
- Gerçek bir sürüm değişimi ve gerçek trafikle denenmedi; senaryolar sentetik özetlerle kuruldu.
- Veritabanı kesintisinde sürüm kaydı atlanır (gerileme olayı o açılışta üretilmez).
