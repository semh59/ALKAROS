# V1-RMD-254 - Stop WaiterPwa table cards showing a wrong/duplicate "Boş" amount label

- Task ID: V1-RMD-254
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`src/Clients/WaiterPwa/wwwroot/js/screens/tables.js`'in `renderTables()`
fonksiyonunda (grep + kod okumayla doğrulandı), her masa kartının alt
satırındaki tutar alanı `busy ? formatMoney(table.amount) : 'Boş'` ile
render ediliyor — yani `table.status !== 'occupied'` olan **her** masa için
(Available, Reserved, Cleaning, OutOfService dahil) bu alan sabit
`'Boş'` metnini basıyor.

Bu iki ayrı, doğrulanmış sorun yaratıyor:

1. **Available (Boş) masalarda tam tekrar:** kartın durum etiketi zaten
   "Boş" yazıyor (`TABLE_STATUS.available.label`), alt satır aynı kelimeyi
   ikinci kez basıyor — gereksiz tekrar.
2. **Reserved/Cleaning/OutOfService masalarda yanlış bilgi:** durum
   etiketi "Rezerve"/"Toplanıyor"/"Servis dışı" derken, aynı kartın alt
   satırı "Boş" yazıyor — birbirini yalanlayan iki metin aynı kartta.
   Rezerve bir masa boş değildir; bu, garsonun bir bakışta yanlış anlayabileceği
   bir çelişki.

Gerçek ekranda bu 2026-09-19'da alınan bir ekran görüntüsünde de görüldü
(rakip kıyaslama turu, `evidence/reference/2026-09-19-competitor-ui-comparison/`).

## Owned surface

- `evidence/V1-RMD-254/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/wwwroot/js/screens/tables.js
  (V1-WTR-043 sahipliğinde, V1-RMD-169 de burada aynı desenle ek yapmıştı)
  — yalnız `renderTables()`'ın tutar span'i değişir: dolu olmayan masalarda
  sabit `'Boş'` yerine boş string basılır. Başka hiçbir davranış/mantık
  değişmez.

## Dependencies

- None

## Acceptance evidence

- `tests/Clients/StaticApps/waiter-app.test.js` değişiklik öncesi ve
  sonrası yeşil kalır (mevcut testler bu metne bağlı değil, doğrulandı).
- Gerçek Chromium ile önce/sonra ekran görüntüsü: Available, Reserved ve
  Cleaning durumundaki masa kartlarında alt satırın artık boş olduğu,
  "Boş" tekrarının/çelişkisinin kalktığı görülür.
- `dotnet build`/`dotnet test`: bu ortamda .NET SDK kurulu değil (aynı
  V1-RMD-252/253'teki gibi) — Owned surface yine yalnız statik bir `.js`
  dosyası.
- `python tools/plan-audit/plan_audit_tool.py validate` → yeni hata yok.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- Semih'in elle deneyebileceği senaryo: WaiterPwa'da Masalar ekranını aç;
  boş, rezerve ve toplanıyor durumundaki masa kartlarının alt satırında
  artık tutar alanı boş, durum etiketiyle çelişen/tekrar eden bir metin yok.

## Handoff

- None
