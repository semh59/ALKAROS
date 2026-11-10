import { describe, expect, it } from "vitest";
import { canStartTableOrder } from "./tableOrderAvailability";

const table = (status: string, currentOrderId: string | null, active = true) => ({ active, status, currentOrderId });

describe("canStartTableOrder", () => {
  it("accepts an available table", () => expect(canStartTableOrder(table("Available", null))).toBe(true));
  it("accepts a seated table that has no order yet", () => expect(canStartTableOrder(table("Occupied", null))).toBe(true));
  it("accepts a table that already has an order to continue", () => expect(canStartTableOrder(table("Occupied", "o-1"))).toBe(true));
  it("refuses an inactive table", () => expect(canStartTableOrder(table("Available", null, false))).toBe(false));
  it("refuses a reserved or cleaning table", () => {
    expect(canStartTableOrder(table("Reserved", null))).toBe(false);
    expect(canStartTableOrder(table("Cleaning", null))).toBe(false);
  });
  it("refuses when nothing is selected", () => expect(canStartTableOrder(null)).toBe(false));
});
