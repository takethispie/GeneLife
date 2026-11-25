import type { HudDock } from "../hud-dock";

export interface RelationshipInvitationStatus {
  id: string;
  fromPlayerId: string;
  toPlayerId: string;
  kind: string;
  status: string;
  expiresTick: number;
  createdTick: number;
}

export interface PlayerRelationshipStatus {
  playerId: string;
  partnerPlayerId: string | null;
  partnerGenosNetworkId: number | null;
  partnerName: string | null;
  sharesHousehold: boolean;
  canCohabit: boolean;
  invitations: RelationshipInvitationStatus[];
}

export interface RelationshipActionOutcome {
  status: string;
  message?: string;
}

export interface RelationshipsPanelOptions {
  root: HTMLElement;
  apiBaseUrl: string;
  token: () => string | null;
  sendAction: (kind: string, payload: Record<string, unknown>) => Promise<RelationshipActionOutcome>;
  dock?: HudDock;
  fetchImpl?: typeof fetch;
  onError?: (message: string) => void;
}

/**
 * Player partnership and cohabitation panel. It reads the bounded social
 * relationship status and sends player actions through the tick-start command
 * path; it never mutates relationship state locally.
 */
export class RelationshipsPanel {
  private readonly options: RelationshipsPanelOptions;
  private readonly root: HTMLElement;
  private readonly partner: HTMLElement;
  private readonly invitations: HTMLUListElement;
  private readonly notice: HTMLElement;
  private readonly inviteForm: HTMLFormElement;
  private readonly targetInput: HTMLInputElement;
  private readonly cohabitButton: HTMLButtonElement;
  private readonly endButton: HTMLButtonElement;
  private status: PlayerRelationshipStatus | null = null;
  private disposed = false;

  constructor(options: RelationshipsPanelOptions) {
    this.options = options;
    this.root = document.createElement("section");
    this.root.className = "social-relationships";
    this.root.setAttribute("aria-label", "Relationships");

    const header = document.createElement("div");
    header.className = "social-relationships-header";
    header.textContent = "Relationships";
    this.root.appendChild(header);

    this.partner = document.createElement("div");
    this.partner.className = "social-relationships-partner";
    this.partner.textContent = "No partner";
    this.root.appendChild(this.partner);

    this.invitations = document.createElement("ul");
    this.invitations.className = "social-relationships-invitations";
    this.root.appendChild(this.invitations);

    const actions = document.createElement("div");
    actions.className = "social-relationships-actions";

    this.inviteForm = document.createElement("form");
    this.inviteForm.className = "social-relationships-invite";
    this.targetInput = document.createElement("input");
    this.targetInput.type = "text";
    this.targetInput.placeholder = "Player id";
    this.targetInput.setAttribute("aria-label", "Partner player id");
    const inviteButton = document.createElement("button");
    inviteButton.type = "submit";
    inviteButton.textContent = "Invite";
    this.inviteForm.append(this.targetInput, inviteButton);

    this.cohabitButton = document.createElement("button");
    this.cohabitButton.type = "button";
    this.cohabitButton.textContent = "Cohabit";
    this.cohabitButton.addEventListener("click", () => void this.inviteCohabit());

    this.endButton = document.createElement("button");
    this.endButton.type = "button";
    this.endButton.textContent = "End partnership";
    this.endButton.addEventListener("click", () => void this.endPartnership());

    const refreshButton = document.createElement("button");
    refreshButton.type = "button";
    refreshButton.textContent = "Refresh";
    refreshButton.addEventListener("click", () => void this.refresh());

    actions.append(this.inviteForm, this.cohabitButton, this.endButton, refreshButton);
    this.root.appendChild(actions);

    this.notice = document.createElement("div");
    this.notice.className = "social-relationships-notice";
    this.notice.setAttribute("role", "status");
    this.root.appendChild(this.notice);

    this.inviteForm.addEventListener("submit", (event) => {
      event.preventDefault();
      void this.invitePartner();
    });

    if (options.dock) {
      options.dock.addPanel({
        id: "relationships",
        label: "Social",
        icon: "heart",
        order: 30,
        content: this.root,
        onOpen: () => void this.refresh()
      });
    } else {
      options.root.appendChild(this.root);
    }
    void this.refresh();
  }

