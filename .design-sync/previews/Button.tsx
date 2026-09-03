import { Button, Icon } from "@alkaros/pos-terminal";

export const Variants = () => (
  <div style={{ display: "flex", flexWrap: "wrap", gap: 12, alignItems: "center" }}>
    <Button variant="primary">Siparişi gönder</Button>
    <Button variant="secondary">Taslağı kaydet</Button>
    <Button variant="quiet">Vazgeç</Button>
  </div>
);

export const WithIcon = () => (
  <div style={{ display: "flex", flexWrap: "wrap", gap: 12, alignItems: "center" }}>
    <Button variant="primary">
      <Icon name="send" /> Mutfağa ilet
    </Button>
    <Button variant="secondary">
      <Icon name="add" /> Ürün ekle
    </Button>
    <Button variant="quiet">
      <Icon name="trash" /> Satırı sil
    </Button>
  </div>
);

export const Disabled = () => (
  <div style={{ display: "flex", gap: 12, alignItems: "center" }}>
    <Button variant="primary" disabled onClick={() => {}}>
      Gönderiliyor…
    </Button>
    <Button variant="secondary" disabled>
      Düzenlenemez
    </Button>
  </div>
);
