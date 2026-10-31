import { generate } from "lean-qr";
import { useMemo } from "react";

const quietZone = 4;

/** Draws the code from its module matrix, so no markup string is ever built from the address. */
export function QrCodeSvg({ text, label }: { text: string; label: string }) {
  const { path, size } = useMemo(() => {
    const code = generate(text);
    let d = "";
    for (let y = 0; y < code.size; y += 1) {
      for (let x = 0; x < code.size; x += 1) {
        if (code.get(x, y)) d += `M${x + quietZone} ${y + quietZone}h1v1h-1z`;
      }
    }
    return { path: d, size: code.size + quietZone * 2 };
  }, [text]);

  return <svg className="mtq__svg" role="img" aria-label={label} viewBox={`0 0 ${size} ${size}`} shapeRendering="crispEdges">
    <rect width={size} height={size} fill="#fff" />
    <path d={path} fill="#000" />
  </svg>;
}
