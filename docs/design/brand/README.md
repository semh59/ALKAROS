# ALKAROS marka varlıkları

Logonun kalıcı yeri burasıdır. Arayüz çalışırken logo aramaya gerek yok —
ihtiyaç duyulan varyant bu klasörden alınır.

| Dosya | Ne zaman kullanılır |
| --- | --- |
| `alkaros-logo.png` | Yatay kilit (AR monogramı + ALKAROS yazısı), **açık zeminde**. Varsayılan logo budur. |
| `alkaros-logo-on-dark.png` | Aynı kilit, **koyu zeminde** — lacivert konturlar beyaza döner, zemin şeffaftır. |
| `alkaros-mark.png` | Yalnız AR monogramı, açık zeminde. Dar alan (favicon, uygulama ikonu, üst bar). |
| `alkaros-mark-on-dark.png` | Yalnız monogram, koyu zeminde (şeffaf). Örn. `--color-ink` üst barı. |
| `alkaros-brand-sheet.png` | Semih'in verdiği orijinal marka sayfası: logo + resmî palet kutuları. Kaynak referans, doğrudan arayüzde kullanılmaz. |

## Kurallar

Renk değerleri `docs/design/foundations.md` §1'in kendisidir; buradaki
dosyalar o paletin görsel karşılığıdır ve ondan sapmaz:

- `--color-ink` `#0B2135` — monogramın koyu konturları, wordmark
- `--color-brand` `#1B4D7B` — ikincil marka
- `--color-accent` `#00CFFF` — monogramın camgöbeği hattı. Bu renk **her iki**
  zemin varyantında da aynen korunur; markanın kendi değeridir.
- `--color-text` `#222222`, `--color-canvas` `#F4F6F8`

Koyu zemin varyantlarında yalnız lacivert konturlar beyaza çevrilmiştir —
`#00CFFF` açık zeminde metin/ikon rengi olarak kullanılamayacağı için
(`foundations.md` §1.1, WCAG) monogramın okunurluğunu taşıyan katman
konturlardır, camgöbeği değildir.

Logoyu yeniden renklendirmek, döndürmek, oranını bozmak veya camgöbeği
hattını başka bir renge çevirmek marka dışıdır; yeni bir varyant gerekiyorsa
önce `foundations.md` güncellenir, sonra buraya dosya eklenir.
