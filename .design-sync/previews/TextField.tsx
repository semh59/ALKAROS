import { TextField } from "@alkaros/pos-terminal";

export function Default() {
  return (
    <div style={{ maxWidth: 320 }}>
      <TextField label="Masa numarası" placeholder="örn. 14" inputMode="numeric" defaultValue="14" />
    </div>
  );
}

export function WithHint() {
  return (
    <div style={{ maxWidth: 320 }}>
      <TextField
        label="Müşteri adı"
        hint="Adisyon fişinde ve çağrı ekranında görünür."
        placeholder="Ad Soyad"
        defaultValue="Elif Demir"
      />
    </div>
  );
}

export function WithError() {
  return (
    <div style={{ maxWidth: 320 }}>
      <TextField
        label="Kapak ücreti"
        error="Tutar 0'dan büyük olmalı."
        defaultValue="0"
        inputMode="decimal"
      />
    </div>
  );
}

export function Disabled() {
  return (
    <div style={{ maxWidth: 320 }}>
      <TextField label="Adisyon no" defaultValue="A-2049" disabled />
    </div>
  );
}
