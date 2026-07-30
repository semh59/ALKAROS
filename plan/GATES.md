# Version Gates

## Global kurallar

- `v0` kapanmadan production uygulama geliştirmesi başlamaz.
- Bir sürümün açık finansal, stok veya mevzuat kararı sonraki sürüme borç olarak
  taşınmaz.
- Dış entegrasyon sözleşmesi gerçek erişim olmadan tamamlanmış sayılmaz.
- Her migration boş PostgreSQL 18 veritabanında ileri ve geri doğrulanır.
- Her cached projection için source-of-truth, atomik güncelleme ve rebuild yolu
  belgelenmeden ilgili modül tamamlanmaz.

## Sürüm zinciri

`V0 -> V1 -> V11 -> V12 -> V13 -> V14 -> V15 -> V20`

## Sabit gate kimlikleri

| Gate | Kapanma koşulu |
| --- | --- |
| `GATE-V0-ENTRY` | PDF hash, başlangıç envanteri ve kaynak kayıtları doğrulanır. |
| `GATE-V0-EXIT` | Uygulanabilir V0 görevleri `Done`, dış kanıt bekleyenler açık `Blocked` ve tüketicileri başlamamış olur. |
| `GATE-V1-ENTRY` | `GATE-V0-EXIT` kapanır. |
| `GATE-V1-EXIT` | V1 görevleri, task-scope enforcement ve otomatik kanıtları tamamlanır. |
| `GATE-V11-ENTRY` | `GATE-V1-EXIT` kapanır. |
| `GATE-V11-EXIT` | V1.1 görevleri ve stok/reçete invariant kanıtları tamamlanır. |
| `GATE-V12-ENTRY` | `GATE-V11-EXIT` kapanır. |
| `GATE-V12-MEAL-CARD-ADAPTERS` | V0-MCD approved provider listesi ve her provider için generated adapter Done olur; liste boşsa downstream task'lar tarihli NotApplicable olur. |
| `GATE-V12-FSC-STRATEGY` | V0-CMP-001 strategy kararı ve yalnız seçilen Hugin veya QNB contract kanıtı Done olur; uygulanmayan branch tarihli NotApplicable olur. |
| `GATE-V12-EXIT` | V1.2 ödeme, fiscal ve cash görevlerinin uygulanabilir kapsamı tamamlanır. |
| `GATE-V13-ENTRY` | `GATE-V12-EXIT` kapanır. |
| `GATE-V13-EXIT` | V1.3 hesap ve invoicing görevlerinin uygulanabilir kapsamı tamamlanır. |
| `GATE-V14-ENTRY` | `GATE-V13-EXIT` kapanır. |
| `GATE-V14-EXIT` | V1.4 public channel, stock race ve reconciliation kanıtları tamamlanır. |
| `GATE-V15-ENTRY` | `GATE-V14-EXIT` kapanır. |
| `GATE-V15-EXIT` | V1.5 hardening, recovery ve runbook doğrulamaları tamamlanır. |
| `GATE-V20-ENTRY` | `GATE-V15-EXIT` kapanır. |
| `GATE-V20-EXIT` | `V20-REL-003` signed Approve; `V20-REL-004` ve `V20-REL-005` kanıtla `Done` olur. |

Bir gate, uygulanabilir görevlerde açık `Blocked`, kanıtsız `Done`, onaysız
`NotApplicable`, açık critical/high finding veya çözümlenmemiş
finansal/stok/mevzuat kararı varken kapanamaz.

Bir consumer, dependency'si `NotApplicable` olduğunda yalnız kendi acceptance
sözleşmesi bu sonucu açıkça ele alıyorsa başlayabilir; aksi durumda gate açık kalır.

`GATE-V1-ENTRY` sonrasında yalnız `V1-FND-001` başlatılır. Ardından sırasıyla
`V1-FND-003`, `V1-FND-004`, `V1-FND-005`, `V1-SEC-001`, `V1-SEC-002`,
`V1-FND-002` ve `V1-FND-006` tamamlanır. Bu sekiz görev gerçek kanıtla `Done`
olmadan başka hiçbir application görevi `InProgress` yapılamaz.

## Canlı veri kuralı

`V20-REL-003` signed Approve kararı üretmeden hiçbir sürüm gerçek müşteri veya
gerçek para ile çalıştırılmaz. Production yetkisi yalnız `V20-REL-004` task'ına
aittir. `V20-REL-002` yalnız sentetik ya da yetkili sanitize edilmiş veriyle
yapılan non-production pilot rehearsal görevidir.
