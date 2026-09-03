import { Button, ModalDialog } from "@alkaros/pos-terminal";

// ModalDialog renders a position:fixed backdrop. The capture cell is a
// containing block (transform), so it stays inside the card; the wrapper just
// gives it a predictable size.
function Frame({ children }: { children: React.ReactNode }) {
  return (
    <div style={{ position: "relative", height: 440, transform: "translateZ(0)", overflow: "hidden", borderRadius: 8 }}>
      {children}
    </div>
  );
}

export function Open() {
  return (
    <Frame>
      <ModalDialog open title="Siparişi iptal et" onClose={() => {}}>
        <p style={{ margin: 0, font: "400 0.95rem/1.5 var(--ds-font-sans)", color: "var(--ds-color-muted)" }}>
          Masa 14 için henüz mutfağa iletilmemiş 6 ürün var. İptal edilirse bu
          satırlar silinir ve işlem geri alınamaz.
        </p>
        <div style={{ display: "flex", gap: 12, justifyContent: "flex-end" }}>
          <Button variant="quiet">Vazgeç</Button>
          <Button variant="primary">Siparişi iptal et</Button>
        </div>
      </ModalDialog>
    </Frame>
  );
}
