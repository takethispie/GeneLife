import { describe, expect, it, vi } from "vitest";
import { RespawnPopin } from "./respawn-popin";

describe("RespawnPopin", () => {
  it("respawns and closes on success", async () => {
    const root = document.createElement("div");
    const onRespawn = vi.fn().mockResolvedValue({ ok: true });
    const popin = new RespawnPopin({ root, onRespawn });

    popin.show({ name: "Ada", age: 72 });
    expect(popin.isOpen).toBe(true);
    expect(root.textContent).toContain("Ada died at age 72");

    (root.querySelector("button") as HTMLButtonElement).click();

    await vi.waitFor(() => expect(popin.isOpen).toBe(false));
    expect(onRespawn).toHaveBeenCalledTimes(1);
  });

  it("stays open and surfaces the error when respawn fails", async () => {
    const root = document.createElement("div");
    const onRespawn = vi.fn().mockResolvedValue({ ok: false, message: "No parent sim" });
    const popin = new RespawnPopin({ root, onRespawn });

    popin.show({ name: "Ada" });
    const button = root.querySelector("button") as HTMLButtonElement;
    button.click();

    await vi.waitFor(() => expect(root.textContent).toContain("No parent sim"));
    expect(popin.isOpen).toBe(true);
    expect(button.disabled).toBe(false);
    expect(button.textContent).toBe("Respawn as child");
  });

  it("ignores repeat clicks while a request is in flight", async () => {
    const root = document.createElement("div");
    let resolve: (value: { ok: boolean }) => void = () => undefined;
    const onRespawn = vi.fn(
      () => new Promise<{ ok: boolean }>((innerResolve) => {
        resolve = innerResolve;
      })
    );
    const popin = new RespawnPopin({ root, onRespawn });

    popin.show({ name: "Ada" });
    const button = root.querySelector("button") as HTMLButtonElement;
    button.click();
    button.click();

    expect(button.disabled).toBe(true);
    expect(onRespawn).toHaveBeenCalledTimes(1);

    resolve({ ok: true });
    await vi.waitFor(() => expect(popin.isOpen).toBe(false));
  });
});
