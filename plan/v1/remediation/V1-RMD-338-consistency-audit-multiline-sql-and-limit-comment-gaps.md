# V1-RMD-338 - `consistency_audit.py`nin çok satırlı SQL ve LIMIT/yorum boşlukları kapatıldı

- Task ID: V1-RMD-338
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) orta seviye bulgusu: "`consistency_audit.py`'nin cross-schema
yazma kontrolü çok satırlı SQL'i, LIMIT kontrolü ise yorum/string içindeki 'limit' kelimesini kaçırıyor." İki
GERÇEK, yeniden üretilebilir boşluk doğrulandı (aşağıdaki kanıta bkz.):

1. Rule 5/7 (cross-schema yazma): `_WRITE_TARGET_RE.findall(raw)` her satırı TEK TEK tarıyordu. Bu kod
   tabanının kendi `"""..."""` (triple-quoted) ham SQL dizgileri fiilin (`UPDATE`) ve şema-nitelikli hedefin
   (`other_schema.table`) AYRI satırlara yazılmasını rutin olarak destekliyor — bu durumda regex hiçbir satırda
   ikisini birlikte göremiyor ve ihlal asla bildirilmiyor.
2. Rule 6 (LIMIT kontrolü): `_LIMIT_RE.search(body_text)` metot gövdesinin HAM metnini arıyordu — C#'ın kendi
   `//` yorumları dahil. Gövdede SQL'le hiç ilgisi olmayan İngilizce bir yorum ("// no limit needed here...")
   `\bLIMIT\b` ile eşleşip kontrolün GERÇEK bir SQL `LIMIT`'i var sanmasına, dolayısıyla asıl sınırsız SELECT'in
   hiç bildirilmemesine yol açıyordu.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): tools/consistency-audit/consistency_audit.py
- `plan/v1/remediation/V1-RMD-338-consistency-audit-multiline-sql-and-limit-comment-gaps.md`

## In scope

1. Yeni bir paylaşılan `_find_write_targets(text)` yardımcı fonksiyonu: `_WRITE_TARGET_RE`'yi artık tek bir
   satır üzerinde değil, dosyanın TAMAMI üzerinde (`finditer`) çalıştırıyor — `\s+` zaten satır sonlarını
   aşabiliyor, yalnızca eşleşmenin BAŞLADIĞI satırın numarasını hesaplayıp aynı "yorum satırını atla" kuralını
   koruyor. Hem rule 5 (src/Modules) hem rule 7 (src/Host) bu tek yardımcıyı kullanacak şekilde birleştirildi.
2. Yeni `_strip_line_comments(text)`: LIMIT araması yapılmadan ÖNCE metot gövdesindeki `//`/`///` yorumlarını
   temizliyor — gerçek bir SQL `LIMIT`'i asla bir C# yorumunun içinde yaşamaz, bu yüzden bu temizlik hiçbir
   gerçek pozitifi kaybetmiyor. `_SQL_FROM_RE` araması (bir SELECT olup olmadığı) VE rule 5/7'nin kendi taraması
   bilerek HAM metin üzerinde kalıyor — yalnızca LIMIT araması için uygulanıyor.

## Out of scope

1. LIMIT'in bir STRING literal içinde (SQL dışı, örn. bir hata mesajı `"rate limit exceeded"`) yanlışlıkla
   eşleşmesi — bunu güvenilir şekilde ayırt etmek gerçek bir SQL ayrıştırıcısı gerektirir; bu, bulgunun kendi
   "yorum" kısmından daha büyük, ayrı bir iyileştirme. Bu görev bilinçli olarak yalnızca yorumları temizliyor,
   dokümante edilmiş bir kalan sınırlama olarak string literal riski not edildi.
2. Blok yorumları (`/* ... */`) temizlemek — bu kod tabanının tarandı, hiçbir `.cs` dosyasında bu stil
   kullanılmıyor (yalnızca `//`/`///` XML doc yorumları var); eklemek şu an test edilemeyen, gerçek bir kullanım
   örneği olmayan kod olurdu.

## Dependencies

- None

## Acceptance evidence

- GERÇEK iki repro dosyası oluşturuldu (geçici, sonra silindi):
  `src/Modules/Kitchen/ScratchRmd338MultilineWrite.cs` (fiil ve hedefi ayrı satırlarda bir `UPDATE billing.bills`)
  ve `src/Modules/Kitchen/ScratchRmd338LimitFalseNegative.cs` (LIMIT'siz bir SELECT + "no limit needed here" yorumu).
- Eski koda karşı (`git stash`): `consistency-audit: clean` — HER İKİ gerçek ihlal de sessizce kaçırıldı,
  denetimin bulgusunu birebir doğruluyor.
- Düzeltilmiş koda karşı: `consistency-audit: 2 violation(s)` — her iki dosya doğru satır numarası ve doğru
  mesajla bildirildi.
- `git stash pop` ile düzeltme geri getirildi (`diff` ile bayt-bayt doğrulandı), aynı iki dosyaya karşı yeniden
  aynı 2 ihlal bildirildi.
- Repro dosyaları silindi, `python tools/consistency-audit/consistency_audit.py` bu deponun TAMAMINA karşı
  yeniden `consistency-audit: clean` verdi — değişikliğin gerçek kod tabanında hiçbir yeni yanlış pozitif
  üretmediği doğrulandı.

## Handoff

- None
