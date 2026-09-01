# V1-RMD-079 - Deployment HTTPS and PWA offline field readiness

- Task ID: V1-RMD-079
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-08-31

## Goal

Sahaya çıkmadan önce HTTPS ve WaiterPwa çevrimdışı kuyruğunun gerçek koşulda çalışacağını güvenceye almak. Reverse proxy her ana bilgisayar adında TLS sunar, WaiterPwa güvenli olmayan bağlamda service worker kaydını sessizce geçmek yerine kullanıcıya görünür bir uyarı ve tanı verir, ve dağıtım dokümanına sertifika stratejisi kararı ile saha test yordamı eklenir.

## Owned surface

- `plan/v1/remediation/V1-RMD-079-deployment-https-and-pwa-offline-field-readiness.md`
- `compose.yaml`
- `deploy/docker/Caddyfile`
- PO:2026-09-01 kararıyla deploy/docker/README.md yüzeyi V1-RMD-096'ya devredildi (Host-terminated HTTPS iki-yollu TLS anlatımı); bu historical task closed kalır ve dosyayı bundan sonra V1-RMD-096 sahiplenir.
- PO:2026-09-01 kararıyla src/Clients/WaiterPwa/wwwroot/waiter-app.js yüzeyi V1-RMD-083’e devredildi; bu historical task closed kalır.
- `evidence/V1-RMD-079/**`

## In scope

- Caddy yapılandırmasının yalnızca `localhost` yerine her istek ana bilgisayar adında (LAN IP dahil) TLS sonlandırıp Host'a yönlendirmesi.
- `waiter-app.js` içinde service worker kaydı öncesi `window.isSecureContext` denetimi; güvenli bağlam yoksa çevrimdışı modun kapalı olduğunu bildiren görünür uyarı; kayıt hatasında da görünür tanı mesajı.
- `deploy/docker/README.md` içine sertifika stratejisi kararı (LAN dağıtımı için Caddy iç CA kökünün cihaza yüklenmesi; dış go-live için onaylı public sertifika) ve WiFi kapatarak yapılan PWA çevrimdışı saha testi yordamı.

## Out of scope

- Host'un HTTP dinleyicisini kaldırmak veya Dockerfile giriş noktasını değiştirmek.
- Public sertifika sağlayıcısı entegrasyonu veya DNS yapılandırması.

## Dependencies

- V1-GOV-042

## Deliverables

- Her ana bilgisayar adında TLS sunan Caddy yapılandırması, güvenli bağlam denetimli WaiterPwa service worker kaydı ve saha test yordamı içeren dağıtım dokümanı.

## Acceptance evidence

- `node --check src/Clients/WaiterPwa/wwwroot/waiter-app.js` sıfır çıkış kodu verir.
- Semih; `docker compose up` sonrası bir telefondan `https://<host-ip>:8443` adresine bağlanır, Caddy kök sertifikasını yükledikten sonra WaiterPwa'yı açar, WiFi'ı kapatıp bir sipariş girer ve WiFi geri gelince kuyruğun sunucuya iletildiğini doğrular; güvenli olmayan bağlamda çevrimdışı mod uyarısının göründüğünü doğrular.

## Handoff

- V1-RMD-080
