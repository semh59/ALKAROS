# V1-RMD-096 - Host-terminated HTTPS with a self-signed fallback certificate

- Task ID: V1-RMD-096
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-01

## Goal

Host `serve` fiili `--urls` içinde birden çok `;` ayrılmış adres kabul eder ve en az biri `https://` olduğunda Kestrel TLS'i kendisi sonlandırır. Sertifika kaynağı: mount edilmiş PEM (`--tls-cert` / `--tls-key`, onaylı public veya iç-CA sertifikası) ya da `--self-signed-host` için üretilen ve `<content-root>/tls` altında önbelleğe alınan kendinden imzalı bir sertifika (SAN: verilen host + `localhost` + `127.0.0.1` + `::1`). Böylece konteyner imajı, ters proxy önde olmasa veya `X-Forwarded-Proto` başlığını kaybetse bile garson telefonlarına güvenli bağlam (service worker / çevrimdışı kuyruk) verir. `http://...:5080` düz metin portu bir güvenilir TLS-sonlandıran proxy için kalır ve loopback dışı, `X-Forwarded-Proto: https` olmayan istekleri 400 `HTTPS_REQUIRED` ile reddetmeye devam eder.

## Owned surface

- `plan/v1/remediation/V1-RMD-096-host-terminated-https.md`
- `src/Host/DualScreen/DualScreenTls.cs`
- `evidence/V1-RMD-096/**`

> `src/Host/DualScreen/DualScreenOptions.cs`, `src/Host/DualScreen/DualScreenApplication.cs`,
> `deploy/docker/README.md`, `tests/Host/MigrationComposition/DualScreen/DualScreenOptionsTests.cs` ve
> `tests/Host/MigrationComposition/DualScreen/DualScreenHostTests.cs` yüzeyleri `V1-RMD-098`'e devredildi
> (Docker arayüz/backend ayrımı, 2026-09-03). Kök `./Dockerfile` kaldırıldı; yerine
> `deploy/docker/Dockerfile` (`V1-RMD-098`).

## In scope

- `DualScreenOptions`: `--urls` çoklu adres ayrıştırma; `--tls-cert`, `--tls-key`, `--self-signed-host` argümanları; https varken sertifika kaynağı zorunluluğu; `ServesHttpsDirectly`.
- `DualScreenTls`: mount edilmiş PEM yükleme (PKCS#12 round-trip) veya kendinden imzalı üretim/önbellek; salt-okunur content root'a dayanıklı.
- `DualScreenApplication.Build`: https varsa `ConfigureKestrel(ConfigureHttpsDefaults(ServerCertificate))`; `Run` artık `app.Run()`.
- `Dockerfile`: `EXPOSE 5080 5443`; ENTRYPOINT `--urls http://0.0.0.0:5080;https://0.0.0.0:5443 --self-signed-host localhost`; yorum bloğu.
- `compose.yaml`: `alkaros-host-tls` hacmi, `8444:5443` yayını, `--self-signed-host ${ALKAROS_PROXY_HOST}`.
- `deploy/docker/README.md`: iki bağımsız TLS yolu (Caddy 8443, Host 8444 kendinden imzalı), `--tls-cert` ile gerçek sertifika, cihaz sertifika kurulumu, saha testi adımları.
- Testler: options ayrıştırma negatif/pozitif; Host'un kendinden imzalı sertifikayla HTTPS sonlandırdığını ve `X-Forwarded-Proto` olmadan güvenli bağlam sunduğunu doğrulayan uçtan uca test.

## Out of scope

- Streaming replikasyon veya ödeme/fiscal davranışı.
- Caddy'nin kaldırılması; birincil TLS yolu olarak kalır.
- Genel `ACME` / public sertifika otomasyonu; `--tls-cert` ile mount noktası sağlanır.

## Dependencies

- V1-GOV-068

## Deliverables

- `DualScreenTls` ve güncellenmiş `DualScreenOptions` / `DualScreenApplication` / `Dockerfile` / `compose.yaml` / `deploy/docker/README.md`.
- `evidence/V1-RMD-096/` altında canlı HTTPS kontrolü ve test sonucu.

## Acceptance evidence

- `dotnet test` DualScreen filtresi: options negatif/pozitif testleri ve `HostTerminatesHttpsWithASelfSignedCertificateAndNeedsNoForwardedProto` geçer.
- Canlı: `https://127.0.0.1:5443/` düz istek (forwarded başlık yok) 200 döner; `http://127.0.0.1:5080/` 400 `HTTPS_REQUIRED` döner; sertifika SAN'ı `DNS:pos.lan, DNS:localhost, IP Address:127.0.0.1, IP Address:::1`.
- `docker compose config` geçerli.

## Handoff

- V1-GOV-069
