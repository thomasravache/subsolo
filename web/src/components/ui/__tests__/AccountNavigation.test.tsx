// @vitest-environment jsdom
import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { BottomNav } from "@/components/ui/BottomNav";
import { Header } from "@/components/ui/Header";

/**
 * SPEC-0016 UT-04 — a navegação muda conforme a sessão: na navegação inferior (celular), a aba "Conta" leva a
 * `/entrar` sem usuário e a `/conta` com usuário, e o item ativo tem `aria-current="page"`; no cabeçalho (desktop), o
 * link é "Entrar" → `/entrar` sem usuário e "Conta" → `/conta` com usuário. Confere o caminho do destino: o link de
 * entrar pode levar `?returnTo=` com a página atual (para a volta depois do link mágico).
 */

const bottomNav = () =>
  screen.getByRole("navigation", { name: "Navegação inferior" });

const headerNav = () => screen.getByRole("navigation", { name: "Navegação" });

/** Caminho do destino do link (o `/entrar` pode levar `?returnTo=` da página atual). */
function pathOf(link: HTMLElement): string {
  return new URL(link.getAttribute("href") ?? "", "https://subsolo.test")
    .pathname;
}

describe("navegação conforme a sessão", () => {
  it("SPEC-0016:UT-04 sem usuário, a aba Conta leva a /entrar", () => {
    render(<BottomNav city="cidade-exemplo" current="agenda" />);

    const account = within(bottomNav()).getByRole("link", { name: /conta/i });
    expect(pathOf(account)).toBe("/entrar");
    expect(account.getAttribute("aria-current")).toBeNull();
  });

  it("SPEC-0016:UT-04 com usuário, a aba Conta leva a /conta", () => {
    render(<BottomNav city="cidade-exemplo" current="agenda" signedIn />);

    const account = within(bottomNav()).getByRole("link", { name: /conta/i });
    expect(pathOf(account)).toBe("/conta");
  });

  it("SPEC-0016:UT-04 em /conta, a aba Conta é o item ativo (aria-current=page)", () => {
    render(<BottomNav city="cidade-exemplo" current="conta" signedIn />);

    const nav = bottomNav();
    expect(
      within(nav)
        .getByRole("link", { name: /conta/i })
        .getAttribute("aria-current"),
    ).toBe("page");
    expect(
      within(nav)
        .getByRole("link", { name: /agenda/i })
        .getAttribute("aria-current"),
    ).toBeNull();
    expect(
      within(nav)
        .getAllByRole("link")
        .filter((link) => link.getAttribute("aria-current") === "page"),
    ).toHaveLength(1);
  });

  it("SPEC-0016:UT-04 na agenda, a Agenda continua sendo o item ativo", () => {
    render(<BottomNav city="cidade-exemplo" current="agenda" signedIn />);

    const nav = bottomNav();
    expect(
      within(nav)
        .getByRole("link", { name: /agenda/i })
        .getAttribute("aria-current"),
    ).toBe("page");
    expect(
      within(nav)
        .getByRole("link", { name: /conta/i })
        .getAttribute("aria-current"),
    ).toBeNull();
  });

  it("SPEC-0016:UT-04 cabeçalho sem usuário mostra Entrar → /entrar", () => {
    render(
      <Header city="cidade-exemplo" cities={[]} onCityChange={() => {}} />,
    );

    const nav = headerNav();
    expect(pathOf(within(nav).getByRole("link", { name: "Entrar" }))).toBe(
      "/entrar",
    );
    expect(within(nav).queryByRole("link", { name: "Conta" })).toBeNull();
  });

  it("SPEC-0016:UT-04 cabeçalho com usuário mostra Conta → /conta", () => {
    render(
      <Header
        city="cidade-exemplo"
        cities={[]}
        onCityChange={() => {}}
        signedIn
      />,
    );

    const nav = headerNav();
    expect(pathOf(within(nav).getByRole("link", { name: "Conta" }))).toBe(
      "/conta",
    );
    expect(within(nav).queryByRole("link", { name: "Entrar" })).toBeNull();
  });
});
