# V1 - Core Restaurant Operation

## Hedef

Gerçek para kabul etmeyen fakat masa, sipariş, mutfak ve adisyon temelini uçtan
uca çalıştıran çekirdek operasyon.

## Giriş koşulu

`GATE-V1-ENTRY` kapanmış olmalıdır.

## Çıkış kapısı

- Bu sürüm altında 246 görev tanımlıdır: 241 `Done`, 5 onaylı `NotApplicable`, 0 `Planned` ve
  0 `InProgress` görev vardır. 2026-09-03 24. dalga (Docker arayüz/backend ayrımı A1-full: `web`
  Caddy imajı statik istemci paketlerini sunar + TLS sonlandırır + `/api` `/hubs`'ı `api:5080`'e
  proxy'ler; `api` imajı `serve --api-only` ile yalnız JSON API + hub'ları düz HTTP çalıştırır;
  `Dockerfile` → `deploy/docker/Dockerfile` adlandırılmış aşamalarla; operatör araçları
  `compose.ops.yaml`, dev overlay `compose.dev.yaml`; `V1-RMD-096` Host self-signed `8444` yedeği
  bu ayrımla kaldırıldı; WaiterPwa personel girişi + `crypto.randomUUID` düz-HTTP çökme düzeltmesi;
  Semih onayıyla, `V1-RMD-098`) ile `GATE-V1-EXIT` yeniden açılıp `V1-GOV-070` ile kesin olarak
  yeniden mühürlenmiştir. 2026-09-01 23. dalga (Host-terminated HTTPS: Host artık
  `https://0.0.0.0:5443` üzerinde kendinden imzalı bir sertifikayla — SAN
  `ALKAROS_PROXY_HOST` — TLS sonlandırır; garson telefonları Caddy önde olmasa da güvenli
  bağlam / çevrimdışı kuyruk elde eder; düz `5080` portu güvenilir proxy için kalır ve
  `HTTPS_REQUIRED` ile fail-closed'dır; Semih onayıyla V1'e çekildi, `V1-RMD-096`) ile
  `GATE-V1-EXIT` kapısı yeniden açılıp `V1-GOV-069` ile kesin olarak yeniden
  mühürlenmiştir. 22. dalga (`V1-RMD-095`, `V1-GOV-067`) PostgreSQL WAL arşivleme /
  point-in-time recovery ve rakiplere göre kalibre edilmiş RPO/RTO hedefleri tamamlanmıştır
  (para/mali/denetim RPO ~5 dk; `V0-BKP-001`/`V0-BKP-002` `## Onay` bloklu `Done`, devir
  listesi 11 → 9). 21. dalga (`V1-RMD-094`,
  `V1-GOV-065`) KVKK saklama anonimleştirme fiili tamamlanmıştır; `audit.audit_events`
  AUD-01 append-only olduğu için `V15-KVK-002`'ye devredildi. 20. dalga (`V1-RMD-093`, `V1-GOV-063`) tamamlanmıştır; 1M sipariş satırında
  20 terminalde sipariş gönderimi p95 45.9 ms / p99 73.9 ms, sıfır deadlock.
- `V1-FND-001`, `V1-FND-010`, `V1-FND-003`, `V1-FND-004`, `V1-FND-005`, `V1-SEC-001`,
  `V1-SEC-002`, `V1-FND-002` ve `V1-FND-006` sıralı foundation kapısı geçmeden
  başka application görevi başlamaz.
- Kimlik, yetki, masa, sipariş, mutfak ve bill foundation testleri geçer.
- Duplicate submit ve concurrency senaryoları kanıtlanır.
- Payment UI ve gerçek fiscal akış kapalıdır.
- Audit, print queue ve yerel backup temel akışları geri kazanılabilir durumdadır.

## Modüller

`alerts`, `billing`, `cash-design`, `cashier-ui`, `catalog`, `foundation`, `governance`,
`identity-authorization`, `kitchen-printing`, `operations`, `orders`,
`reconciliation`, `remediation`, `reporting`, `security-foundation`, `settings`,
`table-management`, `waiter-pwa`.

Doğrulanan plan hacmi: 18 modül/dizin, 246 tek-sahip görev.
2026-09-01 12. dalga F bölümü modül domain incelemesi (`V1-RMD-084..085`, `V1-GOV-047`) V1-GOV-046 ile açıldı ve V1-GOV-047 ile kesin olarak mühürlendi.
2026-09-01 13. dalga PostgreSQL yedekleme/geri yükleme mekanizması (`V1-RMD-086`, `V1-GOV-049`) V1-GOV-048 ile açıldı ve V1-GOV-049 ile kesin olarak mühürlendi.
2026-09-01 14. dalga V1 go-live yük testi temel ölçümü (`V1-RMD-087`, `V1-GOV-051`) V1-GOV-050 ile açıldı ve V1-GOV-051 ile kesin olarak mühürlendi.
2026-09-01 15. dalga dağıtım altyapısı performans ayarı (`V1-RMD-088`, `V1-GOV-053`) V1-GOV-052 ile açıldı ve V1-GOV-053 ile kesin olarak mühürlendi.
2026-09-01 16. dalga orders ölçek indeks migration'ı (`V1-RMD-089`, `V1-GOV-055`) V1-GOV-054 ile açıldı ve V1-GOV-055 ile kesin olarak mühürlendi.
2026-09-01 17. dalga oturtmada sürüm toleransı (`V1-RMD-090`, `V1-GOV-057`) V1-GOV-056 ile açıldı ve V1-GOV-057 ile kesin olarak mühürlendi.
2026-09-01 18. dalga operasyonel veri housekeeping (`V1-RMD-091`, `V1-GOV-059`) V1-GOV-058 ile açıldı ve V1-GOV-059 ile kesin olarak mühürlendi.
2026-09-01 19. dalga cihaz/tarayıcı test planı ve vanilla istemci a11y smoke (`V1-RMD-092`, `V1-GOV-061`) V1-GOV-060 ile açıldı ve V1-GOV-061 ile kesin olarak mühürlendi.
2026-09-01 20. dalga yazma kritik yolu yük testi (`V1-RMD-093`, `V1-GOV-063`) V1-GOV-062 ile açıldı ve V1-GOV-063 ile kesin olarak mühürlendi.
2026-09-01 21. dalga KVKK saklama anonimleştirme fiili (`V1-RMD-094`, `V1-GOV-065`) V1-GOV-064 ile açıldı ve V1-GOV-065 ile kesin olarak mühürlendi.
2026-09-01 22. dalga WAL arşivleme / point-in-time recovery ve rakiplere göre kalibre RPO/RTO (`V1-RMD-095`, `V1-GOV-067`) V1-GOV-066 ile açıldı ve V1-GOV-067 ile kesin olarak mühürlendi.
2026-09-01 23. dalga Host-terminated HTTPS kendinden imzalı yedek sertifika (`V1-RMD-096`, `V1-GOV-069`) V1-GOV-068 ile açıldı ve V1-GOV-069 ile kesin olarak mühürlendi.
2026-09-03 24. dalga Docker arayüz/backend ayrımı A1-full (`V1-RMD-098`, `V1-GOV-070`) V1-RMD-098 ile açıldı ve V1-GOV-070 ile kesin olarak mühürlendi.
