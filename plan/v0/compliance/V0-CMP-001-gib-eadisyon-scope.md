# V0-CMP-001 - Determine GIB and e-Adisyon scope

- Task ID: V0-CMP-001
- Status: Done
- Assignee: Semih (product owner)
- Work type: validation
- Surface state: Existing

## Source basis

- PDF:II.2.16
- PDF:II.3.12
- PDF:II.5.4
- PDF:III.19
- EXT:GIB-YNOKC-GUIDE
- EXT:GIB-TK2-4.0
- EXT:GIB-EADISYON
- EXT:GIB-VUK509-2026
- EXT:GIB-YNOKC-SSS

## Goal

YN ÖKC, adisyon/e-Adisyon ve 2026 GİB kurallarının hangi işletme olgularında (masada servis, gerçek usul
vergilendirme, e-Fatura/e-Arşiv mükellefiyeti) devreye girdiğini, GİB'in gerçek yayımlanmış metnine dayanarak
yazılı bir beyan matrisi olarak belgelemek — belirli bir işletmenin bu olgulara sahip olup olmadığına ALKAROS
karar vermez, her işletme kendi muhasebecisiyle doğruladığı olguyu kurulumda beyan eder (`V0-GOV-065`).

## Owned surface

- `evidence/v0/compliance/V0-CMP-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- İşletme tipi, belge başlangıç/kapanış ilişkisi, saklama, raporlama ve entegratör/device sorumluluğu.
- Kapsam dışı: e-İrsaliye belge akışı (restoran perakende satışında irsaliye düzenlenmez); belge süreçleri
  e-Fatura/e-Arşiv ve YN ÖKC/adisyon ile sınırlıdır. Gelecekte irsaliye gereksinimi çıkarsa yeni görev açılır.

## Out of scope

- Vergi hukuku yorumu üretmek veya QNB adapter kodlamak.

## Dependencies

- None

## Onay

Approved by Semih — Founder/Product Owner — 2026-09-18, via `V0-GOV-065`.
Görevin özgün Blocker'ı (hedef işletme profili için mali müşavir onaylı,
merkezi bir uygulanabilirlik kararı) self-declared configuration modeliyle
değiştirildi: ALKAROS hangi işletmenin yükümlü olduğuna karar vermez,
GİB'in gerçek yayımlanmış koşulunu (VUK 509 IV.12) doğru uygular; her
işletme üç gerçek olguyu (masada servis, gerçek usul, e-Fatura/e-Arşiv
mükellefiyeti) kendi muhasebecisiyle kurulumda beyan eder. Bu, Token/Beko
belgesinin e-Adisyon'u hukuken tam karşıladığı iddiasını KAPATMAZ — o soru
ayrı ve `V20-CMP-001`'in (nihai compliance sign-off) kapsamındadır.

## Deliverables

- V0-CMP-001 için tarihli ve kaynakları belirtilmiş evidence package
  (`evidence/v0/compliance/V0-CMP-001/gib-applicability-matrix.md`).
- 3 beyan olgusu ve 2^3 kombinasyonun her biri için doğru davranış eşlemesi.
- Doğrulanamayan maddeler için açık not; varsayımla kapatma yok.

## Acceptance evidence

- Güncel resmi GİB kaynak sürümleriyle (`EXT:GIB-VUK509-2026` IV.12 dahil) doğrudan eşlenen, işletmenin kendi
  beyanına dayalı bir uygulanabilirlik matrisi mevcut; matris named mali müşavir onayı YERİNE named product
  owner'ın (`V0-GOV-065`) onayladığı self-declaration modelini kullanır.

## Handoff

- V0-HUG-001
- V0-QNB-001
- V13-FSC-001
