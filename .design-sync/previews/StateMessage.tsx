import { Button, StateMessage } from "@alkaros/pos-terminal";

export function Tones() {
  return (
    <div style={{ display: "grid", gap: 12, maxWidth: 460 }}>
      <StateMessage tone="info" title="3 masa serviste">
        Salon bölümünde 3 aktif adisyon var.
      </StateMessage>
      <StateMessage tone="success" title="Sipariş mutfağa iletildi">
        Masa 14 · 6 ürün · 20:41
      </StateMessage>
      <StateMessage tone="warning" title="Son 3 porsiyon">
        Izgara köfte kritik stokta. Yeni sipariş bloke edilebilir.
      </StateMessage>
      <StateMessage tone="error" title="Ürün 86'ya düştü">
        Levrek ızgara için sipariş alınamıyor.
      </StateMessage>
    </div>
  );
}

export function Offline() {
  return (
    <div style={{ maxWidth: 460 }}>
      <StateMessage
        tone="offline"
        title="Çevrimdışı — kuyrukta 4 işlem"
        actions={<Button variant="secondary">Yeniden dene</Button>}
      >
        Bağlantı geldiğinde bekleyen adisyonlar otomatik gönderilir.
      </StateMessage>
    </div>
  );
}

export function Conflict() {
  return (
    <div style={{ maxWidth: 460 }}>
      <StateMessage
        tone="conflict"
        title="Adisyon başka bir cihazda değişti"
        actions={
          <>
            <Button variant="primary">Yeniden yükle</Button>
            <Button variant="quiet">Farkları gör</Button>
          </>
        }
      >
        Kasiyer terminali bu masayı 20:39'da güncelledi.
      </StateMessage>
    </div>
  );
}
