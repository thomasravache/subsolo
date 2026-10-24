// @vitest-environment jsdom
import { render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import type { MyShow } from "@/api/shows";
import { MyShowsList } from "@/components/shows/MyShowsList";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), refresh: vi.fn() }),
}));

vi.mock("@/api/shows", () => ({ changeShowStatus: vi.fn() }));

/**
 * SPEC-0033 IT-03 (na tela) — "Meus shows" do dono: o show com `hidden: true` (de `GET /api/v1/me/shows`, Emenda 1)
 * mostra o selo "Oculto pela moderação" e não oferece "Editar" (nem "Adiar" e "Cancelar show", que o Events recusa com
 * 409 `events.show_hidden`); o show visível continua com as ações. O item vai com `as unknown as MyShow` enquanto o
 * cliente não tiver o campo.
 */
function show(id: string, title: string, hidden: boolean): MyShow {
  return {
    id,
    slug: `${title.toLowerCase().replaceAll(" ", "-")}-2026-11-14`,
    title,
    startsAt: "2026-11-15T01:00:00Z",
    city: "curitiba",
    timeZone: "America/Sao_Paulo",
    venueName: "Porão do Rock",
    status: "scheduled",
    flyerThumbUrl: "/media/flyers/abc-thumb.webp",
    organizerProfileId: "3f1c2b8e-6a4d-4e7f-9b21-8d5c0e9a1f42",
    hidden,
  } as unknown as MyShow;
}

describe("show oculto em Meus shows", () => {
  it("SPEC-0033:IT-03 o show oculto tem o selo e não tem Editar; o visível segue igual", () => {
    render(
      <MyShowsList
        upcoming={[
          show("0b7e4a9c-2d31-4f58-a6e0-5c9f1b3d7e21", "Noite Oculta", true),
          show("1c8f5b0d-3e42-4a69-b7f1-6d0a2c4e8f32", "Noite Visível", false),
        ]}
        past={[]}
        cities={[{ slug: "curitiba", name: "Curitiba" }]}
      />,
    );

    const hidden = screen.getByRole("article", { name: "Noite Oculta" });
    expect(hidden.textContent).toMatch(/oculto pela moderação/i);
    expect(within(hidden).queryByRole("link", { name: /editar/i })).toBeNull();
    expect(within(hidden).queryByRole("button", { name: /adiar/i })).toBeNull();
    expect(
      within(hidden).queryByRole("button", { name: /cancelar show/i }),
    ).toBeNull();

    const visible = screen.getByRole("article", { name: "Noite Visível" });
    expect(visible.textContent).not.toMatch(/oculto pela moderação/i);
    expect(within(visible).getByRole("link", { name: /editar/i })).toBeTruthy();
  });
});
