# V1-RMD-334 - Relay tüneli yeniden kurulduğunda öncekini artık öksüz bırakmıyor

- Task ID: V1-RMD-334
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) orta seviye bulgusu: "Cloudflare tüneli yeniden provizyon
edildiğinde önceki tünel öksüz kalıyor; `DeleteTunnelAsync` tanımlı ama hiç çağrılmıyor." Doğrulandı:
`ICloudflareApiClient.DeleteTunnelAsync` gerçekten var ve gerçek bir Cloudflare API çağrısı yapıyordu, ama kod
tabanında SIFIR çağrı yeri vardı (grep ile doğrulandı). `IRelayProvisioningService.ProvisionAsync`'in kendi belge
yorumu bu boşluğu zaten dürüstçe kayıt altına almıştı ("nothing here manages that lifecycle or cleans up the
orphaned previous tunnel") — bir manager restoranın alt alan adını her değiştirdiğinde (veya bağlantıyı yeniden
kurduğunda) eski tünel Cloudflare hesabında kalıcı olarak öksüz kalıyordu.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Integrations/QrRelay/PublicGateway/RelayProvisioningService.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Integrations/QrRelay/PublicGateway/RelayProvisioningServiceTests.cs
- `plan/v1/remediation/V1-RMD-334-relay-tunnel-cleanup-on-reprovision.md`

## In scope

1. `RelayProvisioningService.ProvisionAsync`: yeni tünel `IRelayTunnelStore`'a kaydedilmeden ÖNCE mevcut
   (eskiyecek) tünel bilgisi okunuyor; yeni tünel başarıyla kaydedildikten SONRA eski tünel için
   `ICloudflareApiClient.DeleteTunnelAsync` en iyi çaba (best-effort) ile çağrılıyor.
2. Cloudflare'in "hâlâ aktif bağlantısı olan bir tüneli silmeyi reddetme" davranışı (belge yorumunun kendi
   belirttiği, beklenen bir durum) yakalanıp sadece loglanıyor — provizyon işleminin kendisini asla
   başarısızlığa düşürmüyor, çünkü yeni tünel zaten doğru şekilde canlı ve kayıtlı.
3. İlk provizyon (önceki tünel yok) durumunda hiçbir silme denemesi yapılmıyor.

## Out of scope

1. Yerel bağlayıcının (`cloudflared` süreci) yeni tünel token'ına GERÇEKTEN geçtiğini doğrulamak/beklemek —
   bu, silme denemesinin NE ZAMAN güvenli olduğunu kesin bilmenin tek yolu olurdu, ama bağlayıcının kendi
   yeniden bağlanma zamanlamasını izlemek ayrı, daha büyük bir tasarım değişikliği. Bu görev en iyi çaba (silme
   başarısız olursa sessizce loglanır) yaklaşımını seçti — Cloudflare'in kendi reddi zaten güvenli bir
   varsayılan (tünel gerçekten aktifse hiçbir şey kırılmaz, sadece öksüz kalmaya devam eder ve bir sonraki
   provizyonda tekrar denenir).
2. Zaten var olan öksüz tünellerin (bu düzeltmeden ÖNCE oluşmuş) geriye dönük temizliği — bu, gerçek bir
   Cloudflare hesabına karşı çalışacak ayrı, tek seferlik bir operasyon görevi gerektirir.

## Dependencies

- None

## Acceptance evidence

- `tests/Integrations/QrRelay/PublicGateway/ALKAROS.QrRelay.PublicGateway.Tests.csproj`: 26/26 test geçti (3 yeni
  test dahil: `ReprovisioningDeletesThePreviousTunnelAfterTheNewOneIsLive`,
  `ANeverProvisionedRelayHasNoPreviousTunnelToDelete`,
  `ANewTunnelStaysTheResultEvenWhenCloudflareRefusesToDeleteTheOldOneBecauseItIsStillConnected`).
- Mutasyon kontrolü: silme çağrısı bloğu geçici olarak kaldırıldı, `ReprovisioningDeletesThePreviousTunnelAfterTheNewOneIsLive`
  beklenen şekilde kırmızıya döndü (`Expected: "tunnel-id-old", Actual: null`). Dosya bayt-bayt geri yüklendi
  (`diff` ile doğrulandı, `IDENTICAL`), paket yeniden 26/26 yeşile döndü.
- `ALKAROS.QrRelay.csproj`: sıfır hata, sıfır uyarı ile derlendi.

## Handoff

- None
