# QR/NFC uzaktan sipariş bağlantısı — kurulum rehberi

> Source basis: PO:2026-09-07
> Companion docs: `docs/architecture/qr-relay-topology.md` (V0-ARC-009),
> `docs/architecture/qr-relay-provider-decision.md` (V12-QRT-002),
> `plan/v1.2/qr-transport/V12-QRT-003-relay-credential-configuration-ui.md`

Bu rehber, uzaktan (QR/NFC) sipariş bağlantısının çalışması için **bir kez**
yapılması gereken adımları tarif eder. Bu adımlar **restoran başına değil,
ALKAROS için bir kez** yapılır — hiçbir restoranın kendi domain'ine ya da
Cloudflare hesabına ihtiyacı yoktur (`V12-QRT-002` kararı).

Legend: **DONE** — repo'da hazır ve bugün kullanılabilir · **MANUAL** —
repo dışında, gerçek bir kaynak/hesap gerektirir · **BEKLIYOR** — henüz
kodlanmadı, ayrı bir görev (`V12-QRT-001`).

---

## 1. ALKAROS için bir kerelik kurulum — MANUAL

Bunlar restoranın değil, **ALKAROS'un** (işletmenin) sahip olacağı şeyler.

1. **Bir domain satın al** (örn. `alkaros.app` gibi — restoranların adıyla
   ilgisi yok, tamamen ALKAROS'un kendi markası). Bugün test için alınan
   domain (V0-QRG-001 kanıtı) bu adımın **dışında** kaldı, yalnızca
   fizibilite kanıtı içindi; production'da kullanılmayacak.
2. **Cloudflare hesabı aç**, domain'i bu hesaba ekle (nameserver'ları
   Cloudflare'e yönlendir — domain'i doğrudan Cloudflare'den alırsan bu
   otomatik olur).
3. **Bir API token oluştur** (Cloudflare panelinde: My Profile → API
   Tokens → Create Token → Custom Token), yalnızca şu iki izinle:
   - `Account` → `Cloudflare Tunnel` → `Edit`
   - `Zone` → `DNS` → `Edit`, kapsam: yalnızca yukarıda alınan tek domain
   Daha geniş bir izin **verilmemeli** — bu token ilerde restoran
   kurulumlarını otomatikleştirecek küçük bir sunucu tarafında saklanacak
   (bkz. adım 5), kapsamı ne kadar darsa sızıntı riski o kadar düşük olur.
4. **Sunucuda bir şifreleme anahtarı üret ve ortam değişkeni olarak ayarla**
   — bu, token'ın veritabanında şifreli saklanmasını sağlayan asıl anahtar,
   token'ın kendisinden ayrı ve daha önemli bir sır:

   ```bash
   # 32 baytlık rastgele anahtar, base64:
   openssl rand -base64 32
   ```

   Çıkan değeri ALKAROS Host'un çalıştığı sunucuda
   `ALKAROS_SECRET_ENVELOPE_MASTER_KEY` ortam değişkenine ata (Docker
   Compose kullanıyorsan `compose.yaml`'daki `environment:` bölümüne, ya da
   bir Docker secret dosyasına). Bu değer kaybolursa, o ana kadar kaydedilen
   token okunamaz hâle gelir — yedeğini güvenli bir yerde (parola
   kasası) tut.

## 2. Arayüzden bilgileri gir — DONE (bugün kullanılabilir)

Adım 1 tamamlandıktan sonra, Cloudflare panelinde şu üç bilgiyi de not al
(hiçbiri gizli değildir, token gibi korunması gerekmez):

- **Account ID** — panelin sağ alt köşesinde, herhangi bir domain
  sayfasında görünür.
- **Zone ID** — domain'i seçip Overview sayfasına gidince sağ tarafta
  görünür (aynı sayfada Account ID de tekrar görünür).
- **Ana alan adı** — adım 1.1'de aldığın domain'in kendisi (örn. `alkaros.app`).

Sonra:

1. ALKAROS'a **yönetici** hesabıyla giriş yap.
2. `/settings/relay` adresine git.
3. Adım 1.3'te oluşturduğun API token'ı ve yukarıdaki üç bilgiyi doldurup
   **Kaydet**'e bas.
4. Ekranda "Yapılandırıldı" durumunu göreceksin — token bir daha hiçbir
   ekranda görünmez, yalnızca şifreli olarak saklanır; Account ID/Zone
   ID/ana alan adı geri okunabilir (gizli değiller).

## 3. Restoran başına — DONE (bugün kullanılabilir)

Adım 2 tamamlandıktan sonra, aynı ekranda ikinci bir kart belirir:

1. Restoranı tanımlayan kısa bir **alt alan adı etiketi** yaz (örn. `sube1`
   — sonuç `sube1.<ana alan adı>` olur).
2. **Bağlantıyı Etkinleştir**'e bas. Backend, Cloudflare Tunnel'ı oluşturur,
   DNS kaydını ekler ve sonucu (`sube1.alkaros.app` gibi) ekranda gösterir.

Bundan sonra kalan tek adım — bu tünelin **restoranın kendi
bilgisayarında** bir arka plan servisi olarak (`cloudflared`) çalıştırılması
— henüz otomatik değil; bu, ayrı bir görevin kapsamı (LocalConnector,
`V12-QRT-001`'in tamamlanmamış kısmı). O tamamlandığında bu adım da
elle bir komut çalıştırmadan, arayüzden yapılabilecek.
