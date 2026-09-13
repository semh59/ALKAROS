# V1-RMD-192 - `web` container'ında zombie process sızıntısı

- Task ID: V1-RMD-192
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

Çalışan `alkaros-web-1` container'ı canlı incelenirken bulundu: Caddy bu
container'da PID 1 olarak çalışıyor, hiçbir init süreci yok, ve compose'un
kendi healthcheck'i (`wget -q --spider https://localhost/health/ready`,
her 10 saniyede bir) her çalıştığında busybox'ın TLS için çatalladığı bir
`ssl_client` alt-süreci bırakıyor. Kimse bu alt-süreçleri `wait()`'lemediği
için hepsi zombie kalıyor.

Kanıt (düzeltmeden önce, gerçek container üzerinde):

- `docker exec alkaros-web-1 grep -l 'State:.*Z' /proc/[0-9]*/status | wc -l`
  → **10643** — container yaklaşık 30 saattir açıktı; 30h × 3600s ÷ 10s ≈
  10800, gözlenen sayıya çok yakın.
- `ps aux` çıktısında hepsi `[ssl_client]` (köşeli parantez = defunct).
- `docker exec alkaros-web-1 cat /sys/fs/cgroup/pids.max` → `max` — hiçbir
  PID tavanı da yok, sızıntı sınırsız büyüyordu.

Bu, günler/haftalar içinde host'un `kernel.pid_max`'ına çarpıp
container'ın (ve dolayısıyla PosTerminal/WaiterPwa/Cashier'in tamamının)
yeni süreç açamayarak çökmesine yol açabilecek gerçek bir üretim riskiydi.

## Owned surface

- `plan/v1/remediation/V1-RMD-192-web-container-zombie-process-leak.md` (yeni)
- Sınırlı ek:
  - compose.yaml (deploy/compose sahipliğinde) — `web` servisine `init: true`
    eklendi (Docker'ın gömülü `tini`'sini PID 1 yapar, her zombie'yi düzgün
    reap eder). Healthcheck'in kendisine dokunulmadı — zaten doğru şeyi
    (gerçek TLS handshake üzerinden uçtan uca sağlık) doğruluyordu, eksik
    olan yalnızca reaping'di.

## Out of scope

- `compose.dev.yaml`'ın kendi `web` override'ı: doğrulandı,
  `docker compose -f compose.yaml -f compose.dev.yaml config` çıktısında
  `init: true` zaten doğru şekilde miras kalıyor, ayrı bir değişiklik
  gerekmedi.
- Healthcheck mekanizmasının kendisini `wget`'ten `curl`'e değiştirmek:
  kök neden reaping eksikliği idi, healthcheck aracı değil; `init: true`
  bunu healthcheck'e dokunmadan çözüyor.

## Dependencies

- None

## Acceptance evidence

- Düzeltmeden önce: `alkaros-web-1` içinde 10643 zombie doğrulandı
  (yukarıdaki Goal bölümü).
- `docker compose up -d --no-deps web` ile container `init: true` ile
  yeniden oluşturuldu; `docker inspect alkaros-web-1 --format
  '{{.HostConfig.Init}}'` → `true`.
- Yeniden oluşturmadan sonra **~150 saniye (15 healthcheck döngüsü)**
  boyunca gerçek container üzerinde izlendi:
  - `ps aux | wc -l` sabit **6** kaldı (büyümedi).
  - `grep -l 'State:.*Z' /proc/[0-9]*/status | wc -l` → **0**, tüm süre
    boyunca.
  - `docker inspect ... Health` → `healthy`, `FailingStreak=0` (healthcheck
    kendisi hâlâ doğru çalışıyor).
- `docker compose -f compose.yaml -f compose.dev.yaml config` → `init:
  true` `web` bloğunda doğrulandı.

## Handoff

- None
