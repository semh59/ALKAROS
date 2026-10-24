# qa-ux-tasarim - V1-RMD-601 (SHA c794e951)

Kapsam: 123/123 (16 dosya satır satır, 6 kısmen, kalan 101 sunucu/test/göç dosyası UX açısından sınıflandırıldı; İngilizce sızıntı taraması: dilim istemci dosyalarında 0). Ekran görüntüsü alınamadı (giriş hesabı yok, dev parolası yasak); görsel bulgular kod ve hesaplama kanıtıdır. tsc/vitest ana oturum kaydı yeşil.

| No | Önem | Kanıt | Sorun / etki / öneri |
|---|---|---|---|
| UX-01 | Yüksek | `TableManagementContracts.cs:287-291`, `FloorPlanWorkspace.tsx:438` | Sunucu Unmerge yayınlamaz; düğme hep pasif, `title` dokunmatikte görünmez. Semih senaryosu arayüzden tamamlanamaz. Bilinen (RMD032-F004; e2e planı `docs/engineering/e2e-playwright-master-test-plan.md:123` pasif durumu bekler). (qa-frontend F-1.) |
| UX-02 | Yüksek | `workspace.tsx:205`, `:241`, `TableWorkspace.tsx:333` | 8 sn yoklama her turda "loading": FloorPlanWorkspace sökülür, düzenleme taslağı ve açık modal kaybolur, ekran yanıp söner; yorum `:236-238` yanlış; test yalnız çağrı sayısı sayar (`workspace.test.tsx:592-626`). (qa-frontend F-2.) |
| UX-03 | Yüksek | `TableWorkspace.tsx:289`, `workspace.tsx:299`, `ReservationStation.tsx:144`, `:21-25` | Rezervasyon istasyonu canManage={false}: Onayla sessizce hiçbir şey yapmaz. (qa-frontend F-3.) |
| UX-04 | Orta | `TableWorkspace.tsx:209,477,291`, `sheets/transfer.js:51` | Devir/birleştirme/rezervasyon için zorunlu serbest metin gerekçe; garson uygulaması varsayılan doldurur; masaüstü klavye ister. |
| UX-05 | Orta | `models.ts:157-167`, `tableApi.ts:82`, `Contracts.cs:155-162` | Rezervasyon formu yalnız kişi sayısı + gerekçe; saat/ad/telefon yok; kartta saat/kişi görünmez. |
| UX-06 | Orta | `TableWorkspace.tsx:473,475,468`, `Contracts.cs:288` | Devir/birleştirme listesi tüm aktif masaları filtresiz sunar (dolu/rezerve/servis dışı/birleşik); garson uygulaması yalnız boş masaları listeler; birleştirme yalnız dolu masada başlar (ürün kararı mı kusur mu: karar verilmedi). |
| UX-07 | Orta | `TableWorkspace.tsx:469-479`, `:445-449`, `:476` | Yıkıcı işlemlerde onay diyaloğu sonucu özetlemez; Unmerge metni "güncel satır sürümleriyle ayrılacaktır"; geri alma yok. |
| UX-08 | Orta | `TableWorkspace.tsx:92-94`, `tableApi.ts:56`, `workspace.tsx:224`, `FloorPlanWorkspace.tsx:252`, UI_STYLE_GUIDE §3 | Sunucu `error.message` olduğu gibi gösterilir (bilinen V1-RMD-114/367); hata kodu→Türkçe sözlük yok. |
| UX-09 | Orta | `FloorPlanWorkspace.tsx:252-253`, `workspace.tsx:259` | TableManagementApiError instanceof ApiError değil; çakışma tespiti hiç çalışmaz, regex mesaj metnine dayanır. (qa-frontend F-9.) |
| UX-10 | Orta | `workspace.tsx:217`, `:239-243`, `tableApi.ts:49` | Yoklama başarısızlığında "offline", kat görünümü gizlenir, yoklama durur; son veri bantla korunmaz. |
| UX-11 | Orta | `tables.js:26-28`, `:113-114`, `transfer.js:19-21` | PWA masa listesi isteği başarısızsa "Bu bölgede masa yok."/"Şu anda boş masa yok."; hata/çevrimdışı ayrımı yok. (qa-frontend F-7.) |
| UX-12 | Orta | `floorPlan.css:79-81`, `:48` | Kat planı durum metni 0,48-0,64 rem (8-10 px), rozet 7,7 px. |
| UX-13 | Orta | hesaplama (node -e): `#00cfff` beyaz üzeri 1,85:1; `#00aad1` `#fff0eb` üzeri 2,47, beyaz üzeri 2,74; `tables.css:28,142,163,164`, `tokens.css:30` | Accent rengi metin olarak WCAG 4,5:1'in çok altında: hızlı eylem düğmesi metni, dolu rozeti, kicker. |
| UX-14 | Orta | `TableWorkspace.tsx:497,472,505,508,495`, `FloorPlanWorkspace.tsx:327,411,423,441` | Çalışana "v12", "Satır sürümü", "Sunucu yetkisi ve v… kaynak gerçek", GUID parçası; tutar/süre kartta yok (garson uygulaması gösterir). |
| UX-15 | Orta | `FloorPlanWorkspace.tsx:437-439,431`, `TableWorkspace.tsx:507` | Pasif düğme nedeni yalnız `title`'da. |
| UX-16 | Orta | `grep Unmerge features/tables/*.test.tsx` 0 (çıkarım) | UX-01/02/03 testlerden kaçmış (test-kalitesi ajanına devir). |
| UX-17 | Düşük | `TableWorkspace.tsx:348-390`, `:336-338` | Boş durum metni yetkisiz kullanıcıya da "yönetici olarak başlayın"; yetkisiz durumda giriş düğmesi yok. |
| UX-18 | Düşük | `TableWorkspace.tsx:384-387`, `:408` | Özet sayaçları Toplanıyor/Servis dışı içermez; toplam diğerlerinin toplamı değil. |
| UX-19 | Düşük | `tables.js:77-83`, `waiter-app.css:422,471`, `tables.js:172-235` | PWA "Servis dışı" "Toplanıyor" ile aynı sarı; servis dışı masa açılabilir (sunucu reddi doğrulanamadı). |
| UX-20 | Düşük | `tables.css:46`, `TableWorkspace.tsx:453,464`, `floorPlan.css:116`, `waiter-app.css:459-464` | "Etkin" kutusu varsayılan boyutta; `touch-action: none` operasyon modunda da (768-1023 px tablette kaydırma); garson etiketleri ~11 px. |
| UX-21 | Düşük | `TableWorkspace.tsx:196-198`, `workspace.tsx:196`, `tableApi.ts:112` | Kat planı hatası sessizce liste görünümüne düşer; Unmerge için mergeGroupId yok. |
| UX-22 | Düşük | `FloorPlanWorkspace.tsx:332,334,450,249,352`, `TableWorkspace.tsx:353` | Klavye ipucu dokunmatikte anlamsız; "İncele ve kaydet" doğrudan kaydeder; "atomik"; "SALON GİRİŞİ ↑" sabit. |
| UX-23 | Düşük | `ReservationStation.tsx:43`, `:62` | Oturum geri yükleme hatasında her durum "anonymous"; ham mesaj. |

Temiz: İngilizce sızıntı 0, durum/eylem etiketleri Türkçe (`strings.ts:1105-1127`); garson karosu 112 px, hızlı ekle 48 px; durum yalnız renkle verilmez; yükleme/çevrimdışı/hata/eski/yetkisiz dalları Türkçe, çakışma ayrı, bölge silme onayı; başarı/uyarı/tehlike/bilgi kontrastı 6:1+; birleştirme/devir ana yol istek gövdesi doğru.
Doğrulanamayan: gerçek tarayıcı ekran görüntüsü (UX-02, 12, 13 canlı gözlenemedi); Semih senaryosu veri tarafı; boş masada birleştirme ürün kararı; PWA canlı yenileme; garsonun birleştirme yapamamasının bilinçli rol kararı olduğu; servis dışı masaya dokunma sunucu davranışı.
