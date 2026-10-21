# qa-ux-tasarim — V1-RMD-597 ham rapor özeti

18/231 kalem (UX yüzeyi: yetki kararları 6 dosya, personel/roller 8, WaiterPwa 3; ek olarak giriş/PIN/kilit akışı dosyaları). Giriş ekranları canlı yığına GET ile görüntülendi (tmp/qa/V1-RMD-597/qa-ux-tasarim/*.png); PIN tuş takımı, yetki kararları ve personel ekranı oturum gerektirdiğinden gezilemedi — bulgular Kod okuması (KO).

| No | Önem | Kanıt | Özet |
|---|---|---|---|
| UX-01 | Yüksek | routes/workspace.tsx:542,548; ApiErrorCatalog.cs:76 | Onayla/Reddet/Devri geri al/Sıkılaştırmayı kaldır eylemlerinde hata yakalanmaz; kendi talebini onaylama (SELF_APPROVAL_NOT_ALLOWED) ve 409 durumlarında ekran sessiz kalır. |
| UX-02 | Yüksek | AuthorizationDecisionsWorkspace.tsx:32-34,111,134,160 | Yönetici talep edenin adını görmez; yalnız kimliğin ilk 8 karakteri ("Personel 3FA2B1C9"); rol ve talep yaşı modelde var ama gösterilmez. |
| UX-03 | Orta | kiosk-lock.js:221-224, auth.js:115-116 | 5 yanlış PIN sonrası mesaj kilit ekranına yazılır, sonra giriş ekranı açılır ve loginError boş; kalan hak söylenmez. |
| UX-04 | Orta | DualScreenApplication.Endpoints.cs:83, AuthenticationService.cs:21 | Kilitli hesapta doğru parola girilse bile "parola hatalı" denir; kullanıcı yöneticiye başvurması gerektiğini bilmez (kullanıcı sayımı sızıntısı kararı güvenlikle birlikte). |
| UX-05 | Orta | StaffSection.tsx:75, RecoveryPanel.tsx:62-63, sections.ts:40-41 | Personel ekranı "Kilitli" gösterir ama kilit açma düğmesi yok; başka sekmede (security.manage) ve ayrı yetki; ek 6+ dokunuş. |
| UX-06 | Orta | StaffSection.tsx:76-79, RolesPanels.tsx:77,78,122 | Pasifleştirme onaysız, izin verme onaysız/geri alma onaylı: tutarsız; açık oturumun akıbeti söylenmez. |
| UX-07 | Orta | kiosk-lock.js:129, waiter-app.js:145, Endpoints.cs:~141 | PIN kurulu bilgisi yalnız yerel bayrak; çıkış/cihaz değişiminde kilit kapalı başlar, her vardiya 5+ dokunuş (hasPin yanıtta yok). |
| UX-08 | Orta | kiosk-lock.js:94-95, waiter-app.js:584, Endpoints.cs:246 | PIN üst sınırı sunucuda yok, tuş takımı 12 haneyi aşamaz; 13+ haneli PIN girilemez. |
| UX-09 | Orta | kiosk-lock.js:171-178, waiter-app.js:403-407 | PIN girişi en az 4 rakam + ✓; otomatik gönderme yok; fiziksel klavye çalışmaz. |
| UX-10 | Orta | AuthorizationDecisionsWorkspace.tsx:94,135, workspace.tsx:532 | Ham ISO/UTC zaman damgası ekranda ("Son güncelleme 2026-10-21T14:03:11Z", "Bitiş …+00:00"). |
| UX-11 | Orta | authorization-decisions/models.ts:54,67; management-staff-roles/models.ts:57 | Haritada olmayan izin/gerekçe kodu ham yazılır; iki ekranda aynı izin farklı adlı; "Tanımsız izin" farklı izinleri aynı gösterir. |
| UX-12 | Orta | AuthorizationDecisionsWorkspace.tsx:114-115, workspace.tsx:540-542 | Bekleyen talepler otomatik yenilenmez; her eylem sonrası tüm ekran yükleniyor durumuna döner; başarı mesajı yok; tutar vurgulu değil. |
| UX-13 | Düşük | StaffSection.tsx:60-62, RolesPanels.tsx:115-129 | Hesap açma formunda rol alanı yok; yeni personel rolsüz kalır (ek 3+ dokunuş). |
| UX-14 | Düşük | management-staff-roles.css:9, tokens.css:93 | İzin onay kutusu satırı 48 px hedefin altında. |
| UX-15 | Düşük | index.html:216, Cashier.tsx, strings.ts:393, kiosk-lock.js:88, Endpoints.cs:83,~254 | "Şifre"/"Parola" terimi tutarsız. |
| UX-16 | Düşük | Cashier.tsx:368,369,383 | Kasa girişinde İngilizce "ALKAROS RESTAURANT OS" (marka kararı) ve çift durum rozeti. |
| UX-17 | Düşük | auth.js:20,28 | Yedek isim "Garson"; müdür/kasiyer girerse geçici "Garson" yazabilir. |

Fırsatlar: FR-01 hasPin yanıtta; FR-02 kilit ekranında kullanıcı adı ("Merhaba Ayşe"); FR-03 kasa/yönetim ekranına boşta kilit + PIN ile hızlı vardiya geçişi (PosTerminal'de boşta kilit yok); FR-04 garsona onay sonucu bildirimi (void-comp.js:173,255 yalnız toast; doğrulanamadı); FR-05 personel listesini yazmadan seçilebilir yap + rozet; FR-06 zayıf PIN (0000, 1234) reddi (güvenliğe).
Temiz: Türkçe metin (yalnız UX-16), ham hata mesajı yok, PIN çift dokunuş koruması (pinSubmitInFlight), odak tuzağı, PIN tuşları 68×58 px, yıkıcı işlemde onay (pasifleştirme hariç), yetki kararları ekranının yükleniyor/boş/çevrimdışı/hata/yetkisiz durumları, panel "Tekrar dene".
Doğrulanamayan: oturum gerektiren ekranların canlı gezisi (tohum hesabı yok), /garson/ adresinin kasa girişi göstermesi, mutfak/müdür akışları, göç tohumları ile etiket sözlüğü karşılaştırması.
