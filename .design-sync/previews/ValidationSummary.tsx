import { ValidationSummary } from "@alkaros/pos-terminal";

export function Default() {
  return (
    <div style={{ maxWidth: 420 }}>
      <ValidationSummary
        title="Siparişi gönderemedik"
        errors={[
          "Masa 14 için bir garson seçilmedi.",
          "2 numaralı satırda pişirme derecesi zorunlu.",
          "Kapak ücreti 0'dan büyük olmalı.",
        ]}
      />
    </div>
  );
}

export function SingleError() {
  return (
    <div style={{ maxWidth: 420 }}>
      <ValidationSummary
        title="Kaydedilemedi"
        errors={["İnternet bağlantısı yok — değişiklikler kuyruğa alındı."]}
      />
    </div>
  );
}
