import type {
  ChatChannel,
  ChatEmote,
  ChatMessage,
  ChatSendRequest,
  ChatSendResult,
  EmoteKind,
  EmoteSendResult
} from "../../contracts/hub";
import type { HudDock } from "../hud-dock";

const EMOTES: ReadonlyArray<{ kind: EmoteKind; value: number }> = [
  { kind: "Wave", value: 0 },
  { kind: "Nod", value: 1 },
  { kind: "ShakeHead", value: 2 },
  { kind: "Sit", value: 3 },
  { kind: "Stand", value: 4 },
  { kind: "Cheer", value: 5 },
  { kind: "Point", value: 6 }
];

const EMOTE_LABELS: Record<number, string> = {
  0: "waves",
  1: "nods",
  2: "shakes their head",
  3: "sits down",
  4: "stands up",
  5: "cheers",
  6: "points"
};

export interface ChatPanelOptions {
  root: HTMLElement;
  apiBaseUrl: string;
  token: () => string | null;
  sendChat: (request: ChatSendRequest) => Promise<ChatSendResult>;
  sendEmote: (emote: number) => Promise<EmoteSendResult>;
  dock?: HudDock;
  fetchImpl?: typeof fetch;
  maxLength?: number;
  onError?: (message: string) => void;
}

/**
 * DOM chat panel and emote bar. It sits outside the Pixi render loop, sends
 * through the hub, and renders only the bounded public fields the server
 * returns. Chat message text is always inserted with textContent.
 */
export class ChatPanel {
  private readonly options: ChatPanelOptions;
  private readonly root: HTMLElement;
  private readonly log: HTMLOListElement;
  private readonly notice: HTMLElement;
  private readonly form: HTMLFormElement;
  private readonly channelSelect: HTMLSelectElement;
  private readonly targetInput: HTMLInputElement;
  private readonly textInput: HTMLInputElement;
  private readonly seen = new Set<string>();
  private readonly timers = new Set<ReturnType<typeof setTimeout>>();
  private disposed = false;

  constructor(options: ChatPanelOptions) {
    this.options = options;
    this.root = document.createElement("section");
    this.root.className = "social-chat";
    this.root.setAttribute("aria-label", "Chat");

    const header = document.createElement("div");
    header.className = "social-chat-header";
    header.textContent = "Chat";
    this.root.appendChild(header);

    this.log = document.createElement("ol");
    this.log.className = "social-chat-log";
    this.log.setAttribute("aria-live", "polite");
    this.root.appendChild(this.log);

    this.notice = document.createElement("div");
    this.notice.className = "social-chat-notice";
    this.notice.setAttribute("role", "status");
    this.root.appendChild(this.notice);

    this.form = document.createElement("form");
    this.form.className = "social-chat-form";
    this.channelSelect = document.createElement("select");
    this.channelSelect.className = "social-chat-channel";
    this.channelSelect.setAttribute("aria-label", "Chat channel");
    for (const channel of ["Say", "Venue", "Whisper"] as const) {
      const option = document.createElement("option");
      option.value = channel;
      option.textContent = channel;
      this.channelSelect.appendChild(option);
    }

    this.targetInput = document.createElement("input");
    this.targetInput.className = "social-chat-target";
    this.targetInput.type = "text";
    this.targetInput.placeholder = "Whisper to…";
    this.targetInput.hidden = true;

    this.textInput = document.createElement("input");
    this.textInput.className = "social-chat-input";
    this.textInput.type = "text";
    this.textInput.maxLength = options.maxLength ?? 240;
    this.textInput.placeholder = "Say something";
    this.textInput.setAttribute("aria-label", "Message");

    const send = document.createElement("button");
    send.type = "submit";
    send.textContent = "Send";

    this.form.append(this.channelSelect, this.targetInput, this.textInput, send);
    this.root.appendChild(this.form);

    const emoteBar = document.createElement("div");
    emoteBar.className = "social-chat-emotes";
    emoteBar.setAttribute("role", "group");
    emoteBar.setAttribute("aria-label", "Emotes");
    for (const emote of EMOTES) {
      const button = document.createElement("button");
      button.type = "button";
      button.className = "social-chat-emote";
      button.dataset.emote = String(emote.value);
      button.textContent = emote.kind;
      button.addEventListener("click", () => void this.sendEmote(emote.value));
      emoteBar.appendChild(button);
    }
    this.root.appendChild(emoteBar);

    this.channelSelect.addEventListener("change", () => {
      this.targetInput.hidden = this.channelSelect.value !== "Whisper";
    });
    this.form.addEventListener("submit", (event) => {
      event.preventDefault();
      void this.sendMessage();
    });

    if (options.dock) {
      options.dock.addPanel({
        id: "chat",
        label: "Chat",
        icon: "chat",
        order: 20,
        content: this.root,
        onOpen: () => void this.refresh()
      });
    } else {
      options.root.appendChild(this.root);
    }
    void this.refresh();
  }

