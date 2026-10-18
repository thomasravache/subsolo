// @vitest-environment jsdom
import { act, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { ShowForEdit } from "@/api/shows";
import { updateShow } from "@/api/shows";
import { ShowForm } from "@/components/shows/ShowForm";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), refresh: vi.fn() }),
}));

vi.mock("@/api/profiles", () => ({
  searchClaimableProfiles: vi.fn(async () => ({ ok: true, value: [] })),
}));

vi.mock("@/api/shows", () => ({
  uploadFlyer: vi.fn(),
  createShow: vi.fn(),
  updateShow: vi.fn(async () => ({
    ok: false,
    code: "events.invalid_show",
    status: 400,
    fields: {
      "Lineup[1]": ["Banda não encontrada. Escolha outra ou escreva o nome."],
    },
  })),
}));

/**
 * SPEC-0024 H23-07 (revisão da SPEC-0023) — no "Editar show", quando a API recusa a line-up
 * (`events.invalid_show` em `Lineup[1]`), a mensagem aparece e cada campo de banda liga o erro pelo próprio
 * `aria-describedby` (não só o `fieldset`), junto com o `aria-invalid`.
 */
const show: ShowForEdit = {
  id: "3f1c2b8e-6a4d-4e7f-9b21-8d5c0e9a1f42",
  slug: "noite-da-peste-ii-2026-11-14",
  organizerProfileId: "0b7e4a9c-2d31-4f58-a6e0-5c9f1b3d7e21",
  organizerName: "Peste Produções",
  title: "Noite da Peste II",
  city: "cidade-exemplo",
  startsLocal: "2026-11-14T22:00",
  venue: { profileId: null, name: "Porão do Rock" },
  lineup: [
    { profileId: null, name: "Cova Rasa" },
    { profileId: null, name: "Necrocorpo" },
  ],
  styles: ["death"],
  price: "R$ 30",
  ticketUrl: null,
  flyerId: "a".repeat(64),
  flyerThumbUrl: `/media/flyers/${"a".repeat(64)}-thumb.webp`,
  status: "scheduled",
};

describe("erro da line-up", () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it("SPEC-0024:H23-07 cada campo de banda liga o erro da line-up por aria-describedby", async () => {
    render(
      <ShowForm
        mode="edit"
        cities={[{ slug: "cidade-exemplo", name: "Cidade Exemplo" }]}
        styles={[{ slug: "death", name: "death" }]}
        organizers={[
          {
            id: show.organizerProfileId,
            name: "Peste Produções",
            city: "cidade-exemplo",
          },
        ]}
        isAdmin={false}
        show={show}
      />,
    );

    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: /^salvar/i }));
    });

    expect(updateShow).toHaveBeenCalledTimes(1);
    const message = screen.getByText(
      "Banda não encontrada. Escolha outra ou escreva o nome.",
    );
    expect(message.id, "a mensagem do erro sem id").toBeTruthy();
    const bands = screen.getAllByRole("textbox", { name: /^banda \d+$/i });
    expect(bands).toHaveLength(2);
    for (const band of bands) {
      expect(band.getAttribute("aria-invalid")).toBe("true");
      expect(
        (band.getAttribute("aria-describedby") ?? "").split(/\s+/),
        `${band.getAttribute("name")} sem o erro da line-up no aria-describedby`,
      ).toContain(message.id);
    }
  });
});
