// @vitest-environment jsdom
import { render, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { getProfileFormData, getProfileQueue } from "@/api/profiles-data";
import type { QueueItem } from "@/api/profiles";
import { getReportQueue } from "@/api/reports-data";

import { adminAccess } from "../admin-access";

vi.mock("../admin-access", () => ({ adminAccess: vi.fn() }));
vi.mock("@/api/profiles-data", () => ({
  getProfileQueue: vi.fn(),
  getProfileFormData: vi.fn(),
}));
vi.mock("@/api/reports-data", () => ({ getReportQueue: vi.fn() }));

/**
 * SPEC-0037 UT-02 (H33-03) — a página `/admin/fila` (SSR) com a leitura de denúncias (`GET /api/v1/admin/reports`)
 * falhando: a aba "Pedidos de perfil" (SPEC-0021) continua com os pedidos e só o painel de denúncias mostra o aviso de
 * indisponível (a página inteira não vira "Painel indisponível").
 */
const request: QueueItem = {
  requestId: "0b7e4a9c-2d31-4f58-a6e0-5c9f1b3d7e21",
  kind: "new",
  type: "band",
  name: "Cova Rasa",
  city: "cidade-exemplo",
  styles: ["death"],
  links: { instagram: null, bandcamp: null, metalArchives: null },
  contactOrProof: "@covarasa",
  requesterEmail: "banda@subsolo.test",
  createdAtUtc: "2026-10-20T12:00:00Z",
  profileId: null,
};

async function renderPage() {
  const { default: QueuePage } = await import("./page");
  return render(await QueuePage());
}

afterEach(() => {
  vi.resetAllMocks();
});

describe("página Fila do admin", () => {
  it("SPEC-0037:UT-02 com as denúncias indisponíveis, os pedidos de perfil seguem na tela e só o painel de denúncias avisa", async () => {
    vi.mocked(adminAccess).mockResolvedValue("ok");
    vi.mocked(getProfileQueue).mockResolvedValue([request]);
    vi.mocked(getProfileFormData).mockResolvedValue({
      cities: [{ slug: "cidade-exemplo", name: "Cidade Exemplo" }],
      styles: [],
    });
    vi.mocked(getReportQueue).mockResolvedValue(undefined);

    await renderPage();

    expect(
      screen.queryByRole("heading", { name: /painel indisponível/i }),
      "a falha das denúncias derrubou a página inteira",
    ).toBeNull();
    const profilesTab = screen.getByRole("tab", { name: /pedidos de perfil/i });
    expect(profilesTab.getAttribute("aria-selected")).toBe("true");
    const profilesPanel = document.getElementById(
      profilesTab.getAttribute("aria-controls") ?? "",
    );
    expect(profilesPanel, "sem o painel de pedidos de perfil").not.toBeNull();
    expect(within(profilesPanel!).getByText(/Cova Rasa/)).toBeDefined();

    const reportsTab = screen.getByRole("tab", { name: /denúncias/i });
    const reportsPanel = document.getElementById(
      reportsTab.getAttribute("aria-controls") ?? "",
    );
    expect(reportsPanel, "sem o painel de denúncias").not.toBeNull();
    expect(reportsPanel!.textContent ?? "").toMatch(/indisponíve/i);
    expect(profilesPanel!.textContent ?? "").not.toMatch(/indisponíve/i);
  });
});
