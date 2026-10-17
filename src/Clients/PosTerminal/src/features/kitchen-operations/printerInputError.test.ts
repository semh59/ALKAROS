import { describe, expect, it } from "vitest";
import { printerInputError, type KitchenPrinterInput } from "./models";

const valid: KitchenPrinterInput = { name: "Bar", stationId: "bar", ipAddress: "192.168.1.50", port: 9100, profile: "Default", isActive: true };

describe("printerInputError", () => {
  it("accepts a valid private address", () => {
    expect(printerInputError(valid)).toBeNull();
  });

  it.each([
    ["blank name", { name: "  " }, "Yazıcı adı boş olamaz."],
    ["blank station", { stationId: "  " }, "İstasyon boş olamaz."],
    ["not four numbers", { ipAddress: "192.168.1" }, "Ağ adresi 192.168.1.50 gibi dört sayıdan oluşmalı."],
    ["five numbers", { ipAddress: "192.168.1.1.1" }, "Ağ adresi 192.168.1.50 gibi dört sayıdan oluşmalı."],
    ["octet above 255", { ipAddress: "192.168.1.256" }, "Ağ adresi 192.168.1.50 gibi dört sayıdan oluşmalı."],
    ["text octet", { ipAddress: "192.168.a.1" }, "Ağ adresi 192.168.1.50 gibi dört sayıdan oluşmalı."],
    ["public address", { ipAddress: "8.8.8.8" }, "Ağ adresi işletme ağındaki (192.168.x.x, 10.x.x.x, 172.16-31.x.x) bir adres olmalı."],
    ["172.32 outside the private block", { ipAddress: "172.32.0.1" }, "Ağ adresi işletme ağındaki (192.168.x.x, 10.x.x.x, 172.16-31.x.x) bir adres olmalı."],
    ["192.169 outside the private block", { ipAddress: "192.169.0.1" }, "Ağ adresi işletme ağındaki (192.168.x.x, 10.x.x.x, 172.16-31.x.x) bir adres olmalı."],
    ["port zero", { port: 0 }, "Port 1 ile 65535 arasında olmalı."],
    ["port above range", { port: 65536 }, "Port 1 ile 65535 arasında olmalı."],
    ["fractional port", { port: 91.5 }, "Port 1 ile 65535 arasında olmalı."],
  ])("names the field for %s", (_label, change, message) => {
    expect(printerInputError({ ...valid, ...change })).toBe(message);
  });

  it.each(["10.0.0.1", "172.16.0.1", "172.31.255.254", "192.168.0.1"])("accepts %s", (ipAddress) => {
    expect(printerInputError({ ...valid, ipAddress })).toBeNull();
  });

  it.each([1, 65535])("accepts port %i", (port) => {
    expect(printerInputError({ ...valid, port })).toBeNull();
  });
});
