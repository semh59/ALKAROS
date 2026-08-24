# V1-WTR-005 - Browser E2E runner reliability

- Task ID: V1-WTR-005
- Status: InProgress
- Assignee: Codex-/root
- Work type: implementation
- Surface state: Existing

## Goal

Browser E2E runner'daki sessiz hata yutma, boş stderr handler ve kişiye özel Chrome profil yolu davranışlarını
kaldırmak; başlatma hatalarını deterministik raporlamak ve geçici kaynakları her çıkış yolunda temizlemek.

## Owned surface

- `tools/run_e2e_browser_test.js`
- `tools/tests/run_e2e_browser_test.test.js`

## In scope

- Chrome executable ve geçici profil yolunu platform-bağımsız, doğrulanabilir girdilerden çözmek.
- CDP başlatma denemelerinde son hatayı ve sınırlı stderr çıktısını korumak.
- Chrome process, WebSocket ve geçici profil temizliğini başarı/hata yollarında garanti etmek.

## Out of scope

- WebPrototype ürün davranışını veya UI assertion kapsamını değiştirmek.
- Yeni tarayıcı otomasyonu bağımlılığı eklemek.

## Dependencies

- V1-WTR-004

## Acceptance evidence

- Node testleri executable/profile çözümü, bounded stderr ve CDP retry hata raporlamasını doğrular.
- `node --check tools/run_e2e_browser_test.js` exit 0 verir.
- `node --test tools/tests/run_e2e_browser_test.test.js` exit 0 verir.
- `python -B tools/task-scope/task_scope_tool.py --task-id V1-WTR-005 --format text` exit 0 verir.
- Semih, geçersiz `ALKAROS_CHROME_PATH` ile runner'ı başlatıp açık executable hatası aldığını görebilir.
