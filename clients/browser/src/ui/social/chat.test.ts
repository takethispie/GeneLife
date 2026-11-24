import { afterEach, describe, expect, it, vi } from "vitest";
import type { ChatMessage, ChatSendRequest, ChatSendResult, EmoteSendResult } from "../../contracts/hub";
import { ChatPanel } from "./chat";

const emptyHistory = () =>
  vi.fn().mockResolvedValue(new Response(JSON.stringify([]), { status: 200 }));

const reply = (text: string, overrides: Partial<ChatMessage> = {}): ChatMessage => ({
  id: "m1",
  playerId: "alice",
  displayName: "Alice",
  channel: 0,
  text,
  tick: 1,
  targetPlayerId: null,
  venueNetworkId: null,
  ...overrides
});

describe("chat panel", () => {
  afterEach(() => {
    document.body.replaceChildren();
    vi.restoreAllMocks();
  });

  it("sends a say message and renders the returned message", async () => {
    const root = document.createElement("div");
    document.body.appendChild(root);
    const sendChat = vi.fn(async (request: ChatSendRequest): Promise<ChatSendResult> => ({
      accepted: true,
      code: null,
      notice: null,
      message: reply(request.text),
      recipients: ["bob"]
    }));
    const panel = new ChatPanel({
      root,
      apiBaseUrl: "http://localhost",
      token: () => "token",
      fetchImpl: emptyHistory(),
      sendChat,
      sendEmote: vi.fn()
    });

    const input = root.querySelector<HTMLInputElement>(".social-chat-input");
    const form = root.querySelector<HTMLFormElement>(".social-chat-form");
    expect(input).not.toBeNull();
    input!.value = "hello world";
    form!.dispatchEvent(new Event("submit", { bubbles: true, cancelable: true }));

    await vi.waitFor(() => expect(sendChat).toHaveBeenCalled());
    expect(sendChat).toHaveBeenCalledWith({
      text: "hello world",
      channel: "Say",
      targetPlayerId: null
    });
    await vi.waitFor(() => expect(root.textContent).toContain("hello world"));
    expect(root.textContent).toContain("Alice");
    panel.dispose();
  });

  it("requires a target for a whisper and shows admission notices", async () => {
    const root = document.createElement("div");
    document.body.appendChild(root);
    const sendChat = vi.fn(async (): Promise<ChatSendResult> => ({
      accepted: false,
      code: "rate_limited",
      notice: "You are sending messages too quickly. Try again shortly.",
      message: null,
      recipients: []
    }));
    const panel = new ChatPanel({
      root,
      apiBaseUrl: "http://localhost",
      token: () => "token",
      fetchImpl: emptyHistory(),
      sendChat,
      sendEmote: vi.fn()
    });

    const channel = root.querySelector<HTMLSelectElement>(".social-chat-channel")!;
    const input = root.querySelector<HTMLInputElement>(".social-chat-input")!;
    const form = root.querySelector<HTMLFormElement>(".social-chat-form")!;

    channel.value = "Whisper";
    channel.dispatchEvent(new Event("change"));
    input.value = "secret";
    form.dispatchEvent(new Event("submit", { bubbles: true, cancelable: true }));

    await vi.waitFor(() => expect(root.textContent).toContain("Choose a friend"));
    expect(sendChat).not.toHaveBeenCalled();

    const target = root.querySelector<HTMLInputElement>(".social-chat-target")!;
    target.value = "bob";
    form.dispatchEvent(new Event("submit", { bubbles: true, cancelable: true }));
    await vi.waitFor(() => expect(sendChat).toHaveBeenCalled());
    expect(sendChat).toHaveBeenCalledWith({
      text: "secret",
      channel: "Whisper",
      targetPlayerId: "bob"
    });
    await vi.waitFor(() => expect(root.textContent).toContain("too quickly"));
    panel.dispose();
  });

  it("loads bounded history and appends pushed messages", async () => {
    const root = document.createElement("div");
    document.body.appendChild(root);
    const fetchImpl = vi.fn().mockResolvedValue(new Response(JSON.stringify([
      reply("from history")
    ]), { status: 200 }));
    const panel = new ChatPanel({
      root,
      apiBaseUrl: "http://localhost",
      token: () => "token",
      fetchImpl,
      sendChat: vi.fn(),
      sendEmote: vi.fn()
    });

    await vi.waitFor(() => expect(root.textContent).toContain("from history"));
    expect(fetchImpl).toHaveBeenCalledWith(
      "http://localhost/api/social/chat?channel=say&take=50",
      { headers: { Authorization: "Bearer token" } }
    );

    panel.addMessage(reply("pushed message", { id: "m2" }));
    expect(root.textContent).toContain("pushed message");

    panel.addMessage(reply("pushed message", { id: "m2" }));
    expect(root.querySelectorAll(".social-chat-row")).toHaveLength(2);
    panel.dispose();
  });

  it("sends predefined emotes and renders a short bubble", async () => {
    const root = document.createElement("div");
    document.body.appendChild(root);
    const sendEmote = vi.fn(async (emote: number): Promise<EmoteSendResult> => ({
      accepted: true,
      code: null,
      notice: null,
      emote: {
        id: "e1",
        playerId: "alice",
        displayName: "Alice",
        emote,
        tick: 1,
        venueNetworkId: null
      },
      recipients: ["bob"]
    }));
    const panel = new ChatPanel({
      root,
      apiBaseUrl: "http://localhost",
      token: () => "token",
      fetchImpl: emptyHistory(),
      sendChat: vi.fn(),
      sendEmote
    });

    const wave = root.querySelector<HTMLButtonElement>('[data-emote="0"]')!;
    wave.click();

    await vi.waitFor(() => expect(sendEmote).toHaveBeenCalledWith(0));
    await vi.waitFor(() => expect(root.textContent).toContain("Alice waves"));
    expect(root.querySelector(".social-chat-bubble")).not.toBeNull();
    panel.dispose();
  });
});
