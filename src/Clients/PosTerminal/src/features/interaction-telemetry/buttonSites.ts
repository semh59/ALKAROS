export interface ButtonSite {
  readonly file: string;
  readonly line: number;
  readonly marked: boolean;
}

/** Finds the end of a JSX opening tag, skipping braces and quotes so arrow functions inside props do not end it early. */
function tagEnd(source: string, from: number): number {
  let depth = 0;
  let quote = "";
  for (let i = from; i < source.length; i += 1) {
    const ch = source[i]!;
    if (quote) {
      if (ch === quote && source[i - 1] !== "\\") quote = "";
    } else if (depth === 0 && (ch === '"' || ch === "'")) {
      quote = ch;
    } else if (ch === "{") {
      depth += 1;
    } else if (ch === "}") {
      depth -= 1;
    } else if (ch === ">" && depth === 0 && source[i - 1] !== "=") {
      return i;
    }
  }
  return source.length;
}

/** The tag with every quoted value and brace group removed, so only real attribute names remain. */
function attributeNames(tag: string): string {
  let depth = 0;
  let quote = "";
  let kept = "";
  for (const ch of tag) {
    if (quote) {
      if (ch === quote) quote = "";
    } else if (ch === "{") {
      depth += 1;
    } else if (ch === "}") {
      depth -= 1;
    } else if (depth === 0 && (ch === "\"" || ch === "'")) {
      quote = ch;
    } else if (depth === 0) {
      kept += ch;
    }
  }
  return kept;
}

/** Lists the button sites (<Button ...> and <button ...>) of one source file. */
export function findButtonSites(file: string, source: string): ButtonSite[] {
  const sites: ButtonSite[] = [];
  for (const match of source.matchAll(/<(?:Button|button)(?=[\s>/])/g)) {
    const start = match.index!;
    const tag = source.slice(start, tagEnd(source, start + match[0].length) + 1);
    sites.push({ file, line: source.slice(0, start).split("\n").length, marked: /\bdata-telemetry-id\s*=/.test(attributeNames(tag)) });
  }
  return sites;
}
