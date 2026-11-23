import type { KeyBindingAction, KeyBindings } from "../ui/settings";

export class InputMap {
  constructor(private bindings: KeyBindings) {}

  update(bindings: KeyBindings): void {
    this.bindings = bindings;
  }

  matches(event: Pick<KeyboardEvent, "key">, action: KeyBindingAction): boolean {
    const keys = this.bindings[action] ?? [];
    return keys.includes(event.key.toLowerCase());
  }

  actionFor(event: Pick<KeyboardEvent, "key">): KeyBindingAction | null {
    const key = event.key.toLowerCase();
    for (const [action, keys] of Object.entries(this.bindings) as [KeyBindingAction, string[]][]) {
      if (keys.includes(key)) return action;
    }
    return null;
  }

  describe(action: KeyBindingAction): string {
    return (this.bindings[action] ?? []).join(" / ");
  }
}

export function isTypingTarget(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;
  const tag = target.tagName.toLowerCase();
  return (
    tag === "input" ||
    tag === "textarea" ||
    tag === "select" ||
    target.isContentEditable ||
    target.closest("[data-hud-control]") !== null
  );
}
