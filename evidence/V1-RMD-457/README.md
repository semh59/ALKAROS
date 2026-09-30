# V1-RMD-457 - kabul kanıtı

- PosTerminal `pnpm run typecheck`, `pnpm run lint` ve `pnpm test` exit code 0 (`typecheck.log`, `lint.log`, `vitest.log`;
  54 dosya, 404 test).
- İzinli ve izinsiz oturum: Roller ve Personelin rolleri panelleri yalnız `identity.roles.manage` olan oturuma çizilir; izin
  yoksa hiçbir düğmesi yoktur. Bu denetim kaldırılınca test kırmızı (`mutation-permission-gate.log`); dosya geri yüklendi, `cmp` aynı.
- Testler ayrıca: sunucunun gönderdiği 37 iznin hepsinin Türkçe adı olması, İngilizce izin adının ve ham izin kodunun ekranda
  görünmemesi, hazır rollerin Türkçe adları (Yönetici, Kasiyer, Garson...), izin vermenin anında, geri almanın onay sorarak
  yapılması (Vazgeç hiçbir şey göndermez), rol kodu doğrulaması, kişide olmayan rollerin listelenmesi, rol geri almada onay, tanımsız
  rol kimliği için nötr ad ve sunucunun Türkçe gerekçesinin aynen görünmesi.
- Gerçek deneme (`gercek-deneme.log`, `gercek-deneme-roller.png`): temiz PostgreSQL veritabanı, gerçek Host, tarayıcıdan gerçek giriş.
  Hazır 9 rol Türkçe adlarıyla listelendi; geçersiz rol kodu reddedildi; rol oluşturuldu; aynı rol tekrar denenince sunucunun Türkçe
  gerekçesi göründü; iki izin verildi, biri onayla geri alındı (sunucuda yalnız "tables.status" kaldı); rol bir personele atandı ve
  o personel giriş yaptığında yetkileri tam olarak ["tables.status"] geldi. Ekranda ham izin kodu yok.
- Deneme iki hata yakaladı ve düzeltildi: iki panel ayrı veri tutuyordu (yeni rol "Personelin rolleri" listesinde görünmüyordu;
  artık ortak yenileme sayacıyla birlikte yenileniyor), her değişiklikte panel "Yükleniyor" durumuna dönüp kayıyordu (artık eski
  veri gösterilirken arkada yenileniyor).
- Bilinen sınırlar: yeni izin tanımlama ve rol silme bu görevde yok. Yeni bir izin kodu eklenirse Türkçe adı `models.ts` sözlüğüne
  eklenmelidir (aksi halde "Tanımsız izin" görünür). Kendi yönetici rolünden izin geri almak mümkündür; ekran yalnız onay sorar.
- Semih'in elle deneyebileceği senaryo: yönetici oturumuyla Yönetim > Personel ve roller'de yeni bir rol oluşturun, ona birkaç izin
  verin, bir personele atayın; o personelle giriş yapıp yalnız o izinlerin geldiğini görün. İzni olmayan oturumda bu panellerin görünmediğini görün.
