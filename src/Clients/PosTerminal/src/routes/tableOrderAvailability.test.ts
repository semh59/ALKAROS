import { describe, expect, it } from "vitest";
import { canStartTableOrder } from "./tableOrderAvailability";

const table = (status: string, active = true) => ({ active, status });

describe("canStartTableOrder", () => {
  it("accepts an available table", () => expect(canStartTableOrder(table("Available"))).toBe(true));
  it("accepts a seated table, with or without an order to continue", () => expect(canStartTableOrder(table("Occupied"))).toBe(true));
  it("refuses an inactive table", () => expect(canStartTableOrder(table("Available", false))).toBe(false));
  it("refuses a reserved or cleaning table", () => {
    expect(canStartTableOrder(table("Reserved"))).toBe(false);
    expect(canStartTableOrder(table("Cleaning"))).toBe(false);
  });
  it("refuses when nothing is selected", () => expect(canStartTableOrder(null)).toBe(false));
});
