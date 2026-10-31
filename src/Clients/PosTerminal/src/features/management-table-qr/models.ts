export interface TableQrRow {
  tableId: string;
  tableNumber: string;
  zoneName: string | null;
  tableActive: boolean;
  qrIssuedAt: string | null;
}

export interface TableQrCode {
  tableId: string;
  qrUrl: string;
}
