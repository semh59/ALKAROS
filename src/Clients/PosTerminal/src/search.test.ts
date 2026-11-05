import { describe, expect, it } from "vitest";
import { foldSearch } from "./search";

describe("foldSearch", () => {
  it.each([
    ["ISPARTA", "isparta"],
    ["Isparta", "isparta"],
    ["ıspar", "ispar"],
    ["İSTANBUL", "istanbul"],
    ["Çorbası", "çorbasi"],
  ])("folds %s to %s", (input, expected) => expect(foldSearch(input)).toBe(expected));
});
