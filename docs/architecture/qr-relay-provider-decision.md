# QR Relay Provider and Onboarding Ownership Model

> **Task:** V14-QRT-002
> **Status:** Done
> **Source basis:** PO:2026-09-07
> **Approver:** Semih — 2026-09-07
> **Decision type:** Business decision (named business approver)
> **Baseline:** `V0-ARC-009` (`docs/architecture/qr-relay-topology.md`) — bu karar
> V0-ARC-009'un topology seçimini değiştirmez; V0-ARC-009'un açıkça "Out of
> scope" bıraktığı iki şeyi (relay implementation, cloud sağlayıcı satın alma)
> somutlaştırır.

## 1. Karar

**Seçilen:** Cloudflare Tunnel, ALKAROS'un sahip olduğu tek bir wildcard
domain altında, restoran başına otomatik alt-alan adı ve tünel kaydıyla.

- **Provider:** Cloudflare Tunnel (`cloudflared`). Public ingress, TLS
  termination, durable delivery ve outbound-only bağlantı kabulü tamamen
  Cloudflare'in ücretsiz katmanında; ALKAROS hiçbir relay sunucusu
  işletmez.
- **Domain sahipliği:** ALKAROS tek bir domain satın alır (örn.
  `alkaros.app`). Her restoran kuruluşunda, ALKAROS'un Cloudflare hesabı
  üzerinden bir API çağrısıyla `<restoran-slug>.siparis.alkaros.app`
  alt-alan adı ve buna bağlı tünel kaydı otomatik oluşturulur. Restoran
  kendi domain'ini almak **zorunda değildir**.
- **Kurulum:** Restoranın POS makinesinde tek bir kurulum komutu
  (`cloudflared service install --token <kurulum-anında-üretilen-token>`)
  çalıştırılır; servis olarak kaydolur, yeniden başlatmada kendiliğinden
  ayağa kalkar, `V0-ARC-009` kuralına uygun şekilde yalnız outbound bağlantı
  kurar.

## 2. Reddedilen alternatifler

| Alternatif | Neden reddedildi |
| --- | --- |
| **A — ALKAROS'un işlettiği merkezi çok-kiracılı relay servisi** (kendi sunucusu, kendi uptime/patch/on-call sorumluluğu) | Semih'in açık kararı: ALKAROS bir SaaS/hosting işletmesi olmak istemiyor ("Sen her restoran için neden böyle bir saas işe gireyim ki"). Sürekli işletilen bir servis, sürekli operasyonel sorumluluk demektir. |
| **C — Yalnız restoran Wi-Fi'ı, hiç public ingress yok** | Ayrı bir görüşmede "mantıksız" bulunarak reddedildi — müşterinin masada otururken mobil veri kullanma senaryosunu (Wi-Fi'a hiç bağlanmadan) kapsamıyor. |
| **Restoranın kendi domain'ini alması ve kendi Cloudflare hesabını açması** | Teknik olarak çalışır ama kurulum sürtünmesini restorana yıkar; "en basit uygulama" ilkesiyle çelişir. ALKAROS'un tek domain + otomatik alt-alan adı modeli bu sürtünmeyi ortadan kaldırıyor. |

## 3. ALKAROS'un merkezi sorumluluğu neden "SaaS işletmek" değil

- Canlı trafiği taşıyan altyapı (relay, queue, TLS) tamamen Cloudflare'de
  çalışır — ALKAROS'un işlettiği hiçbir sunucu yok.
- ALKAROS'un tek merkezi varlığı: bir domain kaydı + bir Cloudflare hesabı.
  Bunlar **çalışan bir servis değil**, DNS/tünel kaydı — kurulum anında bir
  kerelik API çağrısı.
- Bir restoranın tüneli kurulduktan sonra, ALKAROS'un hesabında sorun çıksa
  bile o restoranın **mevcut** tüneli çalışmaya devam eder; yalnız *yeni*
  restoran onboarding'i geçici olarak etkilenir. 7/24 izleme/on-call
  yükümlülüğü yok.

## 4. Etkilenen task ID'leri

- `V14-QRT-001` — bu kararın somutlaştırdığı provider/model ile, connector
  implementasyonunu `cloudflared` servis kaydı + Cloudflare Tunnel API
  entegrasyonu olarak uygular (görev dosyası buna göre güncellendi).
- `V0-QRG-001` — bu karardaki provider/model ile test edilecek; gerçek
  relay/domain/TLS/credentials ihtiyacı hâlâ karşılanmadı, görev hâlâ
  `Blocked`.

## 5. Hâlâ açık olan gerçek kaynak ihtiyacı

Bu karar bir *model* seçimidir, bir *kaynak* değildir. `V0-QRG-001`'in
ihtiyaç duyduğu gerçek şey değişmedi: en az bir gerçek domain adı ve
üzerine kurulmuş bir Cloudflare hesabı (test/geliştirme amaçlı). Bu karar
yalnızca o kaynak sağlandığında hangi somut adımların atılacağını
tanımlar.