  addMessage(message: ChatMessage): void {
    if (this.disposed || this.seen.has(message.id)) return;

    this.seen.add(message.id);
    if (this.seen.size > 500) {
      const oldest = this.seen.values().next().value;
      if (oldest !== undefined) this.seen.delete(oldest);
    }

    const row = document.createElement("li");
    row.className = "social-chat-row";
    row.dataset.channel = String(message.channel);

    const author = document.createElement("span");
    author.className = "social-chat-author";
    author.textContent = message.displayName ?? "Resident";

    const text = document.createElement("span");
    text.className = "social-chat-text";
    text.textContent = message.text;

    row.append(author, text);
    this.log.appendChild(row);
    while (this.log.children.length > 200) this.log.firstElementChild?.remove();
    this.log.scrollTop = this.log.scrollHeight;
  }

  showEmote(emote: ChatEmote): void {
    if (this.disposed) return;

    const bubble = document.createElement("div");
    bubble.className = "social-chat-bubble";
    bubble.textContent = `${emote.displayName ?? "Resident"} ${EMOTE_LABELS[Number(emote.emote)] ?? "gestures"}`;
    this.root.appendChild(bubble);

    const timer = setTimeout(() => {
      bubble.remove();
      this.timers.delete(timer);
    }, 2500);
    this.timers.add(timer);
  }

  async refresh(): Promise<void> {
    const token = this.options.token();
    if (token === null || this.disposed) return;

    try {
      const fetcher = this.options.fetchImpl ?? fetch;
      const channel = this.channelSelect.value.toLowerCase();
      const target = this.targetInput.value.trim();
      const suffix = channel === "whisper" && target !== ""
        ? `&targetPlayerId=${encodeURIComponent(target)}`
        : "";
      const response = await fetcher(
        `${this.options.apiBaseUrl}/api/social/chat?channel=${channel}&take=50${suffix}`,
        { headers: { Authorization: `Bearer ${token}` } }
      );
      if (!response.ok) return;

      const items = (await response.json()) as ChatMessage[];
      if (!Array.isArray(items)) return;
      this.log.replaceChildren();
      this.seen.clear();
      for (const item of items) this.addMessage(item);
    } catch (error) {
      this.options.onError?.(error instanceof Error ? error.message : "Chat history unavailable");
    }
  }

  dispose(): void {
    this.disposed = true;
    for (const timer of this.timers) clearTimeout(timer);
    this.timers.clear();
    this.root.remove();
  }

  private async sendMessage(): Promise<void> {
    if (this.disposed) return;

    const text = this.textInput.value.trim();
    if (text === "") return;

    const channel = this.channelSelect.value as ChatChannel;
    const targetPlayerId = this.targetInput.value.trim();
    if (channel === "Whisper" && targetPlayerId === "") {
      this.setNotice("Choose a friend to whisper.");
      return;
    }

    try {
      const result = await this.options.sendChat({
        text,
        channel,
        targetPlayerId: channel === "Whisper" ? targetPlayerId : null
      });
      if (!result.accepted) {
        this.setNotice(result.notice ?? "Message not sent.");
        return;
      }

      if (result.message !== null) this.addMessage(result.message);
      this.textInput.value = "";
      this.setNotice("");
    } catch (error) {
      this.setNotice(error instanceof Error ? error.message : "Message not sent.");
    }
  }

  private async sendEmote(emote: number): Promise<void> {
    if (this.disposed) return;

    try {
      const result = await this.options.sendEmote(emote);
      if (!result.accepted) {
        this.setNotice(result.notice ?? "Emote not sent.");
        return;
      }
      if (result.emote !== null) this.showEmote(result.emote);
      this.setNotice("");
    } catch (error) {
      this.setNotice(error instanceof Error ? error.message : "Emote not sent.");
    }
  }

  private setNotice(message: string): void {
    if (this.disposed) return;
    this.notice.textContent = message;
  }
}
