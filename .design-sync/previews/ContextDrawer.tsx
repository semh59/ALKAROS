import { Button, ContextDrawer, StateMessage } from "@alkaros/pos-terminal";

export function Persistent() {
  return (
    <div style={{ height: 380, width: 360, border: "1px solid var(--ds-color-line)", borderRadius: 8, overflow: "hidden", display: "grid" }}>
      <ContextDrawer presentation="persistent" title="Masa 14 · detay">
        <div style={{ display: "grid", gap: 10, font: "400 0.85rem/1.5 var(--ds-font-sans)", color: "var(--ds-color-ink)" }}>
          <div>6 ürün · ₺842,00</div>
          <div style={{ color: "var(--ds-color-muted)" }}>Garson: Kerem Yıldız</div>
          <div style={{ color: "var(--ds-color-muted)" }}>Açılış: 19:58 · 43 dk</div>
          <Button variant="secondary">Adisyonu yazdır</Button>
        </div>
      </ContextDrawer>
    </div>
  );
}

// presentation="sheet" is position:fixed; the transform wrapper contains it.
export function Sheet() {
  return (
    <div style={{ position: "relative", height: 380, transform: "translateZ(0)", overflow: "hidden", borderRadius: 8, background: "var(--ds-color-canvas)" }}>
      <ContextDrawer presentation="sheet" title="Değiştiriciler" onClose={() => {}}>
        <div style={{ display: "grid", gap: 10 }}>
          <StateMessage tone="warning" title="Pişirme derecesi zorunlu" />
          <Button variant="secondary">Az pişmiş</Button>
          <Button variant="secondary">Orta</Button>
          <Button variant="primary">Kaydet</Button>
        </div>
      </ContextDrawer>
    </div>
  );
}
