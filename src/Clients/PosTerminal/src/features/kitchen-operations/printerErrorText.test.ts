import { describe, expect, it } from "vitest";
import { printerErrorText } from "./models";

describe("printer save error text", () => {
  it.each([
    [400, "VALIDATION_FAILED", "ağ adresi işletme ağındaki"],
    [409, "DUPLICATE_RESOURCE", "Bu adda bir yazıcı zaten var."],
    [403, "FORBIDDEN", "yetkiniz yok"],
    [409, "DOMAIN_CONFLICT", "başka bir işlemle değiştirilmiş"],
    [409, "CONCURRENT_MODIFICATION", "başka bir işlemle değiştirilmiş"],
    [500, "INTERNAL_ERROR", "Yazıcı kaydedilemedi."],
  ])("maps %i %s to a Turkish sentence", (status, code, expected) => {
    const text = printerErrorText({ status, code, message: "raw server text" });

    expect(text).toContain(expected);
    expect(text).not.toContain("raw server text");
  });
});
