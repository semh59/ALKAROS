# V1-RMD-031 - Complete containerized release

- Task ID: V1-RMD-031
- Status: Blocked
- Assignee: /root
- Work type: release
- Surface state: Existing

## Goal

Tamamlanan production çalışma alanlarını birleştirmek; Host, PosTerminal production asset'leri, sıralı migration'lar ve
digest-pinned PostgreSQL 18 içeren, güvenli configuration, kalıcılık ve health davranışına sahip tek komutlu Docker
sürümü sunmak.

## Owned surface

- `Dockerfile`
- `ALKAROS.slnx`
- `build/project-manifest.json`
- `src/Host/packages.lock.json`
- `src/Clients/PosTerminal/package.json`
- `src/Clients/PosTerminal/pnpm-lock.yaml`
- `src/Clients/PosTerminal/src/api.ts`
- `src/Clients/PosTerminal/src/contracts.ts`
- `src/Clients/PosTerminal/src/main.tsx`
- `src/Clients/PosTerminal/src/styles.css`
- `src/Clients/PosTerminal/vite.config.ts`
- `tests/Clients/PosTerminal/Experience/**`
- `evidence/V1-RMD-031/**`
- PO:2026-08-28 `RMD032-F001` kararıyla `DualScreenApplication.cs` ve `App.tsx` runtime kitchen station contract
  remediation custody'si V1-RMD-034'e devredildi; external blocker ve historical release kaydı değişmeden kalır.

## Dependencies

- V1-RMD-028
- V1-RMD-029
- V1-RMD-030
- V1-RMD-019
- V1-RMD-020
- V1-RMD-033

## Blocker

- Bu görev ancak `V1-RMD-031-F002` için onaylı dış HTTPS sertifikası ve bağımsız browser E2E; fiscal/printer/payment/provider sandbox veya cihaz; backup/RPO-RTO; licensing; security-assessment ve imzalı go-live kanıtları sağlandıktan sonra yeniden açılabilir. Yerel CA işletim sistemi trust store'una sessizce kurulmadı ve kanıt uydurulmadı.

## Acceptance evidence

- Multi-stage build repository'de pinlenmiş .NET/Node/pnpm toolchain'lerini, locked restore'u ve temiz kaynak girdilerini
  kullanır; son image yerel `bin/obj/dist` yerine publish edilmiş Host, güncel PosTerminal bundle ve gerekli migration
  dosyalarını içerir.
- Compose digest-pinned PostgreSQL 18, isimli kalıcı volume, fail-closed secret/config davranışı, health check, restart
  policy ve migration sonrasında sıralı Host readiness içerir. Plain HTTP ile proxy/cookie davranışı production güvenlik
  kurallarına uyar.
- Belgelenmiş tek komut tam stack'i build eder ve başlatır. Login, salon/sandalye kurulumu, masa siparişi,
  taşıma/birleştirme/ayırma, hesap bölme tasarımı, katalog düzenleme, mutfak ilerletme ve müşteri ekranı container
  servislerine karşı mock olmadan çalışır.
- Temiz build/testler, image incelemesi, SBOM/vulnerability/license kontrolleri, migration up/down, cold start, restart,
  persistence, başarısız dependency ve recovery kanıtı geçer. Zorunlu dış donanım/provider/go-live kanıtı uydurulmaz;
  yokluğunda açık blocker kalır.

## Handoff

- V1-RMD-032
