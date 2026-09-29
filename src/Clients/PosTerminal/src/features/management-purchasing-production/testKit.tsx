import { act, type ReactElement } from "react";
import { createRoot, type Root } from "react-dom/client";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

let root: Root | null = null;

export async function render(element: ReactElement) {
  document.documentElement.lang = "tr";
  document.body.innerHTML = '<div id="root"></div>';
  root = createRoot(document.getElementById("root")!);
  await act(async () => root!.render(element));
}

export async function unmount() {
  if (root) await act(async () => root!.unmount());
  root = null;
}

export const buttons = () => [...document.querySelectorAll("button")].map((button) => button.textContent);

export async function press(text: string) {
  const button = [...document.querySelectorAll("button")].find((candidate) => candidate.textContent === text)!;
  await act(async () => button.click());
}

export async function type(id: string, value: string) {
  const input = document.getElementById(id) as HTMLInputElement | HTMLSelectElement;
  const proto = input instanceof HTMLSelectElement ? HTMLSelectElement.prototype : HTMLInputElement.prototype;
  await act(async () => {
    Object.getOwnPropertyDescriptor(proto, "value")!.set!.call(input, value);
    input.dispatchEvent(new Event(input instanceof HTMLSelectElement ? "change" : "input", { bubbles: true }));
  });
}

export async function submit(label: string) {
  const form = document.querySelector(`form[aria-label="${label}"]`)!;
  await act(async () => { form.dispatchEvent(new Event("submit", { bubbles: true, cancelable: true })); });
}

export const alertText = () => document.querySelector('[role="alert"]')?.textContent;
