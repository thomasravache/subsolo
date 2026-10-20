// @vitest-environment jsdom
import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { BottomNav, type BottomNavProps } from "@/components/ui/BottomNav";

/**
 * SPEC-0027 E2E-01 (parte da navegação) — a navegação inferior (celular) tem as abas Agenda, Seguindo, Apoie e Conta,
 * nessa ordem (§6: "Buscar", fora do MVP, vira "Seguindo"; "Apoie" fica, decisão da SPEC-0011); "Seguindo" leva a
 * `/seguindo` com ou sem sessão (a página manda o anônimo para `/entrar?returnTo=/seguindo`) e é o item ativo
 * (`aria-current="page"`) na própria aba.
 */
const bottomNav = () =>
  screen.getByRole("navigation", { name: "Navegação inferior" });

const following = "seguindo" as BottomNavProps["current"];

describe("aba Seguindo na navegação inferior", () => {
  it("SPEC-0027:E2E-01 as abas são Agenda, Seguindo, Apoie e Conta, nessa ordem", () => {
    render(<BottomNav city="curitiba" current="agenda" signedIn />);

    const names = within(bottomNav())
      .getAllByRole("link")
      .map((link) => (link.textContent ?? "").trim().toLowerCase());
    expect(names).toEqual(["agenda", "seguindo", "apoie", "conta"]);
  });

  it.each([true, false])(
    "SPEC-0027:E2E-01 Seguindo leva a /seguindo (com sessão: %s)",
    (signedIn) => {
      render(
        <BottomNav city="curitiba" current="agenda" signedIn={signedIn} />,
      );

      const tab = within(bottomNav()).getByRole("link", { name: /seguindo/i });
      expect(tab.getAttribute("href")).toBe("/seguindo");
      expect(tab.getAttribute("aria-current")).toBeNull();
    },
  );

  it("SPEC-0027:E2E-01 em /seguindo, a aba Seguindo é o item ativo", () => {
    render(<BottomNav city="curitiba" current={following} signedIn />);

    const nav = bottomNav();
    expect(
      within(nav)
        .getByRole("link", { name: /seguindo/i })
        .getAttribute("aria-current"),
    ).toBe("page");
    expect(
      within(nav)
        .getByRole("link", { name: /agenda/i })
        .getAttribute("aria-current"),
    ).toBeNull();
  });
});
