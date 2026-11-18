import { describe, expect, it, vi } from "vitest";

// The second test only means something after the first ran: it reads what the first one left behind.
describe("page timer teardown", () => {
  const ticks = vi.fn();

  it("starts a page interval that a finished test would leave behind", async () => {
    window.setInterval(ticks, 20);
    await new Promise((done) => setTimeout(done, 50));
    expect(ticks).toHaveBeenCalled();
  });

  it("finds that interval stopped in the next test", async () => {
    ticks.mockClear();
    await new Promise((done) => setTimeout(done, 80));
    expect(ticks).not.toHaveBeenCalled();
  });
});
