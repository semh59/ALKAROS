# V1-RMD-259 - Masa durumu bilinmeyen bir değer için ham İngilizce yerine Türkçe gösterir

- Task ID: V1-RMD-259
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

`docs/engineering/e2e-playwright-master-test-plan.md`'nin §0'ında (2026-09-24,
E2E ana planı yazılırken tesadüfen bulunan gerçek üretim boşlukları listesi,
madde 2) kayıtlı bulguyu kapatır.

`src/Clients/WaiterPwa/wwwroot/js/screens/tables.js`'in `TABLE_STATUS`
sözlüğü, tanınmayan bir `table.status` değeri için
`{ label: table.status, cls: '' }`'e düşüyordu — yani sunucudan gelecek
eşlenmemiş/gelecekteki/hatalı bir durum kodu, ham ve çevrilmemiş haliyle
doğrudan garsonun ekranına basılabilirdi (`docs/UI_STYLE_GUIDE.md` §3
ihlali).

**Gerçek erişilebilirlik incelendi**: `src/Modules/Tables/TableLifecycle/
TableState.cs`'in gerçek enum'u tam olarak 5 değer tanımlıyor (`Available`,
`Occupied`, `Reserved`, `Cleaning`, `OutOfService`), ve `database/migrations/
V1/V1-TBL-001/010-tables.up.sql`'in `current_status` sütunu bu 5 değeri
CHECK kısıtıyla DB seviyesinde zorluyor — `TABLE_STATUS` bu 5'inin tamamını
zaten doğru eşliyor (bug yalnız fallback'teydi, eksik bir eşleme değil).
Bu, bilinmeyen bir değerin bugün GERÇEK bir kullanıcı akışından asla
üretilemeyeceği anlamına geliyor (CHECK kısıtı bunu engelliyor) — ama
fallback'in kendisi hâlâ savunma katmanı olarak yanlıştı: gelecekte yeni bir
durum eklenirse, bir migration hatası CHECK'i geçersiz kılarsa, ya da
bozuk/elle-eklenmiş veri varsa, aynı sızıntı gerçekleşirdi.

## Owned surface

- `plan/v1/remediation/V1-RMD-259-table-status-unknown-fallback-turkish.md` (yeni)
- Sınırlı ek:
  - src/Clients/WaiterPwa/wwwroot/js/screens/tables.js (V1-WTR-043/048
    sahipliğinde) — yalnız `TABLE_STATUS[table.status] || {...}` fallback
    satırı, ham `table.status`'u değil sabit "Bilinmiyor" Türkçe etiketini
    döndürecek şekilde değiştirildi.

## Out of scope

- `TABLE_STATUS`'un kendi 5 girdisi — zaten doğru, dokunulmadı.
- Backend'in `TableState` enum'u veya DB CHECK kısıtı — zaten doğru,
  dokunulmadı.

## Dependencies

- None

## Acceptance evidence

- Gerçek kaynak kod okunarak doğrulandı: `TableState.cs`'in 5 değeri ile
  `TABLE_STATUS`'un 5 anahtarı (`available`/`occupied`/`reserved`/
  `cleaning`/`outofservice`) birebir eşleşiyor; `010-tables.up.sql:19`'un
  CHECK kısıtı aynı 5 değeri DB seviyesinde zorluyor.
- **Gerçek bir E2E ile kanıtlanamadı, dürüstçe kaydedilir**: DB'nin kendi
  CHECK kısıtı bilinmeyen bir `current_status` değerinin seed edilmesini
  engelliyor — bu senaryoyu gerçek Postgres'e karşı sahnelemek, kısıtı
  bilerek atlamayı (ör. kısıtı geçici kaldırıp elle INSERT) gerektirir ki bu
  gerçekçi olmayan, yapay bir test üretir. Bunun yerine doğrulama kod
  okuma + `node --check` ile sınırlı kaldı.
- `node --check src/Clients/WaiterPwa/wwwroot/js/screens/tables.js` → temiz.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None
