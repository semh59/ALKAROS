import { useEffect, useState } from "react";

export function Fixed({ orderId }: { orderId: string | null }) {
  const [reason, setReason] = useState<string | null>(null);
  useEffect(() => { setReason(null); }, [orderId]);
  return <span>{reason}</span>;
}
