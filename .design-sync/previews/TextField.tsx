import { TextField } from "@alkaros/pos-terminal";

export const Default = () => (
  <div style={{ maxWidth: 320 }}>
    <TextField label="Masa numarası" placeholder="örn. 14" inputMode="numeric" defaultValue="14" />
  </div>
);

export const WithHint = () => (
  <div style={{ maxWidth: 320 }}>
    <TextField
      label="Müşteri adı"
      hint="Adisyon fişinde ve çağrı ekranında görünür."
      placeholder="Ad Soyad"
      defaultValue="Elif Demir"
    />
  </div>
);

export const WithError = () => (
  <div style={{ maxWidth: 320 }}>
    <TextField label="Kapak ücreti" error="Tutar 0'dan büyük olmalı." defaultValue="0" inputMode="decimal" />
  </div>
);

export const Disabled = () => (
  <div style={{ maxWidth: 320 }}>
    <TextField label="Adisyon no" defaultValue="A-2049" disabled />
  </div>
);
