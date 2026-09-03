import { Button, Icon } from "@alkaros/pos-terminal";

export const Gallery = () => {
  const names = [
    "sales", "tables", "billing", "kitchen", "catalog", "system",
    "check", "refresh", "offline", "clock", "info", "warning", "close",
    "conflict", "lock", "forbidden", "brand", "user", "register",
    "add", "send", "trash", "pause", "recall", "gift", "search",
  ] as const;
  return (
    <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(84px, 1fr))", gap: 8, maxWidth: 560 }}>
      {names.map((name) => (
        <div
          key={name}
          style={{
            display: "grid",
            justifyItems: "center",
            gap: 6,
            padding: "12px 4px",
            border: "1px solid var(--ds-color-line)",
            borderRadius: "var(--ds-radius-sm)",
            color: "var(--ds-color-ink)",
          }}
        >
          <Icon name={name} />
          <span style={{ font: "400 11px/1.2 var(--ds-font-sans)", color: "var(--ds-color-muted)" }}>{name}</span>
        </div>
      ))}
    </div>
  );
};

export const Sizes = () => (
  <div style={{ display: "flex", gap: 20, alignItems: "flex-end", color: "var(--ds-color-brand)" }}>
    {[16, 24, 32, 44].map((px) => (
      <span key={px} style={{ fontSize: px, display: "inline-flex", alignItems: "center", gap: 6 }}>
        <Icon name="kitchen" label={`${px} piksel`} />
        <span style={{ font: "400 12px/1 var(--ds-font-sans)", color: "var(--ds-color-muted)" }}>{px}px</span>
      </span>
    ))}
  </div>
);

export const InContext = () => (
  <div style={{ display: "grid", gap: 12, maxWidth: 320 }}>
    <h3 style={{ margin: 0, display: "flex", alignItems: "center", gap: 8, font: "700 1rem/1.2 var(--ds-font-sans)", color: "var(--ds-color-ink)" }}>
      <Icon name="tables" /> Masa düzeni
    </h3>
    <Button variant="primary">
      <Icon name="recall" /> Turu tekrarla
    </Button>
    <p style={{ margin: 0, display: "flex", alignItems: "center", gap: 6, font: "400 0.85rem/1.4 var(--ds-font-sans)", color: "var(--ds-color-muted)" }}>
      <Icon name="clock" /> 20–45 dk · yemekte
    </p>
  </div>
);
