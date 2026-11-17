# V1-RMD-1147 - Ürün ızgarasında hızlı dokunuşlar sessizce kaybolmaz

- Task ID: V1-RMD-1147
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi 15 (bulgu 9): satış ekranında ürün ızgarasına 50 ms ve daha sık aralıkla dokunulunca bazı dokunuşlar hiçbir uyarı olmadan kayboluyor. Ekranda zaten bir dokunuş kuyruğu var (bekleyen dokunuşlar bir önceki yanıtın sürümüyle sırayla gönderilir); eksik, kuyruğun hangi koşulda dokunuş yuttuğunun bulunması ve düzeltilmesidir.

1. Önce kök neden bulunur: değişken sunucu gecikmesiyle (0-80 ms) ve 20-50 ms aralıkla art arda dokunuş yapan bir zamanlama testi yazılır; her dokunuşun ya sunucuya tam bir kez gittiği ya da kullanıcıya "N dokunuş eklenmedi" denildiği doğrulanır. Kaybı yeniden üreten durum (örneğin dokunuşun sipariş okunurken, kuyruk kapanırken ya da başka bir işlemin `busy` durumunda `addProduct` başında sessizce atılması) kodda gösterilir.
2. Kayıp üretilirse düzeltilir: dokunuş ya kuyruğa girer ya da açık bir Türkçe uyarıyla reddedilir; sessiz kayıp kalmaz. Aynı ürüne art arda dokunuş tek istekte toplanır, sunucuya iki kez gitmez (anahtarsız ekleme çift kayıt üretebileceğinden yeniden gönderme eklenmez).
3. Kayıp üretilemezse bu RESULT'ta kanıtla (test çıktısı) yazılır, zamanlama testi yine kalıcı güvence olarak eklenir ve iş "yeniden üretilemedi" olarak kapatılır; tahmini düzeltme yapılmaz.

## Owned surface

- `evidence/V1-RMD-1147/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.rapid-taps.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Zamanlama testi, kök neden, (varsa) düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. PosTerminal testleri

## Out of scope

- Sunucu tarafı kalem ekleme sözleşmesi; yineleme anahtarı eklenmesi; başka ekranlar

## Dependencies

- V1-RMD-1146

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi
3. PosTerminal testleri exit code 0

## Handoff

- None
