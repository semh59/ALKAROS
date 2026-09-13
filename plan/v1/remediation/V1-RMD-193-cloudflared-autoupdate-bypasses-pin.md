# V1-RMD-193 - cloudflared kendi kendini güncelleyip pinlenen sürümü atlıyordu

- Task ID: V1-RMD-193
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

`alkaros-api-1`'in gerçek logları incelenirken bulundu: `deploy/docker/
Dockerfile`, `cloudflared`'ı bilinçli olarak tam sürüm + SHA256 checksum
ile pinliyor ("aynı base-image digest pinleme konvansiyonu" — kendi
yorumu). Ama `CloudflaredProcessFactory.Start()`, cloudflared'ı
`--no-autoupdate` bayrağı olmadan çağırıyordu; cloudflared varsayılan
olarak 24 saatte bir güncelleme kontrolü yapıyor (`autoupdateFreq=
86400000`, kendi log satırında görünüyor) ve bulduğunda kendi on-disk
binary'sini SESSİZCE değiştirip yeniden başlıyor.

Kanıt, gerçek container loglarında iki kez tekrarlanmış:
```
2026-09-10T18:28:09Z ERR Initiating shutdown error="cloudflared has been updated to version 2026.9.0"
2026-09-13T00:14:40Z ERR Initiating shutdown error="cloudflared has been updated to version 2026.9.1"
```
Her ikisinde de gerçek checksum/PID değişimi var; her restart penceresinde
tünel kısa süre kesiliyor (QUIC/datagram hata patlaması loglarda geçişin
etrafında görünüyor). Dockerfile'ın pinleme niyeti çalışma zamanında
sessizce geçersiz kılınıyordu — imaj rebuild/onay olmadan binary
değişiyordu, aynı zamanda relay tüneline gerçek bir kesinti riski
katıyordu.

## Owned surface

- `plan/v1/remediation/V1-RMD-193-cloudflared-autoupdate-bypasses-pin.md` (yeni)
- Sınırlı ek:
  - src/Integrations/QrRelay/LocalConnector/CloudflaredProcessFactory.cs
    (QrRelay modülü sahipliğinde) — `cloudflared tunnel run` çağrısına
    `--no-autoupdate` eklendi.

## Out of scope

- Yok — `docker compose build api` + `docker compose up -d --no-deps api`
  ile aynı oturumda gerçek imaj rebuild edilip yayına alındı, bkz.
  Acceptance evidence.

## Dependencies

- None

## Acceptance evidence

- `docker exec alkaros-api-1 cloudflared tunnel --help` → gerçek binary'nin
  kendi yardım metni doğrulandı: `--no-autoupdate  Disable periodic check
  for updates, restarting the server with the new version.` — istenen
  bayrağın tam olarak bu davranışı kapattığı gerçek binary'den teyit
  edildi.
- Düzeltmeden önce, gerçek `alkaros-api-1` loglarında iki ayrı gerçek
  auto-update olayı doğrulandı (yukarıdaki Goal bölümü).
- `dotnet build src/Integrations/QrRelay/ALKAROS.QrRelay.csproj -c Debug`
  → 0 uyarı, 0 hata.
- `dotnet test tests/Integrations/QrRelay/LocalConnector/*.csproj -c Debug`
  → **7/7 yeşil** (bu paket sahte bir `ICloudflaredProcessFactory`
  kullanıyor, gerçek argüman listesini sınamıyor — regresyon riski yok,
  ama gerçek davranışı da doğrulamıyor; asıl kanıt yukarıdaki gerçek
  binary --help doğrulaması).
- `docker compose build api` ile gerçek imaj rebuild edildi, sonra
  `docker compose up -d --no-deps api` ile container yeniden oluşturuldu.
  Yeni container'ın kendi başlangıç logunda:
  ```
  Version 2026.8.3 (Checksum f29324fe934d1e100617484c78deef803c4dc2cd351d645bbde42e96b4fccc5e)
  Settings: map[no-autoupdate:true]
  ```
  — hem Dockerfile'ın pinlediği tam sürüm/checksum hem de
  `no-autoupdate:true`'nun gerçekten etkin olduğu, cloudflared'ın kendi
  loguyla doğrulandı. DNS/QUIC precheck'leri (`region1`/`region2.
  v2.argotunnel.com`) PASS geçti, tünel sağlıklı yeniden kuruldu —
  `docker inspect alkaros-api-1 --format '{{.State.Health.Status}}'`
  → `healthy`.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal.

## Handoff

- None