  async refresh(): Promise<void> {
    const token = this.options.token();
    if (token === null || this.disposed) return;

    try {
      const fetcher = this.options.fetchImpl ?? fetch;
      const response = await fetcher(`${this.options.apiBaseUrl}/api/social/relationships`, {
        headers: { Authorization: `Bearer ${token}` }
      });
      if (!response.ok) return;

      const status = (await response.json()) as PlayerRelationshipStatus;
      if (status === null || typeof status !== "object") return;
      this.status = status;
      this.render();
    } catch (error) {
      this.options.onError?.(
        error instanceof Error ? error.message : "Relationship status unavailable"
      );
    }
  }

  dispose(): void {
    this.disposed = true;
    this.root.remove();
  }

  private render(): void {
    if (this.disposed) return;
    const status = this.status;
    if (status === null) return;

    this.partner.textContent = status.partnerPlayerId === null
      ? "No partner"
      : `Partner: ${status.partnerName ?? status.partnerPlayerId}${
          status.sharesHousehold ? " (same household)" : ""
        }`;

    this.cohabitButton.disabled = status.partnerPlayerId === null || !status.canCohabit;
    this.endButton.disabled = status.partnerPlayerId === null;

    this.invitations.replaceChildren();
    for (const invitation of status.invitations) {
      if (invitation.status !== "Pending") continue;

      const row = document.createElement("li");
      row.className = "social-relationships-invitation";
      row.textContent = invitation.toPlayerId === status.playerId
        ? `${invitation.fromPlayerId} invites you to ${invitation.kind.toLowerCase()}`
        : `Invitation sent to ${invitation.toPlayerId} (${invitation.kind.toLowerCase()})`;

      if (invitation.toPlayerId === status.playerId) {
        const accept = document.createElement("button");
        accept.type = "button";
        accept.textContent = "Accept";
        accept.addEventListener("click", () => void this.respond(invitation.id, true));

        const decline = document.createElement("button");
        decline.type = "button";
        decline.textContent = "Decline";
        decline.addEventListener("click", () => void this.respond(invitation.id, false));

        row.append(accept, decline);
      }

      this.invitations.appendChild(row);
    }
  }

  private async invitePartner(): Promise<void> {
    const targetPlayerId = this.targetInput.value.trim();
    if (targetPlayerId === "") {
      this.setNotice("Enter a player id to invite.");
      return;
    }

    await this.sendAction("InvitePartner", { targetPlayerId });
    this.targetInput.value = "";
  }

  private async inviteCohabit(): Promise<void> {
    const partnerPlayerId = this.status?.partnerPlayerId;
    if (partnerPlayerId === null || partnerPlayerId === undefined) return;
    await this.sendAction("InviteCohabit", { targetPlayerId: partnerPlayerId });
  }

  private async endPartnership(): Promise<void> {
    const partnerPlayerId = this.status?.partnerPlayerId;
    if (partnerPlayerId === null || partnerPlayerId === undefined) return;
    await this.sendAction("EndPartnership", {
      partnerPlayerId,
      splitHousehold: true
    });
  }

  private async respond(invitationId: string, accept: boolean): Promise<void> {
    await this.sendAction("RespondToInvitation", { invitationId, accept });
  }

  private async sendAction(kind: string, payload: Record<string, unknown>): Promise<void> {
    try {
      const outcome = await this.options.sendAction(kind, payload);
      if (outcome.status !== "sent") {
        this.setNotice(outcome.message ?? "Action not accepted.");
        return;
      }
      this.setNotice("");
      await this.refresh();
    } catch (error) {
      this.setNotice(error instanceof Error ? error.message : "Action not accepted.");
    }
  }

  private setNotice(message: string): void {
    if (!this.disposed) this.notice.textContent = message;
  }
}
