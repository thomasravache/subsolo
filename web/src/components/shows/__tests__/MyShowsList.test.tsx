// @vitest-environment jsdom
import type { ComponentProps } from "react";
import { render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import type { MyShow } from "@/api/shows";
import { MyShowsList } from "@/components/shows/MyShowsList";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), refresh: vi.fn() }),
}));

vi.mock("@/api/shows", () => ({ changeShowStatus: vi.fn() }));

/**
 * SPEC-0024 H23-06 (revisão da SPEC-0023) — "Meus shows" mostra a data e a hora no fuso que vem em cada item de
 * `GET /api/v1/me/shows` (`timeZone`, campo aditivo), sem a tabela de fusos lida da agenda pública: um show em Manaus
 * (UTC−4) às 02:00Z aparece às 22:00, não às 23:00 de São Paulo. As props vão soltas (`timeZones` vazio enquanto o
 * componente ainda o declarar) para o teste servir antes e depois da remoção.
 */
const manausShow = {
  id: "0b7e4a9c-2d31-4f58-a6e0-5c9f1b3d7e21",
  slug: "show-em-manaus-2026-11-14",
  title: "Show em Manaus",
  startsAt: "2026-11-15T02:00:00Z",
  city: "manaus",
  timeZone: "America/Manaus",
  venueName: "Porão do Norte",
  status: "scheduled",
  flyerThumbUrl: "/media/flyers/abc-thumb.webp",
  organizerProfileId: "3f1c2b8e-6a4d-4e7f-9b21-8d5c0e9a1f42",
} as unknown as MyShow;

describe("fuso em Meus shows", () => {
  it("SPEC-0024:H23-06 a hora do show sai no fuso do item (timeZone da cidade)", () => {
    const props = {
      upcoming: [manausShow],
      past: [],
      cities: [{ slug: "manaus", name: "Manaus" }],
      timeZones: {},
    } as unknown as ComponentProps<typeof MyShowsList>;

    render(<MyShowsList {...props} />);

    const card = screen.getByRole("article", { name: "Show em Manaus" });
    const time = card.querySelector("time");
    expect(time, "cartão sem <time>").not.toBeNull();
    expect(within(card).getByText(/22:00/)).toBeTruthy();
    expect(time?.textContent ?? "").not.toContain("23:00");
  });
});
