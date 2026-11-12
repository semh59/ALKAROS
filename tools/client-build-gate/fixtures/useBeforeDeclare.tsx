import { useEffect, useState } from "react";

export function Broken({ orderId }: { orderId: string | null }) {
  useEffect(() => { setReason(null); }, [orderId]);
  const [reason, setReason] = useState<string | null>(null);
  return <span>{reason}</span>;
}
