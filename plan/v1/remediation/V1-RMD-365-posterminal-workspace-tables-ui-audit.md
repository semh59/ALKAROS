# V1-RMD-365 - PosTerminal workspace.tsx + Masa (Tables) modül denetimi: bulgu yok

- Task ID: V1-RMD-365
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: investigation
- Surface state: Existing

## Source basis

- PO:2026-09-27

## Goal

Modül-modül arayüz denetim sürecinin (plan/v1/ui-audit/UI_AUDIT_PROGRESS.md) 6. modülü:
`src/Clients/PosTerminal/src/routes/workspace.tsx` (582 satır, tüm rota yönlendirme kabuğu) ve
`src/Clients/PosTerminal/src/features/tables/**` (`TableWorkspace.tsx` 402 satır, `FloorPlanWorkspace.tsx`
507 satır, `tableApi.ts`, `models.ts`, toplam ~2774 satır). On iki boyut üzerinden tam olarak
tarandı; **gerçek, eyleme geçirilebilir bir bulgu YOK.**

Bu, sürecin kendi kuralının öngördüğü meşru bir sonuç ("bulunan sorunlar çözülmeden diğerine
geçmek yok" — burada çözülecek gerçek bir sorun bulunamadı, uydurulmadı). Modül 4'ün (WaiterPwa)
"az bulgu" sonucundan bir adım öteye geçip Modül 5'in (PosTerminal Cashier.tsx) 2 bulgusundan da
daha olgun çıktı — muhtemelen bu kod tabanının en dikkatli tasarlanmış köşesi:

- **Kat planı (`FloorPlanWorkspace.tsx`) sürükle-bırak masa düzenleyicisi zaten tam bir klavye
  eşdeğerine sahip**: ok tuşları taşır, Shift+ok boyutlandırır, Ctrl 1px hassasiyet verir, R
  döndürür — VE bunun ötesinde, işaretleyici kullanamayan bir kullanıcı için ayrı, tam işlevsel
  bir "Erişilebilir masa listesi" (`floor-plan-dense-list`) sunuyor. Bu, çoğu üretim POS
  ürününün bile atladığı bir erişilebilirlik derinliği.
- Her masa düğmesinin `aria-label`'ı durumu + bağlamı + (kurulum modundaysa) klavye ipucunu tek
  cümlede özetliyor.
- `ModalDialog`/`ContextDrawer` (paylaşılan `design-system/primitives.tsx`) zaten tam bir odak
  tuzağı + Escape kapatma + tetikleyiciye odak-geri-dönüşü uyguluyor — Cashier vanilla'nın
  (Modül 1) sıfırdan kurduğu desenden daha DRY, tek merkezi bir uygulama.
- Filtre/görünüm düğme grupları (`role="group"` + `aria-pressed`) zaten tutarlı, kasıtlı bir
  desen — Modül 5'in kategori-rayı bulgusundaki "hiç ARIA yok" durumundan farklı olarak burada
  ARIA zaten var, yalnızca farklı (ama geçerli) bir desen seçimi (toggle-button-group, tab/radio
  yerine) — bu bir hata değil, kod tabanı genelinde tutarlı bir tasarım kararı (aynı desen zaten
  axe'ten geçen 13 diğer dosyada da kullanılıyor).
- `ProductionShell` (paylaşılan kabuk) zaten kendi `ProductionShell.test.tsx`'inde axe taranıyor.
- Sistematik CSS/JS sınıf adı uyumsuzluğu taraması (workspace.tsx + TableWorkspace.tsx +
  FloorPlanWorkspace.tsx'teki 90 benzersiz statik sınıf adı, `tables.css`/`floorPlan.css`/
  `styles.css`/`shell.css`'e karşı) sıfır gerçek eksik buldu (birkaç yanlış-pozitif: durum
  dizgileri ternary'lerden yakalanmış, `table-workspace__table-area` CSS'siz ama ebeveyni
  (`table-workspace__content`, CSS grid) çocuğun kendi stiline ihtiyaç duymadan onu zaten
  konumlandırıyor — gerçek bir kusur değil).
- `tableApi.ts` — her eylem için sunucunun beklediği `expectedRowVersion`/reason alanlarını
  doğru taşıyor, ağ hatası ile sunucu hatasını ayırıyor (`TableManagementApiError`).

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/ui-audit/UI_AUDIT_PROGRESS.md
- `plan/v1/remediation/V1-RMD-365-posterminal-workspace-tables-ui-audit.md`

## In scope

- Yok — kod değişikliği yapılmadı.

## Out of scope

- Ürün-katmanı (P1-P4) gözlemi de yok: masa yönetimi rakip POS ürünleriyle (Toast'un masa
  haritası) kıyaslanabilir, hatta erişilebilirlik derinliğinde onları geçiyor; öğrenme eşiği
  düşük (ok tuşu ipuçları ekranda görünür durumda).

## Dependencies

- None

## Acceptance evidence

- Kod değişikliği yapılmadığı için test/mutation-check gerekmedi. Mevcut test paketi
  (`TableWorkspace.test.tsx`, `FloorPlanWorkspace.test.tsx`, `tableApi.test.ts`, ikisi de kendi
  axe taramasını zaten içeriyor) zaten `src/Clients/PosTerminal`'in tam paketiyle (269 test)
  Modül 5'in kapanışında yeşil doğrulandı; bu modülde ek bir değişiklik yapılmadığından yeniden
  çalıştırmaya gerek yoktu.
- **Düzeltme notu (aynı gün):** yukarıdaki "gerçek, eyleme geçirilebilir bir bulgu YOK" sonucu
  eksikti. Modül 7'yi (billing) kapatırken bulunan sistemik bir hata sınıfı deseni,
  `workspace.tsx`'teki `TableRoute.load()`/`handleSaveFloorPlan()`'da da GERÇEKTEN mevcuttu —
  `tableApi.ts`'nin kendi `TableManagementApiError`'ı yalnızca paylaşılan `ApiError` kontrol
  edilerek atlanıyordu; ilk taramada bu, `TableWorkspace.tsx`'in kendi (doğru) `errorMessage()`
  yardımcısıyla karıştırılmış, `workspace.tsx`'in ayrı rota-seviyesi `load()`'u kontrol
  edilmemişti. Bulgu V1-RMD-367'de düzeltildi — ayrıntı ve kanıt orada.

## Handoff

- V1-RMD-367
