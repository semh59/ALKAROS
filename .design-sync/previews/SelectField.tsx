import { SelectField } from "@alkaros/pos-terminal";

export function Default() {
  return (
    <div style={{ maxWidth: 320 }}>
      <SelectField label="Servis alanı" defaultValue="salon">
        <option value="salon">Salon</option>
        <option value="teras">Teras</option>
        <option value="bar">Bar</option>
        <option value="paket">Paket servis</option>
      </SelectField>
    </div>
  );
}

export function WithHint() {
  return (
    <div style={{ maxWidth: 320 }}>
      <SelectField
        label="Servis aşaması"
        hint="Mutfak, siparişleri bu sıraya göre hazırlar."
        defaultValue="ana"
      >
        <option value="baslangic">Başlangıç</option>
        <option value="ana">Ana yemek</option>
        <option value="tatli">Tatlı / Kahve</option>
      </SelectField>
    </div>
  );
}

export function WithError() {
  return (
    <div style={{ maxWidth: 320 }}>
      <SelectField label="Garson" error="Bir garson seçmelisiniz." defaultValue="">
        <option value="">Seçiniz…</option>
        <option value="1">Kerem Yıldız</option>
        <option value="2">Sena Aktaş</option>
      </SelectField>
    </div>
  );
}
